using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Skyboxes;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.App.Views;

public sealed class MainWindow : Window
{
    private const int MaxTerrainBrushUndoStrokes = 20;
    private const double DefaultTerrainBrushRadius = 512;
    private const double DefaultTerrainBrushStrength = 48;
    private const double DefaultTerrainBrushFeather = 68;
    private const float DefaultTerrainAddCopyOffset = 64f;
    private const float TerrainPlacedObjectLift = 8f;

    private static readonly string[] MobyCategoryOptions =
    [
        "All objects",
        "Confirmed IDs",
        "Observed IDs",
        "Mapped IDs",
        "Inferred IDs",
        "Gems",
        "Keys",
        "Treasure Objects",
        "Enemies",
        "Fodder",
        "Egg Thieves",
        "Chests",
        "Reward Chests",
        "Flight Targets",
        "Dragons",
        "Transport",
        "Scenery",
        "Portals",
        "Whirlwinds",
        "Controls",
        "Unknown",
        "Edited",
        "Needs ID",
        "Needs ID: Actors",
        "Needs ID: Scenery",
        "Needs ID: Controls",
        "Needs ID: Special"
    ];
    private static readonly TerrainColorOption[] TerrainColorPaletteOptions =
    [
        new("Grass shadow", ColorRgba.FromRgb(48, 88, 48)),
        new("Grass bright", ColorRgba.FromRgb(105, 193, 105)),
        new("Stone shadow", ColorRgba.FromRgb(82, 84, 78)),
        new("Stone bright", ColorRgba.FromRgb(164, 166, 156)),
        new("Sand shadow", ColorRgba.FromRgb(142, 104, 64)),
        new("Sand bright", ColorRgba.FromRgb(222, 184, 105)),
        new("Water deep", ColorRgba.FromRgb(36, 76, 152)),
        new("Water bright", ColorRgba.FromRgb(140, 200, 255)),
        new("Ice shadow", ColorRgba.FromRgb(92, 146, 184)),
        new("Ice bright", ColorRgba.FromRgb(206, 242, 255)),
        new("Lava shadow", ColorRgba.FromRgb(130, 30, 18)),
        new("Lava bright", ColorRgba.FromRgb(255, 136, 28)),
        new("Purple magic", ColorRgba.FromRgb(142, 90, 190)),
        new("Dark earth", ColorRgba.FromRgb(92, 64, 42))
    ];

    private EditorWorkspace _workspace;
    private LevelCatalog _catalog;
    private SkyboxCatalog _skyboxCatalog;
    private readonly TextTargetCatalog _textTargets = TextTargetCatalog.CreateDefault();
    private readonly EditorViewport _viewport = new();
    private readonly List<Button> _mapOrientationButtons = new();
    private readonly ComboBox _levelJumpBox = new();
    private readonly ListBox _levelList = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _levelTitle = new();
    private readonly TextBlock _levelDetails = new();
    private readonly TextBlock _gemCounterText = new();
    private readonly TextBlock _skyboxDetails = new();
    private readonly ComboBox _skyboxPresetBox = new();
    private readonly TextBox _skyboxCustomPaletteBox = new();
    private readonly TextBox _skyboxDiscImagePathBox = new();
    private readonly TextBox _skyboxWadAnalysisPathBox = new();
    private readonly TextBlock _levelTextDetails = new();
    private readonly TextBox _levelTextReplacementBox = new();
    private readonly ComboBox _exeStringPresetBox = new();
    private readonly TextBox _exeStringOriginalBox = new();
    private readonly TextBox _exeStringReplacementBox = new();
    private readonly TextBox _discImagePathBox = new();
    private readonly TextBlock _sourceDiscStatusText = new();
    private readonly TextBlock _selectionTitle = new();
    private readonly TextBlock _selectionDetails = new();
    private readonly TextBlock _linkedMobyHint = new();
    private readonly ListBox _linkedMobyList = new();
    private readonly ComboBox _mobyCategoryBox = new();
    private readonly TextBox _mobySearchBox = new();
    private readonly TextBlock _objectReadinessHint = new();
    private readonly TextBlock _identityPriorityHint = new();
    private Button? _identityPriorityLoadButton;
    private Button? _objectSelectedIdTestButton;
    private readonly TextBlock _mobyListHint = new();
    private readonly ListBox _mobyList = new();
    private readonly ComboBox _terrainBrushModeBox = new();
    private readonly CheckBox _terrainPlayableOnlyBox = new() { Content = "Only edit playable ground", IsChecked = true };
    private readonly CheckBox _terrainJoinSeamsBox = new() { Content = "Keep ground edges joined", IsChecked = true };
    private readonly TextBlock _terrainBrushSafetyText = new();
    private readonly TextBlock _terrainBrushHistoryText = new();
    private readonly TextBlock _terrainPointEditText = new();
    private readonly TextBlock _terrainReadinessHint = new();
    private readonly TextBlock _terrainExportReadinessText = new();
    private readonly TextBlock _terrainProofQueueHint = new();
    private readonly Slider _terrainBrushRadiusSlider = new() { Minimum = 96, Maximum = 1536, Value = DefaultTerrainBrushRadius };
    private readonly Slider _terrainBrushStrengthSlider = new() { Minimum = 8, Maximum = 256, Value = DefaultTerrainBrushStrength };
    private readonly Slider _terrainBrushFeatherSlider = new() { Minimum = 0, Maximum = 100, Value = DefaultTerrainBrushFeather };
    private readonly TextBlock _terrainBrushRadiusText = new();
    private readonly TextBlock _terrainBrushStrengthText = new();
    private readonly TextBlock _terrainBrushFeatherText = new();
    private readonly ComboBox _terrainSurfaceQuickBox = new();
    private readonly TextBlock _terrainMaterialActionText = new();
    private readonly TextBlock _terrainMaterialProofStatusText = new();
    private readonly TextBlock _terrainSurfaceQuickHint = new();
    private readonly TextBlock _objectActionHint = new();
    private Button? _objectAddButton;
    private Button? _objectRemoveButton;
    private Button? _objectEditButton;
    private Button? _objectCopyButton;
    private Button? _objectPasteButton;
    private Button? _objectUndoButton;
    private Button? _objectUndoRemoveButton;
    private Button? _toolbarCreateBinButton;
    private Button? _objectRestoreLevelButton;
    private readonly TextBlock _terrainActionHint = new();
    private Button? _terrainUndoButton;
    private Button? _terrainFindAddSourceButton;
    private Button? _terrainTaskMoveFaceButton;
    private Button? _terrainTaskSinglePointButton;
    private Button? _terrainTaskPaintFaceButton;
    private Button? _terrainTaskAddCopyButton;
    private Button? _terrainTaskRemoveFaceButton;
    private Button? _terrainTaskSaveButton;
    private readonly TextBlock _terrainTaskHint = new();
    private Button? _terrainPointPreviousButton;
    private Button? _terrainPointNextButton;
    private Button? _terrainPointPlayableButton;
    private Button? _terrainPointRaiseButton;
    private Button? _terrainPointLowerButton;
    private Button? _terrainPointXMinusButton;
    private Button? _terrainPointXPlusButton;
    private Button? _terrainPointYMinusButton;
    private Button? _terrainPointYPlusButton;
    private Button? _terrainPointResetButton;
    private Button? _terrainPointEditButton;
    private Button? _terrainUndoStrokeButton;
    private Button? _terrainRedoStrokeButton;
    private readonly ComboBox _identityBatchBox = new();
    private readonly TextBlock _identityBatchHint = new();
    private readonly TextBox _identityObservationNameBox = new();
    private readonly ComboBox _identityObservationSuggestionBox = new();
    private readonly TextBlock _identityObservationHint = new();
    private Control? _homeworldTextGroup;
    private Moby? _selectedMoby;
    private Moby? _lastRemovedMoby;
    private MobyClipboard? _mobyClipboard;
    private PendingMobyAdd? _pendingMobyAdd;
    private TerrainPolygon? _selectedTerrain;
    private int _selectedTerrainIndex = -1;
    private int _selectedTerrainPointIndex = -1;
    private TerrainLookClipboard? _terrainLookClipboard;
    private float? _activeFlattenTerrainZ;
    private TerrainProofTargetSelection? _activeTerrainProofTarget;
    private LevelDefinition? _currentLevel;
    private GeometryCandidate? _currentGeometry;
    private List<Moby> _currentMobys = new();
    private int _loadedMobyEdits;
    private int _loadedTerrainEdits;
    private string _terrainCacheHealthMessage = "";
    private HashSet<string> _terrainCollisionTriangleKeys = new(StringComparer.Ordinal);
    private string _terrainCollisionReadinessMessage = "Playable terrain collision: not checked";
    private int _terrainCollisionMatchedFaces;
    private Dictionary<TerrainPolygon, float[]>? _activeTerrainBrushUndo;
    private readonly List<IReadOnlyList<TerrainBrushStrokeFace>> _terrainBrushUndoHistory = new();
    private readonly List<IReadOnlyList<TerrainBrushStrokeFace>> _terrainBrushRedoHistory = new();
    private string _activeTerrainBrushVerb = "";
    private string _lastTerrainBrushSummary = "";
    private string _savedTerrainEditSignature = "";
    private string _savedMobyEditSignature = "";
    private IReadOnlyList<CustomTerrainTextureImport> _customTerrainTextures = Array.Empty<CustomTerrainTextureImport>();
    private MobyMetadataResult _mobyMetadata;
    private bool _syncingLevelSelection;
    private bool _handlingLevelSelection;
    private bool _levelJumpSelectionWired;
    private bool _levelListSelectionWired;
    private bool _buildingPortableCache;
    private bool _loadingLevel;
    private bool _allowCloseWithUnsavedTerrain;
    private bool _showingCloseUnsavedTerrainDialog;
    private bool _syncingExeStringPreset;
    private bool _syncingMobyList;
    private bool _syncingLinkedMobyList;
    private bool _syncingTerrainBrushSafety;
    private bool _syncingTerrainSurfaceQuick;
    private int _levelLoadRequestId;
    private IdentityBatchRestoreSet _activeIdentityBatchRestore = new(new Dictionary<int, IdentityBatchRestoreState>());
    private string _activeIdentityBatchName = "";
    private IdentityBatchCandidate? _identityPriorityCandidate;
    private readonly bool _releaseMode;

    public MainWindow()
    {
        _workspace = EditorWorkspace.Find();
        _releaseMode = IsReleaseMode(_workspace);
        _catalog = LevelCatalog.Load(_workspace.RootPath);
        _skyboxCatalog = SkyboxCatalog.Load(_workspace);
        _skyboxDiscImagePathBox.Text = DiscImageLocator.FindImage(_workspace);
        _skyboxWadAnalysisPathBox.Text = WadAnalysisLocator.Find(_workspace);
        _discImagePathBox.Text = DiscImageLocator.FindImage(_workspace);
        _viewport.SelectionChanged += (_, e) => ShowSelection(e);
        _viewport.MobyEditRequested += async (_, moby) => await EditMobyAsync(moby);
        _viewport.MobyUndoRequested += (_, moby) => UndoMoby(moby);
        _viewport.TerrainEditRequested += async (_, e) => await EditTerrainAsync(e.TerrainIndex, e.Terrain);
        _viewport.TerrainMoveRequested += (_, e) => MoveTerrainFromViewport(e);
        _viewport.TerrainRemoveRequested += (_, e) => StageTerrainRemovalFromViewport(e.TerrainIndex, e.Terrain);
        _viewport.TerrainLookCopyRequested += (_, e) => CopyTerrainLook(e.TerrainIndex, e.Terrain);
        _viewport.TerrainLookPasteRequested += async (_, e) => await PasteTerrainLookAsync(e.TerrainIndex, e.Terrain);
        _viewport.TerrainPointEditRequested += async (_, e) => await EditTerrainPointAsync(e.TerrainIndex, e.Terrain, e.PointIndex);
        _viewport.TerrainPointMoveRequested += (_, e) => MoveTerrainPointFromViewport(e);
        _viewport.TerrainBrushRequested += (_, e) => ApplyViewportTerrainBrush(e);
        _viewport.TerrainBrushAdjustmentRequested += (_, e) => AdjustTerrainBrushFromViewport(e);
        _viewport.TerrainBrushModeRequested += (_, e) => SelectTerrainBrushMode(e.Action);
        _viewport.TerrainBrushStrokeFinished += (_, _) => FinishTerrainBrushUndo();
        _viewport.MobyMoveRequested += (_, e) => MoveMobyFromViewport(e);
        _viewport.ObjectPlacementRequested += (_, e) => PlacePendingMobyAtViewport(e.ScreenPoint);
        _viewport.ObjectPlacementCanceled += (_, _) => CancelPendingMobyPlacement();
        _viewport.ObjectCopyRequested += (_, _) => CopySelectedMoby();
        _viewport.ObjectPasteRequested += (_, e) => PasteMobyClipboardAtViewport(e.ScreenPoint);
        _viewport.ViewModeChanged += (_, mode) => _statusText.Text = mode == ViewportViewMode.Fly3D
            ? "Fly 3D mode: use W/A/S/D, Q/E, arrow keys, right-drag, and scroll to fly around. Use Ctrl/Command+C and Ctrl/Command+V to copy and paste objects."
            : $"Map mode: {MapOrientationStatus()} Drag objects to move them. Use Ctrl/Command+C and Ctrl/Command+V to copy and paste objects.";
        _terrainBrushRadiusSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                RefreshTerrainBrushLabels();
                UpdateTerrainBrushPreview();
            }
        };
        _terrainBrushStrengthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                RefreshTerrainBrushLabels();
                UpdateTerrainBrushPreview();
            }
        };
        _terrainBrushFeatherSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                RefreshTerrainBrushLabels();
                UpdateTerrainBrushPreview();
            }
        };
        _terrainBrushModeBox.ItemsSource = TerrainBrushModeOption.All;
        _terrainBrushModeBox.SelectedItem = TerrainBrushModeOption.All[0];
        _terrainBrushModeBox.SelectionChanged += (_, _) => RefreshTerrainBrushMode();
        _terrainPlayableOnlyBox.PropertyChanged += (_, e) =>
        {
            if (!_syncingTerrainBrushSafety && e.Property == ToggleButton.IsCheckedProperty)
                RefreshTerrainBrushSafetyState(updateStatus: true);
        };
        RefreshTerrainBrushLabels();
        UpdateTerrainBrushPreview();
        RefreshTerrainBrushHistoryText();
        RefreshTerrainBrushSafetyState(updateStatus: false, defaultToSafe: true);
        RefreshTerrainBrushMode();
        RefreshSourceDiscStatus();

        Title = _releaseMode ? "Spyro Editor Release" : "Spyro Editor";
        Icon = LoadAppIcon();
        Width = 1320;
        Height = 860;
        MinWidth = 980;
        MinHeight = 640;
        Background = new SolidColorBrush(Color.FromRgb(238, 241, 244));

        Content = BuildLayout();
        SetMapOrientation(EditorUiDefaults.UseGameViewMapOrientation, announce: false);
        LoadLevels();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (_currentLevel == null || _currentGeometry != null || _currentMobys.Count > 0)
            return;

        try
        {
            await SelectLevelAsync(_currentLevel);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not load {_currentLevel.DisplayName}: {ex.Message}";
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowCloseWithUnsavedTerrain || !HasUnsavedTerrainEdits() && !HasUnsavedMobyEdits())
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        if (_showingCloseUnsavedTerrainDialog)
            return;

        _showingCloseUnsavedTerrainDialog = true;
        _ = ConfirmCloseWithUnsavedTerrainAsync();
    }

    private Control BuildLayout()
    {
        Grid root = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };

        root.Children.Add(BuildToolbar());

        Grid body = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(360))
            }
        };
        Grid.SetRow(body, 1);
        body.Children.Add(BuildViewportPanel());
        body.Children.Add(BuildEditorPanel());
        root.Children.Add(body);

        Border status = new()
        {
            Padding = new Thickness(14, 7),
            Background = new SolidColorBrush(Color.FromRgb(31, 36, 43)),
            Child = _statusText
        };
        _statusText.Foreground = new SolidColorBrush(Color.FromRgb(222, 230, 238));
        _statusText.FontSize = 12;
        Grid.SetRow(status, 2);
        root.Children.Add(status);

        return root;
    }

    private Control BuildToolbar()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 10)
        };

        WrapPanel tools = new() { Orientation = Orientation.Horizontal };
        tools.Children.Add(BuildToolbarLevelSelector());
        tools.Children.Add(NewButton("Fit View", () => _viewport.ResetView()));
        tools.Children.Add(NewButton("Map View", () => _viewport.SetViewMode(ViewportViewMode.Map)));
        tools.Children.Add(NewButton("Fly 3D", () => _viewport.SetViewMode(ViewportViewMode.Fly3D)));
        tools.Children.Add(NewMapOrientationButton());
        tools.Children.Add(NewAsyncButton("Open BIN/CUE", async () => await ChooseSourceDiscImageAsync()));
        _toolbarCreateBinButton = NewAsyncButton("Create BIN", async () => await CreateObjectTestBinAsync());
        StyleCreateBinButton(_toolbarCreateBinButton);
        tools.Children.Add(_toolbarCreateBinButton);
        tools.Children.Add(NewAsyncButton("Help", async () => await ShowHelpAsync()));

        _sourceDiscStatusText.FontSize = 12;
        _sourceDiscStatusText.FontWeight = FontWeight.SemiBold;
        _sourceDiscStatusText.VerticalAlignment = VerticalAlignment.Center;
        _sourceDiscStatusText.Margin = new Thickness(2, 0, 8, 8);
        tools.Children.Add(_sourceDiscStatusText);

        Control title = BuildBrandTitle();

        Grid dock = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        dock.Children.Add(title);
        Grid.SetColumn(tools, 1);
        dock.Children.Add(tools);

        border.Child = dock;
        return border;
    }

    private static Control BuildBrandTitle()
    {
        try
        {
            return new Image
            {
                Source = LoadAssetBitmap("Assets/Brand/spyro-editor-title.png"),
                Width = 220,
                Height = 46,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Debug.WriteLine(ex);
            return new TextBlock
            {
                Text = "Spyro Editor",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 18, 0)
            };
        }
    }

    private static Bitmap LoadAssetBitmap(string assetPath)
    {
        using Stream stream = AssetLoader.Open(new Uri($"avares://Spyro.Editor.App/{assetPath}"));
        return new Bitmap(stream);
    }

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Spyro.Editor.App/Assets/Brand/app-icon.png"));
            return new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    private Control BuildToolMenu(string placeholder, params ToolbarAction[] actions)
    {
        ComboBox box = new()
        {
            PlaceholderText = placeholder,
            ItemsSource = actions,
            MinHeight = 34,
            Width = 150,
            Margin = new Thickness(0, 0, 8, 8)
        };
        box.SelectionChanged += (_, _) => _ = RunToolbarActionAsync(box);
        return box;
    }

    private static async Task RunToolbarActionAsync(ComboBox box)
    {
        if (box.SelectedItem is not ToolbarAction action)
            return;

        box.SelectedItem = null;
        try
        {
            await action.Execute();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void ToggleMapOrientation()
    {
        SetMapOrientation(!_viewport.IsMapYFlipped, announce: true);
    }

    private void SetMapOrientation(bool gameView, bool announce)
    {
        _viewport.SetMapYFlipped(gameView);
        RefreshMapOrientationButtons();
        if (announce)
            _statusText.Text = gameView
                ? "Game view is on: the map orientation now matches the in-game direction."
                : "Raw capture view is on: the map orientation now matches the source cache direction.";
    }

    private string MapOrientationStatus()
    {
        return _viewport.IsMapYFlipped
            ? "game-view orientation is on."
            : "raw capture orientation is on.";
    }

    private Button NewMapOrientationButton()
    {
        Button button = NewButton("", ToggleMapOrientation);
        _mapOrientationButtons.Add(button);
        RefreshMapOrientationButtons();
        return button;
    }

    private void RefreshMapOrientationButtons()
    {
        bool gameView = _viewport.IsMapYFlipped;
        foreach (Button button in _mapOrientationButtons)
        {
            button.Content = gameView ? "Game View: On" : "Raw Capture View";
            button.Background = new SolidColorBrush(gameView
                ? Color.FromRgb(220, 238, 255)
                : Color.FromRgb(237, 241, 245));
            button.Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45));
        }
    }

    private Control BuildToolbarLevelSelector()
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 10, 8)
        };

        _levelJumpBox.MinHeight = 34;
        _levelJumpBox.Width = 190;
        _levelJumpBox.PlaceholderText = "Level";
        WireLevelJumpSelection();

        panel.Children.Add(_levelJumpBox);
        return panel;
    }

    private Control BuildLevelPanel()
    {
        Border border = PanelShell();
        border.BorderThickness = new Thickness(0, 0, 1, 0);

        Grid panel = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        panel.Children.Add(SectionLabel("Levels"));

        _levelJumpBox.MinHeight = 34;
        _levelJumpBox.PlaceholderText = "Jump to level";
        WireLevelJumpSelection();
        Grid.SetRow(_levelJumpBox, 1);
        panel.Children.Add(_levelJumpBox);

        WireLevelListSelection();
        _levelList.Background = Brushes.Transparent;
        _levelList.BorderThickness = new Thickness(0);
        _levelList.ClipToBounds = true;
        _levelList.MinHeight = 0;
        _levelList.VerticalAlignment = VerticalAlignment.Stretch;
        _levelList.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        _levelList.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Visible);
        _levelList.SetValue(ScrollViewer.AllowAutoHideProperty, false);
        _levelList.ItemTemplate = new FuncDataTemplate<LevelDefinition>((level, _) => new Border
        {
            Height = 30,
            Padding = new Thickness(6, 2),
            Child = new TextBlock
            {
                Text = level?.DisplayName ?? "",
                Foreground = new SolidColorBrush(Color.FromRgb(36, 45, 55)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        Grid.SetRow(_levelList, 2);
        panel.Children.Add(_levelList);

        border.Child = panel;
        return border;
    }

    private void WireLevelJumpSelection()
    {
        if (_levelJumpSelectionWired)
            return;

        _levelJumpSelectionWired = true;
        _levelJumpBox.SelectionChanged += (_, _) => _ = HandleLevelJumpSelectionAsync();
    }

    private void WireLevelListSelection()
    {
        if (_levelListSelectionWired)
            return;

        _levelListSelectionWired = true;
        _levelList.SelectionChanged += (_, _) => _ = HandleLevelListSelectionAsync();
    }

    private Control BuildViewportPanel()
    {
        Grid.SetColumn(_viewport, 0);
        return _viewport;
    }

    private Control BuildEditorPanel()
    {
        Border border = PanelShell();
        border.BorderThickness = new Thickness(1, 0, 0, 0);
        Grid.SetColumn(border, 1);

        StackPanel panel = new() { Spacing = 12 };
        if (_releaseMode)
        {
            panel.Children.Add(SectionLabel("Level"));
            panel.Children.Add(BuildLevelSummaryPanel());
            panel.Children.Add(NewDivider());
        }
        panel.Children.Add(BuildEditControls());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Objects"));
        panel.Children.Add(BuildMobyList());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Selection"));
        panel.Children.Add(BuildSelectionSummaryPanel());
        if (!_releaseMode)
        {
            panel.Children.Add(BuildCollapsedPanel("Observed ID / naming", BuildIdentityObservationPanel()));
            panel.Children.Add(NewDivider());
            panel.Children.Add(BuildAdvancedEditorPanel());
        }
        ShowSelection(ViewportSelectionChangedEventArgs.None);

        border.Child = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        return border;
    }

    private Control BuildAdvancedEditorPanel()
    {
        StackPanel panel = new() { Spacing = 10 };
        panel.Children.Add(SectionLabel("Map"));
        panel.Children.Add(BuildAdvancedMapPanel());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Object Tests"));
        panel.Children.Add(BuildAdvancedObjectExportPanel());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Identity Tests"));
        panel.Children.Add(BuildIdentityBatchPanel());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Level"));
        panel.Children.Add(BuildLevelSummaryPanel());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Skybox"));
        panel.Children.Add(BuildSkyboxPanel());
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("Original Disc"));
        panel.Children.Add(BuildDiscImagePanel());
        panel.Children.Add(NewDivider());
        _homeworldTextGroup = BuildHomeworldTextGroup();
        panel.Children.Add(_homeworldTextGroup);
        panel.Children.Add(NewDivider());
        panel.Children.Add(SectionLabel("UI Text"));
        panel.Children.Add(BuildUiTextPanel());

        return new Expander
        {
            Header = "Advanced",
            IsExpanded = false,
            Content = panel
        };
    }

    private Control BuildAdvancedMapPanel()
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        grid.Children.Add(NewAsyncButton("Open Workspace", async () => await OpenWorkspaceAsync()));
        AddGridButton(grid, NewMapOrientationButton(), 1, 0);
        AddGridButton(grid, NewAsyncButton("Import Editor Cache", async () => await ImportEditorCacheAsync()), 0, 1);
        AddGridButton(grid, NewButton("Open Cache Folder", OpenEditorCacheFolder), 1, 1);
        AddGridButton(grid, NewButton("Fit View", () => _viewport.ResetView()), 0, 2);
        AddGridButton(grid, NewButton("Map View", () => _viewport.SetViewMode(ViewportViewMode.Map)), 1, 2);
        Button fly = NewButton("Fly 3D", () => _viewport.SetViewMode(ViewportViewMode.Fly3D));
        Grid.SetColumnSpan(fly, 2);
        AddGridButton(grid, fly, 0, 3);
        Button cache = NewAsyncButton("Build Local Cache", async () => await BuildPortableCacheAsync());
        Grid.SetColumnSpan(cache, 2);
        AddGridButton(grid, cache, 0, 4);
        return grid;
    }

    private Control BuildAdvancedObjectExportPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(NewSmallNote("Candidate BINs are disposable tests for cross-level objects that are still guarded by the normal Create BIN button."));
        panel.Children.Add(NewAsyncButton("Create Candidate BIN", async () => await CreateObjectCandidateBinAsync()));
        panel.Children.Add(NewButton("Open Candidate Tests", OpenCandidateTestsLauncher));
        panel.Children.Add(NewAsyncButton("Record Test Result", async () => await RecordCandidateTestResultAsync()));
        return panel;
    }

    private Control BuildSelectionSummaryPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _selectionTitle.FontSize = 16;
        _selectionTitle.FontWeight = FontWeight.SemiBold;
        _selectionTitle.Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43));
        _selectionDetails.TextWrapping = TextWrapping.Wrap;
        _selectionDetails.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _selectionDetails.LineHeight = 20;
        _linkedMobyHint.TextWrapping = TextWrapping.Wrap;
        _linkedMobyHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _linkedMobyHint.FontSize = 12;
        panel.Children.Add(_selectionTitle);
        panel.Children.Add(_selectionDetails);
        panel.Children.Add(SectionLabel("Linked Objects"));
        panel.Children.Add(_linkedMobyHint);
        panel.Children.Add(BuildLinkedMobyList());
        return panel;
    }

    private Control BuildLevelSummaryPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _levelTitle.FontSize = 20;
        _levelTitle.FontWeight = FontWeight.SemiBold;
        _levelTitle.Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43));
        _gemCounterText.FontSize = 15;
        _gemCounterText.FontWeight = FontWeight.SemiBold;
        _gemCounterText.Foreground = new SolidColorBrush(Color.FromRgb(36, 94, 130));
        _gemCounterText.TextWrapping = TextWrapping.Wrap;
        _levelDetails.TextWrapping = TextWrapping.Wrap;
        _levelDetails.LineHeight = 20;
        _levelDetails.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        panel.Children.Add(_levelTitle);
        panel.Children.Add(_gemCounterText);
        panel.Children.Add(_levelDetails);
        return panel;
    }

    private Control BuildHomeworldTextGroup()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(SectionLabel("Homeworld Portal/Text"));
        panel.Children.Add(BuildLevelTextPanel());
        return panel;
    }

    private Control BuildDiscImagePanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _discImagePathBox.MinHeight = 34;
        _discImagePathBox.PlaceholderText = "Spyro the Dragon (USA).cue or .bin";
        panel.Children.Add(NewAsyncButton("Choose BIN/CUE", async () => await ChooseSourceDiscImageAsync()));
        panel.Children.Add(_discImagePathBox);
        panel.Children.Add(NewSmallNote("Used when creating patched test BIN/CUE files. You can choose either the CUE or the BIN; the editor remembers it for this workspace."));
        return panel;
    }

    private Control BuildMobyList()
    {
        StackPanel panel = new() { Spacing = 6 };

        _mobyCategoryBox.ItemsSource = MobyCategoryOptions;
        _mobyCategoryBox.SelectedIndex = 0;
        _mobyCategoryBox.MinHeight = 32;
        _mobyCategoryBox.SelectionChanged += (_, _) => RefreshMobyList(_selectedMoby);

        _mobySearchBox.PlaceholderText = "Search name, T#, family, type, state, gem, chest, thief, fodder, flight, unknown";
        _mobySearchBox.MinHeight = 32;
        _mobySearchBox.TextChanged += (_, _) => RefreshMobyList(_selectedMoby);

        _mobyListHint.TextWrapping = TextWrapping.Wrap;
        _mobyListHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _mobyListHint.FontSize = 12;

        _mobyList.MinHeight = 140;
        _mobyList.MaxHeight = 230;
        _mobyList.Background = Brushes.Transparent;
        _mobyList.BorderThickness = new Thickness(0);
        _mobyList.ItemTemplate = new FuncDataTemplate<Moby>((moby, _) => BuildMobyListItemControl(moby));
        _mobyList.SelectionChanged += (_, _) =>
        {
            if (_syncingMobyList || _mobyList.SelectedItem is not Moby moby)
                return;

            _viewport.SelectMoby(moby, true);
        };

        if (!_releaseMode)
        {
            panel.Children.Add(BuildObjectReadinessPanel());
            panel.Children.Add(BuildCollapsedPanel("ID review queue", BuildIdentityPriorityPanel()));
        }
        panel.Children.Add(BuildMobyQuickFilters());
        if (!_releaseMode)
            panel.Children.Add(_mobyCategoryBox);
        panel.Children.Add(_mobySearchBox);
        panel.Children.Add(_mobyListHint);
        panel.Children.Add(_mobyList);
        return panel;
    }

    private Control BuildObjectReadinessPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 248, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(207, 216, 226)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 2, 0, 4)
        };

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "Object Status",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(38, 48, 59))
        });

        _objectReadinessHint.TextWrapping = TextWrapping.Wrap;
        _objectReadinessHint.Foreground = new SolidColorBrush(Color.FromRgb(53, 63, 76));
        _objectReadinessHint.FontSize = 12;
        _objectReadinessHint.LineHeight = 17;
        panel.Children.Add(_objectReadinessHint);

        WrapPanel buttons = new() { Orientation = Orientation.Horizontal };
        buttons.Children.Add(NewAsyncButton("ID Families", async () => await ShowMobyIdentityFamiliesAsync()));
        buttons.Children.Add(NewButton("Needs ID", ShowQuestionableMobys));
        _objectSelectedIdTestButton = NewAsyncButton("ID Test", async () => await LoadSelectedMobyIdentityTestAsync());
        buttons.Children.Add(_objectSelectedIdTestButton);
        panel.Children.Add(buttons);

        border.Child = panel;
        RefreshObjectReadinessHint();
        return border;
    }

    private Control BuildIdentityPriorityPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 244, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(181, 207, 232)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 2, 0, 4)
        };

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "Next ID Target",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 71, 106))
        });

        _identityPriorityHint.TextWrapping = TextWrapping.Wrap;
        _identityPriorityHint.Foreground = new SolidColorBrush(Color.FromRgb(53, 72, 91));
        _identityPriorityHint.FontSize = 12;
        _identityPriorityHint.LineHeight = 17;

        WrapPanel buttons = new() { Orientation = Orientation.Horizontal };
        _identityPriorityLoadButton = NewButton("Load Priority Test", LoadDisplayedIdentityPriorityTest);
        buttons.Children.Add(_identityPriorityLoadButton);
        buttons.Children.Add(NewButton("Load Cluster Test", LoadNextClusterReviewTest));
        buttons.Children.Add(NewAsyncButton("Review Top Families", async () => await ShowMobyIdentityFamiliesAsync()));
        buttons.Children.Add(NewButton("Open Review Folder", OpenIdentityReleaseReviewFolder));
        buttons.Children.Add(NewButton("Open Unknown Links", OpenUnknownMobyTriageReport));
        buttons.Children.Add(NewButton("Open Proof Clusters", OpenUnknownMobyClusterMapReport));
        buttons.Children.Add(NewButton("Open Cluster Review", OpenUnknownMobyClusterReviewFolder));
        buttons.Children.Add(NewButton("Show Needs ID", ShowQuestionableMobys));

        panel.Children.Add(_identityPriorityHint);
        panel.Children.Add(buttons);
        border.Child = panel;
        RefreshIdentityPriorityHint();
        return border;
    }

    private Control BuildMobyQuickFilters()
    {
        WrapPanel filters = new() { Orientation = Orientation.Horizontal };
        filters.Children.Add(NewButton("All", () => SelectMobyCategory("All objects")));
        filters.Children.Add(NewButton("Treasure", () => SelectMobyCategory("Treasure Objects")));
        filters.Children.Add(NewButton("Gems", () => SelectMobyCategory("Gems")));
        filters.Children.Add(NewButton("Chests", () => SelectMobyCategory("Chests")));
        filters.Children.Add(NewButton("Enemies", () => SelectMobyCategory("Enemies")));
        filters.Children.Add(NewButton("Transport", () => SelectMobyCategory("Transport")));
        filters.Children.Add(NewButton("Flight", () => SelectMobyCategory("Flight Targets")));
        filters.Children.Add(NewButton("Edited", () => SelectMobyCategory("Edited")));
        if (!_releaseMode)
            filters.Children.Add(NewButton("Needs ID", ShowQuestionableMobys));
        return filters;
    }

    private void SelectMobyCategory(string category)
    {
        int index = Array.IndexOf(MobyCategoryOptions, category);
        if (index < 0)
            return;

        _mobyCategoryBox.SelectedIndex = index;
        RefreshMobyList(_selectedMoby);
    }

    private Control BuildIdentityObservationPanel()
    {
        StackPanel panel = new() { Spacing = 6 };
        _identityObservationNameBox.MinHeight = 32;
        _identityObservationNameBox.PlaceholderText = "Observed name, e.g. Egg thief or Wide tree";
        _identityObservationSuggestionBox.MinHeight = 32;
        _identityObservationSuggestionBox.PlaceholderText = "Suggested trusted names from this level";
        _identityObservationHint.TextWrapping = TextWrapping.Wrap;
        _identityObservationHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _identityObservationHint.FontSize = 12;

        panel.Children.Add(NewSmallNote("Select an object, type the exact in-game name you observed, then save it for this model family."));
        panel.Children.Add(_identityObservationNameBox);
        panel.Children.Add(_identityObservationSuggestionBox);
        WrapPanel buttons = new() { Orientation = Orientation.Horizontal };
        buttons.Children.Add(NewButton("Use Suggested Name", UseSuggestedIdentityObservationName));
        buttons.Children.Add(NewAsyncButton("Save Observed ID", async () => await SaveSelectedIdentityObservationAsync()));
        buttons.Children.Add(NewAsyncButton("Record Review Result", async () => await RecordSelectedIdentityReviewResultAsync()));
        buttons.Children.Add(NewAsyncButton("Record Cluster Result", async () => await RecordSelectedClusterReviewResultAsync()));
        panel.Children.Add(buttons);
        panel.Children.Add(_identityObservationHint);
        return panel;
    }

    private Control BuildIdentityBatchPanel()
    {
        StackPanel panel = new() { Spacing = 6 };
        _identityBatchBox.MinHeight = 32;
        _identityBatchBox.SelectionChanged += (_, _) => UpdateSelectedIdentityBatchHint();
        _identityBatchHint.TextWrapping = TextWrapping.Wrap;
        _identityBatchHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _identityBatchHint.FontSize = 12;

        panel.Children.Add(_identityBatchBox);
        panel.Children.Add(NewButton("Load Next Priority", LoadNextPriorityIdentityBatch));
        panel.Children.Add(NewAsyncButton("Load Selected ID Test", async () => await LoadSelectedMobyIdentityTestAsync()));
        panel.Children.Add(NewButton("Load Test Batch", LoadSelectedIdentityBatch));
        panel.Children.Add(NewButton("Clear Test Batch", ClearActiveIdentityBatch));
        panel.Children.Add(_identityBatchHint);
        return panel;
    }

    private Control BuildLinkedMobyList()
    {
        StackPanel panel = new() { Spacing = 6 };
        _linkedMobyList.MinHeight = 40;
        _linkedMobyList.MaxHeight = 150;
        _linkedMobyList.Background = Brushes.Transparent;
        _linkedMobyList.BorderThickness = new Thickness(0);
        _linkedMobyList.ItemTemplate = new FuncDataTemplate<LinkedMobyItem>((item, _) => new TextBlock
        {
            Text = item == null
                ? ""
                : $"{item.Relationship}: T{item.Moby.TrueIndex}  {item.Moby.DisplayLabel}",
            Foreground = new SolidColorBrush(Color.FromRgb(36, 45, 55)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4, 4)
        });
        _linkedMobyList.SelectionChanged += (_, _) =>
        {
            if (_syncingLinkedMobyList || _linkedMobyList.SelectedItem is not LinkedMobyItem item)
                return;

            _viewport.SelectMoby(item.Moby, true);
        };

        Button fitLinkedButton = NewButton("Fit Linked", FocusSelectedLinkedGroup);
        panel.Children.Add(_linkedMobyList);
        panel.Children.Add(fitLinkedButton);
        return panel;
    }

    private Control BuildSkyboxPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _skyboxDetails.TextWrapping = TextWrapping.Wrap;
        _skyboxDetails.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _skyboxDetails.LineHeight = 20;

        _skyboxPresetBox.ItemsSource = SkyboxPresetCatalog.Presets;
        _skyboxPresetBox.MinHeight = 34;

        _skyboxCustomPaletteBox.PlaceholderText = "#0B1038 #243A80 #AAB6D8";
        _skyboxCustomPaletteBox.MinHeight = 34;

        _skyboxDiscImagePathBox.PlaceholderText = "Spyro the Dragon (USA).bin";
        _skyboxDiscImagePathBox.MinHeight = 34;
        _skyboxWadAnalysisPathBox.PlaceholderText = "spyro-wad-analysis.json";
        _skyboxWadAnalysisPathBox.MinHeight = 34;

        panel.Children.Add(_skyboxDetails);
        panel.Children.Add(_skyboxPresetBox);
        panel.Children.Add(_skyboxCustomPaletteBox);
        panel.Children.Add(_skyboxDiscImagePathBox);
        panel.Children.Add(_skyboxWadAnalysisPathBox);
        panel.Children.Add(NewAsyncButton("Save Skybox Plan", async () => await SaveSkyboxPlanAsync()));
        panel.Children.Add(NewAsyncButton("Create Skybox CUE", async () => await CreateSkyboxCueAsync()));
        return panel;
    }

    private Control BuildLevelTextPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _levelTextDetails.TextWrapping = TextWrapping.Wrap;
        _levelTextDetails.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _levelTextDetails.LineHeight = 20;

        _levelTextReplacementBox.MinHeight = 34;
        _levelTextReplacementBox.PlaceholderText = "Replacement text";
        panel.Children.Add(_levelTextDetails);
        panel.Children.Add(_levelTextReplacementBox);
        panel.Children.Add(NewAsyncButton("Save Lettering Plan", async () => await SaveLevelTextPlanAsync()));
        panel.Children.Add(NewAsyncButton("Create Lettering CUE", async () => await CreateLevelTextCueAsync()));
        return panel;
    }

    private Control BuildUiTextPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        _exeStringPresetBox.MinHeight = 34;
        _exeStringPresetBox.PlaceholderText = "UI text preset";
        _exeStringPresetBox.ItemsSource = ExeStringPreset.Known;
        _exeStringPresetBox.SelectionChanged += (_, _) =>
        {
            if (_syncingExeStringPreset || _exeStringPresetBox.SelectedItem is not ExeStringPreset preset)
                return;

            _exeStringOriginalBox.Text = preset.OriginalText;
            _exeStringReplacementBox.Text = preset.ReplacementText;
        };
        _exeStringOriginalBox.MinHeight = 34;
        _exeStringOriginalBox.Text = "ENTERING %s...";
        _exeStringReplacementBox.MinHeight = 34;
        _exeStringReplacementBox.Text = "LOADING %s...";
        _exeStringPresetBox.SelectedItem = ExeStringPreset.Known[0];
        panel.Children.Add(_exeStringPresetBox);
        panel.Children.Add(_exeStringOriginalBox);
        panel.Children.Add(_exeStringReplacementBox);
        panel.Children.Add(NewAsyncButton("Save UI Text Plan", async () => await SaveExeStringPlanAsync()));
        panel.Children.Add(NewAsyncButton("Create UI Text CUE", async () => await CreateExeStringCueAsync()));
        LoadSavedExeStringPlan();
        return panel;
    }

    private void LoadLevels()
    {
        _levelList.ItemsSource = _catalog.Levels;
        _levelJumpBox.ItemsSource = _catalog.Levels;
        _statusText.Text = $"Loaded {_catalog.Levels.Count} levels from {_workspace.RootPath}";
        LevelDefinition? initialLevel = _catalog.FindByKey("stonehill") ?? _catalog.Levels.FirstOrDefault();
        SyncLevelPickers(initialLevel);
        _currentLevel = initialLevel;
        _levelTitle.Text = initialLevel?.DisplayName ?? "No level selected";
        _viewport.EmptyMessage = initialLevel == null
            ? "Open a Spyro workspace or BIN/CUE to start editing."
            : $"Loading {initialLevel.DisplayName}...";
    }

    private async Task HandleLevelJumpSelectionAsync()
    {
        try
        {
            await SelectLevelFromPickerAsync(_levelJumpBox.SelectedItem as LevelDefinition);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            SyncLevelPickers(_currentLevel);
            _statusText.Text = $"Could not change levels: {ex.Message}";
        }
    }

    private async Task HandleLevelListSelectionAsync()
    {
        try
        {
            await SelectLevelFromPickerAsync(_levelList.SelectedItem as LevelDefinition);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            SyncLevelPickers(_currentLevel);
            _statusText.Text = $"Could not change levels: {ex.Message}";
        }
    }

    private async Task SelectLevelFromPickerAsync(LevelDefinition? level)
    {
        if (_syncingLevelSelection || _handlingLevelSelection)
            return;
        if (_buildingPortableCache)
        {
            SyncLevelPickers(_currentLevel);
            _statusText.Text = "Still building level maps from the selected BIN/CUE. Wait for that to finish before changing levels.";
            return;
        }
        if (_loadingLevel)
        {
            SyncLevelPickers(_currentLevel);
            _statusText.Text = "Still loading the current level. Try changing levels again in a moment.";
            return;
        }

        _handlingLevelSelection = true;
        try
        {
            if (level == null)
                return;

            if (_currentLevel != null && string.Equals(level.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            {
                SyncLevelPickers(_currentLevel);
                return;
            }

            if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(level))
            {
                SyncLevelPickers(_currentLevel);
                return;
            }
            if (!await PersistCurrentMobyEditsBeforeLeavingLevelAsync(level))
            {
                SyncLevelPickers(_currentLevel);
                return;
            }

            _loadingLevel = true;
            RefreshLevelSelectionAvailability();
            _statusText.Text = $"Loading {level.DisplayName}...";
            await Task.Yield();

            SyncLevelPickers(level);
            await SelectLevelAsync(level);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            SyncLevelPickers(_currentLevel);
            _statusText.Text = $"Could not open that level: {ex.Message}";
        }
        finally
        {
            _loadingLevel = false;
            _handlingLevelSelection = false;
            RefreshLevelSelectionAvailability();
        }
    }

    private async Task SelectAdjacentLevelAsync(int delta)
    {
        if (_catalog.Levels.Count == 0)
            return;
        if (_buildingPortableCache || _loadingLevel)
        {
            _statusText.Text = _buildingPortableCache
                ? "Still building level maps from the selected BIN/CUE. Wait for that to finish before changing levels."
                : "Still loading the current level. Try changing levels again in a moment.";
            return;
        }

        LevelDefinition? selected = _currentLevel ?? _levelList.SelectedItem as LevelDefinition ?? _levelJumpBox.SelectedItem as LevelDefinition;
        int index = -1;
        if (selected != null)
        {
            for (int i = 0; i < _catalog.Levels.Count; i++)
            {
                if (ReferenceEquals(_catalog.Levels[i], selected))
                {
                    index = i;
                    break;
                }
            }
        }

        int nextIndex = Math.Clamp(index + delta, 0, _catalog.Levels.Count - 1);
        LevelDefinition next = _catalog.Levels[nextIndex];
        if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(next))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }
        if (!await PersistCurrentMobyEditsBeforeLeavingLevelAsync(next))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }

        _loadingLevel = true;
        RefreshLevelSelectionAvailability();
        try
        {
            _statusText.Text = $"Loading {next.DisplayName}...";
            await Task.Yield();
            SyncLevelPickers(next);
            await SelectLevelAsync(next);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            SyncLevelPickers(_currentLevel);
            _statusText.Text = $"Could not open that level: {ex.Message}";
        }
        finally
        {
            _loadingLevel = false;
            RefreshLevelSelectionAvailability();
        }
    }

    private async Task<bool> ConfirmLeaveLevelWithUnsavedTerrainAsync(LevelDefinition nextLevel)
    {
        if (_currentLevel == null || !HasUnsavedTerrainEdits())
            return true;
        if (string.Equals(nextLevel.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            return true;

        UnsavedTerrainDecision decision = await ShowUnsavedTerrainDialogAsync(
            "Save terrain before changing levels?",
            $"{_currentLevel.DisplayName} has unsaved terrain edits. Save them before opening {nextLevel.DisplayName}, leave without saving, or stay on this level.");
        return await ApplyUnsavedTerrainDecisionAsync(decision, "changing levels");
    }

    private async Task<bool> PersistCurrentMobyEditsBeforeLeavingLevelAsync(LevelDefinition nextLevel)
    {
        if (_currentLevel == null || !HasUnsavedMobyEdits())
            return true;
        if (string.Equals(nextLevel.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            return true;

        return await PersistCurrentMobyEditsBeforeActionAsync("changing levels");
    }

    private async Task<bool> PersistCurrentMobyEditsBeforeActionAsync(string actionLabel)
    {
        if (_currentLevel == null || !HasUnsavedMobyEdits())
            return true;

        try
        {
            int saved = await PersistCurrentMobyEditsAsync();
            _statusText.Text = $"Saved {saved} object edit(s) for {_currentLevel.DisplayName}.";
            return true;
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not save object edits before {actionLabel}: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> ConfirmLeaveWorkspaceWithUnsavedTerrainAsync(string folderPath)
    {
        if (_currentLevel == null)
            return true;

        if (HasUnsavedTerrainEdits())
        {
            string destination = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(destination))
                destination = "the selected workspace";

            UnsavedTerrainDecision decision = await ShowUnsavedTerrainDialogAsync(
                "Save terrain before opening workspace?",
                $"{_currentLevel.DisplayName} has unsaved terrain edits. Save them before opening {destination}, leave without saving, or stay in this workspace.");
            if (!await ApplyUnsavedTerrainDecisionAsync(decision, "opening workspace"))
                return false;
        }

        return await PersistCurrentMobyEditsBeforeActionAsync("opening workspace");
    }

    private async Task ConfirmCloseWithUnsavedTerrainAsync()
    {
        try
        {
            if (_currentLevel == null || !HasUnsavedTerrainEdits() && !HasUnsavedMobyEdits())
            {
                _allowCloseWithUnsavedTerrain = true;
                Close();
                return;
            }

            bool canClose = true;
            if (HasUnsavedTerrainEdits())
            {
                UnsavedTerrainDecision decision = await ShowUnsavedTerrainDialogAsync(
                    "Save terrain before closing?",
                    $"{_currentLevel.DisplayName} has unsaved terrain edits. Save them before closing the editor, close without saving, or stay in the editor.");
                canClose = await ApplyUnsavedTerrainDecisionAsync(decision, "closing editor");
            }

            if (canClose && await PersistCurrentMobyEditsBeforeActionAsync("closing editor"))
            {
                _allowCloseWithUnsavedTerrain = true;
                Close();
            }
        }
        finally
        {
            _showingCloseUnsavedTerrainDialog = false;
        }
    }

    private async Task<bool> ApplyUnsavedTerrainDecisionAsync(UnsavedTerrainDecision decision, string actionLabel)
    {
        if (decision == UnsavedTerrainDecision.Cancel)
            return false;
        if (decision == UnsavedTerrainDecision.Save)
        {
            try
            {
                await SaveCurrentTerrainEditsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Could not save terrain before {actionLabel}: {ex.Message}";
                return false;
            }
        }

        return true;
    }

    private async Task<UnsavedTerrainDecision> ShowUnsavedTerrainDialogAsync(string heading, string message)
    {
        Window dialog = new()
        {
            Title = "Unsaved Terrain",
            Width = 500,
            Height = 245,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildUnsavedTerrainDialogContent(dialog, heading, message);
        return await dialog.ShowDialog<UnsavedTerrainDecision>(this);
    }

    private void SyncLevelPickers(LevelDefinition? level)
    {
        _syncingLevelSelection = true;
        try
        {
            _levelList.SelectedItem = level;
            _levelJumpBox.SelectedItem = level;
            if (level != null)
                _levelList.ScrollIntoView(level);
        }
        finally
        {
            _syncingLevelSelection = false;
        }
    }

    private void RefreshLevelSelectionAvailability()
    {
        bool enabled = !_buildingPortableCache && !_loadingLevel;
        _levelJumpBox.IsEnabled = enabled;
        _levelList.IsEnabled = enabled;
    }

    private static Control BuildUnsavedTerrainDialogContent(Window dialog, string heading, string message)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43))
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Stay Here");
        Button discard = NewButton("Leave Without Saving");
        Button save = NewButton("Save Terrain");
        cancel.Click += (_, _) => dialog.Close(UnsavedTerrainDecision.Cancel);
        discard.Click += (_, _) => dialog.Close(UnsavedTerrainDecision.Discard);
        save.Click += (_, _) => dialog.Close(UnsavedTerrainDecision.Save);
        buttons.Children.Add(cancel);
        buttons.Children.Add(discard);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task OpenWorkspaceAsync()
    {
        try
        {
            IStorageProvider? storage = StorageProvider;
            if (storage == null)
            {
                _statusText.Text = "Folder picker is not available in this environment.";
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Spyro editor workspace",
                AllowMultiple = false
            });

            string? folderPath = folders.FirstOrDefault()?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(folderPath))
                return;
            if (!await ConfirmLeaveWorkspaceWithUnsavedTerrainAsync(folderPath))
                return;

            _workspace = new EditorWorkspace(folderPath);
            _catalog = LevelCatalog.Load(_workspace.RootPath);
            _skyboxCatalog = SkyboxCatalog.Load(_workspace);
            _skyboxDiscImagePathBox.Text = DiscImageLocator.FindImage(_workspace);
            _skyboxWadAnalysisPathBox.Text = WadAnalysisLocator.Find(_workspace);
            _discImagePathBox.Text = DiscImageLocator.FindImage(_workspace);
            RefreshSourceDiscStatus();
            LoadSavedExeStringPlan();
            LoadLevels();
            _statusText.Text = $"Opened workspace: {_workspace.RootPath}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not open workspace: {ex.Message}";
        }
    }

    private async Task ChooseSourceDiscImageAsync()
    {
        try
        {
            IStorageProvider? storage = StorageProvider;
            if (storage == null)
            {
                _statusText.Text = "File picker is not available in this environment.";
                return;
            }

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose your Spyro CUE or BIN",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("PlayStation disc image")
                    {
                        Patterns = ["*.cue", "*.bin"]
                    },
                    FilePickerFileTypes.All
                ]
            });

            string? selectedPath = files.FirstOrDefault()?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            DiscImageSelection selection = DiscImageLocator.ConfigureImage(_workspace, selectedPath);
            _discImagePathBox.Text = selection.ImagePath;
            _skyboxDiscImagePathBox.Text = selection.ImagePath;
            RefreshSourceDiscStatus();

            if (!selection.ImageExists)
            {
                _statusText.Text = $"Selected {Path.GetFileName(selectedPath)}, but the matching BIN was not found. Keep the CUE and BIN in the same folder.";
                return;
            }

            string cueNote = selection.CueExists
                ? $" Matching CUE: {Path.GetFileName(selection.CuePath)}."
                : " No matching CUE was found, so patched output will use a simple generated CUE.";
            _statusText.Text = $"Using {Path.GetFileName(selection.ImagePath)} for Create BIN.{cueNote} Rebuilding level maps and objects from that disc now...";
            await BuildPortableCacheAsync(overwrite: true, triggeredByDiscSelection: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not open/use that BIN/CUE: {ex.Message}";
        }
    }

    private async Task ImportEditorCacheAsync()
    {
        try
        {
            IStorageProvider? storage = StorageProvider;
            if (storage == null)
            {
                _statusText.Text = "Folder picker is not available in this environment.";
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose editor-cache folder",
                AllowMultiple = false
            });

            string? selectedFolder = folders.FirstOrDefault()?.Path.LocalPath;
            if (string.IsNullOrWhiteSpace(selectedFolder) || !Directory.Exists(selectedFolder))
                return;

            string sourceCache = ResolveEditorCacheImportFolder(selectedFolder);
            if (!Directory.Exists(sourceCache))
            {
                _statusText.Text = "That folder does not contain editor-cache data. Choose the editor-cache folder or a folder that contains it.";
                return;
            }

            int sourceMobys = Directory.GetFiles(sourceCache, "*-mobys.json").Length;
            int sourceOverlays = Directory.GetFiles(sourceCache, "*-runtime-scene-editor-overlay.json").Length;
            if (sourceMobys == 0 && sourceOverlays == 0)
            {
                _statusText.Text = "That editor-cache folder has no level object or terrain map JSON files.";
                return;
            }

            string targetCache = Path.Combine(_workspace.RootPath, "editor-cache");
            if (PathsEqual(sourceCache, targetCache))
            {
                SelectLevel(_currentLevel ?? _levelJumpBox.SelectedItem as LevelDefinition);
                _statusText.Text = $"This workspace is already using that editor-cache: {sourceMobys} object cache(s), {sourceOverlays} terrain map(s).";
                return;
            }

            CopyDirectory(sourceCache, targetCache);
            SelectLevel(_currentLevel ?? _levelJumpBox.SelectedItem as LevelDefinition);
            _statusText.Text = $"Imported editor-cache: {sourceMobys} object cache(s), {sourceOverlays} terrain map(s).";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not import editor-cache: {ex.Message}";
        }
    }

    private void OpenEditorCacheFolder()
    {
        string targetCache = Path.Combine(_workspace.RootPath, "editor-cache");
        Directory.CreateDirectory(targetCache);
        try
        {
            OpenPath(targetCache);
            _statusText.Text = $"Opened editor-cache folder: {targetCache}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open editor-cache folder: {ex.Message}";
        }
    }

    private void SelectLevel(LevelDefinition? level)
    {
        if (level == null)
            return;

        _levelLoadRequestId++;
        ApplyLoadedLevel(level, LoadLevelData(level.Key));
    }

    private async Task SelectLevelAsync(LevelDefinition level)
    {
        int requestId = ++_levelLoadRequestId;
        LevelLoadData loaded = await Task.Run(() => LoadLevelData(level.Key));
        if (requestId != _levelLoadRequestId)
            return;

        ApplyLoadedLevel(level, loaded);
    }

    private void ApplyLoadedLevel(LevelDefinition level, LevelLoadData loaded)
    {
        _currentLevel = level;
        _levelTitle.Text = level.DisplayName;
        ClearTerrainBrushUndoHistory();

        _currentGeometry = loaded.Geometry;
        _terrainCacheHealthMessage = loaded.TerrainCacheHealthMessage;
        _loadedTerrainEdits = loaded.LoadedTerrainEdits;
        _terrainCollisionTriangleKeys = loaded.TerrainCollisionTriangleKeys;
        _terrainCollisionReadinessMessage = loaded.TerrainCollisionReadinessMessage;
        _terrainCollisionMatchedFaces = loaded.TerrainCollisionMatchedFaces;
        _savedTerrainEditSignature = BuildTerrainEditSignature(_currentGeometry);
        _currentMobys = loaded.Mobys;
        _loadedMobyEdits = loaded.LoadedMobyEdits;
        _savedMobyEditSignature = BuildMobyEditSignature(_currentMobys);
        _mobyMetadata = loaded.MobyMetadata;
        _viewport.TerrainPatchSafetyClassifier = ClassifyTerrainPatchSafety;
        RefreshTerrainBrushSafetyState(updateStatus: false, defaultToSafe: true);
        _activeIdentityBatchRestore = new IdentityBatchRestoreSet(new Dictionary<int, IdentityBatchRestoreState>());
        _activeIdentityBatchName = "";
        _customTerrainTextures = loaded.CustomTerrainTextures;
        _lastRemovedMoby = null;
        _viewport.Geometry = _currentGeometry;
        _viewport.Mobys = _currentMobys;
        RefreshMobyList();
        RefreshIdentityBatchList();
        RefreshIdentityPriorityHint();
        RefreshObjectReadinessHint();
        RefreshTerrainReadinessHint();
        RefreshIdentityObservationSuggestions(null);
        _viewport.EmptyMessage = BuildMissingDataMessage(level.Key);
        RefreshCurrentLevelDetails();
        RefreshTerrainProofQueueHint();
        UpdateLevelToolPanels(level);
        _statusText.Text = $"{level.DisplayName}: {_viewport.Geometry?.Polygons.Count ?? 0} terrain faces, {_viewport.Mobys.Count} mobys";
    }

    private int ApplyCustomTerrainTexturePreviews()
    {
        return CustomTerrainTexturePreview.Apply(_currentGeometry, _customTerrainTextures);
    }

    private async Task BuildPortableCacheAsync(bool overwrite = true, bool triggeredByDiscSelection = false)
    {
        if (_catalog.Levels.Count == 0)
        {
            _statusText.Text = "Open a workspace with spyro-level-catalog.json before building the cache.";
            return;
        }
        if (_buildingPortableCache)
        {
            _statusText.Text = "Already building level maps. Wait for that to finish before starting another cache build.";
            return;
        }

        _buildingPortableCache = true;
        RefreshLevelSelectionAvailability();
        _statusText.Text = triggeredByDiscSelection
            ? "Building level maps and objects from the selected Spyro disc..."
            : "Building portable cache from selected disc, copied RAM, and overlay files...";
        try
        {
            PortableEditorCacheResult result = await Task.Run(() =>
                PortableEditorCacheBuilder.BuildAsync(_workspace, _catalog, overwrite: overwrite, fastReuseExistingCache: !overwrite).GetAwaiter().GetResult());
            SelectLevel(_currentLevel ?? _levelJumpBox.SelectedItem as LevelDefinition);
            string objectNote = result.MobyCacheCount == 0
                ? " Object placement could not be decoded from the selected disc."
                : "";
            _statusText.Text = triggeredByDiscSelection
                ? result.ReusedExistingCache
                    ? $"Selected disc and reused existing level maps: {result.OverlayCacheCount}/{result.LevelCount} terrain map(s), {result.MobyCacheCount}/{result.LevelCount} object cache(s).{objectNote}"
                    : $"Selected disc and rebuilt level maps from that BIN/CUE: {result.OverlayCacheCount}/{result.LevelCount} terrain map(s), {result.MobyCacheCount}/{result.LevelCount} object cache(s).{objectNote}"
                : $"Built cache: {result.MobyCacheCount}/{result.LevelCount} object cache(s), {result.OverlayCacheCount}/{result.LevelCount} terrain map(s).{objectNote}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not build cache: {ex.Message}";
        }
        finally
        {
            _buildingPortableCache = false;
            RefreshLevelSelectionAvailability();
        }
    }

    private async Task SaveCurrentEditsAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before saving edits.";
            return;
        }

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        int mobyCount = await PersistCurrentMobyEditsAsync();
        int terrainCount = _currentGeometry == null
            ? 0
            : await TerrainEditStore.SaveAsync(terrainEditsPath, _currentGeometry.Polygons, _currentLevel.DisplayName);

        _loadedMobyEdits = mobyCount;
        _loadedTerrainEdits = terrainCount;
        _savedTerrainEditSignature = BuildTerrainEditSignature(_currentGeometry);
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        string customArt = _customTerrainTextures.Count > 0
            ? $"; custom terrain art {_customTerrainTextures.Count}"
            : "";
        _statusText.Text = $"Saved {mobyCount} object edit(s). Terrain: {BuildTerrainEditSummary()}{customArt}.";
    }

    private async Task<int> PersistCurrentMobyEditsAsync()
    {
        if (_currentLevel == null)
            return 0;

        string mobyEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-native-edits.json");
        _loadedMobyEdits = await MobyEditStore.SaveAsync(mobyEditsPath, _currentMobys, _currentLevel.DisplayName);
        _savedMobyEditSignature = BuildMobyEditSignature(_currentMobys);
        return _loadedMobyEdits;
    }

    private async Task SaveCurrentTerrainEditsAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before saving terrain.";
            return;
        }
        if (_currentGeometry == null)
        {
            _statusText.Text = "No terrain is loaded yet.";
            return;
        }

        await PersistCurrentTerrainEditsAsync();
        RefreshCurrentLevelDetails();
        _statusText.Text = $"Saved terrain edits: {BuildTerrainEditSummary()}.";
    }

    private async Task<int> PersistCurrentTerrainEditsAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
            return 0;

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        _loadedTerrainEdits = await TerrainEditStore.SaveAsync(terrainEditsPath, _currentGeometry.Polygons, _currentLevel.DisplayName);
        _savedTerrainEditSignature = BuildTerrainEditSignature(_currentGeometry);
        RefreshTerrainReadinessHint();
        return _loadedTerrainEdits;
    }

    private async Task RestoreCurrentLevelAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before restoring it.";
            return;
        }
        if (_loadingLevel)
        {
            _statusText.Text = "Still loading the current level. Try Restore Level again in a moment.";
            return;
        }

        Window dialog = new()
        {
            Title = "Restore Level",
            Width = 430,
            Height = 230,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildRestoreLevelDialogContent(dialog, _currentLevel);
        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        LevelDefinition level = _currentLevel;
        string levelKey = level.Key;
        string[] paths =
        [
            Path.Combine(_workspace.RootPath, $"{levelKey}-native-edits.json"),
            Path.Combine(_workspace.RootPath, $"{levelKey}-terrain-edits.json"),
            Path.Combine(_workspace.RootPath, $"{levelKey}-terrain-material-overrides.json"),
            CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, levelKey)
        ];

        int deleted = 0;
        foreach (string path in paths)
        {
            if (!File.Exists(path))
                continue;

            File.Delete(path);
            deleted++;
        }

        _selectedMoby = null;
        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _activeTerrainProofTarget = null;
        _lastRemovedMoby = null;
        _loadingLevel = true;
        RefreshLevelSelectionAvailability();
        _statusText.Text = $"Restoring {level.DisplayName}...";
        try
        {
            await Task.Yield();
            await SelectLevelAsync(level);
            _statusText.Text = $"Restored {level.DisplayName} from the original cache and cleared {deleted} saved edit file(s).";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not restore {level.DisplayName}: {ex.Message}";
        }
        finally
        {
            _loadingLevel = false;
            RefreshLevelSelectionAvailability();
        }
    }

    private async Task RestoreCurrentTerrainAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before restoring terrain.";
            return;
        }
        if (_loadingLevel)
        {
            _statusText.Text = "Still loading the current level. Try Restore Terrain again in a moment.";
            return;
        }

        Window dialog = new()
        {
            Title = "Restore Terrain",
            Width = 430,
            Height = 230,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildRestoreTerrainDialogContent(dialog, _currentLevel);
        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        LevelDefinition level = _currentLevel;
        string levelKey = level.Key;
        string[] paths =
        [
            Path.Combine(_workspace.RootPath, $"{levelKey}-terrain-edits.json"),
            Path.Combine(_workspace.RootPath, $"{levelKey}-terrain-material-overrides.json"),
            CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, levelKey)
        ];

        int deleted = 0;
        foreach (string path in paths)
        {
            if (!File.Exists(path))
                continue;

            File.Delete(path);
            deleted++;
        }

        Moby? selectedMoby = _selectedMoby != null && !_selectedMoby.IsRemoved ? _selectedMoby : null;
        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _activeTerrainProofTarget = null;
        ClearTerrainBrushUndoHistory();
        _loadingLevel = true;
        RefreshLevelSelectionAvailability();
        _statusText.Text = $"Restoring terrain for {level.DisplayName}...";
        try
        {
            await Task.Yield();
            RestoredTerrainLoadData loaded = await Task.Run(() =>
            {
                TerrainGeometryLoadData geometry = LoadGeometryData(levelKey);
                IReadOnlyList<CustomTerrainTextureImport> customTextures = CustomTerrainTextureStore.Load(_workspace.RootPath, levelKey);
                CustomTerrainTexturePreview.Apply(geometry.Geometry, customTextures);
                TerrainCollisionLoadData collision = LoadTerrainCollisionData(levelKey, geometry.Geometry);
                return new RestoredTerrainLoadData(geometry, collision, customTextures);
            });

            _currentGeometry = loaded.Geometry.Geometry;
            _terrainCacheHealthMessage = loaded.Geometry.TerrainCacheHealthMessage;
            _loadedTerrainEdits = loaded.Geometry.LoadedTerrainEdits;
            _savedTerrainEditSignature = BuildTerrainEditSignature(_currentGeometry);
            _customTerrainTextures = loaded.CustomTerrainTextures;
            _terrainCollisionTriangleKeys = loaded.Collision.TriangleKeys;
            _terrainCollisionMatchedFaces = loaded.Collision.MatchedFaces;
            _terrainCollisionReadinessMessage = loaded.Collision.ReadinessMessage;
            RefreshTerrainBrushSafetyState(updateStatus: false, defaultToSafe: true);
            _viewport.Geometry = _currentGeometry;
            if (selectedMoby != null)
                _viewport.SelectMoby(selectedMoby, false);
            else
                ShowSelection(ViewportSelectionChangedEventArgs.None);
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            _statusText.Text = $"Restored terrain for {level.DisplayName} and cleared {deleted} terrain edit file(s). Object edits were left alone.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            _statusText.Text = $"Could not restore terrain for {level.DisplayName}: {ex.Message}";
        }
        finally
        {
            _loadingLevel = false;
            RefreshLevelSelectionAvailability();
        }
    }

    private static Control BuildRestoreLevelDialogContent(Window dialog, LevelDefinition level)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"Restore {level.DisplayName} to original?",
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43))
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This clears saved object edits, terrain edits, material overrides, and custom terrain texture imports for this level, then reloads the original cached level.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button restore = NewButton("Restore Level");
        cancel.Click += (_, _) => dialog.Close(false);
        restore.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(restore);
        panel.Children.Add(buttons);
        return panel;
    }

    private static Control BuildRestoreTerrainDialogContent(Window dialog, LevelDefinition level)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"Restore {level.DisplayName} terrain?",
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43))
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This clears terrain geometry edits, material overrides, and custom terrain texture imports for this level only. Object edits stay untouched.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button restore = NewButton("Restore Terrain");
        cancel.Click += (_, _) => dialog.Close(false);
        restore.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(restore);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task ImportCustomTerrainTextureAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before importing terrain texture art.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which texture ID to replace.";
            return;
        }

        if (!await ConfirmSharedTerrainTextureEditAsync("import custom art over this shared terrain texture", "Import Shared Texture Art"))
            return;

        IStorageProvider? storage = StorageProvider;
        if (storage == null)
        {
            _statusText.Text = "File picker is not available in this environment.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Import art for texture ID {_selectedTerrain.TextureId}",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
                {
                    Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.gif", "*.tif", "*.tiff"]
                },
                FilePickerFileTypes.All
            ]
        });

        string? sourcePath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures");
        Directory.CreateDirectory(customDir);
        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        string stagedName = $"{SafeFilePart(_currentLevel.Key)}-texture-{_selectedTerrain.TextureId:000}-{DateTime.Now:yyyyMMdd-HHmmss}{extension.ToLowerInvariant()}";
        string stagedPath = Path.Combine(customDir, stagedName);
        File.Copy(sourcePath, stagedPath, true);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            _selectedTerrain.TextureId,
            stagedPath,
            Path.GetFileName(sourcePath),
            "both",
            64,
            "imported-image");

        RefreshCurrentLevelDetails();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        string exportScope = "Create Test BIN will include normal and close-detail texture patches for this level.";
        _statusText.Text = $"Imported custom art for texture ID {_selectedTerrain.TextureId}. {exportScope}";
    }

    private async Task CompareTerrainBehaviorRamPairAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before comparing terrain behavior RAM.";
            return;
        }

        IStorageProvider? storage = StorageProvider;
        if (storage == null)
        {
            _statusText.Text = "File picker is not available in this environment.";
            return;
        }

        string? beforePath = await PickRamDumpAsync(storage, $"Before terrain touch RAM for {_currentLevel.DisplayName}");
        if (string.IsNullOrWhiteSpace(beforePath))
            return;

        string? afterPath = await PickRamDumpAsync(storage, $"After terrain touch RAM for {_currentLevel.DisplayName}");
        if (string.IsNullOrWhiteSpace(afterPath))
            return;

        try
        {
            string surface = _selectedTerrain == null
                ? "terrain"
                : TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
            TerrainProofTargetSelection? proofTarget = ActiveTerrainProofTargetForSelection();
            string terrainEvent = BuildTerrainEventName(surface, proofTarget);
            IReadOnlyList<TerrainBehaviorFocusControl> focusControls = LoadTerrainBehaviorFocusControls(_currentLevel.Key);
            TerrainBehaviorRamPairReport report = TerrainBehaviorRamPairComparer.Compare(
                await File.ReadAllBytesAsync(beforePath),
                await File.ReadAllBytesAsync(afterPath),
                new TerrainBehaviorRamPairOptions(_currentLevel.Key, terrainEvent, focusControls));
            TerrainBehaviorRule? behaviorRule = null;
            if (_selectedTerrain != null)
            {
                TerrainBehaviorProofFile proofFile = await TerrainBehaviorProofStore.RecordObservationAsync(
                    _workspace.RootPath,
                    _currentLevel.Key,
                    _selectedTerrain.TextureId,
                    _selectedTerrain.Surface,
                    _selectedTerrain.RuntimeKey,
                    report);
                behaviorRule = proofFile.Rules
                    .Where(rule => rule.TextureId == _selectedTerrain.TextureId)
                    .OrderByDescending(rule => rule.Confidence == "proven" ? 2 : rule.Confidence == "observed" ? 1 : 0)
                    .ThenByDescending(rule => rule.ObservationCount)
                    .FirstOrDefault();
                if (_currentGeometry != null)
                    TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);

                await BuildAndWriteTerrainProofReportsAsync();
            }

            string outDir = Path.Combine(_workspace.RootPath, "_local", "terrain-behavior");
            Directory.CreateDirectory(outDir);
            string stem = $"{SafeFilePart(_currentLevel.Key)}-{SafeFilePart(terrainEvent)}-{DateTime.Now:yyyyMMdd-HHmmss}";
            string jsonPath = Path.Combine(outDir, $"{stem}.json");
            string markdownPath = Path.Combine(outDir, $"{stem}.md");
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, NewJsonOptions()));
            await File.WriteAllTextAsync(markdownPath, BuildTerrainBehaviorRamPairMarkdown(report, beforePath, afterPath, behaviorRule, proofTarget));

            _viewport.InvalidateVisual();
            if (_selectedTerrain != null)
                ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
            string ruleText = behaviorRule == null
                ? ""
                : $" Recorded {TerrainBehaviorClassifier.FormatBehavior(behaviorRule.Behavior).ToLowerInvariant()} for texture {behaviorRule.TextureId} ({behaviorRule.Confidence}) and refreshed proof targets.";
            string targetText = proofTarget == null
                ? ""
                : $" Proof target: {proofTarget.Target.CaptureKind} {SelectedProofTargetRole(proofTarget)}.";
            _statusText.Text = $"Terrain behavior comparison saved: {Path.GetFileName(markdownPath)}.{ruleText}{targetText} {report.Interpretation}";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not compare terrain RAM: {ex.Message}";
        }
    }

    private async Task<string?> PickRamDumpAsync(IStorageProvider storage, string title)
    {
        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("RAM dumps")
                {
                    Patterns = ["*.bin", "*.ram", "*.dump"]
                },
                FilePickerFileTypes.All
            ]
        });

        string? path = files.FirstOrDefault()?.Path.LocalPath;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private TerrainProofTargetSelection? ActiveTerrainProofTargetForSelection()
    {
        if (_selectedTerrain == null || _activeTerrainProofTarget == null)
            return null;

        string expectedRuntimeKey = ActiveProofTargetRuntimeKey(_activeTerrainProofTarget);
        if (string.Equals(_selectedTerrain.RuntimeKey, expectedRuntimeKey, StringComparison.OrdinalIgnoreCase))
            return _activeTerrainProofTarget;

        return null;
    }

    private static string ActiveProofTargetRuntimeKey(TerrainProofTargetSelection selection)
    {
        return selection.UseCompanion
            ? selection.Target.NearestSolidOrHazardRuntimeKey
            : selection.Target.RuntimeKey;
    }

    private static string SelectedProofTargetRole(TerrainProofTargetSelection selection)
    {
        return selection.UseCompanion ? "companion" : "target";
    }

    private static string BuildTerrainEventName(string surface, TerrainProofTargetSelection? proofTarget)
    {
        string normalizedSurface = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        if (string.IsNullOrWhiteSpace(normalizedSurface) || normalizedSurface == "unknown")
            normalizedSurface = "terrain";

        if (proofTarget == null)
            return normalizedSurface == "terrain" ? "terrain-touch" : $"{normalizedSurface}-touch";

        string captureKind = proofTarget.UseCompanion
            ? "solid-control"
            : proofTarget.Target.CaptureKind;
        return $"{normalizedSurface}-{captureKind}";
    }

    private IReadOnlyList<TerrainBehaviorFocusControl> LoadTerrainBehaviorFocusControls(string levelKey)
    {
        string evidencePath = Path.Combine(_workspace.RootPath, "_local", "smoke", "terrain-behavior-evidence.json");
        List<TerrainBehaviorFocusControl> controls = new();
        AddActiveProofTargetFocusControl(controls, levelKey);
        if (File.Exists(evidencePath))
        {
            try
            {
                using FileStream stream = File.OpenRead(evidencePath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (TryGetJsonArray(document.RootElement, "NearestControlSamples", "nearestControlSamples", out JsonElement samples))
                {
                    foreach (JsonElement sample in samples.EnumerateArray())
                    {
                        string sampleLevel = FirstJsonString(sample, "LevelKey", "levelKey");
                        if (!string.Equals(sampleLevel, levelKey, StringComparison.OrdinalIgnoreCase))
                            continue;

                        double distance = FirstJsonDouble(sample, 999999, "NearestHazardDistance", "nearestHazardDistance");
                        if (distance > 64)
                            continue;

                        string id = FirstJsonString(sample, "ControlId", "controlId");
                        if (!id.StartsWith("T", StringComparison.OrdinalIgnoreCase) || !int.TryParse(id[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trueIndex))
                            continue;

                        controls.Add(new TerrainBehaviorFocusControl(
                            TrueIndex: trueIndex,
                            Label: FirstJsonString(sample, "Label", "label"),
                            HazardSurface: FirstJsonString(sample, "NearestHazardSurface", "nearestHazardSurface"),
                            HazardTextureId: FirstJsonInt32(sample, -1, "NearestHazardTextureId", "nearestHazardTextureId"),
                            HazardRuntimeKey: FirstJsonString(sample, "NearestHazardRuntimeKey", "nearestHazardRuntimeKey"),
                            HazardDistance: distance,
                            HazardZDelta: FirstJsonDouble(sample, 0, "NearestHazardZDelta", "nearestHazardZDelta")));
                    }
                }
            }
            catch
            {
                controls.Clear();
            }
        }

        if (controls.Count == 0 && _selectedMoby?.VisualKind == MobyVisualKind.Control)
        {
            controls.Add(new TerrainBehaviorFocusControl(
                _selectedMoby.TrueIndex,
                _selectedMoby.DisplayLabel,
                _selectedTerrain?.Surface ?? "",
                _selectedTerrain?.TextureId ?? -1,
                _selectedTerrain?.RuntimeKey ?? "",
                null,
                null));
        }

        return controls
            .GroupBy(control => control.TrueIndex)
            .Select(group => group.OrderBy(control => control.HazardDistance ?? double.MaxValue).First())
            .OrderBy(control => control.HazardDistance ?? double.MaxValue)
            .ThenBy(control => control.TrueIndex)
            .Take(12)
            .ToList();
    }

    private void AddActiveProofTargetFocusControl(List<TerrainBehaviorFocusControl> controls, string levelKey)
    {
        TerrainProofTargetSelection? proofTarget = ActiveTerrainProofTargetForSelection();
        if (proofTarget == null || !string.Equals(proofTarget.Target.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase))
            return;

        string id = proofTarget.Target.NearestControlId;
        if (!id.StartsWith("T", StringComparison.OrdinalIgnoreCase) || !int.TryParse(id[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trueIndex))
            return;

        controls.Add(new TerrainBehaviorFocusControl(
            TrueIndex: trueIndex,
            Label: proofTarget.Target.NearestControlLabel,
            HazardSurface: proofTarget.Target.Surface,
            HazardTextureId: proofTarget.Target.TextureId,
            HazardRuntimeKey: proofTarget.Target.RuntimeKey,
            HazardDistance: proofTarget.Target.NearestControlDistance,
            HazardZDelta: proofTarget.Target.NearestControlZDelta));
    }

    private static string BuildTerrainBehaviorRamPairMarkdown(
        TerrainBehaviorRamPairReport report,
        string beforePath,
        string afterPath,
        TerrainBehaviorRule? behaviorRule,
        TerrainProofTargetSelection? proofTarget)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Terrain Behavior RAM Pair");
        builder.AppendLine();
        builder.AppendLine(report.Interpretation);
        builder.AppendLine();
        builder.AppendLine($"- Level: {report.LevelKey}");
        builder.AppendLine($"- Event: {report.TerrainEvent}");
        builder.AppendLine($"- Before: {Path.GetFileName(beforePath)}");
        builder.AppendLine($"- After: {Path.GetFileName(afterPath)}");
        builder.AppendLine($"- Decoded mobys: {report.Before.DecodedMobys} -> {report.After.DecodedMobys}");
        builder.AppendLine($"- Spyro fields: {report.SpyroFieldSource}");
        builder.AppendLine($"- Spyro health: {report.Before.Spyro.Health} -> {report.After.Spyro.Health}");
        builder.AppendLine($"- Spyro state: {report.Before.Spyro.StateHex}/{report.Before.Spyro.StateSubHex} -> {report.After.Spyro.StateHex}/{report.After.Spyro.StateSubHex}");
        builder.AppendLine($"- Spyro i-frames: {report.Before.Spyro.IFrames} -> {report.After.Spyro.IFrames}");
        if (proofTarget != null)
        {
            builder.AppendLine($"- Proof target: {proofTarget.Target.CaptureKind} / {SelectedProofTargetRole(proofTarget)}");
            builder.AppendLine($"- Target face: {proofTarget.Target.RuntimeKey}, texture {proofTarget.Target.TextureId}, {proofTarget.Target.Surface}");
            if (!string.IsNullOrWhiteSpace(proofTarget.Target.NearestSolidOrHazardRuntimeKey))
                builder.AppendLine($"- Companion face: {proofTarget.Target.NearestSolidOrHazardRuntimeKey}, distance {proofTarget.Target.NearestSolidOrHazardDistance:0.##}");
            builder.AppendLine($"- Target next step: {proofTarget.Target.NextCapture}");
        }
        if (behaviorRule != null)
        {
            builder.AppendLine($"- Recorded behavior: {TerrainBehaviorClassifier.FormatBehavior(behaviorRule.Behavior)} ({behaviorRule.Confidence})");
            builder.AppendLine($"- Behavior observations for texture {behaviorRule.TextureId}: {behaviorRule.ObservationCount}");
        }
        builder.AppendLine();
        builder.AppendLine("## Spyro Player Changes");
        builder.AppendLine();
        if (report.SpyroChanges.Count == 0)
        {
            builder.AppendLine("No named Spyro health/state/position fields changed.");
        }
        else
        {
            builder.AppendLine("| Field | Before | After |");
            builder.AppendLine("|---|---:|---:|");
            foreach (TerrainBehaviorFieldChange change in report.SpyroChanges)
                builder.AppendLine($"| {change.Field} | {change.Before} | {change.After} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Global Changes");
        builder.AppendLine();
        if (report.GlobalChanges.Count == 0)
        {
            builder.AppendLine("No known global pointer/counter changes.");
        }
        else
        {
            builder.AppendLine("| Field | Before | After |");
            builder.AppendLine("|---|---:|---:|");
            foreach (TerrainBehaviorFieldChange change in report.GlobalChanges)
                builder.AppendLine($"| {change.Field} | {change.Before} | {change.After} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Watched Ranges");
        builder.AppendLine();
        builder.AppendLine("| Range | Offset | Bytes changed | First changes |");
        builder.AppendLine("|---|---|---:|---|");
        foreach (ByteRangeDiff range in report.WatchedRanges)
        {
            string first = string.Join("; ", range.ChangedBytes.Take(6).Select(change => $"{change.Offset} {change.BeforeHex}->{change.AfterHex}"));
            builder.AppendLine($"| {range.Label} | {range.RamOffset} | {range.ChangedByteCount} | {first} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Focus Hazard Controls");
        builder.AppendLine();
        if (report.FocusControls.Count == 0)
        {
            builder.AppendLine("No focus controls were selected. Generate the terrain behavior evidence report or select a control moby before comparing.");
        }
        else
        {
            builder.AppendLine("| Control | Label | Hazard | Field changes | Main bytes | Special bytes |");
            builder.AppendLine("|---|---|---|---|---:|---:|");
            foreach (TerrainBehaviorFocusControlDiff control in report.FocusControls)
            {
                string fields = control.FieldChanges.Count == 0
                    ? "none"
                    : string.Join("; ", control.FieldChanges.Select(change => $"{change.Field}:{change.Before}->{change.After}"));
                string hazard = $"{control.Focus.HazardSurface} tex {control.Focus.HazardTextureId} {control.Focus.HazardRuntimeKey} distance {control.Focus.HazardDistance:0.##}";
                builder.AppendLine($"| T{control.Focus.TrueIndex} | {EscapeMarkdownCell(control.Focus.Label)} | {EscapeMarkdownCell(hazard)} | {EscapeMarkdownCell(fields)} | {control.MainRecordDiff.ChangedByteCount} | {control.SpecialDataDiff.ChangedByteCount} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Strongest Changed Clusters");
        builder.AppendLine();
        builder.AppendLine("| Offset | Bytes changed | First changes |");
        builder.AppendLine("|---|---:|---|");
        foreach (ChangedCluster cluster in report.ChangedClusters)
            builder.AppendLine($"| {cluster.RamOffset} | {cluster.ChangedByteCount} | {EscapeMarkdownCell(string.Join("; ", cluster.FirstChanges))} |");

        builder.AppendLine();
        builder.AppendLine("## Promotion Rule");
        builder.AppendLine();
        builder.AppendLine(report.NextRepeatabilityRule);
        return builder.ToString();
    }

    private async Task ShowTerrainProofSummaryAsync()
    {
        TerrainBehaviorProofReports reports = await BuildAndWriteTerrainProofReportsAsync();
        IReadOnlyList<TerrainProofSummaryItem> summaries = reports.Summary.Surfaces
            .OrderByDescending(item => string.Equals(item.LevelKey, _currentLevel?.Key, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => TerrainSummarySurfaceRank(item.Surface))
            .ThenBy(item => item.ProofStatusRank)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(TerrainProofSummaryItem.FromReport)
            .ToList();
        if (summaries.Count == 0)
        {
            _statusText.Text = "No terrain geometry cache found yet. Build the portable cache first.";
            return;
        }

        ListBox list = new()
        {
            ItemsSource = summaries,
            MinHeight = 420,
            ItemTemplate = new FuncDataTemplate<TerrainProofSummaryItem>((item, _) => new TextBlock
            {
                Text = item?.ListText ?? "",
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 112
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not TerrainProofSummaryItem item)
            {
                details.Text = "Select a surface row to see what still needs proof.";
                return;
            }

            details.Text =
                $"{item.DisplayName} / {TerrainMaterialClassifier.FormatSurface(item.Surface)}\n" +
                $"{item.FaceCount} face(s), {item.TextureCount} texture ID(s)\n" +
                $"Editor behavior: {item.BehaviorSummary}\n" +
                $"Proof state: {item.ProofSummary}\n" +
                $"{item.NextStep}";
        }

        list.SelectionChanged += (_, _) => RefreshDetails();
        list.SelectedIndex = 0;
        RefreshDetails();

        Window dialog = new()
        {
            Title = "Terrain Proof Summary",
            Width = 980,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainProofSummaryDialogContent(dialog, list, details);
        TerrainProofSummaryItem? selected = await dialog.ShowDialog<TerrainProofSummaryItem?>(this);
        if (selected != null)
            await SelectTerrainProofSummaryLevelAsync(selected);
    }

    private async Task RefreshTerrainProofReportsAsync()
    {
        TerrainBehaviorProofReports reports = await BuildAndWriteTerrainProofReportsAsync();
        int hazardTargets = reports.Targets.Targets.Count(target => target.CaptureKind == "hazard-touch");
        int solidTargets = reports.Targets.Targets.Count(target => target.CaptureKind == "solid-control");
        int proven = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank >= 3);
        int observed = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank == 2);
        int missing = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank == 0);
        RefreshTerrainReadinessHint();
        RefreshTerrainProofQueueHint();
        _statusText.Text = $"Terrain proof reports refreshed: {hazardTargets} hazard target(s), {solidTargets} solid-control target(s), {proven} proven, {observed} observed, {missing} missing.";
    }

    private void RefreshTerrainReadinessHint()
    {
        _terrainReadinessHint.Text = BuildTerrainReadinessHint();
        RefreshTerrainExportReadiness();
    }

    private string BuildTerrainReadinessHint()
    {
        if (_currentLevel == null)
            return "Choose a level to see terrain edit and behavior readiness.";

        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return "Terrain is not loaded yet. Build or open the portable cache before editing this level.";

        IReadOnlyDictionary<string, int> counts = TerrainMaterialClassifier.CountSurfaces(_currentGeometry);
        string surfaceSummary = counts.Count == 0
            ? "no mapped surfaces"
            : string.Join(", ", counts
                .OrderBy(pair => TerrainSummarySurfaceRank(pair.Key))
                .ThenByDescending(pair => pair.Value)
                .Take(5)
                .Select(pair => $"{TerrainMaterialClassifier.FormatSurface(pair.Key)} {pair.Value}"));
        int hazardFaces = _currentGeometry.Polygons.Count(face =>
            TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface) is "water" or "lava" or "ooze");
        int editedFaces = _currentGeometry.Polygons.Count(face => face.IsTerrainEdited);
        int riskyGeometryEdits = _currentGeometry.Polygons.Count(IsRiskyTerrainGeometryEdit);
        int structuralEdits = _currentGeometry.Polygons.Count(face => face.HasStructureEdit);
        var customUsage = GetCustomTerrainTextureUsage();
        string collision = _terrainCollisionReadinessMessage.Replace("Playable terrain collision: ", "", StringComparison.OrdinalIgnoreCase);
        string proof = BuildCurrentLevelTerrainProofReadiness();
        string patchReadiness = BuildCurrentLevelTerrainPatchReadiness();
        string structure = BuildCurrentLevelTerrainStructureReadiness();
        string coverage = BuildTerrainReadinessCoverageSummary(compact: true);
        string behaviorReadiness = BuildTerrainBehaviorReadinessBreakdown();
        string edits = editedFaces == 0
            ? "No terrain edits saved/staged yet."
            : $"{editedFaces} edited face(s); {riskyGeometryEdits} risky geometry edit(s); {structuralEdits} structural edit(s).";
        string texture = _customTerrainTextures.Count == 0
            ? "No custom terrain texture art staged."
            : customUsage.UnusedImports > 0
                ? $"{customUsage.ActiveImports} active custom texture import(s), {customUsage.UnusedImports} unused import(s) ignored by Create BIN."
                : $"{customUsage.ActiveImports} active custom texture import(s) staged.";

        return $"Terrain editing is enabled as a beta tool. Surfaces: {surfaceSummary}. Hazard-looking faces: {hazardFaces}. Collision: {collision}.\n{behaviorReadiness}\n{edits} {texture} {proof}\n{patchReadiness}\n{structure}\n{coverage}";
    }

    private string BuildCurrentLevelTerrainPatchReadiness()
    {
        if (_currentLevel == null)
            return "";

        TerrainGeometryReadiness? geometry = LoadCurrentTerrainGeometryReadiness();
        TerrainTextureReadiness? texture = LoadCurrentTerrainTextureReadiness();
        string geometryText = geometry == null
            ? "movement/point not generated"
            : $"movement/point {FormatTerrainGeometryReadiness(geometry)}";
        string textureText = texture == null
            ? "palette/texture not generated"
            : $"palette/texture {FormatTerrainTextureReadiness(texture)}";
        return $"Patch readiness: {geometryText}; {textureText}.";
    }

    private TerrainGeometryReadiness? LoadCurrentTerrainGeometryReadiness()
    {
        if (_currentLevel == null)
            return null;

        return LoadTerrainGeometryReadinessRows()
            .FirstOrDefault(row => string.Equals(row.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<TerrainGeometryReadiness> LoadTerrainGeometryReadinessRows()
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "cross-level-terrain-geometry", "terrain-geometry-readiness.json");
        if (!File.Exists(path))
            return [];

        try
        {
            List<TerrainGeometryReadiness> readiness = new();
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!TryGetJsonArray(document.RootElement, "Rows", "rows", out JsonElement rows))
                return [];

            foreach (JsonElement row in rows.EnumerateArray())
            {
                string levelKey = GetJsonString(row, "LevelKey");
                if (string.IsNullOrWhiteSpace(levelKey))
                    continue;

                readiness.Add(new TerrainGeometryReadiness(
                    levelKey,
                    GetJsonString(row, "LevelName", levelKey),
                    GetJsonString(row, "Status", "unknown"),
                    GetJsonString(row, "RuntimeKey"),
                    GetJsonString(row, "Edit"),
                    GetJsonInt32(row, "SourceSectorCount", 0),
                    GetJsonInt32(row, "MatchedSectorCount", 0),
                    GetJsonInt32(row, "PatchCount", 0),
                    GetJsonInt32(row, "VisualPatchCount", 0),
                    GetJsonBoolean(row, "HadCollisionMatch"),
                    GetJsonBoolean(row, "HasCollisionPatch"),
                    GetJsonString(row, "Notes")));
            }

            return readiness;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return [];
    }

    private TerrainTextureReadiness? LoadCurrentTerrainTextureReadiness()
    {
        if (_currentLevel == null)
            return null;

        return LoadTerrainTextureReadinessRows()
            .FirstOrDefault(row => string.Equals(row.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<TerrainTextureReadiness> LoadTerrainTextureReadinessRows()
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "cross-level-terrain-textures", "terrain-texture-readiness.json");
        if (!File.Exists(path))
            return [];

        try
        {
            List<TerrainTextureReadiness> readiness = new();
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!TryGetJsonArray(document.RootElement, "Rows", "rows", out JsonElement rows))
                return [];

            foreach (JsonElement row in rows.EnumerateArray())
            {
                string levelKey = GetJsonString(row, "LevelKey");
                if (string.IsNullOrWhiteSpace(levelKey))
                    continue;

                readiness.Add(new TerrainTextureReadiness(
                    levelKey,
                    GetJsonString(row, "LevelName", levelKey),
                    GetJsonString(row, "Status", "unknown"),
                    GetJsonString(row, "RuntimeKey"),
                    GetJsonInt32(row, "SourceTextureId", -1),
                    GetJsonInt32(row, "TargetTextureId", -1),
                    GetJsonInt32(row, "SourceSectorCount", 0),
                    GetJsonInt32(row, "MatchedSectorCount", 0),
                    GetJsonInt32(row, "TextureSwapPatchCount", 0),
                    GetJsonInt32(row, "CustomTexturePatchCount", 0),
                    GetJsonInt32(row, "CustomTextureImportCount", 0),
                    GetJsonInt32(row, "PreviewedFaceCount", 0),
                    GetJsonString(row, "DescriptorTiers"),
                    GetJsonString(row, "Notes")));
            }

            return readiness;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return [];
    }

    private static string FormatTerrainGeometryReadiness(TerrainGeometryReadiness readiness)
    {
        string sample = string.IsNullOrWhiteSpace(readiness.RuntimeKey)
            ? ""
            : $" on sample face {readiness.RuntimeKey}";
        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"full for captured data{sample}, visual + collision patches",
            "visual-only" => $"visual-only{sample}; collision still needs proof",
            "skipped" => $"needs capture/proof. {readiness.Notes}",
            _ => $"{readiness.Status}{sample}. {readiness.Notes}"
        };
    }

    private static string FormatTerrainTextureReadiness(TerrainTextureReadiness readiness)
    {
        string sample = string.IsNullOrWhiteSpace(readiness.RuntimeKey)
            ? ""
            : $" on sample face {readiness.RuntimeKey}";
        string swap = readiness.SourceTextureId >= 0 && readiness.TargetTextureId >= 0
            ? $" {readiness.SourceTextureId}->{readiness.TargetTextureId}"
            : "";
        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"full for captured data{sample}: texture swap{swap} + custom imported palette patches",
            "texture-swap-only" => $"texture swap only{sample}{swap}; custom imported palette patch still needs proof",
            "skipped" => $"needs capture/proof. {readiness.Notes}",
            _ => $"{readiness.Status}{sample}{swap}. {readiness.Notes}"
        };
    }

    private string BuildCurrentLevelTerrainStructureReadiness()
    {
        if (_currentLevel == null)
            return "";

        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null)
            return "Structure readiness: not generated yet. Run the terrain smoke/readiness pass before trusting add-copy or remove-face writeback on this level.";

        return $"Structure readiness: {FormatTerrainStructureReadiness(readiness)}";
    }

    private TerrainStructureReadiness? LoadCurrentTerrainStructureReadiness()
    {
        if (_currentLevel == null)
            return null;

        return LoadTerrainStructureReadinessRows()
            .FirstOrDefault(row => string.Equals(row.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<TerrainStructureReadiness> LoadTerrainStructureReadinessRows()
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "cross-level-terrain-structure", "terrain-structure-readiness.json");
        if (!File.Exists(path))
            return [];

        try
        {
            List<TerrainStructureReadiness> readiness = new();
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!TryGetJsonArray(document.RootElement, "Rows", "rows", out JsonElement rows))
                return [];

            foreach (JsonElement row in rows.EnumerateArray())
            {
                string levelKey = GetJsonString(row, "LevelKey");
                if (string.IsNullOrWhiteSpace(levelKey))
                    continue;

                readiness.Add(new TerrainStructureReadiness(
                    levelKey,
                    GetJsonString(row, "LevelName", levelKey),
                    GetJsonString(row, "Status", "unknown"),
                    GetJsonString(row, "RemoveRuntimeKey"),
                    GetJsonInt32(row, "RemovePatchCount", 0),
                    GetJsonBoolean(row, "RemoveHadCollision"),
                    GetJsonBoolean(row, "RemoveHasCollisionPatch"),
                    GetJsonString(row, "AddRuntimeKey"),
                    GetJsonInt32(row, "AddSlackBytes", 0),
                    GetJsonInt32(row, "AddRequiredSlackBytes", 0),
                    GetJsonInt32(row, "AddPatchCount", 0),
                    GetJsonBoolean(row, "AddHasIndependentVertices"),
                    GetJsonBoolean(row, "AddHasCollisionPatch"),
                    GetJsonBoolean(row, "AddHasFallbackAppend"),
                    GetJsonString(row, "Notes")));
            }

            return readiness;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return [];
    }

    private string BuildTerrainReadinessCoverageSummary(bool compact)
    {
        IReadOnlyList<TerrainGeometryReadiness> geometry = LoadTerrainGeometryReadinessRows();
        IReadOnlyList<TerrainTextureReadiness> texture = LoadTerrainTextureReadinessRows();
        IReadOnlyList<TerrainStructureReadiness> structure = LoadTerrainStructureReadinessRows();
        if (geometry.Count == 0 && texture.Count == 0 && structure.Count == 0)
        {
            return compact
                ? "Cross-level proof: no generated terrain readiness reports yet."
                : "Cross-level proof summary: no terrain readiness reports have been generated yet. Run the terrain smoke/readiness pass before trusting Create BIN terrain writeback.";
        }

        string geometryText = geometry.Count == 0
            ? "movement/point: no report"
            : FormatTerrainReadinessCoverage(
                "movement/point",
                geometry.Count,
                ("full", CountTerrainReadinessStatus(geometry, row => row.Status, "smoke-passed")),
                ("visual-only", CountTerrainReadinessStatus(geometry, row => row.Status, "visual-only")),
                ("needs capture", CountTerrainReadinessStatus(geometry, row => row.Status, "skipped")));
        string textureText = texture.Count == 0
            ? "palette/texture: no report"
            : FormatTerrainReadinessCoverage(
                "palette/texture",
                texture.Count,
                ("full", CountTerrainReadinessStatus(texture, row => row.Status, "smoke-passed")),
                ("swap-only", CountTerrainReadinessStatus(texture, row => row.Status, "texture-swap-only")),
                ("needs capture", CountTerrainReadinessStatus(texture, row => row.Status, "skipped")));
        string structureText = structure.Count == 0
            ? "add/remove: no report"
            : FormatTerrainReadinessCoverage(
                "add/remove",
                structure.Count,
                ("full", CountTerrainReadinessStatus(structure, row => row.Status, "smoke-passed")),
                ("remove-only", CountTerrainReadinessStatus(structure, row => row.Status, "remove-only")),
                ("partial", CountTerrainReadinessStatus(structure, row => row.Status, "structure-partial")),
                ("blocked", CountTerrainReadinessStatus(structure, row => row.Status, "blocked")),
                ("needs capture", CountTerrainReadinessStatus(structure, row => row.Status, "skipped")));

        string summary = $"Cross-level proof: {geometryText}; {textureText}; {structureText}.";
        if (compact)
            return summary;

        return summary + " Levels marked needs capture can still be viewed and staged in the editor, but patched BIN/CUE terrain writeback is not proof-backed until that level has overlay/RAM capture data.";
    }

    private static string FormatTerrainReadinessCoverage(string label, int total, params (string Label, int Count)[] groups)
    {
        string summary = string.Join(", ", groups
            .Where(group => group.Count > 0)
            .Select(group => $"{group.Count} {group.Label}"));
        if (string.IsNullOrWhiteSpace(summary))
            summary = "no classified rows";

        return $"{label}: {summary} ({total} level(s))";
    }

    private static int CountTerrainReadinessStatus<T>(IEnumerable<T> rows, Func<T, string> getStatus, string status)
    {
        return rows.Count(row => string.Equals(getStatus(row), status, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatTerrainStructureReadiness(TerrainStructureReadiness readiness)
    {
        string remove = readiness.RemovePatchCount > 0
            ? readiness.RemoveHasCollisionPatch
                ? "remove-face patches visible terrain and collision"
                : readiness.RemoveHadCollision
                    ? "remove-face patches visible terrain, collision still partial"
                    : "remove-face patches visible terrain"
            : "remove-face is not proven yet";
        string add = FormatTerrainStructureAddMode(readiness);
        string addSample = string.IsNullOrWhiteSpace(readiness.AddRuntimeKey)
            ? ""
            : $" Sample add face {readiness.AddRuntimeKey}{FormatTerrainStructureSlack(readiness)}.";

        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"full for captured data: {remove}; add-copy patches {add}.{addSample}",
            "structure-partial" => $"partial: {remove}; add-copy is {add}, so in-game collision still needs proof.{addSample}",
            "remove-only" => $"remove-only: {remove}; add-copy has no safe sector room/proof yet for this captured level.",
            "skipped" => $"needs capture/proof before structural terrain writeback. {readiness.Notes}",
            "blocked" => $"blocked by the current proof pass. {readiness.Notes}",
            _ => $"{readiness.Status}: {remove}; add-copy is {add}.{addSample}"
        };
    }

    private static string FormatTerrainStructureAddMode(TerrainStructureReadiness readiness)
    {
        if (readiness.AddHasIndependentVertices && readiness.AddHasCollisionPatch)
            return "independent visible terrain plus copied collision";
        if (readiness.AddHasIndependentVertices)
            return "independent visible terrain only";
        if (readiness.AddHasFallbackAppend)
            return "legacy partial terrain proof";
        if (readiness.AddPatchCount > 0)
            return "not fully classified";
        return "not proven";
    }

    private static string FormatTerrainStructureSlack(TerrainStructureReadiness readiness)
    {
        return readiness.AddRequiredSlackBytes > 0
            ? $", slack {readiness.AddSlackBytes}/{readiness.AddRequiredSlackBytes} bytes"
            : "";
    }

    private string BuildTerrainRemoveFaceReadinessText(TerrainPolygon terrain)
    {
        TerrainRemoveFaceReadiness readiness = GetTerrainRemoveFaceReadiness(terrain);
        string visible = readiness.CanRemoveVisibleFace
            ? "visible face can be neutralized"
            : readiness.HasSourceSearch
                ? "visible face is not source-mapped yet"
                : "source-search map is missing";
        string collision = readiness.CanRemoveCollision
            ? $"matched collision can be neutralized ({readiness.MatchedCollisionTriangles}/{readiness.TotalCollisionTriangles} triangle(s))"
            : readiness.HasCollisionData
                ? readiness.TotalCollisionTriangles > 0
                    ? "no matching collision triangles for this face"
                    : "face has no collision triangles to match"
                : "collision data is not matched for this level";
        return $"{visible}; {collision}";
    }

    private TerrainRemoveFaceReadiness GetTerrainRemoveFaceReadiness(TerrainPolygon terrain)
    {
        bool hasSourceSearch = TryBuildTerrainSectorAppendSlack(out Dictionary<int, int> sectorSlack);
        bool hasSourceDerivedFace = IsSourceDerivedVisibleTerrainFace(terrain);
        bool hasSourceSector = (hasSourceSearch && terrain.SectorOffset >= 0 && sectorSlack.ContainsKey(terrain.SectorOffset)) || hasSourceDerivedFace;
        bool hasFaceOffset = terrain.FaceOffset >= 0 && terrain.SectorOffset >= 0;
        bool hasCollisionData = _terrainCollisionTriangleKeys.Count > 0;
        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        return new TerrainRemoveFaceReadiness(
            HasSourceSearch: hasSourceSearch || hasSourceDerivedFace,
            HasSourceSector: hasSourceSector,
            HasFaceOffset: hasFaceOffset,
            HasCollisionData: hasCollisionData,
            MatchedCollisionTriangles: coverage.MatchedTriangles,
            TotalCollisionTriangles: coverage.TotalTriangles,
            CanRemoveVisibleFace: hasSourceSector && hasFaceOffset,
            CanRemoveCollision: hasCollisionData && coverage.MatchedTriangles > 0);
    }

    private bool IsSourceDerivedVisibleTerrainFace(TerrainPolygon terrain)
    {
        return _currentGeometry != null &&
            IsSourceDerivedGeometry(_currentGeometry) &&
            terrain.SectorOffset >= 0 &&
            terrain.FaceOffset >= terrain.SectorOffset;
    }

    private string BuildTerrainAddCopyFaceReadinessText(TerrainPolygon terrain)
    {
        TerrainAddCopyFaceReadiness readiness = GetTerrainAddCopyFaceReadiness(terrain);
        string detail = readiness.IsHighDetail
            ? "high-detail face"
            : "not high-detail; native add-copy currently supports high-detail faces only";
        string source = readiness.HasSourceSector
            ? "source sector mapped"
            : readiness.HasSourceSearch
                ? "source sector not mapped for this face"
                : "source-search map missing";
        string slack = readiness.RequiredSlackBytes <= 0 || readiness.RequiredSlackBytes == int.MaxValue
            ? "required slack unknown"
            : readiness.AvailableSlackBytes >= readiness.RequiredSlackBytes
                ? $"sector slack ready ({readiness.AvailableSlackBytes}/{readiness.RequiredSlackBytes} bytes)"
                : $"sector slack short ({readiness.AvailableSlackBytes}/{readiness.RequiredSlackBytes} bytes)";
        string collision = readiness.HasCollisionMatch
            ? $"playable collision matched ({readiness.MatchedCollisionTriangles}/{readiness.TotalCollisionTriangles} triangle(s))"
            : readiness.TotalCollisionTriangles > 0
                ? "no playable collision match for this source face"
                : "collision match unknown";
        string verdict = readiness.CanAddIndependentVisibleTerrain
            ? readiness.HasCollisionMatch
                ? "Best path: Create BIN should be able to add independent visible vertices; copied collision still depends on a same-cell placeholder."
                : "Best path: Create BIN should be able to add independent visible vertices, but copied collision is not proven for this face."
            : "Best path: use Find Best Add Face or choose a high-detail source with mapped sector slack before trusting this add-copy.";

        return $"Selected source readiness: {detail}; {source}; {slack}; {collision}. {verdict}";
    }

    private TerrainAddCopyFaceReadiness GetTerrainAddCopyFaceReadiness(TerrainPolygon terrain)
    {
        int requiredSlack = RequiredTerrainAddCopySlack(terrain);
        bool hasSourceSearch = TryBuildTerrainSectorAppendSlack(out Dictionary<int, int> sectorSlack);
        int availableSlack = terrain.SectorOffset >= 0 && sectorSlack.TryGetValue(terrain.SectorOffset, out int slack)
            ? slack
            : 0;
        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        bool highDetail = string.Equals(terrain.Detail, "hp", StringComparison.OrdinalIgnoreCase);
        bool sourceMapped = hasSourceSearch && terrain.SectorOffset >= 0 && sectorSlack.ContainsKey(terrain.SectorOffset);
        bool enoughSlack = requiredSlack > 0 && requiredSlack != int.MaxValue && availableSlack >= requiredSlack;
        return new TerrainAddCopyFaceReadiness(
            IsHighDetail: highDetail,
            HasSourceSearch: hasSourceSearch,
            HasSourceSector: sourceMapped,
            AvailableSlackBytes: availableSlack,
            RequiredSlackBytes: requiredSlack,
            HasCollisionMatch: coverage.MatchedTriangles > 0,
            MatchedCollisionTriangles: coverage.MatchedTriangles,
            TotalCollisionTriangles: coverage.TotalTriangles,
            CanAddIndependentVisibleTerrain: highDetail && sourceMapped && enoughSlack);
    }

    private bool TryBuildTerrainSectorAppendSlack(out Dictionary<int, int> result)
    {
        result = new Dictionary<int, int>();
        if (_currentLevel == null)
            return false;

        string sourceSearchPath = FindCurrentTerrainSourceSearchPath(_currentLevel.Key);
        if (!File.Exists(sourceSearchPath))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(sourceSearchPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!TryGetJsonArray(document.RootElement, "Results", "results", out JsonElement results))
                return false;

            Dictionary<int, (long WadOffset, int Size)> sectors = new();
            foreach (JsonElement entry in results.EnumerateArray())
            {
                if (!TryGetJsonArray(entry, "FullSectorHits", "fullSectorHits", out JsonElement hits) || hits.GetArrayLength() == 0)
                    continue;

                JsonElement firstHit = hits.EnumerateArray().First();
                int sectorOffset = FirstJsonInt32(entry, -1, "SectorOffset", "sectorOffset");
                int sectorSize = FirstJsonInt32(entry, 0, "SectorSizeBytes", "sectorSizeBytes");
                long wadOffset = FirstJsonInt64(firstHit, -1, "WadOffset", "wadOffset");
                if (sectorOffset < 0 || sectorSize <= 0 || wadOffset < 0 || sectors.ContainsKey(sectorOffset))
                    continue;

                sectors[sectorOffset] = (wadOffset, sectorSize);
            }

            if (sectors.Count == 0)
                return true;

            long[] wadOffsets = sectors.Values
                .Select(value => value.WadOffset)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            foreach (KeyValuePair<int, (long WadOffset, int Size)> pair in sectors)
            {
                long next = wadOffsets.FirstOrDefault(offset => offset > pair.Value.WadOffset);
                if (next <= 0)
                {
                    result[pair.Key] = 0;
                    continue;
                }

                long sectorEnd = pair.Value.WadOffset + pair.Value.Size;
                long slack = next - sectorEnd;
                result[pair.Key] = slack <= 0 ? 0 : slack > int.MaxValue ? int.MaxValue : (int)slack;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            result.Clear();
            return false;
        }
    }

    private string FindCurrentTerrainSourceSearchPath(string levelKey)
    {
        string nativePath = Path.Combine(_workspace.RootPath, "_local", "terrain", $"{levelKey}-runtime-terrain-source-search-native.json");
        if (File.Exists(nativePath))
            return nativePath;

        return TerrainPatchDataLocator.FindSourceSearch(_workspace, levelKey);
    }

    private static int RequiredTerrainAddCopySlack(TerrainPolygon terrain)
    {
        IReadOnlyList<int> vertexIndexes = NormalizeTerrainVertexIndexes(terrain.VertexIndexes, terrain.OriginalZValues.Length);
        int uniqueVertexCount = vertexIndexes.Distinct().Count();
        return uniqueVertexCount <= 0 ? int.MaxValue : (uniqueVertexCount * 4) + 16;
    }

    private static IReadOnlyList<int> NormalizeTerrainVertexIndexes(IReadOnlyList<int> indexes, int zValueCount)
    {
        if (indexes.Count <= zValueCount)
            return indexes;

        List<int> result = new();
        HashSet<int> seen = new();
        foreach (int index in indexes)
        {
            if (!seen.Add(index))
                continue;

            result.Add(index);
            if (result.Count == zValueCount)
                break;
        }

        return result.Count == zValueCount ? result : indexes.Take(zValueCount).ToArray();
    }

    private string BuildTerrainRemoveStructureDetailsText(TerrainPolygon terrain)
    {
        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null)
        {
            if (IsSourceDerivedVisibleTerrainFace(terrain))
                return "Structure readiness: source-derived WAD offsets are available for the visible face. Normal Remove Terrain still waits for matched playable collision so the edit is not visual-only.";

            return "Structure readiness: no current-level add/remove proof report has been generated yet.";
        }

        string sample = string.IsNullOrWhiteSpace(readiness.RemoveRuntimeKey)
            ? "No remove-face sample passed the current proof pass for this level."
            : string.Equals(readiness.RemoveRuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
                ? "This selected face was the sampled remove-face proof."
                : $"The proof sampled remove face {readiness.RemoveRuntimeKey}; this selected face will still be reviewed by Create BIN.";
        return $"Structure readiness: {FormatTerrainStructureReadiness(readiness)}\n{sample}\nNormal Remove Terrain is enabled only when the selected visible face and its gameplay collision are both mapped.";
    }

    private string BuildTerrainAddCopyStructureDetailsText(TerrainPolygon terrain)
    {
        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null)
            return "Structure readiness: no current-level add/remove proof report has been generated yet.";

        string sample = string.IsNullOrWhiteSpace(readiness.AddRuntimeKey)
            ? "No add-copy sample passed the current proof pass for this level."
            : string.Equals(readiness.AddRuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
                ? "This selected face was the sampled add-copy proof."
                : $"The proof sampled add-copy face {readiness.AddRuntimeKey}; this selected source face may differ by sector slack/collision. Use Find Best Add Face first when you want the safest add-copy source.";
        return $"Structure readiness: {FormatTerrainStructureReadiness(readiness)}\n{sample}";
    }

    private string BuildTerrainBehaviorReadinessBreakdown()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return "Behavior readiness: no terrain faces loaded.";

        int proofBacked = 0;
        int hazardCandidates = 0;
        int solidCandidates = 0;
        int steepCandidates = 0;
        int unknown = 0;
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (TerrainFaceHasBehaviorProof(face))
            {
                proofBacked++;
                continue;
            }

            string behavior = TerrainBehaviorClassifier.FormatBehavior(face.Behavior).ToLowerInvariant();
            string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
            if (behavior.Contains("hazard", StringComparison.OrdinalIgnoreCase) || surface is "water" or "lava" or "ooze")
                hazardCandidates++;
            else if (behavior.Contains("solid", StringComparison.OrdinalIgnoreCase))
                solidCandidates++;
            else if (behavior.Contains("steep", StringComparison.OrdinalIgnoreCase) || behavior.Contains("wall", StringComparison.OrdinalIgnoreCase))
                steepCandidates++;
            else
                unknown++;
        }

        List<string> parts = new();
        if (proofBacked > 0)
            parts.Add($"{proofBacked} proof-backed");
        if (hazardCandidates > 0)
            parts.Add($"{hazardCandidates} hazard candidates");
        if (solidCandidates > 0)
            parts.Add($"{solidCandidates} solid candidates");
        if (steepCandidates > 0)
            parts.Add($"{steepCandidates} steep/wall candidates");
        if (unknown > 0)
            parts.Add($"{unknown} unknown");

        string customFaces = BuildCustomTerrainTextureFaceSummary();
        string suffix = string.IsNullOrWhiteSpace(customFaces) ? "" : $" {customFaces}";
        return $"Behavior readiness: {string.Join(", ", parts)}. Candidate behavior still needs RAM proof before trusting water/lava/goo damage or safe-ground behavior.{suffix}";
    }

    private string BuildCustomTerrainTextureFaceSummary()
    {
        if (_currentGeometry == null || _customTerrainTextures.Count == 0)
            return "";

        HashSet<int> customTextureIds = _customTerrainTextures.Select(texture => texture.TextureId).ToHashSet();
        int faces = _currentGeometry.Polygons.Count(face => customTextureIds.Contains(face.TextureId));
        if (faces > 0)
        {
            var usage = GetCustomTerrainTextureUsage();
            string unused = usage.UnusedImports > 0
                ? $" {usage.UnusedImports} unused custom import(s) will be ignored by Create BIN."
                : "";
            return $"Custom texture art currently previews on {faces} face(s).{unused}";
        }

        return $"{_customTerrainTextures.Count} unused custom texture import(s) are staged but do not apply to any current face.";
    }

    private string BuildCustomTerrainArtStageSummary()
    {
        if (_customTerrainTextures.Count == 0)
            return "0";

        var usage = GetCustomTerrainTextureUsage();
        if (usage.UnusedImports == 0)
            return $"{usage.ActiveImports} active import(s) on {usage.AffectedFaces} face(s)";
        if (usage.ActiveImports == 0)
            return $"{usage.UnusedImports} unused import(s), ignored by Create BIN";

        return $"{usage.ActiveImports} active import(s) on {usage.AffectedFaces} face(s), {usage.UnusedImports} unused";
    }

    private async Task ClearUnusedCustomTerrainArtAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before clearing unused terrain art.";
            return;
        }

        if (_currentGeometry == null)
        {
            _statusText.Text = "Load this level's terrain before clearing unused terrain art.";
            return;
        }

        if (_customTerrainTextures.Count == 0)
        {
            _statusText.Text = "There is no custom terrain art staged for this level.";
            return;
        }

        HashSet<int> liveTextureIds = CurrentTerrainTextureIds();
        CustomTerrainTextureImport[] activeImports = _customTerrainTextures
            .Where(texture => liveTextureIds.Contains(texture.TextureId))
            .ToArray();
        CustomTerrainTextureImport[] unusedImports = _customTerrainTextures
            .Where(texture => !liveTextureIds.Contains(texture.TextureId))
            .ToArray();
        if (unusedImports.Length == 0)
        {
            var usage = GetCustomTerrainTextureUsage();
            _statusText.Text = $"All {usage.ActiveImports} custom terrain art import(s) are still used by current terrain faces.";
            return;
        }

        string manifestPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, _currentLevel.Key);
        if (activeImports.Length == 0)
        {
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
        }
        else
        {
            await CustomTerrainTextureStore.SaveManifestAsync(manifestPath, _currentLevel.Key, _currentLevel.DisplayName, activeImports);
        }

        string freedTextureIds = string.Join(", ", unusedImports
            .Select(texture => texture.TextureId)
            .Distinct()
            .OrderBy(textureId => textureId)
            .Take(8)
            .Select(textureId => textureId.ToString()));
        int distinctUnused = unusedImports.Select(texture => texture.TextureId).Distinct().Count();
        if (distinctUnused > 8)
            freedTextureIds += ", ...";

        _customTerrainTextures = activeImports;
        ApplyCustomTerrainTexturePreviews();
        RefreshCurrentLevelDetails();
        RefreshTerrainSurfaceQuickState();
        RefreshTerrainReadinessHint();
        _viewport.InvalidateVisual();
        _statusText.Text = $"Cleared {unusedImports.Length} unused custom terrain art import(s) from {distinctUnused} texture ID(s) ({freedTextureIds}). {activeImports.Length} active import(s) remain for current terrain faces.";
    }

    private (int ActiveImports, int UnusedImports, int ActiveTextures, int AffectedFaces) GetCustomTerrainTextureUsage()
    {
        if (_customTerrainTextures.Count == 0)
            return (0, 0, 0, 0);

        if (_currentGeometry == null)
            return (_customTerrainTextures.Count, 0, _customTerrainTextures.Select(texture => texture.TextureId).Distinct().Count(), 0);

        HashSet<int> liveTextureIds = CurrentTerrainTextureIds();
        HashSet<int> activeCustomTextureIds = _customTerrainTextures
            .Where(texture => liveTextureIds.Contains(texture.TextureId))
            .Select(texture => texture.TextureId)
            .ToHashSet();
        int activeImports = _customTerrainTextures.Count(texture => activeCustomTextureIds.Contains(texture.TextureId));
        int affectedFaces = _currentGeometry.Polygons.Count(face => !face.IsTerrainRemoved && activeCustomTextureIds.Contains(face.TextureId));
        return (activeImports, _customTerrainTextures.Count - activeImports, activeCustomTextureIds.Count, affectedFaces);
    }

    private HashSet<int> CurrentTerrainTextureIds()
    {
        return _currentGeometry == null
            ? new HashSet<int>()
            : _currentGeometry.Polygons
                .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
                .Select(face => face.TextureId)
                .ToHashSet();
    }

    private string BuildCurrentLevelTerrainProofReadiness()
    {
        if (_currentLevel == null)
            return "";

        string summaryPath = Path.Combine(_workspace.RootPath, "_local", "smoke", "terrain-behavior-proof-summary.json");
        if (!File.Exists(summaryPath))
            return "Proofs: not generated yet; use Refresh Proofs.";

        try
        {
            using FileStream stream = File.OpenRead(summaryPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("Surfaces", out JsonElement surfaces) || surfaces.ValueKind != JsonValueKind.Array)
                return "Proofs: summary file is missing surface rows; use Refresh Proofs.";

            int rows = 0;
            int proven = 0;
            int observed = 0;
            int missing = 0;
            int hazardMissing = 0;
            int solidMissing = 0;
            foreach (JsonElement surface in surfaces.EnumerateArray())
            {
                string levelKey = GetJsonString(surface, "LevelKey");
                if (!string.Equals(levelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
                    continue;

                rows++;
                int rank = GetJsonInt32(surface, "ProofStatusRank", 0);
                string material = TerrainMaterialClassifier.NormalizeSurfaceName(GetJsonString(surface, "Surface"));
                if (rank >= 3)
                    proven++;
                else if (rank == 2)
                    observed++;
                else
                {
                    missing++;
                    if (material is "water" or "lava" or "ooze")
                        hazardMissing++;
                    else
                        solidMissing++;
                }
            }

            if (rows == 0)
                return "Proofs: no current-level rows yet; use Refresh Proofs.";
            return $"Proofs: {proven} proven, {observed} observed, {missing} missing ({hazardMissing} hazard, {solidMissing} solid/control).";
        }
        catch
        {
            return "Proofs: summary could not be read; use Refresh Proofs.";
        }
    }

    private async Task ShowTerrainSurfaceGroupsAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before opening terrain surface groups.";
            return;
        }

        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
        {
            _statusText.Text = "Terrain is not loaded for this level yet.";
            return;
        }

        List<TerrainSurfaceGroupItem> groups = BuildTerrainSurfaceGroupItems().ToList();
        if (groups.Count == 0)
        {
            _statusText.Text = $"{_currentLevel.DisplayName} has no terrain surface groups loaded.";
            return;
        }

        ComboBox surfaceFilter = new()
        {
            ItemsSource = new[] { "All surfaces", "Hazards", "Ground", "Edited", "Custom texture", "Missing proof", "Hazard proof needed", "Solid proof needed" },
            SelectedIndex = 0,
            MinWidth = 165
        };
        ListBox list = new()
        {
            MinHeight = 390,
            ItemTemplate = new FuncDataTemplate<TerrainSurfaceGroupItem>((item, _) => new TextBlock
            {
                Text = item?.ListText ?? "",
                TextWrapping = TextWrapping.Wrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 126
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not TerrainSurfaceGroupItem item)
            {
                details.Text = "Select a surface group to see edit, proof, and texture patch status.";
                return;
            }

            string custom = item.HasCustomTexture
                ? "Custom texture art is staged and will be included by Create BIN."
                : "No custom texture art is staged for this texture.";
            string edits = item.EditedFaces == 0
                ? "No edited faces in this group."
                : $"{item.EditedFaces} edited face(s) in this group.";
            details.Text =
                $"{TerrainMaterialClassifier.FormatSurface(item.Surface)} texture {item.TextureId}, {item.FaceCount} face(s)\n" +
                $"Behavior: {item.BehaviorSummary}\n" +
                $"Proof: {item.ProofSummary}. {item.ProofNextStep}\n" +
                $"{custom} {edits}\n" +
                $"Representative face: {item.RuntimeKey} at {item.X:0.##}, {item.Y:0.##}, {item.Z:0.##}";
        }

        void RefreshFilteredList()
        {
            List<TerrainSurfaceGroupItem> filtered = groups
                .Where(item => MatchesTerrainSurfaceGroupFilter(item, surfaceFilter.SelectedIndex))
                .OrderBy(item => TerrainSummarySurfaceRank(item.Surface))
                .ThenBy(item => item.ProofStatusRank)
                .ThenByDescending(item => item.FaceCount)
                .ThenBy(item => item.TextureId)
                .ToList();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Count == 0 ? -1 : 0;
            RefreshDetails();
        }

        list.SelectionChanged += (_, _) => RefreshDetails();
        surfaceFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        RefreshFilteredList();

        Window dialog = new()
        {
            Title = "Terrain Surface Groups",
            Width = 1020,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainSurfaceGroupsDialogContent(dialog, surfaceFilter, list, details);
        TerrainSurfaceGroupAction? action = await dialog.ShowDialog<TerrainSurfaceGroupAction?>(this);
        if (action == null)
            return;

        if (action.Kind == TerrainSurfaceGroupActionKind.SelectProofTarget)
            await SelectTerrainSurfaceGroupProofTargetAsync(action.Item);
        else
            SelectTerrainSurfaceGroup(action.Item);
    }

    private Control BuildTerrainSurfaceGroupsDialogContent(Window dialog, ComboBox surfaceFilter, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Current-level terrain surfaces, textures, edits, and proof state",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        panel.Children.Add(surfaceFilter);
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button close = NewButton("Close");
        Button proofTargets = NewButton("Select Proof Target");
        Button selectFace = NewButton("Select Face");
        close.Click += (_, _) => dialog.Close(null);
        proofTargets.Click += (_, _) =>
        {
            if (list.SelectedItem is TerrainSurfaceGroupItem item)
                dialog.Close(new TerrainSurfaceGroupAction(item, TerrainSurfaceGroupActionKind.SelectProofTarget));
        };
        selectFace.Click += (_, _) =>
        {
            if (list.SelectedItem is TerrainSurfaceGroupItem item)
                dialog.Close(new TerrainSurfaceGroupAction(item, TerrainSurfaceGroupActionKind.SelectFace));
        };
        buttons.Children.Add(close);
        buttons.Children.Add(proofTargets);
        buttons.Children.Add(selectFace);
        panel.Children.Add(buttons);
        return panel;
    }

    private IEnumerable<TerrainSurfaceGroupItem> BuildTerrainSurfaceGroupItems()
    {
        if (_currentGeometry == null || _currentLevel == null)
            yield break;

        TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(_workspace.RootPath, _currentLevel.Key);
        HashSet<int> customTextureIds = _customTerrainTextures.Select(texture => texture.TextureId).ToHashSet();
        foreach (IGrouping<(string Surface, int TextureId), TerrainPolygon> group in _currentGeometry.Polygons
            .Where(polygon => polygon.TextureId >= 0)
            .GroupBy(polygon => (TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface), polygon.TextureId)))
        {
            List<TerrainPolygon> faces = group.ToList();
            TerrainPolygon representative = faces
                .OrderByDescending(face => face.Points.Count)
                .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                .First();
            int representativeIndex = _currentGeometry.Polygons.IndexOf(representative);
            List<TerrainBehaviorRule> rules = proofFile.Rules
                .Where(rule => rule.TextureId == group.Key.TextureId || string.Equals(rule.Surface, group.Key.Surface, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(rule => RuleConfidenceRank(rule.Confidence))
                .ThenBy(rule => rule.TextureId)
                .ToList();

            yield return new TerrainSurfaceGroupItem
            {
                Surface = group.Key.Surface,
                TextureId = group.Key.TextureId,
                FaceCount = faces.Count,
                EditedFaces = faces.Count(face => face.IsTerrainEdited),
                BehaviorSummary = BuildBehaviorSummary(faces),
                ProofSummary = BuildProofRuleSummary(rules),
                ProofStatusRank = rules.Count == 0 ? 0 : rules.Max(rule => RuleConfidenceRank(rule.Confidence)),
                ProofNextStep = BuildTerrainProofSummaryNextStep(group.Key.Surface, rules),
                HasCustomTexture = customTextureIds.Contains(group.Key.TextureId),
                TerrainIndex = representativeIndex,
                RuntimeKey = representative.RuntimeKey,
                X = representative.Center.X,
                Y = representative.Center.Y,
                Z = representative.AvgZ
            };
        }
    }

    private void SelectTerrainSurfaceGroup(TerrainSurfaceGroupItem item)
    {
        if (_currentGeometry == null || item.TerrainIndex < 0 || item.TerrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = $"Could not select representative face {item.RuntimeKey}.";
            return;
        }

        _viewport.FocusTerrain(item.TerrainIndex);
        _statusText.Text = $"Selected {TerrainMaterialClassifier.FormatSurface(item.Surface)} texture {item.TextureId}: {item.FaceCount} face(s), proof {item.ProofSummary}.";
    }

    private async Task SelectTerrainSurfaceGroupProofTargetAsync(TerrainSurfaceGroupItem item)
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before selecting terrain proof targets.";
            return;
        }

        string targetPath = Path.Combine(_workspace.RootPath, "_local", "smoke", "terrain-behavior-proof-targets.json");
        List<TerrainProofTargetItem> targets = LoadTerrainProofTargets(targetPath)
            .Where(target => target.ProofStatusRank < 3)
            .Where(target => string.Equals(target.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            .Where(target => string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(target.Surface), TerrainMaterialClassifier.NormalizeSurfaceName(item.Surface), StringComparison.OrdinalIgnoreCase))
            .Where(target => target.TextureId == item.TextureId)
            .ToList();

        if (targets.Count == 0)
        {
            targets = LoadTerrainProofTargets(targetPath)
                .Where(target => target.ProofStatusRank < 3)
                .Where(target => string.Equals(target.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
                .Where(target => string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(target.Surface), TerrainMaterialClassifier.NormalizeSurfaceName(item.Surface), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        TerrainProofTargetItem? target = OrderTerrainProofTargets(targets).FirstOrDefault();
        if (target == null)
        {
            _statusText.Text = $"No missing proof target is listed for {TerrainMaterialClassifier.FormatSurface(item.Surface)} texture {item.TextureId}. Use Refresh Proofs if the target list is stale.";
            return;
        }

        await SelectTerrainProofTargetAsync(target, useCompanion: false);
    }

    private static bool MatchesTerrainSurfaceGroupFilter(TerrainSurfaceGroupItem item, int selectedIndex)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(item.Surface);
        return selectedIndex switch
        {
            1 => surface is "water" or "lava" or "ooze",
            2 => surface is "grass" or "sand" or "stone" or "ground" or "brick" or "ice",
            3 => item.EditedFaces > 0,
            4 => item.HasCustomTexture,
            5 => item.ProofStatusRank < 3,
            6 => (surface is "water" or "lava" or "ooze") && item.ProofStatusRank < 3,
            7 => surface is not ("water" or "lava" or "ooze") && item.ProofStatusRank < 3,
            _ => true
        };
    }

    private void RefreshTerrainProofQueueHint()
    {
        _terrainProofQueueHint.Text = BuildCurrentLevelTerrainProofQueueHint();
    }

    private string BuildCurrentLevelTerrainProofQueueHint()
    {
        if (_currentLevel == null)
            return "Terrain proof queue: choose a level to see the next behavior capture target.";

        string targetPath = Path.Combine(_workspace.RootPath, "_local", "smoke", "terrain-behavior-proof-targets.json");
        if (!File.Exists(targetPath))
            return "Terrain proof queue: use Refresh Proofs to build the water/lava/goo and solid-control capture list.";

        List<TerrainProofTargetItem> currentTargets = LoadTerrainProofTargets(targetPath)
            .Where(target => string.Equals(target.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            .Where(target => target.ProofStatusRank < 3)
            .OrderBy(target => target.CaptureKind == "solid-control" ? 1 : 0)
            .ThenBy(target => TerrainSummarySurfaceRank(target.Surface))
            .ThenBy(target => target.ProofStatusRank)
            .ThenBy(target => target.NearestControlDistance ?? double.MaxValue)
            .ThenBy(target => target.TextureId)
            .ToList();

        if (currentTargets.Count == 0)
            return $"Terrain proof queue: no missing behavior targets listed for {_currentLevel.DisplayName}. Use Proof Summary for all-level status.";

        int hazardTargets = currentTargets.Count(target => target.CaptureKind == "hazard-touch");
        int solidTargets = currentTargets.Count(target => target.CaptureKind == "solid-control");
        TerrainProofTargetItem next = currentTargets[0];
        string captureKind = next.CaptureKind.Replace('-', ' ');
        string companion = string.IsNullOrWhiteSpace(next.NearestSolidOrHazardRuntimeKey)
            ? ""
            : $" Companion face: {next.NearestSolidOrHazardRuntimeKey}; use Proof Pair after selecting this target.";
        return $"Terrain proof queue: next {_currentLevel.DisplayName} capture is {TerrainMaterialClassifier.FormatSurface(next.Surface)} {captureKind}, texture {next.TextureId}, face {next.RuntimeKey}. Missing here: {hazardTargets} hazard, {solidTargets} solid-control.{companion}";
    }

    private async Task<TerrainBehaviorProofReports> BuildAndWriteTerrainProofReportsAsync()
    {
        TerrainBehaviorProofReports reports = await TerrainBehaviorProofReportBuilder.BuildAsync(_workspace, _catalog);
        await TerrainBehaviorProofReportBuilder.WriteAsync(reports, Path.Combine(_workspace.RootPath, "_local", "smoke"));
        return reports;
    }

    private Control BuildTerrainProofSummaryDialogContent(Window dialog, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Terrain surfaces, candidate behaviors, and exact-proof status",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button close = NewButton("Close");
        Button open = NewButton("Open Level");
        close.Click += (_, _) => dialog.Close(null);
        open.Click += (_, _) => dialog.Close(list.SelectedItem as TerrainProofSummaryItem);
        buttons.Children.Add(close);
        buttons.Children.Add(open);
        panel.Children.Add(buttons);
        return panel;
    }

    private IReadOnlyList<TerrainProofSummaryItem> LoadTerrainProofSummaryItems()
    {
        string cacheDir = Path.Combine(_workspace.RootPath, "editor-cache");
        if (!Directory.Exists(cacheDir))
            return [];

        List<TerrainProofSummaryItem> items = new();
        foreach (string overlayPath in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            string fileName = Path.GetFileName(overlayPath);
            string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            TerrainMaterialClassifier.Apply(levelKey, _workspace.RootPath, geometry);
            TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(_workspace.RootPath, levelKey);
            LevelDefinition? level = _catalog.FindByKey(levelKey);

            foreach (IGrouping<string, TerrainPolygon> surfaceGroup in geometry.Polygons
                .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                .Where(group => ShouldShowTerrainProofSummarySurface(group.Key, proofFile)))
            {
                HashSet<int> textureIds = surfaceGroup.Select(face => face.TextureId).Where(textureId => textureId >= 0).ToHashSet();
                List<TerrainBehaviorRule> rules = proofFile.Rules
                    .Where(rule => textureIds.Contains(rule.TextureId) || string.Equals(rule.Surface, surfaceGroup.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(rule => RuleConfidenceRank(rule.Confidence))
                    .ThenBy(rule => rule.TextureId)
                    .ToList();

                items.Add(new TerrainProofSummaryItem
                {
                    LevelKey = levelKey,
                    DisplayName = level?.DisplayName ?? levelKey,
                    Surface = surfaceGroup.Key,
                    FaceCount = surfaceGroup.Count(),
                    TextureCount = textureIds.Count,
                    BehaviorSummary = BuildBehaviorSummary(surfaceGroup),
                    ProofSummary = BuildProofRuleSummary(rules),
                    ProofStatusRank = rules.Count == 0 ? 0 : rules.Max(rule => RuleConfidenceRank(rule.Confidence)),
                    NextStep = BuildTerrainProofSummaryNextStep(surfaceGroup.Key, rules)
                });
            }
        }

        string? currentKey = _currentLevel?.Key;
        return items
            .OrderByDescending(item => string.Equals(item.LevelKey, currentKey, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => TerrainSummarySurfaceRank(item.Surface))
            .ThenBy(item => item.ProofStatusRank)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldShowTerrainProofSummarySurface(string surface, TerrainBehaviorProofFile proofFile)
    {
        if (surface is "water" or "lava" or "ooze" or "grass" or "sand" or "stone" or "ground")
            return true;

        return proofFile.Rules.Any(rule => string.Equals(rule.Surface, surface, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildBehaviorSummary(IEnumerable<TerrainPolygon> faces)
    {
        return string.Join(", ", faces
            .GroupBy(face => face.Behavior, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(group => $"{TerrainBehaviorClassifier.FormatBehavior(group.Key)} {group.Count()}"));
    }

    private static string BuildProofRuleSummary(IReadOnlyList<TerrainBehaviorRule> rules)
    {
        if (rules.Count == 0)
            return "not captured yet";

        return string.Join(", ", rules
            .GroupBy(rule => rule.Confidence, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => RuleConfidenceRank(group.Key))
            .Select(group => $"{group.Key} {group.Count()}"));
    }

    private static string BuildTerrainProofSummaryNextStep(string surface, IReadOnlyList<TerrainBehaviorRule> rules)
    {
        bool hasProven = rules.Any(rule => string.Equals(rule.Confidence, "proven", StringComparison.OrdinalIgnoreCase));
        bool hasObserved = rules.Any(rule => string.Equals(rule.Confidence, "observed", StringComparison.OrdinalIgnoreCase));
        if (hasProven)
            return "Proven for at least one matching texture; use Proof Targets only as regression checks after terrain edits.";

        if (surface is "water" or "lava" or "ooze")
        {
            if (hasObserved)
                return "Repeat the touch capture and pair it with a solid-control capture in the same level.";

            return "Use Proof Targets to select a face, then capture before/after RAM at the moment Spyro touches it.";
        }

        if (surface is "grass" or "sand" or "stone" or "ground")
        {
            if (hasObserved)
                return "Repeat the walk/no-response capture to promote this solid-control proof.";

            return "Capture before/after RAM with Spyro walking on this surface; it should not change health, i-frames, or state.";
        }

        return "Classify the surface first, then capture a before/after RAM pair if it appears to affect Spyro.";
    }

    private static int TerrainSummarySurfaceRank(string surface)
    {
        return surface switch
        {
            "water" => 0,
            "lava" => 1,
            "ooze" => 2,
            "grass" => 3,
            "sand" => 4,
            "stone" => 5,
            "ground" => 6,
            _ => 7
        };
    }

    private static int RuleConfidenceRank(string confidence)
    {
        return confidence switch
        {
            "proven" => 3,
            "observed" => 2,
            "noisy" => 1,
            _ => 0
        };
    }

    private async Task SelectTerrainProofSummaryLevelAsync(TerrainProofSummaryItem item)
    {
        LevelDefinition? level = _catalog.FindByKey(item.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level {item.LevelKey}.";
            return;
        }

        if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(level))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);
        _statusText.Text = $"{item.DisplayName}: {TerrainMaterialClassifier.FormatSurface(item.Surface)} proof status is {item.ProofSummary}.";
    }

    private async Task ShowTerrainProofTargetsAsync()
    {
        TerrainBehaviorProofReports reports = await BuildAndWriteTerrainProofReportsAsync();
        IReadOnlyList<TerrainProofTargetItem> targets = OrderTerrainProofTargets(reports.Targets.Targets.Select(TerrainProofTargetItem.FromReport));
        if (targets.Count == 0)
        {
            _statusText.Text = "No terrain proof targets found yet. Build the portable cache first.";
            return;
        }

        bool hasCurrentLevelTargets = _currentLevel != null &&
            targets.Any(target => string.Equals(target.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
        ComboBox levelFilter = new()
        {
            ItemsSource = new[] { "This level", "All levels" },
            SelectedIndex = hasCurrentLevelTargets ? 0 : 1,
            MinWidth = 140
        };
        ComboBox kindFilter = new()
        {
            ItemsSource = new[] { "All captures", "Hazard touches", "Solid controls" },
            SelectedIndex = 0,
            MinWidth = 160
        };
        ComboBox surfaceFilter = new()
        {
            ItemsSource = new[] { "All surfaces", "Water", "Lava", "Goo", "Solid materials" },
            SelectedIndex = 0,
            MinWidth = 160
        };
        ListBox list = new()
        {
            MinHeight = 420,
            ItemTemplate = new FuncDataTemplate<TerrainProofTargetItem>((target, _) => new TextBlock
            {
                Text = target?.ListText ?? "",
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 86
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not TerrainProofTargetItem target)
            {
                details.Text = "Select a target to see the exact capture step.";
                return;
            }

            details.Text =
                $"{target.DisplayName} / {target.Surface} texture {target.TextureId}\n" +
                $"Runtime key: {target.RuntimeKey}\n" +
                $"Nearest control: {target.NearestControlId} {target.NearestControlLabel} ({target.NearestControlDistance:0.##})\n" +
                (string.IsNullOrWhiteSpace(target.NearestSolidOrHazardRuntimeKey)
                    ? ""
                    : $"Companion face: {target.NearestSolidOrHazardRuntimeKey} ({target.NearestSolidOrHazardDistance:0.##} units)\n") +
                $"{target.NextCapture}";
        }

        void RefreshFilteredList()
        {
            List<TerrainProofTargetItem> filtered = targets
                .Where(target => MatchesTerrainProofLevelFilter(target, levelFilter.SelectedIndex))
                .Where(target => MatchesTerrainProofKindFilter(target, kindFilter.SelectedIndex))
                .Where(target => MatchesTerrainProofSurfaceFilter(target, surfaceFilter.SelectedIndex))
                .ToList();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Count == 0 ? -1 : 0;
            RefreshDetails();
        }

        list.SelectionChanged += (_, _) => RefreshDetails();
        levelFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        kindFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        surfaceFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        RefreshFilteredList();

        Window dialog = new()
        {
            Title = "Terrain Proof Targets",
            Width = 980,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainProofTargetsDialogContent(dialog, levelFilter, kindFilter, surfaceFilter, list, details);
        TerrainProofTargetSelection? selected = await dialog.ShowDialog<TerrainProofTargetSelection?>(this);
        if (selected != null)
            await SelectTerrainProofTargetAsync(selected.Target, selected.UseCompanion);
    }

    private async Task SelectNextTerrainProofTargetAsync()
    {
        TerrainBehaviorProofReports reports = await BuildAndWriteTerrainProofReportsAsync();
        List<TerrainProofTargetItem> targets = OrderTerrainProofTargets(reports.Targets.Targets
            .Select(TerrainProofTargetItem.FromReport)
            .Where(target => target.ProofStatusRank < 3))
            .ToList();
        TerrainProofTargetItem? target = _currentLevel == null
            ? targets.FirstOrDefault()
            : targets.FirstOrDefault(item => string.Equals(item.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
                ?? targets.FirstOrDefault();

        if (target == null)
        {
            _statusText.Text = "All terrain proof targets already have proven captures.";
            return;
        }

        await SelectTerrainProofTargetAsync(target, useCompanion: false);
    }

    private void SelectActiveTerrainProofPairFace()
    {
        if (_activeTerrainProofTarget == null)
        {
            _statusText.Text = "Select Next Proof first, then use Proof Pair to jump between its target and companion face.";
            return;
        }

        TerrainProofTargetItem target = _activeTerrainProofTarget.Target;
        bool useCompanion = !_activeTerrainProofTarget.UseCompanion;
        string runtimeKey = useCompanion ? target.NearestSolidOrHazardRuntimeKey : target.RuntimeKey;
        if (string.IsNullOrWhiteSpace(runtimeKey))
        {
            _statusText.Text = $"{target.DisplayName} {TerrainMaterialClassifier.FormatSurface(target.Surface)} texture {target.TextureId} has no companion face listed.";
            return;
        }

        if (_currentLevel == null || !string.Equals(_currentLevel.Key, target.LevelKey, StringComparison.OrdinalIgnoreCase))
        {
            _statusText.Text = "The active proof target is from another level. Use Next Proof to reload it before jumping to its pair.";
            return;
        }

        if (_currentGeometry == null)
        {
            _statusText.Text = $"No terrain geometry is loaded for {_currentLevel.DisplayName}.";
            return;
        }

        int index = _currentGeometry.Polygons.FindIndex(polygon => string.Equals(polygon.RuntimeKey, runtimeKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _statusText.Text = $"Face {runtimeKey} was not found in {_currentLevel.DisplayName}.";
            return;
        }

        _activeTerrainProofTarget = new TerrainProofTargetSelection(target, useCompanion);
        _viewport.FocusTerrain(index);
        string role = useCompanion ? "companion/control" : "target";
        _statusText.Text = $"Selected proof {role} face {runtimeKey} for {target.DisplayName} {TerrainMaterialClassifier.FormatSurface(target.Surface)} texture {target.TextureId}.";
    }

    private static IReadOnlyList<TerrainProofTargetItem> OrderTerrainProofTargets(IEnumerable<TerrainProofTargetItem> targets)
    {
        return targets
            .OrderBy(target => target.CaptureKind == "solid-control" ? 1 : 0)
            .ThenBy(target => TerrainSummarySurfaceRank(target.Surface))
            .ThenBy(target => target.ProofStatusRank)
            .ThenBy(target => target.NearestControlDistance ?? double.MaxValue)
            .ThenBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target.TextureId)
            .ToList();
    }

    private Control BuildTerrainProofTargetsDialogContent(Window dialog, ComboBox levelFilter, ComboBox kindFilter, ComboBox surfaceFilter, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Water, lava, goo, and solid-control RAM capture queue",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        StackPanel filters = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        filters.Children.Add(levelFilter);
        filters.Children.Add(kindFilter);
        filters.Children.Add(surfaceFilter);
        panel.Children.Add(filters);
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button close = NewButton("Close");
        Button select = NewButton("Select Face");
        Button companion = NewButton("Select Companion");
        close.Click += (_, _) => dialog.Close(null);
        select.Click += (_, _) =>
        {
            if (list.SelectedItem is TerrainProofTargetItem target)
                dialog.Close(new TerrainProofTargetSelection(target, false));
        };
        companion.Click += (_, _) =>
        {
            if (list.SelectedItem is TerrainProofTargetItem target)
                dialog.Close(new TerrainProofTargetSelection(target, true));
        };
        buttons.Children.Add(close);
        buttons.Children.Add(companion);
        buttons.Children.Add(select);
        panel.Children.Add(buttons);
        return panel;
    }

    private static bool MatchesTerrainProofKindFilter(TerrainProofTargetItem target, int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => target.CaptureKind == "hazard-touch",
            2 => target.CaptureKind == "solid-control",
            _ => true
        };
    }

    private bool MatchesTerrainProofLevelFilter(TerrainProofTargetItem target, int selectedIndex)
    {
        if (selectedIndex != 0 || _currentLevel == null)
            return true;

        return string.Equals(target.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTerrainProofSurfaceFilter(TerrainProofTargetItem target, int selectedIndex)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(target.Surface);
        return selectedIndex switch
        {
            1 => surface == "water",
            2 => surface == "lava",
            3 => surface == "ooze",
            4 => surface is "grass" or "sand" or "stone" or "ground",
            _ => true
        };
    }

    private IReadOnlyList<TerrainProofTargetItem> LoadTerrainProofTargets(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            using FileStream stream = File.OpenRead(path);
            TerrainProofTargetReport? report = JsonSerializer.Deserialize<TerrainProofTargetReport>(stream, NewJsonOptions());
            return OrderTerrainProofTargets(report?.Targets ?? []);
        }
        catch
        {
            return [];
        }
    }

    private async Task SelectTerrainProofTargetAsync(TerrainProofTargetItem target, bool useCompanion)
    {
        LevelDefinition? level = _catalog.FindByKey(target.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level for target {target.LevelKey}.";
            return;
        }

        if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(level))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);
        if (_currentGeometry == null)
        {
            _statusText.Text = $"Loaded {level.DisplayName}, but no geometry is available for target {target.RuntimeKey}.";
            return;
        }

        string runtimeKey = useCompanion ? target.NearestSolidOrHazardRuntimeKey : target.RuntimeKey;
        if (string.IsNullOrWhiteSpace(runtimeKey))
        {
            _statusText.Text = $"No companion face is listed for {target.DisplayName} {target.Surface} texture {target.TextureId}.";
            return;
        }

        int index = _currentGeometry.Polygons.FindIndex(polygon => string.Equals(polygon.RuntimeKey, runtimeKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _statusText.Text = $"Loaded {level.DisplayName}, but face {runtimeKey} was not found.";
            return;
        }

        _activeTerrainProofTarget = new TerrainProofTargetSelection(target, useCompanion);
        _viewport.FocusTerrain(index);
        string selectionKind = useCompanion ? "companion face" : "target face";
        string pairHint = string.IsNullOrWhiteSpace(target.NearestSolidOrHazardRuntimeKey)
            ? ""
            : " Use Proof Pair to jump between target and companion faces.";
        _statusText.Text = $"{target.DisplayName} {selectionKind} selected: {target.Surface} texture {target.TextureId}. {target.NextCapture}{pairHint}";
    }

    private static string EscapeMarkdownCell(string value)
    {
        return (value ?? "").Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private async Task RecolorSelectedTerrainTextureAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before recoloring terrain texture art.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which texture ID to recolor.";
            return;
        }

        if (!await ConfirmSharedTerrainTextureEditAsync("pick colors for this shared terrain texture", "Color Shared Texture"))
            return;

        ColorPalettePicker lowPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 0.58), "Current shadow");
        ColorPalettePicker highPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 1.28), "Current highlight");
        TextBlock details = new()
        {
            Text = $"Texture ID {_selectedTerrain.TextureId}\nThis generates a 64x64 PNG texture and stages it as a custom terrain import.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Recolor Terrain Texture",
            Width = 480,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainRecolorDialogContent(dialog, lowPicker, highPicker, details);

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        ColorRgba low = lowPicker.SelectedColor;
        ColorRgba high = highPicker.SelectedColor;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string fileName = $"{SafeFilePart(_currentLevel.Key)}-texture-{_selectedTerrain.TextureId:000}-recolor-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string stagedPath = Path.Combine(customDir, fileName);
        await TerrainTexturePngWriter.WriteGradientAsync(stagedPath, 64, 64, low, high);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            _selectedTerrain.TextureId,
            stagedPath,
            fileName,
            "both",
            64,
            "generated-palette",
            "Manual gradient",
            NormalizeHex(low),
            NormalizeHex(high));
        int previewFaces = ApplyCustomTerrainTexturePreviews();

        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"Generated recolored terrain art for texture ID {_selectedTerrain.TextureId} and previewed it on {previewFaces} face(s). Create Test BIN will include normal and close-detail texture patches.";
    }

    private async Task ImportSelectedTerrainPaletteAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before importing a terrain palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which texture ID to recolor.";
            return;
        }

        if (!await ConfirmSharedTerrainTextureEditAsync("import a palette over this shared terrain texture", "Import Shared Texture Palette"))
            return;

        IStorageProvider? storage = StorageProvider;
        if (storage == null)
        {
            _statusText.Text = "File picker is not available in this environment.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Import palette for texture ID {_selectedTerrain.TextureId}",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Palette files")
                {
                    Patterns = ["*.txt", "*.hex", "*.pal", "*.gpl", "*.json", "*.png"]
                },
                FilePickerFileTypes.All
            ]
        });

        string? sourcePath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        TerrainPaletteImport palette;
        try
        {
            palette = await TerrainPaletteImporter.ImportFileAsync(sourcePath);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not import terrain palette: {ex.Message}";
            return;
        }

        await StageSelectedTerrainTexturePaletteAsync(
            palette,
            Path.GetFileName(sourcePath),
            "imported-palette",
            "imported-palette",
            "Imported palette");
    }

    private async Task PasteSelectedTerrainPaletteAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before pasting a terrain palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which texture ID to recolor.";
            return;
        }

        if (!await ConfirmSharedTerrainTextureEditAsync("paste a palette over this shared terrain texture", "Paste Shared Texture Palette"))
            return;

        TerrainPaletteImport? palette = await ShowPasteTerrainPaletteDialogAsync(
            $"Paste palette for texture ID {_selectedTerrain.TextureId}",
            $"Texture ID {_selectedTerrain.TextureId}",
            "Pasted texture palette",
            "#18305C #78D2FA");
        if (palette == null)
            return;

        await StageSelectedTerrainTexturePaletteAsync(
            palette,
            "pasted-palette",
            "pasted-palette",
            "pasted-palette",
            "Pasted palette");
    }

    private async Task StageSelectedTerrainTexturePaletteAsync(TerrainPaletteImport palette, string sourceImageName, string sourceKind, string fileSlug, string statusVerb)
    {
        if (_currentLevel == null || _selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which texture ID to recolor.";
            return;
        }

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string fileName = $"{SafeFilePart(_currentLevel.Key)}-texture-{_selectedTerrain.TextureId:000}-{SafeFilePart(fileSlug)}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string stagedPath = Path.Combine(customDir, fileName);
        await TerrainTexturePngWriter.WritePaletteAsync(stagedPath, 64, 64, palette.Colors);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            _selectedTerrain.TextureId,
            stagedPath,
            sourceImageName,
            "both",
            64,
            sourceKind,
            palette.DisplayName,
            palette.LowHex,
            palette.HighHex,
            paletteHexColors: palette.Colors.Select(NormalizeHex).ToArray());
        int previewFaces = ApplyCustomTerrainTexturePreviews();

        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"{statusVerb} {palette.DisplayName} ({palette.ColorCount} color(s), {palette.LowHex}->{palette.HighHex}) for texture ID {_selectedTerrain.TextureId} and previewed {previewFaces} face(s). Create Test BIN will include the texture patches.";
    }

    private async Task RecolorSelectedTerrainFaceAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before recoloring one terrain face.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select the terrain face you want to recolor first.";
            return;
        }

        ColorPalettePicker lowPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 0.58), "Current shadow");
        ColorPalettePicker highPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 1.28), "Current highlight");
        TextBlock details = new()
        {
            Text = $"Selected face {_selectedTerrain.RuntimeKey}\nThe editor will give this face its own local texture slot first, so other faces using texture {_selectedTerrain.TextureId} keep their current art.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Recolor One Terrain Face",
            Width = 500,
            Height = 540,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainRecolorDialogContent(dialog, lowPicker, highPicker, details);

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        ColorRgba low = lowPicker.SelectedColor;
        ColorRgba high = highPicker.SelectedColor;
        await StageSelectedTerrainFaceGradientAsync(low, high, "generated-face-palette", "Face-local gradient", "recolor", "Recolored");
    }

    private async Task PaintSelectedTerrainFaceAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before painting terrain.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select the exact terrain face you want to paint first.";
            return;
        }

        ColorPalettePicker lowPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 0.58), "Current shadow");
        ColorPalettePicker highPicker = CreateTerrainColorPicker(ScaleColor(_selectedTerrain.SurfaceColor, 1.28), "Current highlight");
        List<TerrainTextureSwapChoice> inGameChoices = BuildTerrainTextureSwapChoices()
            .Where(choice => choice.TextureId != _selectedTerrain.TextureId)
            .OrderBy(choice => TerrainSummarySurfaceRank(choice.Surface))
            .ThenByDescending(choice => choice.FaceCount)
            .ThenBy(choice => choice.TextureId)
            .ToList();
        List<TerrainCrossLevelLookChoice> crossLevelChoices = BuildCrossLevelTerrainLookChoices(_selectedTerrain.Surface)
            .ToList();

        TextBlock message = NewSmallNote("");
        Window dialog = new()
        {
            Title = "Paint Selected Terrain Face",
            Width = 760,
            Height = 680,
            MinWidth = 620,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainPaintDialogContent(
            dialog,
            _selectedTerrain,
            lowPicker,
            highPicker,
            inGameChoices,
            crossLevelChoices,
            message);

        TerrainPaintDialogResult? result = await dialog.ShowDialog<TerrainPaintDialogResult?>(this);
        if (result == null)
            return;

        switch (result.Mode)
        {
            case TerrainPaintModeKind.CustomColors:
                await StageSelectedTerrainFaceGradientAsync(
                    result.LowColor,
                    result.HighColor,
                    "generated-face-palette",
                    "Painted face palette",
                    "paint",
                    "Painted");
                break;
            case TerrainPaintModeKind.InGameLook:
                if (result.InGameLook != null)
                    await ApplySelectedTerrainInGameLookAsync(result.InGameLook);
                break;
            case TerrainPaintModeKind.CrossLevelLook:
                if (result.CrossLevelLook != null)
                    await ApplySelectedTerrainCrossLevelLookAsync(result.CrossLevelLook);
                break;
            case TerrainPaintModeKind.ImportedPalette:
                await ImportSelectedTerrainFacePaletteAsync();
                break;
            case TerrainPaintModeKind.PastedPalette:
                if (result.Palette != null)
                {
                    await StageSelectedTerrainFacePaletteAsync(
                        result.Palette,
                        "paint-face-palette",
                        "pasted-face-palette",
                        "paint-palette",
                        "Painted");
                }
                break;
        }
    }

    private async Task StageSelectedTerrainFaceGradientAsync(ColorRgba low, ColorRgba high, string sourceKind, string paletteName, string fileSlug, string statusVerb)
    {
        TerrainFaceLocalTextureResult? local = await EnsureSelectedTerrainFaceLocalTextureAsync($"{statusVerb.ToLowerInvariant()} this terrain face");
        if (local == null || _selectedTerrain == null || _currentLevel == null)
            return;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string fileName = $"{SafeFilePart(_currentLevel.Key)}-face-{SafeFilePart(_selectedTerrain.RuntimeKey)}-texture-{local.TextureId:000}-{SafeFilePart(fileSlug)}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string stagedPath = Path.Combine(customDir, fileName);
        await TerrainTexturePngWriter.WriteGradientAsync(stagedPath, 64, 64, low, high);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            local.TextureId,
            stagedPath,
            fileName,
            "both",
            64,
            sourceKind,
            paletteName,
            NormalizeHex(low),
            NormalizeHex(high));
        int previewFaces = ApplyCustomTerrainTexturePreviews();
        int savedEdits = await PersistCurrentTerrainEditsAsync();

        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"{statusVerb} one terrain face using local texture {local.TextureId} ({local.SourceSummary}); previewed {previewFaces} face(s) and saved {savedEdits} terrain edit(s). Create Test BIN will patch this face plus its custom texture art.";
    }

    private async Task ImportSelectedTerrainFacePaletteAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before importing a face palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select the terrain face you want to recolor first.";
            return;
        }

        IStorageProvider? storage = StorageProvider;
        if (storage == null)
        {
            _statusText.Text = "File picker is not available in this environment.";
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Import palette for selected face {_selectedTerrain.RuntimeKey}",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Palette files")
                {
                    Patterns = ["*.txt", "*.hex", "*.pal", "*.gpl", "*.json", "*.png"]
                },
                FilePickerFileTypes.All
            ]
        });

        string? sourcePath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        TerrainPaletteImport palette;
        try
        {
            palette = await TerrainPaletteImporter.ImportFileAsync(sourcePath);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not import terrain palette: {ex.Message}";
            return;
        }

        await StageSelectedTerrainFacePaletteAsync(
            palette,
            Path.GetFileName(sourcePath),
            "imported-face-palette",
            "imported-palette",
            "Imported");
    }

    private async Task PasteSelectedTerrainFacePaletteAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before pasting a face palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select the terrain face you want to recolor first.";
            return;
        }

        TerrainPaletteImport? palette = await ShowPasteTerrainPaletteDialogAsync(
            $"Paste palette for selected face {_selectedTerrain.RuntimeKey}",
            $"Selected face {_selectedTerrain.RuntimeKey}",
            "Pasted face palette",
            "#305830 #69C169");
        if (palette == null)
            return;

        await StageSelectedTerrainFacePaletteAsync(
            palette,
            "pasted-face-palette",
            "pasted-face-palette",
            "pasted-palette",
            "Pasted");
    }

    private async Task StageSelectedTerrainFacePaletteAsync(TerrainPaletteImport palette, string sourceImageName, string sourceKind, string fileSlug, string statusVerb)
    {
        TerrainFaceLocalTextureResult? local = await EnsureSelectedTerrainFaceLocalTextureAsync($"{statusVerb.ToLowerInvariant()} a palette for this terrain face");
        if (local == null || _selectedTerrain == null || _currentLevel == null)
            return;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string fileName = $"{SafeFilePart(_currentLevel.Key)}-face-{SafeFilePart(_selectedTerrain.RuntimeKey)}-texture-{local.TextureId:000}-{SafeFilePart(fileSlug)}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string stagedPath = Path.Combine(customDir, fileName);
        await TerrainTexturePngWriter.WritePaletteAsync(stagedPath, 64, 64, palette.Colors);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            local.TextureId,
            stagedPath,
            sourceImageName,
            "both",
            64,
            sourceKind,
            palette.DisplayName,
            palette.LowHex,
            palette.HighHex,
            paletteHexColors: palette.Colors.Select(NormalizeHex).ToArray());
        int previewFaces = ApplyCustomTerrainTexturePreviews();
        int savedEdits = await PersistCurrentTerrainEditsAsync();

        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"{statusVerb} {palette.DisplayName} for one terrain face using local texture {local.TextureId} ({local.SourceSummary}); previewed {previewFaces} face(s) and saved {savedEdits} terrain edit(s). Create Test BIN will include this face-local palette.";
    }

    private async Task<TerrainPaletteImport?> ShowPasteTerrainPaletteDialogAsync(string title, string target, string defaultName, string defaultPaletteText)
    {
        TextBox nameBox = new()
        {
            Text = defaultName,
            MinWidth = 220
        };
        TextBox paletteBox = new()
        {
            Text = defaultPaletteText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };
        TextBlock message = NewSmallNote("Paste at least two colors. Supported formats include #RRGGBB values, space-separated hex values, or RGB rows like 24 48 92.");
        TerrainPaletteImport? parsed = null;
        Window dialog = new()
        {
            Title = title,
            Width = 560,
            Height = 480,
            MinWidth = 460,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildPasteTerrainPaletteDialogContent(dialog, nameBox, paletteBox, message, target, () =>
        {
            try
            {
                parsed = TerrainPaletteImporter.ImportText(paletteBox.Text ?? "", nameBox.Text ?? defaultName);
                dialog.Close(true);
            }
            catch (Exception ex)
            {
                parsed = null;
                message.Foreground = new SolidColorBrush(Color.FromRgb(160, 48, 48));
                message.Text = $"Could not parse palette: {ex.Message}";
            }
        });

        bool accepted = await dialog.ShowDialog<bool>(this);
        return accepted ? parsed : null;
    }

    private static Control BuildPasteTerrainPaletteDialogContent(Window dialog, TextBox nameBox, TextBox paletteBox, TextBlock message, string target, Action apply)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Name", nameBox, 0);
        AddLabeledField(fields, "Colors", paletteBox, 1);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = target,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        panel.Children.Add(fields);
        panel.Children.Add(message);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button applyButton = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        applyButton.Click += (_, _) => apply();
        buttons.Children.Add(cancel);
        buttons.Children.Add(applyButton);
        panel.Children.Add(buttons);

        return panel;
    }

    private static string BuildCustomTerrainTextureSummary(CustomTerrainTextureImport texture)
    {
        string palette = string.IsNullOrWhiteSpace(texture.PaletteLowHex) || string.IsNullOrWhiteSpace(texture.PaletteHighHex)
            ? ""
            : $", palette {texture.PaletteLowHex}->{texture.PaletteHighHex}";
        string paletteCount = texture.PaletteHexColors?.Count > 2
            ? $", {texture.PaletteHexColors.Count} colors"
            : "";
        string name = string.IsNullOrWhiteSpace(texture.PaletteName) ? "" : $", {texture.PaletteName}";
        string kind = string.IsNullOrWhiteSpace(texture.SourceKind) ? "imported-image" : texture.SourceKind;
        return $"{texture.SourceImageName} ({kind}{name}{paletteCount}, {texture.DescriptorTier}, {texture.TileSize}x{texture.TileSize}{palette})";
    }

    private static string NormalizeHex(ColorRgba color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private async Task<TerrainFaceLocalTextureResult?> EnsureSelectedTerrainFaceLocalTextureAsync(string action)
    {
        if (_currentLevel == null || _currentGeometry == null || _selectedTerrain == null)
        {
            _statusText.Text = "Select a terrain face first.";
            return null;
        }

        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (string.IsNullOrWhiteSpace(sourceImage) || !File.Exists(sourceImage))
        {
            _statusText.Text = "Choose your Spyro BIN/CUE first so the editor can find a safe local texture slot.";
            return null;
        }

        IReadOnlyList<TerrainTextureSlot> slots;
        try
        {
            slots = TerrainPatchExporter.InspectTextureSlots(sourceImage, _currentLevel);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not inspect this level's texture slots: {ex.Message}";
            return null;
        }

        Dictionary<int, int> textureUseCounts = _currentGeometry.Polygons
            .Where(polygon => !polygon.IsTerrainRemoved && polygon.TextureId >= 0)
            .GroupBy(polygon => polygon.TextureId)
            .ToDictionary(group => group.Key, group => group.Count());

        bool CurrentSlotIsUsable()
        {
            TerrainTextureSlot? current = slots.FirstOrDefault(slot => slot.TextureId == _selectedTerrain.TextureId);
            return current != null && current.HasNormalDescriptors && current.HasCloseDescriptors;
        }

        textureUseCounts.TryGetValue(_selectedTerrain.TextureId, out int currentUseCount);
        if (_selectedTerrain.HasTextureEdit && currentUseCount <= 1 && CurrentSlotIsUsable())
        {
            return new TerrainFaceLocalTextureResult(
                _selectedTerrain.TextureId,
                _selectedTerrain.OriginalTextureId,
                Math.Max(1, currentUseCount),
                "reusing this face's local texture slot");
        }

        HashSet<int> usedTextureIds = textureUseCounts.Keys.ToHashSet();
        foreach (CustomTerrainTextureImport import in _customTerrainTextures)
            usedTextureIds.Add(import.TextureId);

        TerrainTextureSlot? slot = TerrainPatchExporter.FindUnusedTextureSlot(sourceImage, _currentLevel, usedTextureIds, preferBothDescriptorTiers: true);
        if (slot == null)
        {
            if (currentUseCount <= 1 && CurrentSlotIsUsable())
            {
                return new TerrainFaceLocalTextureResult(
                    _selectedTerrain.TextureId,
                    _selectedTerrain.OriginalTextureId,
                    Math.Max(1, currentUseCount),
                    "reusing the selected face's already-unique texture");
            }

            var usage = GetCustomTerrainTextureUsage();
            string cleanupHint = usage.UnusedImports > 0
                ? $" Clear Unused Art can free {usage.UnusedImports} unused custom import(s) first."
                : "";
            _statusText.Text = $"Could not find an unused texture slot to {action}. Try a different level or use the shared texture color tools for now.{cleanupHint}";
            return null;
        }

        int originalTextureId = _selectedTerrain.TextureId;
        string originalSurface = _selectedTerrain.Surface;
        _selectedTerrain.ApplyTextureOverride(slot.TextureId);
        if (!string.IsNullOrWhiteSpace(originalSurface))
            await TerrainMaterialClassifier.SaveOverrideAsync(_currentLevel.Key, _workspace.RootPath, slot.TextureId, originalSurface);

        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        await PersistCurrentTerrainEditsAsync();
        return new TerrainFaceLocalTextureResult(
            slot.TextureId,
            originalTextureId,
            Math.Max(1, currentUseCount),
            $"copied face from shared texture {originalTextureId} ({Math.Max(1, currentUseCount)} face(s)) to unused texture {slot.TextureId}");
    }

    private static ColorPalettePicker CreateTerrainColorPicker(ColorRgba initialColor, string initialName)
    {
        List<TerrainColorOption> options = [new(initialName, initialColor)];
        options.AddRange(TerrainColorPaletteOptions.Where(option => option.Color != initialColor));
        return new ColorPalettePicker(options);
    }

    private static Control BuildColorPaletteItem(TerrainColorOption? option)
    {
        if (option == null)
            return new TextBlock();

        StackPanel row = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        row.Children.Add(new Border
        {
            Width = 26,
            Height = 18,
            CornerRadius = new CornerRadius(3),
            BorderBrush = new SolidColorBrush(Color.FromRgb(92, 102, 112)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(ToAvaloniaColor(option.Color))
        });
        row.Children.Add(new TextBlock
        {
            Text = option.Name,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        });
        return row;
    }

    private static Color ToAvaloniaColor(ColorRgba color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private async Task ApplySurfaceTerrainPaletteAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before applying a terrain palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first so the editor knows which surface to recolor.";
            return;
        }

        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
        ComboBox presetBox = new()
        {
            ItemsSource = TerrainPalettePresetCatalog.Presets,
            SelectedItem = TerrainPalettePresetCatalog.DefaultForSurface(surface),
            MinWidth = 190
        };
        TerrainPalettePreset initialPreset = presetBox.SelectedItem as TerrainPalettePreset ?? TerrainPalettePresetCatalog.DefaultForSurface(surface);
        ColorPalettePicker lowPicker = CreateTerrainColorPicker(initialPreset.Low, $"{initialPreset.DisplayName} shadow");
        ColorPalettePicker highPicker = CreateTerrainColorPicker(initialPreset.High, $"{initialPreset.DisplayName} highlight");
        presetBox.SelectionChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is not TerrainPalettePreset preset)
                return;

            lowPicker.SetSelectedColor(preset.Low, $"{preset.DisplayName} shadow");
            highPicker.SetSelectedColor(preset.High, $"{preset.DisplayName} highlight");
        };
        CheckBox allSurfaceBox = new()
        {
            Content = $"Apply to all {TerrainMaterialClassifier.FormatSurface(surface)} textures in this level",
            IsChecked = true
        };
        TextBlock details = new()
        {
            Text = $"Selected texture {_selectedTerrain.TextureId} on {TerrainMaterialClassifier.FormatSurface(surface)}.\nWhen this level already has a matching target material texture, the face texture IDs are switched too; generated PNGs keep palette metadata.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Apply Surface Palette",
            Width = 520,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildSurfacePaletteDialogContent(dialog, presetBox, lowPicker, highPicker, allSurfaceBox, details);

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        TerrainPalettePreset preset = presetBox.SelectedItem as TerrainPalettePreset ?? TerrainPalettePresetCatalog.DefaultForSurface(surface);
        ColorRgba low = lowPicker.SelectedColor;
        ColorRgba high = highPicker.SelectedColor;

        bool customColors = !string.Equals(NormalizeHex(low), NormalizeHex(preset.Low), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(NormalizeHex(high), NormalizeHex(preset.High), StringComparison.OrdinalIgnoreCase);
        string paletteName = customColors ? $"{preset.DisplayName} custom" : preset.DisplayName;
        string targetSurface = TerrainMaterialClassifier.NormalizeSurfaceName(preset.Surface);
        bool applyAllSurface = allSurfaceBox.IsChecked == true;
        List<TerrainPolygon> targetFaces = applyAllSurface
            ? _currentGeometry.Polygons
                .Where(polygon => string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface), surface, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : [_selectedTerrain];
        List<int> sourceTextureIds = targetFaces
            .Select(polygon => polygon.TextureId)
            .Where(textureId => textureId >= 0)
            .Distinct()
            .OrderBy(textureId => textureId)
            .ToList();

        if (sourceTextureIds.Count == 0)
        {
            _statusText.Text = "No matching terrain textures were found for that palette.";
            return;
        }

        int? targetTextureId = FindTextureIdForSurface(targetSurface, sourceTextureIds);
        bool switchedTextureIds = targetTextureId.HasValue && !string.Equals(targetSurface, surface, StringComparison.OrdinalIgnoreCase);
        if (switchedTextureIds)
        {
            int textureId = targetTextureId.GetValueOrDefault();
            foreach (TerrainPolygon face in targetFaces)
                face.ApplyTextureOverride(textureId);
        }
        else if (!string.Equals(targetSurface, surface, StringComparison.OrdinalIgnoreCase))
        {
            foreach (int textureId in sourceTextureIds)
                await TerrainMaterialClassifier.SaveOverrideAsync(_currentLevel.Key, _workspace.RootPath, textureId, targetSurface);
        }

        List<int> paletteTextureIds = switchedTextureIds
            ? [targetTextureId!.Value]
            : sourceTextureIds;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        foreach (int textureId in paletteTextureIds)
        {
            string fileName = $"{SafeFilePart(_currentLevel.Key)}-texture-{textureId:000}-{SafeFilePart(preset.Id)}-{stamp}.png";
            string stagedPath = Path.Combine(customDir, fileName);
            await TerrainTexturePngWriter.WriteGradientAsync(stagedPath, 64, 64, low, high);
            _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
                _workspace.RootPath,
                _currentLevel.Key,
                _currentLevel.DisplayName,
                textureId,
                stagedPath,
                fileName,
                "both",
                64,
                "generated-surface-palette",
                paletteName,
                NormalizeHex(low),
                NormalizeHex(high));
        }

        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        int previewFaces = ApplyCustomTerrainTexturePreviews();
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        _loadedTerrainEdits = await TerrainEditStore.SaveAsync(terrainEditsPath, _currentGeometry.Polygons, _currentLevel.DisplayName);
        _savedTerrainEditSignature = BuildTerrainEditSignature(_currentGeometry);
        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        string swapText = switchedTextureIds
            ? $" Switched {targetFaces.Count} face(s) from {TerrainMaterialClassifier.FormatSurface(surface)} texture(s) to {TerrainMaterialClassifier.FormatSurface(targetSurface)} texture {targetTextureId.GetValueOrDefault()}."
            : !string.Equals(targetSurface, surface, StringComparison.OrdinalIgnoreCase)
            ? $" No existing {TerrainMaterialClassifier.FormatSurface(targetSurface)} texture was found, so this is an editor/material-label and texture-art change only for now."
            : "";
        _statusText.Text = $"Applied {paletteName} to {paletteTextureIds.Count} terrain texture(s), saved {_loadedTerrainEdits} terrain edit(s), and previewed {previewFaces} face(s).{swapText} Create Test BIN will include the texture patches.";
    }

    private async Task ApplySelectedTerrainInGamePaletteAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before using an in-game terrain palette.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select the terrain face you want to change first.";
            return;
        }

        List<TerrainTextureSwapChoice> choices = BuildTerrainTextureSwapChoices()
            .Where(choice => choice.TextureId != _selectedTerrain.TextureId)
            .OrderBy(choice => TerrainSummarySurfaceRank(choice.Surface))
            .ThenByDescending(choice => choice.FaceCount)
            .ThenBy(choice => choice.TextureId)
            .ToList();
        if (choices.Count == 0)
        {
            _statusText.Text = "No other in-game terrain textures are available in this level.";
            return;
        }

        List<TerrainTextureSurfaceFilter> filters =
        [
            new TerrainTextureSurfaceFilter("", $"All looks ({choices.Count} textures)")
        ];
        filters.AddRange(choices
            .GroupBy(choice => choice.Surface)
            .OrderBy(group => TerrainSummarySurfaceRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new TerrainTextureSurfaceFilter(
                group.Key,
                $"{TerrainMaterialClassifier.FormatSurface(group.Key)} ({group.Count()} texture(s), {group.Sum(choice => choice.FaceCount)} faces)")));
        ComboBox filterBox = new()
        {
            ItemsSource = filters,
            SelectedIndex = 0,
            MinWidth = 220
        };

        ListBox list = new()
        {
            MinHeight = 300,
            ItemTemplate = new FuncDataTemplate<TerrainTextureSwapChoice>((item, _) =>
            {
                if (item == null)
                    return new TextBlock();

                Grid row = new()
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(30)),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Margin = new Thickness(4, 3)
                };
                row.Children.Add(new Border
                {
                    Width = 24,
                    Height = 18,
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(ToAvaloniaColor(item.Color))
                });
                AddGridControl(row, new TextBlock
                {
                    Text = item.ListText,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
                }, 1, 0);
                return row;
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 116
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not TerrainTextureSwapChoice choice)
            {
                details.Text = "Choose the in-game look this selected terrain face should borrow.";
                return;
            }

            string custom = choice.HasCustomTexture
                ? "This target texture already has custom art staged."
                : "This uses the original in-game texture art.";
            details.Text =
                $"Selected face {_selectedTerrain.RuntimeKey}: texture {_selectedTerrain.TextureId} -> {choice.TextureId}\n" +
                $"Borrowed look: {TerrainMaterialClassifier.FormatSurface(choice.Surface)}, {choice.FaceCount} face(s), proof {choice.ProofSummary}\n" +
                $"Behavior: {choice.BehaviorSummary}\n" +
                $"{custom} Representative face: {choice.RuntimeKey} at {choice.X:0.##}, {choice.Y:0.##}, {choice.Z:0.##}";
        }

        void RefreshChoices()
        {
            int? previousTexture = (list.SelectedItem as TerrainTextureSwapChoice)?.TextureId;
            string filter = filterBox.SelectedItem is TerrainTextureSurfaceFilter surfaceFilter
                ? surfaceFilter.Surface
                : "";
            List<TerrainTextureSwapChoice> visible = string.IsNullOrWhiteSpace(filter)
                ? choices
                : choices.Where(choice => string.Equals(choice.Surface, filter, StringComparison.OrdinalIgnoreCase)).ToList();
            list.ItemsSource = visible;
            if (visible.Count == 0)
            {
                list.SelectedIndex = -1;
                RefreshDetails();
                return;
            }

            int index = previousTexture.HasValue
                ? visible.FindIndex(choice => choice.TextureId == previousTexture.Value)
                : 0;
            list.SelectedIndex = index >= 0 ? index : 0;
            RefreshDetails();
        }

        filterBox.SelectionChanged += (_, _) => RefreshChoices();
        list.SelectionChanged += (_, _) => RefreshDetails();
        RefreshChoices();

        Window dialog = new()
        {
            Title = "Use In-Game Terrain Palette",
            Width = 700,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainTextureSwapDialogContent(dialog, filterBox, list, details);

        TerrainTextureSwapChoice? selected = await dialog.ShowDialog<TerrainTextureSwapChoice?>(this);
        if (selected == null || _selectedTerrain == null || _currentGeometry == null || _currentLevel == null)
            return;

        await ApplySelectedTerrainInGameLookAsync(selected);
    }

    private async Task ApplySelectedTerrainInGameLookAsync(TerrainTextureSwapChoice selected)
    {
        if (_selectedTerrain == null || _currentGeometry == null || _currentLevel == null)
            return;

        int originalTextureId = _selectedTerrain.TextureId;
        _selectedTerrain.ApplyTextureOverride(selected.TextureId);
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        int savedEdits = await PersistCurrentTerrainEditsAsync();
        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"Applied {TerrainMaterialClassifier.FormatSurface(selected.Surface)} in-game palette/texture {selected.TextureId} to one face (was texture {originalTextureId}) and saved {savedEdits} terrain edit(s). Create Test BIN will patch this face's texture id.";
    }

    private async Task ApplySelectedTerrainCrossLevelLookAsync(TerrainCrossLevelLookChoice selected)
    {
        TerrainFaceLocalTextureResult? local = await EnsureSelectedTerrainFaceLocalTextureAsync($"borrow {selected.LevelName} terrain art for this face");
        if (local == null || _selectedTerrain == null || _currentLevel == null || _currentGeometry == null)
            return;

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string sourceSlug = $"{selected.LevelKey}-{selected.Surface}-tex-{selected.TextureId}";
        string stagedPath;
        string sourceImageName;
        string sourceKind;
        string paletteName = $"{selected.LevelName} {TerrainMaterialClassifier.FormatSurface(selected.Surface)} look";
        string lowHex = "";
        string highHex = "";
        IReadOnlyList<string> paletteHexColors = Array.Empty<string>();
        string sourceDetail;

        if (!string.IsNullOrWhiteSpace(selected.CustomSourceImagePath) && File.Exists(selected.CustomSourceImagePath))
        {
            string extension = Path.GetExtension(selected.CustomSourceImagePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            string fileName = $"{SafeFilePart(_currentLevel.Key)}-face-{SafeFilePart(_selectedTerrain.RuntimeKey)}-texture-{local.TextureId:000}-borrow-{SafeFilePart(sourceSlug)}-{stamp}{extension.ToLowerInvariant()}";
            stagedPath = Path.Combine(customDir, fileName);
            File.Copy(selected.CustomSourceImagePath, stagedPath, true);
            sourceImageName = string.IsNullOrWhiteSpace(selected.CustomSourceImageName)
                ? Path.GetFileName(selected.CustomSourceImagePath)
                : selected.CustomSourceImageName;
            sourceKind = "borrowed-cross-level-custom-art";
            paletteName = string.IsNullOrWhiteSpace(selected.CustomPaletteName)
                ? paletteName
                : $"{paletteName}: {selected.CustomPaletteName}";
            lowHex = selected.CustomPaletteLowHex;
            highHex = selected.CustomPaletteHighHex;
            paletteHexColors = selected.CustomPaletteHexColors;
            sourceDetail = "copied from source custom art";
        }
        else
        {
            ColorRgba low = ScaleColor(selected.Color, 0.58);
            ColorRgba high = ScaleColor(selected.Color, 1.28);
            string fileName = $"{SafeFilePart(_currentLevel.Key)}-face-{SafeFilePart(_selectedTerrain.RuntimeKey)}-texture-{local.TextureId:000}-borrow-{SafeFilePart(sourceSlug)}-{stamp}.png";
            stagedPath = Path.Combine(customDir, fileName);
            sourceImageName = fileName;
            lowHex = NormalizeHex(low);
            highHex = NormalizeHex(high);
            paletteHexColors = [lowHex, highHex];

            TerrainTextureImageExport? exportedTexture = null;
            LevelDefinition? sourceLevel = _catalog.FindByKey(selected.LevelKey);
            string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
            if (sourceLevel != null && File.Exists(sourceImage))
            {
                try
                {
                    exportedTexture = await TerrainPatchExporter.TryExportTerrainTextureImageAsync(
                        sourceImage,
                        sourceLevel,
                        selected.TextureId,
                        stagedPath);
                }
                catch
                {
                    exportedTexture = null;
                }
            }

            if (exportedTexture != null)
            {
                sourceKind = "borrowed-cross-level-texture-art";
                paletteName = $"{paletteName}: {exportedTexture.Width}x{exportedTexture.Height} {exportedTexture.DescriptorTier}";
                sourceDetail = $"extracted actual {selected.LevelName} texture art ({exportedTexture.PixelCount} pixel(s))";
            }
            else
            {
                await TerrainTexturePngWriter.WriteGradientAsync(stagedPath, 64, 64, low, high);
                sourceKind = "generated-cross-level-look";
                sourceDetail = "generated from the source face color";
            }
        }

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            local.TextureId,
            stagedPath,
            sourceImageName,
            "both",
            64,
            sourceKind,
            paletteName,
            lowHex,
            highHex,
            paletteHexColors: paletteHexColors);

        await TerrainMaterialClassifier.SaveOverrideAsync(_currentLevel.Key, _workspace.RootPath, local.TextureId, selected.Surface);
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        int previewFaces = ApplyCustomTerrainTexturePreviews();
        int savedEdits = await PersistCurrentTerrainEditsAsync();

        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"Borrowed {TerrainMaterialClassifier.FormatSurface(selected.Surface)} from {selected.LevelName} texture {selected.TextureId} onto one local face texture {local.TextureId} ({sourceDetail}); previewed {previewFaces} face(s) and saved {savedEdits} terrain edit(s). Create Test BIN will patch this face and its texture art.";
    }

    private IReadOnlyList<TerrainTextureSwapChoice> BuildTerrainTextureSwapChoices()
    {
        if (_currentLevel == null || _currentGeometry == null)
            return Array.Empty<TerrainTextureSwapChoice>();

        TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(_workspace.RootPath, _currentLevel.Key);
        HashSet<int> customTextureIds = _customTerrainTextures.Select(texture => texture.TextureId).ToHashSet();
        List<TerrainTextureSwapChoice> choices = new();
        foreach (IGrouping<int, TerrainPolygon> group in _currentGeometry.Polygons
            .Where(polygon => !polygon.IsTerrainRemoved && polygon.TextureId >= 0)
            .GroupBy(polygon => polygon.TextureId))
        {
            List<TerrainPolygon> faces = group.ToList();
            TerrainPolygon representative = faces
                .OrderByDescending(face => face.Points.Count)
                .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                .First();
            string surface = faces
                .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                .OrderByDescending(surfaceGroup => surfaceGroup.Count())
                .ThenBy(surfaceGroup => surfaceGroup.Key, StringComparer.OrdinalIgnoreCase)
                .Select(surfaceGroup => surfaceGroup.Key)
                .FirstOrDefault("unknown");
            List<TerrainBehaviorRule> rules = proofFile.Rules
                .Where(rule => rule.TextureId == group.Key || string.Equals(rule.Surface, surface, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(rule => RuleConfidenceRank(rule.Confidence))
                .ThenBy(rule => rule.TextureId)
                .ToList();

            choices.Add(new TerrainTextureSwapChoice
            {
                Surface = surface,
                TextureId = group.Key,
                FaceCount = faces.Count,
                BehaviorSummary = BuildBehaviorSummary(faces),
                ProofSummary = BuildProofRuleSummary(rules),
                ProofStatusRank = rules.Count == 0 ? 0 : rules.Max(rule => RuleConfidenceRank(rule.Confidence)),
                HasCustomTexture = customTextureIds.Contains(group.Key),
                RuntimeKey = representative.RuntimeKey,
                X = representative.Center.X,
                Y = representative.Center.Y,
                Z = representative.AvgZ,
                Color = representative.SurfaceColor
            });
        }

        return choices;
    }

    private IReadOnlyList<TerrainCrossLevelLookChoice> BuildCrossLevelTerrainLookChoices(string preferredSurface)
    {
        if (_currentLevel == null || _catalog.Levels.Count == 0)
            return Array.Empty<TerrainCrossLevelLookChoice>();

        string preferred = TerrainMaterialClassifier.NormalizeSurfaceName(preferredSurface);
        List<TerrainCrossLevelLookChoice> choices = new();
        foreach (LevelDefinition level in _catalog.Levels)
        {
            if (string.Equals(LevelCatalog.NormalizeKey(level.Key), LevelCatalog.NormalizeKey(_currentLevel.Key), StringComparison.OrdinalIgnoreCase))
                continue;

            string? overlayPath = FindCachedTerrainOverlayPath(level.Key);
            if (string.IsNullOrWhiteSpace(overlayPath))
                continue;

            GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayPath);
            if (healthIssue?.BlocksLoading == true)
                continue;

            GeometryCandidate geometry;
            try
            {
                geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            }
            catch
            {
                continue;
            }

            string editsPath = Path.Combine(_workspace.RootPath, $"{level.Key}-terrain-edits.json");
            TerrainEditStore.Load(editsPath, geometry.Polygons);
            TerrainMaterialClassifier.Apply(level.Key, _workspace.RootPath, geometry);
            IReadOnlyList<CustomTerrainTextureImport> customImports = CustomTerrainTextureStore.Load(_workspace.RootPath, level.Key);
            TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(_workspace.RootPath, level.Key);
            foreach (IGrouping<int, TerrainPolygon> group in geometry.Polygons
                .Where(polygon => !polygon.IsTerrainRemoved && polygon.TextureId >= 0)
                .GroupBy(polygon => polygon.TextureId))
            {
                List<TerrainPolygon> faces = group.ToList();
                TerrainPolygon representative = faces
                    .OrderByDescending(face => face.Points.Count)
                    .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                    .First();
                string surface = faces
                    .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                    .OrderByDescending(surfaceGroup => surfaceGroup.Count())
                    .ThenBy(surfaceGroup => surfaceGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(surfaceGroup => surfaceGroup.Key)
                    .FirstOrDefault("unknown");
                List<TerrainBehaviorRule> rules = proofFile.Rules
                    .Where(rule => rule.TextureId == group.Key || string.Equals(rule.Surface, surface, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(rule => RuleConfidenceRank(rule.Confidence))
                    .ThenBy(rule => rule.TextureId)
                    .ToList();
                CustomTerrainTextureImport? customImport = customImports
                    .Where(import => import.TextureId == group.Key)
                    .FirstOrDefault(import => File.Exists(import.SourceImagePath));

                choices.Add(new TerrainCrossLevelLookChoice
                {
                    LevelKey = level.Key,
                    LevelName = level.DisplayName,
                    Surface = surface,
                    TextureId = group.Key,
                    FaceCount = faces.Count,
                    BehaviorSummary = BuildBehaviorSummary(faces),
                    ProofSummary = BuildProofRuleSummary(rules),
                    ProofStatusRank = rules.Count == 0 ? 0 : rules.Max(rule => RuleConfidenceRank(rule.Confidence)),
                    HasCustomTexture = customImport != null,
                    RuntimeKey = representative.RuntimeKey,
                    X = representative.Center.X,
                    Y = representative.Center.Y,
                    Z = representative.AvgZ,
                    Color = representative.SurfaceColor,
                    PreferredSurfaceRank = string.Equals(surface, preferred, StringComparison.OrdinalIgnoreCase) ? 0 : 1,
                    CustomSourceImagePath = customImport?.SourceImagePath ?? "",
                    CustomSourceImageName = customImport?.SourceImageName ?? "",
                    CustomPaletteName = customImport?.PaletteName ?? "",
                    CustomPaletteLowHex = customImport?.PaletteLowHex ?? "",
                    CustomPaletteHighHex = customImport?.PaletteHighHex ?? "",
                    CustomPaletteHexColors = customImport?.PaletteHexColors ?? Array.Empty<string>()
                });
            }
        }

        return choices
            .OrderBy(choice => choice.PreferredSurfaceRank)
            .ThenBy(choice => TerrainSummarySurfaceRank(choice.Surface))
            .ThenByDescending(choice => choice.ProofStatusRank)
            .ThenByDescending(choice => choice.FaceCount)
            .ThenBy(choice => choice.LevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(choice => choice.TextureId)
            .ToArray();
    }

    private string? FindCachedTerrainOverlayPath(string levelKey)
    {
        string cachePath = Path.Combine(_workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        if (File.Exists(cachePath))
            return cachePath;

        string sourceDerivedPath = SourceDerivedTerrainOverlayPath(levelKey);
        if (File.Exists(sourceDerivedPath))
            return sourceDerivedPath;

        string generatedPath = _workspace.ResolveFile($"{levelKey}-runtime-scene-editor-overlay.json", "generated-research");
        return File.Exists(generatedPath) ? generatedPath : null;
    }

    private static Control BuildTerrainTextureSwapDialogContent(Window dialog, ComboBox filterBox, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Choose an existing terrain look from this level",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        Grid filterRow = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        AddGridControl(filterRow, new TextBlock
        {
            Text = "Show",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        }, 0, 0);
        AddGridControl(filterRow, filterBox, 1, 0);
        panel.Children.Add(filterRow);
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply To Face");
        cancel.Click += (_, _) => dialog.Close(null);
        apply.Click += (_, _) =>
        {
            if (list.SelectedItem is TerrainTextureSwapChoice choice)
                dialog.Close(choice);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private static Control BuildTerrainPaintDialogContent(
        Window dialog,
        TerrainPolygon selectedTerrain,
        ColorPalettePicker lowPicker,
        ColorPalettePicker highPicker,
        IReadOnlyList<TerrainTextureSwapChoice> inGameChoices,
        IReadOnlyList<TerrainCrossLevelLookChoice> crossLevelChoices,
        TextBlock message)
    {
        ComboBox modeBox = new()
        {
            ItemsSource = TerrainPaintModeOption.All,
            SelectedIndex = 0,
            MinWidth = 240,
            MinHeight = 34
        };

        ListBox lookList = new()
        {
            ItemsSource = inGameChoices,
            MinHeight = 210,
            ItemTemplate = new FuncDataTemplate<TerrainTextureSwapChoice>((item, _) =>
            {
                if (item == null)
                    return new TextBlock();

                Grid row = new()
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(30)),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Margin = new Thickness(4, 3)
                };
                row.Children.Add(new Border
                {
                    Width = 24,
                    Height = 18,
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(ToAvaloniaColor(item.Color))
                });
                AddGridControl(row, new TextBlock
                {
                    Text = item.ListText,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
                }, 1, 0);
                return row;
            })
        };
        if (inGameChoices.Count > 0)
            lookList.SelectedIndex = 0;

        TextBlock lookDetails = NewSmallNote("");
        void RefreshLookDetails()
        {
            if (lookList.SelectedItem is not TerrainTextureSwapChoice choice)
            {
                lookDetails.Text = inGameChoices.Count == 0
                    ? "No other in-game terrain looks were found in this level."
                    : "Choose an in-game look to borrow for this selected face.";
                return;
            }

            string custom = choice.HasCustomTexture
                ? "This target look already has custom art staged."
                : "This target look uses original in-game art.";
            lookDetails.Text =
                $"Texture {selectedTerrain.TextureId} -> {choice.TextureId}; {TerrainMaterialClassifier.FormatSurface(choice.Surface)}, {choice.FaceCount} face(s), proof {choice.ProofSummary}. {custom}";
        }
        lookList.SelectionChanged += (_, _) => RefreshLookDetails();
        RefreshLookDetails();

        ListBox crossLevelList = new()
        {
            ItemsSource = crossLevelChoices,
            MinHeight = 230,
            ItemTemplate = new FuncDataTemplate<TerrainCrossLevelLookChoice>((item, _) =>
            {
                if (item == null)
                    return new TextBlock();

                Grid row = new()
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(30)),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Margin = new Thickness(4, 3)
                };
                row.Children.Add(new Border
                {
                    Width = 24,
                    Height = 18,
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(ToAvaloniaColor(item.Color))
                });
                AddGridControl(row, new TextBlock
                {
                    Text = item.ListText,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
                }, 1, 0);
                return row;
            })
        };
        if (crossLevelChoices.Count > 0)
            crossLevelList.SelectedIndex = 0;

        TextBlock crossLevelDetails = NewSmallNote("");
        void RefreshCrossLevelDetails()
        {
            if (crossLevelList.SelectedItem is not TerrainCrossLevelLookChoice choice)
            {
                crossLevelDetails.Text = crossLevelChoices.Count == 0
                    ? "No cached terrain looks from other levels were found. Build/open the portable cache to make cross-level looks available."
                    : "Choose a terrain look from another cached level.";
                return;
            }

            string custom = choice.HasCustomTexture
                ? "The source level has custom art staged, so the editor will copy that art."
                : "The editor will generate a local 64x64 palette from the source face color.";
            crossLevelDetails.Text =
                $"Selected face texture {selectedTerrain.TextureId} -> local borrowed texture from {choice.LevelName} texture {choice.TextureId}.\n" +
                $"{TerrainMaterialClassifier.FormatSurface(choice.Surface)}, {choice.FaceCount} face(s), proof {choice.ProofSummary}. {custom}";
        }
        crossLevelList.SelectionChanged += (_, _) => RefreshCrossLevelDetails();
        RefreshCrossLevelDetails();

        TextBox paletteNameBox = new()
        {
            Text = "Custom face palette",
            MinHeight = 34
        };
        TextBox paletteTextBox = new()
        {
            Text = "#305830 #69C169",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 112
        };

        Border customPanel = BuildTerrainPaintPanel(BuildTerrainPaintColorFields(lowPicker, highPicker));
        Border inGamePanel = BuildTerrainPaintPanel(BuildTerrainPaintInGamePanel(lookList, lookDetails));
        Border crossLevelPanel = BuildTerrainPaintPanel(BuildTerrainPaintInGamePanel(crossLevelList, crossLevelDetails));
        Border importPanel = BuildTerrainPaintPanel(NewSmallNote("Choose this to import a palette/image file after pressing Paint Face. Supported files include TXT, HEX, PAL, GPL, JSON, and PNG palette images."));
        Border palettePanel = BuildTerrainPaintPanel(BuildTerrainPaintPaletteFields(paletteNameBox, paletteTextBox));

        void SetMessage(string text, bool error = false)
        {
            message.Text = text;
            message.Foreground = new SolidColorBrush(error ? Color.FromRgb(160, 48, 48) : Color.FromRgb(72, 81, 92));
        }

        void RefreshMode()
        {
            TerrainPaintModeOption mode = modeBox.SelectedItem as TerrainPaintModeOption ?? TerrainPaintModeOption.All[0];
            customPanel.IsVisible = mode.Kind == TerrainPaintModeKind.CustomColors;
            inGamePanel.IsVisible = mode.Kind == TerrainPaintModeKind.InGameLook;
            crossLevelPanel.IsVisible = mode.Kind == TerrainPaintModeKind.CrossLevelLook;
            importPanel.IsVisible = mode.Kind == TerrainPaintModeKind.ImportedPalette;
            palettePanel.IsVisible = mode.Kind == TerrainPaintModeKind.PastedPalette;
            SetMessage(mode.Description);
        }
        modeBox.SelectionChanged += (_, _) => RefreshMode();
        RefreshMode();

        Grid modeGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        AddLabeledField(modeGrid, "Paint with", modeBox, 0);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"Selected face {selectedTerrain.RuntimeKey}, texture {selectedTerrain.TextureId}",
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45)),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(NewSmallNote("This paints only the selected terrain face. If needed, the editor first gives that face its own local texture slot so nearby terrain keeps its original look."));
        panel.Children.Add(modeGrid);
        panel.Children.Add(customPanel);
        panel.Children.Add(inGamePanel);
        panel.Children.Add(crossLevelPanel);
        panel.Children.Add(importPanel);
        panel.Children.Add(palettePanel);
        panel.Children.Add(message);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Paint Face");
        cancel.Click += (_, _) => dialog.Close(null);
        apply.Click += (_, _) =>
        {
            TerrainPaintModeOption mode = modeBox.SelectedItem as TerrainPaintModeOption ?? TerrainPaintModeOption.All[0];
            switch (mode.Kind)
            {
                case TerrainPaintModeKind.CustomColors:
                    if (!lowPicker.TryApplyTypedHex(out string lowError))
                    {
                        SetMessage($"Low color: {lowError}", error: true);
                        return;
                    }
                    if (!highPicker.TryApplyTypedHex(out string highError))
                    {
                        SetMessage($"High color: {highError}", error: true);
                        return;
                    }
                    dialog.Close(TerrainPaintDialogResult.ForCustomColors(lowPicker.SelectedColor, highPicker.SelectedColor));
                    return;
                case TerrainPaintModeKind.InGameLook:
                    if (lookList.SelectedItem is not TerrainTextureSwapChoice choice)
                    {
                        SetMessage("Choose an in-game terrain look first.", error: true);
                        return;
                    }
                    dialog.Close(TerrainPaintDialogResult.ForInGameLook(choice));
                    return;
                case TerrainPaintModeKind.CrossLevelLook:
                    if (crossLevelList.SelectedItem is not TerrainCrossLevelLookChoice crossLevelChoice)
                    {
                        SetMessage("Choose a terrain look from another cached level first.", error: true);
                        return;
                    }
                    dialog.Close(TerrainPaintDialogResult.ForCrossLevelLook(crossLevelChoice));
                    return;
                case TerrainPaintModeKind.ImportedPalette:
                    dialog.Close(TerrainPaintDialogResult.ForImportedPalette());
                    return;
                case TerrainPaintModeKind.PastedPalette:
                    try
                    {
                        TerrainPaletteImport palette = TerrainPaletteImporter.ImportText(
                            paletteTextBox.Text ?? "",
                            string.IsNullOrWhiteSpace(paletteNameBox.Text) ? "Custom face palette" : paletteNameBox.Text.Trim());
                        dialog.Close(TerrainPaintDialogResult.ForPalette(palette));
                    }
                    catch (Exception ex)
                    {
                        SetMessage($"Could not parse palette: {ex.Message}", error: true);
                    }
                    return;
            }
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);

        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static Border BuildTerrainPaintPanel(Control child)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 249, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 219, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = child
        };
    }

    private static Control BuildTerrainPaintColorFields(ColorPalettePicker lowPicker, ColorPalettePicker highPicker)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(112)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Low color", lowPicker.Control, 0);
        AddLabeledField(fields, "High color", highPicker.Control, 1);
        return fields;
    }

    private static Control BuildTerrainPaintInGamePanel(ListBox lookList, TextBlock lookDetails)
    {
        StackPanel panel = new()
        {
            Spacing = 8
        };
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = lookList
        });
        panel.Children.Add(lookDetails);
        return panel;
    }

    private static Control BuildTerrainPaintPaletteFields(TextBox nameBox, TextBox paletteBox)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Name", nameBox, 0);
        AddLabeledField(fields, "Colors", paletteBox, 1);
        return fields;
    }

    private int? FindTextureIdForSurface(string surface, IReadOnlyCollection<int> excludedTextureIds)
    {
        if (_currentGeometry == null)
            return null;

        surface = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        if (surface == "unknown")
            return null;

        HashSet<int> excluded = excludedTextureIds.ToHashSet();
        return _currentGeometry.Polygons
            .Where(polygon => polygon.TextureId >= 0)
            .Where(polygon => !excluded.Contains(polygon.TextureId))
            .Where(polygon => string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(polygon.Surface), surface, StringComparison.OrdinalIgnoreCase))
            .GroupBy(polygon => polygon.TextureId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
    }

    private Control BuildSurfacePaletteDialogContent(Window dialog, ComboBox presetBox, ColorPalettePicker lowPicker, ColorPalettePicker highPicker, CheckBox allSurfaceBox, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Palette", presetBox, 0);
        AddLabeledField(fields, "Low color", lowPicker.Control, 1);
        AddLabeledField(fields, "High color", highPicker.Control, 2);
        AddGridControl(fields, allSurfaceBox, 1, 3);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            if (!lowPicker.TryApplyTypedHex(out string lowError))
            {
                details.Foreground = new SolidColorBrush(Color.FromRgb(160, 48, 48));
                details.Text = $"Low color: {lowError}";
                return;
            }
            if (!highPicker.TryApplyTypedHex(out string highError))
            {
                details.Foreground = new SolidColorBrush(Color.FromRgb(160, 48, 48));
                details.Text = $"High color: {highError}";
                return;
            }

            dialog.Close(true);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private Control BuildTerrainRecolorDialogContent(Window dialog, ColorPalettePicker lowPicker, ColorPalettePicker highPicker, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(112)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Low color", lowPicker.Control, 0);
        AddLabeledField(fields, "High color", highPicker.Control, 1);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Generate");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            if (!lowPicker.TryApplyTypedHex(out string lowError))
            {
                details.Foreground = new SolidColorBrush(Color.FromRgb(160, 48, 48));
                details.Text = $"Low color: {lowError}";
                return;
            }
            if (!highPicker.TryApplyTypedHex(out string highError))
            {
                details.Foreground = new SolidColorBrush(Color.FromRgb(160, 48, 48));
                details.Text = $"High color: {highError}";
                return;
            }

            dialog.Close(true);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task<bool> ConfirmSharedTerrainTextureEditAsync(string action, string title)
    {
        if (_selectedTerrain == null || _currentGeometry == null)
            return false;

        int textureId = _selectedTerrain.TextureId;
        int affectedFaces = CountTerrainFacesUsingTexture(textureId);
        if (affectedFaces <= 1)
            return true;

        Window dialog = new()
        {
            Title = title,
            Width = 540,
            Height = 300,
            MinWidth = 460,
            MinHeight = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildSharedTerrainTextureEditDialogContent(dialog, textureId, affectedFaces, action);
        return await dialog.ShowDialog<bool>(this);
    }

    private int CountTerrainFacesUsingTexture(int textureId)
    {
        if (_currentGeometry == null || textureId < 0)
            return 0;

        return _currentGeometry.Polygons.Count(polygon => !polygon.IsTerrainRemoved && polygon.TextureId == textureId);
    }

    private static Control BuildSharedTerrainTextureEditDialogContent(Window dialog, int textureId, int affectedFaces, string action)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "This is a shared texture.",
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Texture {textureId} is currently used by {affectedFaces} terrain faces. If you {action}, every face using this texture will get that art in the test BIN.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });
        panel.Children.Add(new TextBlock
        {
            Text = "To change only the selected patch, use Color Selected Face, Import Face Palette, or Paste Face Palette. Those tools give the face its own local texture slot first.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button continueButton = NewButton("Edit Shared Texture");
        cancel.Click += (_, _) => dialog.Close(false);
        continueButton.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(continueButton);
        panel.Children.Add(buttons);
        return panel;
    }

    private static string SafeFilePart(string text)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(text.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : char.ToLowerInvariant(ch)).ToArray());
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(cleaned) ? "level" : cleaned.Trim('-');
    }

    private string EnsureUserOutputDirectory()
    {
        string path = Path.Combine(_workspace.RootPath, "output");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FriendlyFilePart(string text)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(text
            .Select(ch => invalid.Contains(ch) ? '-' : char.IsWhiteSpace(ch) ? ' ' : ch)
            .ToArray());
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        cleaned = cleaned.Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "Patch" : cleaned;
    }

    private void RefreshCurrentLevelDetails()
    {
        if (_currentLevel == null)
            return;

        bool hasUnsavedTerrainEdits = HasUnsavedTerrainEdits();
        bool hasUnsavedMobyEdits = HasUnsavedMobyEdits();
        string unsavedText = hasUnsavedMobyEdits && hasUnsavedTerrainEdits
            ? " - unsaved objects/terrain"
            : hasUnsavedMobyEdits
            ? " - unsaved objects"
            : hasUnsavedTerrainEdits
            ? " - unsaved terrain"
            : "";
        _levelTitle.Text = $"{_currentLevel.DisplayName}{unsavedText}";
        if (_terrainTaskSaveButton != null)
            _terrainTaskSaveButton.Content = hasUnsavedTerrainEdits ? "Save Terrain *" : "Save Terrain";
        _gemCounterText.Text = BuildLevelGemCounter();
        int visibleObjects = _currentMobys.Count(moby => !moby.IsRemoved);
        _levelDetails.Text =
            $"Terrain faces: {_currentGeometry?.Polygons.Count ?? 0}\n" +
            $"Objects: {visibleObjects}\n" +
            $"Surfaces: {BuildSurfaceSummary(_currentGeometry)}\n" +
            $"Terrain proof: {BuildTerrainProofReadinessSummary()}\n" +
            $"{_terrainCollisionReadinessMessage}\n" +
            $"Object edits: {BuildMobyEditSummary()}\n" +
            $"Terrain edits: {BuildTerrainEditSummary()}\n" +
            $"Custom terrain art: {BuildCustomTerrainArtStageSummary()}";
        RefreshTerrainExportReadiness();
        RefreshActionAvailability();
    }

    private void RefreshActionAvailability()
    {
        bool hasLevel = _currentLevel != null;
        bool hasObject = _selectedMoby != null && !_selectedMoby.IsRemoved;
        bool objectHasEdits = hasObject && (_selectedMoby!.HasAnyEdit || _selectedMoby.IsAdded);
        bool hasTerrain = _selectedTerrain != null && _selectedTerrainIndex >= 0;
        bool terrainHasEdits = hasTerrain && _selectedTerrain!.IsTerrainEdited;
        bool hasUnsavedTerrainEdits = HasUnsavedTerrainEdits();
        bool hasAnyTerrainReviewItem = (_currentGeometry?.Polygons.Any(polygon => polygon.IsTerrainEdited) ?? false) || _customTerrainTextures.Count > 0;
        bool hasTerrainPoint = hasTerrain && !_selectedTerrain!.IsTerrainRemoved && _selectedTerrain.Points.Count > 0 && _selectedTerrain.ZValues.Length > 0;
        bool canUseTerrainRemoveInLevel = CanUseTerrainRemoveInCurrentLevel();
        bool canRemoveSelectedTerrain = canUseTerrainRemoveInLevel && hasTerrain && !_selectedTerrain!.IsTerrainRemoved && CanStageTerrainRemoval(_selectedTerrain);
        bool canUseTerrainAddCopyInLevel = CanUseTerrainAddCopyInCurrentLevel();

        SetButtonEnabled(_objectAddButton, hasLevel);
        SetButtonEnabled(_objectRemoveButton, hasObject);
        SetButtonEnabled(_objectEditButton, hasObject);
        SetButtonEnabled(_objectCopyButton, hasObject);
        SetButtonEnabled(_objectPasteButton, hasLevel && _mobyClipboard != null);
        SetButtonEnabled(_objectUndoButton, objectHasEdits);
        SetButtonEnabled(_objectUndoRemoveButton, _lastRemovedMoby != null);
        SetButtonEnabled(_toolbarCreateBinButton, hasLevel);
        SetButtonEnabled(_objectRestoreLevelButton, hasLevel);
        SetButtonEnabled(_objectSelectedIdTestButton, hasObject && IsQuestionableMoby(_selectedMoby!));

        SetButtonEnabled(_terrainUndoButton, terrainHasEdits);
        bool canStageSelectedAddCopy = canUseTerrainAddCopyInLevel && hasTerrain && CanStageTerrainAddCopy(_selectedTerrain!);
        bool canStageAnyAddCopy = canUseTerrainAddCopyInLevel && (canStageSelectedAddCopy || (_currentGeometry?.Polygons.Count > 0 && HasAnyTerrainAddCopySource()));
        SetButtonEnabled(_terrainFindAddSourceButton, canUseTerrainAddCopyInLevel && HasAnyTerrainAddCopySource());
        SetButtonEnabled(_terrainTaskMoveFaceButton, hasTerrain && !_selectedTerrain!.IsTerrainRemoved);
        SetButtonEnabled(_terrainTaskSinglePointButton, hasTerrainPoint);
        SetButtonEnabled(_terrainTaskPaintFaceButton, hasTerrain && !_selectedTerrain!.IsTerrainRemoved);
        SetButtonEnabled(_terrainTaskAddCopyButton, canStageAnyAddCopy);
        SetButtonEnabled(_terrainTaskRemoveFaceButton, canRemoveSelectedTerrain);
        SetButtonEnabled(_terrainTaskSaveButton, hasUnsavedTerrainEdits);
        SetButtonEnabled(_terrainPointPreviousButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointNextButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointPlayableButton, hasTerrainPoint && HasPlayableTerrainPoint(_selectedTerrain!));
        SetButtonEnabled(_terrainPointRaiseButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointLowerButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointXMinusButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointXPlusButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointYMinusButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointYPlusButton, hasTerrainPoint);
        SetButtonEnabled(_terrainPointResetButton, hasTerrainPoint && SelectedTerrainPointHasEdit());
        SetButtonEnabled(_terrainPointEditButton, hasTerrainPoint);

        _objectActionHint.Text = BuildObjectActionHint(hasLevel, hasObject, objectHasEdits);
        _terrainActionHint.Text = BuildTerrainActionHint(hasLevel, hasTerrain, terrainHasEdits, hasUnsavedTerrainEdits, canUseTerrainRemoveInLevel, canRemoveSelectedTerrain);
        _terrainTaskHint.Text = BuildTerrainTaskHint(hasLevel, hasTerrain, terrainHasEdits, hasUnsavedTerrainEdits, hasTerrainPoint, canStageAnyAddCopy, hasAnyTerrainReviewItem, canUseTerrainRemoveInLevel, canUseTerrainAddCopyInLevel, canRemoveSelectedTerrain);
        RefreshTerrainPointControls();
        RefreshObjectReadinessHint();
    }

    private bool HasAnyTerrainAddCopySource()
    {
        if (!CanUseTerrainAddCopyInCurrentLevel())
            return false;

        return TryGetBestTerrainAddSource(out _, out _) ||
            TryGetBestTerrainAddSourceByFaceReadiness(out _, out _);
    }

    private bool CanStageTerrainAddCopy(TerrainPolygon terrain)
    {
        if (!CanUseTerrainAddCopyInCurrentLevel())
            return false;
        if (terrain.IsTerrainRemoved)
            return false;
        return GetTerrainAddCopyFaceReadiness(terrain).CanAddIndependentVisibleTerrain;
    }

    private bool CanStageTerrainRemoval(TerrainPolygon terrain)
    {
        if (!CanUseTerrainRemoveInCurrentLevel())
            return false;
        if (terrain.IsTerrainRemoved)
            return false;

        return CanRemoveTerrainForGameplay(GetTerrainRemoveFaceReadiness(terrain));
    }

    private static bool CanRemoveTerrainForGameplay(TerrainRemoveFaceReadiness readiness)
    {
        return readiness.CanRemoveVisibleFace && readiness.CanRemoveCollision;
    }

    private bool CanUseTerrainRemoveInCurrentLevel()
    {
        if (_currentGeometry != null &&
            IsSourceDerivedGeometry(_currentGeometry) &&
            _currentGeometry.Polygons.Any(IsSourceDerivedVisibleTerrainFace))
            return true;

        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        return readiness != null &&
            !TerrainStructureStatusBlocksWriteback(readiness.Status) &&
            readiness.RemovePatchCount > 0;
    }

    private bool CanUseTerrainAddCopyInCurrentLevel()
    {
        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null || TerrainStructureStatusBlocksWriteback(readiness.Status))
            return false;
        if (string.Equals(readiness.Status, "remove-only", StringComparison.OrdinalIgnoreCase))
            return false;

        return readiness.AddPatchCount > 0 &&
            (readiness.AddHasIndependentVertices || readiness.AddHasCollisionPatch || readiness.AddHasFallbackAppend);
    }

    private static bool TerrainStructureStatusBlocksWriteback(string status)
    {
        return string.Equals(status, "skipped", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetButtonEnabled(Button? button, bool enabled)
    {
        if (button != null)
            button.IsEnabled = enabled;
    }

    private string BuildObjectActionHint(bool hasLevel, bool hasObject, bool objectHasEdits)
    {
        if (!hasLevel)
            return "Open a level before adding or editing objects.";
        if (_pendingMobyAdd != null)
            return $"Click the map or Fly 3D view to place {_pendingMobyAdd.Label}. Press Escape to cancel placement.";
        if (!hasObject)
            return _lastRemovedMoby == null
                ? "Select an object to edit, remove, copy, or undo it. Add Object lets you choose a kind, then click the view to place it."
                : $"Select an object to edit, or restore the recently removed {_lastRemovedMoby.DisplayLabel}.";
        if (objectHasEdits)
            return $"Selected {_selectedMoby!.DisplayLabel}; undo is available for this object's staged changes.";
        return $"Selected {_selectedMoby!.DisplayLabel}; use Edit Object for details, Copy Object to duplicate it, or Remove Object to stage removal.";
    }

    private string BuildTerrainActionHint(bool hasLevel, bool hasTerrain, bool terrainHasEdits, bool hasUnsavedTerrainEdits, bool canUseTerrainRemoveInLevel, bool canRemoveSelectedTerrain)
    {
        if (!hasLevel)
            return "Open a level before editing terrain.";
        if (!hasTerrain)
            return "Select a terrain face to edit its material, texture, or height. Brushes can still be configured before selection.";
        if (_selectedTerrain?.IsTerrainRemoved == true)
            return "Selected terrain is staged for removal. Undo Terrain restores this face in the editor.";
        if (_selectedTerrain?.IsTerrainAddClone == true)
            return "Selected terrain is staged as an added copy. The ghost is the source face; the selected offset face is the new terrain. Move Face, Edit Point, and Paint Face now edit the copied face.";
        if (terrainHasEdits)
        {
            string addHint = _selectedTerrain != null && !CanStageTerrainAddCopy(_selectedTerrain)
                ? BuildTerrainAddCopyUnavailableHint()
                : "";
            string editHint = hasUnsavedTerrainEdits
                ? "Selected terrain has staged edits. Save Terrain writes them into the editor edit file; Create BIN uses saved/staged terrain data."
                : "Selected terrain has saved edits. Undo Terrain clears this face's terrain change.";
            return string.IsNullOrWhiteSpace(addHint) ? editHint : $"{editHint} {addHint}";
        }
        if (_selectedTerrain != null && !CanStageTerrainAddCopy(_selectedTerrain))
        {
            string remove = canRemoveSelectedTerrain
                ? "removed"
                : "reviewed";
            string removeHint = canRemoveSelectedTerrain
                ? ""
                : $" {BuildTerrainRemoveGameplayBlockedHint(_selectedTerrain)}";
            return $"Selected terrain can still be moved, {remove}, recolored, or edited point-by-point, but it is not a safe Add Terrain Copy source yet. {BuildTerrainAddCopyUnavailableHint()}{removeHint}";
        }
        if (!canRemoveSelectedTerrain && _selectedTerrain != null)
            return $"Selected terrain is unchanged. Move, paint, or edit points now; Remove Terrain is disabled until the editor can patch both the visible face and gameplay collision. {BuildTerrainRemoveGameplayBlockedHint(_selectedTerrain)}";
        return "Selected terrain is unchanged. Use Move Face, Edit Point, Paint Face, Add Terrain, or Remove Terrain to stage terrain edits.";
    }

    private string BuildTerrainTaskHint(bool hasLevel, bool hasTerrain, bool terrainHasEdits, bool hasUnsavedTerrainEdits, bool hasTerrainPoint, bool canStageAnyAddCopy, bool hasAnyTerrainReviewItem, bool canUseTerrainRemoveInLevel, bool canUseTerrainAddCopyInLevel, bool canRemoveSelectedTerrain)
    {
        if (!hasLevel)
            return "Open a level, then select a terrain face to move, paint, add, or remove it.";

        if (!hasTerrain)
        {
            string review = hasAnyTerrainReviewItem
                ? " Review Edits shows current saved/staged terrain and texture-art changes."
                : "";
            return $"Select a terrain face to enable terrain editing. Create BIN can still build saved terrain/custom texture edits for this level.{review}";
        }

        if (_selectedTerrain == null)
            return "Select a terrain face to enable terrain tasks.";

        if (_selectedTerrain.IsTerrainRemoved)
            return "This face is staged for removal. Review Edits shows the remove-face summary, and Preflight BIN shows the visible mesh and matched collision patches.";

        if (_selectedTerrain.IsTerrainAddClone)
            return "This face is staged as added terrain. Move Face changes its placement, Edit Point reshapes it, Paint Face changes its texture, and Advanced > Preflight BIN shows the add-copy patch outcome.";

        if (terrainHasEdits)
        {
            string saved = hasUnsavedTerrainEdits
                ? "Save Terrain writes this staged edit into the editor edit file."
                : "This edit is already saved in the editor edit file.";
            return $"{BuildTerrainEditReviewSummary(_selectedTerrain)}. {saved} Create BIN uses these terrain edits; Advanced > Preflight BIN shows the exact patch rows first.";
        }

        List<string> choices = ["Move Face changes all points on the selected face"];
        choices.Add(hasTerrainPoint ? "Edit Point changes one vertex" : "Edit Point needs a face with editable points");
        choices.Add(canRemoveSelectedTerrain ? "Remove Terrain can delete the visible face and matched gameplay collision" : "Remove Terrain is disabled until this face has visible and collision proof");
        choices.Add(canStageAnyAddCopy ? "Add Terrain can stage a proven copied face" : canUseTerrainAddCopyInLevel ? "Add Terrain needs a proven source face for this level" : "Add Terrain needs current-level add-copy proof");
        choices.Add("Paint Face can use custom colors, in-game looks, or pasted palettes");
        return string.Join(". ", choices) + ".";
    }

    private string BuildTerrainAddCopyUnavailableHint()
    {
        if (_currentGeometry != null && IsSourceDerivedGeometry(_currentGeometry))
            return "Add Terrain is still disabled for source-derived levels until copied-face sector relocation/slack is proven. Move, paint, and edit points can still be used; Remove Terrain enables only when visible mesh and gameplay collision are both mapped.";

        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null)
            return "No current-level add/remove proof report exists yet, so Add Terrain is disabled until the structure readiness pass maps this level.";
        if (TerrainStructureStatusBlocksWriteback(readiness.Status))
            return $"Add Terrain is disabled for {_currentLevel?.DisplayName ?? "this level"} because structure readiness is {FormatTerrainStructureReadiness(readiness)}";
        if (string.Equals(readiness.Status, "remove-only", StringComparison.OrdinalIgnoreCase))
            return $"Add Terrain is disabled for {_currentLevel?.DisplayName ?? "this level"} because this level is remove-only right now.";
        if (!CanUseTerrainAddCopyInCurrentLevel())
            return $"Add Terrain needs more structure proof for {_currentLevel?.DisplayName ?? "this level"}: {FormatTerrainStructureReadiness(readiness)}";

        return HasAnyTerrainAddCopySource()
            ? "Use Add Terrain Copy to auto-pick the best proven source, or Find Best Add Face to inspect it first."
            : "No native add-copy source is proven for this level yet.";
    }

    private string BuildTerrainRemoveUnavailableHint()
    {
        if (_currentGeometry != null && IsSourceDerivedGeometry(_currentGeometry))
            return "Remove Terrain needs source-derived face offsets and matched gameplay collision. Rebuild/open this level's source-derived terrain cache, then run/capture collision proof if the selected face cannot be removed.";

        TerrainStructureReadiness? readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null)
            return "No current-level add/remove proof report exists yet, so Remove Terrain is disabled until the structure readiness pass maps this level.";
        return $"Remove Terrain is disabled for {_currentLevel?.DisplayName ?? "this level"} until remove-face proof is available. Current structure readiness: {FormatTerrainStructureReadiness(readiness)}";
    }

    private string BuildTerrainRemoveGameplayBlockedHint(TerrainPolygon terrain)
    {
        if (!CanUseTerrainRemoveInCurrentLevel())
            return BuildTerrainRemoveUnavailableHint();

        TerrainRemoveFaceReadiness readiness = GetTerrainRemoveFaceReadiness(terrain);
        if (CanRemoveTerrainForGameplay(readiness))
            return "Remove Terrain is ready for this face.";

        if (!readiness.CanRemoveVisibleFace)
        {
            return readiness.HasSourceSearch
                ? "Remove Terrain is disabled because this face is not mapped to a source WAD face yet."
                : "Remove Terrain is disabled because the source terrain map is missing for this level.";
        }

        string collision = readiness.HasCollisionData
            ? readiness.TotalCollisionTriangles > 0
                ? "No matching collision triangle is mapped for this face yet."
                : "This face has no mapped collision triangle yet."
            : "Playable collision is not mapped for this level yet.";
        return $"Remove Terrain is disabled because this would only hide the ground; Spyro could still walk on it or be hurt by it. {collision}";
    }

    private bool HasUnsavedTerrainEdits()
    {
        return _currentGeometry != null
            && !string.Equals(BuildTerrainEditSignature(_currentGeometry), _savedTerrainEditSignature, StringComparison.Ordinal);
    }

    private bool HasUnsavedMobyEdits()
    {
        return !string.Equals(BuildMobyEditSignature(_currentMobys), _savedMobyEditSignature, StringComparison.Ordinal);
    }

    private string BuildMobyEditSummary()
    {
        int live = _currentMobys.Count(moby => moby.HasAnyEdit);
        if (live == 0)
            return HasUnsavedMobyEdits()
                ? _loadedMobyEdits > 0 ? $"0 live, {_loadedMobyEdits} saved, unsaved" : "none, unsaved"
                : _loadedMobyEdits > 0 ? $"0 live, {_loadedMobyEdits} saved" : "none";

        List<string> parts = new()
        {
            $"{live} live",
            HasUnsavedMobyEdits() ? "unsaved" : "saved"
        };
        if (_loadedMobyEdits != live)
            parts.Add($"{_loadedMobyEdits} saved");
        return string.Join(", ", parts);
    }

    private string BuildLevelGemCounter()
    {
        List<Moby> treasureMobys = _currentMobys
            .Where(moby => !moby.IsRemoved && moby.TreasureValue > 0)
            .ToList();
        int value = treasureMobys.Sum(moby => moby.TreasureValue);
        int looseGems = treasureMobys.Count(moby => moby.IsGemLike);
        int rewardHolders = treasureMobys.Count - looseGems;
        int added = treasureMobys.Count(gem => gem.IsAdded);
        string addedText = added > 0 ? $", {added} added" : "";
        return $"Gems: {value} value, {looseGems} loose/contained, {rewardHolders} in objects{addedText}";
    }

    private string BuildTerrainEditSummary()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return "none";

        TerrainEditSafetySummary summary = BuildTerrainEditSafetySummary();
        if (summary.LiveEdits == 0)
        {
            bool noLiveEditsButChanged = HasUnsavedTerrainEdits();
            if (_loadedTerrainEdits > 0)
                return noLiveEditsButChanged ? $"0 live, {_loadedTerrainEdits} saved, unsaved" : $"0 live, {_loadedTerrainEdits} saved";
            return noLiveEditsButChanged ? "none, unsaved" : "none";
        }

        List<string> parts = new()
        {
            $"{summary.LiveEdits} live",
            $"{summary.HeightEdits} height",
            $"{summary.PositionEdits} position",
            $"{summary.TextureEdits} texture",
            $"{summary.StructuralEdits} structural"
        };
        parts.Add(HasUnsavedTerrainEdits() ? "unsaved" : "saved");
        if (_loadedTerrainEdits != summary.LiveEdits)
            parts.Add($"{_loadedTerrainEdits} saved");

        if (summary.HeightEdits > 0 || summary.PositionEdits > 0)
        {
            List<string> patchParts = new();
            if (summary.PlayableHeightEdits > 0)
                patchParts.Add($"{summary.PlayableHeightEdits} playable");
            if (summary.PartialHeightEdits > 0)
                patchParts.Add($"{summary.PartialHeightEdits} partial");
            if (summary.VisualOnlyHeightEdits > 0)
                patchParts.Add($"{summary.VisualOnlyHeightEdits} visual-only");
            if (summary.UnknownHeightEdits > 0)
                patchParts.Add($"{summary.UnknownHeightEdits} unknown");
            parts.Add($"geometry patches: {string.Join(", ", patchParts)}");
        }

        return string.Join("; ", parts);
    }

    private string BuildTerrainExportReviewSummary()
    {
        List<string> lines = new()
        {
            $"Terrain edits: {BuildTerrainEditSummary()}."
        };

        int textureIdSwitches = _currentGeometry?.Polygons.Count(polygon => polygon.HasTextureEdit) ?? 0;
        if (textureIdSwitches > 0)
            lines.Add($"{textureIdSwitches} face(s) switch texture IDs in the test BIN.");

        int structuralEdits = _currentGeometry?.Polygons.Count(polygon => polygon.HasStructureEdit) ?? 0;
        if (structuralEdits > 0)
        {
            int removeFaces = _currentGeometry?.Polygons.Count(polygon => polygon.IsTerrainRemoved) ?? 0;
            int addCopies = _currentGeometry?.Polygons.Count(polygon => polygon.IsTerrainAddClone) ?? 0;
            List<string> structuralParts = [];
            if (removeFaces > 0)
                structuralParts.Add($"{removeFaces} remove");
            if (addCopies > 0)
                structuralParts.Add($"{addCopies} add-copy");
            lines.Add($"{structuralEdits} structural terrain edit(s) staged ({string.Join(", ", structuralParts)}). Create BIN can neutralize removed faces and add copied visible faces with independent vertices; copied collision is added when the source face has matched collision and a same-cell placeholder.");
        }

        if (_customTerrainTextures.Count == 0)
        {
            lines.Add("Custom texture art: none staged. Label-only material changes affect editor classification, not in-game texture pages.");
        }
        else
        {
            HashSet<int> liveTextureIds = CurrentTerrainTextureIds();
            List<CustomTerrainTextureImport> activeTextures = _customTerrainTextures
                .Where(texture => liveTextureIds.Contains(texture.TextureId))
                .ToList();
            int unusedImports = _customTerrainTextures.Count - activeTextures.Count;
            List<string> textureSummaries = activeTextures
                .GroupBy(texture => texture.TextureId)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    string tiers = string.Join("/", group
                        .Select(texture => FormatTextureDescriptorTier(texture.DescriptorTier))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                    string names = string.Join(", ", group
                        .Select(texture => string.IsNullOrWhiteSpace(texture.PaletteName) ? texture.SourceImageName : texture.PaletteName)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(2));
                    return string.IsNullOrWhiteSpace(names)
                        ? $"texture {group.Key} ({tiers})"
                        : $"texture {group.Key} ({tiers}: {names})";
                })
                .Take(5)
                .ToList();
            string extra = activeTextures.Select(texture => texture.TextureId).Distinct().Count() > textureSummaries.Count
                ? ", ..."
                : "";
            if (activeTextures.Count == 0)
            {
                lines.Add($"Custom texture art: {_customTerrainTextures.Count} unused import(s) are staged, but no current face uses those texture IDs. Create BIN will ignore them until a face uses that texture.");
            }
            else
            {
                string unused = unusedImports > 0
                    ? $" {unusedImports} unused import(s) will be ignored."
                    : "";
                lines.Add($"Custom texture art: {activeTextures.Count} active import(s) staged for Create BIN: {string.Join("; ", textureSummaries)}{extra}.{unused}");
            }
        }

        lines.Add($"Gameplay behavior proof: {BuildTerrainProofReadinessSummary()}. Visual texture/material edits still need Proof Targets before treating water, lava, or goo behavior as proven.");
        return string.Join("\n", lines);
    }

    private string BuildTerrainTextureExportForReview(int textureId)
    {
        List<CustomTerrainTextureImport> imports = _customTerrainTextures
            .Where(texture => texture.TextureId == textureId)
            .OrderBy(texture => texture.DescriptorTier, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (imports.Count == 0)
            return "no custom texture art staged for this texture";

        return string.Join("; ", imports.Select(texture =>
        {
            string source = string.IsNullOrWhiteSpace(texture.PaletteName)
                ? texture.SourceImageName
                : texture.PaletteName;
            string tier = FormatTextureDescriptorTier(texture.DescriptorTier);
            return string.IsNullOrWhiteSpace(source)
                ? $"{tier} texture-page patch staged"
                : $"{tier} texture-page patch staged from {source}";
        }));
    }

    private string BuildSelectedTerrainTextureScopeSummary(TerrainPolygon terrain)
    {
        int textureUseCount = _currentGeometry?.Polygons.Count(polygon =>
            !polygon.IsTerrainRemoved && polygon.TextureId == terrain.TextureId) ?? 0;
        int originalTextureUseCount = _currentGeometry?.Polygons.Count(polygon =>
            !polygon.IsTerrainRemoved && polygon.OriginalTextureId == terrain.OriginalTextureId) ?? 0;
        List<CustomTerrainTextureImport> imports = _customTerrainTextures
            .Where(texture => texture.TextureId == terrain.TextureId)
            .ToList();

        string textureScope = textureUseCount <= 1
            ? "only this selected face currently uses it"
            : $"{textureUseCount} current face(s) use it";
        string customScope = imports.Count == 0
            ? "no custom texture-page art is staged"
            : textureUseCount <= 1
                ? $"{imports.Count} custom texture-page import(s) are face-local"
                : $"{imports.Count} custom texture-page import(s) will affect all {textureUseCount} face(s) using texture {terrain.TextureId}";

        if (terrain.HasTextureEdit)
        {
            string sourceScope = originalTextureUseCount <= 1
                ? "the original texture was unique"
                : $"the original texture was shared by {originalTextureUseCount} face(s)";
            return $"Selected face texture ID changed {terrain.OriginalTextureId}->{terrain.TextureId}; {textureScope}; {customScope}; {sourceScope}.";
        }

        return $"Selected face still uses texture {terrain.TextureId}; {textureScope}; {customScope}.";
    }

    private string BuildSelectedTerrainExportImpactSummary(TerrainPolygon terrain)
    {
        List<string> parts = new();

        if (terrain.HasStructureEdit)
            parts.Add(terrain.StructureEdit switch
            {
                TerrainStructureEditKind.RemoveFace => "remove face by degenerating its visible face record and matched collision",
                TerrainStructureEditKind.AddCloneFace => "add copied visible terrain with independent vertices, plus copied collision when a same-cell placeholder is available",
                _ => "structural terrain edit staged"
            });

        if (terrain.HasHeightEdit || terrain.HasPositionEdit)
            parts.Add(ClassifyTerrainPatchSafety(terrain) switch
            {
                TerrainPatchSafetyKind.PlayableHeight => terrain.HasPositionEdit ? "playable geometry patch" : "playable height patch",
                TerrainPatchSafetyKind.PartialHeight => terrain.HasPositionEdit ? "partial playable geometry patch" : "partial playable height patch",
                TerrainPatchSafetyKind.VisualOnlyHeight => terrain.HasPositionEdit ? "visual mesh geometry only" : "visual mesh height only",
                TerrainPatchSafetyKind.UnknownHeight => terrain.HasPositionEdit ? "geometry patch with unknown collision" : "height patch with unknown collision",
                _ => terrain.HasPositionEdit ? "position edit" : "height edit"
            });

        if (terrain.HasTextureEdit)
            parts.Add($"texture ID {terrain.OriginalTextureId} -> {terrain.TextureId}");

        int textureImports = _customTerrainTextures.Count(texture => texture.TextureId == terrain.TextureId);
        if (textureImports > 0)
            parts.Add($"{textureImports} texture-art import(s)");

        if (parts.Count == 0)
            parts.Add("nothing yet for this face");

        parts.Add(TerrainFaceHasBehaviorProof(terrain)
            ? "behavior proof available"
            : "no behavior change/proof from material label alone");

        return string.Join("; ", parts) + ".";
    }

    private static string FormatTextureDescriptorTier(string descriptorTier)
    {
        return descriptorTier switch
        {
            "hqData" => "normal",
            "hqDataClose" => "close",
            "both" => "normal+close",
            _ => string.IsNullOrWhiteSpace(descriptorTier) ? "unknown tier" : descriptorTier
        };
    }

    private string BuildTerrainProofReadinessSummary()
    {
        if (_currentLevel == null || _currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return "none";

        TerrainBehaviorProofFile proofFile = TerrainBehaviorProofStore.Load(_workspace.RootPath, _currentLevel.Key);
        int proven = proofFile.Rules.Count(rule => string.Equals(rule.Confidence, "proven", StringComparison.OrdinalIgnoreCase));
        int observed = proofFile.Rules.Count(rule => string.Equals(rule.Confidence, "observed", StringComparison.OrdinalIgnoreCase));
        int noisy = proofFile.Rules.Count(rule => string.Equals(rule.Confidence, "noisy", StringComparison.OrdinalIgnoreCase));
        int hazardCandidates = _currentGeometry.Polygons.Count(polygon =>
            TerrainBehaviorClassifier.FormatBehavior(polygon.Behavior).Contains("hazard", StringComparison.OrdinalIgnoreCase) &&
            !polygon.BehaviorConfidence.Contains("proof", StringComparison.OrdinalIgnoreCase) &&
            !polygon.BehaviorConfidence.Contains("observed", StringComparison.OrdinalIgnoreCase));

        List<string> parts = [];
        if (proven > 0)
            parts.Add($"{proven} proven");
        if (observed > 0)
            parts.Add($"{observed} observed");
        if (noisy > 0)
            parts.Add($"{noisy} noisy");
        if (hazardCandidates > 0)
            parts.Add($"{hazardCandidates} hazard candidate faces");

        return parts.Count == 0
            ? "no behavior proof yet"
            : string.Join("; ", parts);
    }

    private string BuildSelectedTerrainProofStatus(TerrainPolygon terrain)
    {
        if (_currentLevel == null)
            return "No level loaded.";

        TerrainBehaviorRule? rule = TerrainBehaviorProofStore.FindBestRule(
            _workspace.RootPath,
            _currentLevel.Key,
            terrain.TextureId,
            terrain.Surface);
        if (rule != null)
        {
            return $"{TerrainBehaviorClassifier.FormatBehavior(rule.Behavior)} ({rule.Confidence}, {rule.ObservationCount} capture(s))";
        }

        string behavior = TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior);
        string source = string.IsNullOrWhiteSpace(terrain.BehaviorConfidence) ? "unproven" : terrain.BehaviorConfidence;
        return $"{behavior} ({source}; no RAM proof for this texture yet)";
    }

    private TerrainEditSafetySummary BuildTerrainEditSafetySummary()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return new TerrainEditSafetySummary(0, 0, 0, 0, 0, 0, 0, 0, 0);

        List<TerrainPolygon> edited = _currentGeometry.Polygons
            .Where(polygon => polygon.IsTerrainEdited)
            .ToList();
        int height = edited.Count(polygon => polygon.HasHeightEdit);
        int position = edited.Count(polygon => polygon.HasPositionEdit);
        int texture = edited.Count(polygon => polygon.HasTextureEdit);
        int structural = edited.Count(polygon => polygon.HasStructureEdit);
        int playableHeight = 0;
        int partialHeight = 0;
        int visualOnlyHeight = 0;
        int unknownHeight = 0;
        foreach (TerrainPolygon polygon in edited.Where(polygon => polygon.HasHeightEdit || polygon.HasPositionEdit))
        {
            if (_terrainCollisionTriangleKeys.Count == 0 || _terrainCollisionMatchedFaces == 0)
            {
                unknownHeight++;
                continue;
            }

            TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(polygon);
            if (coverage.MatchedTriangles == 0)
                visualOnlyHeight++;
            else if (coverage.MatchedTriangles < coverage.TotalTriangles)
                partialHeight++;
            else
                playableHeight++;
        }

        return new TerrainEditSafetySummary(edited.Count, height, position, texture, structural, playableHeight, partialHeight, visualOnlyHeight, unknownHeight);
    }

    private static string BuildTerrainEditSignature(GeometryCandidate? geometry)
    {
        if (geometry == null)
            return "";

        StringBuilder builder = new();
        foreach (TerrainPolygon polygon in geometry.Polygons.Where(polygon => polygon.IsTerrainEdited).OrderBy(polygon => polygon.RuntimeKey, StringComparer.Ordinal))
        {
            builder.Append(polygon.RuntimeKey)
                .Append('|')
                .Append(polygon.TextureId)
                .Append('|')
                .Append(polygon.StructureEdit)
                .Append('|');
            foreach (float delta in polygon.TerrainVertexDeltas())
                builder.Append(MathF.Round(delta, 3).ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            builder.Append('|');
            foreach (Vector2f delta in polygon.TerrainVertexXYDeltas())
            {
                builder.Append(MathF.Round(delta.X, 3).ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(MathF.Round(delta.Y, 3).ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(',');
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildMobyEditSignature(IEnumerable<Moby> mobys)
    {
        StringBuilder builder = new();
        foreach (Moby moby in mobys.Where(moby => moby.HasAnyEdit).OrderBy(moby => moby.TrueIndex).ThenBy(moby => moby.Index))
        {
            builder.Append(moby.Index)
                .Append('|')
                .Append(moby.TrueIndex)
                .Append('|')
                .Append(moby.IsAdded)
                .Append('|')
                .Append(moby.IsRemoved)
                .Append('|')
                .Append(MathF.Round(moby.Position.X, 4).ToString("0.####", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(MathF.Round(moby.Position.Y, 4).ToString("0.####", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(MathF.Round(moby.Position.Z, 4).ToString("0.####", CultureInfo.InvariantCulture))
                .Append('|')
                .Append(moby.Type)
                .Append('|')
                .Append(moby.State)
                .Append('|')
                .Append(moby.SourceByte36)
                .Append('|')
                .Append(moby.SourceByte37)
                .Append('|')
                .Append(moby.SourceByte4F)
                .Append('|')
                .Append(moby.Flag4A)
                .Append('|')
                .Append(moby.Flag4B)
                .Append('|')
                .Append(moby.Label)
                .Append('|')
                .Append(moby.CrossLevelTemplateId)
                .Append('|')
                .Append(moby.CrossLevelSourceLevelKey)
                .Append('|')
                .Append(moby.CrossLevelSourceTrueIndex)
                .Append('|')
                .Append(moby.SourceCloneLevelKey)
                .Append('|')
                .Append(moby.SourceCloneTrueIndex)
                .AppendLine();
        }

        return builder.ToString();
    }

    private void UpdateLevelToolPanels(LevelDefinition level)
    {
        SkyboxLevelEntry? skybox = _skyboxCatalog.FindForLevel(level.Key);
        if (skybox == null)
        {
            _skyboxDetails.Text = "No skybox catalog entry is loaded for this level yet.";
        }
        else
        {
            string donors = skybox.CompatibleDonorNames.Count > 0
                ? string.Join(", ", skybox.CompatibleDonorNames)
                : "none listed";
            string note = skybox.Notes.Count > 0 ? $"\n{skybox.Notes[0]}" : "";
            _skyboxDetails.Text =
                $"Catalog: {skybox.Status}\n" +
                $"WAD entry {skybox.AssetWadEntry}, subfile {skybox.SkySubfileIndex}, {skybox.SkySubfileSize:N0} bytes\n" +
                $"Compatible donors: {donors}" +
                note;
        }

        _skyboxPresetBox.SelectedItem = SkyboxPresetCatalog.DefaultForLevel(level.Key);
        _skyboxCustomPaletteBox.Text = "";
        LoadSavedSkyboxPlan(level);

        bool isHomeWorld = IsHomeWorld(level);
        if (_homeworldTextGroup != null)
            _homeworldTextGroup.IsVisible = isHomeWorld;

        TextTargetEntry? textTarget = isHomeWorld ? _textTargets.FindForLevel(level) : null;
        if (!isHomeWorld)
        {
            _levelTextDetails.Text = "";
            _levelTextReplacementBox.Text = "";
        }
        else if (textTarget == null)
        {
            _levelTextDetails.Text = "No fixed text slot has been mapped for this level yet.";
            _levelTextReplacementBox.Text = "";
        }
        else
        {
            _levelTextDetails.Text =
                $"Original: {textTarget.OriginalText}\n" +
                $"Maximum length: {textTarget.MaxLength} characters. Shorter text is padded safely.";
            _levelTextReplacementBox.Text = textTarget.OriginalText;
        }
        LoadSavedLevelTextPlan(level, textTarget);
    }

    private static bool IsHomeWorld(LevelDefinition level)
    {
        string key = LevelCatalog.NormalizeKey(level.Key);
        return key is "artisans" or "peacekeepers" or "magiccrafters" or "beastmakers" or "dreamweavers" or "gnastysworld";
    }

    private void LoadSavedSkyboxPlan(LevelDefinition level)
    {
        string path = Path.Combine(_workspace.RootPath, $"{level.Key}-skybox-edit-plan.json");
        if (!File.Exists(path))
            return;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string planLevel = GetJsonString(root, "levelKey");
            if (!string.IsNullOrWhiteSpace(planLevel) &&
                !string.Equals(LevelCatalog.NormalizeKey(planLevel), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase))
                return;

            string presetId = GetJsonString(root, "preset");
            SkyboxPreset? preset = SkyboxPresetCatalog.Presets.FirstOrDefault(item =>
                string.Equals(item.Id, presetId, StringComparison.OrdinalIgnoreCase) && item.SupportsLevel(level.Key));
            if (preset != null)
                _skyboxPresetBox.SelectedItem = preset;

            string paletteHex = GetJsonString(root, "customPaletteHex");
            if (!string.IsNullOrWhiteSpace(paletteHex))
                _skyboxCustomPaletteBox.Text = paletteHex;

            string savedName = preset?.DisplayName ?? GetJsonString(root, "presetName", "saved skybox plan");
            _skyboxDetails.Text += $"\nSaved plan: {savedName}.";
        }
        catch
        {
            _skyboxDetails.Text += "\nSaved skybox plan could not be loaded.";
        }
    }

    private void LoadSavedLevelTextPlan(LevelDefinition level, TextTargetEntry? textTarget)
    {
        string path = Path.Combine(_workspace.RootPath, $"{level.Key}-level-text-edit-plan.json");
        if (!File.Exists(path))
            return;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string planLevel = GetJsonString(root, "levelKey");
            if (!string.IsNullOrWhiteSpace(planLevel) &&
                !string.Equals(LevelCatalog.NormalizeKey(planLevel), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase))
                return;

            string replacement = TextTargetCatalog.NormalizeReplacement(GetJsonString(root, "replacementText"));
            if (textTarget != null && TextTargetCatalog.IsSafeReplacement(replacement, textTarget.MaxLength))
                _levelTextReplacementBox.Text = replacement;

            if (!string.IsNullOrWhiteSpace(replacement))
                _levelTextDetails.Text += $"\nSaved plan: {replacement}.";
        }
        catch
        {
            _levelTextDetails.Text += "\nSaved lettering plan could not be loaded.";
        }
    }

    private void LoadSavedExeStringPlan()
    {
        string path = Path.Combine(_workspace.RootPath, "ui-text-edit-plan.json");
        if (!File.Exists(path))
            return;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string original = GetJsonString(root, "originalText").Trim();
            string replacement = GetJsonString(root, "replacementText").Trim();
            if (IsSafeExeStringText(original) && IsSafeExeStringText(replacement) && replacement.Length <= original.Length)
            {
                SyncExeStringPreset(original);
                _exeStringOriginalBox.Text = original;
                _exeStringReplacementBox.Text = replacement;
            }
        }
        catch
        {
            _levelTextDetails.Text += "\nSaved UI text plan could not be loaded.";
        }
    }

    private void SyncExeStringPreset(string original)
    {
        _syncingExeStringPreset = true;
        try
        {
            _exeStringPresetBox.SelectedItem = ExeStringPreset.Known.FirstOrDefault(preset =>
                string.Equals(preset.OriginalText, original, StringComparison.Ordinal));
        }
        finally
        {
            _syncingExeStringPreset = false;
        }
    }

    private async Task SaveSkyboxPlanAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before saving a skybox plan.";
            return;
        }

        if (_skyboxPresetBox.SelectedItem is not SkyboxPreset preset)
            preset = SkyboxPresetCatalog.DefaultForLevel(_currentLevel.Key);

        if (!preset.SupportsLevel(_currentLevel.Key))
        {
            _statusText.Text = $"{preset.DisplayName} is not marked safe for {_currentLevel.DisplayName} yet.";
            return;
        }

        string paletteHex = _skyboxCustomPaletteBox.Text?.Trim() ?? "";
        if (preset.Id == "Custom" && !IsSafePaletteText(paletteHex))
        {
            _statusText.Text = "Custom skybox colors need to be #RRGGBB values separated by spaces.";
            return;
        }

        SkyboxLevelEntry? skybox = _skyboxCatalog.FindForLevel(_currentLevel.Key);
        string path = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-skybox-edit-plan.json");
        var plan = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            kind = "skybox-color-palette-plan",
            levelKey = _currentLevel.Key,
            levelName = _currentLevel.DisplayName,
            preset = preset.Id,
            presetName = preset.DisplayName,
            customPaletteHex = preset.Id == "Custom" ? paletteHex : "",
            sourceTool = "tools/Export-SpyroSkyPrimitiveColorPalette.ps1",
            planOnlyArguments = BuildSkyboxToolArguments(preset, paletteHex),
            catalog = skybox == null
                ? null
                : new
                {
                    skybox.AssetWadEntry,
                    skybox.MetadataWadEntry,
                    skybox.SkySubfileIndex,
                    skybox.SkySubfileSize,
                    skybox.SkyWadOffset,
                    skybox.SkyImageOffset,
                    skybox.CompatibleDonorNames,
                    skybox.Notes
                },
            notes = new[]
            {
                "This plan uses the safer RGB sky primitive path, not whole-subfile skybox swaps.",
                "The native Mac editor can export this plan for supported Stone Hill presets."
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(plan, NewJsonOptions()));
        _statusText.Text = $"Saved skybox plan for {_currentLevel.DisplayName}: {preset.DisplayName}.";
    }

    private async Task CreateSkyboxCueAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose Stone Hill before creating a skybox test.";
            return;
        }

        if (!string.Equals(LevelCatalog.NormalizeKey(_currentLevel.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
        {
            _statusText.Text = "Native skybox export is currently mapped for Stone Hill only.";
            return;
        }

        if (_skyboxPresetBox.SelectedItem is not SkyboxPreset preset)
            preset = SkyboxPresetCatalog.DefaultForLevel(_currentLevel.Key);

        if (!SkyboxColorPatchExporter.NativePresetIds.Contains(preset.Id))
        {
            _statusText.Text = $"{preset.DisplayName} still needs a native exporter port.";
            return;
        }

        string sourceImage = FirstExistingDiscImagePath(_skyboxDiscImagePathBox.Text, _discImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before creating a skybox test.";
            return;
        }

        string wadAnalysis = _skyboxWadAnalysisPathBox.Text?.Trim() ?? "";
        if (!File.Exists(wadAnalysis))
        {
            try
            {
                wadAnalysis = Path.Combine(_workspace.RootPath, "spyro-wad-analysis.json");
                await WadAnalysisBuilder.BuildAsync(sourceImage, wadAnalysis);
                _skyboxWadAnalysisPathBox.Text = wadAnalysis;
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Could not build spyro-wad-analysis.json from the selected BIN/CUE: {ex.Message}";
                return;
            }
        }

        string paletteHex = _skyboxCustomPaletteBox.Text?.Trim() ?? "";
        if (preset.Id == "Custom" && !IsSafePaletteText(paletteHex))
        {
            _statusText.Text = "Custom skybox colors need to be #RRGGBB values separated by spaces.";
            return;
        }

        string outputDir = EnsureUserOutputDirectory();
        string outputPrefix = Path.Combine(outputDir, $"Spyro Editor - Stone Hill - Skybox - {FriendlyFilePart(preset.DisplayName)}");

        _statusText.Text = $"Creating skybox test: {preset.DisplayName}...";
        try
        {
            SkyboxColorPatchResult result = await SkyboxColorPatchExporter.ExportAsync(new SkyboxColorPatchRequest(
                SourceImagePath: sourceImage,
                SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
                WadAnalysisPath: wadAnalysis,
                OutputPrefix: outputPrefix,
                Preset: preset,
                CustomPaletteHex: paletteHex,
                WriteImage: true));

            _statusText.Text = $"Created {Path.GetFileName(result.OutputCuePath)} with {result.Plan.PatchCount} sky color patches.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create skybox test: {ex.Message}";
        }
    }

    private async Task SaveLevelTextPlanAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before saving a lettering plan.";
            return;
        }
        if (!IsHomeWorld(_currentLevel))
        {
            _statusText.Text = "Portal/name lettering edits are available only on homeworlds.";
            return;
        }

        TextTargetEntry? target = _textTargets.FindForLevel(_currentLevel);
        if (target == null)
        {
            _statusText.Text = "This level does not have a mapped fixed text slot yet.";
            return;
        }

        string replacement = TextTargetCatalog.NormalizeReplacement(_levelTextReplacementBox.Text ?? "");
        if (!TextTargetCatalog.IsSafeReplacement(replacement, target.MaxLength))
        {
            _statusText.Text = $"Use {target.MaxLength} or fewer safe characters for {target.OriginalText}.";
            return;
        }

        string path = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-level-text-edit-plan.json");
        var plan = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            kind = "exe-level-name-string-plan",
            levelKey = _currentLevel.Key,
            levelName = _currentLevel.DisplayName,
            scriptKey = target.ScriptKey,
            originalText = target.OriginalText,
            replacementText = replacement,
            maxLength = target.MaxLength,
            nulledByteCount = target.MaxLength - replacement.Length,
            sourceTool = "tools/Export-SpyroLevelTextPatchTest.ps1",
            planOnlyArguments = new[]
            {
                "-TargetLevelKey", target.ScriptKey,
                "-ReplacementName", replacement,
                "-PlanOnly"
            },
            notes = new[]
            {
                "This edits the anchored executable level-name string table.",
                "It is expected to affect fly-in title lettering; portal label behavior still needs in-game confirmation."
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(plan, NewJsonOptions()));
        _statusText.Text = $"Saved lettering plan: {target.OriginalText} -> {replacement}.";
    }

    private async Task SaveExeStringPlanAsync()
    {
        string original = _exeStringOriginalBox.Text?.Trim() ?? "";
        string replacement = _exeStringReplacementBox.Text?.Trim() ?? "";
        if (!IsSafeExeStringText(original) || !IsSafeExeStringText(replacement))
        {
            _statusText.Text = "UI text edits need non-empty printable text.";
            return;
        }

        if (replacement.Length > original.Length)
        {
            _statusText.Text = $"Use {original.Length} or fewer characters for this fixed UI text slot.";
            return;
        }

        string path = Path.Combine(_workspace.RootPath, "ui-text-edit-plan.json");
        var plan = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            kind = "exe-fixed-slot-string-plan",
            originalText = original,
            replacementText = replacement,
            maxLength = original.Length,
            nulledByteCount = original.Length - replacement.Length,
            sourceTool = "tools/Export-SpyroExeStringPatchTest.ps1",
            planOnlyArguments = new[]
            {
                "-OriginalText", original,
                "-ReplacementText", replacement,
                "-PlanOnly"
            },
            notes = new[]
            {
                "This edits one unique executable ASCII string.",
                "Replacement text must fit inside the original fixed-length slot.",
                "Use Create UI Text CUE to verify the string exists uniquely in the selected original BIN."
            }
        };

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(plan, NewJsonOptions()));
        _statusText.Text = $"Saved UI text plan: {original} -> {replacement}.";
    }

    private async Task CreateLevelTextCueAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before creating a lettering test.";
            return;
        }
        if (!IsHomeWorld(_currentLevel))
        {
            _statusText.Text = "Portal/name lettering tests are available only on homeworlds.";
            return;
        }

        TextTargetEntry? target = _textTargets.FindForLevel(_currentLevel);
        if (target == null)
        {
            _statusText.Text = "This level does not have a mapped fixed text slot yet.";
            return;
        }

        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before creating a lettering test.";
            return;
        }

        string replacement = TextTargetCatalog.NormalizeReplacement(_levelTextReplacementBox.Text ?? "");
        if (!TextTargetCatalog.IsSafeReplacement(replacement, target.MaxLength))
        {
            _statusText.Text = $"Use {target.MaxLength} or fewer safe characters for {target.OriginalText}.";
            return;
        }

        string outputDir = EnsureUserOutputDirectory();
        string outputPrefix = Path.Combine(outputDir, $"Spyro Editor - {_currentLevel.DisplayName} - Lettering - {FriendlyFilePart(replacement)}");

        _statusText.Text = $"Creating lettering test for {_currentLevel.DisplayName}...";
        try
        {
            LevelTextPatchResult result = await LevelTextPatchExporter.ExportAsync(new LevelTextPatchRequest(
                SourceImagePath: sourceImage,
                SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
                OutputPrefix: outputPrefix,
                Target: target,
                ReplacementText: replacement,
                WriteImage: true));

            _statusText.Text = $"Created {Path.GetFileName(result.OutputCuePath)} for {target.OriginalText} -> {replacement}.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create lettering test: {ex.Message}";
        }
    }

    private async Task CreateExeStringCueAsync()
    {
        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before creating a UI text test.";
            return;
        }

        string original = _exeStringOriginalBox.Text?.Trim() ?? "";
        string replacement = _exeStringReplacementBox.Text?.Trim() ?? "";
        if (!IsSafeExeStringText(original) || !IsSafeExeStringText(replacement))
        {
            _statusText.Text = "Choose printable original UI text and replacement text.";
            return;
        }

        if (replacement.Length > original.Length)
        {
            _statusText.Text = $"Use {original.Length} or fewer characters for this fixed UI text slot.";
            return;
        }

        string outputDir = EnsureUserOutputDirectory();
        string outputPrefix = Path.Combine(outputDir, $"Spyro Editor - UI Text - {FriendlyFilePart(original)} to {FriendlyFilePart(replacement)}");

        _statusText.Text = $"Creating UI text test: {original} -> {replacement}...";
        try
        {
            ExeStringPatchResult result = await ExeStringPatchExporter.ExportAsync(new ExeStringPatchRequest(
                SourceImagePath: sourceImage,
                SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
                OutputPrefix: outputPrefix,
                OriginalText: original,
                ReplacementText: replacement,
                WriteImage: true));

            _statusText.Text = $"Created {Path.GetFileName(result.OutputCuePath)} for {original} -> {replacement}.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create UI text test: {ex.Message}";
        }
    }

    private async Task CreateObjectTestBinAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before creating a test.";
            return;
        }

        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before creating a test.";
            return;
        }

        await SaveCurrentEditsAsync();
        IReadOnlyList<EditedLevelExportTarget> targets = FindEditedLevelExportTargets();
        if (targets.Count == 0)
        {
            _statusText.Text = "No saved edits were found. Move, edit, add, remove an object, or change terrain first.";
            return;
        }

        TerrainPatchRiskDecision terrainRiskDecision = await ConfirmTerrainPatchRisksAsync();
        if (terrainRiskDecision == TerrainPatchRiskDecision.Review)
        {
            await ShowTerrainEditReviewAsync(initialFilterIndex: 1);
            _statusText.Text = "Review terrain geometry patch risks, then create the test BIN again when it looks right.";
            return;
        }
        if (terrainRiskDecision == TerrainPatchRiskDecision.Cancel)
        {
            _statusText.Text = "Canceled test BIN creation so you can review terrain geometry patch risks.";
            return;
        }

        string outputDir = EnsureUserOutputDirectory();
        _statusText.Text = targets.Count == 1
            ? $"Creating test for {targets[0].Level.DisplayName}..."
            : $"Creating one test for saved edits across {targets.Count} levels...";
        try
        {
            CombinedTestBinResult result = await CreateAllSavedEditsTestAsync(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                outputDir,
                targets);
            if (!result.WroteImage)
            {
                string skipped = result.SkippedEdits > 0
                    ? $" Skipped {result.SkippedEdits} edit(s) that are not export-ready yet."
                    : " Move, edit, add, remove an object, or raise/lower terrain first.";
                _statusText.Text = $"No source patches were ready across the saved edits.{skipped}";
                return;
            }

            string levelSummary = result.PatchedLevelNames.Count == 0
                ? ""
                : $" for {FormatShortList(result.PatchedLevelNames, 4)}";
            string skippedSummary = result.SkippedEdits > 0
                ? $" Skipped {result.SkippedEdits} edit(s) that are not export-ready yet."
                : "";
            string folderStatus = OpenContainingFolderStatus(result.OutputCuePath);
            _statusText.Text = $"Created all-edits test {Path.GetFileName(result.OutputCuePath)}{levelSummary}: {result.ObjectPatches} object patch(es), {result.TerrainPatches} terrain patch(es).{skippedSummary}{folderStatus}";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create test: {ex.Message}";
        }
    }

    private IReadOnlyList<EditedLevelExportTarget> FindEditedLevelExportTargets()
    {
        List<EditedLevelExportTarget> targets = [];
        foreach (LevelDefinition level in _catalog.Levels)
        {
            bool hasObjectEdits = level.HasSourceTable && NativeEditFileHasEdits(Path.Combine(_workspace.RootPath, $"{level.Key}-native-edits.json"));
            bool hasTerrainEdits = TerrainEditFileHasEdits(Path.Combine(_workspace.RootPath, $"{level.Key}-terrain-edits.json"));
            bool hasCustomTerrainTextures = CustomTerrainTextureFileHasTextures(CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, level.Key));
            if (hasObjectEdits || hasTerrainEdits || hasCustomTerrainTextures)
                targets.Add(new EditedLevelExportTarget(level, hasObjectEdits, hasTerrainEdits, hasCustomTerrainTextures));
        }

        return targets;
    }

    private async Task<CombinedTestBinResult> CreateAllSavedEditsTestAsync(
        string sourceImage,
        string sourceCue,
        string outputDir,
        IReadOnlyList<EditedLevelExportTarget> targets)
    {
        string tempDir = Path.Combine(outputDir, "_combined-build");
        Directory.CreateDirectory(tempDir);
        foreach (string staleImage in Directory.EnumerateFiles(tempDir, "*.bin").Concat(Directory.EnumerateFiles(tempDir, "*.cue")))
        {
            try
            {
                File.Delete(staleImage);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        string currentImage = sourceImage;
        string currentCue = sourceCue;
        int step = 0;
        int objectPatches = 0;
        int terrainPatches = 0;
        int skippedEdits = 0;
        List<string> patchedLevelNames = [];
        List<object> stepSummaries = [];

        foreach (EditedLevelExportTarget target in targets)
        {
            bool patchedThisLevel = false;
            if (target.HasObjectEdits)
            {
                string editsPath = Path.Combine(_workspace.RootPath, $"{target.Level.Key}-native-edits.json");
                string outputPrefix = Path.Combine(tempDir, $"{++step:00}-{SafeOutputPart(target.Level.DisplayName)}-objects");
                MobySourcePatchResult objectResult = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
                    SourceImagePath: currentImage,
                    SourceCuePath: currentCue,
                    OutputPrefix: outputPrefix,
                    Level: target.Level,
                    NativeEditsPath: editsPath,
                    WriteImage: true));

                objectPatches += objectResult.Plan.PatchCount;
                skippedEdits += objectResult.Plan.SkippedEdits.Count;
                patchedThisLevel |= objectResult.WroteImage;
                stepSummaries.Add(new
                {
                    level = target.Level.DisplayName,
                    kind = "objects",
                    objectResult.Plan.PatchCount,
                    skipped = objectResult.Plan.SkippedEdits.Count,
                    plan = objectResult.OutputPlanPath
                });
                if (objectResult.WroteImage)
                {
                    currentImage = objectResult.OutputImagePath;
                    currentCue = objectResult.OutputCuePath;
                }
            }

            if (target.HasTerrainEdits || target.HasCustomTerrainTextures)
            {
                string outputName = $"{++step:00}-{SafeOutputPart(target.Level.DisplayName)}-terrain";
                TerrainPatchResult? terrainResult = await TryCreateTerrainTestAsync(target.Level, currentImage, currentCue, tempDir, outputName);
                if (terrainResult != null)
                {
                    terrainPatches += terrainResult.Plan.PatchCount;
                    skippedEdits += terrainResult.Plan.SkippedEdits.Count;
                    patchedThisLevel |= terrainResult.WroteImage;
                    stepSummaries.Add(new
                    {
                        level = target.Level.DisplayName,
                        kind = "terrain",
                        terrainResult.Plan.PatchCount,
                        skipped = terrainResult.Plan.SkippedEdits.Count,
                        plan = terrainResult.OutputPlanPath
                    });
                    if (terrainResult.WroteImage)
                    {
                        currentImage = terrainResult.OutputImagePath;
                        currentCue = terrainResult.OutputCuePath;
                    }
                }
            }

            if (patchedThisLevel && !patchedLevelNames.Contains(target.Level.DisplayName, StringComparer.OrdinalIgnoreCase))
                patchedLevelNames.Add(target.Level.DisplayName);
        }

        if (objectPatches + terrainPatches == 0 || !File.Exists(currentImage))
            return new CombinedTestBinResult("", "", objectPatches, terrainPatches, skippedEdits, patchedLevelNames, false);

        string finalPrefix = Path.Combine(outputDir, "Spyro Editor - All Saved Edits");
        string finalImage = $"{finalPrefix}.bin";
        string finalCue = $"{finalPrefix}.cue";
        string finalSummary = $"{finalPrefix}.combined-export-summary.json";
        File.Copy(currentImage, finalImage, true);
        string cueText = BuildCueText(currentCue, Path.GetFileName(finalImage));
        await File.WriteAllTextAsync(finalCue, cueText, Encoding.ASCII);
        await File.WriteAllTextAsync(finalSummary, JsonSerializer.Serialize(new
        {
            generatedAt = DateTime.Now.ToString("s"),
            sourceImage,
            outputImage = finalImage,
            outputCue = finalCue,
            levelCount = patchedLevelNames.Count,
            levels = patchedLevelNames,
            objectPatches,
            terrainPatches,
            skippedEdits,
            steps = stepSummaries
        }, new JsonSerializerOptions { WriteIndented = true }));

        return new CombinedTestBinResult(finalImage, finalCue, objectPatches, terrainPatches, skippedEdits, patchedLevelNames, true);
    }

    private static string BuildCueText(string sourceCuePath, string outputBinName)
    {
        if (!File.Exists(sourceCuePath))
            return $"FILE \"{outputBinName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n";

        string[] lines = File.ReadAllLines(sourceCuePath, Encoding.ASCII);
        bool replaced = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!replaced && trimmed.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(" BINARY", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"FILE \"{outputBinName}\" BINARY";
                replaced = true;
            }
        }

        return string.Join('\n', replaced ? lines : new[] { $"FILE \"{outputBinName}\" BINARY" }.Concat(lines)) + "\n";
    }

    private string BuildOtherSavedObjectEditHint()
    {
        if (_currentLevel == null)
            return "";

        List<string> names = [];
        foreach (LevelDefinition level in _catalog.Levels)
        {
            if (string.Equals(level.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            string editsPath = Path.Combine(_workspace.RootPath, $"{level.Key}-native-edits.json");
            if (NativeEditFileHasEdits(editsPath))
                names.Add(level.DisplayName);
        }

        if (names.Count == 0)
            return "";

        string levelList = names.Count == 1
            ? names[0]
            : string.Join(", ", names.Take(names.Count - 1)) + ", and " + names[^1];
        return $" Other saved object edits exist for {levelList}; switch to that level and run Create BIN to make its test disc too.";
    }

    private static bool NativeEditFileHasEdits(string path)
    {
        return JsonEditFileHasEdits(path);
    }

    private static bool TerrainEditFileHasEdits(string path)
    {
        return JsonEditFileHasEdits(path);
    }

    private static bool JsonEditFileHasEdits(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("editCount", out JsonElement countElement) &&
                countElement.ValueKind == JsonValueKind.Number &&
                countElement.TryGetInt32(out int count) &&
                count > 0)
            {
                return true;
            }

            return root.TryGetProperty("edits", out JsonElement editsElement) &&
                editsElement.ValueKind == JsonValueKind.Array &&
                editsElement.GetArrayLength() > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool CustomTerrainTextureFileHasTextures(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("textureCount", out JsonElement countElement) &&
                countElement.ValueKind == JsonValueKind.Number &&
                countElement.TryGetInt32(out int count) &&
                count > 0)
            {
                return true;
            }

            return root.TryGetProperty("textures", out JsonElement texturesElement) &&
                texturesElement.ValueKind == JsonValueKind.Array &&
                texturesElement.GetArrayLength() > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static string SafeOutputPart(string value)
    {
        string cleaned = string.Join("-", value
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        cleaned = cleaned.Replace(' ', '-');
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(cleaned) ? "level" : cleaned.Trim('-');
    }

    private static string FormatShortList(IReadOnlyList<string> values, int limit)
    {
        if (values.Count == 0)
            return "";
        if (values.Count <= limit)
            return values.Count == 1 ? values[0] : string.Join(", ", values.Take(values.Count - 1)) + ", and " + values[^1];

        return $"{string.Join(", ", values.Take(limit))}, and {values.Count - limit} more";
    }

    private async Task CreateObjectCandidateBinAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before creating a candidate test.";
            return;
        }

        if (!_currentLevel.HasSourceTable)
        {
            _statusText.Text = $"{_currentLevel.DisplayName} does not have source moby patching mapped yet.";
            return;
        }

        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before creating a candidate test.";
            return;
        }

        await SaveCurrentEditsAsync();
        string editsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-native-edits.json");
        if (!File.Exists(editsPath))
        {
            _statusText.Text = "Add or edit an object before creating a candidate test.";
            return;
        }

        string outputDir = EnsureUserOutputDirectory();
        string outputPrefix = Path.Combine(outputDir, $"Spyro Editor - {_currentLevel.DisplayName} - Candidate Object Test");
        string outputBin = $"{outputPrefix}.bin";
        string outputCue = $"{outputPrefix}.cue";

        try
        {
            MobySourcePatchPlan candidatePlan = MobySourcePatchExporter.BuildPlan(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                outputBin,
                outputCue,
                _currentLevel,
                editsPath,
                allowPlanOnlyActorPackageImports: true);
            List<MobyActorPackageImportPreview> candidatePreviews = candidatePlan.PackageImportPreviews
                .Where(preview =>
                    preview.CanWriteImage &&
                    (string.Equals(preview.RecipeStatus, "experimental-plan-only", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(preview.RecipeStatus, "experimental-image-write", StringComparison.OrdinalIgnoreCase)) &&
                    preview.GuardReason.Contains("disposable candidate mode", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidatePreviews.Count == 0)
            {
                string skipped = candidatePlan.SkippedEdits.Count > 0 ? $" {candidatePlan.SkippedEdits[0]}" : "";
                _statusText.Text = $"No guarded cross-level candidate is ready for {_currentLevel.DisplayName}.{skipped}";
                return;
            }

            MobySourcePatchResult result = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
                SourceImagePath: sourceImage,
                SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
                OutputPrefix: outputPrefix,
                Level: _currentLevel,
                NativeEditsPath: editsPath,
                WriteImage: true,
                AllowPlanOnlyActorPackageImports: true));

            string labels = string.Join(", ", candidatePreviews
                .Select(preview => string.IsNullOrWhiteSpace(preview.Label) ? preview.TemplateId : preview.Label)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3));
            _statusText.Text = result.WroteImage
                ? $"Created disposable candidate {Path.GetFileName(result.OutputCuePath)} for {labels}. Use Open Candidate Tests to test it in DuckStation before trusting this recipe. Checklist: {Path.GetFileName(await MobyCandidateValidationReportWriter.WriteAsync(result))}{OpenContainingFolderStatus(result.OutputCuePath)}"
                : $"No candidate patches were written for {_currentLevel.DisplayName}.";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not create candidate test: {ex.Message}";
        }
    }

    private void OpenCandidateTestsLauncher()
    {
        string launcherPath = FindCandidateTestsLauncherPath();
        if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
        {
            _statusText.Text = "Candidate test launcher was not found. Create a candidate BIN first, then use the launcher in the workspace.";
            return;
        }

        try
        {
            OpenPath(launcherPath);
            _statusText.Text = $"Opened {Path.GetFileName(launcherPath)}. Choose the candidate CUE you want to test in DuckStation.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open candidate tests: {ex.Message}";
        }
    }

    private string FindCandidateTestsLauncherPath()
    {
        string preferred = OperatingSystem.IsWindows()
            ? Path.Combine(_workspace.RootPath, "Launch Cross-Level Candidate Tests.bat")
            : Path.Combine(_workspace.RootPath, "Launch Cross-Level Candidate Tests.command");
        if (File.Exists(preferred))
            return preferred;

        string fallback = OperatingSystem.IsWindows()
            ? Path.Combine(_workspace.RootPath, "Launch Cross-Level Candidate Tests.command")
            : Path.Combine(_workspace.RootPath, "Launch Cross-Level Candidate Tests.bat");
        return File.Exists(fallback) ? fallback : "";
    }

    private void OpenCandidateCue(CandidateResultItem candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.CuePath) || !File.Exists(candidate.CuePath))
        {
            _statusText.Text = $"Candidate CUE was not found for {candidate.Label}. Recreate the candidate BIN, then try again.";
            return;
        }

        try
        {
            OpenPath(candidate.CuePath);
            _statusText.Text = $"Opened {Path.GetFileName(candidate.CuePath)}. Test it in DuckStation, then save the result here.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open candidate CUE: {ex.Message}";
        }
    }

    private void OpenPath(string path)
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsMacOS())
        {
            startInfo = new ProcessStartInfo("open") { UseShellExecute = false };
            startInfo.ArgumentList.Add(path);
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = _workspace.RootPath };
        }
        else
        {
            startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            startInfo.ArgumentList.Add(path);
        }

        Process.Start(startInfo);
    }

    private string OpenContainingFolderStatus(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string? folder = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return "";

        try
        {
            OpenPath(folder);
            return " Opened the output folder.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return $" Output folder: {folder}";
        }
    }

    private async Task RecordCandidateTestResultAsync()
    {
        List<CandidateResultItem> candidates = LoadCandidateResultItems()
            .OrderByDescending(item => _currentLevel != null && string.Equals(item.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => string.Equals(NormalizeCandidateStatus(item.Status), "untested", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.LastWrittenUtc)
            .ThenBy(item => item.LevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
        {
            _statusText.Text = "No candidate result files exist yet. Create a candidate BIN first.";
            return;
        }

        ComboBox candidateBox = new()
        {
            ItemsSource = candidates,
            SelectedIndex = 0,
            MinWidth = 420
        };
        ComboBox statusBox = new()
        {
            ItemsSource = new[] { "tested-pass", "tested-fail", "untested" },
            SelectedItem = NormalizeCandidateStatus(candidates[0].Status),
            MinWidth = 180
        };
        CheckBox bootedBox = NewEvidenceBox("Level booted", candidates[0].BootedLevel);
        CheckBox appearedBox = NewEvidenceBox("Object appeared", candidates[0].ObjectAppeared);
        CheckBox mobyRecordsBox = NewEvidenceBox("Moby records loaded", candidates[0].MobyRecordsPresent);
        CheckBox actorRootsBox = NewEvidenceBox("Actor roots loaded", candidates[0].ActorRootsPresent);
        CheckBox behaviorBox = NewEvidenceBox("Behavior was correct", candidates[0].BehaviorCorrect);
        CheckBox regressionBox = NewEvidenceBox("No nearby object broke", candidates[0].NoNearbyRegression);
        TextBox testerBox = new() { Text = candidates[0].Tester, MinWidth = 240 };
        TextBox notesBox = new()
        {
            Text = candidates[0].Notes,
            MinWidth = 420,
            Height = 110,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        StackPanel behaviorChecksPanel = new() { Spacing = 6 };
        List<CheckBox> behaviorCheckBoxes = [];
        Button openCueButton = NewButton("Open Selected CUE");
        openCueButton.Click += (_, _) =>
        {
            CandidateResultItem selected = candidateBox.SelectedItem as CandidateResultItem ?? candidates[0];
            OpenCandidateCue(selected);
        };
        Button verifyRamButton = NewButton("Verify Boot RAM");
        verifyRamButton.Click += async (_, _) =>
        {
            try
            {
                CandidateResultItem selected = candidateBox.SelectedItem as CandidateResultItem ?? candidates[0];
                if (string.IsNullOrWhiteSpace(selected.PlanPath) || !File.Exists(selected.PlanPath))
                {
                    _statusText.Text = "This candidate does not have a patch plan to verify against RAM.";
                    return;
                }

                string? ramPath = await PickRamDumpAsync(StorageProvider, $"Booted candidate RAM for {selected.LevelName}");
                if (string.IsNullOrWhiteSpace(ramPath))
                    return;

                CrossLevelCandidateRamVerification verification = CrossLevelCandidateRamVerifier.VerifyPlanFile(selected.PlanPath, ramPath);
                bootedBox.IsChecked = verification.BootedLevel;
                mobyRecordsBox.IsChecked = verification.Checks.Count > 0 ? verification.ObjectRecordsMatch : null;
                actorRootsBox.IsChecked = verification.RootChecks.Count > 0 ? verification.ActorRootsMatch : null;
                appearedBox.IsChecked = verification.BootedLevel && verification.ObjectRecordsMatch && verification.ActorRootsMatch;
                notesBox.Text = AppendCandidateRamVerificationNote(notesBox.Text, verification);
                _statusText.Text = verification.Summary;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
            {
                Debug.WriteLine(ex);
                _statusText.Text = $"Could not verify candidate RAM: {ex.Message}";
            }
        };

        void LoadSelectedCandidate()
        {
            CandidateResultItem selected = candidateBox.SelectedItem as CandidateResultItem ?? candidates[0];
            statusBox.SelectedItem = NormalizeCandidateStatus(selected.Status);
            bootedBox.IsChecked = selected.BootedLevel;
            appearedBox.IsChecked = selected.ObjectAppeared;
            mobyRecordsBox.IsChecked = selected.MobyRecordsPresent;
            actorRootsBox.IsChecked = selected.ActorRootsPresent;
            behaviorBox.IsChecked = selected.BehaviorCorrect;
            regressionBox.IsChecked = selected.NoNearbyRegression;
            testerBox.Text = selected.Tester;
            notesBox.Text = selected.Notes;
            LoadBehaviorCheckBoxes(selected, behaviorChecksPanel, behaviorCheckBoxes);
        }

        candidateBox.SelectionChanged += (_, _) => LoadSelectedCandidate();
        LoadSelectedCandidate();

        Window dialog = new()
        {
            Title = "Record Candidate Result",
            Width = 620,
            Height = 620,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildCandidateResultDialogContent(dialog, candidateBox, statusBox, openCueButton, verifyRamButton, bootedBox, appearedBox, mobyRecordsBox, actorRootsBox, behaviorBox, regressionBox, behaviorChecksPanel, behaviorCheckBoxes, testerBox, notesBox));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        CandidateResultItem candidate = candidateBox.SelectedItem as CandidateResultItem ?? candidates[0];
        try
        {
            JsonNode? rootNode = JsonNode.Parse(await File.ReadAllTextAsync(candidate.ResultPath));
            if (rootNode is not JsonObject root)
                throw new InvalidDataException("Candidate result file did not contain a JSON object.");

            JsonObject result = root["result"] as JsonObject ?? new JsonObject();
            root["result"] = result;
            string status = statusBox.SelectedItem?.ToString() ?? "untested";
            result["status"] = status;
            result["testedAt"] = string.Equals(status, "untested", StringComparison.OrdinalIgnoreCase)
                ? ""
                : DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            result["tester"] = testerBox.Text?.Trim() ?? "";
            result["emulator"] = "DuckStation";
            result["bootedLevel"] = BoolNode(bootedBox.IsChecked);
            result["objectAppeared"] = BoolNode(appearedBox.IsChecked);
            result["mobyRecordsPresent"] = BoolNode(mobyRecordsBox.IsChecked);
            result["actorRootsPresent"] = BoolNode(actorRootsBox.IsChecked);
            result["behaviorCorrect"] = BoolNode(behaviorBox.IsChecked);
            result["noNearbyRegression"] = BoolNode(regressionBox.IsChecked);
            result["notes"] = notesBox.Text?.Trim() ?? "";
            SaveBehaviorCheckBoxes(root, behaviorCheckBoxes);

            await File.WriteAllTextAsync(candidate.ResultPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            if (File.Exists(candidate.PlanPath))
                await MobyCandidateValidationReportWriter.WriteForPlanAsync(candidate.PlanPath);

            _statusText.Text = $"Recorded {candidate.Label} result as {status}.";
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            _statusText.Text = $"Could not record candidate result: {ex.Message}";
        }
    }

    private static CheckBox NewEvidenceBox(string text, bool? value)
    {
        return new CheckBox
        {
            Content = text,
            IsThreeState = true,
            IsChecked = value
        };
    }

    private static JsonNode? BoolNode(bool? value)
    {
        return value.HasValue ? JsonValue.Create(value.Value) : null;
    }

    private static string NormalizeCandidateStatus(string status)
    {
        if (status.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("verified", StringComparison.OrdinalIgnoreCase))
            return "tested-pass";
        if (status.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("reject", StringComparison.OrdinalIgnoreCase))
            return "tested-fail";
        return "untested";
    }

    private static string AppendCandidateRamVerificationNote(string currentNotes, CrossLevelCandidateRamVerification verification)
    {
        StringBuilder builder = new();
        if (!string.IsNullOrWhiteSpace(currentNotes))
        {
            builder.AppendLine(currentNotes.Trim());
            builder.AppendLine();
        }

        builder.AppendLine($"RAM verification: {verification.Summary}");
        builder.AppendLine($"RAM: {verification.RamPath}");
        foreach (CrossLevelCandidateRamRecordCheck check in verification.Checks.Take(6))
            builder.AppendLine($"- T{check.TrueIndex} {check.Label}: {(check.Matched ? "match" : "mismatch")} ({check.MatchedBytes}/{check.ExpectedBytes}) {check.Details}");
        foreach (CrossLevelCandidateRamRootCheck check in verification.RootChecks.Take(6))
            builder.AppendLine($"- Actor root {check.Slot} -> {check.ActorId}: {(check.Matched ? "match" : "mismatch")} {check.Details}");
        return builder.ToString().Trim();
    }

    private static void LoadBehaviorCheckBoxes(CandidateResultItem selected, StackPanel panel, List<CheckBox> boxes)
    {
        panel.Children.Clear();
        boxes.Clear();
        if (selected.BehaviorChecks.Count == 0)
        {
            panel.Children.Add(NewSmallNote("No family-specific behavior checks were generated for this candidate."));
            return;
        }

        panel.Children.Add(NewSmallNote("Mark these only after testing the candidate in-game."));
        foreach (CandidateBehaviorCheckItem check in selected.BehaviorChecks)
        {
            CheckBox box = new()
            {
                Content = check.Label,
                Tag = check.Id,
                IsThreeState = true,
                IsChecked = check.Passed
            };
            boxes.Add(box);
            panel.Children.Add(box);
        }
    }

    private static void SaveBehaviorCheckBoxes(JsonObject root, IReadOnlyList<CheckBox> boxes)
    {
        if (boxes.Count == 0)
            return;

        JsonArray checks = root["behaviorChecks"] as JsonArray ?? new JsonArray();
        root["behaviorChecks"] = checks;
        foreach (CheckBox box in boxes)
        {
            string id = box.Tag?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(id))
                continue;

            JsonObject? check = checks
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(item["id"]?.GetValue<string>(), id, StringComparison.OrdinalIgnoreCase));
            if (check == null)
            {
                check = new JsonObject
                {
                    ["id"] = id,
                    ["label"] = box.Content?.ToString() ?? id
                };
                checks.Add(check);
            }
            check["passed"] = BoolNode(box.IsChecked);
        }
    }

    private List<CandidateResultItem> LoadCandidateResultItems()
    {
        string objectsDir = Path.Combine(_workspace.RootPath, "_local", "objects");
        if (!Directory.Exists(objectsDir))
            return new List<CandidateResultItem>();

        List<CandidateResultItem> items = new();
        foreach (string path in Directory.GetFiles(objectsDir, "*.candidate-result.json"))
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement root = document.RootElement;
                JsonElement candidate = root.TryGetProperty("candidate", out JsonElement candidateElement) && candidateElement.ValueKind == JsonValueKind.Object
                    ? candidateElement
                    : root;
                JsonElement result = root.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Object
                    ? resultElement
                    : default;

                string label = Path.GetFileNameWithoutExtension(path).Replace(".candidate-result", "", StringComparison.Ordinal);
                if (candidate.TryGetProperty("recipes", out JsonElement recipes) && recipes.ValueKind == JsonValueKind.Array)
                {
                    JsonElement first = recipes.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object)
                        label = GetJsonString(first, "label", label);
                }

                items.Add(new CandidateResultItem(
                    Label: label,
                    LevelKey: GetJsonString(candidate, "levelKey"),
                    LevelName: GetJsonString(candidate, "levelName", "Unknown level"),
                    ResultPath: path,
                    PlanPath: GetJsonString(candidate, "patchPlanPath"),
                    CuePath: GetJsonString(candidate, "cuePath"),
                    Status: result.ValueKind == JsonValueKind.Object ? GetJsonString(result, "status", "untested") : "untested",
                    BootedLevel: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "bootedLevel") : null,
                    ObjectAppeared: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "objectAppeared") : null,
                    MobyRecordsPresent: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "mobyRecordsPresent") : null,
                    ActorRootsPresent: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "actorRootsPresent") : null,
                    BehaviorCorrect: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "behaviorCorrect") : null,
                    NoNearbyRegression: result.ValueKind == JsonValueKind.Object ? GetJsonNullableBool(result, "noNearbyRegression") : null,
                    BehaviorChecks: LoadCandidateBehaviorChecks(root),
                    Tester: result.ValueKind == JsonValueKind.Object ? GetJsonString(result, "tester") : "",
                    Notes: result.ValueKind == JsonValueKind.Object ? GetJsonString(result, "notes") : "",
                    LastWrittenUtc: File.GetLastWriteTimeUtc(path)));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _statusText.Text = $"Skipped unreadable candidate result {Path.GetFileName(path)}: {ex.Message}";
            }
        }

        return items;
    }

    private static bool? GetJsonNullableBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return null;
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static IReadOnlyList<CandidateBehaviorCheckItem> LoadCandidateBehaviorChecks(JsonElement root)
    {
        if (!root.TryGetProperty("behaviorChecks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Array)
            return [];

        List<CandidateBehaviorCheckItem> result = [];
        foreach (JsonElement check in checks.EnumerateArray())
        {
            if (check.ValueKind != JsonValueKind.Object)
                continue;
            result.Add(new CandidateBehaviorCheckItem(
                Id: GetJsonString(check, "id"),
                Label: GetJsonString(check, "label", "Behavior check"),
                Passed: GetJsonNullableBool(check, "passed")));
        }
        return result;
    }

    private Control BuildCandidateResultDialogContent(
        Window dialog,
        ComboBox candidateBox,
        ComboBox statusBox,
        Button openCueButton,
        Button verifyRamButton,
        CheckBox bootedBox,
        CheckBox appearedBox,
        CheckBox mobyRecordsBox,
        CheckBox actorRootsBox,
        CheckBox behaviorBox,
        CheckBox regressionBox,
        StackPanel behaviorChecksPanel,
        IReadOnlyList<CheckBox> behaviorCheckBoxes,
        TextBox testerBox,
        TextBox notesBox)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(NewSmallNote("Record what happened after opening the candidate CUE in DuckStation. Leave a checkbox blank if you did not test that part yet."));
        panel.Children.Add(BuildSimpleField("Candidate", candidateBox));
        panel.Children.Add(BuildSimpleField("Result", statusBox));
        panel.Children.Add(openCueButton);
        panel.Children.Add(verifyRamButton);
        panel.Children.Add(bootedBox);
        panel.Children.Add(appearedBox);
        panel.Children.Add(mobyRecordsBox);
        panel.Children.Add(actorRootsBox);
        panel.Children.Add(behaviorBox);
        panel.Children.Add(regressionBox);
        panel.Children.Add(BuildSimpleField("Behavior Proof", behaviorChecksPanel));
        panel.Children.Add(BuildSimpleField("Tester", testerBox));
        panel.Children.Add(BuildSimpleField("Notes", notesBox));
        TextBlock proofWarning = NewSmallNote("");
        proofWarning.Foreground = new SolidColorBrush(Color.FromRgb(176, 69, 55));
        proofWarning.IsVisible = false;
        panel.Children.Add(proofWarning);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button save = NewButton("Save Result");
        cancel.Click += (_, _) => dialog.Close(false);
        save.Click += (_, _) =>
        {
            string warning = CandidatePassProofWarning(
                statusBox.SelectedItem?.ToString() ?? "",
                bootedBox,
                appearedBox,
                mobyRecordsBox,
                actorRootsBox,
                behaviorBox,
                regressionBox,
                behaviorCheckBoxes);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                proofWarning.Text = warning;
                proofWarning.IsVisible = true;
                return;
            }

            dialog.Close(true);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        return panel;
    }

    private static Control BuildSimpleField(string label, Control field)
    {
        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        });
        panel.Children.Add(field);
        return panel;
    }

    private static string CandidatePassProofWarning(
        string status,
        CheckBox bootedBox,
        CheckBox appearedBox,
        CheckBox mobyRecordsBox,
        CheckBox actorRootsBox,
        CheckBox behaviorBox,
        CheckBox regressionBox,
        IReadOnlyList<CheckBox> behaviorCheckBoxes)
    {
        if (!string.Equals(NormalizeCandidateStatus(status), "tested-pass", StringComparison.OrdinalIgnoreCase))
            return "";

        List<string> missing = [];
        AddMissing(missing, bootedBox, "level booted");
        AddMissing(missing, appearedBox, "object appeared");
        AddMissing(missing, mobyRecordsBox, "moby records loaded");
        AddMissing(missing, actorRootsBox, "actor roots loaded");
        AddMissing(missing, behaviorBox, "behavior was correct");
        AddMissing(missing, regressionBox, "no nearby object broke");
        if (behaviorCheckBoxes.Any(box => box.IsChecked != true))
            missing.Add("all behavior proof checks");

        return missing.Count == 0
            ? ""
            : $"A tested-pass result needs proof for: {string.Join(", ", missing)}.";
    }

    private static void AddMissing(List<string> missing, CheckBox box, string label)
    {
        if (box.IsChecked != true)
            missing.Add(label);
    }

    private static string BuildCreatedObjectPatchSummary(MobySourcePatchPlan? plan)
    {
        if (plan == null)
            return "";

        List<MobyActorPackageImportPreview> packagePreviews = plan.PackageImportPreviews
            .Where(preview => !string.IsNullOrWhiteSpace(preview.TemplateId))
            .ToList();
        if (packagePreviews.Count == 0)
            return "";

        List<string> included = packagePreviews
            .Where(preview => preview.CanWriteImage)
            .Select(preview => string.IsNullOrWhiteSpace(preview.Label) ? preview.TemplateId : preview.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        List<string> skipped = packagePreviews
            .Where(preview => !preview.CanWriteImage)
            .Select(preview => string.IsNullOrWhiteSpace(preview.Label) ? preview.TemplateId : preview.Label)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        List<string> parts = new();
        if (included.Count > 0)
            parts.Add($"included cross-level package support for {string.Join(", ", included)}");
        if (skipped.Count > 0)
            parts.Add($"left {string.Join(", ", skipped)} as preview-only");

        return parts.Count == 0 ? "" : $" Object imports: {string.Join("; ", parts)}.";
    }

    private static string BuildCreatedTerrainPatchSummary(TerrainPatchPlan? plan)
    {
        if (plan == null || plan.Patches.Count == 0)
            return "";

        int collisionAdd = plan.Patches.Count(patch => string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase));
        int playableHeight = plan.Patches.Count(patch =>
            patch.Kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(patch.Kind, "collision-triangle-degenerate", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(patch.Kind, "collision-triangle-side-wall", StringComparison.OrdinalIgnoreCase));
        int visualHeight = plan.Patches.Count(patch => patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
        int textureSwitches = plan.Patches.Count(patch => string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
        int faceRemoval = plan.Patches.Count(patch => patch.Kind.StartsWith("terrain-face-degenerate-", StringComparison.OrdinalIgnoreCase));
        int collisionRemoval = plan.Patches.Count(patch => string.Equals(patch.Kind, "collision-triangle-degenerate", StringComparison.OrdinalIgnoreCase));
        int faceAdds = plan.Patches.Count(patch => string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase));
        int sideWallEdges = plan.TerrainSideWalls.Sum(summary => summary.ExposedEdgeCount);
        int sideWallFaces = plan.TerrainSideWalls.Sum(summary => summary.EmittedFaceCount);
        string sideWallTextures = FormatTerrainSideWallTextureSummary(plan);
        int textureArt = plan.CustomTextureBytePatchCount;
        List<string> parts = new();
        if (playableHeight > 0)
            parts.Add($"{playableHeight} playable height");
        if (visualHeight > 0)
            parts.Add($"{visualHeight} visual height");
        if (faceRemoval > 0)
            parts.Add($"{faceRemoval} removed face");
        if (collisionRemoval > 0)
            parts.Add($"{collisionRemoval} removed collision");
        if (faceAdds > 0)
            parts.Add($"{faceAdds} added face");
        if (collisionAdd > 0)
            parts.Add($"{collisionAdd} added collision");
        if (sideWallFaces > 0)
            parts.Add($"{sideWallFaces}/{Math.Max(sideWallEdges, sideWallFaces)} solid textured side-wall edge{sideWallTextures}");
        if (textureSwitches > 0)
            parts.Add($"{textureSwitches} surface switch");
        if (textureArt > 0)
            parts.Add($"{textureArt} texture art");

        return parts.Count == 0
            ? ""
            : $" Terrain included: {string.Join(", ", parts)} patch(es).";
    }

    private static string FormatTerrainSideWallTextureSummary(TerrainPatchPlan plan)
    {
        int[] textureIds = plan.TerrainSideWalls
            .SelectMany(summary => summary.TextureIds)
            .Distinct()
            .Order()
            .ToArray();
        return textureIds.Length == 0
            ? ""
            : $" ({string.Join("/", textureIds.Select(textureId => $"tex {textureId}"))})";
    }

    private static string BuildSkippedTerrainPatchSummary(TerrainPatchPlan? plan)
    {
        if (plan == null || plan.SkippedEdits.Count == 0)
            return "";

        string examples = string.Join(" | ", plan.SkippedEdits.Take(2));
        string more = plan.SkippedEdits.Count > 2
            ? $" (+{plan.SkippedEdits.Count - 2} more)"
            : "";
        string sideWallCoverage = BuildTerrainSideWallCoverageSummary(plan);
        return $" Skipped terrain: {examples}{more}.{sideWallCoverage}";
    }

    private static string BuildTerrainSideWallCoverageSummary(TerrainPatchPlan plan)
    {
        if (plan.TerrainSideWalls.Count == 0)
            return "";

        int exposed = plan.TerrainSideWalls.Sum(summary => summary.ExposedEdgeCount);
        int solid = plan.TerrainSideWalls.Sum(summary => summary.EmittedFaceCount);
        if (exposed <= 0 || solid >= exposed)
            return "";

        return $" Terrain side walls: {solid}/{exposed} exposed edge(s) have solid sides.";
    }

    private async Task<TerrainPatchRiskDecision> ConfirmTerrainPatchRisksAsync()
    {
        TerrainEditSafetySummary summary = BuildTerrainEditSafetySummary();
        if (!summary.HasPatchRisk)
            return TerrainPatchRiskDecision.Create;

        Window dialog = new()
        {
            Title = "Terrain Patch Check",
            Width = 480,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainPatchRiskDialogContent(dialog, summary);
        return await dialog.ShowDialog<TerrainPatchRiskDecision>(this);
    }

    private static Control BuildTerrainPatchRiskDialogContent(Window dialog, TerrainEditSafetySummary summary)
    {
        List<string> risks = new();
        if (summary.StructuralEdits > 0)
            risks.Add($"{summary.StructuralEdits} structural face edit(s)");
        if (summary.VisualOnlyHeightEdits > 0)
            risks.Add($"{summary.VisualOnlyHeightEdits} visual-only geometry edit(s)");
        if (summary.PartialHeightEdits > 0)
            risks.Add($"{summary.PartialHeightEdits} partial collision geometry edit(s)");
        if (summary.UnknownHeightEdits > 0)
            risks.Add($"{summary.UnknownHeightEdits} geometry edit(s) with unknown collision backing");

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Review terrain geometry patching",
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43))
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"This level has {summary.LiveEdits} terrain edit(s). The editor found {summary.PlayableHeightEdits} fully playable geometry edit(s), but also {string.Join(", ", risks)}.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Height risks may appear in the editor or visual mesh while not fully changing where Spyro can stand in game. Remove-face edits neutralize the visible face and matched collision. Add-copy edits can add independent visible terrain vertices when a sector has source slack, and can add copied collision when the source face has matched collision plus a same-cell placeholder.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            LineHeight = 20
        });
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button review = NewButton("Review Edits");
        Button create = NewButton("Create Anyway");
        cancel.Click += (_, _) => dialog.Close(TerrainPatchRiskDecision.Cancel);
        review.Click += (_, _) => dialog.Close(TerrainPatchRiskDecision.Review);
        create.Click += (_, _) => dialog.Close(TerrainPatchRiskDecision.Create);
        buttons.Children.Add(cancel);
        buttons.Children.Add(review);
        buttons.Children.Add(create);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task<TerrainPatchResult?> TryCreateTerrainTestAsync(string sourceImage, string sourceCue, string outputDir)
    {
        return await TryPlanTerrainPatchAsync(
            sourceImage,
            sourceCue,
            outputDir,
            "Object and Terrain Edits",
            writeImage: true);
    }

    private async Task<TerrainPatchResult?> TryCreateTerrainTestAsync(
        LevelDefinition level,
        string sourceImage,
        string sourceCue,
        string outputDir,
        string outputName)
    {
        return await TryPlanTerrainPatchAsync(
            level,
            sourceImage,
            sourceCue,
            outputDir,
            outputName,
            writeImage: true);
    }

    private async Task PreflightTerrainPatchAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before preflighting terrain.";
            return;
        }

        if (_currentGeometry == null)
        {
            _statusText.Text = "No terrain is loaded yet.";
            return;
        }

        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before preflighting terrain.";
            return;
        }

        await PersistCurrentTerrainEditsAsync();
        string outputDir = EnsureUserOutputDirectory();
        _statusText.Text = $"Preflighting terrain patch plan for {_currentLevel.DisplayName}...";

        try
        {
            TerrainPatchResult? result = await TryPlanTerrainPatchAsync(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                outputDir,
                "Terrain Preflight",
                writeImage: false);
            if (result == null)
            {
                _statusText.Text = "No terrain edits or active custom terrain art are staged for preflight.";
                return;
            }

            TerrainPatchPlan plan = result.Plan;
            string included = BuildCreatedTerrainPatchSummary(plan);
            string skipped = BuildSkippedTerrainPatchSummary(plan);
            string wrote = Path.GetFileName(result.OutputPlanPath);
            string ready = plan.PatchCount > 0
                ? $"{plan.PatchCount} patch record(s) ready"
                : "0 patch records ready";
            _statusText.Text = $"Terrain preflight wrote {wrote}: {ready}.{included}{skipped} No BIN/CUE was created.";
            RefreshCurrentLevelDetails();
            await ShowTerrainPreflightResultAsync(result);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not preflight terrain: {ex.Message}";
        }
    }

    private async Task ShowTerrainPreflightResultAsync(TerrainPatchResult result)
    {
        TerrainPatchPlan plan = result.Plan;
        IReadOnlyList<TerrainPatchPlanReviewItem> items = BuildTerrainPatchPlanReviewItems(plan);
        ListBox list = new()
        {
            MinHeight = 300,
            ItemTemplate = new FuncDataTemplate<TerrainPatchPlanReviewItem>((item, _) => new TextBlock
            {
                Text = item?.ListText ?? "",
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 100
        };

        void RefreshDetails()
        {
            details.Text = list.SelectedItem is TerrainPatchPlanReviewItem item
                ? item.Details
                : "Select a preflight row to inspect the planned patch or skipped terrain reason.";
        }

        list.ItemsSource = items;
        list.SelectedIndex = items.Count == 0 ? -1 : 0;
        list.SelectionChanged += (_, _) => RefreshDetails();
        RefreshDetails();

        Window dialog = new()
        {
            Title = "Terrain BIN Preflight",
            Width = 840,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainPreflightDialogContent(dialog, result, list, details));
        await dialog.ShowDialog(this);
    }

    private IReadOnlyList<TerrainPatchPlanReviewItem> BuildTerrainPatchPlanReviewItems(TerrainPatchPlan plan)
    {
        List<TerrainPatchPlanReviewItem> items = new();
        items.AddRange(BuildTerrainPreflightOutcomeItems(plan));
        foreach (TerrainSideWallPatchSummary summary in plan.TerrainSideWalls)
        {
            string coverage = $"{summary.EmittedFaceCount}/{summary.ExposedEdgeCount} solid side-wall edge(s)";
            string textures = summary.TextureIds.Count == 0
                ? "texture inherited from source face"
                : $"texture {string.Join(", ", summary.TextureIds)}";
            string skipReasons = summary.SkipReasons.Count == 0
                ? "No skipped side-wall edges for this face."
                : string.Join("\n", summary.SkipReasons.Select(reason => $"- {reason}"));
            items.Add(new TerrainPatchPlanReviewItem
            {
                SortRank = summary.SkippedEdgeCount > 0 ? 1 : 0,
                Category = "Terrain side walls",
                RuntimeKey = summary.RuntimeKey,
                Summary = $"{coverage}, {textures}, collision triangles={summary.CollisionTriangleCount}",
                Details =
                    "Terrain side-wall coverage\n" +
                    $"Runtime key: {summary.RuntimeKey}\n" +
                    $"Sector: 0x{summary.SectorOffset:X}\n" +
                    $"Exposed edges: {summary.ExposedEdgeCount}\n" +
                    $"Solid side-wall faces: {summary.EmittedFaceCount}\n" +
                    $"Wall texture: {textures}\n" +
                    $"Collision triangles: {summary.CollisionTriangleCount}\n" +
                    $"Skipped edges: {summary.SkippedEdgeCount}\n\n" +
                    skipReasons
            });
        }

        foreach (TerrainPatch patch in plan.Patches)
        {
            string category = TerrainPatchCategoryLabel(patch.Kind);
            items.Add(new TerrainPatchPlanReviewItem
            {
                SortRank = 0,
                Category = category,
                RuntimeKey = patch.RuntimeKey,
                Summary = $"{patch.Kind}, {patch.ByteLength} byte(s)",
                Details =
                    $"{category}\n" +
                    $"Runtime key: {patch.RuntimeKey}\n" +
                    $"Patch kind: {patch.Kind}\n" +
                    $"WAD offset: {patch.WadRelativeOffset}\n" +
                    $"Image offset: {patch.ImageOffset}\n" +
                    $"Bytes: {patch.ByteLength}\n" +
                    $"Before: {patch.BeforeHexPreview}\n" +
                    $"After: {patch.AfterHexPreview}\n" +
                    patch.Description
            });
        }

        foreach (string skipped in plan.SkippedEdits)
        {
            items.Add(new TerrainPatchPlanReviewItem
            {
                SortRank = 1,
                Category = "Skipped",
                RuntimeKey = RuntimeKeyFromSkippedTerrainEdit(skipped),
                Summary = skipped,
                Details = $"Skipped terrain edit\n{skipped}\n\nThis edit is saved in the editor, but Create BIN cannot safely patch this part yet. Use the selected-face readiness text, Review Edits, or terrain proof/source-search tools to resolve it."
            });
        }

        if (items.Count == 0)
        {
            items.Add(new TerrainPatchPlanReviewItem
            {
                SortRank = 2,
                Category = "No patches",
                RuntimeKey = "",
                Summary = "No terrain patch records were ready.",
                Details = "The preflight ran, but no patch records were produced. Stage a terrain move, point edit, remove, add-copy, texture swap, or active custom terrain art first."
            });
        }

        return items
            .OrderBy(item => item.SortRank)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Summary, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<TerrainPatchPlanReviewItem> BuildTerrainPreflightOutcomeItems(TerrainPatchPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.TerrainEditsPath) || !File.Exists(plan.TerrainEditsPath))
            return Array.Empty<TerrainPatchPlanReviewItem>();

        try
        {
            using FileStream stream = File.OpenRead(plan.TerrainEditsPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
                return Array.Empty<TerrainPatchPlanReviewItem>();

            List<TerrainPatchPlanReviewItem> items = new();
            foreach (JsonElement edit in edits.EnumerateArray())
            {
                string runtimeKey = GetJsonString(edit, "runtimeKey");
                if (string.IsNullOrWhiteSpace(runtimeKey))
                    continue;

                List<TerrainPatch> patches = plan.Patches
                    .Where(patch => string.Equals(patch.RuntimeKey, runtimeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                List<string> skipped = plan.SkippedEdits
                    .Where(skip => string.Equals(RuntimeKeyFromSkippedTerrainEdit(skip), runtimeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                IReadOnlyList<TerrainSideWallPatchSummary> sideWalls = plan.TerrainSideWalls
                    .Where(summary => string.Equals(summary.RuntimeKey, runtimeKey, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                items.Add(new TerrainPatchPlanReviewItem
                {
                    SortRank = -1,
                    Category = "Face outcome",
                    RuntimeKey = runtimeKey,
                    Summary = BuildTerrainPreflightOutcomeSummary(edit, patches, skipped, sideWalls),
                    Details = BuildTerrainPreflightOutcomeDetails(edit, patches, skipped, sideWalls)
                });
            }

            return items;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine(ex);
            return Array.Empty<TerrainPatchPlanReviewItem>();
        }
    }

    private static string BuildTerrainPreflightOutcomeSummary(
        JsonElement edit,
        IReadOnlyList<TerrainPatch> patches,
        IReadOnlyList<string> skipped,
        IReadOnlyList<TerrainSideWallPatchSummary> sideWalls)
    {
        string mode = GetJsonString(edit, "structureEditMode");
        if (string.Equals(mode, "remove-face", StringComparison.OrdinalIgnoreCase))
        {
            bool visible = patches.Any(patch => patch.Kind.StartsWith("terrain-face-degenerate-", StringComparison.OrdinalIgnoreCase));
            bool collision = patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-degenerate", StringComparison.OrdinalIgnoreCase));
            if (visible && collision)
                return "remove ready: visible face and matched collision";
            if (visible)
                return "remove partial: visible face only";
            return skipped.Count > 0 ? "remove skipped: needs proof/source map" : "remove staged: no patch rows";
        }

        if (string.Equals(mode, "add-clone-face", StringComparison.OrdinalIgnoreCase))
        {
            bool independent = patches.Any(patch => string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase));
            bool collision = patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase));
            if (independent && collision)
                return "add ready: independent face and copied collision";
            if (independent)
                return "add partial: independent visible face only";
            return skipped.Count > 0 ? "add skipped: needs source slack/proof" : "add staged: no patch rows";
        }

        bool visual = patches.Any(patch => patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
        bool playable = patches.Any(patch => patch.Kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase));
        bool texture = patches.Any(patch => string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
        string sideWall = BuildTerrainPreflightSideWallSummary(edit, sideWalls);
        if (visual && playable && texture)
            return string.IsNullOrWhiteSpace(sideWall)
                ? "move and texture ready: visible mesh, collision, and palette swap"
                : $"move and texture ready: visible mesh, top collision, palette swap; {sideWall}";
        if (visual && playable)
            return string.IsNullOrWhiteSpace(sideWall)
                ? "move ready: visible mesh and collision"
                : $"move ready: visible mesh and top collision; {sideWall}";
        if (visual)
            return "move partial: visible mesh only";
        if (texture)
            return "texture swap ready";
        return skipped.Count > 0 ? "skipped: needs proof/source map" : "staged: no patch rows";
    }

    private static string BuildTerrainPreflightOutcomeDetails(
        JsonElement edit,
        IReadOnlyList<TerrainPatch> patches,
        IReadOnlyList<string> skipped,
        IReadOnlyList<TerrainSideWallPatchSummary> sideWalls)
    {
        string runtimeKey = GetJsonString(edit, "runtimeKey");
        string mode = GetJsonString(edit, "structureEditMode");
        if (string.IsNullOrWhiteSpace(mode))
            mode = "none";

        string changed = BuildTerrainPreflightChangedLine(edit);
        string patchLines = patches.Count == 0
            ? "Patch rows: none"
            : "Patch rows:\n" + string.Join("\n", patches
                .GroupBy(patch => patch.Kind, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"- {group.Key}: {group.Count()}"));
        string skipLines = skipped.Count == 0
            ? "Skipped rows: none"
            : "Skipped rows:\n" + string.Join("\n", skipped.Select(skip => $"- {skip}"));

        return
            $"Face-level terrain preflight\n" +
            $"Runtime key: {runtimeKey}\n" +
            $"Edit mode: {mode}\n" +
            $"Outcome: {BuildTerrainPreflightOutcomeSummary(edit, patches, skipped, sideWalls)}\n" +
            $"{changed}\n\n" +
            $"{BuildTerrainPreflightSideWallDetails(edit, sideWalls)}\n\n" +
            $"{patchLines}\n\n" +
            $"{skipLines}";
    }

    private static string BuildTerrainPreflightSideWallSummary(JsonElement edit, IReadOnlyList<TerrainSideWallPatchSummary> sideWalls)
    {
        if (!TerrainPreflightChangesShape(edit))
            return "";

        int exposed = sideWalls.Sum(summary => summary.ExposedEdgeCount);
        int solid = sideWalls.Sum(summary => summary.EmittedFaceCount);
        int collision = sideWalls.Sum(summary => summary.CollisionTriangleCount);
        int skipped = sideWalls.Sum(summary => summary.SkippedEdgeCount);
        if (exposed <= 0)
            return "vertical sides not proven for this face";
        if (solid >= exposed && collision >= solid * 2)
            return $"vertical sides ready ({solid}/{exposed} textured collision edges)";
        if (solid > 0)
            return $"vertical sides partial ({solid}/{exposed} edges, {skipped} skipped)";
        return $"vertical sides not ready (0/{exposed} edges)";
    }

    private static string BuildTerrainPreflightSideWallDetails(JsonElement edit, IReadOnlyList<TerrainSideWallPatchSummary> sideWalls)
    {
        if (!TerrainPreflightChangesShape(edit))
            return "Vertical sides: not needed for texture-only changes.";

        if (sideWalls.Count == 0)
            return "Vertical sides: no side-wall patch summary was produced for this face. The top terrain may patch, but exposed raised/lowered sides are not proven solid yet.";

        int exposed = sideWalls.Sum(summary => summary.ExposedEdgeCount);
        int solid = sideWalls.Sum(summary => summary.EmittedFaceCount);
        int collision = sideWalls.Sum(summary => summary.CollisionTriangleCount);
        int skipped = sideWalls.Sum(summary => summary.SkippedEdgeCount);
        string textures = string.Join(", ", sideWalls
            .SelectMany(summary => summary.TextureIds)
            .Distinct()
            .Order()
            .Select(textureId => $"texture {textureId}"));
        if (string.IsNullOrWhiteSpace(textures))
            textures = "source face texture";
        IEnumerable<string> reasons = sideWalls
            .SelectMany(summary => summary.SkipReasons)
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(reason => $"- {reason}");
        string reasonText = string.Join("\n", reasons);
        if (string.IsNullOrWhiteSpace(reasonText))
            reasonText = "- none";

        return
            "Vertical sides:\n" +
            $"- exposed edges: {exposed}\n" +
            $"- solid textured edges: {solid}\n" +
            $"- side-wall collision triangles: {collision}\n" +
            $"- skipped edges: {skipped}\n" +
            $"- wall texture: {textures}\n" +
            "Skip reasons:\n" +
            reasonText;
    }

    private static bool TerrainPreflightChangesShape(JsonElement edit)
    {
        string mode = GetJsonString(edit, "structureEditMode");
        if (string.Equals(mode, "remove-face", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "add-clone-face", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasTerrainPreflightZChange(edit) || HasTerrainPreflightXYChange(edit);
    }

    private static bool HasTerrainPreflightZChange(JsonElement edit)
    {
        IReadOnlyList<float> originalZ = ReadTerrainPreflightFloatArray(edit, "originalZ");
        IReadOnlyList<float> editedZ = ReadTerrainPreflightFloatArray(edit, "editedZ");
        int count = Math.Min(originalZ.Count, editedZ.Count);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(originalZ[i] - editedZ[i]) > 0.001f)
                return true;
        }

        return false;
    }

    private static bool HasTerrainPreflightXYChange(JsonElement edit)
    {
        IReadOnlyList<Vector2f> originalPoints = ReadTerrainPreflightVector2Array(edit, "originalPoints");
        IReadOnlyList<Vector2f> editedPoints = ReadTerrainPreflightVector2Array(edit, "editedPoints");
        int count = Math.Min(originalPoints.Count, editedPoints.Count);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(originalPoints[i].X - editedPoints[i].X) > 0.001f ||
                Math.Abs(originalPoints[i].Y - editedPoints[i].Y) > 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildTerrainPreflightChangedLine(JsonElement edit)
    {
        int textureOriginal = GetJsonInt32(edit, "textureIdOriginal", -1);
        int textureEdited = GetJsonInt32(edit, "textureIdEdited", -1);
        string texture = textureEdited >= 0
            ? textureOriginal >= 0
                ? $"texture {textureOriginal}->{textureEdited}"
                : $"texture ->{textureEdited}"
            : "texture unchanged";

        IReadOnlyList<float> originalZ = ReadTerrainPreflightFloatArray(edit, "originalZ");
        IReadOnlyList<float> editedZ = ReadTerrainPreflightFloatArray(edit, "editedZ");
        IReadOnlyList<Vector2f> originalPoints = ReadTerrainPreflightVector2Array(edit, "originalPoints");
        IReadOnlyList<Vector2f> editedPoints = ReadTerrainPreflightVector2Array(edit, "editedPoints");
        string z = SummarizeTerrainPreflightZChange(originalZ, editedZ);
        string xy = SummarizeTerrainPreflightXYChange(originalPoints, editedPoints);
        return $"Changed: {texture}; {z}; {xy}";
    }

    private static IReadOnlyList<float> ReadTerrainPreflightFloatArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<float>();

        List<float> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number))
                result.Add(number);
            else if (float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                result.Add(parsed);
        }

        return result;
    }

    private static IReadOnlyList<Vector2f> ReadTerrainPreflightVector2Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return Array.Empty<Vector2f>();

        List<Vector2f> result = new();
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                result.Add(new Vector2f(GetJsonSingle(value, "x", 0), GetJsonSingle(value, "y", 0)));
                continue;
            }

            if (value.ValueKind != JsonValueKind.Array)
                continue;

            float[] parts = value.EnumerateArray()
                .Select(part => part.ValueKind == JsonValueKind.Number && part.TryGetSingle(out float number)
                    ? number
                    : float.TryParse(part.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                        ? parsed
                        : 0)
                .Take(2)
                .ToArray();
            if (parts.Length == 2)
                result.Add(new Vector2f(parts[0], parts[1]));
        }

        return result;
    }

    private static string SummarizeTerrainPreflightZChange(IReadOnlyList<float> originalZ, IReadOnlyList<float> editedZ)
    {
        if (editedZ.Count == 0)
            return "Z unchanged";

        List<float> deltas = new();
        for (int i = 0; i < editedZ.Count; i++)
        {
            float original = i < originalZ.Count ? originalZ[i] : editedZ[i];
            float delta = editedZ[i] - original;
            if (Math.Abs(delta) > 0.001f)
                deltas.Add(delta);
        }

        if (deltas.Count == 0)
            return "Z unchanged";
        return deltas.Count == 1
            ? $"Z {deltas[0]:+0.###;-0.###;0}"
            : $"Z {deltas.Min():+0.###;-0.###;0} to {deltas.Max():+0.###;-0.###;0}";
    }

    private static string SummarizeTerrainPreflightXYChange(IReadOnlyList<Vector2f> originalPoints, IReadOnlyList<Vector2f> editedPoints)
    {
        if (editedPoints.Count == 0)
            return "XY unchanged";

        List<Vector2f> deltas = new();
        for (int i = 0; i < editedPoints.Count; i++)
        {
            Vector2f original = i < originalPoints.Count ? originalPoints[i] : editedPoints[i];
            Vector2f delta = new(editedPoints[i].X - original.X, editedPoints[i].Y - original.Y);
            if (Math.Abs(delta.X) > 0.001f || Math.Abs(delta.Y) > 0.001f)
                deltas.Add(delta);
        }

        if (deltas.Count == 0)
            return "XY unchanged";

        bool same = deltas.All(delta =>
            Math.Abs(delta.X - deltas[0].X) <= 0.001f &&
            Math.Abs(delta.Y - deltas[0].Y) <= 0.001f);
        return same
            ? $"XY {deltas[0].X:+0.###;-0.###;0}, {deltas[0].Y:+0.###;-0.###;0}"
            : $"XY {deltas.Count} point(s) changed";
    }

    private static string TerrainPatchCategoryLabel(string kind)
    {
        if (kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase))
            return "Collision";
        if (kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase))
            return "Visible mesh";
        if (kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase))
            return "Texture art";
        if (kind.StartsWith("terrain-face-degenerate-", StringComparison.OrdinalIgnoreCase))
            return "Remove face";
        if (kind.StartsWith("terrain-", StringComparison.OrdinalIgnoreCase))
            return "Terrain structure";
        if (string.Equals(kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase))
            return "Texture swap";
        return "Terrain patch";
    }

    private string RuntimeKeyFromSkippedTerrainEdit(string skipped)
    {
        if (string.IsNullOrWhiteSpace(skipped))
            return "";

        if (_currentGeometry != null)
        {
            foreach (TerrainPolygon polygon in _currentGeometry.Polygons
                .Where(polygon => !string.IsNullOrWhiteSpace(polygon.RuntimeKey))
                .OrderByDescending(polygon => polygon.RuntimeKey.Length))
            {
                string key = polygon.RuntimeKey;
                if (skipped.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase) ||
                    skipped.StartsWith(key + " ", StringComparison.OrdinalIgnoreCase) ||
                    skipped.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }
        }

        int marker = skipped.IndexOf(": ", StringComparison.Ordinal);
        return marker <= 0 ? "" : skipped[..marker];
    }

    private bool FocusTerrainPreflightItem(TerrainPatchPlanReviewItem item)
    {
        if (string.IsNullOrWhiteSpace(item.RuntimeKey))
        {
            _statusText.Text = "That preflight row is not tied to a single terrain face.";
            return false;
        }

        if (_currentGeometry == null)
        {
            _statusText.Text = "No terrain geometry is loaded right now.";
            return false;
        }

        int index = _currentGeometry.Polygons.FindIndex(polygon => string.Equals(polygon.RuntimeKey, item.RuntimeKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            _statusText.Text = $"Could not find terrain face {item.RuntimeKey} in the current level.";
            return false;
        }

        _viewport.FocusTerrain(index);
        _statusText.Text = $"Focused terrain face {item.RuntimeKey}: {item.Category}.";
        return true;
    }

    private Control BuildTerrainPreflightDialogContent(Window dialog, TerrainPatchResult result, ListBox list, TextBlock details)
    {
        TerrainPatchPlan plan = result.Plan;
        string summary =
            $"{plan.LevelName}: {plan.PatchCount} patch record(s), {plan.TotalPatchedBytes} byte(s), {plan.SkippedEdits.Count} skipped terrain edit(s).\n" +
            $"{BuildCreatedTerrainPatchSummary(plan).Trim()} {BuildSkippedTerrainPatchSummary(plan).Trim()}\n" +
            $"Plan file: {Path.GetFileName(result.OutputPlanPath)}\nNo BIN/CUE was created.";

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = summary.Trim(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });
        panel.Children.Add(list);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button openPlan = NewButton("Open Plan");
        Button focusFace = NewButton("Focus Face");
        Button close = NewButton("Close");
        openPlan.Click += (_, _) =>
        {
            try
            {
                OpenPath(result.OutputPlanPath);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                _statusText.Text = $"Could not open preflight plan: {ex.Message}";
            }
        };
        focusFace.Click += (_, _) =>
        {
            if (list.SelectedItem is not TerrainPatchPlanReviewItem item)
            {
                _statusText.Text = "Select a preflight row before focusing a face.";
                return;
            }

            if (FocusTerrainPreflightItem(item))
                dialog.Close();
        };
        close.Click += (_, _) => dialog.Close();
        buttons.Children.Add(openPlan);
        buttons.Children.Add(focusFace);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task<TerrainPatchResult?> TryPlanTerrainPatchAsync(
        string sourceImage,
        string sourceCue,
        string outputDir,
        string outputName,
        bool writeImage)
    {
        if (_currentLevel == null)
            return null;

        return await TryPlanTerrainPatchAsync(
            _currentLevel,
            sourceImage,
            sourceCue,
            outputDir,
            outputName,
            writeImage);
    }

    private async Task<TerrainPatchResult?> TryPlanTerrainPatchAsync(
        LevelDefinition level,
        string sourceImage,
        string sourceCue,
        string outputDir,
        string outputName,
        bool writeImage)
    {
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{level.Key}-terrain-edits.json");
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, level.Key);
        if (!File.Exists(terrainEditsPath) && !File.Exists(customTexturesPath))
            return null;

        TerrainGeometryLoadData geometryData = level == _currentLevel && _currentGeometry != null
            ? new TerrainGeometryLoadData(_currentGeometry, _loadedTerrainEdits, _terrainCacheHealthMessage)
            : LoadGeometryData(level.Key);
        GeometryCandidate? geometry = geometryData.Geometry;
        string activeCustomTexturesPath = await CreateActiveCustomTerrainTextureManifestAsync(level, geometry, customTexturesPath);
        if (!File.Exists(terrainEditsPath) && string.IsNullOrWhiteSpace(activeCustomTexturesPath))
            return null;

        string ramPath = TerrainPatchDataLocator.FindRamDump(_workspace, level.Key);
        string sourceSearchPath = TerrainPatchDataLocator.FindSourceSearch(_workspace, level.Key);
        if (geometry != null && File.Exists(sourceImage))
        {
            string searchDir = Path.Combine(_workspace.RootPath, "_local", "terrain");
            Directory.CreateDirectory(searchDir);
            string nativeSearchPath = Path.Combine(searchDir, $"{level.Key}-runtime-terrain-source-search-native.json");
            if (File.Exists(ramPath))
            {
                TerrainSourceSearchResult search = await TerrainSourceSearchBuilder.BuildAsync(new TerrainSourceSearchRequest(
                    SourceImagePath: sourceImage,
                    OutputPath: nativeSearchPath,
                    Level: level,
                    Geometry: geometry,
                    RamPath: ramPath));
                sourceSearchPath = search.OutputPath;
            }
            else if (IsSourceDerivedGeometry(geometry))
            {
                TerrainSourceSearchResult search = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(new SourceDerivedTerrainSourceSearchRequest(
                    SourceImagePath: sourceImage,
                    OutputPath: nativeSearchPath,
                    Level: level,
                    Geometry: geometry));
                sourceSearchPath = search.OutputPath;
            }
        }

        string outputPrefix = outputName.StartsWith("Spyro Editor - ", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(outputDir, outputName)
            : Path.Combine(outputDir, $"Spyro Editor - {level.DisplayName} - {outputName}");
        return await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
            SourceImagePath: sourceImage,
            SourceCuePath: sourceCue,
            OutputPrefix: outputPrefix,
            Level: level,
            RamPath: ramPath,
            SourceSearchPath: sourceSearchPath,
            TerrainEditsPath: terrainEditsPath,
            CustomTexturesPath: activeCustomTexturesPath,
            WriteImage: writeImage));
    }

    private static bool IsSourceDerivedGeometry(GeometryCandidate geometry)
    {
        return geometry.Name.Contains("source-wad", StringComparison.OrdinalIgnoreCase) ||
            geometry.Polygons.Any(polygon => polygon.SectorOffset >= 0x800000);
    }

    private async Task<string> CreateActiveCustomTerrainTextureManifestAsync(string customTexturesPath)
    {
        if (_currentLevel == null || string.IsNullOrWhiteSpace(customTexturesPath) || !File.Exists(customTexturesPath))
            return "";

        IReadOnlyList<CustomTerrainTextureImport>? currentImports = _customTerrainTextures.Count > 0
            ? _customTerrainTextures
            : null;
        return await CreateActiveCustomTerrainTextureManifestAsync(_currentLevel, _currentGeometry, customTexturesPath, currentImports);
    }

    private async Task<string> CreateActiveCustomTerrainTextureManifestAsync(
        LevelDefinition level,
        GeometryCandidate? geometry,
        string customTexturesPath,
        IReadOnlyList<CustomTerrainTextureImport>? importOverride = null)
    {
        if (string.IsNullOrWhiteSpace(customTexturesPath) || !File.Exists(customTexturesPath))
            return "";

        IReadOnlyList<CustomTerrainTextureImport> imports = importOverride ?? CustomTerrainTextureStore.LoadManifest(customTexturesPath);
        if (imports.Count == 0)
            return "";

        if (geometry == null)
            return customTexturesPath;

        HashSet<int> liveTextureIds = geometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
            .Select(face => face.TextureId)
            .ToHashSet();
        CustomTerrainTextureImport[] activeImports = imports
            .Where(import => liveTextureIds.Contains(import.TextureId))
            .ToArray();
        if (activeImports.Length == 0)
            return "";
        if (activeImports.Length == imports.Count)
            return customTexturesPath;

        string manifestDirectory = Path.GetDirectoryName(customTexturesPath) ?? _workspace.RootPath;
        activeImports = activeImports
            .Select(import => import with
            {
                SourceImagePath = Path.IsPathRooted(import.SourceImagePath)
                    ? import.SourceImagePath
                    : Path.GetFullPath(Path.Combine(manifestDirectory, import.SourceImagePath))
            })
            .ToArray();
        string filteredDir = Path.Combine(_workspace.RootPath, "_local", "terrain");
        Directory.CreateDirectory(filteredDir);
        string filteredPath = Path.Combine(filteredDir, $"{level.Key}-active-custom-terrain-textures.json");
        await CustomTerrainTextureStore.SaveManifestAsync(filteredPath, level.Key, level.DisplayName, activeImports);
        return filteredPath;
    }

    private void ShowSelection(ViewportSelectionChangedEventArgs selection)
    {
        if (selection.Moby != null)
        {
            Moby moby = selection.Moby;
            _selectedMoby = moby;
            _selectedTerrain = null;
            _selectedTerrainIndex = -1;
            _selectedTerrainPointIndex = -1;
            _activeTerrainProofTarget = null;
            SelectMobyInList(moby);
            RefreshLinkedMobyList(moby);
            RefreshTerrainSurfaceQuickState();
        _selectionTitle.Text = moby.DisplayLabel;
            _selectionDetails.Text =
                $"True index: {moby.TrueIndex}\n" +
                $"Category: {MobyListBadge(moby)}\n" +
                $"Identity: {BuildMobyIdentityStatus(moby)}\n" +
                $"Type/state: 0x{moby.Type:X2} / 0x{moby.State:X2}\n" +
                $"XYZ: {moby.Position.X:0.0}, {moby.Position.Y:0.0}, {moby.Position.Z:0.0}\n" +
                BuildMobyMetadataDetails(moby) +
                $"Technical: {moby.TechnicalSummary}\n" +
                $"Edited: {(moby.HasAnyEdit ? "yes" : "no")}\n" +
                $"Patch: {moby.PatchStatus}" +
                (moby.HasLoadedNativeEdit ? $"\nLoaded edit: {moby.LoadedNativeEditSummary}" : "");
            _identityObservationHint.Text = $"{BuildIdentityFamilySummary(moby)}\nFingerprint: {IdentityFingerprint(moby)}";
            RefreshIdentityObservationSuggestions(moby);
            RefreshObjectReadinessHint();
            RefreshActionAvailability();
            _statusText.Text = $"Selected moby {moby.DisplayLabel} at true index {moby.TrueIndex}.";
            return;
        }

        if (selection.Terrain != null)
        {
            TerrainPolygon terrain = selection.Terrain;
            _selectedMoby = null;
            _selectedTerrain = terrain;
            _selectedTerrainIndex = selection.TerrainIndex;
            if (selection.TerrainPointIndex >= 0)
                _selectedTerrainPointIndex = selection.TerrainPointIndex;
            _selectedTerrainPointIndex = NormalizeTerrainPointIndex(terrain, _selectedTerrainPointIndex);
            TerrainProofTargetSelection? activeProofTarget = ActiveTerrainProofTargetForSelection();
            if (_activeTerrainProofTarget != null && activeProofTarget == null)
                _activeTerrainProofTarget = null;
            SelectMobyInList(null);
            RefreshLinkedMobyList(null);
            RefreshTerrainSurfaceQuickState();
            RefreshIdentityObservationSuggestions(null);
            _selectionTitle.Text = $"Terrain face {selection.TerrainIndex}";
            CustomTerrainTextureImport? customTexture = _customTerrainTextures.FirstOrDefault(texture => texture.TextureId == terrain.TextureId);
            _selectionDetails.Text =
                $"Runtime key: {terrain.RuntimeKey}\n" +
                $"Texture ID: {terrain.TextureId}\n" +
                $"Surface: {TerrainMaterialClassifier.FormatSurface(terrain.Surface)} ({terrain.SurfaceSource})\n" +
                $"Behavior: {TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior)} ({terrain.BehaviorConfidence})\n" +
                $"Proof status: {BuildSelectedTerrainProofStatus(terrain)}\n" +
                $"Surface edit status: {BuildTerrainSurfaceEditStatus(terrain)}\n" +
                (string.IsNullOrWhiteSpace(terrain.BehaviorNote) ? "" : $"Proof note: {terrain.BehaviorNote}\n") +
                $"Face color: #{terrain.FaceColor.R:X2}{terrain.FaceColor.G:X2}{terrain.FaceColor.B:X2}\n" +
                $"Face words: {DisplayRawWord(terrain.Word3)} / {DisplayRawWord(terrain.Word4)}\n" +
                $"Average Z: {terrain.AvgZ:0.0}\n" +
                $"Z range: {terrain.MinZ:0.0} to {terrain.MaxZ:0.0}\n" +
                $"Selected point: {BuildSelectedTerrainPointSummary()}\n" +
                $"Geometry edit: {FormatTerrainGeometryEdit(terrain)}\n" +
                $"Test BIN patch: {FormatTerrainPatchReadiness(terrain)}\n" +
                $"Playable collision: {FormatTerrainCollisionReadiness(terrain)}" +
                (activeProofTarget == null
                    ? ""
                    : $"\nActive proof target: {activeProofTarget.Target.CaptureKind} ({SelectedProofTargetRole(activeProofTarget)})") +
                (customTexture == null
                    ? ""
                    : $"\nCustom import: {BuildCustomTerrainTextureSummary(customTexture)}");
            _identityObservationHint.Text = "";
            RefreshObjectReadinessHint();
            RefreshActionAvailability();
            _statusText.Text = activeProofTarget == null
                ? $"Selected terrain face {selection.TerrainIndex}."
                : $"Selected terrain face {selection.TerrainIndex} for {activeProofTarget.Target.CaptureKind} {SelectedProofTargetRole(activeProofTarget)} capture.";
            return;
        }

        _selectedMoby = null;
        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _selectedTerrainPointIndex = -1;
        _activeTerrainProofTarget = null;
        SelectMobyInList(null);
        RefreshLinkedMobyList(null);
        RefreshTerrainSurfaceQuickState();
        RefreshIdentityObservationSuggestions(null);
        _selectionTitle.Text = "Nothing selected";
        _selectionDetails.Text = "Click a terrain face or object in the viewport. Use Map View for editing, or Fly 3D to look around the level.";
        _identityObservationHint.Text = "";
        RefreshObjectReadinessHint();
        RefreshActionAvailability();
    }

    private Control BuildEditControls()
    {
        if (_releaseMode)
            return BuildPrimaryObjectControls();

        TabControl tabs = new()
        {
            MinHeight = 320,
            Items =
            {
                new TabItem
                {
                    Header = EditorTabHeader("Objects"),
                    Content = BuildPrimaryObjectControls()
                },
                new TabItem
                {
                    Header = EditorTabHeader("Terrain (beta)"),
                    Content = BuildTerrainTabControls()
                }
            }
        };
        tabs.SelectionChanged += (_, _) => _viewport.TerrainFocusMode = tabs.SelectedIndex == 1;
        _viewport.TerrainFocusMode = tabs.SelectedIndex == 1;
        return tabs;
    }

    private static TextBlock EditorTabHeader(string text, bool enabled = true)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(enabled ? Color.FromRgb(31, 38, 45) : Color.FromRgb(128, 137, 147)),
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Margin = new Thickness(2, 0)
        };
    }

    private Control BuildPrimaryObjectControls()
    {
        StackPanel panel = new() { Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        WrapPanel objectActions = new() { Orientation = Orientation.Horizontal };
        _objectAddButton = NewAsyncButton("Add Object", async () => await AddMobyNearSelectionAsync());
        _objectRemoveButton = NewButton("Remove Object", RemoveSelectedMoby);
        _objectEditButton = NewAsyncButton("Edit Object", async () =>
        {
            if (_selectedMoby != null)
                await EditMobyAsync(_selectedMoby);
        });
        _objectCopyButton = NewButton("Copy Object", CopySelectedMoby);
        _objectPasteButton = NewButton("Paste Object", PasteMobyClipboardAtLastPointer);
        _objectUndoButton = NewButton("Undo Object", () =>
        {
            if (_selectedMoby != null)
                UndoMoby(_selectedMoby);
        });
        _objectUndoRemoveButton = NewButton("Undo Remove", UndoLastRemovedMoby);
        _objectRestoreLevelButton = NewAsyncButton("Restore Level", async () => await RestoreCurrentLevelAsync());
        StyleObjectEditButton(_objectAddButton);
        StyleObjectEditButton(_objectRemoveButton);
        StyleObjectEditButton(_objectEditButton);
        StyleObjectEditButton(_objectCopyButton);
        StyleObjectEditButton(_objectPasteButton);
        StyleObjectEditButton(_objectUndoButton);
        StyleObjectEditButton(_objectUndoRemoveButton);
        StyleRestoreLevelButton(_objectRestoreLevelButton);

        objectActions.Children.Add(_objectAddButton);
        objectActions.Children.Add(_objectRemoveButton);
        objectActions.Children.Add(_objectEditButton);
        objectActions.Children.Add(_objectCopyButton);
        objectActions.Children.Add(_objectPasteButton);
        objectActions.Children.Add(_objectUndoButton);
        objectActions.Children.Add(_objectUndoRemoveButton);
        objectActions.Children.Add(_objectRestoreLevelButton);

        _objectActionHint.TextWrapping = TextWrapping.Wrap;
        _objectActionHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _objectActionHint.FontSize = 12;
        _objectActionHint.LineHeight = 18;
        panel.Children.Add(objectActions);
        panel.Children.Add(_objectActionHint);
        RefreshActionAvailability();
        return panel;
    }

    private Control BuildTerrainTabControls()
    {
        StackPanel outer = new() { Spacing = 9, Margin = new Thickness(0, 10, 0, 0) };
        outer.Children.Add(BuildTerrainTaskControls());
        outer.Children.Add(BuildTerrainBrushControls());
        outer.Children.Add(BuildCollapsedPanel("Material and paint", BuildTerrainMaterialControls()));
        outer.Children.Add(BuildTerrainPointControls());
        _terrainActionHint.TextWrapping = TextWrapping.Wrap;
        _terrainActionHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainActionHint.FontSize = 12;
        _terrainActionHint.LineHeight = 18;
        outer.Children.Add(_terrainActionHint);
        outer.Children.Add(BuildAdvancedTerrainControls());
        RefreshActionAvailability();
        return outer;
    }

    private Control BuildTerrainReadinessPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 246, 239)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(196, 218, 187)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 2, 0, 4)
        };

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "Terrain Status",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 89, 50))
        });
        _terrainReadinessHint.TextWrapping = TextWrapping.Wrap;
        _terrainReadinessHint.Foreground = new SolidColorBrush(Color.FromRgb(57, 73, 59));
        _terrainReadinessHint.FontSize = 12;
        _terrainReadinessHint.LineHeight = 17;
        panel.Children.Add(_terrainReadinessHint);

        WrapPanel buttons = new() { Orientation = Orientation.Horizontal };
        buttons.Children.Add(NewAsyncButton("Surface Groups", async () => await ShowTerrainSurfaceGroupsAsync()));
        buttons.Children.Add(NewAsyncButton("Proof Targets", async () => await ShowTerrainProofTargetsAsync()));
        buttons.Children.Add(NewAsyncButton("Capabilities", async () => await ShowTerrainCapabilitiesAsync()));
        panel.Children.Add(buttons);

        border.Child = panel;
        RefreshTerrainReadinessHint();
        return border;
    }

    private Control BuildTerrainTaskControls()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(247, 250, 253)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 214, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Terrain Tasks",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });

        Grid taskGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        _terrainTaskMoveFaceButton = NewAsyncButton("Move Face", MoveSelectedTerrainFaceAsync);
        _terrainTaskSinglePointButton = NewAsyncButton("Edit Point", EditSelectedTerrainPointAsync);
        _terrainTaskPaintFaceButton = NewAsyncButton("Paint Face", PaintSelectedTerrainFaceAsync);
        _terrainTaskAddCopyButton = NewAsyncButton("Add Terrain", StageAddCloneSelectedTerrainAsync);
        _terrainTaskRemoveFaceButton = NewAsyncButton("Remove Terrain", StageRemoveSelectedTerrainAsync);
        _terrainUndoButton = NewButton("Undo Terrain", UndoSelectedTerrain);
        _terrainTaskSaveButton = NewAsyncButton("Save Terrain", SaveCurrentTerrainEditsAsync);

        taskGrid.Children.Add(_terrainTaskMoveFaceButton);
        AddGridButton(taskGrid, _terrainTaskSinglePointButton, 1, 0);
        AddGridButton(taskGrid, _terrainTaskPaintFaceButton, 0, 1);
        AddGridButton(taskGrid, _terrainTaskAddCopyButton, 1, 1);
        AddGridButton(taskGrid, _terrainTaskRemoveFaceButton, 0, 2);
        AddGridButton(taskGrid, _terrainUndoButton, 1, 2);
        AddGridButton(taskGrid, _terrainTaskSaveButton, 0, 3);

        panel.Children.Add(taskGrid);
        _terrainTaskHint.TextWrapping = TextWrapping.Wrap;
        _terrainTaskHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainTaskHint.FontSize = 12;
        _terrainTaskHint.LineHeight = 18;
        panel.Children.Add(_terrainTaskHint);
        border.Child = panel;
        return border;
    }

    private async Task ShowTerrainCapabilitiesAsync()
    {
        IReadOnlyList<TerrainCapabilityReviewItem> items = BuildTerrainCapabilityReviewItems();
        ListBox list = new()
        {
            MinHeight = 320,
            ItemTemplate = new FuncDataTemplate<TerrainCapabilityReviewItem>((item, _) => new TextBlock
            {
                Text = item?.ListText ?? "",
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 130
        };

        void RefreshDetails()
        {
            details.Text = list.SelectedItem is TerrainCapabilityReviewItem item
                ? item.Details
                : "Select a capability row to see what is currently proven for this level.";
        }

        list.ItemsSource = items;
        list.SelectedIndex = items.Count == 0 ? -1 : 0;
        list.SelectionChanged += (_, _) => RefreshDetails();
        RefreshDetails();

        Window dialog = new()
        {
            Title = "Terrain Editing Capabilities",
            Width = 880,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainCapabilitiesDialogContent(dialog, list, details));
        await dialog.ShowDialog(this);
    }

    private IReadOnlyList<TerrainCapabilityReviewItem> BuildTerrainCapabilityReviewItems()
    {
        List<TerrainCapabilityReviewItem> items = new();
        string levelName = _currentLevel?.DisplayName ?? "No level";
        TerrainGeometryReadiness? geometry = LoadCurrentTerrainGeometryReadiness();
        TerrainTextureReadiness? texture = LoadCurrentTerrainTextureReadiness();
        TerrainStructureReadiness? structure = LoadCurrentTerrainStructureReadiness();

        string selection = _selectedTerrain == null
            ? "No terrain face is selected."
            : $"Selected face {_selectedTerrainIndex} ({_selectedTerrain.RuntimeKey}), texture {_selectedTerrain.TextureId}, {TerrainMaterialClassifier.FormatSurface(_selectedTerrain.Surface)}.";
        string geometryStatus = geometry?.Status ?? "not generated";
        string textureStatus = texture?.Status ?? "not generated";
        string structureStatus = structure?.Status ?? "not generated";

        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 0,
            Feature = "Move face / points",
            Status = geometryStatus,
            Evidence = geometry == null ? "No cross-level geometry readiness report found." : FormatTerrainGeometryReadiness(geometry),
            Details =
                $"{levelName} movement and single-point editing\n" +
                $"{selection}\n\n" +
                "What this covers:\n" +
                "- Raise/lower whole faces.\n" +
                "- Move whole faces in X/Y/Z.\n" +
                "- Edit one selected terrain point in X/Y/Z.\n\n" +
                (geometry == null
                    ? "No generated readiness row is available yet. Use Preflight BIN after staging a move/point edit, or rerun the terrain smoke/readiness pass."
                    : $"Readiness: {FormatTerrainGeometryReadiness(geometry)}\nSample edit: {geometry.Edit}\nSource sectors: {geometry.MatchedSectorCount}/{geometry.SourceSectorCount}\nPatches: {geometry.PatchCount} total, {geometry.VisualPatchCount} visual.\nNotes: {geometry.Notes}")
        });

        string removeEvidence = structure == null
            ? "No structure readiness report found."
            : structure.RemovePatchCount > 0
                ? $"{structure.RemovePatchCount} remove patch(es); collision patch={FormatYesNo(structure.RemoveHasCollisionPatch)}"
                : "Remove-face is not proven in the structure report.";
        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 1,
            Feature = "Remove terrain",
            Status = structureStatus,
            Evidence = removeEvidence,
            Details =
                $"{levelName} remove-face editing\n" +
                $"{selection}\n\n" +
                "What this covers:\n" +
                "- Stage a selected face for removal.\n" +
                "- Create BIN neutralizes the visible face record and matched collision triangles together.\n" +
                "- Visual-only deletion stays blocked in the normal editor because gameplay collision would remain.\n\n" +
                (_selectedTerrain == null ? "" : $"Selected face: {BuildTerrainRemoveFaceReadinessText(_selectedTerrain)}\n") +
                (structure == null
                    ? "No generated structure readiness row is available yet."
                    : $"Readiness: {FormatTerrainStructureReadiness(structure)}\nRemove sample: {BlankOr(structure.RemoveRuntimeKey, "none")}\nNotes: {structure.Notes}")
        });

        string addEvidence = structure == null
            ? "No structure readiness report found."
            : string.IsNullOrWhiteSpace(structure.AddRuntimeKey)
                ? "No add-copy source proved safe for this level."
                : $"{FormatTerrainStructureAddMode(structure)} on sample {structure.AddRuntimeKey}{FormatTerrainStructureSlack(structure)}";
        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 2,
            Feature = "Add copied terrain",
            Status = structureStatus,
            Evidence = addEvidence,
            Details =
                $"{levelName} add-copy terrain editing\n" +
                $"{selection}\n\n" +
                "What this covers:\n" +
                "- Copy a proven source face and place the copy with X/Y/Z offsets.\n" +
                "- Patch independent visible vertices when the source sector has enough safe room.\n" +
                "- Copy playable collision when a matching collision placeholder is available.\n\n" +
                (_selectedTerrain == null ? "Select a terrain face or use Find Best Add Face to inspect a source.\n" : $"{BuildTerrainAddCopyFaceReadinessText(_selectedTerrain)}\n") +
                (structure == null
                    ? "No generated structure readiness row is available yet."
                    : $"Readiness: {FormatTerrainStructureReadiness(structure)}\nAdd sample: {BlankOr(structure.AddRuntimeKey, "none")}\nNotes: {structure.Notes}")
        });

        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 3,
            Feature = "Borrow in-game look",
            Status = textureStatus,
            Evidence = texture == null ? "No texture readiness report found." : FormatTerrainTextureReadiness(texture),
            Details =
                $"{levelName} in-game texture/palette swaps\n" +
                $"{selection}\n\n" +
                "What this covers:\n" +
                "- Borrow another terrain texture ID from the current level.\n" +
                "- Keep the edit face-local by changing only the selected face's texture ID.\n" +
                "- Use existing in-game art without custom texture imports.\n\n" +
                (texture == null
                    ? "No generated texture readiness row is available yet."
                    : $"Readiness: {FormatTerrainTextureReadiness(texture)}\nSample: {texture.RuntimeKey}, texture {texture.SourceTextureId}->{texture.TargetTextureId}\nTexture swap patches: {texture.TextureSwapPatchCount}\nNotes: {texture.Notes}")
        });

        string customEvidence = _customTerrainTextures.Count == 0
            ? "No custom/imported terrain art is currently staged."
            : BuildCustomTerrainArtStageSummary();
        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 4,
            Feature = "Custom/import palette",
            Status = textureStatus,
            Evidence = customEvidence,
            Details =
                $"{levelName} custom palette and imported terrain art\n" +
                $"{selection}\n\n" +
                "What this covers:\n" +
                "- Color one selected face by giving it a local texture slot.\n" +
                "- Paste or import a palette from text/PNG/palette files.\n" +
                "- Color or import art for a shared texture when you intentionally want all faces using that texture to change.\n\n" +
                (texture == null
                    ? "No generated texture readiness row is available yet."
                    : $"Readiness: {FormatTerrainTextureReadiness(texture)}\nCustom texture imports in sample: {texture.CustomTextureImportCount}\nCustom texture byte patches: {texture.CustomTexturePatchCount}\nPreviewed faces: {texture.PreviewedFaceCount}\nDescriptor tiers: {BlankOr(texture.DescriptorTiers, "unknown")}\n") +
                $"Current staged art: {customEvidence}"
        });

        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 5,
            Feature = "Behavior proof",
            Status = "beta proof",
            Evidence = BuildTerrainProofReadinessSummary(),
            Details =
                $"{levelName} terrain behavior proof\n" +
                "Visual material colors are separate from gameplay behavior. Hazard-looking surfaces still need proof targets before we treat water/lava/goo as truly behaving that way in game.\n\n" +
                $"{BuildCurrentLevelTerrainProofReadiness()}\n" +
                $"{BuildTerrainBehaviorReadinessBreakdown()}\n" +
                $"{BuildTerrainProofQueueHintTextOnly()}"
        });

        items.Add(new TerrainCapabilityReviewItem
        {
            SortRank = 6,
            Feature = "Selected-face preflight",
            Status = _selectedTerrain == null ? "select face" : "available",
            Evidence = _selectedTerrain == null ? "Select a face, then use Preflight BIN." : BuildSelectedTerrainExportImpactSummary(_selectedTerrain),
            Details = _selectedTerrain == null
                ? $"{levelName}\nSelect a terrain face to see the exact Create BIN preflight for visible mesh, playable collision, palette/art, and add/remove structure."
                : $"{levelName}\n{selection}\n\n{BuildSelectedTerrainCreateBinPreflight(_selectedTerrain)}\n\nWill export: {BuildSelectedTerrainExportImpactSummary(_selectedTerrain)}\n{BuildSelectedTerrainLevelProofReadiness(_selectedTerrain)}"
        });

        return items
            .OrderBy(item => item.SortRank)
            .ThenBy(item => item.Feature, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private Control BuildTerrainCapabilitiesDialogContent(Window dialog, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = _currentLevel == null
                ? "Open a level to see terrain edit capability details."
                : $"{_currentLevel.DisplayName} terrain edit capability and patch-readiness summary",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        panel.Children.Add(NewSmallNote("This view explains what the editor can currently save and what Create BIN can patch. It does not replace in-game beta testing, especially for collision feel and hazard behavior."));
        panel.Children.Add(NewSmallNote(BuildTerrainReadinessCoverageSummary(compact: false)));
        panel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button openReports = NewButton("Open Reports");
        Button close = NewButton("Close");
        openReports.Click += (_, _) =>
        {
            string reportDir = Path.Combine(_workspace.RootPath, "_local", "smoke");
            if (!Directory.Exists(reportDir))
            {
                _statusText.Text = "No terrain readiness reports folder exists yet. Run the terrain smoke/readiness pass first.";
                return;
            }

            try
            {
                OpenPath(reportDir);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                _statusText.Text = $"Could not open terrain reports folder: {ex.Message}";
            }
        };
        close.Click += (_, _) => dialog.Close();
        buttons.Children.Add(openReports);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        return panel;
    }

    private string BuildTerrainProofQueueHintTextOnly()
    {
        return BuildCurrentLevelTerrainProofQueueHint();
    }

    private static string FormatYesNo(bool value) => value ? "yes" : "no";

    private static string BlankOr(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private Control BuildTerrainExportReadinessPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 4)
        };

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "Selected Terrain Export",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(38, 48, 59))
        });

        _terrainExportReadinessText.TextWrapping = TextWrapping.Wrap;
        _terrainExportReadinessText.Foreground = new SolidColorBrush(Color.FromRgb(58, 67, 77));
        _terrainExportReadinessText.FontSize = 12;
        _terrainExportReadinessText.LineHeight = 17;
        panel.Children.Add(_terrainExportReadinessText);

        border.Child = panel;
        RefreshTerrainExportReadiness();
        return border;
    }

    private void RefreshTerrainExportReadiness()
    {
        _terrainExportReadinessText.Text = BuildSelectedTerrainExportReadiness();
    }

    private string BuildSelectedTerrainExportReadiness()
    {
        if (_currentLevel == null)
            return "Open a level to see terrain export readiness.";

        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return "Terrain is not loaded yet.";

        if (_selectedTerrain == null)
            return $"{BuildTerrainExportReviewSummary()}\nSelect a terrain face to see face-specific export readiness.";

        TerrainPolygon terrain = _selectedTerrain;
        string textureArt = BuildTerrainTextureExportForReview(terrain.TextureId);
        string material = TerrainMaterialClassifier.FormatSurface(terrain.Surface);
        string behaviorProof = BuildTerrainMaterialProofHint(terrain.Surface, terrain.TextureId);
        string behaviorReadiness = BuildSelectedTerrainBehaviorReadiness(terrain);
        string levelProof = BuildSelectedTerrainLevelProofReadiness(terrain);
        string selectedSummary =
            $"Face {_selectedTerrainIndex}: texture {terrain.TextureId}, {material}.\n" +
            $"{BuildSelectedTerrainCreateBinPreflight(terrain)}\n" +
            $"Will export: {BuildSelectedTerrainExportImpactSummary(terrain)}\n" +
            $"Height export: {FormatTerrainPatchReadiness(terrain)}.\n" +
            $"Level proof: {levelProof}\n" +
            $"Playable collision: {FormatTerrainCollisionReadiness(terrain)}.\n" +
            $"Texture art: {textureArt}.\n" +
            $"Behavior: {behaviorReadiness} {behaviorProof}";

        return $"{selectedSummary}\n{BuildTerrainExportReviewSummary()}";
    }

    private string BuildSelectedTerrainCreateBinPreflight(TerrainPolygon terrain)
    {
        List<string> lines = ["Create BIN preflight:"];
        lines.Add($"- visible mesh: {BuildSelectedTerrainVisualMeshPreflight(terrain)}");
        lines.Add($"- playable collision: {BuildSelectedTerrainCollisionPreflight(terrain)}");
        lines.Add($"- palette/art: {BuildSelectedTerrainTexturePreflight(terrain)}");
        lines.Add($"- add/remove: {BuildSelectedTerrainStructurePreflight(terrain)}");
        return string.Join("\n", lines);
    }

    private string BuildSelectedTerrainVisualMeshPreflight(TerrainPolygon terrain)
    {
        if (terrain.IsTerrainRemoved)
            return "will be flattened/neutralized in the patched level";

        if (terrain.IsTerrainAddClone)
        {
            TerrainAddCopyFaceReadiness readiness = GetTerrainAddCopyFaceReadiness(terrain);
            return readiness.CanAddIndependentVisibleTerrain
                ? $"will add an independent copied face using {readiness.AvailableSlackBytes}/{readiness.RequiredSlackBytes} bytes of sector space"
                : "added copy is staged, but this face is not proven to fit as independent visible terrain yet";
        }

        if (terrain.HasHeightEdit || terrain.HasPositionEdit)
            return terrain.HasPositionEdit
                ? "will move edited vertices in X/Y/Z where source face records are mapped"
                : "will move edited vertex heights where source face records are mapped";

        if (terrain.HasTextureEdit)
            return "shape is unchanged; only the face texture ID changes";

        return "no selected-face geometry edit is staged yet";
    }

    private string BuildSelectedTerrainCollisionPreflight(TerrainPolygon terrain)
    {
        if (_terrainCollisionTriangleKeys.Count == 0)
            return "not matched for this level yet, so only visible mesh changes are proven";

        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        if (terrain.IsTerrainAddClone)
        {
            TerrainAddCopyFaceReadiness readiness = GetTerrainAddCopyFaceReadiness(terrain);
            return readiness.HasCollisionMatch
                ? $"source collision is matched ({readiness.MatchedCollisionTriangles}/{readiness.TotalCollisionTriangles}); copied collision still needs same-cell space in the patch plan"
                : "source collision is not matched, so the copied face may be visible-only";
        }

        if (coverage.MatchedTriangles == 0)
            return "no matching collision triangles; Spyro may still use the old playable ground";

        if (coverage.MatchedTriangles >= coverage.TotalTriangles)
            return terrain.IsTerrainRemoved
                ? "matched collision will be neutralized with the removed face"
                : "matched collision should move with the edited face";

        return $"partially matched ({coverage.MatchedTriangles}/{coverage.TotalTriangles}); only matched triangles are proven";
    }

    private string BuildSelectedTerrainTexturePreflight(TerrainPolygon terrain)
    {
        List<CustomTerrainTextureImport> imports = _customTerrainTextures
            .Where(texture => texture.TextureId == terrain.TextureId)
            .ToList();
        if (imports.Count > 0)
        {
            string names = string.Join(", ", imports
                .Select(texture => string.IsNullOrWhiteSpace(texture.PaletteName) ? texture.SourceImageName : texture.PaletteName)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2));
            string extra = imports.Count > 2 ? ", ..." : "";
            string label = string.IsNullOrWhiteSpace(names) ? "custom texture import" : names + extra;
            return $"{imports.Count} custom normal/close texture-page import(s) staged for texture {terrain.TextureId}: {label}. {BuildSelectedTerrainTextureScopeSummary(terrain)}";
        }

        if (terrain.HasTextureEdit)
            return $"will borrow in-game texture/palette {terrain.TextureId} instead of {terrain.OriginalTextureId}. {BuildSelectedTerrainTextureScopeSummary(terrain)}";

        return $"uses the current in-game texture; material labels alone do not patch texture pages. {BuildSelectedTerrainTextureScopeSummary(terrain)}";
    }

    private string BuildSelectedTerrainStructurePreflight(TerrainPolygon terrain)
    {
        if (terrain.IsTerrainRemoved)
            return $"remove-face edit is staged; {BuildTerrainRemoveFaceReadinessText(terrain)}";
        if (terrain.IsTerrainAddClone)
            return GetTerrainAddCopyFaceReadiness(terrain).CanAddIndependentVisibleTerrain
                ? "add-copy edit is staged and has a mapped visible terrain path"
                : "add-copy edit is staged but needs a better source face or source map proof";
        return "no structural add/remove edit on this face";
    }

    private string BuildSelectedTerrainLevelProofReadiness(TerrainPolygon terrain)
    {
        TerrainGeometryReadiness? geometry = LoadCurrentTerrainGeometryReadiness();
        TerrainTextureReadiness? texture = LoadCurrentTerrainTextureReadiness();
        TerrainStructureReadiness? structure = LoadCurrentTerrainStructureReadiness();
        return string.Join("; ",
        [
            $"movement/point {FormatSelectedGeometryReadiness(geometry, terrain)}",
            $"palette/texture {FormatSelectedTextureReadiness(texture, terrain)}",
            $"add/remove {FormatSelectedStructureReadiness(structure, terrain)}"
        ]);
    }

    private static string FormatSelectedGeometryReadiness(TerrainGeometryReadiness? readiness, TerrainPolygon terrain)
    {
        if (readiness == null)
            return "not generated";
        string sample = string.Equals(readiness.RuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
            ? "this face was sampled"
            : $"level sampled {readiness.RuntimeKey}";
        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"{sample}, visual + collision patch proof",
            "visual-only" => $"{sample}, visual-only patch proof",
            "skipped" => $"needs capture/proof ({readiness.Notes})",
            _ => $"{sample}, {readiness.Status}"
        };
    }

    private static string FormatSelectedTextureReadiness(TerrainTextureReadiness? readiness, TerrainPolygon terrain)
    {
        if (readiness == null)
            return "not generated";
        string sample = string.Equals(readiness.RuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
            ? "this face was sampled"
            : $"level sampled {readiness.RuntimeKey}";
        string swap = readiness.SourceTextureId >= 0 && readiness.TargetTextureId >= 0
            ? $" {readiness.SourceTextureId}->{readiness.TargetTextureId}"
            : "";
        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"{sample}, swap{swap} + custom palette proof",
            "texture-swap-only" => $"{sample}, swap{swap} proof only",
            "skipped" => $"needs capture/proof ({readiness.Notes})",
            _ => $"{sample}, {readiness.Status}"
        };
    }

    private static string FormatSelectedStructureReadiness(TerrainStructureReadiness? readiness, TerrainPolygon terrain)
    {
        if (readiness == null)
            return "not generated";
        string remove = string.Equals(readiness.RemoveRuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
            ? "remove sample"
            : string.IsNullOrWhiteSpace(readiness.RemoveRuntimeKey) ? "no remove sample" : $"remove sampled {readiness.RemoveRuntimeKey}";
        string add = string.Equals(readiness.AddRuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase)
            ? "add sample"
            : string.IsNullOrWhiteSpace(readiness.AddRuntimeKey) ? "no add sample" : $"add sampled {readiness.AddRuntimeKey}";
        return readiness.Status.ToLowerInvariant() switch
        {
            "smoke-passed" => $"{remove}, {add}, full structure proof",
            "structure-partial" => $"{remove}, {add}, partial structure proof",
            "remove-only" => $"{remove}, add not proven",
            "skipped" => $"needs capture/proof ({readiness.Notes})",
            _ => $"{remove}, {add}, {readiness.Status}"
        };
    }

    private string BuildSelectedTerrainBehaviorReadiness(TerrainPolygon terrain)
    {
        string behavior = TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior);
        string source = string.IsNullOrWhiteSpace(terrain.BehaviorConfidence)
            ? "unproven"
            : terrain.BehaviorConfidence;
        if (TerrainFaceHasBehaviorProof(terrain))
            return $"{behavior}, proof-backed ({source}).";

        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(terrain.Surface);
        if (surface is "water" or "lava" or "ooze" ||
            behavior.Contains("hazard", StringComparison.OrdinalIgnoreCase))
        {
            return $"{behavior}, hazard candidate only ({source}); use Proof Targets before trusting in-game damage.";
        }

        if (behavior.Contains("solid", StringComparison.OrdinalIgnoreCase))
            return $"{behavior}, solid candidate only ({source}); use a solid-control proof before treating it as safe ground.";

        if (behavior.Contains("steep", StringComparison.OrdinalIgnoreCase) ||
            behavior.Contains("wall", StringComparison.OrdinalIgnoreCase))
        {
            return $"{behavior}, steep/wall candidate only ({source}); verify collision before relying on it.";
        }

        return $"{behavior}, visual/material-only ({source}); behavior is not proven.";
    }

    private Control BuildTerrainProofQueueHint()
    {
        _terrainProofQueueHint.TextWrapping = TextWrapping.Wrap;
        _terrainProofQueueHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainProofQueueHint.FontSize = 12;
        _terrainProofQueueHint.LineHeight = 18;
        RefreshTerrainProofQueueHint();
        return _terrainProofQueueHint;
    }

    private Control BuildTerrainMaterialControls()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Terrain Look",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });
        Grid picker = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        _terrainSurfaceQuickBox.ItemsSource = TerrainSurfaceChoice.Known;
        _terrainSurfaceQuickBox.MinHeight = 32;
        _terrainSurfaceQuickBox.SelectionChanged += (_, _) =>
        {
            if (!_syncingTerrainSurfaceQuick)
                RefreshTerrainSurfaceQuickState();
        };
        AddGridControl(picker, new TextBlock
        {
            Text = "Material",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        }, 0, 0);
        AddGridControl(picker, _terrainSurfaceQuickBox, 1, 0);

        Grid buttons = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        buttons.Children.Add(NewAsyncButton("Label Only", async () => await ApplySelectedTerrainSurfaceQuickAsync()));
        AddGridButton(buttons, NewAsyncButton("Use Material Look", async () => await ApplySelectedTerrainMaterialAndPaletteAsync()), 1, 0);

        Grid textureButtons = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
            }
        };
        Button inGamePaletteButton = NewAsyncButton("Borrow In-Game Look", async () => await ApplySelectedTerrainInGamePaletteAsync());
        Grid.SetColumnSpan(inGamePaletteButton, 2);
        AddGridButton(textureButtons, inGamePaletteButton, 0, 0);
        AddGridButton(textureButtons, NewButton("Copy Face Look", CopySelectedTerrainLook), 0, 1);
        AddGridButton(textureButtons, NewAsyncButton("Paste Face Look", async () => await PasteSelectedTerrainLookAsync()), 1, 1);

        _terrainMaterialActionText.TextWrapping = TextWrapping.Wrap;
        _terrainMaterialActionText.Foreground = new SolidColorBrush(Color.FromRgb(34, 52, 70));
        _terrainMaterialActionText.FontSize = 12;
        _terrainMaterialActionText.FontWeight = FontWeight.SemiBold;
        _terrainMaterialActionText.LineHeight = 17;

        _terrainMaterialProofStatusText.TextWrapping = TextWrapping.Wrap;
        _terrainMaterialProofStatusText.Foreground = new SolidColorBrush(Color.FromRgb(43, 58, 73));
        _terrainMaterialProofStatusText.FontSize = 12;
        _terrainMaterialProofStatusText.LineHeight = 17;

        _terrainSurfaceQuickHint.TextWrapping = TextWrapping.Wrap;
        _terrainSurfaceQuickHint.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainSurfaceQuickHint.FontSize = 12;
        _terrainSurfaceQuickHint.LineHeight = 18;

        panel.Children.Add(picker);
        panel.Children.Add(buttons);
        panel.Children.Add(textureButtons);
        panel.Children.Add(BuildTerrainMaterialStatusPanel());
        panel.Children.Add(_terrainMaterialActionText);
        panel.Children.Add(_terrainSurfaceQuickHint);
        RefreshTerrainSurfaceQuickState();
        return panel;
    }

    private Control BuildTerrainMaterialStatusPanel()
    {
        StackPanel panel = new() { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = "In-game status",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 71, 106))
        });
        panel.Children.Add(_terrainMaterialProofStatusText);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 245, 251)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(191, 211, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    private Control BuildAdvancedTerrainControls()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(BuildTerrainReadinessPanel());
        panel.Children.Add(BuildTerrainExportReadinessPanel());

        Grid tools = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        tools.Children.Add(NewAsyncButton("Review Edits", async () => await ShowTerrainEditReviewAsync()));
        AddGridButton(tools, NewAsyncButton("Preflight BIN", async () => await PreflightTerrainPatchAsync()), 1, 0);
        AddGridButton(tools, NewAsyncButton("Next Proof", async () => await SelectNextTerrainProofTargetAsync()), 0, 1);
        AddGridButton(tools, NewAsyncButton("Proof Summary", async () => await ShowTerrainProofSummaryAsync()), 1, 1);
        AddGridButton(tools, NewAsyncButton("Refresh Proofs", async () => await RefreshTerrainProofReportsAsync()), 0, 2);
        AddGridButton(tools, NewButton("Proof Pair", SelectActiveTerrainProofPairFace), 1, 2);
        AddGridButton(tools, NewButton("Find Edit", FindNextEditedTerrain), 0, 3);
        AddGridButton(tools, NewButton("Find Risk", FindNextRiskyTerrainEdit), 1, 3);
        AddGridButton(tools, NewAsyncButton("Restore Terrain", async () => await RestoreCurrentTerrainAsync()), 0, 4);
        _terrainFindAddSourceButton = NewButton("Find Best Add Face", FindBestTerrainAddSource);
        AddGridButton(tools, _terrainFindAddSourceButton, 1, 4);
        AddGridButton(tools, NewAsyncButton("Color Face", async () => await RecolorSelectedTerrainFaceAsync()), 0, 5);
        AddGridButton(tools, NewAsyncButton("Import Face Palette", async () => await ImportSelectedTerrainFacePaletteAsync()), 1, 5);
        AddGridButton(tools, NewAsyncButton("Paste Face Palette", async () => await PasteSelectedTerrainFacePaletteAsync()), 0, 6);
        AddGridButton(tools, NewAsyncButton("Surface Palette", async () => await ApplySurfaceTerrainPaletteAsync()), 1, 6);
        AddGridButton(tools, NewAsyncButton("Color Shared Texture", async () => await RecolorSelectedTerrainTextureAsync()), 0, 7);
        AddGridButton(tools, NewAsyncButton("Import Shared Palette", async () => await ImportSelectedTerrainPaletteAsync()), 1, 7);
        AddGridButton(tools, NewAsyncButton("Paste Shared Palette", async () => await PasteSelectedTerrainPaletteAsync()), 0, 8);
        AddGridButton(tools, NewAsyncButton("Import Shared Art", async () => await ImportCustomTerrainTextureAsync()), 1, 8);
        Button clearUnusedArtButton = NewAsyncButton("Clear Unused Art", async () => await ClearUnusedCustomTerrainArtAsync());
        Grid.SetColumnSpan(clearUnusedArtButton, 2);
        AddGridButton(tools, clearUnusedArtButton, 0, 9);
        panel.Children.Add(tools);
        panel.Children.Add(BuildTerrainProofQueueHint());
        panel.Children.Add(new TextBlock
        {
            Text = "Advanced holds diagnostics, proof reports, imports, shared-texture changes, and cleanup. The main Terrain tab is the normal edit flow; Preflight BIN only writes a patch plan for inspection.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });

        return new Expander
        {
            Header = "Advanced terrain tools",
            IsExpanded = false,
            Content = panel
        };
    }

    private async Task ApplySelectedTerrainSurfaceQuickAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before setting terrain material.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first.";
            return;
        }

        if (_terrainSurfaceQuickBox.SelectedItem is not TerrainSurfaceChoice choice)
        {
            _statusText.Text = "Choose a terrain material first.";
            return;
        }

        string originalSurface = TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
        string targetSurface = TerrainMaterialClassifier.NormalizeSurfaceName(choice.Surface);
        if (string.Equals(originalSurface, targetSurface, StringComparison.OrdinalIgnoreCase))
        {
            _statusText.Text = $"Texture {_selectedTerrain.TextureId} is already labeled {TerrainMaterialClassifier.FormatSurface(targetSurface)}.";
            RefreshTerrainSurfaceQuickState();
            return;
        }

        await ApplyTerrainSurfaceOverrideAsync(_selectedTerrain.TextureId, targetSurface);
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainSurfaceQuickState();
        RefreshTerrainReadinessHint();
        _statusText.Text = $"Labeled texture {_selectedTerrain.TextureId} as {TerrainMaterialClassifier.FormatSurface(targetSurface)} for editor readability only. Use Material Look, Borrow In-Game Look, or the selected-face/shared-texture color tools to stage texture-page patches for Create BIN; behavior still needs proof captures.";
    }

    private async Task ApplySelectedTerrainMaterialAndPaletteAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before matching terrain material in game.";
            return;
        }

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
        {
            _statusText.Text = "Select a terrain face first.";
            return;
        }

        if (_terrainSurfaceQuickBox.SelectedItem is not TerrainSurfaceChoice choice)
        {
            _statusText.Text = "Choose a terrain material first.";
            return;
        }

        string targetSurface = TerrainMaterialClassifier.NormalizeSurfaceName(choice.Surface);
        if (targetSurface == "unknown")
        {
            _statusText.Text = "Choose a concrete material like grass, water, lava, goo, stone, sand, or ground.";
            return;
        }

        int textureId = _selectedTerrain.TextureId;
        TerrainPalettePreset preset = TerrainPalettePresetCatalog.DefaultForSurface(targetSurface);
        await ApplyTerrainSurfaceOverrideAsync(textureId, targetSurface);

        string customDir = Path.Combine(_workspace.RootPath, "_local", "custom-textures", "generated");
        Directory.CreateDirectory(customDir);
        string fileName = $"{SafeFilePart(_currentLevel.Key)}-texture-{textureId:000}-{SafeFilePart(preset.Id)}-quick-{DateTime.Now:yyyyMMdd-HHmmss}.png";
        string stagedPath = Path.Combine(customDir, fileName);
        await TerrainTexturePngWriter.WriteGradientAsync(stagedPath, 64, 64, preset.Low, preset.High);

        _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
            _workspace.RootPath,
            _currentLevel.Key,
            _currentLevel.DisplayName,
            textureId,
            stagedPath,
            fileName,
            "both",
            64,
            "generated-surface-palette",
            preset.DisplayName,
            NormalizeHex(preset.Low),
            NormalizeHex(preset.High));

        int previewFaces = ApplyCustomTerrainTexturePreviews();
        RefreshCurrentLevelDetails();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshTerrainSurfaceQuickState();
        RefreshTerrainReadinessHint();
        string behavior = TerrainBehaviorClassifier.FormatBehavior(_selectedTerrain.Behavior).ToLowerInvariant();
        _statusText.Text = $"Matched texture {textureId} to {TerrainMaterialClassifier.FormatSurface(targetSurface)} with {preset.DisplayName}, previewed {previewFaces} face(s), and staged texture patches for Create BIN. Behavior is still {behavior} until proven with terrain proof captures. {BuildTerrainMaterialProofHint(targetSurface, textureId)}";
    }

    private void RefreshTerrainSurfaceQuickState()
    {
        _syncingTerrainSurfaceQuick = true;
        try
        {
            bool hasSelection = _selectedTerrain != null && _selectedTerrain.TextureId >= 0;
            _terrainSurfaceQuickBox.IsEnabled = hasSelection;
            if (_selectedTerrain != null)
                _terrainSurfaceQuickBox.SelectedItem = TerrainSurfaceChoice.Find(_selectedTerrain.Surface);
            else
                _terrainSurfaceQuickBox.SelectedItem = null;
        }
        finally
        {
            _syncingTerrainSurfaceQuick = false;
        }

        _terrainSurfaceQuickHint.Text = BuildTerrainMaterialQuickHint();
        _terrainMaterialActionText.Text = BuildTerrainMaterialActionSummary();
        _terrainMaterialProofStatusText.Text = BuildTerrainMaterialProofStatusSummary();
        RefreshTerrainExportReadiness();
    }

    private string BuildTerrainMaterialProofStatusSummary()
    {
        if (_currentLevel == null)
            return "Open a level to see whether terrain art and behavior can be exported.";

        if (_selectedTerrain == null || _selectedTerrain.TextureId < 0)
            return "Select a terrain face. The editor will show whether its label, texture art, and gameplay behavior are actually backed by export/proof data.";

        string selectedSurface = _terrainSurfaceQuickBox.SelectedItem is TerrainSurfaceChoice choice
            ? TerrainMaterialClassifier.NormalizeSurfaceName(choice.Surface)
            : TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
        string displaySurface = TerrainMaterialClassifier.FormatSurface(selectedSurface);
        bool hasCustomTexture = _customTerrainTextures.Any(texture => texture.TextureId == _selectedTerrain.TextureId);
        bool hasProof = HasTerrainMaterialProof(selectedSurface, _selectedTerrain.TextureId);
        string textureStatus = hasCustomTexture
            ? "staged for Create BIN"
            : "not staged yet";
        string proofStatus = BuildTerrainMaterialProofHint(selectedSurface, _selectedTerrain.TextureId);
        string behaviorNote = selectedSurface is "water" or "lava" or "ooze"
            ? "Texture art alone does not prove this will hurt, reset, or otherwise affect Spyro."
            : "Texture art alone does not prove this behaves as safe ground.";

        return
            $"Texture {_selectedTerrain.TextureId}: {displaySurface}\n" +
            $"Editor label: visible in this tool immediately.\n" +
            $"Test BIN art: {textureStatus}.\n" +
            $"Art scope: {BuildSelectedTerrainTextureScopeSummary(_selectedTerrain)}\n" +
            $"Gameplay behavior: {(hasProof ? "proof-backed" : "needs proof")}. {proofStatus} {behaviorNote}";
    }

    private string BuildTerrainMaterialActionSummary()
    {
        if (_selectedTerrain == null)
            return "Material actions: select a terrain face first.";

        string selectedSurface = _terrainSurfaceQuickBox.SelectedItem is TerrainSurfaceChoice choice
            ? TerrainMaterialClassifier.NormalizeSurfaceName(choice.Surface)
            : TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
        string surface = TerrainMaterialClassifier.FormatSurface(selectedSurface);
        string staged = _customTerrainTextures.Any(texture => texture.TextureId == _selectedTerrain.TextureId)
            ? "texture art is staged for Create BIN"
            : "no texture art is staged yet";
        string proof = HasTerrainMaterialProof(selectedSurface, _selectedTerrain.TextureId)
            ? "gameplay behavior has RAM proof"
            : "gameplay behavior still needs RAM proof";

        return $"Texture {_selectedTerrain.TextureId}: Label marks it as {surface} in the editor; Use Material Look writes matching art into the test BIN. Face color tools isolate the selected patch first. {staged}; {proof}. {BuildSelectedTerrainTextureScopeSummary(_selectedTerrain)}";
    }

    private string BuildTerrainMaterialQuickHint()
    {
        if (_selectedTerrain == null)
            return "Select a terrain face to label its material or stage matching in-game texture art.";

        string surface = TerrainMaterialClassifier.FormatSurface(_selectedTerrain.Surface);
        string selectedSurface = _terrainSurfaceQuickBox.SelectedItem is TerrainSurfaceChoice choice
            ? TerrainMaterialClassifier.NormalizeSurfaceName(choice.Surface)
            : TerrainMaterialClassifier.NormalizeSurfaceName(_selectedTerrain.Surface);
        string behavior = TerrainBehaviorClassifier.FormatBehavior(_selectedTerrain.Behavior);
        string custom = _customTerrainTextures.Any(texture => texture.TextureId == _selectedTerrain.TextureId)
            ? " Custom texture art is staged for this texture."
            : " No custom texture art is staged yet.";

        return $"Texture {_selectedTerrain.TextureId}: {surface}. Behavior: {behavior}. {BuildTerrainSurfaceEditStatus(_selectedTerrain)}.{custom} {BuildSelectedTerrainTextureScopeSummary(_selectedTerrain)} Color Selected Face, Import Face Palette, and Paste Face Palette affect only the selected patch by giving it a local texture slot. Color Shared Texture, Import Shared Palette, Paste Shared Palette, and Import Shared Art affect every face sharing this texture. {BuildTerrainMaterialProofHint(selectedSurface, _selectedTerrain.TextureId)}";
    }

    private bool HasTerrainMaterialProof(string surface, int textureId)
    {
        return _currentLevel != null &&
            TerrainBehaviorProofStore.FindBestRule(
                _workspace.RootPath,
                _currentLevel.Key,
                textureId,
                TerrainMaterialClassifier.NormalizeSurfaceName(surface)) != null;
    }

    private bool TerrainFaceHasBehaviorProof(TerrainPolygon terrain)
    {
        if (_currentLevel == null || terrain.TextureId < 0)
            return false;

        string confidence = terrain.BehaviorConfidence;
        if (confidence.Contains("proof", StringComparison.OrdinalIgnoreCase) ||
            confidence.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            confidence.Contains("observed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TerrainBehaviorProofStore.FindBestRule(
            _workspace.RootPath,
            _currentLevel.Key,
            terrain.TextureId,
            terrain.Surface) != null;
    }

    private string BuildTerrainMaterialProofHint(string surface, int textureId)
    {
        if (_currentLevel == null)
            return "Gameplay behavior proof is unavailable until a level is loaded.";

        surface = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
        TerrainBehaviorRule? rule = TerrainBehaviorProofStore.FindBestRule(
            _workspace.RootPath,
            _currentLevel.Key,
            textureId,
            surface);
        if (rule != null)
        {
            return $"RAM proof: {TerrainBehaviorClassifier.FormatBehavior(rule.Behavior).ToLowerInvariant()} ({rule.Confidence}, {rule.ObservationCount} capture(s)).";
        }

        if (surface is "water" or "lava" or "ooze")
            return $"RAM proof: no confirmed {TerrainMaterialClassifier.FormatSurface(surface)} hazard response yet; use Proof Targets before trusting this to hurt or reset Spyro.";

        if (surface is "grass" or "sand" or "stone" or "ground" or "brick" or "ice" or "cliff")
            return $"RAM proof: no confirmed {TerrainMaterialClassifier.FormatSurface(surface)} solid-control response yet; use Proof Targets before treating behavior as proven.";

        return "RAM proof: classify this material and capture before/after RAM before treating behavior as proven.";
    }

    private static string FormatTerrainGeometryEdit(TerrainPolygon terrain)
    {
        if (!terrain.HasHeightEdit && !terrain.HasPositionEdit)
            return "none";

        List<string> parts = new();
        if (terrain.HasHeightEdit)
        {
            float[] deltas = terrain.TerrainVertexDeltas().ToArray();
            if (deltas.Length == 0)
                parts.Add($"{terrain.TerrainEditDeltaZ:+0.0;-0.0;0.0} Z avg");
            else if (deltas
                .Select((delta, index) => new { delta, index })
                .Where(item => Math.Abs(item.delta) > 0.001f)
                .ToList() is { Count: 1 } changedZ)
            {
                var point = changedZ[0];
                parts.Add($"point {point.index + 1} Z {point.delta:+0.0;-0.0;0.0}");
            }
            else
            {
                int changedCount = deltas.Count(delta => Math.Abs(delta) > 0.001f);
                parts.Add($"{terrain.TerrainEditDeltaZ:+0.0;-0.0;0.0} Z avg, {deltas.Min():+0.0;-0.0;0.0} to {deltas.Max():+0.0;-0.0;0.0} vertices");
                if (changedCount > 0)
                    parts[^1] += $" ({changedCount} point(s))";
            }
        }

        if (terrain.HasPositionEdit)
        {
            Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
            var changedXY = deltas
                .Select((delta, index) => new { delta, index })
                .Where(item => Math.Abs(item.delta.X) > 0.001f || Math.Abs(item.delta.Y) > 0.001f)
                .ToList();
            float minX = deltas.Length == 0 ? 0 : deltas.Min(delta => delta.X);
            float maxX = deltas.Length == 0 ? 0 : deltas.Max(delta => delta.X);
            float minY = deltas.Length == 0 ? 0 : deltas.Min(delta => delta.Y);
            float maxY = deltas.Length == 0 ? 0 : deltas.Max(delta => delta.Y);
            if (changedXY.Count == 1)
            {
                var point = changedXY[0];
                parts.Add($"point {point.index + 1} X {point.delta.X:+0;-0;0}, Y {point.delta.Y:+0;-0;0}");
            }
            else
            {
                string summary = $"X {minX:+0;-0;0} to {maxX:+0;-0;0}, Y {minY:+0;-0;0} to {maxY:+0;-0;0}";
                if (changedXY.Count > 0)
                    summary += $" ({changedXY.Count} point(s))";
                parts.Add(summary);
            }
        }

        return string.Join("; ", parts);
    }

    private void RefreshTerrainCollisionReadiness(string levelKey)
    {
        TerrainCollisionLoadData loaded = LoadTerrainCollisionData(levelKey, _currentGeometry);
        _terrainCollisionTriangleKeys = loaded.TriangleKeys;
        _terrainCollisionMatchedFaces = loaded.MatchedFaces;
        _terrainCollisionReadinessMessage = loaded.ReadinessMessage;
    }

    private TerrainCollisionLoadData LoadTerrainCollisionData(string levelKey, GeometryCandidate? geometry)
    {
        HashSet<string> triangleKeys = new(StringComparer.Ordinal);
        if (geometry == null || geometry.Polygons.Count == 0)
            return new TerrainCollisionLoadData(triangleKeys, "Playable terrain collision: no terrain loaded", 0);

        string ramPath = TerrainPatchDataLocator.FindRamDump(_workspace, levelKey);
        if (!File.Exists(ramPath))
            return new TerrainCollisionLoadData(triangleKeys, "Playable terrain collision: RAM capture missing", 0);

        try
        {
            SpyroCollisionTable table = SpyroCollisionDecoder.Decode(File.ReadAllBytes(ramPath));
            triangleKeys = table.Triangles
                .Select(CollisionTriangleKey)
                .ToHashSet(StringComparer.Ordinal);
            int matchedFaces = geometry.Polygons.Count(polygon => HasTerrainCollisionMatch(polygon, triangleKeys));
            return new TerrainCollisionLoadData(triangleKeys, $"Playable terrain collision: {matchedFaces}/{geometry.Polygons.Count} faces matched", matchedFaces);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or InvalidOperationException)
        {
            return new TerrainCollisionLoadData(triangleKeys, "Playable terrain collision: could not decode RAM collision table", 0);
        }
    }

    private void RefreshTerrainBrushSafetyState(bool updateStatus, bool defaultToSafe = false)
    {
        bool hasGeometry = _currentGeometry?.Polygons.Count > 0;
        bool hasPlayableMatches = hasGeometry && _terrainCollisionTriangleKeys.Count > 0 && _terrainCollisionMatchedFaces > 0;
        _viewport.TerrainBrushVertexClassifier = ClassifyTerrainBrushPreviewVertex;

        _syncingTerrainBrushSafety = true;
        try
        {
            _terrainPlayableOnlyBox.IsEnabled = hasPlayableMatches;
            if (!hasPlayableMatches)
            {
                _terrainPlayableOnlyBox.IsChecked = false;
            }
            else if (defaultToSafe || _terrainPlayableOnlyBox.IsChecked != false)
            {
                _terrainPlayableOnlyBox.IsChecked = true;
            }
        }
        finally
        {
            _syncingTerrainBrushSafety = false;
        }

        if (!hasGeometry)
        {
            _terrainBrushSafetyText.Text = "Load a level to use the terrain brush.";
            _viewport.TerrainBrushSafetyHint = "";
            return;
        }

        if (!hasPlayableMatches)
        {
            _terrainBrushSafetyText.Text = "Playable ground patching is not ready for this level; terrain brush edits may only change the visible mesh.";
            _viewport.TerrainBrushSafetyHint = "visual-only possible";
            if (updateStatus)
                _statusText.Text = "No playable terrain collision matches are available for this level yet.";
            return;
        }

        string count = $"{_terrainCollisionMatchedFaces}/{_currentGeometry!.Polygons.Count}";
        if (_terrainPlayableOnlyBox.IsChecked == true)
        {
            _terrainBrushSafetyText.Text = $"{count} faces can patch playable ground. Shared visual side points follow moved playable terrain, and Create BIN now attempts solid textured side walls where the sector has safe room.";
            _viewport.TerrainBrushSafetyHint = "safe playable only";
            if (updateStatus)
                _statusText.Text = "Only edit playable ground is on; matching visual side points will follow, and side-wall coverage is reported in Review Edits/Create BIN.";
        }
        else
        {
            _terrainBrushSafetyText.Text = $"{count} faces can patch playable ground. The brush can also paint visible-only terrain, but unproven spots may not get gameplay collision.";
            _viewport.TerrainBrushSafetyHint = "all visible terrain";
            if (updateStatus)
                _statusText.Text = "Only edit playable ground is off; visible-only terrain can be edited.";
        }
    }

    private string FormatTerrainCollisionReadiness(TerrainPolygon terrain)
    {
        if (_terrainCollisionTriangleKeys.Count == 0)
            return _terrainCollisionReadinessMessage.Replace("Playable terrain collision: ", "", StringComparison.OrdinalIgnoreCase);

        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        if (coverage.MatchedTriangles == 0)
            return "not matched; visual geometry patch only for now";

        if (coverage.MatchedTriangles >= coverage.TotalTriangles)
            return "matched; test BIN can include playable geometry patches";

        return $"partially matched ({coverage.MatchedTriangles}/{coverage.TotalTriangles} triangles); safe brush edits matched vertices only";
    }

    private string FormatTerrainPatchReadiness(TerrainPolygon terrain)
    {
        TerrainPatchSafetyKind safety = ClassifyTerrainPatchSafety(terrain);
        return safety switch
        {
            TerrainPatchSafetyKind.None => "not edited yet",
            TerrainPatchSafetyKind.Structural => "structural terrain edit staged; Create BIN can remove faces or add copied visible terrain where supported",
            TerrainPatchSafetyKind.PlayableHeight => "ready; height should affect playable ground",
            TerrainPatchSafetyKind.PartialHeight => "partly ready; some height points may be visual-only",
            TerrainPatchSafetyKind.VisualOnlyHeight => "visual-only; may not change where Spyro stands",
            TerrainPatchSafetyKind.UnknownHeight => "unknown; needs terrain proof/collision data",
            TerrainPatchSafetyKind.TextureOnly => "ready; texture/surface edit only",
            _ => "unknown"
        };
    }

    private bool HasTerrainCollisionMatch(TerrainPolygon terrain)
    {
        return GetTerrainCollisionCoverage(terrain).MatchedTriangles > 0;
    }

    private static bool HasTerrainCollisionMatch(TerrainPolygon terrain, IReadOnlySet<string> triangleKeys)
    {
        return GetTerrainCollisionCoverage(terrain, triangleKeys).MatchedTriangles > 0;
    }

    private TerrainBrushPreviewVertexKind ClassifyTerrainBrushPreviewVertex(TerrainPolygon terrain, int vertexIndex)
    {
        if (!IsPlayableTerrainOnlyEnabled())
            return TerrainBrushPreviewVertexKind.Editable;

        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        return coverage.VertexIndexes.Contains(vertexIndex)
            ? TerrainBrushPreviewVertexKind.Editable
            : TerrainBrushPreviewVertexKind.VisualOnly;
    }

    private TerrainCollisionCoverage GetTerrainCollisionCoverage(TerrainPolygon terrain)
    {
        return GetTerrainCollisionCoverage(terrain, _terrainCollisionTriangleKeys);
    }

    private static TerrainCollisionCoverage GetTerrainCollisionCoverage(TerrainPolygon terrain, IReadOnlySet<string> triangleKeys)
    {
        HashSet<int> vertexIndexes = new();
        if (triangleKeys.Count == 0 || terrain.Points.Count < 3 || terrain.OriginalZValues.Length < terrain.Points.Count)
            return new TerrainCollisionCoverage(0, 0, vertexIndexes);

        int matchedTriangles = 0;
        int totalTriangles = 0;
        for (int i = 1; i < terrain.Points.Count - 1; i++)
        {
            totalTriangles++;
            if (triangleKeys.Contains(VisualTriangleKey(terrain, 0, i, i + 1)))
            {
                matchedTriangles++;
                vertexIndexes.Add(0);
                vertexIndexes.Add(i);
                vertexIndexes.Add(i + 1);
            }
        }

        return new TerrainCollisionCoverage(matchedTriangles, totalTriangles, vertexIndexes);
    }

    private static string VisualTriangleKey(TerrainPolygon terrain, int a, int b, int c)
    {
        return string.Join("|", new[]
            {
                VisualPointKey(terrain.Points[a].X, terrain.Points[a].Y, terrain.OriginalZValues[a]),
                VisualPointKey(terrain.Points[b].X, terrain.Points[b].Y, terrain.OriginalZValues[b]),
                VisualPointKey(terrain.Points[c].X, terrain.Points[c].Y, terrain.OriginalZValues[c])
            }
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string CollisionTriangleKey(SpyroCollisionTriangle triangle)
    {
        return string.Join("|", triangle.Points
            .Select(point => VisualPointKey(point.X, point.Y, point.Z))
            .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string VisualPointKey(float x, float y, float z)
    {
        return $"{MathF.Round(x)},{MathF.Round(y)},{MathF.Round(z)}";
    }

    private Control BuildTerrainBrushControls()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(247, 250, 253)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 214, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Brush Sculpt",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(42, 61, 79))
        });

        Grid modeGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        TextBlock modeLabel = new()
        {
            Text = "Brush",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
        _terrainBrushModeBox.MinWidth = 160;
        AddGridControl(modeGrid, modeLabel, 0, 0);
        AddGridControl(modeGrid, _terrainBrushModeBox, 1, 0);
        _terrainPlayableOnlyBox.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainPlayableOnlyBox.Margin = new Thickness(0, 0, 0, 2);
        _terrainJoinSeamsBox.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainJoinSeamsBox.Margin = new Thickness(0, 0, 0, 2);
        _terrainBrushSafetyText.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainBrushSafetyText.FontSize = 12;
        _terrainBrushSafetyText.TextWrapping = TextWrapping.Wrap;
        _terrainBrushHistoryText.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainBrushHistoryText.FontSize = 12;
        _terrainBrushHistoryText.TextWrapping = TextWrapping.Wrap;

        Grid buttons = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto)
            }
        };
        panel.Children.Add(modeGrid);
        panel.Children.Add(_terrainPlayableOnlyBox);
        panel.Children.Add(_terrainJoinSeamsBox);
        panel.Children.Add(_terrainBrushSafetyText);
        _terrainUndoStrokeButton = NewButton("Undo Stroke", UndoLastTerrainBrushStroke);
        _terrainRedoStrokeButton = NewButton("Redo Stroke", RedoLastTerrainBrushStroke);
        buttons.Children.Add(_terrainUndoStrokeButton);
        AddGridButton(buttons, _terrainRedoStrokeButton, 1, 0);
        Button resetBrushButton = NewButton("Reset Brush", ResetTerrainBrush);
        AddGridButton(buttons, resetBrushButton, 2, 0);

        panel.Children.Add(buttons);

        StackPanel tuning = new() { Spacing = 8 };
        tuning.Children.Add(_terrainBrushHistoryText);
        tuning.Children.Add(BuildTerrainBrushSlider("Size", _terrainBrushRadiusSlider, _terrainBrushRadiusText));
        tuning.Children.Add(BuildTerrainBrushSlider("Strength", _terrainBrushStrengthSlider, _terrainBrushStrengthText));
        tuning.Children.Add(BuildTerrainBrushSlider("Feather", _terrainBrushFeatherSlider, _terrainBrushFeatherText));
        tuning.Children.Add(BuildTerrainBrushPresetControls());
        panel.Children.Add(BuildCollapsedPanel("Brush tuning", tuning));

        RefreshTerrainBrushHistoryText();
        border.Child = panel;
        return border;
    }

    private Control BuildTerrainBrushPresetControls()
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        grid.Children.Add(NewButton("Fine", () => SetTerrainBrushPreset("Fine brush", 176, 24, 56)));
        AddGridButton(grid, NewButton("Normal", () => SetTerrainBrushPreset("Normal brush", DefaultTerrainBrushRadius, DefaultTerrainBrushStrength, DefaultTerrainBrushFeather)), 1, 0);
        AddGridButton(grid, NewButton("Wide", () => SetTerrainBrushPreset("Wide brush", 1024, 72, 78)), 2, 0);
        return grid;
    }

    private Control BuildTerrainPointControls()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };

        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Single Point",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(38, 48, 59))
        });

        _terrainPointEditText.TextWrapping = TextWrapping.Wrap;
        _terrainPointEditText.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        _terrainPointEditText.FontSize = 12;
        _terrainPointEditText.LineHeight = 17;
        panel.Children.Add(_terrainPointEditText);

        Grid pointGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        _terrainPointPreviousButton = NewButton("Previous Point", () => SelectTerrainPointOffset(-1));
        _terrainPointNextButton = NewButton("Next Point", () => SelectTerrainPointOffset(1));
        _terrainPointPlayableButton = NewButton("Playable Point", SelectNextPlayableTerrainPoint);
        _terrainPointResetButton = NewButton("Reset Point", ResetSelectedTerrainPoint);
        _terrainPointEditButton = NewAsyncButton("Edit Point", EditSelectedTerrainPointAsync);
        _terrainPointRaiseButton = NewButton("Point Up", () => NudgeSelectedTerrainPoint(32));
        _terrainPointLowerButton = NewButton("Point Down", () => NudgeSelectedTerrainPoint(-32));
        _terrainPointXMinusButton = NewButton("Point X-", () => NudgeSelectedTerrainPointXY(-32, 0));
        _terrainPointXPlusButton = NewButton("Point X+", () => NudgeSelectedTerrainPointXY(32, 0));
        _terrainPointYMinusButton = NewButton("Point Y-", () => NudgeSelectedTerrainPointXY(0, -32));
        _terrainPointYPlusButton = NewButton("Point Y+", () => NudgeSelectedTerrainPointXY(0, 32));

        pointGrid.Children.Add(_terrainPointPreviousButton);
        AddGridButton(pointGrid, _terrainPointNextButton, 1, 0);
        AddGridButton(pointGrid, _terrainPointPlayableButton, 2, 0);
        AddGridButton(pointGrid, _terrainPointResetButton, 0, 1);
        AddGridButton(pointGrid, _terrainPointEditButton, 1, 1);
        AddGridButton(pointGrid, _terrainPointRaiseButton, 2, 1);
        AddGridButton(pointGrid, _terrainPointLowerButton, 0, 2);
        AddGridButton(pointGrid, _terrainPointXMinusButton, 1, 2);
        AddGridButton(pointGrid, _terrainPointXPlusButton, 2, 2);
        AddGridButton(pointGrid, _terrainPointYMinusButton, 0, 3);
        AddGridButton(pointGrid, _terrainPointYPlusButton, 1, 3);
        panel.Children.Add(pointGrid);

        border.Child = panel;
        RefreshTerrainPointControls();
        return new Expander
        {
            Header = "Precise point controls",
            IsExpanded = false,
            Content = border
        };
    }

    private static Control BuildTerrainBrushSlider(string label, Slider slider, TextBlock valueText)
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(72)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(56))
            },
            ColumnSpacing = 8
        };
        TextBlock labelText = new()
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
        valueText.VerticalAlignment = VerticalAlignment.Center;
        valueText.Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92));
        valueText.TextAlignment = TextAlignment.Right;
        AddGridControl(grid, labelText, 0, 0);
        AddGridControl(grid, slider, 1, 0);
        AddGridControl(grid, valueText, 2, 0);
        return grid;
    }

    private void RefreshTerrainBrushLabels()
    {
        _terrainBrushRadiusText.Text = $"{_terrainBrushRadiusSlider.Value:0}";
        _terrainBrushStrengthText.Text = $"{_terrainBrushStrengthSlider.Value:0}";
        _terrainBrushFeatherText.Text = $"{_terrainBrushFeatherSlider.Value:0}%";
        RefreshTerrainBrushHistoryText();
    }

    private void UpdateTerrainBrushPreview()
    {
        _viewport.TerrainBrushRadius = _terrainBrushRadiusSlider.Value;
        _viewport.TerrainBrushStrength = _terrainBrushStrengthSlider.Value;
        _viewport.TerrainBrushFeather = _terrainBrushFeatherSlider.Value;
    }

    private void RefreshTerrainBrushHistoryText()
    {
        string mode = BuildTerrainBrushModeSummary();
        string history = _terrainBrushUndoHistory.Count == 0 && _terrainBrushRedoHistory.Count == 0
            ? "No brush strokes to undo yet."
            : $"Brush history: {_terrainBrushUndoHistory.Count} undo, {_terrainBrushRedoHistory.Count} redo.";
        _terrainBrushHistoryText.Text = string.IsNullOrWhiteSpace(_lastTerrainBrushSummary)
            ? $"{mode}\n{history}"
            : $"{mode}\n{history}\nLast brush: {_lastTerrainBrushSummary}";
        if (_terrainUndoStrokeButton != null)
            _terrainUndoStrokeButton.IsEnabled = _terrainBrushUndoHistory.Count > 0;
        if (_terrainRedoStrokeButton != null)
            _terrainRedoStrokeButton.IsEnabled = _terrainBrushRedoHistory.Count > 0;
    }

    private string BuildTerrainBrushModeSummary()
    {
        TerrainBrushModeOption selected = _terrainBrushModeBox.SelectedItem as TerrainBrushModeOption
            ?? TerrainBrushModeOption.All[0];
        if (selected.Action == TerrainBrushAction.Off)
            return "Mode: Select objects and terrain.";

        return $"Mode: {selected.Label} active. Size {_terrainBrushRadiusSlider.Value:0}, strength {_terrainBrushStrengthSlider.Value:0}, feather {_terrainBrushFeatherSlider.Value:0}%. Left-drag terrain to paint.";
    }

    private void SetTerrainBrushPreset(string label, double radius, double strength, double feather)
    {
        _terrainBrushRadiusSlider.Value = Math.Clamp(radius, _terrainBrushRadiusSlider.Minimum, _terrainBrushRadiusSlider.Maximum);
        _terrainBrushStrengthSlider.Value = Math.Clamp(strength, _terrainBrushStrengthSlider.Minimum, _terrainBrushStrengthSlider.Maximum);
        _terrainBrushFeatherSlider.Value = Math.Clamp(feather, _terrainBrushFeatherSlider.Minimum, _terrainBrushFeatherSlider.Maximum);
        RefreshTerrainBrushLabels();
        UpdateTerrainBrushPreview();
        _statusText.Text = $"{label}: size {_terrainBrushRadiusSlider.Value:0}, strength {_terrainBrushStrengthSlider.Value:0}, feather {_terrainBrushFeatherSlider.Value:0}%.";
    }

    private void AdjustTerrainBrushFromViewport(ViewportTerrainBrushAdjustmentRequestedEventArgs e)
    {
        string label = e.Kind switch
        {
            TerrainBrushAdjustmentKind.Size => AdjustTerrainBrushSlider(_terrainBrushRadiusSlider, e.Direction * 64, "Brush size", ""),
            TerrainBrushAdjustmentKind.Strength => AdjustTerrainBrushSlider(_terrainBrushStrengthSlider, e.Direction * 8, "Brush strength", ""),
            TerrainBrushAdjustmentKind.Feather => AdjustTerrainBrushSlider(_terrainBrushFeatherSlider, e.Direction * 5, "Brush feather", "%"),
            _ => ""
        };
        if (!string.IsNullOrWhiteSpace(label))
            _statusText.Text = label;
    }

    private string AdjustTerrainBrushSlider(Slider slider, double delta, string label, string suffix)
    {
        double value = Math.Clamp(slider.Value + delta, slider.Minimum, slider.Maximum);
        slider.Value = value;
        RefreshTerrainBrushLabels();
        UpdateTerrainBrushPreview();
        return $"{label}: {value:0}{suffix}.";
    }

    private void RefreshTerrainBrushMode()
    {
        TerrainBrushModeOption selected = _terrainBrushModeBox.SelectedItem as TerrainBrushModeOption
            ?? TerrainBrushModeOption.All[0];
        _activeFlattenTerrainZ = null;
        _viewport.TerrainBrushAction = selected.Action;
        RefreshTerrainBrushHistoryText();
        if (selected.Action != TerrainBrushAction.Off)
            _statusText.Text = $"{selected.Label}: left-drag terrain in Map View or Fly 3D to paint with the smooth brush.";
    }

    private void RunTerrainBrushCommandOrMode(TerrainBrushAction action, Action selectedAction)
    {
        SelectTerrainBrushMode(action, announce: _selectedTerrain == null);
        if (_selectedTerrain != null)
        {
            RunTerrainBrushCommand(selectedAction);
            _statusText.Text = $"{_statusText.Text} Paint mode is active; left-drag terrain to keep sculpting.";
            return;
        }
    }

    private void SelectTerrainBrushMode(TerrainBrushAction action, bool announce = true)
    {
        TerrainBrushModeOption? option = TerrainBrushModeOption.All.FirstOrDefault(item => item.Action == action);
        if (option == null)
            return;

        _terrainBrushModeBox.SelectedItem = option;
        RefreshTerrainBrushMode();
        if (announce)
            _statusText.Text = $"{option.Label}: left-drag terrain in Map View or Fly 3D to paint with the smooth brush.";
    }

    private void ResetTerrainBrush()
    {
        _terrainBrushModeBox.SelectedItem = TerrainBrushModeOption.All[0];
        _terrainBrushRadiusSlider.Value = DefaultTerrainBrushRadius;
        _terrainBrushStrengthSlider.Value = DefaultTerrainBrushStrength;
        _terrainBrushFeatherSlider.Value = DefaultTerrainBrushFeather;
        RefreshTerrainBrushSafetyState(updateStatus: false, defaultToSafe: true);
        RefreshTerrainBrushLabels();
        UpdateTerrainBrushPreview();
        RefreshTerrainBrushMode();
        _statusText.Text = "Reset the terrain brush to safe default settings.";
    }

    private void FindNextEditedTerrain()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
        {
            _statusText.Text = "No terrain is loaded yet.";
            return;
        }

        List<int> editedIndexes = _currentGeometry.Polygons
            .Select((polygon, index) => new { polygon, index })
            .Where(item => item.polygon.IsTerrainEdited)
            .Select(item => item.index)
            .ToList();
        if (editedIndexes.Count == 0)
        {
            _statusText.Text = "No edited terrain faces yet.";
            return;
        }

        int nextIndex = editedIndexes.FirstOrDefault(index => index > _selectedTerrainIndex);
        if (nextIndex <= _selectedTerrainIndex)
            nextIndex = editedIndexes[0];

        _viewport.FocusTerrain(nextIndex);
        int position = editedIndexes.IndexOf(nextIndex) + 1;
        _statusText.Text = $"Found edited terrain face {nextIndex} ({position}/{editedIndexes.Count}).";
    }

    private void FindNextRiskyTerrainEdit()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
        {
            _statusText.Text = "No terrain is loaded yet.";
            return;
        }

        List<int> riskyIndexes = _currentGeometry.Polygons
            .Select((polygon, index) => new { polygon, index })
            .Where(item => item.polygon.HasStructureEdit || IsRiskyTerrainGeometryEdit(item.polygon))
            .Select(item => item.index)
            .ToList();
        if (riskyIndexes.Count == 0)
        {
            _statusText.Text = "No terrain edits needing review found.";
            return;
        }

        int nextIndex = riskyIndexes.FirstOrDefault(index => index > _selectedTerrainIndex);
        if (nextIndex <= _selectedTerrainIndex)
            nextIndex = riskyIndexes[0];

        TerrainPolygon terrain = _currentGeometry.Polygons[nextIndex];
        _viewport.FocusTerrain(nextIndex);
        int position = riskyIndexes.IndexOf(nextIndex) + 1;
        if (terrain.IsTerrainRemoved)
            _statusText.Text = $"Found remove-face terrain edit {nextIndex} ({position}/{riskyIndexes.Count}): Create BIN can neutralize the visible face and matched collision.";
        else if (terrain.IsTerrainAddClone)
            _statusText.Text = $"Found add-copy terrain edit {nextIndex} ({position}/{riskyIndexes.Count}): Create BIN can add independent visible terrain, plus copied collision when this face has a matched same-cell placeholder.";
        else
            _statusText.Text = $"Found risky terrain edit {nextIndex} ({position}/{riskyIndexes.Count}): {FormatTerrainCollisionReadiness(terrain)}.";
    }

    private void FindBestTerrainAddSource()
    {
        if (!CanUseTerrainAddCopyInCurrentLevel())
        {
            _statusText.Text = BuildTerrainAddCopyUnavailableHint();
            return;
        }

        if (!TryGetBestTerrainAddSource(out TerrainStructureReadiness? readiness, out int index))
        {
            if (TryGetBestTerrainAddSourceByFaceReadiness(out int estimatedIndex, out TerrainAddCopyFaceReadiness estimatedReadiness))
            {
                TerrainPolygon estimatedTerrain = _currentGeometry!.Polygons[estimatedIndex];
                _viewport.FocusTerrain(estimatedIndex);
                string collision = estimatedReadiness.HasCollisionMatch
                    ? "collision matched; copied collision still needs Create BIN proof"
                    : "visible-copy only; collision is not matched";
                _statusText.Text = $"Selected likely add-copy source face {estimatedIndex} ({estimatedTerrain.RuntimeKey}): slack {estimatedReadiness.AvailableSlackBytes}/{estimatedReadiness.RequiredSlackBytes} bytes, {collision}. Use Add Terrain Copy, then Review Edits/Create BIN to verify the patch.";
                return;
            }

            string levelName = _currentLevel?.DisplayName ?? "this level";
            _statusText.Text = $"No proved or estimated add-copy source face is available for {levelName} yet. Build/load terrain source-search data, run the structure readiness smoke pass, or use a captured level with add-copy proof.";
            return;
        }

        TerrainStructureReadiness currentReadiness = readiness!;
        TerrainPolygon terrain = _currentGeometry!.Polygons[index];
        _viewport.FocusTerrain(index);
        string strength = currentReadiness.AddHasIndependentVertices && currentReadiness.AddHasCollisionPatch
            ? "full visible terrain plus copied collision proof"
            : currentReadiness.AddHasIndependentVertices
                ? "visible terrain proof; collision still partial"
                : "partial structure proof";
        _statusText.Text = $"Selected best add-copy source face {index} ({terrain.RuntimeKey}) for {currentReadiness.LevelName}: {strength}. Use Add Terrain Copy to stage a copied face from here.";
    }

    private bool TrySelectBestTerrainAddCopySource(out int index, out TerrainPolygon? terrain, out string reason)
    {
        index = -1;
        terrain = null;
        reason = "";
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return false;

        if (TryGetBestTerrainAddSource(out TerrainStructureReadiness? readiness, out int provedIndex))
        {
            TerrainStructureReadiness currentReadiness = readiness!;
            TerrainPolygon provedTerrain = _currentGeometry.Polygons[provedIndex];
            string strength = currentReadiness.AddHasIndependentVertices && currentReadiness.AddHasCollisionPatch
                ? "full visible terrain plus copied collision proof"
                : currentReadiness.AddHasIndependentVertices
                    ? "visible terrain proof; collision still partial"
                    : "partial structure proof";
            SelectTerrainAddCopySource(provedIndex, provedTerrain);
            index = provedIndex;
            terrain = provedTerrain;
            reason = $"best proved source face {provedIndex} ({provedTerrain.RuntimeKey}), {strength}";
            return true;
        }

        if (TryGetBestTerrainAddSourceByFaceReadiness(out int estimatedIndex, out TerrainAddCopyFaceReadiness estimatedReadiness))
        {
            TerrainPolygon estimatedTerrain = _currentGeometry.Polygons[estimatedIndex];
            string collision = estimatedReadiness.HasCollisionMatch
                ? "collision matched; copied collision still needs Create BIN proof"
                : "visible-copy only; collision is not matched";
            SelectTerrainAddCopySource(estimatedIndex, estimatedTerrain);
            index = estimatedIndex;
            terrain = estimatedTerrain;
            reason = $"best live source face {estimatedIndex} ({estimatedTerrain.RuntimeKey}), slack {estimatedReadiness.AvailableSlackBytes}/{estimatedReadiness.RequiredSlackBytes} bytes, {collision}";
            return true;
        }

        return false;
    }

    private void SelectTerrainAddCopySource(int index, TerrainPolygon terrain)
    {
        _selectedTerrain = terrain;
        _selectedTerrainIndex = index;
        _viewport.FocusTerrain(index);
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(index, terrain));
    }

    private bool TryGetBestTerrainAddSourceByFaceReadiness(out int index, out TerrainAddCopyFaceReadiness readiness)
    {
        index = -1;
        readiness = default;
        if (!CanUseTerrainAddCopyInCurrentLevel())
            return false;
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return false;

        List<(int Index, TerrainPolygon Terrain, TerrainAddCopyFaceReadiness Readiness)> candidates = _currentGeometry.Polygons
            .Select((terrain, terrainIndex) => (Index: terrainIndex, Terrain: terrain, Readiness: GetTerrainAddCopyFaceReadiness(terrain)))
            .Where(item => item.Readiness.CanAddIndependentVisibleTerrain)
            .OrderByDescending(item => item.Readiness.HasCollisionMatch)
            .ThenByDescending(item => item.Readiness.AvailableSlackBytes - item.Readiness.RequiredSlackBytes)
            .ThenByDescending(item => item.Terrain.Points.Count)
            .ThenBy(item => item.Index)
            .ToList();
        if (candidates.Count == 0)
            return false;

        index = candidates[0].Index;
        readiness = candidates[0].Readiness;
        return true;
    }

    private bool TryGetBestTerrainAddSource(out TerrainStructureReadiness? readiness, out int index)
    {
        readiness = null;
        index = -1;
        if (!CanUseTerrainAddCopyInCurrentLevel())
            return false;
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0 || _currentLevel == null)
            return false;

        readiness = LoadCurrentTerrainStructureReadiness();
        if (readiness == null || string.IsNullOrWhiteSpace(readiness.AddRuntimeKey))
            return false;

        bool hasUsefulProof = readiness.AddHasIndependentVertices || readiness.AddHasCollisionPatch || readiness.AddHasFallbackAppend || readiness.AddPatchCount > 0;
        if (!hasUsefulProof)
            return false;

        string addRuntimeKey = readiness.AddRuntimeKey;
        index = _currentGeometry.Polygons.FindIndex(polygon => string.Equals(polygon.RuntimeKey, addRuntimeKey, StringComparison.OrdinalIgnoreCase));
        return index >= 0;
    }

    private async Task ShowTerrainEditReviewAsync(int initialFilterIndex = 0)
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
        {
            _statusText.Text = "No terrain is loaded yet.";
            return;
        }

        IReadOnlyList<TerrainEditReviewItem> items = BuildTerrainEditReviewItems();
        if (items.Count == 0 && _customTerrainTextures.Count == 0)
        {
            _statusText.Text = "No edited terrain faces or custom terrain textures yet.";
            return;
        }

        ComboBox filterBox = new()
        {
            ItemsSource = new[] { "All edits", "Needs review", "Playable geometry", "Structure", "Palette/art", "Texture only" },
            SelectedIndex = Math.Clamp(initialFilterIndex, 0, 5),
            MinWidth = 160
        };
        ListBox list = new()
        {
            MinHeight = 340,
            ItemTemplate = new FuncDataTemplate<TerrainEditReviewItem>((item, _) => new TextBlock
            {
                Text = item?.ListText ?? "",
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
                FontSize = 12,
                Margin = new Thickness(4, 3)
            })
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 90
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not TerrainEditReviewItem item)
            {
                details.Text = _customTerrainTextures.Count > 0
                    ? "No edited terrain face is selected. The texture summary above shows staged texture-page art that Create BIN can include."
                    : "Select an edited terrain face to review its patch status.";
                return;
            }

            string target = item.IsTextureArtOnly
                ? $"Texture {item.TextureId}: custom palette/art"
                : $"Face {item.TerrainIndex}: {item.SafetyLabel}";
            string affected = (item.IsTextureArtOnly || item.HasCustomTextureArt) && item.AffectedFaceCount > 0
                ? $"\nAffected faces: {item.AffectedFaceCount}"
                : "";
            details.Text =
                $"{target}\n" +
                $"What changed: {item.EditSummary}\n" +
                $"Create BIN: {item.ExportImpact}\n" +
                $"Runtime key: {item.RuntimeKey}{affected}\n" +
                $"Surface: {item.Surface}\n" +
                $"Texture: {item.TextureChange}\n" +
                $"Geometry edit: {item.HeightEdit}\n" +
                $"Playable collision: {item.CollisionReadiness}\n" +
                $"Texture art: {item.TextureExport}\n" +
                $"Gameplay proof: {item.ProofStatus}\n" +
                $"Next step: {BuildTerrainEditReviewNextStep(item)}";
        }

        void RefreshFilteredList()
        {
            List<TerrainEditReviewItem> filtered = items
                .Where(item => MatchesTerrainEditReviewFilter(item, filterBox.SelectedIndex))
                .ToList();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Count == 0 ? -1 : 0;
            RefreshDetails();
        }

        filterBox.SelectionChanged += (_, _) => RefreshFilteredList();
        list.SelectionChanged += (_, _) => RefreshDetails();
        RefreshFilteredList();

        Window dialog = new()
        {
            Title = "Review Terrain Edits",
            Width = 800,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainEditReviewDialogContent(dialog, BuildTerrainExportReviewSummary(), filterBox, list, details);
        TerrainEditReviewItem? selected = await dialog.ShowDialog<TerrainEditReviewItem?>(this);
        if (selected != null)
            SelectTerrainEditReviewItem(selected);
    }

    private IReadOnlyList<TerrainEditReviewItem> BuildTerrainEditReviewItems()
    {
        if (_currentGeometry == null)
            return Array.Empty<TerrainEditReviewItem>();

        List<TerrainEditReviewItem> faceItems = _currentGeometry.Polygons
            .Select((polygon, index) => new { polygon, index })
            .Where(item => item.polygon.IsTerrainEdited)
            .Select(item =>
            {
                TerrainPatchSafetyKind safety = ClassifyTerrainPatchSafety(item.polygon);
                int customTextureImports = _customTerrainTextures.Count(texture => texture.TextureId == item.polygon.TextureId);
                int affectedTextureFaces = customTextureImports == 0
                    ? 0
                    : _currentGeometry.Polygons.Count(polygon => polygon.TextureId == item.polygon.TextureId && !polygon.IsTerrainRemoved);
                return new TerrainEditReviewItem
                {
                    TerrainIndex = item.index,
                    RuntimeKey = item.polygon.RuntimeKey,
                    Safety = safety,
                    SafetyLabel = FormatTerrainPatchSafetyKind(safety),
                    Surface = TerrainMaterialClassifier.FormatSurface(item.polygon.Surface),
                    TextureId = item.polygon.TextureId,
                    OriginalTextureId = item.polygon.OriginalTextureId,
                    HasHeightEdit = item.polygon.HasHeightEdit,
                    HasPositionEdit = item.polygon.HasPositionEdit,
                    HasTextureEdit = item.polygon.HasTextureEdit,
                    HasStructureEdit = item.polygon.HasStructureEdit,
                    HasCustomTextureArt = customTextureImports > 0,
                    CustomTextureImportCount = customTextureImports,
                    AffectedFaceCount = affectedTextureFaces,
                    EditSummary = BuildTerrainEditReviewSummary(item.polygon),
                    ExportImpact = BuildSelectedTerrainExportImpactSummary(item.polygon),
                    HeightEdit = FormatTerrainGeometryEdit(item.polygon),
                    CollisionReadiness = FormatTerrainCollisionReadiness(item.polygon),
                    TextureChange = BuildTerrainTextureReviewLine(item.polygon),
                    TextureExport = BuildTerrainTextureExportForReview(item.polygon.TextureId),
                    ProofStatus = BuildSelectedTerrainProofStatus(item.polygon)
                };
            })
            .ToList();

        foreach (IGrouping<int, CustomTerrainTextureImport> group in _customTerrainTextures.GroupBy(texture => texture.TextureId))
        {
            if (faceItems.Any(item => item.TextureId == group.Key && item.HasCustomTextureArt))
                continue;

            List<(TerrainPolygon Polygon, int Index)> affected = _currentGeometry.Polygons
                .Select((polygon, index) => (polygon, index))
                .Where(item => item.polygon.TextureId == group.Key && !item.polygon.IsTerrainRemoved)
                .ToList();
            TerrainPolygon? representative = affected.FirstOrDefault().Polygon;
            int representativeIndex = affected.Count == 0 ? -1 : affected[0].Index;
            string surfaces = affected.Count == 0
                ? "Unknown"
                : string.Join(", ", affected
                    .Select(item => TerrainMaterialClassifier.FormatSurface(item.Polygon.Surface))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4));
            if (affected.Select(item => TerrainMaterialClassifier.FormatSurface(item.Polygon.Surface)).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 4)
                surfaces += ", ...";

            faceItems.Add(new TerrainEditReviewItem
            {
                TerrainIndex = representativeIndex,
                RuntimeKey = representative?.RuntimeKey ?? $"texture {group.Key}",
                Safety = TerrainPatchSafetyKind.TextureOnly,
                SafetyLabel = affected.Count == 0 ? "Unused texture art" : "Texture art",
                Surface = string.IsNullOrWhiteSpace(surfaces) ? "Unknown" : surfaces,
                TextureId = group.Key,
                OriginalTextureId = group.Key,
                HasCustomTextureArt = true,
                CustomTextureImportCount = group.Count(),
                IsTextureArtOnly = true,
                AffectedFaceCount = affected.Count,
                EditSummary = $"Custom palette/art staged for texture {group.Key}.",
                ExportImpact = affected.Count == 0
                    ? $"Create BIN will ignore texture {group.Key} because no current terrain face uses it."
                    : $"Create BIN will patch the texture-page art for every face using texture {group.Key}.",
                HeightEdit = "none",
                CollisionReadiness = "not a geometry edit",
                TextureChange = "texture ID unchanged",
                TextureExport = BuildTerrainTextureExportForReview(group.Key),
                ProofStatus = affected.Count == 0
                    ? "unused staged art; select a face and paste/swap this look before exporting"
                    : "art-only change; terrain behavior does not change unless the face texture ID is also swapped"
            });
        }

        return faceItems
            .OrderBy(item => TerrainPatchSafetySortRank(item.Safety))
            .ThenBy(item => item.IsTextureArtOnly ? 1 : 0)
            .ThenBy(item => item.TerrainIndex)
            .ThenBy(item => item.TextureId)
            .ToList();
    }

    private string BuildTerrainEditReviewSummary(TerrainPolygon terrain)
    {
        List<string> parts = [];
        if (terrain.IsTerrainRemoved)
            parts.Add("remove this terrain face");
        else if (terrain.IsTerrainAddClone)
            parts.Add("add a copied terrain face");

        if (terrain.HasHeightEdit || terrain.HasPositionEdit)
            parts.Add($"move terrain points: {FormatTerrainGeometryEdit(terrain)}");

        if (terrain.HasTextureEdit)
            parts.Add($"swap texture {terrain.OriginalTextureId} to {terrain.TextureId}");

        int textureImports = _customTerrainTextures.Count(texture => texture.TextureId == terrain.TextureId);
        if (textureImports > 0)
            parts.Add($"{textureImports} custom palette/art import(s) for texture {terrain.TextureId}");

        return parts.Count == 0
            ? "no face-level terrain edit"
            : string.Join("; ", parts);
    }

    private static string BuildTerrainTextureReviewLine(TerrainPolygon terrain)
    {
        return terrain.HasTextureEdit
            ? $"{terrain.OriginalTextureId} -> {terrain.TextureId}"
            : $"{terrain.TextureId} unchanged";
    }

    private static bool MatchesTerrainEditReviewFilter(TerrainEditReviewItem item, int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => item.Safety is TerrainPatchSafetyKind.Structural or TerrainPatchSafetyKind.PartialHeight or TerrainPatchSafetyKind.VisualOnlyHeight or TerrainPatchSafetyKind.UnknownHeight,
            2 => item.Safety == TerrainPatchSafetyKind.PlayableHeight,
            3 => item.HasStructureEdit,
            4 => item.HasTextureEdit || item.HasCustomTextureArt || item.IsTextureArtOnly,
            5 => item.Safety == TerrainPatchSafetyKind.TextureOnly,
            _ => true
        };
    }

    private static string BuildTerrainEditReviewNextStep(TerrainEditReviewItem item)
    {
        if (item.IsTextureArtOnly && item.AffectedFaceCount == 0)
            return "Clear Unused Art, or paste/swap this look onto a face before exporting.";
        if (item.HasStructureEdit)
            return "Run Preflight BIN and check the Face outcome row for visible mesh and collision add/remove readiness.";
        if (item.HasHeightEdit || item.HasPositionEdit)
            return "Run Preflight BIN and check whether both visible mesh and playable collision patch rows are ready.";
        if (item.HasTextureEdit || item.HasCustomTextureArt || item.IsTextureArtOnly)
            return "Run Preflight BIN and confirm texture-id or texture-page art rows are included before Create BIN.";
        return "No terrain patch is staged for this row.";
    }

    private Control BuildTerrainEditReviewDialogContent(Window dialog, string exportSummary, ComboBox filterBox, ListBox list, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = exportSummary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });
        Grid filters = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        TextBlock filterLabel = new()
        {
            Text = "Show",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
        AddGridControl(filters, filterLabel, 0, 0);
        AddGridControl(filters, filterBox, 1, 0);
        panel.Children.Add(filters);
        panel.Children.Add(list);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button close = NewButton("Close");
        Button jump = NewButton("Jump To Face");
        close.Click += (_, _) => dialog.Close(null);
        jump.Click += (_, _) => dialog.Close(list.SelectedItem as TerrainEditReviewItem);
        buttons.Children.Add(close);
        buttons.Children.Add(jump);
        panel.Children.Add(buttons);
        return panel;
    }

    private void SelectTerrainEditReviewItem(TerrainEditReviewItem item)
    {
        if (item.IsTextureArtOnly)
        {
            if (_currentGeometry != null && item.TerrainIndex >= 0 && item.TerrainIndex < _currentGeometry.Polygons.Count)
            {
                _viewport.FocusTerrain(item.TerrainIndex);
                _statusText.Text = $"Reviewing custom terrain art for texture {item.TextureId}: affects {item.AffectedFaceCount} face(s).";
            }
            else
            {
                _statusText.Text = $"Reviewing custom terrain art for texture {item.TextureId}: no current face uses that texture.";
            }
            return;
        }

        if (_currentGeometry == null || item.TerrainIndex < 0 || item.TerrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "Could not find that terrain edit anymore.";
            return;
        }

        _viewport.FocusTerrain(item.TerrainIndex);
        _statusText.Text = $"Reviewing terrain face {item.TerrainIndex}: {item.SafetyLabel}.";
    }

    private static string FormatTerrainPatchSafetyKind(TerrainPatchSafetyKind safety)
    {
        return safety switch
        {
            TerrainPatchSafetyKind.Structural => "Structural",
            TerrainPatchSafetyKind.PlayableHeight => "Playable geometry",
            TerrainPatchSafetyKind.PartialHeight => "Partial geometry",
            TerrainPatchSafetyKind.VisualOnlyHeight => "Visual-only geometry",
            TerrainPatchSafetyKind.UnknownHeight => "Unknown geometry",
            TerrainPatchSafetyKind.TextureOnly => "Texture only",
            _ => "No edit"
        };
    }

    private static int TerrainPatchSafetySortRank(TerrainPatchSafetyKind safety)
    {
        return safety switch
        {
            TerrainPatchSafetyKind.Structural => 0,
            TerrainPatchSafetyKind.VisualOnlyHeight => 0,
            TerrainPatchSafetyKind.PartialHeight => 1,
            TerrainPatchSafetyKind.UnknownHeight => 2,
            TerrainPatchSafetyKind.PlayableHeight => 3,
            TerrainPatchSafetyKind.TextureOnly => 4,
            _ => 5
        };
    }

    private bool IsRiskyTerrainGeometryEdit(TerrainPolygon terrain)
    {
        if (!terrain.HasHeightEdit && !terrain.HasPositionEdit)
            return false;

        if (_terrainCollisionTriangleKeys.Count == 0 || _terrainCollisionMatchedFaces == 0)
            return true;

        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        return coverage.MatchedTriangles <= 0 || coverage.MatchedTriangles < coverage.TotalTriangles;
    }

    private TerrainPatchSafetyKind ClassifyTerrainPatchSafety(TerrainPolygon terrain)
    {
        if (!terrain.IsTerrainEdited)
            return TerrainPatchSafetyKind.None;
        if (terrain.HasStructureEdit)
            return TerrainPatchSafetyKind.Structural;
        if (!terrain.HasHeightEdit && !terrain.HasPositionEdit)
            return TerrainPatchSafetyKind.TextureOnly;
        if (_terrainCollisionTriangleKeys.Count == 0 || _terrainCollisionMatchedFaces == 0)
            return TerrainPatchSafetyKind.UnknownHeight;

        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        if (coverage.MatchedTriangles == 0)
            return TerrainPatchSafetyKind.VisualOnlyHeight;
        if (coverage.MatchedTriangles < coverage.TotalTriangles)
            return TerrainPatchSafetyKind.PartialHeight;
        return TerrainPatchSafetyKind.PlayableHeight;
    }

    private Control BuildAdvancedMovementControls()
    {
        StackPanel panel = new() { Spacing = 8 };
        Grid positionGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        positionGrid.Children.Add(NewButton("X-", () => NudgeSelectedMoby(-64, 0, 0)));
        AddGridButton(positionGrid, NewButton("Y-", () => NudgeSelectedMoby(0, -64, 0)), 1, 0);
        AddGridButton(positionGrid, NewButton("Z-", () => NudgeSelectedMoby(0, 0, -32)), 2, 0);
        AddGridButton(positionGrid, NewButton("X+", () => NudgeSelectedMoby(64, 0, 0)), 0, 1);
        AddGridButton(positionGrid, NewButton("Y+", () => NudgeSelectedMoby(0, 64, 0)), 1, 1);
        AddGridButton(positionGrid, NewButton("Z+", () => NudgeSelectedMoby(0, 0, 32)), 2, 1);
        panel.Children.Add(positionGrid);
        panel.Children.Add(new TextBlock
        {
            Text = "Use these only when you need exact coordinate nudges. Dragging objects on the map is the friendlier path.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });

        return new Expander
        {
            Header = "Advanced movement",
            IsExpanded = false,
            Content = panel
        };
    }

    private static void AddGridButton(Grid grid, Button button, int column, int row)
    {
        AddGridControl(grid, button, column, row);
    }

    private static void AddGridControl(Grid grid, Control control, int column, int row)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private static void AddLabeledField(Grid grid, string label, Control control, int row)
    {
        TextBlock text = new()
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
        AddGridControl(grid, text, 0, row);
        AddGridControl(grid, control, 1, row);
    }

    private static void AddLabeledFieldWithHelp(Grid grid, string label, Control control, int row, Func<Task> onHelp)
    {
        AddGridControl(grid, FieldLabelWithHelp(label, onHelp), 0, row);
        AddGridControl(grid, control, 1, row);
    }

    private static Control SectionLabelWithHelp(string label, Func<Task> onHelp)
    {
        return FieldLabelWithHelp(label, onHelp);
    }

    private static Control FieldLabelWithHelp(string label, Func<Task> onHelp)
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontWeight = FontWeight.SemiBold
        });
        Button help = new()
        {
            Content = "?",
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            MinHeight = 24,
            Background = new SolidColorBrush(Color.FromRgb(225, 232, 239)),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        };
        help.Click += async (_, _) =>
        {
            try
            {
                await onHelp();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        };
        panel.Children.Add(help);
        return panel;
    }

    private static Control ScrollableDialogContent(Control content)
    {
        return new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private void NudgeSelectedMoby(float dx, float dy, float dz)
    {
        if (_selectedMoby == null)
        {
            _statusText.Text = "Select an object before using XYZ nudges.";
            return;
        }

        List<Moby> moved = GetLinkedMoveMobys(_selectedMoby).ToList();
        bool snapToTerrain = Math.Abs(dz) <= 0.001f && (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f);
        int snapped = 0;
        foreach (Moby moby in moved)
        {
            if (MoveMoby(moby, dx, dy, dz, snapToTerrain))
                snapped++;
        }

        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(_selectedMoby));
        RefreshMobyList(_selectedMoby);
        string snapText = snapped > 0 ? $" Snapped {snapped} object(s) to terrain Z." : "";
        _statusText.Text = moved.Count > 1
            ? $"Moved {_selectedMoby.DisplayLabel} with {moved.Count - 1} linked object(s).{snapText}"
            : $"Moved {_selectedMoby.DisplayLabel}.{snapText}";
    }

    private void MoveMobyFromViewport(MobyMoveRequestedEventArgs e)
    {
        List<Moby> moved = GetLinkedMoveMobys(e.Moby).ToList();
        bool snapToTerrain = Math.Abs(e.Dz) <= 0.001f && (Math.Abs(e.Dx) > 0.001f || Math.Abs(e.Dy) > 0.001f);
        int snapped = 0;
        foreach (Moby moby in moved)
        {
            if (MoveMoby(moby, e.Dx, e.Dy, e.Dz, snapToTerrain))
                snapped++;
        }

        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(e.Moby));
        RefreshMobyList(e.Moby);
        string snapText = snapped > 0 ? $" Snapped {snapped} object(s) to terrain Z." : "";
        _statusText.Text = moved.Count > 1
            ? $"Moved {e.Moby.DisplayLabel} with {moved.Count - 1} linked object(s).{snapText}"
            : $"Moved {e.Moby.DisplayLabel}.{snapText}";
    }

    private void ApplyViewportTerrainBrush(ViewportTerrainBrushRequestedEventArgs e)
    {
        _selectedTerrain = e.Terrain;
        _selectedTerrainIndex = e.TerrainIndex;
        if (e.IsStrokeStart)
            BeginTerrainBrushUndo(TerrainBrushActionStrokeVerb(e.Action));

        float sampleStrength = (float)Math.Clamp(e.StrengthScale, 0.05, 1.0);
        switch (e.Action)
        {
            case TerrainBrushAction.Raise:
                _activeFlattenTerrainZ = null;
                SculptTerrainAt(e.BrushCenter, 1, 0.32f * sampleStrength);
                break;
            case TerrainBrushAction.Lower:
                _activeFlattenTerrainZ = null;
                SculptTerrainAt(e.BrushCenter, -1, 0.32f * sampleStrength);
                break;
            case TerrainBrushAction.Blend:
                _activeFlattenTerrainZ = null;
                SmoothTerrainAt(e.BrushCenter, 0.62f * sampleStrength);
                break;
            case TerrainBrushAction.Flatten:
                if (e.IsStrokeStart || _activeFlattenTerrainZ == null)
                    _activeFlattenTerrainZ = TerrainZAt(e.Terrain, e.BrushCenter);
                FlattenTerrainAt(e.BrushCenter, _activeFlattenTerrainZ.Value, 0.65f * sampleStrength);
                break;
            case TerrainBrushAction.Restore:
                _activeFlattenTerrainZ = null;
                RestoreTerrainAt(e.BrushCenter, 0.75f * sampleStrength);
                break;
        }
    }

    private void RunTerrainBrushCommand(Action action)
    {
        BeginTerrainBrushUndo();
        action();
        FinishTerrainBrushUndo();
    }

    private void SculptSelectedTerrain(int direction)
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select terrain before using the smooth brush.";
            return;
        }

        SculptTerrainAt(_selectedTerrain.Center, direction, 1f);
    }

    private void SculptTerrainAt(Vector2f center, int direction, float strengthScale)
    {
        float radius = (float)_terrainBrushRadiusSlider.Value;
        float strength = (float)_terrainBrushStrengthSlider.Value * Math.Sign(direction) * Math.Clamp(strengthScale, 0.05f, 1f);
        TerrainBrushResult result = ApplyTerrainBrush(center, radius, (polygon, vertexIndex, falloff) =>
        {
            float currentDelta = polygon.ZValues[vertexIndex] - polygon.OriginalZValues[vertexIndex];
            return currentDelta + (strength * falloff);
        });

        RefreshAfterTerrainBrush(result, strength >= 0 ? "Raised" : "Lowered");
    }

    private void SmoothSelectedTerrain()
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select terrain before using Blend.";
            return;
        }

        SmoothTerrainAt(_selectedTerrain.Center, 1f);
    }

    private void FlattenSelectedTerrain()
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select terrain before using Level.";
            return;
        }

        float targetZ = TerrainZAt(_selectedTerrain, _selectedTerrain.Center);
        FlattenTerrainAt(_selectedTerrain.Center, targetZ, 1f);
    }

    private void RestoreSelectedTerrain()
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select terrain before using Restore Smooth.";
            return;
        }

        RestoreTerrainAt(_selectedTerrain.Center, 1f);
    }

    private void SmoothTerrainAt(Vector2f center, float strengthScale)
    {
        float radius = (float)_terrainBrushRadiusSlider.Value;
        Dictionary<TerrainPolygon, int> faceIndexByPolygon = BuildTerrainFaceIndexLookup();
        Dictionary<(int FaceIndex, int VertexIndex), float> localTargets = BuildTerrainBrushLocalSmoothTargets(center, radius, faceIndexByPolygon);
        float fallbackTargetZ = WeightedTerrainZAt(center, radius);
        float blendStrength = Math.Clamp((float)_terrainBrushStrengthSlider.Value / 256f, 0.05f, 1f) * Math.Clamp(strengthScale, 0.05f, 1f);
        TerrainBrushResult result = ApplyTerrainBrush(center, radius, (polygon, vertexIndex, falloff) =>
        {
            float currentZ = polygon.ZValues[vertexIndex];
            float targetZ = faceIndexByPolygon.TryGetValue(polygon, out int faceIndex) &&
                localTargets.TryGetValue((faceIndex, vertexIndex), out float localTargetZ)
                    ? localTargetZ
                    : fallbackTargetZ;
            float blendedZ = currentZ + ((targetZ - currentZ) * falloff * blendStrength);
            return blendedZ - polygon.OriginalZValues[vertexIndex];
        });

        RefreshAfterTerrainBrush(result, "Blended");
    }

    private void FlattenTerrainAt(Vector2f center, float targetZ, float strengthScale)
    {
        float radius = (float)_terrainBrushRadiusSlider.Value;
        float blendStrength = Math.Clamp((float)_terrainBrushStrengthSlider.Value / 256f, 0.05f, 1f) * Math.Clamp(strengthScale, 0.05f, 1f);
        TerrainBrushResult result = ApplyTerrainBrush(center, radius, (polygon, vertexIndex, falloff) =>
        {
            float currentZ = polygon.ZValues[vertexIndex];
            float flattenedZ = currentZ + ((targetZ - currentZ) * falloff * blendStrength);
            return flattenedZ - polygon.OriginalZValues[vertexIndex];
        });

        RefreshAfterTerrainBrush(result, "Leveled");
    }

    private void RestoreTerrainAt(Vector2f center, float strengthScale)
    {
        float radius = (float)_terrainBrushRadiusSlider.Value;
        float restoreStrength = Math.Clamp((float)_terrainBrushStrengthSlider.Value / 256f, 0.05f, 1f) * Math.Clamp(strengthScale, 0.05f, 1f);
        TerrainBrushResult result = ApplyTerrainBrush(center, radius, (polygon, vertexIndex, falloff) =>
        {
            float currentDelta = polygon.ZValues[vertexIndex] - polygon.OriginalZValues[vertexIndex];
            float restoredDelta = currentDelta - (currentDelta * falloff * restoreStrength);
            return Math.Abs(restoredDelta) <= 0.001f ? 0 : restoredDelta;
        });

        RefreshAfterTerrainBrush(result, "Restored");
    }


    private static float TerrainZAt(TerrainPolygon terrain, Vector2f point)
    {
        return terrain.TryGetZ(point.X, point.Y, out float z) ? z : terrain.AvgZ;
    }

    private void UndoTerrainBrushArea()
    {
        if (_selectedTerrain == null || _currentGeometry == null)
        {
            _statusText.Text = "Select terrain before undoing a brush area.";
            return;
        }

        float radius = (float)_terrainBrushRadiusSlider.Value;
        float safeRadius = Math.Max(1, radius);
        int affectedFaces = 0;
        bool clearedUndoHistory = false;
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved)
                continue;
            if (!polygon.HasHeightEdit || !TouchesTerrainBrush(polygon, _selectedTerrain.Center, safeRadius))
                continue;

            if (!clearedUndoHistory)
            {
                ClearTerrainBrushUndoHistory();
                clearedUndoHistory = true;
            }
            polygon.ResetTerrainHeightEdit();
            affectedFaces++;
        }

        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        _lastTerrainBrushSummary = affectedFaces == 0
            ? "There were no height edits inside the brush area."
            : $"Undid height edits on {affectedFaces} terrain face(s) inside the brush area.";
        RefreshTerrainBrushHistoryText();
        _statusText.Text = _lastTerrainBrushSummary;
    }

    private void UndoLastTerrainBrushStroke()
    {
        if (_terrainBrushUndoHistory.Count == 0)
        {
            _statusText.Text = "There is no terrain brush stroke to undo yet.";
            return;
        }

        IReadOnlyList<TerrainBrushStrokeFace> stroke = _terrainBrushUndoHistory[^1];
        _terrainBrushUndoHistory.RemoveAt(_terrainBrushUndoHistory.Count - 1);
        int restored = 0;
        foreach (TerrainBrushStrokeFace undo in stroke)
        {
            undo.Polygon.ApplyTerrainVertexDeltas(undo.BeforeDeltas);
            restored++;
        }

        _terrainBrushRedoHistory.Add(stroke);
        _activeTerrainBrushUndo = null;
        _viewport.InvalidateVisual();
        if (_selectedTerrain != null)
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        string remaining = _terrainBrushUndoHistory.Count > 0 ? $" {_terrainBrushUndoHistory.Count} more stroke undo(s) available." : "";
        _lastTerrainBrushSummary = $"Undid a terrain brush stroke across {restored} {Plural(restored, "terrain face", "terrain faces")}.{remaining}";
        RefreshTerrainBrushHistoryText();
        _statusText.Text = _lastTerrainBrushSummary;
    }

    private void RedoLastTerrainBrushStroke()
    {
        if (_terrainBrushRedoHistory.Count == 0)
        {
            _statusText.Text = "There is no terrain brush stroke to redo.";
            return;
        }

        IReadOnlyList<TerrainBrushStrokeFace> stroke = _terrainBrushRedoHistory[^1];
        _terrainBrushRedoHistory.RemoveAt(_terrainBrushRedoHistory.Count - 1);
        int restored = 0;
        foreach (TerrainBrushStrokeFace redo in stroke)
        {
            redo.Polygon.ApplyTerrainVertexDeltas(redo.AfterDeltas);
            restored++;
        }

        PushTerrainBrushUndoStroke(stroke);
        _activeTerrainBrushUndo = null;
        _viewport.InvalidateVisual();
        if (_selectedTerrain != null)
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        string remaining = _terrainBrushRedoHistory.Count > 0 ? $" {_terrainBrushRedoHistory.Count} more stroke redo(s) available." : "";
        _lastTerrainBrushSummary = $"Redid a terrain brush stroke across {restored} {Plural(restored, "terrain face", "terrain faces")}.{remaining}";
        RefreshTerrainBrushHistoryText();
        _statusText.Text = _lastTerrainBrushSummary;
    }

    private void NudgeSelectedTerrain(float dz)
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select a terrain face before using height nudges.";
            return;
        }

        if (_selectedTerrain.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before changing its height.";
            return;
        }

        ClearTerrainBrushUndoHistory();
        _selectedTerrain.ApplyTerrainDeltaZ(_selectedTerrain.TerrainEditDeltaZ + dz);
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainPointControls();
        _statusText.Text = $"{(dz >= 0 ? "Raised" : "Lowered")} terrain face {_selectedTerrainIndex} by Z {dz:+0;-0;0}. Save Terrain and Create BIN will include the moved vertices.";
    }

    private void NudgeSelectedTerrainPosition(float dx, float dy)
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select a terrain face before moving terrain.";
            return;
        }

        if (_selectedTerrain.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before moving it.";
            return;
        }

        int pointCount = Math.Min(_selectedTerrain.Points.Count, _selectedTerrain.OriginalPoints.Count);
        if (pointCount <= 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        TerrainCollisionCoverage coverage = IsPlayableTerrainOnlyEnabled()
            ? GetTerrainCollisionCoverage(_selectedTerrain)
            : TerrainCollisionCoverage.Empty;
        if (IsPlayableTerrainOnlyEnabled() && coverage.VertexIndexes.Count < pointCount)
        {
            _statusText.Text = "Only edit playable ground is on, and this face is not fully matched to playable collision. Turn that off to move the visible mesh only.";
            return;
        }

        ClearTerrainBrushUndoHistory();
        Vector2f[] deltas = _selectedTerrain.TerrainVertexXYDeltas().ToArray();
        for (int i = 0; i < deltas.Length; i++)
            deltas[i] = new Vector2f(deltas[i].X + dx, deltas[i].Y + dy);

        _selectedTerrain.ApplyTerrainVertexXYDeltas(deltas);
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainPointControls();
        _statusText.Text = $"Moved terrain face {_selectedTerrainIndex} by X {dx:+0;-0;0}, Y {dy:+0;-0;0}. Save Terrain and Create BIN will include the moved vertices.";
    }

    private void MoveTerrainFromViewport(ViewportTerrainMoveRequestedEventArgs e)
    {
        if (_currentGeometry == null || e.TerrainIndex < 0 || e.TerrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain face is no longer available in the loaded level.";
            return;
        }

        _selectedTerrain = e.Terrain;
        _selectedTerrainIndex = e.TerrainIndex;
        if (e.StageAddCopy && !StageTerrainAddCopyFromViewport(e.TerrainIndex, e.Terrain))
            return;

        if (Math.Abs(e.Dz) > 0.001f)
            NudgeSelectedTerrain(e.Dz);
        else
            NudgeSelectedTerrainPosition(e.Dx, e.Dy);
    }

    private bool StageTerrainAddCopyFromViewport(int terrainIndex, TerrainPolygon terrain)
    {
        if (terrain.IsTerrainAddClone)
            return true;

        if (terrain.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before Option-dragging it into an added copy.";
            return false;
        }

        if (!CanStageTerrainAddCopy(terrain))
        {
            _statusText.Text = $"That face is not a safe Add Terrain Copy source yet. {BuildTerrainAddCopyUnavailableHint()} {BuildTerrainAddCopyFaceReadinessText(terrain)}";
            return false;
        }

        int pointCount = Math.Min(terrain.Points.Count, terrain.OriginalPoints.Count);
        if (pointCount <= 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points for add-copy.";
            return false;
        }

        ClearTerrainBrushUndoHistory();
        terrain.StageTerrainAddClone();
        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        _statusText.Text = $"Staged terrain face {terrainIndex} as an added copy. Keep dragging to place the copied terrain; the original source stays visible as a ghost.";
        return true;
    }

    private void CopySelectedTerrainLook()
    {
        if (_selectedTerrain == null || _selectedTerrainIndex < 0)
        {
            _statusText.Text = "Select a terrain face before copying its look.";
            return;
        }

        CopyTerrainLook(_selectedTerrainIndex, _selectedTerrain);
    }

    private async Task PasteSelectedTerrainLookAsync()
    {
        if (_selectedTerrain == null || _selectedTerrainIndex < 0)
        {
            _statusText.Text = "Select a terrain face before pasting a copied look.";
            return;
        }

        await PasteTerrainLookAsync(_selectedTerrainIndex, _selectedTerrain);
    }

    private void CopyTerrainLook(int terrainIndex, TerrainPolygon terrain)
    {
        if (_currentGeometry == null || terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain face is no longer available in the loaded level.";
            return;
        }

        if (terrain.TextureId < 0)
        {
            _statusText.Text = "Selected terrain does not expose a texture id to copy.";
            return;
        }

        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(terrain.Surface);
        string source = $"{(_currentLevel?.DisplayName ?? "current level")} face {terrainIndex}";
        IReadOnlyList<CustomTerrainTextureImport> customTextures = _customTerrainTextures
            .Where(texture => texture.TextureId == terrain.TextureId)
            .ToArray();
        _terrainLookClipboard = new TerrainLookClipboard(
            terrain.TextureId,
            surface,
            terrain.SurfaceColor,
            terrain.RuntimeKey,
            source,
            customTextures);
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        string customNote = customTextures.Count > 0
            ? $", {customTextures.Count} custom palette/art import(s)"
            : "";
        _statusText.Text = $"Copied terrain look from face {terrainIndex}: texture {terrain.TextureId}, {TerrainMaterialClassifier.FormatSurface(surface)}{customNote}. Select another face and press V or Paste Face Look.";
    }

    private async Task PasteTerrainLookAsync(int terrainIndex, TerrainPolygon terrain)
    {
        if (_terrainLookClipboard == null)
        {
            _statusText.Text = "Copy a terrain face look first.";
            return;
        }

        if (_currentGeometry == null || terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain face is no longer available in the loaded level.";
            return;
        }

        if (terrain.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before pasting a look onto it.";
            return;
        }

        TerrainLookClipboard look = _terrainLookClipboard;
        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        ClearTerrainBrushUndoHistory();
        if (look.CustomTextures.Count == 0)
        {
            terrain.ApplyTextureOverride(look.TextureId);
            terrain.SetSurface(look.Surface, look.SurfaceColor, $"copied look from {look.SourceLabel}");
            _viewport.InvalidateVisual();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            RefreshTerrainSurfaceQuickState();
            _statusText.Text = $"Pasted {TerrainMaterialClassifier.FormatSurface(look.Surface)} look from {look.SourceLabel} onto face {terrainIndex}. Save Terrain and Create BIN will include the texture swap.";
            return;
        }

        TerrainFaceLocalTextureResult? local = await EnsureSelectedTerrainFaceLocalTextureAsync($"paste the custom look from {look.SourceLabel}");
        if (local == null)
            return;

        foreach (CustomTerrainTextureImport texture in look.CustomTextures)
        {
            _customTerrainTextures = await CustomTerrainTextureStore.AddOrReplaceAsync(
                _workspace.RootPath,
                _currentLevel!.Key,
                _currentLevel.DisplayName,
                local.TextureId,
                texture.SourceImagePath,
                texture.SourceImageName,
                texture.DescriptorTier,
                texture.TileSize,
                texture.SourceKind,
                texture.PaletteName,
                texture.PaletteLowHex,
                texture.PaletteHighHex,
                texture.PaletteHexColors ?? Array.Empty<string>());
        }

        terrain.SetSurface(look.Surface, look.SurfaceColor, $"copied custom look from {look.SourceLabel}");
        int previewFaces = ApplyCustomTerrainTexturePreviews();
        int savedEdits = await PersistCurrentTerrainEditsAsync();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainSurfaceQuickState();
        _statusText.Text = $"Pasted custom {TerrainMaterialClassifier.FormatSurface(look.Surface)} look from {look.SourceLabel} onto face {terrainIndex} using local texture {local.TextureId}; copied {look.CustomTextures.Count} custom import(s), previewed {previewFaces} face(s), saved {savedEdits} terrain edit(s). Create BIN will include the face-local texture art.";
    }

    private void SelectTerrainPointOffset(int offset)
    {
        if (_selectedTerrain == null || _selectedTerrain.Points.Count == 0)
        {
            _statusText.Text = "Select terrain before choosing a single point.";
            return;
        }

        int count = Math.Min(_selectedTerrain.Points.Count, _selectedTerrain.ZValues.Length);
        if (count <= 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        _selectedTerrainPointIndex = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex + offset);
        RefreshTerrainPointControls();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        _statusText.Text = $"Selected terrain point {_selectedTerrainPointIndex + 1} of {count}.";
    }

    private void SelectNextPlayableTerrainPoint()
    {
        if (_selectedTerrain == null || _selectedTerrain.Points.Count == 0)
        {
            _statusText.Text = "Select terrain before choosing a playable point.";
            return;
        }

        if (_terrainCollisionTriangleKeys.Count == 0)
        {
            _statusText.Text = "Playable collision is not matched for this level yet, so the editor cannot identify playable terrain points.";
            return;
        }

        int count = EditableTerrainPointCount(_selectedTerrain);
        if (count <= 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        int[] playableIndexes = GetTerrainCollisionCoverage(_selectedTerrain).VertexIndexes
            .Where(index => index >= 0 && index < count)
            .OrderBy(index => index)
            .ToArray();
        if (playableIndexes.Length == 0)
        {
            _statusText.Text = "This face has no matched playable-collision points yet. You can still edit its visible mesh, but Spyro may keep using old collision.";
            return;
        }

        int current = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
        int next = playableIndexes.FirstOrDefault(index => index > current, playableIndexes[0]);
        _selectedTerrainPointIndex = next;
        RefreshTerrainPointControls();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain, _selectedTerrainPointIndex));
        _statusText.Text = $"Selected playable terrain point {_selectedTerrainPointIndex + 1} of {count}. Create BIN should patch this visible point and matched collision.";
    }

    private void NudgeSelectedTerrainPoint(float dz)
    {
        ApplySelectedTerrainPointEdit(currentDelta => currentDelta + dz, null, dz >= 0 ? "Raised" : "Lowered");
    }

    private void NudgeSelectedTerrainPointXY(float dx, float dy)
    {
        ApplySelectedTerrainPointEdit(null, currentDelta => new Vector2f(currentDelta.X + dx, currentDelta.Y + dy), "Moved");
    }

    private void MoveTerrainPointFromViewport(ViewportTerrainPointMoveRequestedEventArgs e)
    {
        if (_currentGeometry == null || e.TerrainIndex < 0 || e.TerrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain point is no longer available in the loaded level.";
            return;
        }

        _selectedTerrain = e.Terrain;
        _selectedTerrainIndex = e.TerrainIndex;
        _selectedTerrainPointIndex = NormalizeTerrainPointIndex(e.Terrain, e.PointIndex);
        if (Math.Abs(e.Dz) > 0.001f)
            ApplySelectedTerrainPointEdit(currentDelta => currentDelta + e.Dz, null, e.Dz >= 0 ? "Dragged up" : "Dragged down");
        else
            ApplySelectedTerrainPointEdit(null, currentDelta => new Vector2f(currentDelta.X + e.Dx, currentDelta.Y + e.Dy), "Dragged");
    }

    private void ResetSelectedTerrainPoint()
    {
        ApplySelectedTerrainPointEdit(_ => 0, _ => new Vector2f(0, 0), "Reset");
    }

    private async Task EditSelectedTerrainPointAsync()
    {
        if (_selectedTerrain == null || _currentGeometry == null)
        {
            _statusText.Text = "Select a terrain face before editing a single point.";
            return;
        }

        TerrainPolygon selected = _selectedTerrain;
        int pointIndex = NormalizeTerrainPointIndex(selected, _selectedTerrainPointIndex);
        if (pointIndex < 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        if (selected.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before changing its points.";
            return;
        }

        Vector2f point = selected.Points[pointIndex];
        Vector2f originalPoint = selected.OriginalPoints[pointIndex];
        float currentZ = selected.ZValues[pointIndex];
        float originalZ = selected.OriginalZValues[pointIndex];
        TextBox xOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(point.X - originalPoint.X),
            MinWidth = 140
        };
        TextBox yOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(point.Y - originalPoint.Y),
            MinWidth = 140
        };
        TextBox zOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(currentZ - originalZ),
            MinWidth = 140
        };
        TextBlock details = new()
        {
            Text = BuildTerrainPointEditDetailsText(selected, pointIndex),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Edit Terrain Point",
            Width = 520,
            Height = 460,
            MinWidth = 440,
            MinHeight = 390,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainPointEditDialogContent(dialog, xOffsetBox, yOffsetBox, zOffsetBox, details));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        bool hasX = TryParseFloat(xOffsetBox.Text, out float nextXOffset);
        bool hasY = TryParseFloat(yOffsetBox.Text, out float nextYOffset);
        bool hasZ = TryParseFloat(zOffsetBox.Text, out float nextZOffset);
        if (!hasX && !hasY && !hasZ)
        {
            _statusText.Text = "Type at least one numeric point offset before applying.";
            return;
        }

        ApplySelectedTerrainPointEdit(
            hasZ ? _ => nextZOffset : null,
            hasX || hasY
                ? current => new Vector2f(hasX ? nextXOffset : current.X, hasY ? nextYOffset : current.Y)
                : null,
            "Set");
    }

    private string BuildTerrainPointEditDetailsText(TerrainPolygon terrain, int pointIndex)
    {
        Vector2f point = terrain.Points[pointIndex];
        Vector2f originalPoint = terrain.OriginalPoints[pointIndex];
        float currentZ = terrain.ZValues[pointIndex];
        float originalZ = terrain.OriginalZValues[pointIndex];
        string seam = IsTerrainSeamJoiningEnabled()
            ? "Shared seam points with the same original X/Y/Z are updated together."
            : "Seam joining is off, so only this face corner is updated.";
        string playable = IsPlayableTerrainOnlyEnabled()
            ? GetTerrainCollisionCoverage(terrain).VertexIndexes.Contains(pointIndex)
                ? "Only edit playable ground is on, and this point has matched collision."
                : "Only edit playable ground is on, and this point is visual-only; applying will be blocked unless you turn that off."
            : "Only edit playable ground is off, so visual mesh points can be edited even without matched collision.";

        return string.Join("\n",
        [
            $"Face {_selectedTerrainIndex}, point {pointIndex + 1}/{Math.Min(terrain.Points.Count, terrain.ZValues.Length)}.",
            $"Original: X {originalPoint.X:0.###}, Y {originalPoint.Y:0.###}, Z {originalZ:0.###}.",
            $"Current: X {point.X:0.###}, Y {point.Y:0.###}, Z {currentZ:0.###}.",
            seam,
            playable
        ]);
    }

    private static string FormatTerrainPointOffset(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private Control BuildTerrainPointEditDialogContent(Window dialog, TextBox xOffsetBox, TextBox yOffsetBox, TextBox zOffsetBox, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(128)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "X offset", xOffsetBox, 0);
        AddLabeledField(fields, "Y offset", yOffsetBox, 1);
        AddLabeledField(fields, "Z offset", zOffsetBox, 2);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(NewSmallNote("Offsets are measured from the point's original position. Leave a field blank to keep that axis unchanged."));
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);

        return panel;
    }

    private void ApplySelectedTerrainPointEdit(Func<float, float>? nextDeltaZ, Func<Vector2f, Vector2f>? nextDeltaXY, string verb)
    {
        if (_selectedTerrain == null || _currentGeometry == null)
        {
            _statusText.Text = "Select a terrain face before editing a single point.";
            return;
        }

        TerrainPolygon selected = _selectedTerrain;
        int pointIndex = NormalizeTerrainPointIndex(selected, _selectedTerrainPointIndex);
        if (pointIndex < 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        if (selected.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before changing its points.";
            return;
        }

        bool editsXY = nextDeltaXY != null;
        bool playableOnly = IsPlayableTerrainOnlyEnabled();
        TerrainCollisionCoverage selectedCoverage = playableOnly
            ? GetTerrainCollisionCoverage(selected)
            : TerrainCollisionCoverage.Empty;
        if (playableOnly && !selectedCoverage.VertexIndexes.Contains(pointIndex))
        {
            _statusText.Text = "Only edit playable ground is on, and this point is not matched to playable collision. Turn that off to edit the visible mesh only.";
            return;
        }

        string selectedKey = TerrainVertexKey(selected, pointIndex);
        List<(TerrainPolygon Polygon, int PointIndex)> targets = new();
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved)
                continue;
            if (!ReferenceEquals(polygon, selected) && !IsTerrainSeamJoiningEnabled())
                continue;

            TerrainCollisionCoverage coverage = playableOnly
                ? GetTerrainCollisionCoverage(polygon)
                : TerrainCollisionCoverage.Empty;
            int count = Math.Min(Math.Min(polygon.Points.Count, polygon.ZValues.Length), polygon.OriginalZValues.Length);
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(polygon, selected) && !string.Equals(TerrainVertexKey(polygon, i), selectedKey, StringComparison.Ordinal))
                    continue;
                if (ReferenceEquals(polygon, selected) && i != pointIndex)
                    continue;
                if (playableOnly && !coverage.VertexIndexes.Contains(i))
                    continue;

                targets.Add((polygon, i));
            }
        }

        if (targets.Count == 0)
        {
            _statusText.Text = "No editable terrain points matched that selection.";
            return;
        }

        if (editsXY)
            ClearTerrainBrushUndoHistory();
        else
            BeginTerrainBrushUndo("Single point terrain");
        int changed = 0;
        foreach (IGrouping<TerrainPolygon, (TerrainPolygon Polygon, int PointIndex)> group in targets.GroupBy(target => target.Polygon))
        {
            TerrainPolygon polygon = group.Key;
            float[] originalDeltas = polygon.TerrainVertexDeltas().ToArray();
            float[] deltas = originalDeltas.ToArray();
            Vector2f[] originalXYDeltas = polygon.TerrainVertexXYDeltas().ToArray();
            Vector2f[] xyDeltas = originalXYDeltas.ToArray();
            bool changedPolygon = false;
            foreach ((_, int index) in group)
            {
                if (nextDeltaZ != null)
                {
                    float updated = nextDeltaZ(deltas[index]);
                    if (Math.Abs(updated - deltas[index]) > 0.001f)
                    {
                        deltas[index] = updated;
                        changed++;
                        changedPolygon = true;
                    }
                }

                if (nextDeltaXY != null)
                {
                    Vector2f updated = nextDeltaXY(xyDeltas[index]);
                    if (Math.Abs(updated.X - xyDeltas[index].X) > 0.001f ||
                        Math.Abs(updated.Y - xyDeltas[index].Y) > 0.001f)
                    {
                        xyDeltas[index] = updated;
                        changed++;
                        changedPolygon = true;
                    }
                }
            }

            if (!changedPolygon)
                continue;

            if (!editsXY)
                RecordTerrainBrushUndo(polygon, originalDeltas);
            polygon.ApplyTerrainVertexDeltas(deltas);
            polygon.ApplyTerrainVertexXYDeltas(xyDeltas);
        }
        if (!editsXY)
            FinishTerrainBrushUndo();

        _selectedTerrainPointIndex = pointIndex;
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainPointControls();
        string seamText = targets.Count > 1 && IsTerrainSeamJoiningEnabled()
            ? $" Kept {targets.Count - 1} shared point(s) joined."
            : "";
        string patchText = BuildSelectedTerrainPointPatchStatus(_selectedTerrain, _selectedTerrainPointIndex);
        _statusText.Text = changed == 0
            ? "That point was already at the requested position."
            : $"{verb} terrain point {_selectedTerrainPointIndex + 1} on face {_selectedTerrainIndex}.{seamText} {patchText}";
    }

    private void RefreshTerrainPointControls()
    {
        if (_selectedTerrain == null)
        {
            _terrainPointEditText.Text = "Select a terrain face to edit one point at a time.";
            _viewport.SetSelectedTerrainPoint(-1);
            return;
        }

        _selectedTerrainPointIndex = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
        _viewport.SetSelectedTerrainPoint(_selectedTerrainPointIndex);
        _terrainPointEditText.Text = BuildSelectedTerrainPointSummary();
    }

    private float SelectedTerrainPointDelta()
    {
        if (_selectedTerrain == null)
            return 0;

        int index = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
        return index < 0 ? 0 : _selectedTerrain.ZValues[index] - _selectedTerrain.OriginalZValues[index];
    }

    private bool SelectedTerrainPointHasEdit()
    {
        if (_selectedTerrain == null)
            return false;

        int index = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
        if (index < 0)
            return false;

        bool zEdited = Math.Abs(_selectedTerrain.ZValues[index] - _selectedTerrain.OriginalZValues[index]) > 0.001f;
        bool xyEdited = index < _selectedTerrain.OriginalPoints.Count &&
            (Math.Abs(_selectedTerrain.Points[index].X - _selectedTerrain.OriginalPoints[index].X) > 0.001f ||
             Math.Abs(_selectedTerrain.Points[index].Y - _selectedTerrain.OriginalPoints[index].Y) > 0.001f);
        return zEdited || xyEdited;
    }

    private static int NormalizeTerrainPointIndex(TerrainPolygon terrain, int index)
    {
        int count = EditableTerrainPointCount(terrain);
        if (count <= 0)
            return -1;
        if (index < 0)
            return count - 1;
        if (index >= count)
            return 0;
        return index;
    }

    private static int EditableTerrainPointCount(TerrainPolygon terrain)
    {
        return Math.Min(Math.Min(Math.Min(terrain.Points.Count, terrain.OriginalPoints.Count), terrain.ZValues.Length), terrain.OriginalZValues.Length);
    }

    private bool HasPlayableTerrainPoint(TerrainPolygon terrain)
    {
        if (_terrainCollisionTriangleKeys.Count == 0 || EditableTerrainPointCount(terrain) <= 0)
            return false;

        int count = EditableTerrainPointCount(terrain);
        return GetTerrainCollisionCoverage(terrain).VertexIndexes.Any(index => index >= 0 && index < count);
    }

    private string BuildSelectedTerrainPointSummary()
    {
        if (_selectedTerrain == null)
            return "No terrain point selected.";

        int index = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
        if (index < 0)
            return "This terrain face has no editable points.";

        Vector2f point = _selectedTerrain.Points[index];
        Vector2f originalPoint = _selectedTerrain.OriginalPoints[index];
        float originalZ = _selectedTerrain.OriginalZValues[index];
        float currentZ = _selectedTerrain.ZValues[index];
        float deltaX = point.X - originalPoint.X;
        float deltaY = point.Y - originalPoint.Y;
        float deltaZ = currentZ - originalZ;
        string playable = _terrainCollisionTriangleKeys.Count == 0
            ? "collision unknown"
            : GetTerrainCollisionCoverage(_selectedTerrain).VertexIndexes.Contains(index)
                ? "playable collision matched"
                : "visual-only point";
        return $"Point {index + 1}/{Math.Min(_selectedTerrain.Points.Count, _selectedTerrain.ZValues.Length)}: X {point.X:0} ({deltaX:+0;-0;0}), Y {point.Y:0} ({deltaY:+0;-0;0}), Z {currentZ:0.0} ({deltaZ:+0.0;-0.0;0.0}). {playable}. {BuildSelectedTerrainPointPatchStatus(_selectedTerrain, index)}";
    }

    private string BuildSelectedTerrainPointPatchStatus(TerrainPolygon terrain, int pointIndex)
    {
        TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(terrain);
        bool hasCollision = coverage.VertexIndexes.Contains(pointIndex);
        if (_terrainCollisionTriangleKeys.Count == 0)
            return "Create BIN can patch the visible vertex, but playable collision is not matched for this level yet.";
        if (hasCollision)
            return "Create BIN should patch this visible vertex and matched playable collision triangle(s).";
        return "Create BIN may only move the visible mesh for this point; Spyro collision is not matched here.";
    }

    private static string TerrainVertexKey(TerrainPolygon polygon, int index)
    {
        return VisualPointKey(polygon.OriginalPoints[index].X, polygon.OriginalPoints[index].Y, polygon.OriginalZValues[index]);
    }

    private async Task StageRemoveSelectedTerrainAsync()
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select a terrain face before staging removal.";
            return;
        }

        if (_selectedTerrain.IsTerrainRemoved)
        {
            _statusText.Text = "That terrain face is already staged for removal.";
            return;
        }

        if (!CanUseTerrainRemoveInCurrentLevel())
        {
            _statusText.Text = BuildTerrainRemoveUnavailableHint();
            return;
        }

        TerrainPolygon terrain = _selectedTerrain;
        if (!CanStageTerrainRemoval(terrain))
        {
            _statusText.Text = BuildTerrainRemoveGameplayBlockedHint(terrain);
            return;
        }

        TextBlock details = new()
        {
            Text = BuildTerrainRemoveDetailsText(terrain),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        };
        Window dialog = new()
        {
            Title = "Remove Terrain Face",
            Width = 540,
            Height = 420,
            MinWidth = 440,
            MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainRemoveDialogContent(dialog, details);
        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        StageTerrainRemoval(_selectedTerrainIndex, terrain, "Staged");
    }

    private void StageTerrainRemovalFromViewport(int terrainIndex, TerrainPolygon terrain)
    {
        if (_currentGeometry == null || terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain face is no longer available in the loaded level.";
            return;
        }

        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        StageTerrainRemoval(terrainIndex, terrain, "Deleted");
    }

    private void StageTerrainRemoval(int terrainIndex, TerrainPolygon terrain, string verb)
    {
        if (terrain.IsTerrainRemoved)
        {
            _statusText.Text = "That terrain face is already staged for removal.";
            return;
        }

        if (!CanUseTerrainRemoveInCurrentLevel())
        {
            _statusText.Text = BuildTerrainRemoveUnavailableHint();
            return;
        }

        if (!CanStageTerrainRemoval(terrain))
        {
            _statusText.Text = BuildTerrainRemoveGameplayBlockedHint(terrain);
            return;
        }

        ClearTerrainBrushUndoHistory();
        terrain.StageTerrainRemoval();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainSurfaceQuickState();
        _statusText.Text = $"{verb} terrain face {terrainIndex} for removal. {BuildTerrainRemoveFaceReadinessText(terrain)}. Save Terrain records the edit; Create BIN removes the visible face and matched gameplay collision. Use Undo Terrain to restore it.";
    }

    private string BuildTerrainRemoveDetailsText(TerrainPolygon terrain)
    {
        string collision = FormatTerrainCollisionReadiness(terrain);
        TerrainRemoveFaceReadiness readiness = GetTerrainRemoveFaceReadiness(terrain);
        string export = CanRemoveTerrainForGameplay(readiness)
            ? "Create BIN can neutralize the visible face and the matched playable collision triangle(s)."
            : $"Remove Terrain will stay disabled until this is gameplay-safe. {BuildTerrainRemoveGameplayBlockedHint(terrain)}";
        string existingEdits = terrain.IsTerrainEdited
            ? $"Existing staged edits on this face will be included with the remove-face edit. Current readiness: {FormatTerrainPatchReadiness(terrain)}."
            : "This face has no other staged terrain edits.";

        return string.Join("\n",
        [
            $"Face {_selectedTerrainIndex}: {terrain.RuntimeKey}",
            $"Texture {terrain.TextureId}; material {TerrainMaterialClassifier.FormatSurface(terrain.Surface)}.",
            $"Shape: {terrain.Points.Count} point(s), average Z {terrain.AvgZ:0.###}.",
            $"Playable collision: {collision}.",
            $"Remove readiness: {BuildTerrainRemoveFaceReadinessText(terrain)}",
            existingEdits,
            BuildTerrainRemoveStructureDetailsText(terrain),
            export,
            "Use Undo Terrain if you stage this by mistake."
        ]);
    }

    private static Control BuildTerrainRemoveDialogContent(Window dialog, TextBlock details)
    {
        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Stage this terrain face for removal?",
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 37, 43))
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This does not alter the source image yet. The removal is saved as an editor terrain edit and applied when you create a patched BIN/CUE. The editor only allows this when both the visible face and gameplay collision are mapped.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 18
        });
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button remove = NewButton("Remove Face");
        cancel.Click += (_, _) => dialog.Close(false);
        remove.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(remove);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task StageAddCloneSelectedTerrainAsync()
    {
        if (!CanUseTerrainAddCopyInCurrentLevel())
        {
            _statusText.Text = BuildTerrainAddCopyUnavailableHint();
            return;
        }

        TerrainPolygon? terrain = _selectedTerrain;
        int terrainIndex = _selectedTerrainIndex;
        bool autoSelectedSource = false;
        string autoSelectedSourceReason = "";

        if (terrain == null || terrainIndex < 0 || terrain.IsTerrainRemoved || !CanStageTerrainAddCopy(terrain))
        {
            string selectedReason = terrain == null || terrainIndex < 0
                ? "No terrain face was selected."
                : terrain.IsTerrainRemoved
                    ? "The selected terrain face is staged for removal."
                    : "The selected terrain face is not a safe native add-copy source yet.";

            if (!TrySelectBestTerrainAddCopySource(out terrainIndex, out terrain, out autoSelectedSourceReason) || terrain == null)
            {
                _statusText.Text = $"{selectedReason} {BuildTerrainAddCopyUnavailableHint()}";
                return;
            }

            autoSelectedSource = true;
        }

        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        bool hasUniformX = TryGetUniformTerrainXYDelta(terrain, 'x', out float uniformX);
        bool hasUniformY = TryGetUniformTerrainXYDelta(terrain, 'y', out float uniformY);
        bool hasUniformZ = TryGetUniformTerrainZDelta(terrain, out float uniformZ);
        IReadOnlyList<TerrainCopyPlacementOption> placementOptions = BuildTerrainCopyPlacementOptions(terrain, hasUniformX, uniformX, hasUniformY, uniformY, hasUniformZ, uniformZ);
        TextBox xOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(placementOptions[0].XOffset),
            MinWidth = 140
        };
        TextBox yOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(placementOptions[0].YOffset),
            MinWidth = 140
        };
        TextBox zOffsetBox = new()
        {
            Text = FormatTerrainPointOffset(placementOptions[0].ZOffset),
            MinWidth = 140
        };
        TextBlock placementDescription = new()
        {
            Text = placementOptions[0].Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            LineHeight = 17
        };
        ComboBox placementBox = new()
        {
            ItemsSource = placementOptions,
            SelectedItem = placementOptions[0],
            MinWidth = 220
        };
        placementBox.SelectionChanged += (_, _) =>
        {
            if (placementBox.SelectedItem is not TerrainCopyPlacementOption option)
                return;

            xOffsetBox.Text = FormatTerrainPointOffset(option.XOffset);
            yOffsetBox.Text = FormatTerrainPointOffset(option.YOffset);
            zOffsetBox.Text = FormatTerrainPointOffset(option.ZOffset);
            placementDescription.Text = option.Description;
        };
        TextBox textureBox = new()
        {
            Text = terrain.TextureId >= 0 ? terrain.TextureId.ToString(CultureInfo.InvariantCulture) : "",
            MinWidth = 140
        };
        ComboBox surfaceBox = new()
        {
            ItemsSource = TerrainSurfaceChoice.Known,
            SelectedItem = TerrainSurfaceChoice.Find(terrain.Surface),
            MinWidth = 160
        };
        TextBlock details = new()
        {
            Text = autoSelectedSource
                ? $"Auto-selected source: {autoSelectedSourceReason}\n\n{BuildTerrainAddCopyDetailsText(terrain)}"
                : BuildTerrainAddCopyDetailsText(terrain),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Add Terrain Copy",
            Width = 620,
            Height = 640,
            MinWidth = 460,
            MinHeight = 440,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainAddCopyDialogContent(dialog, placementBox, placementDescription, xOffsetBox, yOffsetBox, zOffsetBox, textureBox, surfaceBox, details));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        bool hasXOffset = TryParseFloat(xOffsetBox.Text, out float nextXOffset);
        bool hasYOffset = TryParseFloat(yOffsetBox.Text, out float nextYOffset);
        bool hasZOffset = TryParseFloat(zOffsetBox.Text, out float nextZOffset);
        if (!hasXOffset && !hasYOffset && !terrain.HasPositionEdit)
        {
            nextXOffset = DefaultTerrainAddCopyOffset;
            hasXOffset = true;
        }

        ClearTerrainBrushUndoHistory();
        if (hasXOffset || hasYOffset)
        {
            Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
            if (deltas.Length == 0)
                deltas = terrain.OriginalPoints.Select(_ => new Vector2f(0, 0)).ToArray();
            for (int i = 0; i < deltas.Length; i++)
                deltas[i] = new Vector2f(hasXOffset ? nextXOffset : deltas[i].X, hasYOffset ? nextYOffset : deltas[i].Y);

            terrain.ApplyTerrainVertexXYDeltas(deltas);
        }

        if (hasZOffset)
            terrain.ApplyTerrainDeltaZ(nextZOffset);
        if (TryParseInt(textureBox.Text, out int textureId))
            terrain.ApplyTextureOverride(textureId);
        if (surfaceBox.SelectedItem is TerrainSurfaceChoice surfaceChoice)
            await ApplyTerrainSurfaceOverrideAsync(terrain.TextureId, surfaceChoice.Surface);

        terrain.StageTerrainAddClone();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainSurfaceQuickState();
        string xText = hasXOffset ? $"{nextXOffset:+0;-0;0}" : "kept";
        string yText = hasYOffset ? $"{nextYOffset:+0;-0;0}" : "kept";
        string zText = hasZOffset ? $"{nextZOffset:+0;-0;0}" : "kept";
        string selectedText = autoSelectedSource ? "Auto-selected source and added" : "Added";
        _statusText.Text = $"{selectedText} a copied terrain face from {terrainIndex}: X {xText}, Y {yText}, Z {zText}. Drag the highlighted copy to place it; Shift-drag changes height. Preflight BIN shows the face outcome before a patched BIN/CUE is created.";
    }

    private IReadOnlyList<TerrainCopyPlacementOption> BuildTerrainCopyPlacementOptions(
        TerrainPolygon terrain,
        bool hasUniformX,
        float uniformX,
        bool hasUniformY,
        float uniformY,
        bool hasUniformZ,
        float uniformZ)
    {
        Vector2f sideOffset = TerrainCopyPlacementSuggester.SuggestedBesideOffset(terrain, DefaultTerrainAddCopyOffset);
        float forwardOffset = SuggestedTerrainCopyForwardOffset(terrain);
        List<TerrainCopyPlacementOption> options =
        [
            new("Beside source", sideOffset.X, sideOffset.Y, 0, "Places the copied face beside the longest outside edge of the source, so it starts near the terrain you are extending."),
            new("Forward from source", 0, forwardOffset, 0, "Places the copied face forward on the map. Use this for extending paths or platforms."),
            new("Behind source", 0, -forwardOffset, 0, "Places the copied face behind the source face. Useful when extending terrain in the opposite direction."),
            new("Raised platform", sideOffset.X, sideOffset.Y, Math.Max(64, MathF.Round(Math.Max(terrain.MaxZ - terrain.MinZ, 0) + 96)), "Places a raised copy beside the source so height editing is immediately visible."),
            new("Custom offsets", hasUniformX ? uniformX : sideOffset.X, hasUniformY ? uniformY : sideOffset.Y, hasUniformZ ? uniformZ : 0, "Edit the X/Y/Z fields directly for an exact copied-face placement.")
        ];

        if (terrain.HasPositionEdit || terrain.HasHeightEdit)
        {
            options.Insert(0, new TerrainCopyPlacementOption(
                "Current edited placement",
                hasUniformX ? uniformX : sideOffset.X,
                hasUniformY ? uniformY : sideOffset.Y,
                hasUniformZ ? uniformZ : 0,
                "Keeps the currently staged terrain offsets and converts this source face into an added copy."));
        }

        return options;
    }

    private static float SuggestedTerrainCopyForwardOffset(TerrainPolygon terrain)
    {
        float size = Math.Max(terrain.Bounds.Height, terrain.Bounds.Width * 0.65f);
        if (size <= 0)
            size = DefaultTerrainAddCopyOffset;
        return Math.Clamp(MathF.Round(size + 96), DefaultTerrainAddCopyOffset, 768);
    }

    private string BuildTerrainAddCopyDetailsText(TerrainPolygon terrain)
    {
        string slack = terrain.SectorOffset >= 0
            ? "Create BIN will use sector slack when available, or report the add-copy as a guarded structural edit when it cannot fit cleanly."
            : "This face does not have a source-sector offset, so Create BIN may not be able to add it natively yet.";
        return string.Join("\n",
        [
            $"Source face: {_selectedTerrainIndex} ({terrain.RuntimeKey})",
            $"Current texture: {terrain.TextureId}; material: {TerrainMaterialClassifier.FormatSurface(terrain.Surface)}.",
            $"Shape: {terrain.Points.Count} point(s), average Z {terrain.AvgZ:0.###}.",
            "X/Y offsets move the copied face away from the source. Z offset raises or lowers the copied face.",
            "Texture ID changes the in-game visual palette/art used by the copied face.",
            BuildTerrainAddCopyFaceReadinessText(terrain),
            BuildTerrainAddCopyStructureDetailsText(terrain),
            slack
        ]);
    }

    private Control BuildTerrainAddCopyDialogContent(Window dialog, ComboBox placementBox, TextBlock placementDescription, TextBox xOffsetBox, TextBox yOffsetBox, TextBox zOffsetBox, TextBox textureBox, ComboBox surfaceBox, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(128)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Placement", placementBox, 0);
        Grid.SetColumn(placementDescription, 1);
        Grid.SetRow(placementDescription, 1);
        fields.Children.Add(placementDescription);
        AddLabeledField(fields, "X offset", xOffsetBox, 2);
        AddLabeledField(fields, "Y offset", yOffsetBox, 3);
        AddLabeledField(fields, "Z offset", zOffsetBox, 4);
        AddLabeledField(fields, "Texture ID", textureBox, 5);
        AddLabeledField(fields, "Material label", surfaceBox, 6);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(NewSmallNote("This stages a copied terrain face for Create BIN. The original face stays in place; the copied face is highlighted and can be dragged after you apply."));
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);

        return panel;
    }

    private TerrainBrushResult ApplyTerrainBrush(Vector2f center, float radius, Func<TerrainPolygon, int, float, float> nextDelta)
    {
        if (_currentGeometry == null)
            return new TerrainBrushResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        int affectedFaces = 0;
        int affectedVertices = 0;
        int touchedFaces = 0;
        int editableVerticesInRadius = 0;
        int skippedVisualOnlyFaces = 0;
        int skippedVisualOnlyVertices = 0;
        int followedVisualFaces = 0;
        int followedVisualVertices = 0;
        float safeRadius = Math.Max(1, radius);
        bool playableOnly = IsPlayableTerrainOnlyEnabled();
        Dictionary<TerrainPolygon, float[]> pendingDeltas = new();
        Dictionary<TerrainPolygon, float[]> originalDeltasByPolygon = new();
        Dictionary<TerrainPolygon, HashSet<int>> touchedVertexIndexes = new();
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved)
                continue;

            TerrainCollisionCoverage coverage = playableOnly
                ? GetTerrainCollisionCoverage(polygon)
                : TerrainCollisionCoverage.Empty;
            bool touchesBrush = TouchesTerrainBrush(polygon, center, safeRadius);
            if (touchesBrush)
                touchedFaces++;
            if (playableOnly && coverage.MatchedTriangles == 0)
            {
                if (touchesBrush)
                    skippedVisualOnlyFaces++;
                continue;
            }

            float[] originalDeltas = polygon.TerrainVertexDeltas().ToArray();
            float[] deltas = originalDeltas.ToArray();
            HashSet<int> touchedIndexes = new();
            bool changed = false;
            for (int i = 0; i < polygon.Points.Count && i < polygon.ZValues.Length && i < polygon.OriginalZValues.Length && i < deltas.Length; i++)
            {
                float distance = Distance(center, polygon.Points[i]);
                if (distance > safeRadius)
                    continue;
                if (playableOnly && !coverage.VertexIndexes.Contains(i))
                {
                    skippedVisualOnlyVertices++;
                    continue;
                }

                editableVerticesInRadius++;
                float falloff = TerrainBrushFalloff(distance / safeRadius);
                float updated = nextDelta(polygon, i, falloff);
                if (Math.Abs(updated - deltas[i]) <= 0.001f)
                    continue;

                deltas[i] = updated;
                touchedIndexes.Add(i);
                affectedVertices++;
                changed = true;
            }

            if (!changed)
                continue;

            originalDeltasByPolygon[polygon] = originalDeltas;
            pendingDeltas[polygon] = deltas;
            touchedVertexIndexes[polygon] = touchedIndexes;
            affectedFaces++;
        }

        TerrainVertexSeamAlignResult seamResult = IsTerrainSeamJoiningEnabled()
            ? TerrainVertexSeamAligner.Align(pendingDeltas, touchedVertexIndexes.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value))
            : new TerrainVertexSeamAlignResult(0, 0);
        if (playableOnly)
        {
            (followedVisualFaces, followedVisualVertices) = AddVisualOnlyTerrainFollowers(
                pendingDeltas,
                originalDeltasByPolygon,
                touchedVertexIndexes);
        }

        foreach ((TerrainPolygon polygon, float[] deltas) in pendingDeltas)
        {
            RecordTerrainBrushUndo(polygon, originalDeltasByPolygon[polygon]);
            polygon.ApplyTerrainVertexDeltas(deltas);
        }

        return new TerrainBrushResult(
            affectedFaces,
            affectedVertices,
            touchedFaces,
            editableVerticesInRadius,
            skippedVisualOnlyFaces,
            skippedVisualOnlyVertices,
            seamResult.SyncedVertexGroups,
            seamResult.AdjustedVertices,
            followedVisualFaces,
            followedVisualVertices);
    }

    private (int Faces, int Vertices) AddVisualOnlyTerrainFollowers(
        Dictionary<TerrainPolygon, float[]> pendingDeltas,
        Dictionary<TerrainPolygon, float[]> originalDeltasByPolygon,
        Dictionary<TerrainPolygon, HashSet<int>> touchedVertexIndexes)
    {
        if (_currentGeometry == null || pendingDeltas.Count == 0)
            return (0, 0);

        Dictionary<string, float> playableAnchors = new(StringComparer.Ordinal);
        foreach ((TerrainPolygon polygon, float[] deltas) in pendingDeltas)
        {
            TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(polygon);
            if (coverage.MatchedTriangles == 0)
                continue;
            if (!touchedVertexIndexes.TryGetValue(polygon, out HashSet<int>? touchedIndexes))
                continue;

            foreach (int index in touchedIndexes)
            {
                if (index < 0 || index >= deltas.Length || index >= polygon.OriginalPoints.Count || index >= polygon.OriginalZValues.Length)
                    continue;

                playableAnchors[TerrainVertexKey(polygon, index)] = deltas[index];
            }
        }

        if (playableAnchors.Count == 0)
            return (0, 0);

        int followedFaces = 0;
        int followedVertices = 0;
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved || pendingDeltas.ContainsKey(polygon))
                continue;

            TerrainCollisionCoverage coverage = GetTerrainCollisionCoverage(polygon);
            if (coverage.MatchedTriangles > 0)
                continue;

            int pointCount = EditableTerrainPointCount(polygon);
            if (pointCount <= 0)
                continue;

            float[] originalDeltas = polygon.TerrainVertexDeltas().ToArray();
            float[] deltas = originalDeltas.ToArray();
            HashSet<int> followedIndexes = new();
            for (int i = 0; i < pointCount && i < deltas.Length; i++)
            {
                if (!playableAnchors.TryGetValue(TerrainVertexKey(polygon, i), out float targetDelta))
                    continue;
                if (Math.Abs(deltas[i] - targetDelta) <= 0.001f)
                    continue;

                deltas[i] = targetDelta;
                followedIndexes.Add(i);
            }

            if (followedIndexes.Count == 0)
                continue;

            originalDeltasByPolygon[polygon] = originalDeltas;
            pendingDeltas[polygon] = deltas;
            touchedVertexIndexes[polygon] = followedIndexes;
            followedFaces++;
            followedVertices += followedIndexes.Count;
        }

        return (followedFaces, followedVertices);
    }

    private void BeginTerrainBrushUndo(string activeVerb = "")
    {
        FinishTerrainBrushUndo();
        _terrainBrushRedoHistory.Clear();
        _activeTerrainBrushUndo = new Dictionary<TerrainPolygon, float[]>();
        _activeTerrainBrushVerb = activeVerb;
        RefreshTerrainBrushHistoryText();
    }

    private void RecordTerrainBrushUndo(TerrainPolygon polygon, IReadOnlyList<float> deltas)
    {
        if (_activeTerrainBrushUndo == null || _activeTerrainBrushUndo.ContainsKey(polygon))
            return;

        _activeTerrainBrushUndo[polygon] = deltas.ToArray();
    }

    private void FinishTerrainBrushUndo()
    {
        if (_activeTerrainBrushUndo == null)
            return;

        if (_activeTerrainBrushUndo.Count > 0)
        {
            IReadOnlyList<TerrainBrushStrokeFace> stroke = _activeTerrainBrushUndo
                .Select(pair => new TerrainBrushStrokeFace(pair.Key, pair.Value, pair.Key.TerrainVertexDeltas().ToArray()))
                .ToArray();
            PushTerrainBrushUndoStroke(stroke);
            if (!string.IsNullOrWhiteSpace(_activeTerrainBrushVerb))
            {
                int faces = stroke.Count;
                int vertices = stroke.Sum(item => CountChangedTerrainVertices(item.BeforeDeltas, item.AfterDeltas));
                _lastTerrainBrushSummary = $"{_activeTerrainBrushVerb} stroke finished: changed {vertices} {Plural(vertices, "vertex", "vertices")} across {faces} {Plural(faces, "terrain face", "terrain faces")}.";
                RefreshTerrainBrushHistoryText();
                _statusText.Text = _lastTerrainBrushSummary;
            }
        }

        _activeTerrainBrushUndo = null;
        _activeTerrainBrushVerb = "";
        _activeFlattenTerrainZ = null;
    }

    private static int CountChangedTerrainVertices(IReadOnlyList<float> beforeDeltas, IReadOnlyList<float> afterDeltas)
    {
        int count = 0;
        int length = Math.Min(beforeDeltas.Count, afterDeltas.Count);
        for (int i = 0; i < length; i++)
        {
            if (Math.Abs(beforeDeltas[i] - afterDeltas[i]) > 0.001f)
                count++;
        }

        return count;
    }

    private void PushTerrainBrushUndoStroke(IReadOnlyList<TerrainBrushStrokeFace> stroke)
    {
        _terrainBrushUndoHistory.Add(stroke);
        while (_terrainBrushUndoHistory.Count > MaxTerrainBrushUndoStrokes)
            _terrainBrushUndoHistory.RemoveAt(0);
        RefreshTerrainBrushHistoryText();
    }

    private void ClearTerrainBrushUndoHistory()
    {
        _activeTerrainBrushUndo = null;
        _terrainBrushUndoHistory.Clear();
        _terrainBrushRedoHistory.Clear();
        _lastTerrainBrushSummary = "";
        RefreshTerrainBrushHistoryText();
    }

    private bool IsPlayableTerrainOnlyEnabled()
    {
        return _terrainPlayableOnlyBox.IsChecked == true && _terrainCollisionTriangleKeys.Count > 0 && _terrainCollisionMatchedFaces > 0;
    }

    private bool IsTerrainSeamJoiningEnabled()
    {
        return _terrainJoinSeamsBox.IsChecked == true;
    }

    private static bool TouchesTerrainBrush(TerrainPolygon polygon, Vector2f center, float radius)
    {
        for (int i = 0; i < polygon.Points.Count; i++)
        {
            if (Distance(center, polygon.Points[i]) <= radius)
                return true;
        }

        return Distance(center, polygon.Center) <= radius;
    }

    private float WeightedTerrainZAt(Vector2f center, float radius)
    {
        if (_currentGeometry == null)
            return _selectedTerrain?.AvgZ ?? 0;

        float safeRadius = Math.Max(1, radius);
        double weighted = 0;
        double total = 0;
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved)
                continue;

            bool playableOnly = IsPlayableTerrainOnlyEnabled();
            TerrainCollisionCoverage coverage = playableOnly
                ? GetTerrainCollisionCoverage(polygon)
                : TerrainCollisionCoverage.Empty;
            if (playableOnly && coverage.MatchedTriangles == 0)
                continue;

            for (int i = 0; i < polygon.Points.Count && i < polygon.ZValues.Length; i++)
            {
                if (playableOnly && !coverage.VertexIndexes.Contains(i))
                    continue;

                float distance = Distance(center, polygon.Points[i]);
                if (distance > safeRadius)
                    continue;

                float falloff = TerrainBrushFalloff(distance / safeRadius);
                weighted += polygon.ZValues[i] * falloff;
                total += falloff;
            }
        }

        return total <= 0.0001 ? _selectedTerrain?.AvgZ ?? 0 : (float)(weighted / total);
    }

    private Dictionary<TerrainPolygon, int> BuildTerrainFaceIndexLookup()
    {
        Dictionary<TerrainPolygon, int> faceIndexByPolygon = new();
        if (_currentGeometry == null)
            return faceIndexByPolygon;

        for (int i = 0; i < _currentGeometry.Polygons.Count; i++)
            faceIndexByPolygon[_currentGeometry.Polygons[i]] = i;
        return faceIndexByPolygon;
    }

    private Dictionary<(int FaceIndex, int VertexIndex), float> BuildTerrainBrushLocalSmoothTargets(
        Vector2f center,
        float radius,
        IReadOnlyDictionary<TerrainPolygon, int> faceIndexByPolygon)
    {
        if (_currentGeometry == null)
            return new Dictionary<(int FaceIndex, int VertexIndex), float>();

        float safeRadius = Math.Max(1, radius);
        float neighborhoodRadius = Math.Clamp(safeRadius * 0.38f, 96f, 420f);
        float candidateRadius = safeRadius + neighborhoodRadius;
        bool playableOnly = IsPlayableTerrainOnlyEnabled();
        List<TerrainBrushVertexSample> samples = new();
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            if (polygon.IsTerrainRemoved || !faceIndexByPolygon.TryGetValue(polygon, out int faceIndex))
                continue;

            TerrainCollisionCoverage coverage = playableOnly
                ? GetTerrainCollisionCoverage(polygon)
                : TerrainCollisionCoverage.Empty;
            if (playableOnly && coverage.MatchedTriangles == 0)
                continue;

            for (int vertexIndex = 0; vertexIndex < polygon.Points.Count && vertexIndex < polygon.ZValues.Length; vertexIndex++)
            {
                if (playableOnly && !coverage.VertexIndexes.Contains(vertexIndex))
                    continue;
                if (Distance(center, polygon.Points[vertexIndex]) > candidateRadius)
                    continue;

                samples.Add(new TerrainBrushVertexSample(faceIndex, vertexIndex, polygon.Points[vertexIndex], polygon.ZValues[vertexIndex]));
            }
        }

        return TerrainBrushLocalSmoother.BuildLocalAverageTargets(samples, center, safeRadius, neighborhoodRadius)
            .Where(target => target.NeighborCount > 1)
            .ToDictionary(target => (target.FaceIndex, target.VertexIndex), target => target.TargetZ);
    }

    private void RefreshAfterTerrainBrush(TerrainBrushResult result, string verb)
    {
        _viewport.InvalidateVisual();
        if (_selectedTerrain != null)
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        if (result.AffectedFaces == 0)
        {
            _lastTerrainBrushSummary = BuildEmptyTerrainBrushSummary(result);
            RefreshTerrainBrushHistoryText();
            _statusText.Text = _lastTerrainBrushSummary;
            return;
        }

        _lastTerrainBrushSummary = BuildTerrainBrushResultSummary(result, verb);
        RefreshTerrainBrushHistoryText();
        _statusText.Text = _lastTerrainBrushSummary;
    }

    private static string BuildEmptyTerrainBrushSummary(TerrainBrushResult result)
    {
        if (result.SkippedVisualOnlyFaces > 0 || result.SkippedVisualOnlyVertices > 0)
            return "Only edit playable ground is on, so the brush skipped visible-only terrain there. Turn it off to edit visible mesh only, or paint over matched playable ground.";

        if (result.TouchedFaces > 0 && result.EditableVerticesInRadius == 0)
            return "The brush was over terrain, but no editable vertices were inside the circle. Increase Size or use the Wide preset.";

        if (result.EditableVerticesInRadius > 0)
            return "The brush touched editable vertices, but this mode did not change their current height. Try a stronger brush or a different mode.";

        return "The terrain brush did not touch any editable vertices. Move onto terrain or increase Size.";
    }

    private static string BuildTerrainBrushResultSummary(TerrainBrushResult result, string verb)
    {
        int skippedVisualOnly = result.SkippedVisualOnlyFaces + result.SkippedVisualOnlyVertices;
        string skipped = skippedVisualOnly > 0
            ? $" Skipped {skippedVisualOnly} visual-only {Plural(skippedVisualOnly, "spot", "spots")}."
            : string.Empty;
        string seams = result.SeamAdjustedVertices > 0
            ? $" Aligned {result.SeamAdjustedVertices} shared {Plural(result.SeamAdjustedVertices, "vertex", "vertices")} so neighboring faces stay together."
            : string.Empty;
        string followers = result.FollowedVisualVertices > 0
            ? $" Kept {result.FollowedVisualVertices} visual side {Plural(result.FollowedVisualVertices, "vertex", "vertices")} attached to the playable terrain."
            : string.Empty;
        return $"{verb} {result.AffectedVertices} {Plural(result.AffectedVertices, "vertex", "vertices")} across {result.AffectedFaces} {Plural(result.AffectedFaces, "terrain face", "terrain faces")} with a soft brush.{seams}{followers}{skipped}";
    }

    private static string TerrainBrushActionStrokeVerb(TerrainBrushAction action)
    {
        return action switch
        {
            TerrainBrushAction.Raise => "Raise terrain",
            TerrainBrushAction.Lower => "Lower terrain",
            TerrainBrushAction.Blend => "Blend terrain",
            TerrainBrushAction.Flatten => "Level terrain",
            TerrainBrushAction.Restore => "Restore terrain",
            _ => "Terrain brush"
        };
    }

    private float TerrainBrushFalloff(float normalizedDistance)
    {
        float t = Math.Clamp(1f - normalizedDistance, 0f, 1f);
        float smooth = t * t * (3f - (2f * t));
        float feather = Math.Clamp((float)_terrainBrushFeatherSlider.Value / 100f, 0f, 1f);
        float exponent = 0.35f + (feather * 1.3f);
        return MathF.Pow(smooth, exponent);
    }

    private static float Distance(Vector2f a, Vector2f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static string Plural(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }

    private void UndoSelectedTerrain()
    {
        if (_selectedTerrain == null)
        {
            _statusText.Text = "Select a terrain face before undoing face edits.";
            return;
        }

        ClearTerrainBrushUndoHistory();
        _selectedTerrain.ResetTerrainEdit();
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        _statusText.Text = $"Undid terrain face {_selectedTerrainIndex}.";
    }

    private async Task MoveSelectedTerrainFaceAsync()
    {
        if (_currentGeometry == null || _selectedTerrain == null || _selectedTerrainIndex < 0)
        {
            _statusText.Text = "Select a terrain face before moving it.";
            return;
        }

        if (_selectedTerrain.IsTerrainRemoved)
        {
            _statusText.Text = "That face is staged for removal. Undo Terrain before moving it.";
            return;
        }

        TerrainPolygon terrain = _selectedTerrain;
        int terrainIndex = _selectedTerrainIndex;
        bool hasUniformX = TryGetUniformTerrainXYDelta(terrain, axis: 'x', out float currentX);
        bool hasUniformY = TryGetUniformTerrainXYDelta(terrain, axis: 'y', out float currentY);
        float currentZ = terrain.TerrainEditDeltaZ;
        TextBox xOffsetBox = new()
        {
            Text = hasUniformX ? $"{currentX:0.###}" : "",
            PlaceholderText = hasUniformX ? "0" : "mixed/keep",
            MinWidth = 120
        };
        TextBox yOffsetBox = new()
        {
            Text = hasUniformY ? $"{currentY:0.###}" : "",
            PlaceholderText = hasUniformY ? "0" : "mixed/keep",
            MinWidth = 120
        };
        TextBox zOffsetBox = new()
        {
            Text = $"{currentZ:0.###}",
            PlaceholderText = "0",
            MinWidth = 120
        };
        TextBlock presetDescription = NewSmallNote("");
        IReadOnlyList<TerrainMovePresetOption> presets =
        [
            new("Current offsets", hasUniformX ? currentX : 0, hasUniformY ? currentY : 0, currentZ, "Keep the current face placement values."),
            new("Raise 64", hasUniformX ? currentX : 0, hasUniformY ? currentY : 0, currentZ + 64, "Move the selected face upward."),
            new("Lower 64", hasUniformX ? currentX : 0, hasUniformY ? currentY : 0, currentZ - 64, "Move the selected face downward."),
            new("X +64", (hasUniformX ? currentX : 0) + 64, hasUniformY ? currentY : 0, currentZ, "Move the selected face on the X axis."),
            new("X -64", (hasUniformX ? currentX : 0) - 64, hasUniformY ? currentY : 0, currentZ, "Move the selected face on the X axis."),
            new("Y +64", hasUniformX ? currentX : 0, (hasUniformY ? currentY : 0) + 64, currentZ, "Move the selected face on the Y axis."),
            new("Y -64", hasUniformX ? currentX : 0, (hasUniformY ? currentY : 0) - 64, currentZ, "Move the selected face on the Y axis."),
            new("Custom", hasUniformX ? currentX : 0, hasUniformY ? currentY : 0, currentZ, "Type exact offsets below.")
        ];
        ComboBox presetBox = new()
        {
            ItemsSource = presets,
            SelectedIndex = 0,
            MinWidth = 180,
            MinHeight = 34
        };
        presetBox.SelectionChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is not TerrainMovePresetOption preset)
                return;

            if (preset.Label != "Custom")
            {
                xOffsetBox.Text = $"{preset.XOffset:0.###}";
                yOffsetBox.Text = $"{preset.YOffset:0.###}";
                zOffsetBox.Text = $"{preset.ZOffset:0.###}";
            }
            presetDescription.Text = preset.Description;
        };
        presetDescription.Text = presets[0].Description;

        TextBlock details = NewSmallNote(BuildTerrainMoveDetailsText(terrain, hasUniformX, hasUniformY));
        Window dialog = new()
        {
            Title = "Move Terrain Face",
            Width = 540,
            Height = 470,
            MinWidth = 460,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainMoveDialogContent(dialog, presetBox, presetDescription, xOffsetBox, yOffsetBox, zOffsetBox, details));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        bool hasXOffset = TryParseFloat(xOffsetBox.Text, out float exactXOffset);
        bool hasYOffset = TryParseFloat(yOffsetBox.Text, out float exactYOffset);
        bool hasZOffset = TryParseFloat(zOffsetBox.Text, out float exactZOffset);
        if (!hasXOffset && !hasYOffset && !hasZOffset)
        {
            _statusText.Text = "No terrain movement values were entered.";
            return;
        }

        ApplyExactTerrainMove(terrainIndex, terrain, hasXOffset, exactXOffset, hasYOffset, exactYOffset, hasZOffset, exactZOffset);
    }

    private void ApplyExactTerrainMove(int terrainIndex, TerrainPolygon terrain, bool hasXOffset, float exactXOffset, bool hasYOffset, float exactYOffset, bool hasZOffset, float exactZOffset)
    {
        int pointCount = Math.Min(terrain.Points.Count, terrain.OriginalPoints.Count);
        if ((hasXOffset || hasYOffset) && pointCount <= 0)
        {
            _statusText.Text = "Selected terrain does not expose editable points.";
            return;
        }

        TerrainCollisionCoverage coverage = (hasXOffset || hasYOffset) && IsPlayableTerrainOnlyEnabled()
            ? GetTerrainCollisionCoverage(terrain)
            : TerrainCollisionCoverage.Empty;
        if ((hasXOffset || hasYOffset) && IsPlayableTerrainOnlyEnabled() && coverage.VertexIndexes.Count < pointCount)
        {
            _statusText.Text = "Only edit playable ground is on, and this face is not fully matched to playable collision. Turn that off to move the visible mesh only.";
            return;
        }

        ClearTerrainBrushUndoHistory();
        if (hasXOffset || hasYOffset)
        {
            Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
            if (deltas.Length == 0)
                deltas = terrain.OriginalPoints.Select(_ => new Vector2f(0, 0)).ToArray();
            for (int i = 0; i < deltas.Length; i++)
            {
                float nextX = hasXOffset ? exactXOffset : deltas[i].X;
                float nextY = hasYOffset ? exactYOffset : deltas[i].Y;
                deltas[i] = new Vector2f(nextX, nextY);
            }

            terrain.ApplyTerrainVertexXYDeltas(deltas);
        }

        if (hasZOffset)
            terrain.ApplyTerrainDeltaZ(exactZOffset);

        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainPointControls();
        string xText = hasXOffset ? $"{exactXOffset:+0.###;-0.###;0}" : "kept";
        string yText = hasYOffset ? $"{exactYOffset:+0.###;-0.###;0}" : "kept";
        string zText = hasZOffset ? $"{exactZOffset:+0.###;-0.###;0}" : "kept";
        _statusText.Text = $"Moved terrain face {terrainIndex}: X {xText}, Y {yText}, Z {zText}. Save Terrain and Create BIN will include the moved vertices.";
    }

    private static string BuildTerrainMoveDetailsText(TerrainPolygon terrain, bool hasUniformX, bool hasUniformY)
    {
        string x = hasUniformX ? "single X offset" : "mixed X offsets";
        string y = hasUniformY ? "single Y offset" : "mixed Y offsets";
        return $"Face {terrain.RuntimeKey}, texture {terrain.TextureId}, {terrain.Points.Count} point(s), {x}, {y}. Offsets are measured from this face's original position.";
    }

    private static Control BuildTerrainMoveDialogContent(Window dialog, ComboBox presetBox, TextBlock presetDescription, TextBox xOffsetBox, TextBox yOffsetBox, TextBox zOffsetBox, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(112)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Preset", presetBox, 0);
        AddGridControl(fields, presetDescription, 1, 1);
        AddLabeledField(fields, "X offset", xOffsetBox, 2);
        AddLabeledField(fields, "Y offset", yOffsetBox, 3);
        AddLabeledField(fields, "Z offset", zOffsetBox, 4);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Move Face");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task EditTerrainPointAsync(int terrainIndex, TerrainPolygon terrain, int pointIndex)
    {
        if (_currentGeometry == null || terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count)
        {
            _statusText.Text = "That terrain point is no longer available in the loaded level.";
            return;
        }

        _selectedTerrainPointIndex = NormalizeTerrainPointIndex(terrain, pointIndex);
        if (_selectedTerrainPointIndex < 0)
        {
            _statusText.Text = "That terrain face has no editable points.";
            return;
        }

        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain, _selectedTerrainPointIndex));
        await EditSelectedTerrainPointAsync();
    }

    private async Task EditTerrainAsync(int terrainIndex, TerrainPolygon terrain)
    {
        string originalSurface = TerrainMaterialClassifier.NormalizeSurfaceName(terrain.Surface);
        TextBox deltaBox = new() { Text = $"{terrain.TerrainEditDeltaZ:0.###}", MinWidth = 120 };
        TextBox xOffsetBox = new() { Text = TryGetUniformTerrainXYDelta(terrain, axis: 'x', out float xOffset) ? $"{xOffset:0.###}" : "", MinWidth = 120, PlaceholderText = "mixed" };
        TextBox yOffsetBox = new() { Text = TryGetUniformTerrainXYDelta(terrain, axis: 'y', out float yOffset) ? $"{yOffset:0.###}" : "", MinWidth = 120, PlaceholderText = "mixed" };
        TextBox textureBox = new() { Text = terrain.TextureId >= 0 ? $"{terrain.TextureId}" : "", MinWidth = 120 };
        CustomTerrainTextureImport? customTexture = _customTerrainTextures.FirstOrDefault(texture => texture.TextureId == terrain.TextureId);
        ComboBox surfaceBox = new()
        {
            ItemsSource = TerrainSurfaceChoice.Known,
            SelectedItem = TerrainSurfaceChoice.Find(terrain.Surface),
            MinWidth = 160
        };
        TextBlock details = new()
        {
            Text = BuildTerrainEditDetailsText(terrain, customTexture),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        Window dialog = new()
        {
            Title = "Edit Terrain Face",
            Width = 560,
            Height = 720,
            MinWidth = 480,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildTerrainEditDialogContent(dialog, deltaBox, xOffsetBox, yOffsetBox, textureBox, surfaceBox, details));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        if (TryParseFloat(deltaBox.Text, out float deltaZ))
        {
            ClearTerrainBrushUndoHistory();
            terrain.ApplyTerrainDeltaZ(deltaZ);
        }
        bool hasXOffset = TryParseFloat(xOffsetBox.Text, out float exactXOffset);
        bool hasYOffset = TryParseFloat(yOffsetBox.Text, out float exactYOffset);
        if (hasXOffset || hasYOffset)
        {
            ClearTerrainBrushUndoHistory();
            Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
            if (deltas.Length == 0)
                deltas = terrain.OriginalPoints.Select(_ => new Vector2f(0, 0)).ToArray();
            for (int i = 0; i < deltas.Length; i++)
            {
                float nextX = hasXOffset ? exactXOffset : deltas[i].X;
                float nextY = hasYOffset ? exactYOffset : deltas[i].Y;
                deltas[i] = new Vector2f(nextX, nextY);
            }

            terrain.ApplyTerrainVertexXYDeltas(deltas);
        }
        if (TryParseInt(textureBox.Text, out int textureId))
            terrain.ApplyTextureOverride(textureId);
        if (surfaceBox.SelectedItem is TerrainSurfaceChoice surfaceChoice
            && !string.Equals(surfaceChoice.Surface, originalSurface, StringComparison.OrdinalIgnoreCase))
        {
            await ApplyTerrainSurfaceOverrideAsync(terrain.TextureId, surfaceChoice.Surface);
        }

        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        _viewport.InvalidateVisual();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(terrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainPointControls();
        _statusText.Text = $"Updated terrain face {terrainIndex}.";
    }

    private static bool TryGetUniformTerrainXYDelta(TerrainPolygon terrain, char axis, out float value)
    {
        Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
        if (deltas.Length == 0)
        {
            value = 0;
            return true;
        }

        value = axis == 'y' ? deltas[0].Y : deltas[0].X;
        foreach (Vector2f delta in deltas.Skip(1))
        {
            float current = axis == 'y' ? delta.Y : delta.X;
            if (Math.Abs(current - value) > 0.001f)
                return false;
        }

        return true;
    }

    private static bool TryGetUniformTerrainZDelta(TerrainPolygon terrain, out float value)
    {
        float[] deltas = terrain.TerrainVertexDeltas().ToArray();
        if (deltas.Length == 0)
        {
            value = 0;
            return true;
        }

        value = deltas[0];
        foreach (float delta in deltas.Skip(1))
        {
            if (Math.Abs(delta - value) > 0.001f)
                return false;
        }

        return true;
    }

    private string BuildTerrainEditDetailsText(TerrainPolygon terrain, CustomTerrainTextureImport? customTexture)
    {
        string surface = TerrainMaterialClassifier.FormatSurface(terrain.Surface);
        string behavior = TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior);
        string textureArt = customTexture == null
            ? "No custom texture art is staged for this texture."
            : $"Custom texture art staged: {BuildCustomTerrainTextureSummary(customTexture)}.";

        List<string> lines =
        [
            "What this edit affects:",
            $"Height: {FormatTerrainPatchReadiness(terrain)}.",
            $"Texture: texture ID {terrain.TextureId}; texture/art edits are included by Create BIN when patch planning has a valid source map.",
            $"Texture scope: {BuildSelectedTerrainTextureScopeSummary(terrain)}",
            "Placement: X/Y offsets move the whole face or added copy; Single Point controls keep individual vertices editable.",
            $"Material: {surface}. {BuildTerrainSurfaceEditStatus(terrain)}.",
            $"Behavior proof: {BuildTerrainMaterialProofHint(terrain.Surface, terrain.TextureId)}",
            textureArt,
            "",
            "Face details:",
            $"Runtime key: {terrain.RuntimeKey}",
            $"Original texture: {terrain.OriginalTextureId}",
            $"Current behavior: {behavior} ({terrain.BehaviorConfidence})",
            $"Playable collision: {FormatTerrainCollisionReadiness(terrain)}",
            $"Geometry edit: {FormatTerrainGeometryEdit(terrain)}",
            $"Face words: {DisplayRawWord(terrain.Word3)} / {DisplayRawWord(terrain.Word4)}",
            $"Depth/flip: {DisplayFaceDepth(terrain.FaceDepth)} / {(terrain.FaceFlip ? "flipped" : "normal")}",
            $"Color indexes: {DisplayIndexes(terrain.ColourIndexes)}"
        ];

        if (!string.IsNullOrWhiteSpace(terrain.BehaviorNote))
            lines.Add(terrain.BehaviorNote);

        return string.Join("\n", lines);
    }

    private async Task ApplyTerrainSurfaceOverrideAsync(int textureId, string surface)
    {
        if (_currentLevel == null || _currentGeometry == null || textureId < 0)
            return;

        await TerrainMaterialClassifier.SaveOverrideAsync(_currentLevel.Key, _workspace.RootPath, textureId, surface);
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
    }

    private Control BuildTerrainEditDialogContent(Window dialog, TextBox deltaBox, TextBox xOffsetBox, TextBox yOffsetBox, TextBox textureBox, ComboBox surfaceBox, TextBlock details)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(112)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Z offset", deltaBox, 0);
        AddLabeledField(fields, "X offset", xOffsetBox, 1);
        AddLabeledField(fields, "Y offset", yOffsetBox, 2);
        AddLabeledField(fields, "Texture ID", textureBox, 3);
        AddLabeledField(fields, "Material label", surfaceBox, 4);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(NewSmallNote("Material label changes the editor/readability label. To make terrain look different in game, use the Terrain tab's Use Material Look, Borrow In-Game Look, selected-face palette tools, or shared-texture tools before Create BIN."));
        panel.Children.Add(fields);
        panel.Children.Add(details);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private static string DisplayRawWord(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "not captured" : value;
    }

    private static string DisplayFaceDepth(int depth)
    {
        return depth < 0 ? "not captured" : depth.ToString(CultureInfo.InvariantCulture);
    }

    private static string DisplayIndexes(IReadOnlyList<int> indexes)
    {
        return indexes.Count == 0
            ? "not captured"
            : string.Join(", ", indexes.Select(index => index.ToString(CultureInfo.InvariantCulture)));
    }

    private bool MoveMoby(Moby moby, float dx, float dy, float dz, bool snapToTerrain = false)
    {
        float x = moby.Position.X + dx;
        float y = moby.Position.Y + dy;
        float z = moby.Position.Z + dz;
        bool snapped = false;
        if (snapToTerrain && ShouldSnapMobyToTerrain(moby) && TryFindTerrainZAt(x, y, z, out float terrainZ))
        {
            z = ApplyTerrainPlacementLift(new Vector3f(x, y, terrainZ), moby).Z;
            snapped = true;
        }

        moby.Position = new(
            x,
            y,
            z);
        moby.HasLoadedNativeEdit = true;
        moby.LoadedNativeEditSummary = snapped
            ? $"XYZ {moby.Position.X:0.0}, {moby.Position.Y:0.0}, {moby.Position.Z:0.0}; snapped to terrain"
            : $"XYZ {moby.Position.X:0.0}, {moby.Position.Y:0.0}, {moby.Position.Z:0.0}";
        return snapped;
    }

    private bool TryFindTerrainZAt(float x, float y, float referenceZ, out float z, int preferredTerrainIndex = -1, bool preferTopSurface = false)
    {
        z = 0;
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return false;

        return TerrainSnapper.TryFindZAt(
            _currentGeometry.Polygons,
            x,
            y,
            referenceZ,
            out z,
            preferredTerrainIndex,
            preferTopSurface);
    }

    private static bool ShouldSnapMobyToTerrain(Moby moby)
    {
        return moby.VisualKind != MobyVisualKind.Control;
    }

    private static Vector3f ApplyTerrainPlacementLift(Vector3f position, PendingMobyAdd pending)
    {
        return ApplyTerrainPlacementLift(
            position,
            pending.Type,
            pending.SourceByte36,
            pending.Template.SourceByte37,
            pending.SourceByte4F,
            pending.Template.Flag4A,
            pending.Flag4B,
            pending.Label,
            pending.Template.CandidateKind,
            pending.Template.TemplateNote);
    }

    private static Vector3f ApplyTerrainPlacementLift(Vector3f position, MobyClipboard clipboard)
    {
        bool pasteContainedGemAsLooseGem = clipboard.CopiedGemLike &&
            !clipboard.CopiedVisibleGem &&
            clipboard.CopiedGem != GemValue.Unknown;
        if (pasteContainedGemAsLooseGem)
            return position;

        return ApplyTerrainPlacementLift(
            position,
            clipboard.Type,
            clipboard.SourceByte36,
            clipboard.SourceByte37,
            clipboard.SourceByte4F,
            clipboard.Flag4A,
            clipboard.Flag4B,
            clipboard.Label,
            clipboard.CandidateKind,
            clipboard.BehaviorNote);
    }

    private static Vector3f ApplyTerrainPlacementLift(Vector3f position, Moby moby)
    {
        return ShouldApplyTerrainPlacementLift(moby)
            ? new Vector3f(position.X, position.Y, position.Z + TerrainPlacedObjectLift)
            : position;
    }

    private static Vector3f ApplyTerrainPlacementLift(
        Vector3f position,
        int type,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B,
        string label,
        string candidateKind,
        string behaviorNote)
    {
        Moby classifier = new()
        {
            Type = type,
            SourceByte36 = sourceByte36,
            SourceByte37 = sourceByte37,
            SourceByte4F = sourceByte4F,
            Flag4A = flag4A,
            Flag4B = flag4B,
            Label = label,
            CandidateKind = candidateKind,
            BehaviorNote = behaviorNote
        };
        return ApplyTerrainPlacementLift(position, classifier);
    }

    private static bool ShouldApplyTerrainPlacementLift(Moby moby)
    {
        MobyVisualKind kind = moby.VisualKind;
        return kind is not MobyVisualKind.Gem and not MobyVisualKind.Control;
    }

    private IEnumerable<Moby> GetLinkedMoveMobys(Moby selected)
    {
        return MobyLinkTraversal.GetLinkedMoveMobys(selected, _currentMobys);
    }

    private IEnumerable<Moby> GetChestContentMobys(Moby chest)
    {
        HashSet<int> linkedTrueIndexes = chest.Links
            .Where(link => string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase))
            .SelectMany(link => link.TrueIndexes)
            .Where(trueIndex => trueIndex != chest.TrueIndex)
            .ToHashSet();

        foreach (Moby moby in _currentMobys)
        {
            if (!moby.IsRemoved && linkedTrueIndexes.Contains(moby.TrueIndex) && moby.IsChestContent)
                yield return moby;
        }
    }

    private async Task AddMobyNearSelectionAsync()
    {
        IReadOnlyList<MobyPlacementOption> placementOptions = BuildAddMobyPlacementOptions();
        MobyPlacementOption selectedPlacement = placementOptions[0];
        Vector3f position = selectedPlacement.Position;
        IReadOnlyList<AddMobyTemplate> templates = BuildAddMobyTemplates(_selectedMoby, _currentMobys);
        ComboBox placementBox = new()
        {
            ItemsSource = placementOptions,
            SelectedItem = selectedPlacement,
            MinWidth = 260
        };
        ComboBox kindBox = new()
        {
            ItemsSource = templates,
            SelectedIndex = 0,
            MinWidth = 260
        };
        ComboBox gemBox = new()
        {
            ItemsSource = GemValue.Known,
            SelectedItem = GemValue.Red,
            MinWidth = 160
        };
        TextBox nameBox = new() { Text = "New Red gem", MinWidth = 240 };
        TextBox typeBox = new() { Text = "0x20", MinWidth = 120 };
        TextBox stateBox = new() { Text = "0x00", MinWidth = 120 };
        TextBox xBox = NewPositionBox(position.X);
        TextBox yBox = NewPositionBox(position.Y);
        TextBox zBox = NewPositionBox(position.Z);
        TextBlock placementHint = NewSmallNote(selectedPlacement.Description);
        TextBlock note = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };

        void ApplyTemplateToFields()
        {
            AddMobyTemplate template = kindBox.SelectedItem as AddMobyTemplate ?? templates[0];
            typeBox.Text = $"0x{template.Type:X2}";
            stateBox.Text = $"0x{template.State:X2}";
            if (template.DefaultGem != null)
                gemBox.SelectedItem = template.DefaultGem.Value;
            if (template.UsesGem && gemBox.SelectedItem is GemValue gem)
                nameBox.Text = $"New {gem.Name}";
            else if (string.IsNullOrWhiteSpace(nameBox.Text) || nameBox.Text.StartsWith("New ", StringComparison.Ordinal))
                nameBox.Text = template.DefaultLabel;
            note.Text = BuildAddMobyTemplateNote(template);
        }

        placementBox.SelectionChanged += (_, _) =>
        {
            selectedPlacement = placementBox.SelectedItem as MobyPlacementOption ?? placementOptions[0];
            SetPositionBoxes(xBox, yBox, zBox, selectedPlacement.Position);
            placementHint.Text = selectedPlacement.Description;
        };
        kindBox.SelectionChanged += (_, _) => ApplyTemplateToFields();
        gemBox.SelectionChanged += (_, _) =>
        {
            if ((kindBox.SelectedItem as AddMobyTemplate)?.UsesGem == true && gemBox.SelectedItem is GemValue gem)
                nameBox.Text = $"New {gem.Name}";
        };
        ApplyTemplateToFields();

        Window dialog = new()
        {
            Title = "Add Object",
            Width = 560,
            Height = 680,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildAddMobyDialogContent(dialog, placementBox, placementHint, kindBox, gemBox, nameBox, typeBox, stateBox, xBox, yBox, zBox, note));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        AddMobyTemplate selectedTemplate = kindBox.SelectedItem as AddMobyTemplate ?? templates[0];
        if (selectedTemplate.FromCrossLevelTemplate && !selectedTemplate.CurrentLevelReady && !selectedTemplate.CurrentLevelPlaceable)
        {
            _statusText.Text = $"{selectedTemplate.DefaultLabel} is {selectedTemplate.CurrentLevelSupportLabel} It was not added.";
            return;
        }

        GemValue selectedGem = gemBox.SelectedItem is GemValue gemValue ? gemValue : GemValue.Red;
        int type = selectedTemplate.Type;
        int state = selectedTemplate.State;
        if (TryParseByte(typeBox.Text, out int parsedType))
            type = parsedType;
        if (TryParseByte(stateBox.Text, out int parsedState))
            state = parsedState;
        if (TryParseFloat(xBox.Text, out float parsedX) &&
            TryParseFloat(yBox.Text, out float parsedY) &&
            TryParseFloat(zBox.Text, out float parsedZ))
            position = new Vector3f(parsedX, parsedY, parsedZ);

        string label = string.IsNullOrWhiteSpace(nameBox.Text)
            ? selectedTemplate.DefaultLabel
            : nameBox.Text.Trim();
        int sourceByte36 = selectedTemplate.UsesGem ? selectedGem.IdByte : selectedTemplate.SourceByte36;
        int sourceByte4F = selectedTemplate.UsesGem ? selectedGem.ValueByte : selectedTemplate.SourceByte4F;
        int flag4B = selectedTemplate.UsesGem && selectedTemplate.Flag4B != 0xFF ? selectedGem.IdByte : selectedTemplate.Flag4B;

        PendingMobyAdd pending = new(
            selectedTemplate,
            label,
            type,
            state,
            sourceByte36,
            sourceByte4F,
            flag4B,
            selectedGem,
            selectedPlacement,
            templates);

        if (selectedPlacement.PlaceAfterDialog)
        {
            BeginPendingMobyPlacement(pending, position);
            return;
        }

        if (selectedPlacement.SnapOnAdd)
        {
            position = SnapOrNearestTerrain(position);
            position = ApplyTerrainPlacementLift(position, pending);
        }

        AddPendingMobyAtPosition(pending, position);
    }

    private void BeginPendingMobyPlacement(PendingMobyAdd pending, Vector3f fallbackPosition)
    {
        _pendingMobyAdd = pending with { FallbackPosition = fallbackPosition };
        _viewport.ObjectPlacementMode = true;
        RefreshActionAvailability();
        _statusText.Text = $"Click the map or Fly 3D view to place {pending.Label}. Press Escape to cancel.";
    }

    private void PlacePendingMobyAtViewport(Point screenPoint)
    {
        if (_pendingMobyAdd == null)
            return;

        PendingMobyAdd pending = _pendingMobyAdd;
        Vector3f position = pending.FallbackPosition;
        int terrainIndex = -1;
        if (_viewport.TryGetObjectPlacementPosition(screenPoint, position, out Vector3f viewportPosition, out terrainIndex))
            position = viewportPosition;
        if (pending.Placement.SnapOnAdd)
        {
            position = SnapClickedOrNearestTerrain(position, terrainIndex);
            position = ApplyTerrainPlacementLift(position, pending);
        }

        _pendingMobyAdd = null;
        _viewport.ObjectPlacementMode = false;
        AddPendingMobyAtPosition(pending, position);
    }

    private void CancelPendingMobyPlacement()
    {
        if (_pendingMobyAdd == null)
            return;

        string label = _pendingMobyAdd.Label;
        _pendingMobyAdd = null;
        _viewport.ObjectPlacementMode = false;
        RefreshActionAvailability();
        _statusText.Text = $"Canceled placement for {label}.";
    }

    private void AddPendingMobyAtPosition(PendingMobyAdd pending, Vector3f position)
    {
        AddMobyTemplate selectedTemplate = pending.Template;
        GemValue selectedGem = pending.SelectedGem;
        int index = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.Index) + 1;
        int trueIndex = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.TrueIndex) + 1;
        List<Moby> addedMobys;
        Moby moby;
        if (IsSpringChestCrossLevelTemplate(selectedTemplate))
        {
            AddMobyTemplate controllerTemplate = BuildSpringChestControllerTemplate(selectedTemplate);
            Moby controller = CreateAddedMobyFromTemplate(
                controllerTemplate,
                "Spring Chest controller",
                position,
                index,
                trueIndex,
                controllerTemplate.Type,
                controllerTemplate.State,
                controllerTemplate.SourceByte36,
                controllerTemplate.SourceByte4F,
                controllerTemplate.Flag4B,
                selectedGem);
            moby = CreateAddedMobyFromTemplate(selectedTemplate, pending.Label, position, index + 1, trueIndex + 1, pending.Type, pending.State, pending.SourceByte36, pending.SourceByte4F, pending.Flag4B, selectedGem);
            controller.BehaviorNote = "Hidden paired controller for the visible Spring Chest; move and export both records together.";
            moby.BehaviorNote = "Visible Spring Chest shell paired with the hidden controller record.";
            addedMobys = [controller, moby];
            index += 2;
            trueIndex += 2;
        }
        else
        {
            moby = CreateAddedMobyFromTemplate(selectedTemplate, pending.Label, position, index, trueIndex, pending.Type, pending.State, pending.SourceByte36, pending.SourceByte4F, pending.Flag4B, selectedGem);
            addedMobys = [moby];
            index++;
            trueIndex++;
        }

        if (!string.IsNullOrWhiteSpace(selectedTemplate.CompanionTemplateId))
        {
            AddMobyTemplate? companion = pending.AvailableTemplates.FirstOrDefault(template =>
                string.Equals(template.TemplateId, selectedTemplate.CompanionTemplateId, StringComparison.OrdinalIgnoreCase));
            if (companion != null)
            {
                Vector3f companionPosition = new(position.X - 160, position.Y, position.Z);
                if (TryFindTerrainZAt(companionPosition.X, companionPosition.Y, companionPosition.Z, out float companionZ))
                    companionPosition = new Vector3f(companionPosition.X, companionPosition.Y, companionZ);
                companionPosition = ApplyTerrainPlacementLift(
                    companionPosition,
                    companion.Type,
                    companion.SourceByte36,
                    companion.SourceByte37,
                    companion.SourceByte4F,
                    companion.Flag4A,
                    companion.Flag4B,
                    companion.DefaultLabel,
                    companion.CandidateKind,
                    companion.TemplateNote);
                addedMobys.Add(CreateAddedMobyFromTemplate(
                    companion,
                    companion.DefaultLabel,
                    companionPosition,
                    index,
                    trueIndex,
                    companion.Type,
                    companion.State,
                    companion.SourceByte36,
                    companion.SourceByte4F,
                    companion.Flag4B,
                    selectedGem));
            }
        }

        _currentMobys.AddRange(addedMobys);
        _viewport.Mobys = _currentMobys;
        RefreshMobyList(moby);
        RefreshCurrentLevelDetails();
        _viewport.SelectMoby(moby, true);
        string candidateNote = selectedTemplate.FromCrossLevelTemplate && !selectedTemplate.CurrentLevelReady && selectedTemplate.CurrentLevelPlaceable
            ? " Use Advanced > Create Candidate BIN to make a disposable test disc for it."
            : "";
        _statusText.Text = addedMobys.Count > 1
            ? $"Added {moby.DisplayLabel} and {addedMobys.Count - 1} companion object(s).{candidateNote}"
            : $"Added {moby.DisplayLabel}.{candidateNote}";
    }

    private void CopySelectedMoby()
    {
        if (_selectedMoby == null || _selectedMoby.IsRemoved)
        {
            _statusText.Text = "Select an object before copying it.";
            return;
        }

        _mobyClipboard = MobyClipboard.From(_selectedMoby);
        RefreshActionAvailability();
        _statusText.Text = $"Copied {_selectedMoby.DisplayLabel}. Paste with Ctrl+V on Windows or Command+V on Mac.";
    }

    private void PasteMobyClipboardAtLastPointer()
    {
        PasteMobyClipboardAtViewport(_viewport.LastPointerPosition);
    }

    private void PasteMobyClipboardAtViewport(Point screenPoint)
    {
        if (_mobyClipboard == null)
        {
            _statusText.Text = "Copy an object before pasting.";
            return;
        }

        Vector3f fallback = _selectedMoby != null && !_selectedMoby.IsRemoved
            ? _selectedMoby.Position
            : NewMobyDefaultPosition();
        Vector3f position = fallback;
        int terrainIndex = -1;
        if (_viewport.TryGetObjectPlacementPosition(screenPoint, fallback, out Vector3f viewportPosition, out terrainIndex))
            position = viewportPosition;
        position = SnapClickedOrNearestTerrain(position, terrainIndex);
        position = ApplyTerrainPlacementLift(position, _mobyClipboard);

        Moby pasted = CreateMobyFromClipboard(_mobyClipboard, position);
        _currentMobys.Add(pasted);
        _viewport.Mobys = _currentMobys;
        RefreshMobyList(pasted);
        RefreshCurrentLevelDetails();
        _viewport.SelectMoby(pasted, true);
        _statusText.Text = $"Pasted {pasted.DisplayLabel} at the cursor.";
    }

    private Moby CreateMobyFromClipboard(MobyClipboard clipboard, Vector3f position)
    {
        int index = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.Index) + 1;
        int trueIndex = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.TrueIndex) + 1;
        bool pasteContainedGemAsLooseGem = clipboard.CopiedGemLike &&
            !clipboard.CopiedVisibleGem &&
            clipboard.CopiedGem != GemValue.Unknown;
        int type = pasteContainedGemAsLooseGem ? 0x18 : clipboard.Type;
        int state = pasteContainedGemAsLooseGem ? 0x00 : clipboard.State;
        int sourceByte36 = pasteContainedGemAsLooseGem ? clipboard.CopiedGem.IdByte : clipboard.SourceByte36;
        int sourceByte4F = pasteContainedGemAsLooseGem ? clipboard.CopiedGem.ValueByte : clipboard.SourceByte4F;
        int flag4A = pasteContainedGemAsLooseGem ? 0x40 : clipboard.Flag4A;
        int flag4B = pasteContainedGemAsLooseGem ? 0xFF : clipboard.Flag4B;
        ColorRgba color = pasteContainedGemAsLooseGem ? clipboard.CopiedGem.Color : clipboard.Color;
        string label = clipboard.Label.StartsWith("Copy of ", StringComparison.Ordinal)
            ? clipboard.Label
            : pasteContainedGemAsLooseGem
                ? $"Copy of {clipboard.CopiedGem.DisplayName}"
                : $"Copy of {clipboard.Label}";
        string patchLead = pasteContainedGemAsLooseGem
            ? $"Pasted from copied contained gem {clipboard.Label}; converted to a loose native gem so it appears and can be collected in-game."
            : $"Pasted from copied object {clipboard.Label}; it exports as a new native source-table record when supported.";

        return new Moby
        {
            Index = index,
            TrueIndex = trueIndex,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
            Position = position,
            OriginalPosition = position,
            Type = type,
            OriginalType = type,
            State = state,
            OriginalState = state,
            SourceByte36 = sourceByte36,
            OriginalSourceByte36 = sourceByte36,
            SourceByte37 = clipboard.SourceByte37,
            OriginalSourceByte37 = clipboard.SourceByte37,
            SourceByte4F = sourceByte4F,
            OriginalSourceByte4F = sourceByte4F,
            Flag4A = flag4A,
            OriginalFlag4A = flag4A,
            Flag4B = flag4B,
            OriginalFlag4B = flag4B,
            Color = color,
            Label = label,
            OriginalLabel = label,
            PatchStatus = "new-native-editor-object-copy",
            PatchLead = patchLead,
            CrossLevelTemplateId = pasteContainedGemAsLooseGem ? "" : clipboard.CrossLevelTemplateId,
            CrossLevelFamily = pasteContainedGemAsLooseGem ? "" : clipboard.CrossLevelFamily,
            CrossLevelSourceLevelKey = pasteContainedGemAsLooseGem ? "" : clipboard.CrossLevelSourceLevelKey,
            CrossLevelSourceLevelName = pasteContainedGemAsLooseGem ? "" : clipboard.CrossLevelSourceLevelName,
            CrossLevelSourceTrueIndex = pasteContainedGemAsLooseGem ? -1 : clipboard.CrossLevelSourceTrueIndex,
            CrossLevelRequiredExporterFeature = pasteContainedGemAsLooseGem ? "" : clipboard.CrossLevelRequiredExporterFeature,
            CandidateKind = pasteContainedGemAsLooseGem ? "loose gem collectible" : clipboard.CandidateKind,
            Confidence = clipboard.Confidence,
            Evidence = $"Copied from {clipboard.Label} in the native editor.",
            BehaviorNote = pasteContainedGemAsLooseGem ? "" : clipboard.BehaviorNote,
            ZoneLabel = clipboard.ZoneLabel,
            IsAdded = true
        };
    }

    private static bool IsSpringChestCrossLevelTemplate(AddMobyTemplate template)
    {
        return template.FromCrossLevelTemplate &&
            string.Equals(template.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            !template.TemplateId.Contains("controller", StringComparison.OrdinalIgnoreCase);
    }

    private static AddMobyTemplate BuildSpringChestControllerTemplate(AddMobyTemplate shellTemplate)
    {
        return shellTemplate with
        {
            Name = "Spring Chest controller",
            Type = 0x20,
            State = 0x00,
            SourceByte36 = 0xC2,
            SourceByte37 = 0x00,
            SourceByte4F = 0x00,
            Flag4A = 0x10,
            Flag4B = 0x55,
            DefaultLabel = "Spring Chest controller",
            TemplateId = "common.spring_chest_controller.stonehill.local00c2",
            SourceLevelKey = "stonehill",
            SourceLevelName = "Stone Hill",
            SourceTrueIndex = 30,
            AddSupportStatus = "supported-lightweight-object",
            RequiredExporterFeature = "DirectSourceRecordAppend",
            CandidateKind = "Control",
            TemplateNote = "Local controller paired with the visible Spring Chest shell for guarded Stone Hill Spring Chest candidates."
        };
    }

    private Moby CreateAddedMobyFromTemplate(
        AddMobyTemplate template,
        string label,
        Vector3f position,
        int index,
        int trueIndex,
        int type,
        int state,
        int sourceByte36,
        int sourceByte4F,
        int flag4B,
        GemValue selectedGem)
    {
        ColorRgba color = template.UsesGem
            ? selectedGem.Color
            : template.DefaultGem is GemValue templateGem
                ? templateGem.Color
            : template.DefaultColor is ColorRgba templateColor
                ? templateColor
            : template.CopiesSelected && _selectedMoby != null
                ? _selectedMoby.Color
                : Moby.ColorForType(type);

        return new Moby
        {
            Index = index,
            TrueIndex = trueIndex,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
            Position = position,
            OriginalPosition = position,
            Type = type,
            OriginalType = type,
            State = state,
            OriginalState = state,
            SourceByte36 = sourceByte36,
            OriginalSourceByte36 = sourceByte36,
            SourceByte37 = template.SourceByte37,
            SourceByte4F = sourceByte4F,
            OriginalSourceByte4F = sourceByte4F,
            Flag4A = template.Flag4A,
            Flag4B = flag4B,
            OriginalFlag4B = flag4B,
            Color = color,
            Label = label,
            OriginalLabel = label,
            CandidateKind = template.CandidateKind,
            Confidence = template.FromCrossLevelTemplate
                ? "cross-level-template"
                : template.FromLevelTemplate
                ? "same-level-native-clone"
                : "native-editor-added",
            Evidence = template.FromCrossLevelTemplate
                ? $"Added from {template.SourceLevelName} template {template.TemplateId}."
                : template.FromLevelTemplate
                ? $"Added from same-level donor T{template.SourceTrueIndex}."
                : "Added through the Mac native object editor.",
            PatchStatus = template.FromCrossLevelTemplate
                ? template.AddSupportStatus
                : template.FromLevelTemplate
                ? "native-clone"
                : "new-native-editor-object",
            PatchLead = template.CopiesSelected
                ? $"Added in the native editor by cloning {_selectedMoby?.DisplayLabel ?? "the selected object"}; simple 0x18/0x20 clones can use the native source-table append path."
                : template.FromCrossLevelTemplate
                ? BuildCrossLevelPatchLead(template)
                : template.FromLevelTemplate
                ? $"Added in the native editor from same-level donor T{template.SourceTrueIndex}; Create BIN appends a native clone with same-level donor data. Enemy/chest behavior still needs in-game validation."
                : template.UsesGem
                ? "Added in the native editor; simple gem adds export through the native source-table append path."
                : "Added in the native editor; this custom object may need actor-package support before it is playable.",
            CrossLevelTemplateId = template.FromCrossLevelTemplate ? template.TemplateId : "",
            CrossLevelFamily = template.FromCrossLevelTemplate ? template.Family : "",
            CrossLevelSourceLevelKey = template.FromCrossLevelTemplate ? template.SourceLevelKey : "",
            CrossLevelSourceLevelName = template.FromCrossLevelTemplate ? template.SourceLevelName : "",
            CrossLevelSourceTrueIndex = template.FromCrossLevelTemplate ? template.SourceTrueIndex : -1,
            CrossLevelRequiredExporterFeature = template.FromCrossLevelTemplate ? template.RequiredExporterFeature : "",
            IsAdded = true
        };
    }

    private async Task AddChestContentGemAsync()
    {
        Moby? chest = ResolveChestForContentEdit(_selectedMoby);
        if (chest == null)
        {
            _statusText.Text = "Select a chest or one of its linked content gems before adding chest contents.";
            return;
        }

        ComboBox gemBox = new()
        {
            ItemsSource = GemValue.Known,
            SelectedItem = GemValue.Green,
            MinWidth = 180
        };
        TextBlock note = NewSmallNote("This adds a linked contained-gem marker in the editor and keeps it stacked at the chest. Saving keeps the edit; native BIN export for brand-new contained markers still needs the special-data chest-link path.");
        Window dialog = new()
        {
            Title = "Add Chest Gem",
            Width = 390,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildAddChestGemDialogContent(dialog, gemBox, note));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        GemValue gem = gemBox.SelectedItem is GemValue selected ? selected : GemValue.Green;
        Moby content = CreateChestContentMoby(chest, gem);
        _currentMobys.Add(content);
        RefreshChestContentLink(chest, content);
        _viewport.Mobys = _currentMobys;
        RefreshMobyList(content);
        _viewport.SelectMoby(content, true);
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(content));
        _statusText.Text = $"Added linked {gem.DisplayName} content for {chest.DisplayLabel}.";
    }

    private Control BuildAddChestGemDialogContent(Window dialog, ComboBox gemBox, TextBlock note)
    {
        Grid fields = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(92)),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        AddLabeledField(fields, "Gem", gemBox, 0);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(fields);
        panel.Children.Add(new ScrollViewer
        {
            Content = note,
            MaxHeight = 150,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button add = NewButton("Add");
        cancel.Click += (_, _) => dialog.Close(false);
        add.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(add);
        panel.Children.Add(buttons);
        return panel;
    }

    private Moby CreateChestContentMoby(Moby chest, GemValue gem)
    {
        int index = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.Index) + 1;
        int trueIndex = _currentMobys.Count == 0 ? 0 : _currentMobys.Max(moby => moby.TrueIndex) + 1;
        string label = $"Locked chest content: {gem.DisplayName}";
        return new Moby
        {
            Index = index,
            TrueIndex = trueIndex,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
            Position = chest.Position,
            OriginalPosition = chest.Position,
            Type = 0x00,
            OriginalType = 0x00,
            State = 0x00,
            OriginalState = 0x00,
            SourceByte36 = 0x00,
            OriginalSourceByte36 = 0x00,
            SourceByte37 = 0x00,
            SourceByte4F = 0x00,
            OriginalSourceByte4F = 0x00,
            Flag4A = 0xFF,
            Flag4B = gem.IdByte,
            OriginalFlag4B = gem.IdByte,
            Color = gem.Color,
            Label = label,
            OriginalLabel = label,
            CandidateKind = $"locked chest contained {gem.Name.ToLowerInvariant()} reward marker",
            Confidence = "native-editor-added",
            Evidence = "Added through the Mac native chest contents editor.",
            PatchStatus = "new-native-editor-chest-content",
            PatchLead = "Linked to the owning chest in the editor. Native BIN export for brand-new contained markers still needs the contained-gem special-data path.",
            IsAdded = true
        };
    }

    private Moby? ResolveChestForContentEdit(Moby? selected)
    {
        if (selected == null || selected.IsRemoved)
            return null;
        if (selected.IsChest)
            return selected;
        if (!selected.IsChestContent)
            return null;

        foreach (MobyLink link in selected.Links.Where(link => string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase)))
        {
            Moby? chest = _currentMobys.FirstOrDefault(moby => !moby.IsRemoved && moby.IsChest && link.TrueIndexes.Contains(moby.TrueIndex));
            if (chest != null)
                return chest;
        }

        return _currentMobys
            .Where(moby => !moby.IsRemoved && moby.IsChest)
            .OrderBy(moby => DistanceSquared(moby.Position, selected.Position))
            .FirstOrDefault(moby => DistanceSquared(moby.Position, selected.Position) <= 512 * 512);
    }

    private void RefreshChestContentLink(Moby chest, Moby? extraContent = null)
    {
        string key = $"{_currentLevel?.Key ?? "level"}:chest:{chest.TrueIndex}";
        List<Moby> contents = GetChestContentMobys(chest).ToList();
        if (extraContent != null && !contents.Any(moby => ReferenceEquals(moby, extraContent)))
            contents.Add(extraContent);

        contents.AddRange(_currentMobys
            .Where(moby => !moby.IsRemoved && moby.IsChestContent && DistanceSquared(moby.Position, chest.Position) <= 64 * 64)
            .Where(moby => !contents.Any(existing => ReferenceEquals(existing, moby))));

        foreach (Moby moby in _currentMobys)
        {
            moby.Links.RemoveAll(existing =>
                string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(existing.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) && existing.TrueIndexes.Contains(chest.TrueIndex)));
        }

        if (contents.Count == 0)
            return;

        List<int> indexes = new() { chest.TrueIndex };
        indexes.AddRange(contents.OrderBy(moby => moby.TrueIndex).Select(moby => moby.TrueIndex));
        MobyLink link = new()
        {
            Key = key,
            Name = $"Chest contents for T{chest.TrueIndex}",
            Kind = "chest contents",
            LinkedMove = true,
            Confidence = "native-editor",
            Reason = "Chest contents were edited in the Mac native editor.",
            TrueIndexes = indexes
        };

        chest.Links.Add(link);
        foreach (Moby content in contents)
            content.Links.Add(link);
    }

    private IReadOnlyList<AddMobyTemplate> BuildAddMobyTemplates(Moby? selected, IEnumerable<Moby> currentMobys)
    {
        List<AddMobyTemplate> templates = new();
        if (selected != null && !selected.IsRemoved && (!_releaseMode || IsReleaseSafeTrueAddIdentity(
            selected.Type,
            selected.SourceByte36,
            selected.SourceByte37,
            selected.SourceByte4F,
            selected.Flag4A,
            selected.Flag4B)))
        {
            templates.Add(new AddMobyTemplate(
                "Clone selected object",
                selected.Type,
                selected.State,
                selected.SourceByte36,
                selected.SourceByte37,
                selected.SourceByte4F,
                selected.Flag4A,
                selected.Flag4B,
                false,
                $"Copy of {selected.DisplayLabel}",
                CopiesSelected: true,
                DefaultGem: selected.IsGemLike && selected.Gem != GemValue.Unknown ? selected.Gem : null));
        }

        templates.AddRange(_releaseMode
            ? AddMobyTemplate.Known.Where(IsReleaseSafeTrueAddTemplate)
            : AddMobyTemplate.Known);
        List<AddMobyTemplate> levelObjectTemplates = BuildLevelObjectTemplates(currentMobys).ToList();
        templates.AddRange(levelObjectTemplates);
        if (_releaseMode)
            return templates;

        List<AddMobyTemplate> crossLevelTemplates = SortCrossLevelTemplatesForCurrentLevel(LoadCrossLevelObjectTemplates()
            .Where(template => !ShouldHideCrossLevelTemplateInNormalAddList(template, levelObjectTemplates))).ToList();
        templates.AddRange(BuildCrossLevelTemplateBundles(crossLevelTemplates));
        templates.AddRange(crossLevelTemplates);
        return templates;
    }

    private static bool ShouldHideCrossLevelTemplateInNormalAddList(AddMobyTemplate template, IEnumerable<AddMobyTemplate> levelObjectTemplates)
    {
        if (!template.FromCrossLevelTemplate)
            return false;

        if (!string.Equals(template.Family, "springChest", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool IsReleaseSafeTrueAddTemplate(AddMobyTemplate template) =>
        IsReleaseSafeTrueAddIdentity(
            template.Type,
            template.SourceByte36,
            template.SourceByte37,
            template.SourceByte4F,
            template.Flag4A,
            template.Flag4B);

    private static bool IsReleaseSafeTrueAddIdentity(
        int type,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        bool isLooseGem = type == 0x18 &&
            sourceByte37 == 0x00 &&
            flag4A == 0x40 &&
            flag4B == 0xFF &&
            GemValue.TryFromIdByte(sourceByte36, out _);
        bool isNativeKey = type == 0x18 &&
            sourceByte36 == 0xAD &&
            sourceByte37 == 0x00 &&
            flag4A == 0x40 &&
            flag4B == 0xFF;
        bool isNativeKeyChest = type == 0x20 &&
            sourceByte36 == 0xAE &&
            sourceByte37 == 0x00 &&
            flag4A == 0x10 &&
            GemValue.TryFromIdByte(flag4B, out _);
        return isLooseGem || isNativeKey || isNativeKeyChest;
    }

    private static IEnumerable<AddMobyTemplate> BuildCrossLevelTemplateBundles(IReadOnlyList<AddMobyTemplate> templates)
    {
        AddMobyTemplate? key = templates.FirstOrDefault(template =>
            string.Equals(template.Family, "key", StringComparison.OrdinalIgnoreCase) &&
            template.CurrentLevelReady);
        AddMobyTemplate? keyChest = templates.FirstOrDefault(template =>
            string.Equals(template.Family, "lockedChest", StringComparison.OrdinalIgnoreCase) &&
            template.CurrentLevelPlaceable);

        if (key == null || keyChest == null)
            yield break;

        string readiness = keyChest.CurrentLevelReady ? "ready here" : "candidate here";
        yield return keyChest with
        {
            Name = $"From {keyChest.SourceLevelName}: Key + Key Chest pair ({readiness})",
            DefaultLabel = "New Key Chest",
            TemplateNote = "Adds a matching key next to the new key chest so the test level has both halves of the locked-chest setup.",
            CompanionTemplateId = key.TemplateId
        };
    }

    private static IEnumerable<AddMobyTemplate> SortCrossLevelTemplatesForCurrentLevel(IEnumerable<AddMobyTemplate> templates)
    {
        return templates
            .OrderByDescending(template => template.CurrentLevelReady)
            .ThenByDescending(template => template.CurrentLevelPlaceable)
            .ThenBy(template => CrossLevelTemplateFamilyRank(template.Family))
            .ThenBy(template => template.SourceLevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(template => template.DefaultLabel, StringComparer.OrdinalIgnoreCase);
    }

    private static int CrossLevelTemplateFamilyRank(string family)
    {
        return family switch
        {
            "key" => 0,
            "lockedChest" => 1,
            "springChest" => 2,
            "enemyTransform" => 3,
            _ => 9
        };
    }

    private IReadOnlyList<AddMobyTemplate> LoadCrossLevelObjectTemplates()
    {
        string path = _workspace.ResolveFile("spyro-object-templates.json");
        if (!File.Exists(path))
            return Array.Empty<AddMobyTemplate>();

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("templates", out JsonElement templatesElement) ||
                templatesElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<AddMobyTemplate>();

            List<AddMobyTemplate> templates = new();
            foreach (JsonElement template in templatesElement.EnumerateArray())
            {
                if (!GetJsonBoolean(template, "showInAddList"))
                    continue;

                string displayName = FormatCrossLevelObjectTemplateName(template);
                string sourceLevelKey = GetJsonString(template, "sourceLevelKey");
                string sourceLevelName = GetJsonString(template, "sourceLevelName", "another level");
                int sourceTrueIndex = GetJsonInt32(template, "sourceTrueIndex", -1);
                string id = GetJsonString(template, "id", displayName);
                string family = GetJsonString(template, "family");
                string support = GetJsonString(template, "addSupportStatus", "experimental");
                string requiredFeature = GetJsonString(template, "requiredExporterFeature");
                string risk = GetJsonString(template, "dependencyRisk");
                string note = GetJsonString(template, "note");
                string candidateKind = GetJsonString(template, "kind", displayName);
                CrossLevelTemplateLevelStatus levelStatus = CrossLevelEditorTemplateSupport.ResolveLevelStatus(_currentLevel, sourceLevelKey, family, support, _workspace.RootPath);
                ColorRgba? color = ColorRgba.TryParseHex(GetJsonString(template, "color"), out ColorRgba parsedColor)
                    ? parsedColor
                    : null;

                templates.Add(new AddMobyTemplate(
                    $"From {sourceLevelName}: {displayName} ({levelStatus.ShortLabel})",
                    GetJsonInt32(template, "typeHex", 0x20),
                    GetJsonInt32(template, "stateHex", 0),
                    GetJsonInt32(template, "sourceByte36Hex", 0x53),
                    GetJsonInt32(template, "sourceByte37Hex", 0),
                    GetJsonInt32(template, "sourceByte4FHex", 0),
                    GetJsonInt32(template, "flag4AHex", 0),
                    GetJsonInt32(template, "flag4BHex", 0x53),
                    false,
                    $"New {displayName}",
                    FromCrossLevelTemplate: true,
                    DefaultColor: color,
                    TemplateId: id,
                    Family: family,
                    SourceLevelKey: sourceLevelKey,
                    SourceLevelName: sourceLevelName,
                    SourceTrueIndex: sourceTrueIndex,
                    AddSupportStatus: support,
                    RequiredExporterFeature: requiredFeature,
                    DependencyRisk: risk,
                    TemplateNote: note,
                    CandidateKind: candidateKind,
                    CurrentLevelSupportLabel: levelStatus.FullLabel,
                    CurrentLevelRecipeId: levelStatus.RecipeId,
                    CurrentLevelReady: levelStatus.Ready,
                    CurrentLevelPlaceable: levelStatus.Placeable));
            }

            return templates;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            _statusText.Text = $"Cross-level object templates could not be loaded: {ex.Message}";
            return Array.Empty<AddMobyTemplate>();
        }
    }

    private static string FormatCrossLevelObjectTemplateName(JsonElement template)
    {
        string family = GetJsonString(template, "family");
        string displayName = GetJsonString(template, "displayName", "Object");
        return string.Equals(family, "lockedChest", StringComparison.OrdinalIgnoreCase) &&
            displayName.Contains("unlock", StringComparison.OrdinalIgnoreCase)
            ? "Key Chest"
            : displayName;
    }

    private static string BuildAddMobyTemplateNote(AddMobyTemplate template)
    {
        if (!template.FromCrossLevelTemplate)
        {
            if (!string.IsNullOrWhiteSpace(template.TemplateNote))
                return template.TemplateNote;

            return template.FromLevelTemplate
                ? "This clones a same-level source record, which is the safest object-add path for objects already native to this level."
                : "Objects with simple 0x18/0x20 source records export to the test BIN now. Bigger actors may need actor-package support before they are playable.";
        }

        List<string> lines =
        [
            $"{template.DefaultLabel} comes from {template.SourceLevelName}. Status: {FormatTemplateSupportStatus(template.AddSupportStatus)}."
        ];
        if (!string.IsNullOrWhiteSpace(template.CurrentLevelSupportLabel))
            lines.Add(template.CurrentLevelSupportLabel);
        if (!string.IsNullOrWhiteSpace(template.CurrentLevelRecipeId))
            lines.Add($"Recipe: {template.CurrentLevelRecipeId}.");
        if (!string.IsNullOrWhiteSpace(template.RequiredExporterFeature))
            lines.Add($"Needed exporter feature: {template.RequiredExporterFeature}.");
        if (!string.IsNullOrWhiteSpace(template.DependencyRisk))
            lines.Add(template.DependencyRisk);
        if (!string.IsNullOrWhiteSpace(template.TemplateNote))
            lines.Add(template.TemplateNote);
        return string.Join("\n", lines);
    }

    private static string BuildCrossLevelPatchLead(AddMobyTemplate template)
    {
        string status = FormatTemplateSupportStatus(template.AddSupportStatus);
        string feature = string.IsNullOrWhiteSpace(template.RequiredExporterFeature)
            ? ""
            : $" Required exporter feature: {template.RequiredExporterFeature}.";
        string packageNote = template.AddSupportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase)
            ? template.CurrentLevelReady
                ? " Create BIN will include this because the current level has a mapped package recipe."
                : template.CurrentLevelPlaceable
                ? " Normal Create BIN keeps this guarded; use Advanced > Create Candidate BIN for a disposable test."
                : " Create BIN will include this only when the current level has a mapped package recipe."
            : "";
        string levelNote = string.IsNullOrWhiteSpace(template.CurrentLevelSupportLabel)
            ? ""
            : $" {template.CurrentLevelSupportLabel}";
        return $"Added from {template.SourceLevelName} cross-level template {template.TemplateId}. Status: {status}.{feature}{packageNote}{levelNote}";
    }

    private static string FormatTemplateSupportStatus(string status)
    {
        return status switch
        {
            "supported-lightweight-object" => "lightweight object support",
            "experimental-actor-package-swap" => "experimental actor package support",
            "experimental-actor-package-import" => "experimental actor package import",
            "experimental-image-write" => "experimental test BIN writer",
            "" => "unknown",
            _ => status.Replace('-', ' ')
        };
    }

    private static IEnumerable<AddMobyTemplate> BuildLevelObjectTemplates(IEnumerable<Moby> currentMobys)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (Moby moby in currentMobys
            .Where(moby => !moby.IsRemoved && !moby.IsAdded && moby.Type is 0x18 or 0x20)
            .OrderBy(moby => NativeTemplateRank(moby))
            .ThenBy(moby => moby.Type)
            .ThenBy(moby => moby.DisplayLabel)
            .ThenBy(moby => moby.TrueIndex))
        {
            string label = string.IsNullOrWhiteSpace(moby.DisplayLabel) ? Moby.FallbackLabel(moby.Type) : moby.DisplayLabel;
            string key = $"{moby.Type:X2}:{moby.State:X2}:{moby.SourceByte36:X2}:{moby.SourceByte4F:X2}:{moby.Flag4B:X2}:{label}";
            if (!seen.Add(key))
                continue;

            GemValue? defaultGem = moby.IsGemLike && moby.Gem != GemValue.Unknown ? moby.Gem : null;
            int rank = NativeTemplateRank(moby);
            yield return new AddMobyTemplate(
                BuildNativeTemplateName(label, rank),
                moby.Type,
                moby.State,
                moby.SourceByte36,
                moby.SourceByte37,
                moby.SourceByte4F,
                moby.Flag4A,
                moby.Flag4B,
                defaultGem != null,
                $"New {label}",
                FromLevelTemplate: true,
                DefaultGem: defaultGem,
                SourceTrueIndex: moby.TrueIndex,
                Family: NativeTemplateFamily(label),
                CandidateKind: moby.CandidateKind,
                TemplateNote: BuildNativeTemplateNote(label, moby.TrueIndex, rank));

            if (seen.Count >= 32)
                yield break;
        }
    }

    private static int NativeTemplateRank(Moby moby)
    {
        string label = moby.DisplayLabel.ToLowerInvariant();
        if (label.Contains("spring chest"))
            return 0;
        if (label.Contains("key chest") || label.Contains("locked") && label.Contains("chest"))
            return 1;
        if (label.Contains("flame") && label.Contains("chest") || label.Contains("charge chest") || label.Contains("firework chest") || label.Contains("life chest"))
            return 2;
        if (moby.VisualKind == MobyVisualKind.Key)
            return 3;
        if (moby.VisualKind == MobyVisualKind.Gem)
            return 4;
        if (moby.VisualKind == MobyVisualKind.Actor)
            return 5;
        if (moby.VisualKind == MobyVisualKind.Scenery)
            return 6;
        return 9;
    }

    private static string BuildNativeTemplateName(string label, int rank)
    {
        if (rank == 0)
            return $"From this level: {label} (native Spring Chest)";
        if (rank <= 2)
            return $"From this level: {label} (native chest)";
        return $"From this level: {label}";
    }

    private static string StripNewPrefix(string label)
    {
        return label.StartsWith("New ", StringComparison.OrdinalIgnoreCase)
            ? label[4..]
            : label;
    }

    private static string NativeTemplateFamily(string label)
    {
        string lower = label.ToLowerInvariant();
        if (lower.Contains("spring chest"))
            return "springChest";
        if (lower.Contains("key chest") || lower.Contains("locked") && lower.Contains("chest"))
            return "lockedChest";
        if (lower.Contains("chest"))
            return "chest";
        if (lower.Contains("key"))
            return "key";
        return "";
    }

    private static string BuildNativeTemplateNote(string label, int trueIndex, int rank)
    {
        string donor = trueIndex >= 0 ? $" Native donor: T{trueIndex}." : "";
        if (rank == 0)
            return $"Best option for adding a Spring Chest in this level: clone the level's own Spring Chest record and special data instead of importing a cross-level actor package.{donor}";
        if (rank <= 2)
            return $"Best option for adding this chest type in this level: clone the level's own native chest record and special data.{donor}";
        return $"This clones a same-level source record and is safer than importing this object from another level.{donor}";
    }

    private static bool IsNativeSpringChestTemplate(AddMobyTemplate template)
    {
        return template.FromLevelTemplate &&
            (string.Equals(template.Family, "springChest", StringComparison.OrdinalIgnoreCase) ||
             template.DefaultLabel.Contains("Spring Chest", StringComparison.OrdinalIgnoreCase) ||
             template.Name.Contains("Spring Chest", StringComparison.OrdinalIgnoreCase));
    }

    private Control BuildAddMobyDialogContent(Window dialog, ComboBox placementBox, TextBlock placementHint, ComboBox kindBox, ComboBox gemBox, TextBox nameBox, TextBox typeBox, TextBox stateBox, TextBox xBox, TextBox yBox, TextBox zBox, TextBlock note)
    {
        Grid fields = new()
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(92)),
                new ColumnDefinition(GridLength.Star)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };

        AddLabeledField(fields, "Place", BuildPlacementField(placementBox, placementHint), 0);
        AddLabeledField(fields, "Kind", kindBox, 1);
        AddLabeledField(fields, "Gem", gemBox, 2);
        AddLabeledField(fields, "Name", nameBox, 3);
        AddLabeledFieldWithHelp(fields, "Type", typeBox, 4, async () => await ShowMobyTypeStateHelpAsync("Type", typeBox, stateBox));
        AddLabeledFieldWithHelp(fields, "State", stateBox, 5, async () => await ShowMobyTypeStateHelpAsync("State", typeBox, stateBox));
        AddLabeledField(fields, "Position", BuildMobyPositionFields(xBox, yBox, zBox), 6);

        StackPanel panel = new()
        {
            Spacing = 12,
            Margin = new Thickness(16)
        };
        panel.Children.Add(fields);
        panel.Children.Add(note);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button add = NewButton("Add");
        cancel.Click += (_, _) => dialog.Close(false);
        add.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(add);
        panel.Children.Add(buttons);
        return panel;
    }

    private static Control BuildPlacementField(ComboBox placementBox, TextBlock placementHint)
    {
        StackPanel panel = new()
        {
            Spacing = 6
        };
        panel.Children.Add(placementBox);
        panel.Children.Add(placementHint);
        return panel;
    }

    private void RemoveSelectedMoby()
    {
        if (_selectedMoby == null)
        {
            _statusText.Text = "Select an object before removing it.";
            return;
        }

        _lastRemovedMoby = _selectedMoby;
        if (_selectedMoby.IsAdded)
            _currentMobys.Remove(_selectedMoby);
        else
            _selectedMoby.IsRemoved = true;

        Moby? next = _currentMobys.FirstOrDefault(moby => !moby.IsRemoved);
        _selectedMoby = null;
        _viewport.Mobys = _currentMobys;
        RefreshMobyList(next);
        RefreshCurrentLevelDetails();
        if (next != null)
            _viewport.SelectMoby(next, true);
        else
            _viewport.ResetSelection();

        _statusText.Text = "Removed object from this level edit.";
    }

    private void UndoLastRemovedMoby()
    {
        if (_lastRemovedMoby == null)
        {
            _statusText.Text = "No recently removed object to restore.";
            return;
        }

        Moby moby = _lastRemovedMoby;
        if (!_currentMobys.Any(item => ReferenceEquals(item, moby)))
            _currentMobys.Add(moby);

        moby.IsRemoved = false;
        _lastRemovedMoby = null;
        _viewport.Mobys = _currentMobys;
        RefreshMobyList(moby);
        RefreshCurrentLevelDetails();
        _viewport.SelectMoby(moby, true);
        _statusText.Text = $"Restored {moby.DisplayLabel}.";
    }

    private void UndoMoby(Moby moby)
    {
        if (moby.IsAdded)
        {
            _currentMobys.Remove(moby);
            _selectedMoby = null;
            _viewport.Mobys = _currentMobys;
            RefreshMobyList();
            RefreshCurrentLevelDetails();
            _viewport.ResetSelection();
            _statusText.Text = "Undid newly added object.";
            return;
        }

        int linkedPositionUndos = UndoLinkedPositionEdits(moby);
        moby.UndoEdits();
        _viewport.InvalidateVisual();
        RefreshMobyList(moby);
        RefreshCurrentLevelDetails();
        _viewport.SelectMoby(moby, true);
        _statusText.Text = linkedPositionUndos > 0
            ? $"Undid edits for {moby.DisplayLabel} and restored {linkedPositionUndos} linked position(s)."
            : $"Undid edits for {moby.DisplayLabel}.";
    }

    private int UndoLinkedPositionEdits(Moby moby)
    {
        if (!moby.HasPositionEdit)
            return 0;

        List<Moby> linked = GetLinkedMoveMobys(moby)
            .Where(item => !ReferenceEquals(item, moby) && !item.IsAdded && item.HasPositionEdit)
            .ToList();
        foreach (Moby item in linked)
            UndoPositionOnly(item);
        return linked.Count;
    }

    private static void UndoPositionOnly(Moby moby)
    {
        moby.Position = moby.OriginalPosition;
        if (!moby.HasMetadataEdit && !moby.IsRemoved)
        {
            moby.HasLoadedNativeEdit = false;
            moby.LoadedNativeEditSummary = "";
        }
    }

    private async Task EditMobyAsync(Moby moby)
    {
        TextBox nameBox = new() { Text = moby.Label, MinWidth = 240 };
        TextBox typeBox = new() { Text = $"0x{moby.Type:X2}", MinWidth = 120 };
        TextBox stateBox = new() { Text = $"0x{moby.State:X2}", MinWidth = 120 };
        TextBox xBox = NewPositionBox(moby.Position.X);
        TextBox yBox = NewPositionBox(moby.Position.Y);
        TextBox zBox = NewPositionBox(moby.Position.Z);
        bool editsRewardGem = IsRewardGemCarrier(moby);
        ComboBox? gemBox = HasEditableGemValue(moby) ? BuildMobyGemEditBox(moby, nameBox, editsRewardGem) : null;
        List<ChestContentEditorRow> chestRows = GetChestContentMobys(moby)
            .Select(BuildChestContentEditorRow)
            .ToList();
        List<AddMobyTemplate> transformTemplates = BuildMobyTransformTemplates(moby).ToList();
        ComboBox? transformBox = null;
        TextBlock? transformNote = null;
        if (transformTemplates.Count > 1)
        {
            transformBox = new ComboBox
            {
                ItemsSource = transformTemplates,
                SelectedIndex = 0,
                MinWidth = 260
            };
            transformNote = NewSmallNote("Leave this on Keep Current unless you want to turn the selected object into a ready cross-level transform.");
            transformBox.SelectionChanged += (_, _) =>
            {
                AddMobyTemplate selected = transformBox.SelectedItem as AddMobyTemplate ?? transformTemplates[0];
                transformNote.Text = selected.FromCrossLevelTemplate || selected.FromLevelTemplate
                    ? BuildAddMobyTemplateNote(selected)
                    : "Keeps this object's current identity bytes.";
                if (selected.FromCrossLevelTemplate || selected.FromLevelTemplate)
                {
                    typeBox.Text = $"0x{selected.Type:X2}";
                    stateBox.Text = $"0x{selected.State:X2}";
                    if (string.IsNullOrWhiteSpace(nameBox.Text) || string.Equals(nameBox.Text, moby.DisplayLabel, StringComparison.Ordinal))
                        nameBox.Text = selected.DefaultLabel;
                }
            };
        }

        Window dialog = new()
        {
            Title = "Edit Object",
            Width = chestRows.Count > 0 ? 720 : 620,
            Height = chestRows.Count > 0 ? 760 : 680,
            MinHeight = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildMobyEditDialogContent(dialog, nameBox, typeBox, stateBox, xBox, yBox, zBox, gemBox, chestRows, transformBox, transformNote));

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted)
            return;

        int linkedMoveCount = ApplyExactMobyPositionEdit(moby, xBox, yBox, zBox);
        if (TryParseByte(typeBox.Text, out int type))
            moby.SetType(type);
        if (TryParseByte(stateBox.Text, out int state))
            moby.State = state;
        AddMobyTemplate? transformTemplate = transformBox?.SelectedItem as AddMobyTemplate;
        if (transformTemplate?.FromLevelTemplate == true)
            ApplyLevelTemplateToExistingMoby(moby, transformTemplate);
        else if (transformTemplate?.FromCrossLevelTemplate == true)
            ApplyCrossLevelTemplateToExistingMoby(moby, transformTemplate);
        GemValue editedGem = GemValue.Unknown;
        bool changedGemBytes = false;
        if (transformTemplate?.FromCrossLevelTemplate != true && transformTemplate?.FromLevelTemplate != true && gemBox?.SelectedItem is GemValue selectedGem)
        {
            editedGem = selectedGem;
            changedGemBytes = editsRewardGem
                ? moby.Flag4B != selectedGem.IdByte
                : moby.IsChestContent
                ? moby.Flag4B != selectedGem.IdByte
                : moby.SourceByte36 != selectedGem.IdByte || moby.SourceByte4F != selectedGem.ValueByte;
            if (editsRewardGem)
            {
                moby.Flag4B = selectedGem.IdByte;
            }
            else
            {
                moby.ApplyGem(selectedGem);
            }
        }
        moby.Label = string.IsNullOrWhiteSpace(nameBox.Text) ? Moby.FallbackLabel(moby.Type) : nameBox.Text.Trim();
        int removedChestContents = 0;
        foreach (ChestContentEditorRow row in chestRows)
        {
            if (ApplyChestContentRow(row))
                removedChestContents++;
        }
        if (chestRows.Count > 0)
            RefreshChestContentLink(moby);

        if (changedGemBytes && editedGem != GemValue.Unknown)
        {
            moby.HasLoadedNativeEdit = true;
            moby.LoadedNativeEditSummary = editsRewardGem
                ? $"{editedGem.DisplayName} reward byte"
                : $"{editedGem.DisplayName} source bytes";
        }
        _viewport.InvalidateVisual();
        RefreshMobyList(moby);
        RefreshCurrentLevelDetails();
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(moby));
        _statusText.Text = removedChestContents > 0
            ? $"Updated {moby.DisplayLabel} and removed {removedChestContents} chest content marker(s)."
            : linkedMoveCount > 1
            ? $"Updated {moby.DisplayLabel} and moved {linkedMoveCount - 1} linked object(s)."
            : $"Updated {moby.DisplayLabel}.";
    }

    private IEnumerable<AddMobyTemplate> BuildMobyTransformTemplates(Moby moby)
    {
        yield return new AddMobyTemplate(
            "Keep current object",
            moby.Type,
            moby.State,
            moby.SourceByte36,
            moby.SourceByte37,
            moby.SourceByte4F,
            moby.Flag4A,
            moby.Flag4B,
            false,
            moby.DisplayLabel);

        foreach (AddMobyTemplate template in BuildLevelObjectTemplates(_currentMobys))
        {
            if (template.SourceTrueIndex < 0 || template.SourceTrueIndex == moby.TrueIndex)
                continue;

            string label = StripNewPrefix(template.DefaultLabel);
            yield return template with
            {
                Name = $"Replace slot with this level: {label} (donor T{template.SourceTrueIndex})",
                DefaultLabel = label,
                AddSupportStatus = "native-slot-reuse",
                RequiredExporterFeature = "CloneSourceRecordIntoSlot",
                TemplateNote = $"Reuses this existing object slot by cloning the full same-level source record from T{template.SourceTrueIndex}, while preserving this slot's placed position. This is the safer enemy/object route; true-add enemies still need linked behavior data."
            };
        }

        if (_releaseMode)
            yield break;

        foreach (AddMobyTemplate template in LoadCrossLevelObjectTemplates())
        {
            if (template.CurrentLevelReady && CrossLevelEditorTemplateSupport.CanTransform(moby.VisualKind, template.Family))
                yield return template;
        }
    }

    private void ApplyLevelTemplateToExistingMoby(Moby moby, AddMobyTemplate template)
    {
        moby.SetType(template.Type);
        moby.State = template.State;
        moby.SourceByte36 = template.SourceByte36;
        moby.SourceByte37 = template.SourceByte37;
        moby.SourceByte4F = template.SourceByte4F;
        moby.Flag4A = template.Flag4A;
        moby.Flag4B = template.Flag4B;
        moby.Label = template.DefaultLabel;
        moby.Color = template.DefaultColor ?? Moby.ColorForType(template.Type);
        moby.CandidateKind = template.CandidateKind;
        moby.Confidence = "native-slot-reuse";
        moby.Evidence = $"Changed to same-level donor T{template.SourceTrueIndex}.";
        moby.PatchStatus = "native-slot-reuse";
        moby.PatchLead = $"Clone same-level donor T{template.SourceTrueIndex} into existing slot T{moby.TrueIndex}, preserving this slot's placed position.";
        moby.CrossLevelTemplateId = "";
        moby.CrossLevelFamily = "";
        moby.CrossLevelSourceLevelKey = "";
        moby.CrossLevelSourceLevelName = "";
        moby.CrossLevelSourceTrueIndex = -1;
        moby.CrossLevelRequiredExporterFeature = "";
        moby.SourceCloneLevelKey = _currentLevel?.Key ?? "";
        moby.SourceCloneLevelName = _currentLevel?.DisplayName ?? "";
        moby.SourceCloneTrueIndex = template.SourceTrueIndex;
    }

    private static void ApplyCrossLevelTemplateToExistingMoby(Moby moby, AddMobyTemplate template)
    {
        moby.SetType(template.Type);
        moby.State = template.State;
        moby.SourceByte36 = template.SourceByte36;
        moby.SourceByte37 = template.SourceByte37;
        moby.SourceByte4F = template.SourceByte4F;
        moby.Flag4A = template.Flag4A;
        moby.Flag4B = template.Flag4B;
        moby.Label = template.DefaultLabel;
        moby.Color = template.DefaultColor ?? Moby.ColorForType(template.Type);
        moby.CandidateKind = template.CandidateKind;
        moby.Confidence = "cross-level-template";
        moby.Evidence = $"Changed to {template.SourceLevelName} template {template.TemplateId}.";
        moby.PatchStatus = template.AddSupportStatus;
        moby.PatchLead = BuildCrossLevelPatchLead(template);
        moby.CrossLevelTemplateId = template.TemplateId;
        moby.CrossLevelFamily = template.Family;
        moby.CrossLevelSourceLevelKey = template.SourceLevelKey;
        moby.CrossLevelSourceLevelName = template.SourceLevelName;
        moby.CrossLevelSourceTrueIndex = template.SourceTrueIndex;
        moby.CrossLevelRequiredExporterFeature = template.RequiredExporterFeature;
        moby.SourceCloneLevelKey = "";
        moby.SourceCloneLevelName = "";
        moby.SourceCloneTrueIndex = -1;
    }

    private int ApplyExactMobyPositionEdit(Moby moby, TextBox xBox, TextBox yBox, TextBox zBox)
    {
        if (!TryParseFloat(xBox.Text, out float x) ||
            !TryParseFloat(yBox.Text, out float y) ||
            !TryParseFloat(zBox.Text, out float z))
            return 1;

        float dx = x - moby.Position.X;
        float dy = y - moby.Position.Y;
        float dz = z - moby.Position.Z;
        if (Math.Abs(dx) <= 0.001f && Math.Abs(dy) <= 0.001f && Math.Abs(dz) <= 0.001f)
            return 1;

        List<Moby> moved = GetLinkedMoveMobys(moby).ToList();
        bool snapToTerrain = Math.Abs(dz) <= 0.001f && (Math.Abs(dx) > 0.001f || Math.Abs(dy) > 0.001f);
        foreach (Moby item in moved)
            MoveMoby(item, dx, dy, dz, snapToTerrain);
        return moved.Count;
    }

    private static TextBox NewPositionBox(float value)
    {
        return new TextBox
        {
            Text = FormatPosition(value),
            MinWidth = 90
        };
    }

    private static string FormatPosition(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private Control BuildMobyEditDialogContent(Window dialog, TextBox nameBox, TextBox typeBox, TextBox stateBox, TextBox xBox, TextBox yBox, TextBox zBox, ComboBox? gemBox, IReadOnlyList<ChestContentEditorRow> chestRows, ComboBox? transformBox, TextBlock? transformNote)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(SectionLabel("Name"));
        panel.Children.Add(nameBox);
        if (transformBox != null && transformNote != null)
        {
            panel.Children.Add(SectionLabel("Change To"));
            panel.Children.Add(transformBox);
            panel.Children.Add(transformNote);
        }
        panel.Children.Add(SectionLabelWithHelp("Type", async () => await ShowMobyTypeStateHelpAsync("Type", typeBox, stateBox)));
        panel.Children.Add(typeBox);
        panel.Children.Add(SectionLabelWithHelp("State", async () => await ShowMobyTypeStateHelpAsync("State", typeBox, stateBox)));
        panel.Children.Add(stateBox);
        panel.Children.Add(SectionLabel("Position"));
        panel.Children.Add(BuildMobyPositionFields(xBox, yBox, zBox));
        if (gemBox != null)
        {
            panel.Children.Add(SectionLabel("Gem Value"));
            panel.Children.Add(gemBox);
            panel.Children.Add(NewSmallNote("Changing this writes the gem ID/value bytes used by Save Edits and Create Test BIN."));
        }
        if (chestRows.Count > 0)
        {
            panel.Children.Add(SectionLabel("Chest Contents"));
            panel.Children.Add(NewSmallNote(BuildGemSummary(chestRows.Select(row => row.Moby))));
            foreach (ChestContentEditorRow row in chestRows)
                panel.Children.Add(BuildChestContentRow(row));
        }

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply");
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        panel.Children.Add(buttons);
        return panel;
    }

    private async Task ShowHelpAsync()
    {
        Window dialog = new()
        {
            Title = "Help",
            Width = 620,
            Height = 700,
            MinWidth = 520,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = ScrollableDialogContent(BuildHelpDialogContent(dialog));
        await dialog.ShowDialog(this);
    }

    private Control BuildHelpDialogContent(Window dialog)
    {
        StackPanel panel = new()
        {
            Spacing = 14,
            Margin = new Thickness(18)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Spyro Editor Controls",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        });

        AddHelpSection(panel, "Start", [
            "Open BIN/CUE: choose the Spyro disc image you want this editor session to use.",
            "Level list: choose the level to load objects and map data from that disc.",
            "Create BIN: build one patched test disc from all saved level edits."
        ]);

        AddHelpSection(panel, "View", [
            "Fit View: re-center and zoom the current level.",
            "Map View: flat editor map with selectable objects.",
            "Fly 3D: move through the level in a 3D editor view.",
            "Game View: flips the map orientation to match the in-game view."
        ]);

        AddHelpSection(panel, "Mouse", [
            "Left click: select an object.",
            "Left drag: move the selected object.",
            "Double click: edit the object under the cursor.",
            "Right drag or middle drag: pan the map or look around in Fly 3D.",
            "Mouse wheel: zoom the map or move forward/back in Fly 3D."
        ]);

        AddHelpSection(panel, "Fly 3D", [
            "Windows and Mac: W/A/S/D move forward, left, back, and right.",
            "Windows and Mac: Q/E move up and down.",
            "Windows and Mac: arrow keys turn and tilt the camera."
        ]);

        AddHelpSection(panel, "Objects", [
            "Add Object: choose the object kind, press Add, then click the map or Fly 3D view where it should go.",
            "Copy Object: copies the currently selected object's editable data.",
            "Paste Object: creates a new object from the copied data at the cursor.",
            "Edit Object: change name, type, state, gem values, or position.",
            "Remove Object: stages the selected object for removal.",
            "Undo Object: clears staged changes on the selected object."
        ]);

        AddHelpSection(panel, "Keyboard", [
            "Windows: Ctrl+C copies the selected object.",
            "Windows: Ctrl+V pastes the copied object at the cursor.",
            "Mac: Command+C copies the selected object.",
            "Mac: Command+V pastes the copied object at the cursor.",
            "Escape: cancels Add Object placement."
        ]);

        if (!_releaseMode)
        {
            AddHelpSection(panel, "Advanced Terrain", [
                "Number keys choose terrain brush modes.",
                "Shift-drag raises or lowers selected terrain.",
                "Alt/Option-drag copies selected terrain.",
                "C/V without Ctrl or Command copies and pastes terrain face looks."
            ]);
        }

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button close = NewButton("Close");
        close.Click += (_, _) => dialog.Close();
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        return panel;
    }

    private static void AddHelpSection(StackPanel panel, string title, IReadOnlyList<string> lines)
    {
        Border section = new()
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 225, 232)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 252))
        };
        StackPanel content = new() { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        });
        foreach (string line in lines)
            content.Children.Add(NewHelpLine(line));
        section.Child = content;
        panel.Children.Add(section);
    }

    private static TextBlock NewHelpLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 19,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
    }

    private async Task ShowMobyTypeStateHelpAsync(string focus, TextBox typeBox, TextBox stateBox)
    {
        int type = TryParseByte(typeBox.Text, out int parsedType) ? parsedType : -1;
        int state = TryParseByte(stateBox.Text, out int parsedState) ? parsedState : -1;
        TextBlock text = new()
        {
            Text = BuildMobyTypeStateHelpText(focus, type, state),
            TextWrapping = TextWrapping.Wrap,
            FontFamily = FontFamily.Parse("Menlo, Consolas, monospace"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(224, 232, 240)),
            Margin = new Thickness(16)
        };
        Window dialog = new()
        {
            Title = $"{focus} Help",
            Width = 820,
            Height = 680,
            MinHeight = 520,
            Background = new SolidColorBrush(Color.FromRgb(19, 23, 29)),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        Button close = NewButton("Close");
        close.Click += (_, _) => dialog.Close();
        StackPanel panel = new()
        {
            Spacing = 10
        };
        panel.Children.Add(text);
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16)
        };
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        dialog.Content = ScrollableDialogContent(panel);
        await dialog.ShowDialog(this);
    }

    private string BuildMobyTypeStateHelpText(string focus, int type, int state)
    {
        IReadOnlyList<KnownMobyEntity> entities = LoadKnownMobyEntities();
        StringBuilder builder = new();
        builder.AppendLine($"{focus} reference");
        builder.AppendLine();
        builder.AppendLine(type >= 0 && state >= 0
            ? $"Selected values: type 0x{type:X2}, state 0x{state:X2}"
            : "Selected values: could not read one of the fields.");
        builder.AppendLine();

        List<KnownMobyEntity> exact = entities
            .Where(entity => entity.Type == type && entity.State == state)
            .OrderBy(entity => entity.LevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entity => entity.Label, StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .ToList();
        builder.AppendLine("Known entities with this exact type/state:");
        if (exact.Count == 0)
        {
            builder.AppendLine("  No cached examples found.");
        }
        else
        {
            foreach (KnownMobyEntity entity in exact)
                builder.AppendLine($"  {entity.LevelName,-18} T{entity.TrueIndex,3}  {entity.Label}  b36=0x{entity.SourceByte36:X2} f4A=0x{entity.Flag4A:X2} f4B=0x{entity.Flag4B:X2}");
            if (entities.Count(entity => entity.Type == type && entity.State == state) > exact.Count)
                builder.AppendLine("  ...more cached examples exist.");
        }

        builder.AppendLine();
        builder.AppendLine(type >= 0
            ? $"Known states for type 0x{type:X2}:"
            : "Known type/state combinations:");
        IEnumerable<IGrouping<string, KnownMobyEntity>> stateGroups = entities
            .Where(entity => type < 0 || entity.Type == type)
            .GroupBy(entity => $"{entity.Type:X2}:{entity.State:X2}")
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(80);
        foreach (IGrouping<string, KnownMobyEntity> group in stateGroups)
        {
            KnownMobyEntity sample = group.First();
            string labels = string.Join(", ", group
                .Select(entity => entity.Label)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4));
            builder.AppendLine($"  type 0x{sample.Type:X2} state 0x{sample.State:X2}  {group.Count(),4} seen  {labels}");
        }

        builder.AppendLine();
        builder.AppendLine("Note: type/state alone does not fully identify an object. Source bytes such as b36, f4A, and f4B often decide the actual model or behavior.");
        return builder.ToString();
    }

    private IReadOnlyList<KnownMobyEntity> LoadKnownMobyEntities()
    {
        List<KnownMobyEntity> entities = new();
        foreach (LevelDefinition level in _catalog.Levels)
        {
            IEnumerable<Moby> mobys;
            if (_currentLevel != null && string.Equals(level.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
            {
                mobys = _currentMobys;
            }
            else
            {
                string path = Path.Combine(_workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
                if (!File.Exists(path))
                    continue;

                try
                {
                    List<Moby> cached = MobyLoader.LoadCached(path).ToList();
                    MobyMetadataEnricher.Apply(_workspace, level.Key, cached);
                    mobys = cached;
                }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
                {
                    continue;
                }
            }

            foreach (Moby moby in mobys)
            {
                if (moby.IsRemoved)
                    continue;

                entities.Add(new KnownMobyEntity(
                    LevelName: level.DisplayName,
                    TrueIndex: moby.TrueIndex,
                    Label: moby.DisplayLabel,
                    Type: moby.Type,
                    State: moby.State,
                    SourceByte36: moby.SourceByte36,
                    Flag4A: moby.Flag4A,
                    Flag4B: moby.Flag4B));
            }
        }

        return entities;
    }

    private static bool HasEditableGemValue(Moby moby)
    {
        return moby.IsGemLike || IsRewardGemCarrier(moby);
    }

    private static bool IsRewardGemCarrier(Moby moby)
    {
        return !moby.IsLockedChestShell && !moby.IsGemLike && moby.RewardGem != GemValue.Unknown;
    }

    private static GemValue EditableGemValue(Moby moby)
    {
        if (moby.IsGemLike && moby.Gem != GemValue.Unknown)
            return moby.Gem;

        return moby.RewardGem != GemValue.Unknown ? moby.RewardGem : GemValue.Red;
    }

    private static ComboBox BuildMobyGemEditBox(Moby moby, TextBox nameBox, bool editsRewardGem)
    {
        GemValue currentGem = EditableGemValue(moby);
        string originalName = nameBox.Text ?? "";
        ComboBox gemBox = new()
        {
            ItemsSource = GemValue.Known,
            SelectedItem = GemValue.Known.FirstOrDefault(gem => gem.IdByte == currentGem.IdByte),
            MinWidth = 170
        };
        if (gemBox.SelectedItem == null)
            gemBox.SelectedIndex = 0;

        gemBox.SelectionChanged += (_, _) =>
        {
            if (gemBox.SelectedItem is not GemValue gem)
                return;

            string currentName = nameBox.Text ?? "";
            bool shouldUpdateName = !editsRewardGem && (string.IsNullOrWhiteSpace(currentName) ||
                string.Equals(currentName, originalName, StringComparison.Ordinal) ||
                GemValue.Known.Any(known =>
                    string.Equals(currentName, known.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentName, known.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentName, $"New {known.Name}", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentName, $"Locked chest content: {known.DisplayName}", StringComparison.OrdinalIgnoreCase)));
            if (!shouldUpdateName)
                return;

            nameBox.Text = moby.IsChestContent
                ? $"Locked chest content: {gem.DisplayName}"
                : gem.DisplayName;
        };

        return gemBox;
    }

    private static Control BuildMobyPositionFields(TextBox xBox, TextBox yBox, TextBox zBox)
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 4
        };

        AddGridControl(grid, NewFieldLabel("X"), 0, 0);
        AddGridControl(grid, NewFieldLabel("Y"), 1, 0);
        AddGridControl(grid, NewFieldLabel("Z"), 2, 0);
        AddGridControl(grid, xBox, 0, 1);
        AddGridControl(grid, yBox, 1, 1);
        AddGridControl(grid, zBox, 2, 1);
        return grid;
    }

    private static TextBlock NewFieldLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12
        };
    }

    private static TextBlock NewSmallNote(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static ChestContentEditorRow BuildChestContentEditorRow(Moby moby)
    {
        ComboBox gemBox = new()
        {
            ItemsSource = GemValue.Known,
            SelectedItem = GemValue.Known.FirstOrDefault(gem => gem.IdByte == moby.Gem.IdByte),
            MinWidth = 150
        };
        if (gemBox.SelectedItem == null)
            gemBox.SelectedIndex = 0;

        TextBox labelBox = new()
        {
            Text = moby.Label,
            MinWidth = 220
        };
        CheckBox removeBox = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        return new ChestContentEditorRow(moby, labelBox, gemBox, removeBox);
    }

    private static Control BuildChestContentRow(ChestContentEditorRow row)
    {
        Grid grid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(54)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(160)),
                new ColumnDefinition(new GridLength(82))
            },
            ColumnSpacing = 8
        };
        TextBlock index = new()
        {
            Text = $"T{row.Moby.TrueIndex}",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92))
        };
        grid.Children.Add(index);
        AddGridControl(grid, row.LabelBox, 1, 0);
        AddGridControl(grid, row.GemBox, 2, 0);
        AddGridControl(grid, new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                row.RemoveBox,
                new TextBlock
                {
                    Text = "Remove",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
                    FontSize = 12
                }
            }
        }, 3, 0);
        return grid;
    }

    private bool ApplyChestContentRow(ChestContentEditorRow row)
    {
        if (row.RemoveBox.IsChecked == true)
        {
            _lastRemovedMoby = row.Moby;
            if (row.Moby.IsAdded)
                _currentMobys.Remove(row.Moby);
            else
                row.Moby.IsRemoved = true;
            return true;
        }

        if (row.GemBox.SelectedItem is GemValue gem)
            row.Moby.ApplyGem(gem);

        if (!string.IsNullOrWhiteSpace(row.LabelBox.Text))
            row.Moby.Label = row.LabelBox.Text.Trim();
        row.Moby.HasLoadedNativeEdit = true;
        row.Moby.LoadedNativeEditSummary = $"Chest content {row.Moby.Gem.DisplayName}";
        return false;
    }

    private Vector3f NewMobyDefaultPosition()
    {
        return BuildAddMobyPlacementOptions()[0].Position;
    }

    private IReadOnlyList<MobyPlacementOption> BuildAddMobyPlacementOptions()
    {
        List<MobyPlacementOption> options = new();
        Vector3f clickFallback = _selectedMoby != null
            ? SnapOrNearestTerrain(_selectedMoby.Position)
            : _selectedTerrain != null
                ? TerrainCenterPosition(_selectedTerrain)
                : _currentGeometry?.Polygons.Count > 0
                    ? LevelCenterPlacement()
                    : new Vector3f(0, 0, 0);
        options.Add(new MobyPlacementOption(
            "Click in view after Add",
            clickFallback,
            "After pressing Add, click the map or Fly 3D view where this object should go.",
            PlaceAfterDialog: true));

        if (_selectedTerrain != null)
        {
            options.Add(new MobyPlacementOption(
                "Selected terrain face",
                TerrainCenterPosition(_selectedTerrain),
                "Adds the object on the selected terrain face."));
        }

        if (_selectedMoby != null)
        {
            Vector3f nearSelected = SnapOrNearestTerrain(new Vector3f(
                _selectedMoby.Position.X + 64,
                _selectedMoby.Position.Y,
                _selectedMoby.Position.Z));
            options.Add(new MobyPlacementOption(
                "Near selected object",
                nearSelected,
                "Adds the object beside the selected object, snapped to nearby terrain."));

            options.Add(new MobyPlacementOption(
                "Same spot as selected object",
                SnapOrNearestTerrain(_selectedMoby.Position),
                "Adds the object at the selected object's current ground position."));
        }

        if (_currentGeometry?.Polygons.Count > 0)
        {
            options.Add(new MobyPlacementOption(
                "Level center ground",
                LevelCenterPlacement(),
                "Adds the object on the nearest usable terrain around the level center."));
        }

        Vector3f exactPosition = options.Count == 0 ? new Vector3f(0, 0, 0) : options[0].Position;
        options.Add(new MobyPlacementOption(
            "Exact XYZ below",
            exactPosition,
            "Use the coordinate boxes below. Exact placement is not auto-snapped.",
            SnapOnAdd: false));
        return options;
    }

    private Vector3f LevelCenterPlacement()
    {
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return new Vector3f(0, 0, 0);

        Rect2f bounds = _currentGeometry.Bounds;
        float x = bounds.IsEmpty ? _currentGeometry.Polygons.Average(polygon => polygon.Center.X) : (bounds.Left + bounds.Right) * 0.5f;
        float y = bounds.IsEmpty ? _currentGeometry.Polygons.Average(polygon => polygon.Center.Y) : (bounds.Top + bounds.Bottom) * 0.5f;
        float z = (_currentGeometry.MinZ + _currentGeometry.MaxZ) * 0.5f;
        return SnapOrNearestTerrain(new Vector3f(x, y, z));
    }

    private Vector3f SnapClickedOrNearestTerrain(Vector3f preferred, int preferredTerrainIndex)
    {
        if (preferredTerrainIndex >= 0 &&
            TryFindTerrainZAt(preferred.X, preferred.Y, preferred.Z, out float clickedZ, preferredTerrainIndex))
            return new Vector3f(preferred.X, preferred.Y, clickedZ);

        return SnapOrNearestTerrain(preferred);
    }

    private Vector3f SnapOrNearestTerrain(Vector3f preferred, int preferredTerrainIndex = -1, bool preferTopSurface = false)
    {
        if (TryFindTerrainZAt(preferred.X, preferred.Y, preferred.Z, out float snappedZ, preferredTerrainIndex, preferTopSurface))
            return new Vector3f(preferred.X, preferred.Y, snappedZ);

        return TryFindNearestTerrainPosition(preferred, out Vector3f nearest)
            ? nearest
            : preferred;
    }

    private bool TryFindNearestTerrainPosition(Vector3f preferred, out Vector3f position)
    {
        position = preferred;
        if (_currentGeometry == null || _currentGeometry.Polygons.Count == 0)
            return false;

        TerrainPolygon? best = null;
        double bestDistance = double.MaxValue;
        foreach (TerrainPolygon polygon in _currentGeometry.Polygons)
        {
            double distance = DistanceSquared2(preferred.X, preferred.Y, polygon.Center.X, polygon.Center.Y);
            if (distance >= bestDistance)
                continue;

            best = polygon;
            bestDistance = distance;
        }

        if (best == null)
            return false;

        position = TerrainCenterPosition(best);
        return true;
    }

    private static Vector3f TerrainCenterPosition(TerrainPolygon polygon)
    {
        float z = polygon.TryGetZ(polygon.Center.X, polygon.Center.Y, out float terrainZ)
            ? terrainZ
            : polygon.AvgZ;
        return new Vector3f(polygon.Center.X, polygon.Center.Y, z);
    }

    private static double DistanceSquared2(float ax, float ay, float bx, float by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return (dx * dx) + (dy * dy);
    }

    private static void SetPositionBoxes(TextBox xBox, TextBox yBox, TextBox zBox, Vector3f position)
    {
        xBox.Text = FormatPosition(position.X);
        yBox.Text = FormatPosition(position.Y);
        zBox.Text = FormatPosition(position.Z);
    }

    private static bool TryParseByte(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value)
                && value is >= 0 and <= 255;

        return int.TryParse(text, out value) && value is >= 0 and <= 255;
    }

    private static bool TryParseInt(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out value);

        return int.TryParse(text, out value);
    }

    private static bool TryParseFloat(string? text, out float value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text) &&
            float.TryParse(
                text.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
    }

    private static float DistanceSquared(Vector3f a, Vector3f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static string[] BuildSkyboxToolArguments(SkyboxPreset preset, string paletteHex)
    {
        List<string> args = ["-Preset", preset.Id, "-PlanOnly"];
        if (preset.Id == "Custom")
        {
            args.InsertRange(2, ["-PaletteHex", paletteHex]);
        }

        return args.ToArray();
    }

    private static bool IsSafePaletteText(string value)
    {
        string[] tokens = value
            .Split([' ', ',', ';', '|', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 0 && tokens.All(IsHexColor);
    }

    private static bool IsHexColor(string value)
    {
        string text = value.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
            text = text[1..];
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        return text.Length == 6 && text.All(Uri.IsHexDigit);
    }

    private static ColorRgba ScaleColor(ColorRgba color, double amount)
    {
        return ColorRgba.FromRgb(
            Math.Clamp((int)Math.Round(color.R * amount), 0, 255),
            Math.Clamp((int)Math.Round(color.G * amount), 0, 255),
            Math.Clamp((int)Math.Round(color.B * amount), 0, 255));
    }

    private static bool IsSafeExeStringText(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.All(ch => ch is >= ' ' and <= '~');
    }

    private static string ToSafeFileSlug(string value)
    {
        string slug = new string(value
            .ToLowerInvariant()
            .Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-')
            .ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "text" : slug;
    }

    private static string FirstExistingPath(params string?[] paths)
    {
        return paths
            .Select(path => path?.Trim() ?? "")
            .FirstOrDefault(File.Exists) ?? "";
    }

    private static string FirstExistingDiscImagePath(params string?[] paths)
    {
        foreach (string path in paths.Select(path => path?.Trim() ?? "").Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            string imagePath = DiscImageLocator.ResolveImagePath(path);
            if (File.Exists(imagePath))
                return imagePath;
        }

        return "";
    }

    private static bool IsReleaseMode(EditorWorkspace workspace)
    {
        string? value = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        if (!string.IsNullOrWhiteSpace(value))
        {
            return !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        return File.Exists(Path.Combine(workspace.RootPath, "support", "docs", "release-user-guide.md")) &&
            File.Exists(Path.Combine(workspace.RootPath, "support", "spyro-level-catalog.json")) &&
            !Directory.Exists(Path.Combine(workspace.RootPath, "src"));
    }

    private static string ResolveEditorCacheImportFolder(string selectedFolder)
    {
        string fullPath = Path.GetFullPath(selectedFolder);
        if (string.Equals(Path.GetFileName(fullPath), "editor-cache", StringComparison.OrdinalIgnoreCase))
            return fullPath;

        string nested = Path.Combine(fullPath, "editor-cache");
        return Directory.Exists(nested) ? nested : fullPath;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            string targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, targetFile, true);
        }

        foreach (string sourceChild in Directory.EnumerateDirectories(sourceDirectory))
        {
            string targetChild = Path.Combine(targetDirectory, Path.GetFileName(sourceChild));
            if (PathsEqual(sourceChild, targetChild))
                continue;
            CopyDirectory(sourceChild, targetChild);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
    }

    private void RefreshSourceDiscStatus()
    {
        string sourceImage = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        if (File.Exists(sourceImage))
        {
            string cuePath = DiscImageLocator.FindCueForImage(sourceImage);
            _sourceDiscStatusText.Text = File.Exists(cuePath)
                ? $"Disc: {Path.GetFileName(cuePath)}"
                : $"Disc: {Path.GetFileName(sourceImage)}";
            _sourceDiscStatusText.Foreground = new SolidColorBrush(Color.FromRgb(36, 118, 77));
        }
        else
        {
            _sourceDiscStatusText.Text = "No BIN/CUE selected";
            _sourceDiscStatusText.Foreground = new SolidColorBrush(Color.FromRgb(150, 83, 41));
        }
    }

    private static JsonSerializerOptions NewJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    private static string GetJsonString(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return fallback;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? fallback,
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static bool GetJsonBoolean(JsonElement element, string name, bool fallback = false)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return fallback;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(property.GetString(), out bool value) ? value : fallback,
            _ => fallback
        };
    }

    private static float GetJsonSingle(JsonElement element, string name, float fallback)
    {
        return element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Number && property.TryGetSingle(out float value)
            ? value
            : fallback;
    }

    private static bool TryGetJsonArray(JsonElement element, string name, string camelName, out JsonElement array)
    {
        if (element.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array)
            return true;

        if (element.TryGetProperty(camelName, out array) && array.ValueKind == JsonValueKind.Array)
            return true;

        array = default;
        return false;
    }

    private static int FirstJsonInt32(JsonElement element, int fallback, params string[] names)
    {
        foreach (string name in names)
        {
            int value = GetJsonInt32(element, name, int.MinValue);
            if (value != int.MinValue)
                return value;
        }

        return fallback;
    }

    private static double FirstJsonDouble(JsonElement element, double fallback, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
                continue;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out double number))
                return number;

            if (property.ValueKind == JsonValueKind.String &&
                double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                return number;
        }

        return fallback;
    }

    private void RefreshIdentityPriorityHint()
    {
        try
        {
            List<IdentityBatchCandidate> candidates = LoadPriorityIdentityBatchCandidates().ToList();
            _identityPriorityCandidate = SelectIdentityPriorityCandidate(candidates);
            _identityPriorityLoadButton?.SetValue(IsEnabledProperty, _identityPriorityCandidate != null);
            _identityPriorityHint.Text = BuildIdentityPriorityHint(candidates);
            RefreshObjectReadinessHint();
        }
        catch
        {
            _identityPriorityCandidate = null;
            _identityPriorityLoadButton?.SetValue(IsEnabledProperty, false);
            _identityPriorityHint.Text = "ID queue unavailable. Run the smoke report to rebuild the moby identity test list.";
            RefreshObjectReadinessHint();
        }
    }

    private string BuildIdentityPriorityHint(IReadOnlyList<IdentityBatchCandidate> candidates)
    {
        if (_currentLevel == null)
            return "Choose a level to see the next unknown-moby test.";

        int needsIdHere = _currentMobys.Count(moby => !moby.IsRemoved && IsQuestionableMoby(moby));
        if (candidates.Count == 0)
            return needsIdHere == 0
                ? "No generated ID test queue found, and this level has no obvious Needs ID objects."
                : $"{needsIdHere} object(s) in this level still need better labels. Run the smoke report to refresh priority batches.";

        IdentityBatchCandidate? next = SelectIdentityPriorityCandidate(candidates);
        if (next == null)
            return "No generated ID test queue found.";

        string levelName = _catalog.FindByKey(next.LevelKey)?.DisplayName ?? next.LevelKey;
        bool isLocal = string.Equals(next.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase);
        string prefix = isLocal
            ? "This level's next target"
            : $"Next all-level target is {levelName}";
        string localCount = needsIdHere == 0 ? "" : $" {needsIdHere} Needs ID object(s) here.";

        string topFamilyLine = BuildTopIdentityWorkbenchLine(next.LevelKey);
        return $"{prefix}: batch {next.Number}, {next.CurrentLabel}. {next.Impact}{localCount}{topFamilyLine}";
    }

    private string BuildTopIdentityWorkbenchLine(string preferredLevelKey)
    {
        try
        {
            List<MobyIdentityWorkbenchGroup> groups = LoadMobyIdentityWorkbenchGroups().ToList();
            if (groups.Count == 0)
                return "";

            IEnumerable<MobyIdentityWorkbenchGroup> preferred = _currentLevel == null
                ? groups.Where(group => string.Equals(group.LevelKey, preferredLevelKey, StringComparison.OrdinalIgnoreCase))
                : groups.Where(group => string.Equals(group.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
            MobyIdentityWorkbenchGroup? group = preferred
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.LevelName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ??
                groups.OrderByDescending(item => item.Count)
                    .ThenBy(item => item.LevelName, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

            if (group == null)
                return "";

            string sample = group.Samples.FirstOrDefault() is { } first ? $"T{first.TrueIndex}" : "sample not listed";
            string leadText = BuildIdentityWorkbenchLeadLine(group, 3);
            return $"\nTop review family: {group.Count}x {group.CurrentRead} in {group.LevelName}, starting at {sample}.{leadText}";
        }
        catch
        {
            return "";
        }
    }

    private IdentityBatchCandidate? SelectIdentityPriorityCandidate(IReadOnlyList<IdentityBatchCandidate> candidates)
    {
        if (candidates.Count == 0)
            return null;

        if (_currentLevel != null)
        {
            IdentityBatchCandidate? local = candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
            if (local != null)
                return local;
        }

        return candidates[0];
    }

    private void RefreshObjectReadinessHint()
    {
        try
        {
            _objectReadinessHint.Text = BuildObjectReadinessHint();
        }
        catch
        {
            _objectReadinessHint.Text = "Object status unavailable for this level.";
        }
    }

    private string BuildObjectReadinessHint()
    {
        if (_currentLevel == null)
            return "Choose a level to see object totals, treasure, and ID coverage.";

        List<Moby> all = _currentMobys.Where(moby => !moby.IsRemoved).ToList();
        if (all.Count == 0)
            return "No object data is loaded for this level yet.";

        int treasure = all.Sum(moby => moby.TreasureValue);
        int edited = all.Count(moby => moby.HasAnyEdit);
        int needsId = all.Count(IsQuestionableMoby);
        int trusted = all.Count(IsConfirmedMobyIdentity);
        int observed = all.Count(IsObservedMobyIdentity);
        int mapped = all.Count(IsMappedMobyIdentity);
        int inferred = all.Count(IsInferredMobyIdentity);
        int unknown = all.Count(moby => moby.VisualKind == MobyVisualKind.Unknown);

        string categories = FormatObjectCategorySummary(all);
        string reviewLanes = FormatNeedsIdLaneSummary(all);
        string nextTarget = BuildObjectNextIdentityTargetLine();
        string repeatedFamily = BuildLargestQuestionableFamilyLine(all);
        string editText = edited == 0 ? "none edited" : $"{edited} edited";

        List<string> lines =
        [
            $"{all.Count} objects, {treasure} gem value, {editText}.",
            $"Categories: {categories}.",
            $"Identity: {trusted} confirmed, {observed} observed, {mapped} mapped, {inferred} inferred, {needsId} needs ID{(unknown == 0 ? "" : $", {unknown} unknown")}."
        ];

        if (!string.IsNullOrWhiteSpace(reviewLanes))
            lines.Add($"Needs ID lanes: {reviewLanes}.");
        if (!string.IsNullOrWhiteSpace(repeatedFamily))
            lines.Add(repeatedFamily);
        if (!string.IsNullOrWhiteSpace(nextTarget))
            lines.Add(nextTarget);
        string selected = BuildSelectedObjectReadinessLine();
        if (!string.IsNullOrWhiteSpace(selected))
            lines.Add(selected);

        return string.Join('\n', lines);
    }

    private string BuildSelectedObjectReadinessLine()
    {
        if (_selectedMoby == null || _selectedMoby.IsRemoved)
            return "Selected object: none.";

        Moby moby = _selectedMoby;
        string status = BuildMobyIdentityStatus(moby);
        string header = $"Selected object: T{moby.TrueIndex} {moby.DisplayLabel} ({MobyListBadge(moby)}), {status}.";
        if (!IsQuestionableMoby(moby))
            return $"{header} This label is mapped enough for normal editing.";

        int familyCount = CountSameIdentityFingerprintMobys(moby);
        int modelCount = CountSameModelFamilyMobys(moby);
        MobyIdentityFamilyObservation? familyObservation = FindIdentityFamilyObservationForMoby(moby);
        MobyIdentityWorkbenchGroup? group = _currentLevel == null
            ? null
            : FindIdentityWorkbenchGroupForMoby(_currentLevel.Key, moby);
        MobyIdentityModelFamily? modelFamily = _currentLevel == null
            ? null
            : FindMobyIdentityModelFamilyForMoby(_currentLevel.Key, moby);
        string batchStatus = group == null
            ? "No generated ID test was found for this selected family."
            : string.IsNullOrWhiteSpace(group.BatchPath) || !File.Exists(ToWorkspacePath(group.BatchPath))
                ? "This selected family is in the workbench, but has no generated test batch yet."
                : $"ID Test is ready: {group.Count} object(s), {group.CurrentRead}.";
        List<string> suggestions = BuildIdentityObservationSuggestions(moby);
        string suggestionText = suggestions.Count == 0
            ? ""
            : $" Suggested verified names nearby: {string.Join(", ", suggestions.Take(3))}.";
        string familyHint = BuildIdentityFamilyObservationHint(familyObservation);
        string modelHint = BuildIdentityModelFamilyHint(modelFamily, modelCount, familyCount);

        return $"{header} Review family: {familyCount} same-byte object(s); model family: {modelCount} object(s); {MobySourceByteSummary(moby)}. {batchStatus}{modelHint}{suggestionText}{familyHint}";
    }

    private static string BuildIdentityModelFamilyHint(MobyIdentityModelFamily? family, int modelCount, int exactFamilyCount)
    {
        if (family == null)
            return modelCount > exactFamilyCount
                ? " Same model has other reward/state variants in this level."
                : "";

        List<string> parts = new();
        if (family.CurrentReads.Count > 0)
            parts.Add($"Model reads: {string.Join(", ", family.CurrentReads.Take(2))}.");
        if (family.RewardAndBehaviorVariants.Count > 0)
            parts.Add($"Variants: {string.Join("; ", family.RewardAndBehaviorVariants.Take(3))}.");
        if (family.ExactLabelsInFamily.Count > 0)
            parts.Add($"Nearby known labels in model family: {string.Join(", ", family.ExactLabelsInFamily.Take(3))}.");
        if (family.BatchFiles.Count > 0)
            parts.Add($"Model batch: {Path.GetFileName(family.BatchFiles[0])}.");

        return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
    }

    private string BuildIdentityFamilyObservationHint(MobyIdentityFamilyObservation? observation)
    {
        if (observation == null)
            return "";

        List<string> parts = new();
        if (observation.RosterCandidates.Count > 0)
            parts.Add($"Possible roster leads: {string.Join(", ", observation.RosterCandidates.Take(4))}.");
        if (observation.CurrentReads.Count > 0)
            parts.Add($"Current reads: {string.Join(", ", observation.CurrentReads.Take(3))}.");
        if (!string.IsNullOrWhiteSpace(observation.ProofStep))
            parts.Add($"Proof step: {observation.ProofStep}");
        if (!string.IsNullOrWhiteSpace(observation.TestBatch))
            parts.Add($"Batch: {Path.GetFileName(observation.TestBatch)}.");

        return parts.Count == 0 ? "" : " " + string.Join(" ", parts);
    }

    private MobyIdentityFamilyObservation? FindIdentityFamilyObservationForMoby(Moby moby)
    {
        if (_currentLevel == null)
            return null;

        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-identity-family-observations.json");
        if (!File.Exists(path))
            return null;

        string fingerprint = IdentityFingerprint(moby);
        string pointerHex = $"0x{moby.SpecialDataPointer:X8}";
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("observations", out JsonElement observations) ||
                observations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            MobyIdentityFamilyObservation? fallback = null;
            foreach (JsonElement observation in observations.EnumerateArray())
            {
                if (!string.Equals(GetJsonString(observation, "levelKey"), _currentLevel.Key, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetJsonString(observation, "fingerprint"), fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MobyIdentityFamilyObservation parsed = ParseIdentityFamilyObservation(observation);
                int matchTrueIndex = GetJsonInt32(observation, "matchTrueIndex", -1);
                string specialPointer = GetJsonString(observation, "specialDataPointerHex");
                if (matchTrueIndex == moby.TrueIndex ||
                    string.Equals(specialPointer, pointerHex, StringComparison.OrdinalIgnoreCase))
                {
                    return parsed;
                }

                fallback ??= parsed;
            }

            return fallback;
        }
        catch
        {
            return null;
        }
    }

    private MobyIdentityFamilyObservation? FindIdentityFamilyObservationForWorkbenchGroup(MobyIdentityWorkbenchGroup group)
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-identity-family-observations.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("observations", out JsonElement observations) ||
                observations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            MobyIdentityFamilyObservation? fallback = null;
            foreach (JsonElement observation in observations.EnumerateArray())
            {
                if (!string.Equals(GetJsonString(observation, "levelKey"), group.LevelKey, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetJsonString(observation, "fingerprint"), group.Fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MobyIdentityFamilyObservation parsed = ParseIdentityFamilyObservation(observation);
                string specialPointer = GetJsonString(observation, "specialDataPointerHex");
                if (group.Samples.Any(sample => string.Equals(sample.SpecialDataPointer, specialPointer, StringComparison.OrdinalIgnoreCase)))
                    return parsed;

                fallback ??= parsed;
            }

            return fallback;
        }
        catch
        {
            return null;
        }
    }

    private string BuildIdentityWorkbenchLeadLine(MobyIdentityWorkbenchGroup group, int maxLeads)
    {
        MobyIdentityFamilyObservation? observation = FindIdentityFamilyObservationForWorkbenchGroup(group);
        if (observation == null || observation.RosterCandidates.Count == 0)
            return "";

        return $" Possible leads: {string.Join(", ", observation.RosterCandidates.Take(maxLeads))}.";
    }

    private static MobyIdentityFamilyObservation ParseIdentityFamilyObservation(JsonElement observation)
    {
        return new MobyIdentityFamilyObservation(
            ReadJsonStringArray(observation, "currentReads"),
            ReadJsonStringArray(observation, "rosterCandidates"),
            GetJsonString(observation, "proofStep"),
            GetJsonString(observation, "testBatch"));
    }

    private static IReadOnlyList<string> ReadJsonStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return array.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private string BuildObjectNextIdentityTargetLine()
    {
        if (_currentLevel == null || _identityPriorityCandidate == null)
            return "";

        bool isLocal = string.Equals(_identityPriorityCandidate.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase);
        if (!isLocal)
            return "";

        return $"Next ID target: batch {_identityPriorityCandidate.Number}, {_identityPriorityCandidate.CurrentLabel}.";
    }

    private static string BuildLargestQuestionableFamilyLine(IReadOnlyList<Moby> all)
    {
        var family = all
            .Where(IsQuestionableMoby)
            .GroupBy(IdentityFingerprint, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.First().TrueIndex)
            .FirstOrDefault();

        if (family == null)
            return "";

        Moby sample = family.First();
        int count = family.Count();
        string suffix = count > 1
            ? $"{count} matching objects"
            : "single object";
        return $"Largest Needs ID family: {suffix}, {MobyListBadge(sample)} T{sample.TrueIndex} ({IdentityFingerprint(sample)}).";
    }

    private static string FormatObjectCategorySummary(IReadOnlyList<Moby> all)
    {
        (string Label, int Count)[] counts =
        [
            ("gems", all.Count(moby => moby.VisualKind == MobyVisualKind.Gem)),
            ("keys", all.Count(moby => moby.VisualKind == MobyVisualKind.Key)),
            ("chests", all.Count(moby => moby.VisualKind == MobyVisualKind.Chest)),
            ("enemies", all.Count(IsEnemyOrActor)),
            ("flight targets", all.Count(moby => moby.VisualKind == MobyVisualKind.FlightTarget)),
            ("dragons", all.Count(moby => moby.VisualKind == MobyVisualKind.Dragon)),
            ("portals", all.Count(moby => moby.VisualKind == MobyVisualKind.Portal)),
            ("scenery", all.Count(moby => moby.VisualKind == MobyVisualKind.Scenery)),
            ("controls", all.Count(moby => moby.VisualKind == MobyVisualKind.Control || moby.VisualKind == MobyVisualKind.Whirlwind)),
            ("unknown", all.Count(moby => moby.VisualKind == MobyVisualKind.Unknown))
        ];

        return string.Join(", ", counts
            .Where(item => item.Count > 0)
            .Select(item => $"{item.Count} {item.Label}"));
    }

    private static string FormatNeedsIdLaneSummary(IReadOnlyList<Moby> all)
    {
        List<string> lanes = all
            .Where(IsQuestionableMoby)
            .Select(MobyIdentityReviewLane)
            .Where(lane => !string.IsNullOrWhiteSpace(lane))
            .GroupBy(lane => lane, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Count()} {FormatReviewLaneForSentence(group.Key)}")
            .ToList();

        return string.Join(", ", lanes);
    }

    private static string FormatReviewLaneForSentence(string lane)
    {
        return lane switch
        {
            "Actor ID" => "actor/container",
            "Control ID" => "control/helper",
            "Flight ID" => "flight/special",
            "Prop ID" => "scenery/prop",
            "Reward ID" => "reward/interactive",
            _ => lane.ToLowerInvariant()
        };
    }

    private static bool IsTrustedMobyIdentity(string status)
    {
        return status.Equals("Live observed", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Source proven", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Observed", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Mapped", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfirmedMobyIdentity(Moby moby)
    {
        string status = BuildMobyIdentityStatus(moby);
        return IsTrustedMobyIdentity(status) || status.Equals("Template", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsObservedMobyIdentity(Moby moby)
    {
        string status = BuildMobyIdentityStatus(moby);
        return status.Equals("Live observed", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Source proven", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Observed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMappedMobyIdentity(Moby moby)
    {
        return BuildMobyIdentityStatus(moby).Equals("Mapped", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInferredMobyIdentity(Moby moby)
    {
        return IsInferredMobyIdentity(BuildMobyIdentityStatus(moby));
    }

    private static bool IsInferredMobyIdentity(string status)
    {
        return status.Equals("Inferred", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Template", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ShowMobyIdentityFamiliesAsync()
    {
        List<MobyIdentityWorkbenchGroup> groups = LoadMobyIdentityWorkbenchGroups().ToList();
        if (groups.Count == 0)
        {
            _statusText.Text = "No identity workbench data was found. Run the smoke report to rebuild moby ID reports.";
            return;
        }

        bool hasCurrentLevelGroups = _currentLevel != null &&
            groups.Any(group => string.Equals(group.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase));
        ComboBox levelFilter = new()
        {
            ItemsSource = new[] { "This level", "All levels" },
            SelectedIndex = hasCurrentLevelGroups ? 0 : 1,
            MinWidth = 140
        };
        ComboBox laneFilter = new()
        {
            ItemsSource = new[] { "All types", "Actors", "Scenery", "Controls", "Interactive" },
            SelectedIndex = 0,
            MinWidth = 145
        };
        ListBox list = new()
        {
            MinHeight = 390,
            ItemTemplate = new FuncDataTemplate<MobyIdentityWorkbenchGroup>((group, _) => BuildMobyIdentityGroupCard(group))
        };
        TextBlock details = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            MinHeight = 130
        };

        void RefreshDetails()
        {
            if (list.SelectedItem is not MobyIdentityWorkbenchGroup group)
            {
                details.Text = "Select a family to see samples, nearby known objects, and the recommended proof step.";
                return;
            }

            details.Text = BuildMobyIdentityReviewDetails(group);
        }

        void RefreshFilteredList()
        {
            List<MobyIdentityWorkbenchGroup> filtered = groups
                .Where(group => MatchesMobyIdentityLevelFilter(group, levelFilter.SelectedIndex))
                .Where(group => MatchesMobyIdentityLaneFilter(group, laneFilter.SelectedIndex))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.LevelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.CurrentRead, StringComparer.OrdinalIgnoreCase)
                .ToList();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Count == 0 ? -1 : 0;
            RefreshDetails();
        }

        list.SelectionChanged += (_, _) => RefreshDetails();
        levelFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        laneFilter.SelectionChanged += (_, _) => RefreshFilteredList();
        RefreshFilteredList();

        Window dialog = new()
        {
            Title = "Moby ID Families",
            Width = 1020,
            Height = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildMobyIdentityFamiliesDialogContent(dialog, levelFilter, laneFilter, list, details);
        MobyIdentityWorkbenchAction? action = await dialog.ShowDialog<MobyIdentityWorkbenchAction?>(this);
        if (action == null)
            return;

        if (action.Kind == MobyIdentityWorkbenchActionKind.LoadBatch)
            await LoadMobyIdentityWorkbenchBatchAsync(action.Group);
        else
            await SelectMobyIdentityWorkbenchSampleAsync(action.Group);
    }

    private Control BuildMobyIdentityFamiliesDialogContent(Window dialog, ComboBox levelFilter, ComboBox laneFilter, ListBox list, TextBlock details)
    {
        Grid panel = new()
        {
            Margin = new Thickness(16),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 10
        };
        TextBlock title = new()
        {
            Text = "Unresolved moby families",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        };
        panel.Children.Add(title);
        StackPanel filters = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        filters.Children.Add(levelFilter);
        filters.Children.Add(laneFilter);
        Grid.SetRow(filters, 1);
        panel.Children.Add(filters);

        Grid reviewGrid = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1.05, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.95, GridUnitType.Star))
            },
            ColumnSpacing = 12
        };
        Border listFrame = new()
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Child = list
        };
        reviewGrid.Children.Add(listFrame);
        Border detailsFrame = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 249, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                Content = details,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        Grid.SetColumn(detailsFrame, 1);
        reviewGrid.Children.Add(detailsFrame);
        Grid.SetRow(reviewGrid, 2);
        panel.Children.Add(reviewGrid);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button close = NewButton("Close");
        Button sample = NewButton("Select Sample");
        Button batch = NewButton("Load Test Batch");
        close.Click += (_, _) => dialog.Close(null);
        sample.Click += (_, _) =>
        {
            if (list.SelectedItem is MobyIdentityWorkbenchGroup group)
                dialog.Close(new MobyIdentityWorkbenchAction(group, MobyIdentityWorkbenchActionKind.SelectSample));
        };
        batch.Click += (_, _) =>
        {
            if (list.SelectedItem is MobyIdentityWorkbenchGroup group)
                dialog.Close(new MobyIdentityWorkbenchAction(group, MobyIdentityWorkbenchActionKind.LoadBatch));
        };
        buttons.Children.Add(close);
        buttons.Children.Add(batch);
        buttons.Children.Add(sample);
        Grid.SetRow(buttons, 3);
        panel.Children.Add(buttons);
        return panel;
    }

    private Control BuildMobyIdentityGroupCard(MobyIdentityWorkbenchGroup? group)
    {
        if (group == null)
            return new TextBlock();

        StackPanel panel = new()
        {
            Spacing = 3,
            Margin = new Thickness(8, 6)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"{group.Count}x {group.CurrentRead}",
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 40, 50)),
            TextWrapping = TextWrapping.Wrap
        });
        string sample = group.Samples.FirstOrDefault() is { } first
            ? $"Start with {group.LevelName} T{first.TrueIndex}"
            : group.LevelName;
        panel.Children.Add(new TextBlock
        {
            Text = $"{sample} | {group.Lane}",
            Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        string leads = BuildIdentityWorkbenchLeadLine(group, 3);
        if (!string.IsNullOrWhiteSpace(leads))
        {
            panel.Children.Add(new TextBlock
            {
                Text = leads.Trim(),
                Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return panel;
    }

    private string BuildMobyIdentityReviewDetails(MobyIdentityWorkbenchGroup group)
    {
        MobyIdentityFamilyObservation? observation = FindIdentityFamilyObservationForWorkbenchGroup(group);
        MobyIdentityWorkbenchSample? sample = group.Samples.FirstOrDefault();
        string sampleLine = sample == null
            ? "First sample: none listed."
            : $"First sample: T{sample.TrueIndex} at X {sample.X:0.##}, Y {sample.Y:0.##}, Z {sample.Z:0.##}.";
        string nearbyLine = sample == null
            ? ""
            : $"\nNearby known objects: {sample.NearestExactObjects}";
        string leads = observation?.RosterCandidates.Count > 0
            ? $"\nPossible roster leads: {string.Join(", ", observation.RosterCandidates.Take(6))}"
            : "";
        string reads = observation?.CurrentReads.Count > 0
            ? $"\nCurrent reads: {string.Join(", ", observation.CurrentReads.Take(4))}"
            : "";
        string batch = string.IsNullOrWhiteSpace(group.BatchPath)
            ? "No generated test batch is listed for this family."
            : $"Test batch: {Path.GetFileName(group.BatchPath)}";

        return
            $"{group.LevelName}\n" +
            $"{group.Count} matching object(s): {group.CurrentRead}\n\n" +
            $"Status: Needs live proof before we give this a real name.\n" +
            $"Lane: {group.Lane}\n" +
            $"Evidence: {group.EvidenceState}\n" +
            $"Identity bytes: {group.Fingerprint}\n" +
            $"Pointers: {group.PointerGroups}{reads}{leads}\n\n" +
            $"{sampleLine}{nearbyLine}\n\n" +
            $"Recommended proof:\n{group.ProofStep}\n\n" +
            $"{batch}";
    }

    private async Task SelectMobyIdentityWorkbenchSampleAsync(MobyIdentityWorkbenchGroup group)
    {
        MobyIdentityWorkbenchSample? sample = group.Samples.FirstOrDefault();
        if (sample == null)
        {
            _statusText.Text = $"No sample object is listed for {group.LevelName} {group.CurrentRead}.";
            return;
        }

        LevelDefinition? level = _catalog.FindByKey(group.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level {group.LevelKey} for this identity family.";
            return;
        }

        if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(level))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);
        Moby? moby = _currentMobys.FirstOrDefault(item => item.TrueIndex == sample.TrueIndex && !item.IsRemoved);
        if (moby == null)
        {
            _statusText.Text = $"Loaded {level.DisplayName}, but T{sample.TrueIndex} was not found.";
            return;
        }

        SelectMobyCategory("Needs ID");
        RefreshMobyList(moby);
        _viewport.SelectMoby(moby, true);
        _statusText.Text = $"{level.DisplayName} T{moby.TrueIndex} selected for ID review: {group.CurrentRead}. {group.ProofStep}";
    }

    private async Task LoadMobyIdentityWorkbenchBatchAsync(MobyIdentityWorkbenchGroup group)
    {
        string batchPath = ToWorkspacePath(group.BatchPath);
        if (string.IsNullOrWhiteSpace(batchPath) || !File.Exists(batchPath))
        {
            _statusText.Text = $"No generated test batch exists for {group.LevelName} {group.CurrentRead}.";
            return;
        }

        LevelDefinition? level = _catalog.FindByKey(group.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level {group.LevelKey} for this identity batch.";
            return;
        }

        if (!await ConfirmLeaveLevelWithUnsavedTerrainAsync(level))
        {
            SyncLevelPickers(_currentLevel);
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);
        if (_identityBatchBox.ItemsSource is IEnumerable<IdentityBatchFile> batches)
        {
            IdentityBatchFile? selected = batches.FirstOrDefault(batch =>
                string.Equals(Path.GetFullPath(batch.Path), Path.GetFullPath(batchPath), StringComparison.OrdinalIgnoreCase));
            if (selected != null)
                _identityBatchBox.SelectedItem = selected;
        }

        LoadSelectedIdentityBatch();
        SelectMobyCategory("Needs ID");
        _statusText.Text = $"Loaded ID test batch for {level.DisplayName}: {group.Count}x {group.CurrentRead}. {group.ProofStep}";
    }

    private async Task LoadSelectedMobyIdentityTestAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before loading an ID test for the selected object.";
            return;
        }

        if (_selectedMoby == null)
        {
            _statusText.Text = "Select a Needs ID object before loading its matching test.";
            return;
        }

        if (!IsQuestionableMoby(_selectedMoby))
        {
            _statusText.Text = $"T{_selectedMoby.TrueIndex} already has a usable label. Pick a Needs ID object to load a review test.";
            return;
        }

        MobyIdentityWorkbenchGroup? group = FindIdentityWorkbenchGroupForMoby(_currentLevel.Key, _selectedMoby);
        if (group == null)
        {
            _statusText.Text = $"No generated ID test was found for T{_selectedMoby.TrueIndex}. Open ID Families to review the remaining groups.";
            return;
        }

        await LoadMobyIdentityWorkbenchBatchAsync(group);
    }

    private MobyIdentityWorkbenchGroup? FindIdentityWorkbenchGroupForMoby(string levelKey, Moby moby)
    {
        string fingerprint = IdentityFingerprint(moby);
        return LoadMobyIdentityWorkbenchGroups()
            .Where(group =>
                string.Equals(group.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(group.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(group => group.Samples.Any(sample => sample.TrueIndex == moby.TrueIndex))
            .ThenByDescending(group => group.Count)
            .FirstOrDefault();
    }

    private string ToWorkspacePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(_workspace.RootPath, path);
    }

    private IEnumerable<MobyIdentityWorkbenchGroup> LoadMobyIdentityWorkbenchGroups()
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-identity-level-workbench.json");
        if (!File.Exists(path))
            yield break;

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("levels", out JsonElement levels) || levels.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (JsonElement level in levels.EnumerateArray())
        {
            string levelKey = GetJsonString(level, "levelKey");
            string levelName = GetJsonString(level, "levelName", levelKey);
            int totalMobys = GetJsonInt32(level, "totalMobys", 0);
            int exactOrObserved = GetJsonInt32(level, "exactOrObserved", 0);
            int questionable = GetJsonInt32(level, "questionable", 0);
            if (!level.TryGetProperty("groups", out JsonElement groups) || groups.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement group in groups.EnumerateArray())
            {
                List<MobyIdentityWorkbenchSample> samples = new();
                if (group.TryGetProperty("samples", out JsonElement sampleArray) && sampleArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement sample in sampleArray.EnumerateArray())
                    {
                        samples.Add(new MobyIdentityWorkbenchSample(
                            GetJsonInt32(sample, "trueIndex", -1),
                            GetJsonSingle(sample, "x", 0),
                            GetJsonSingle(sample, "y", 0),
                            GetJsonSingle(sample, "z", 0),
                            GetJsonString(sample, "specialDataPointer"),
                            GetJsonString(sample, "nearestExactObjects", "none listed")));
                    }
                }

                yield return new MobyIdentityWorkbenchGroup(
                    levelKey,
                    levelName,
                    totalMobys,
                    exactOrObserved,
                    questionable,
                    GetJsonString(group, "fingerprint"),
                    GetJsonInt32(group, "count", 0),
                    GetJsonString(group, "currentRead", "Needs ID object"),
                    GetJsonString(group, "evidenceState", "no evidence listed"),
                    GetJsonString(group, "lane", "unknown"),
                    GetJsonString(group, "proofStep", "Move a sample into a clear test area and observe it in game."),
                    GetJsonString(group, "pointerGroups", "none"),
                    GetJsonString(group, "batch"),
                    samples);
            }
        }
    }

    private MobyIdentityModelFamily? FindMobyIdentityModelFamilyForMoby(string levelKey, Moby moby)
    {
        return LoadMobyIdentityModelFamilies()
            .Where(family =>
                string.Equals(family.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase) &&
                family.Type == moby.Type &&
                family.SourceByte36 == moby.SourceByte36 &&
                SameModelFamilyRecordScope(family, moby))
            .OrderByDescending(family => family.Samples.Any(sample => sample.TrueIndex == moby.TrueIndex))
            .ThenByDescending(family => family.Questionable)
            .FirstOrDefault();
    }

    private static bool SameModelFamilyRecordScope(MobyIdentityModelFamily family, Moby moby)
    {
        if (moby.SpecialDataPointer != 0)
            return family.SpecialDataPointer == moby.SpecialDataPointer;

        return family.Samples.Any(sample => sample.TrueIndex == moby.TrueIndex) ||
            family.FamilyKey.Contains($"/T{moby.TrueIndex}", StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<MobyIdentityModelFamily> LoadMobyIdentityModelFamilies()
    {
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-identity-model-families.json");
        if (!File.Exists(path))
            yield break;

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("families", out JsonElement families) || families.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (JsonElement family in families.EnumerateArray())
        {
            List<MobyIdentityModelFamilySample> samples = new();
            if (family.TryGetProperty("samples", out JsonElement sampleArray) && sampleArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement sample in sampleArray.EnumerateArray())
                {
                    samples.Add(new MobyIdentityModelFamilySample(
                        GetJsonInt32(sample, "trueIndex", -1),
                        GetJsonString(sample, "fingerprint"),
                        GetJsonString(sample, "label", "Needs ID object"),
                        GetJsonString(sample, "nearestExactObjects", "none listed")));
                }
            }

            yield return new MobyIdentityModelFamily(
                GetJsonString(family, "familyKey"),
                GetJsonString(family, "levelKey"),
                GetJsonString(family, "levelName"),
                GetJsonInt32(family, "typeHex", 0),
                GetJsonInt32(family, "sourceByte36Hex", 0),
                (uint)Math.Max(0, FirstJsonInt64(family, 0, "specialDataPointer", "specialDataPointerHex")),
                GetJsonInt32(family, "questionable", 0),
                GetJsonInt32(family, "total", 0),
                ReadJsonStringArray(family, "currentReads"),
                ReadJsonStringArray(family, "rewardAndBehaviorVariants"),
                ReadJsonStringArray(family, "exactLabelsInFamily"),
                samples,
                ReadJsonStringArray(family, "batchFiles"));
        }
    }

    private bool MatchesMobyIdentityLevelFilter(MobyIdentityWorkbenchGroup group, int selectedIndex)
    {
        if (selectedIndex != 0 || _currentLevel == null)
            return true;

        return string.Equals(group.LevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesMobyIdentityLaneFilter(MobyIdentityWorkbenchGroup group, int selectedIndex)
    {
        string text = $"{group.Lane} {group.CurrentRead}".ToLowerInvariant();
        return selectedIndex switch
        {
            1 => text.Contains("actor") || text.Contains("enemy"),
            2 => text.Contains("scenery") || text.Contains("prop"),
            3 => text.Contains("control") || text.Contains("helper") || text.Contains("nonvisual"),
            4 => text.Contains("interactive") || text.Contains("reward") || text.Contains("container"),
            _ => true
        };
    }

    private static string FormatMobyIdentityGroupListItem(MobyIdentityWorkbenchGroup group)
    {
        string sample = group.Samples.FirstOrDefault() is { } first
            ? $"T{first.TrueIndex}"
            : "no sample";
        return $"{group.LevelName,-15} {group.Count,2}x  {group.CurrentRead,-28} {sample,-6} {group.Fingerprint}";
    }

    private void RefreshIdentityBatchList()
    {
        if (_currentLevel == null)
        {
            _identityBatchBox.ItemsSource = Array.Empty<IdentityBatchFile>();
            _identityBatchHint.Text = "";
            return;
        }

        string batchDir = Path.Combine(_workspace.RootPath, "_local", "smoke", "identity-batches");
        List<IdentityBatchFile> batches = Directory.Exists(batchDir)
            ? Directory.GetFiles(batchDir, $"{_currentLevel.Key}-identity-batch-*-native-edits.json")
                .OrderBy(path => IsQuickWinIdentityBatch(path) ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new IdentityBatchFile(FormatIdentityBatchName(path), path, LoadIdentityBatchDetails(path)))
                .ToList()
            : new List<IdentityBatchFile>();

        _identityBatchBox.ItemsSource = batches;
        _identityBatchBox.SelectedIndex = batches.Count > 0 ? 0 : -1;
        UpdateSelectedIdentityBatchHint();
    }

    private void UpdateSelectedIdentityBatchHint()
    {
        if (_identityBatchBox.SelectedItem is IdentityBatchFile batch)
        {
            _identityBatchHint.Text = batch.Details;
            return;
        }

        _identityBatchHint.Text = _currentLevel == null
            ? ""
            : "No generated identity test batches for this level yet.";
    }

    private string LoadIdentityBatchDetails(string editManifestPath)
    {
        string fullManifestPath = Path.GetFullPath(editManifestPath);
        foreach ((string path, bool quickWin) in IdentityBatchPlanPaths())
        {
            if (!File.Exists(path))
                continue;

            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("batches", out JsonElement batches) && batches.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement batch in batches.EnumerateArray())
                    {
                        string manifest = GetJsonString(batch, "editManifestPath");
                        if (string.IsNullOrWhiteSpace(manifest))
                            continue;

                        string fullPlanManifest = Path.GetFullPath(manifest);
                        if (!string.Equals(fullPlanManifest, fullManifestPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string number = GetJsonString(batch, "Number", "?");
                        string questionableCount = GetJsonString(batch, "questionableCount");
                        string scopeQuestionableCount = GetJsonString(batch, "scopeQuestionableCount");
                        string fingerprint = GetJsonString(batch, "fingerprint");
                        string currentLabel = GetJsonString(batch, "currentLabel", "Needs ID object");
                        string lane = GetJsonString(batch, "lane", "live identity test");
                        string rosterLeads = FormatJsonStringArray(batch, "rosterLeads");
                        string proofStep = GetJsonString(batch, "proofStep");
                        string quickWinReason = GetJsonString(batch, "quickWinReason");
                        string samples = FormatIdentityBatchSamples(batch);
                        string impact = string.IsNullOrWhiteSpace(questionableCount)
                            ? "Resolves matching Needs ID objects after confirmation."
                            : $"Can resolve {questionableCount} Needs ID object(s) after confirmation.";
                        if (!string.IsNullOrWhiteSpace(scopeQuestionableCount) &&
                            !string.Equals(scopeQuestionableCount, questionableCount, StringComparison.OrdinalIgnoreCase))
                        {
                            impact = $"Safely resolves {scopeQuestionableCount} in this model family; full fingerprint has {questionableCount}.";
                        }
                        string title = quickWin ? $"Quick win {number}: {currentLabel}" : $"Batch {number}: {currentLabel}";
                        string reason = string.IsNullOrWhiteSpace(quickWinReason) ? "" : $"\nWhy quick: {quickWinReason}";
                        string leads = string.IsNullOrWhiteSpace(rosterLeads) ? "" : $"\nRoster leads: {rosterLeads}";
                        return $"{title}\n{impact}\n{fingerprint}\n{lane}{leads}{reason}\n{proofStep}\nSamples: {samples}";
                    }
                }
            }
            catch
            {
            }
        }

        return $"{FormatIdentityBatchName(editManifestPath)}\nLoad this batch, create a test BIN, observe the moved objects in game, then save the observed ID from the selected object.";
    }

    private static string FormatJsonStringArray(JsonElement element, string propertyName)
    {
        return string.Join(", ", ReadJsonStringArray(element, propertyName));
    }

    private IEnumerable<(string Path, bool QuickWin)> IdentityBatchPlanPaths()
    {
        string outDir = Path.Combine(_workspace.RootPath, "_local", "smoke");
        yield return (Path.Combine(outDir, "moby-identity-quick-win-tests.json"), true);
        yield return (Path.Combine(outDir, "moby-identity-test-batches.json"), false);
    }

    private static bool IsQuickWinIdentityBatch(string path)
    {
        return Path.GetFileName(path).Contains("-identity-batch-qw", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatIdentityBatchSamples(JsonElement batch)
    {
        if (!batch.TryGetProperty("samples", out JsonElement samples) || samples.ValueKind != JsonValueKind.Array)
            return "";

        List<string> labels = new();
        foreach (JsonElement sample in samples.EnumerateArray().Take(4))
        {
            string levelName = GetJsonString(sample, "LevelName", GetJsonString(sample, "levelName"));
            string trueIndex = GetJsonString(sample, "TrueIndex", GetJsonString(sample, "trueIndex"));
            labels.Add(string.IsNullOrWhiteSpace(levelName) || string.IsNullOrWhiteSpace(trueIndex)
                ? sample.ToString()
                : $"{levelName} T{trueIndex}");
        }

        return string.Join(", ", labels);
    }

    private void LoadSelectedIdentityBatch()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Choose a level before loading a test batch.";
            return;
        }

        if (_identityBatchBox.SelectedItem is not IdentityBatchFile batch || !File.Exists(batch.Path))
        {
            _statusText.Text = "Choose an identity test batch first.";
            return;
        }

        try
        {
            ClearActiveIdentityBatch(showStatus: false);
            IdentityBatchOverlayResult overlay = IdentityBatchOverlay.Apply(batch.Path, _currentMobys);
            List<Moby> changed = overlay.ChangedMobys.ToList();
            _activeIdentityBatchRestore = overlay.RestoreSet;
            _activeIdentityBatchName = changed.Count == 0 ? "" : batch.Name;
            MobyRelationshipRepair.RepairChestContentLinks(_currentLevel.Key, _currentMobys);
            _viewport.Mobys = _currentMobys;
            Moby? focus = changed.FirstOrDefault();
            RefreshMobyList(focus);
            if (focus != null)
                _viewport.SelectMoby(focus, true);
            else
                _viewport.InvalidateVisual();

            _statusText.Text = changed.Count == 0
                ? $"No matching objects were found in {batch.Name}."
                : $"Loaded {changed.Count} object(s) from {batch.Name}. Inspect before saving or creating a test BIN. {batch.Details.Split('\n').FirstOrDefault()}";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not load test batch: {ex.Message}";
        }
    }

    private void LoadNextPriorityIdentityBatch()
    {
        try
        {
            IdentityBatchCandidate? candidate = LoadPriorityIdentityBatchCandidates().FirstOrDefault();
            if (candidate == null)
            {
                _statusText.Text = "No generated priority identity batches were found. Run the smoke report to refresh identity test batches.";
                return;
            }

            LoadIdentityBatchCandidate(candidate);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not load the next priority identity test: {ex.Message}";
        }
    }

    private void LoadDisplayedIdentityPriorityTest()
    {
        try
        {
            IdentityBatchCandidate? candidate = _identityPriorityCandidate;
            if (candidate == null)
            {
                RefreshIdentityPriorityHint();
                candidate = _identityPriorityCandidate;
            }

            if (candidate == null)
            {
                _statusText.Text = "No generated priority identity batches were found. Run the smoke report to refresh identity test batches.";
                return;
            }

            LoadIdentityBatchCandidate(candidate);
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Could not load the displayed identity test: {ex.Message}";
        }
    }

    private void LoadIdentityBatchCandidate(IdentityBatchCandidate candidate)
    {
        LevelDefinition? level = _catalog.FindByKey(candidate.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level {candidate.LevelKey} for the next identity test batch.";
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);

        if (_identityBatchBox.ItemsSource is IEnumerable<IdentityBatchFile> batches)
        {
            IdentityBatchFile? batch = batches.FirstOrDefault(item =>
                string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(candidate.ManifestPath), StringComparison.OrdinalIgnoreCase));
            if (batch != null)
                _identityBatchBox.SelectedItem = batch;
        }

        LoadSelectedIdentityBatch();
        int questionableIndex = Array.IndexOf(MobyCategoryOptions, "Needs ID");
        if (questionableIndex >= 0)
            _mobyCategoryBox.SelectedIndex = questionableIndex;
        RefreshMobyList(_selectedMoby);
        RefreshIdentityPriorityHint();
        _statusText.Text = $"Loaded priority identity test {candidate.Number} in {level.DisplayName}: {candidate.CurrentLabel}. {candidate.Impact}";
    }

    private void LoadNextClusterReviewTest()
    {
        string resultPath = ClusterReviewResultsPath();
        if (!File.Exists(resultPath))
        {
            _statusText.Text = "The cluster review sheet has not been generated yet. Run the smoke report once, then load a cluster test.";
            return;
        }

        List<ClusterReviewResultRow> rows = LoadClusterReviewResultRows(resultPath);
        ClusterReviewResultRow? row = rows
            .Where(row => !string.Equals(row.LevelKey, "multiple", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(row => string.IsNullOrWhiteSpace(row.ResultStatus) && string.IsNullOrWhiteSpace(row.ClusterVerdict)) ??
            rows.FirstOrDefault(row => !string.Equals(row.LevelKey, "multiple", StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            _statusText.Text = "No level-specific cluster review rows were found. Open Cluster Review to inspect cross-level rows.";
            return;
        }

        LoadClusterReviewRow(row);
    }

    private void LoadClusterReviewRow(ClusterReviewResultRow row)
    {
        LevelDefinition? level = _catalog.FindByKey(row.LevelKey);
        if (level == null)
        {
            _statusText.Text = $"Could not find level {row.LevelKey} for cluster review row {row.Priority}.";
            return;
        }

        SyncLevelPickers(level);
        SelectLevel(level);

        string? loadedBatch = TryLoadFirstClusterBatch(row);
        Moby? focus = SelectFirstClusterMoby(row);
        int questionableIndex = Array.IndexOf(MobyCategoryOptions, "Needs ID");
        if (questionableIndex >= 0)
            _mobyCategoryBox.SelectedIndex = questionableIndex;
        RefreshMobyList(focus ?? _selectedMoby);

        string batchNote = string.IsNullOrWhiteSpace(loadedBatch)
            ? "No batch file was available, so the first listed cluster object was selected."
            : $"Loaded batch {loadedBatch}.";
        _statusText.Text = $"Loaded cluster review row {row.Priority}: {row.ClusterKind} in {row.LevelName}, {row.UnknownCount} unknown object(s). {batchNote}";
    }

    private string? TryLoadFirstClusterBatch(ClusterReviewResultRow row)
    {
        foreach (string path in SplitClusterBatchPaths(row.BatchPaths))
        {
            string fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_workspace.RootPath, path);
            if (!File.Exists(fullPath))
                continue;

            if (_identityBatchBox.ItemsSource is IEnumerable<IdentityBatchFile> batches)
            {
                IdentityBatchFile? batch = batches.FirstOrDefault(item =>
                    string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase));
                if (batch != null)
                    _identityBatchBox.SelectedItem = batch;
            }

            try
            {
                if (_identityBatchBox.SelectedItem is IdentityBatchFile selected &&
                    string.Equals(Path.GetFullPath(selected.Path), Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase))
                {
                    LoadSelectedIdentityBatch();
                }
                else
                {
                    ClearActiveIdentityBatch(showStatus: false);
                    IdentityBatchOverlayResult overlay = IdentityBatchOverlay.Apply(fullPath, _currentMobys);
                    List<Moby> changed = overlay.ChangedMobys.ToList();
                    _activeIdentityBatchRestore = overlay.RestoreSet;
                    _activeIdentityBatchName = changed.Count == 0 ? "" : Path.GetFileNameWithoutExtension(fullPath);
                    MobyRelationshipRepair.RepairChestContentLinks(_currentLevel?.Key ?? row.LevelKey, _currentMobys);
                    _viewport.Mobys = _currentMobys;
                    RefreshMobyList(changed.FirstOrDefault());
                    if (changed.FirstOrDefault() is Moby focus)
                        _viewport.SelectMoby(focus, true);
                    else
                        _viewport.InvalidateVisual();
                }

                return Path.GetFileName(fullPath);
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Could not load cluster batch {Path.GetFileName(fullPath)}: {ex.Message}";
                return null;
            }
        }

        return null;
    }

    private Moby? SelectFirstClusterMoby(ClusterReviewResultRow row)
    {
        foreach (int trueIndex in ParseClusterTrueIndexes(row.UnknownRecords))
        {
            Moby? moby = _currentMobys.FirstOrDefault(item => !item.IsRemoved && item.TrueIndex == trueIndex);
            if (moby == null)
                continue;

            _viewport.SelectMoby(moby, true);
            SelectMobyInList(moby);
            return moby;
        }

        return null;
    }

    private void UseSuggestedIdentityObservationName()
    {
        if (_identityObservationSuggestionBox.SelectedItem is not string label || string.IsNullOrWhiteSpace(label))
        {
            _statusText.Text = "Select an object with trusted name suggestions first.";
            return;
        }

        _identityObservationNameBox.Text = label;
        _statusText.Text = $"Using suggested observed name '{label}'. Save it after you verify that this is what the selected object is in game.";
    }

    private void RefreshIdentityObservationSuggestions(Moby? selected)
    {
        List<string> suggestions = BuildIdentityObservationSuggestions(selected);
        _identityObservationSuggestionBox.ItemsSource = suggestions;
        _identityObservationSuggestionBox.SelectedIndex = suggestions.Count > 0 ? 0 : -1;
    }

    private List<string> BuildIdentityObservationSuggestions(Moby? selected)
    {
        if (selected == null)
            return new List<string>();

        string selectedBadge = MobyListBadge(selected);
        MobyVisualKind selectedKind = selected.VisualKind;
        return _currentMobys
            .Where(moby => !moby.IsRemoved)
            .Where(moby => !ReferenceEquals(moby, selected))
            .Where(moby => !IsQuestionableMoby(moby))
            .Where(moby => IsUsableIdentityObservationLabel(moby.DisplayLabel))
            .GroupBy(moby => moby.DisplayLabel.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Label = group.Key,
                Count = group.Count(),
                SameCategory = group.Any(moby =>
                    moby.VisualKind == selectedKind ||
                    string.Equals(MobyListBadge(moby), selectedBadge, StringComparison.OrdinalIgnoreCase)),
                Trusted = group.Count(moby => IsTrustedMobyIdentity(BuildMobyIdentityStatus(moby)))
            })
            .OrderByDescending(item => item.SameCategory)
            .ThenByDescending(item => item.Trusted)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Take(18)
            .Select(item => item.Label)
            .ToList();
    }

    private void ShowQuestionableMobys()
    {
        int questionableIndex = Array.IndexOf(MobyCategoryOptions, "Needs ID");
        if (questionableIndex < 0)
        {
            _statusText.Text = "Needs ID filtering is not available.";
            return;
        }

        _mobyCategoryBox.SelectedIndex = questionableIndex;
        RefreshMobyList(_selectedMoby);
        int count = _currentMobys.Count(moby => !moby.IsRemoved && IsQuestionableMoby(moby));
        _statusText.Text = count == 0
            ? "No objects need ID review in this level."
            : $"Showing {count} object(s) that still need ID review in {_currentLevel?.DisplayName ?? "this level"}.";
    }

    private void OpenIdentityReleaseReviewFolder()
    {
        string reviewRoot = Path.Combine(_workspace.RootPath, "_local", "identity-review");
        if (!Directory.Exists(reviewRoot))
        {
            _statusText.Text = "The ID review folder has not been generated yet. Run the smoke report once, then open it here.";
            return;
        }

        try
        {
            OpenPath(reviewRoot);
            _statusText.Text = "Opened the ID review folder. Use the README and results sheet to record only confirmed object identities.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open the ID review folder: {ex.Message}";
        }
    }

    private void OpenUnknownMobyTriageReport()
    {
        string reportPath = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-unknown-link-triage.md");
        if (!File.Exists(reportPath))
        {
            _statusText.Text = "The unknown moby link report has not been generated yet. Run the smoke report or refresh the ID review queue, then open it here.";
            return;
        }

        try
        {
            OpenPath(reportPath);
            _statusText.Text = "Opened the unknown moby link triage report. Use it as a proof checklist before promoting any question-mark labels.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open the unknown moby link report: {ex.Message}";
        }
    }

    private void OpenUnknownMobyClusterMapReport()
    {
        string reportPath = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-unknown-cluster-map.md");
        if (!File.Exists(reportPath))
        {
            _statusText.Text = "The unknown moby proof cluster map has not been generated yet. Run the smoke report or refresh the ID review queue, then open it here.";
            return;
        }

        try
        {
            OpenPath(reportPath);
            _statusText.Text = "Opened the unknown moby proof cluster map. Start with direct-link clusters and shared special-data families for the fastest ID gains.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open the unknown moby proof cluster map: {ex.Message}";
        }
    }

    private void OpenUnknownMobyClusterReviewFolder()
    {
        string reviewRoot = Path.Combine(_workspace.RootPath, "_local", "identity-review", "clusters");
        if (!Directory.Exists(reviewRoot))
        {
            _statusText.Text = "The cluster review folder has not been generated yet. Run the smoke report or refresh the ID review queue, then open it here.";
            return;
        }

        try
        {
            OpenPath(reviewRoot);
            _statusText.Text = "Opened the cluster review folder. Fill the results sheet after testing proof clusters in-game.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open the cluster review folder: {ex.Message}";
        }
    }

    private void SelectQuestionableMoby(bool advance)
    {
        List<Moby> all = _currentMobys.Where(moby => !moby.IsRemoved).ToList();
        string filter = _mobySearchBox.Text?.Trim() ?? "";
        List<Moby> queue = OrderMobysForList(
            all,
            all.Where(moby => IsQuestionableMoby(moby) && (string.IsNullOrWhiteSpace(filter) || MatchesMobyFilter(moby, filter))).ToList(),
            Array.IndexOf(MobyCategoryOptions, "Needs ID"));
        if (queue.Count == 0)
        {
            _statusText.Text = string.IsNullOrWhiteSpace(filter)
                ? "No objects need ID review in this level."
                : $"No Needs ID objects match \"{filter}\" in this level.";
            return;
        }

        Moby target = queue[0];
        if (advance && _selectedMoby != null)
        {
            int current = queue.FindIndex(moby => ReferenceEquals(moby, _selectedMoby));
            target = current >= 0
                ? queue[(current + 1) % queue.Count]
                : queue.FirstOrDefault(moby => moby.TrueIndex > _selectedMoby.TrueIndex) ?? queue[0];
        }

        int questionableIndex = Array.IndexOf(MobyCategoryOptions, "Needs ID");
        if (questionableIndex >= 0)
            _mobyCategoryBox.SelectedIndex = questionableIndex;

        _viewport.SelectMoby(target, true);
        SelectMobyInList(target);
        RefreshMobyList(target);

        Dictionary<string, int> familyCounts = BuildIdentityFamilyCounts(all);
        string fingerprint = IdentityFingerprint(target);
        int familyCount = familyCounts.GetValueOrDefault(fingerprint, 1);
        string filterNote = string.IsNullOrWhiteSpace(filter) ? "" : $" Matching filter \"{filter}\".";
        _statusText.Text = $"Selected Needs ID target T{target.TrueIndex}: {target.DisplayLabel}. Repeated family: {familyCount} object(s), {fingerprint}.{filterNote}";
    }

    private void ShowSelectedMobyIdentityFamily()
    {
        if (_selectedMoby == null || _selectedMoby.IsRemoved)
        {
            _statusText.Text = "Select an object before filtering to its ID family.";
            return;
        }

        string familyKey = IdentityFilterKey(_selectedMoby);
        _mobySearchBox.Text = $"family:{familyKey}";
        SelectMobyCategory(IsQuestionableMoby(_selectedMoby) ? "Needs ID" : "All objects");
        RefreshMobyList(_selectedMoby);
        int count = CountSameIdentityFingerprintMobys(_selectedMoby);
        _statusText.Text = $"Showing ID family for T{_selectedMoby.TrueIndex}: {count} matching object(s), {MobySourceByteSummary(_selectedMoby)}.";
    }

    private void ShowSelectedMobyModelFamily()
    {
        if (_selectedMoby == null || _selectedMoby.IsRemoved)
        {
            _statusText.Text = "Select an object before filtering to its model family.";
            return;
        }

        string modelKey = ModelFamilyFilterKey(_selectedMoby);
        _mobySearchBox.Text = $"model:{modelKey}";
        SelectMobyCategory(IsQuestionableMoby(_selectedMoby) ? "Needs ID" : "All objects");
        RefreshMobyList(_selectedMoby);
        int count = CountSameModelFamilyMobys(_selectedMoby);
        string scope = _selectedMoby.SpecialDataPointer == 0
            ? $"single no-pointer model row T{_selectedMoby.TrueIndex}"
            : $"special pointer 0x{_selectedMoby.SpecialDataPointer:X8}";
        _statusText.Text = $"Showing model family for T{_selectedMoby.TrueIndex}: {count} object(s), type 0x{_selectedMoby.Type:X2}/source 0x{_selectedMoby.SourceByte36:X2}, {scope}.";
    }

    private IEnumerable<IdentityBatchCandidate> LoadPriorityIdentityBatchCandidates()
    {
        string outDir = Path.Combine(_workspace.RootPath, "_local", "smoke");
        string[] planPaths =
        [
            Path.Combine(outDir, "moby-identity-quick-win-tests.json"),
            Path.Combine(outDir, "moby-identity-test-batches.json")
        ];

        foreach (string path in planPaths)
        {
            if (!File.Exists(path))
                continue;

            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("batches", out JsonElement batches) ||
                batches.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement batch in batches.EnumerateArray())
            {
                string manifest = GetJsonString(batch, "editManifestPath");
                if (string.IsNullOrWhiteSpace(manifest) || !File.Exists(manifest))
                    continue;

                string levelKey = GetFirstBatchSampleLevelKey(batch);
                if (string.IsNullOrWhiteSpace(levelKey))
                    continue;

                string number = GetJsonString(batch, "Number", "?");
                string currentLabel = GetJsonString(batch, "currentLabel", "Needs ID object");
                string scopeCount = GetJsonString(batch, "scopeQuestionableCount");
                string totalCount = GetJsonString(batch, "questionableCount");
                string impact = !string.IsNullOrWhiteSpace(scopeCount) && !string.Equals(scopeCount, totalCount, StringComparison.OrdinalIgnoreCase)
                    ? $"Can resolve {scopeCount} in this model family; full fingerprint has {totalCount}."
                    : string.IsNullOrWhiteSpace(totalCount)
                        ? "Can resolve matching Needs ID objects after confirmation."
                        : $"Can resolve {totalCount} Needs ID object(s) after confirmation.";

                yield return new IdentityBatchCandidate(number, levelKey, manifest, currentLabel, impact);
            }
        }
    }

    private static string GetFirstBatchSampleLevelKey(JsonElement batch)
    {
        if (!batch.TryGetProperty("samples", out JsonElement samples) || samples.ValueKind != JsonValueKind.Array)
            return "";

        foreach (JsonElement sample in samples.EnumerateArray())
        {
            string levelKey = GetJsonString(sample, "LevelKey", GetJsonString(sample, "levelKey"));
            if (!string.IsNullOrWhiteSpace(levelKey))
                return levelKey;
        }

        return "";
    }

    private void ClearActiveIdentityBatch()
    {
        ClearActiveIdentityBatch(showStatus: true);
    }

    private void ClearActiveIdentityBatch(bool showStatus)
    {
        if (_activeIdentityBatchRestore.Count == 0)
        {
            if (showStatus)
                _statusText.Text = "No identity test batch is loaded.";
            return;
        }

        int restored = IdentityBatchOverlay.Clear(_activeIdentityBatchRestore, _currentMobys);
        string batchName = _activeIdentityBatchName;
        _activeIdentityBatchRestore = new IdentityBatchRestoreSet(new Dictionary<int, IdentityBatchRestoreState>());
        _activeIdentityBatchName = "";
        RefreshMobyList(_selectedMoby);
        _viewport.Mobys = _currentMobys;
        _viewport.InvalidateVisual();
        if (showStatus)
            _statusText.Text = $"Cleared {restored} object(s) from {(string.IsNullOrWhiteSpace(batchName) ? "the identity test batch" : batchName)}.";
    }

    private async Task SaveSelectedIdentityObservationAsync()
    {
        if (_currentLevel == null || _selectedMoby == null)
        {
            _statusText.Text = "Select an object before saving an observed ID.";
            return;
        }

        string label = (_identityObservationNameBox.Text ?? "").Trim();
        if (!IsUsableIdentityObservationLabel(label))
        {
            _statusText.Text = "Use a specific observed name, like Wide tree or Metal chest, not a broad word like object or unknown.";
            return;
        }

        Moby selected = _selectedMoby;
        string fingerprint = IdentityFingerprint(selected);
        string path = Path.Combine(_workspace.RootPath, "_local", "smoke", "moby-identity-observations.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);

        List<JsonElement> observations = new();
        if (File.Exists(path))
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (TryGetObservationArray(document.RootElement, out JsonElement existing))
                {
                    foreach (JsonElement observation in existing.EnumerateArray())
                    {
                        if (!SameIdentityObservationScope(observation, fingerprint, selected, _currentLevel.Key))
                            observations.Add(observation.Clone());
                    }
                }
            }
            catch
            {
                observations.Clear();
            }
        }

        string kind = IdentityObservationKind(selected, label);
        string pointerScope = selected.SpecialDataPointer == 0
            ? $"single no-pointer object T{selected.TrueIndex}"
            : $"special-data pointer 0x{selected.SpecialDataPointer:X8}";
        string evidence = $"Live observation recorded from {_currentLevel.DisplayName} T{selected.TrueIndex}: {label}. Applies to {_currentLevel.DisplayName} model family {fingerprint}, {pointerScope}.";
        observations.Add(JsonSerializer.SerializeToElement(new
        {
            fingerprint,
            levelKey = _currentLevel.Key,
            typeHex = $"0x{selected.Type:X2}",
            sourceByte36Hex = $"0x{selected.SourceByte36:X2}",
            flag4AHex = $"0x{selected.Flag4A:X2}",
            flag4BHex = $"0x{selected.Flag4B:X2}",
            sourceByte4FHex = $"0x{selected.SourceByte4F:X2}",
            specialDataPointerHex = $"0x{selected.SpecialDataPointer:X8}",
            matchTrueIndex = selected.SpecialDataPointer == 0 ? selected.TrueIndex : (int?)null,
            label,
            displayTargetLabel = label,
            candidateKind = kind,
            confidence = "live-observed-model-family",
            color = IdentityObservationColor(selected, label),
            evidence,
            sampleLevelKey = _currentLevel.Key,
            sampleLevelName = _currentLevel.DisplayName,
            sampleTrueIndex = selected.TrueIndex,
            recordedAt = DateTimeOffset.Now
        }, NewJsonOptions()));

        var root = new
        {
            generatedAt = DateTimeOffset.Now,
            instructions = "Live observations saved from the native editor. Blank labels and labels containing ?, unknown, candidate, or related are ignored by the editor.",
            observations
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(root, NewJsonOptions()));

        int applied = 0;
        foreach (Moby moby in _currentMobys.Where(moby => SameLiveObservationScope(moby, selected, fingerprint)))
        {
            moby.Label = label;
            moby.OriginalLabel = label;
            moby.CandidateKind = kind;
            moby.Confidence = "live-observed-model-family";
            moby.Evidence = evidence;
            if (ColorRgba.TryParseHex(IdentityObservationColor(selected, label), out ColorRgba color))
                moby.Color = color;
            applied++;
        }

        RefreshMobyList(selected);
        _viewport.Mobys = _currentMobys;
        _viewport.SelectMoby(selected, true);
        _identityObservationNameBox.Text = "";
        int fingerprintCount = CountSameIdentityFingerprintMobys(selected);
        _statusText.Text = fingerprintCount > applied
            ? $"Saved observed ID '{label}' for {applied} matching object(s). {fingerprintCount} object(s) share this byte fingerprint in the level."
            : $"Saved observed ID '{label}' for {applied} matching object(s).";
    }

    private async Task RecordSelectedIdentityReviewResultAsync()
    {
        if (_currentLevel == null || _selectedMoby == null)
        {
            _statusText.Text = "Select the tested Needs ID object before recording an ID review result.";
            return;
        }

        Moby selected = _selectedMoby;
        string resultPath = IdentityReviewResultsPath();
        if (!File.Exists(resultPath))
        {
            _statusText.Text = "The ID review results sheet has not been generated yet. Run the smoke report once, then record results here.";
            return;
        }

        List<IdentityReviewResultRow> rows = LoadIdentityReviewResultRows(resultPath);
        IdentityReviewResultRow? row = FindIdentityReviewResultRow(rows, selected, _currentLevel.Key);
        if (row == null)
        {
            _statusText.Text = $"No ID review row matches {_currentLevel.DisplayName} T{selected.TrueIndex}. Open Review Folder to inspect the generated queue.";
            return;
        }

        ComboBox statusBox = new()
        {
            ItemsSource = new[] { "confirmed", "unknown", "no-visual", "crash", "skipped" },
            SelectedItem = string.IsNullOrWhiteSpace(row.ResultStatus) ? "confirmed" : row.ResultStatus,
            MinWidth = 220,
            MinHeight = 34
        };
        TextBox labelBox = new()
        {
            Text = FirstNonBlank(row.ObservedLabel, _identityObservationNameBox.Text ?? ""),
            PlaceholderText = "Exact observed name",
            MinHeight = 34
        };
        TextBox kindBox = new()
        {
            Text = FirstNonBlank(row.CandidateKind, IdentityObservationKind(selected, FirstNonBlank(row.ObservedLabel, _identityObservationNameBox.Text ?? "", selected.DisplayLabel))),
            PlaceholderText = "Object kind",
            MinHeight = 34
        };
        TextBox notesBox = new()
        {
            Text = row.EvidenceNotes,
            PlaceholderText = "What did it look like or do? Any reward/drop behavior?",
            AcceptsReturn = true,
            MinHeight = 110,
            TextWrapping = TextWrapping.Wrap
        };
        TextBlock warning = NewSmallNote("");
        warning.Foreground = new SolidColorBrush(Color.FromRgb(176, 69, 55));
        warning.IsVisible = false;

        Window dialog = new()
        {
            Title = "Record ID Review Result",
            Width = 660,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildIdentityReviewResultDialogContent(dialog, row, statusBox, labelBox, kindBox, notesBox, warning);
        bool saved = await dialog.ShowDialog<bool>(this);
        if (!saved)
            return;

        string status = (statusBox.SelectedItem?.ToString() ?? "").Trim();
        string observedLabel = (labelBox.Text ?? "").Trim();
        if (status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) && !IsUsableIdentityObservationLabel(observedLabel))
        {
            _statusText.Text = "Confirmed ID results need a specific observed name before they can be saved.";
            return;
        }

        row.Set("resultStatus", status);
        row.Set("observedLabel", observedLabel);
        row.Set("candidateKind", FirstNonBlank(kindBox.Text ?? "", status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) ? IdentityObservationKind(selected, observedLabel) : ""));
        row.Set("evidenceNotes", (notesBox.Text ?? "").Trim());
        row.Set("promoteScope", FirstNonBlank(row.PromoteScope, "fingerprint"));
        WriteIdentityReviewResultRows(resultPath, rows);

        string saveNote = "";
        if (status.Equals("confirmed", StringComparison.OrdinalIgnoreCase))
        {
            _identityObservationNameBox.Text = observedLabel;
            await SaveSelectedIdentityObservationAsync();
            saveNote = " It was also saved as a live observed ID for the editor.";
        }

        _statusText.Text = $"Recorded ID review result for {_currentLevel.DisplayName} T{selected.TrueIndex}: {status}.{saveNote} Use Open Review Folder to see the tester sheet.";
    }

    private Control BuildIdentityReviewResultDialogContent(
        Window dialog,
        IdentityReviewResultRow row,
        ComboBox statusBox,
        TextBox labelBox,
        TextBox kindBox,
        TextBox notesBox,
        TextBlock warning)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"{row.LevelName} {row.TrueIndexes}: {row.CurrentLabel}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 40, 50)),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(NewSmallNote(row.ProofStep));
        panel.Children.Add(BuildSimpleField("Result", statusBox));
        panel.Children.Add(BuildSimpleField("Observed Name", labelBox));
        panel.Children.Add(BuildSimpleField("Kind", kindBox));
        panel.Children.Add(BuildSimpleField("Evidence Notes", notesBox));
        panel.Children.Add(warning);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button save = NewButton("Save Result");
        cancel.Click += (_, _) => dialog.Close(false);
        save.Click += (_, _) =>
        {
            string status = statusBox.SelectedItem?.ToString() ?? "";
            string label = (labelBox.Text ?? "").Trim();
            if (status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) && !IsUsableIdentityObservationLabel(label))
            {
                warning.Text = "Confirmed results need a specific name, not object, unknown, candidate, or a question-mark label.";
                warning.IsVisible = true;
                return;
            }

            dialog.Close(true);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private async Task RecordSelectedClusterReviewResultAsync()
    {
        if (_currentLevel == null || _selectedMoby == null)
        {
            _statusText.Text = "Select an object from the tested cluster before recording a cluster result.";
            return;
        }

        string resultPath = ClusterReviewResultsPath();
        if (!File.Exists(resultPath))
        {
            _statusText.Text = "The cluster review results sheet has not been generated yet. Run the smoke report once, then record cluster results here.";
            return;
        }

        List<ClusterReviewResultRow> rows = LoadClusterReviewResultRows(resultPath);
        ClusterReviewResultRow? row = FindClusterReviewResultRow(rows, _selectedMoby, _currentLevel.Key);
        if (row == null)
        {
            _statusText.Text = $"No cluster review row includes {_currentLevel.DisplayName} T{_selectedMoby.TrueIndex}. Use Load Cluster Test or Open Cluster Review to inspect the queue.";
            return;
        }

        ComboBox statusBox = new()
        {
            ItemsSource = new[] { "tested", "confirmed", "unknown", "no-visual", "crash", "skipped" },
            SelectedItem = string.IsNullOrWhiteSpace(row.ResultStatus) ? "tested" : row.ResultStatus,
            MinWidth = 220,
            MinHeight = 34
        };
        ComboBox verdictBox = new()
        {
            ItemsSource = new[] { "same-object", "variant-family", "support-control", "not-related", "needs-live-test" },
            SelectedItem = string.IsNullOrWhiteSpace(row.ClusterVerdict) ? "needs-live-test" : row.ClusterVerdict,
            MinWidth = 260,
            MinHeight = 34
        };
        TextBox labelBox = new()
        {
            Text = FirstNonBlank(row.ObservedLabel, _identityObservationNameBox.Text ?? ""),
            PlaceholderText = "Optional exact observed name",
            MinHeight = 34
        };
        TextBox notesBox = new()
        {
            Text = row.EvidenceNotes,
            PlaceholderText = "What did the cluster prove? Same object, variant family, support/control, or misleading grouping?",
            AcceptsReturn = true,
            MinHeight = 130,
            TextWrapping = TextWrapping.Wrap
        };
        ComboBox promoteScopeBox = new()
        {
            ItemsSource = new[] { "cluster-only", "model-family", "fingerprint", "do-not-promote" },
            SelectedItem = string.IsNullOrWhiteSpace(row.PromoteScope) ? "cluster-only" : row.PromoteScope,
            MinWidth = 220,
            MinHeight = 34
        };
        TextBlock warning = NewSmallNote("");
        warning.Foreground = new SolidColorBrush(Color.FromRgb(176, 69, 55));
        warning.IsVisible = false;

        Window dialog = new()
        {
            Title = "Record Cluster Result",
            Width = 720,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildClusterReviewResultDialogContent(dialog, row, statusBox, verdictBox, labelBox, notesBox, promoteScopeBox, warning);
        bool saved = await dialog.ShowDialog<bool>(this);
        if (!saved)
            return;

        string status = (statusBox.SelectedItem?.ToString() ?? "").Trim();
        string verdict = (verdictBox.SelectedItem?.ToString() ?? "").Trim();
        string observedLabel = (labelBox.Text ?? "").Trim();
        if (status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) &&
            verdict is "same-object" or "variant-family" &&
            !string.IsNullOrWhiteSpace(observedLabel) &&
            !IsUsableIdentityObservationLabel(observedLabel))
        {
            _statusText.Text = "Cluster labels must be specific before they can be saved for review.";
            return;
        }

        row.Set("resultStatus", status);
        row.Set("clusterVerdict", verdict);
        row.Set("observedLabel", observedLabel);
        row.Set("evidenceNotes", (notesBox.Text ?? "").Trim());
        row.Set("promoteScope", promoteScopeBox.SelectedItem?.ToString() ?? "cluster-only");
        WriteClusterReviewResultRows(resultPath, rows);

        _statusText.Text = $"Recorded cluster row {row.Priority}: {verdict} ({status}). This updates the cluster review sheet only; labels are still gated by the promotion review.";
    }

    private Control BuildClusterReviewResultDialogContent(
        Window dialog,
        ClusterReviewResultRow row,
        ComboBox statusBox,
        ComboBox verdictBox,
        TextBox labelBox,
        TextBox notesBox,
        ComboBox promoteScopeBox,
        TextBlock warning)
    {
        StackPanel panel = new()
        {
            Spacing = 10,
            Margin = new Thickness(16)
        };
        panel.Children.Add(new TextBlock
        {
            Text = $"Cluster {row.Priority}: {row.ClusterKind} in {row.LevelName}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 40, 50)),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(NewSmallNote($"{row.UnknownCount} unknown object(s): {row.UnknownRecords}"));
        panel.Children.Add(NewSmallNote($"Known anchors: {row.KnownAnchors}"));
        panel.Children.Add(NewSmallNote(row.ProofMove));
        panel.Children.Add(BuildSimpleField("Result", statusBox));
        panel.Children.Add(BuildSimpleField("Cluster Verdict", verdictBox));
        panel.Children.Add(BuildSimpleField("Observed Name", labelBox));
        panel.Children.Add(BuildSimpleField("Promotion Scope", promoteScopeBox));
        panel.Children.Add(BuildSimpleField("Evidence Notes", notesBox));
        panel.Children.Add(warning);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button save = NewButton("Save Cluster Result");
        cancel.Click += (_, _) => dialog.Close(false);
        save.Click += (_, _) =>
        {
            string verdict = verdictBox.SelectedItem?.ToString() ?? "";
            string label = (labelBox.Text ?? "").Trim();
            if ((verdict.Equals("same-object", StringComparison.OrdinalIgnoreCase) ||
                    verdict.Equals("variant-family", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(label) &&
                !IsUsableIdentityObservationLabel(label))
            {
                warning.Text = "Use a specific observed name, or leave the name blank until the cluster has a concrete label.";
                warning.IsVisible = true;
                return;
            }

            dialog.Close(true);
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        return new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private string IdentityReviewResultsPath()
    {
        return Path.Combine(_workspace.RootPath, "_local", "identity-review", "results", "moby-identity-results.tsv");
    }

    private string ClusterReviewResultsPath()
    {
        return Path.Combine(_workspace.RootPath, "_local", "identity-review", "clusters", "results", "moby-cluster-results.tsv");
    }

    private static List<IdentityReviewResultRow> LoadIdentityReviewResultRows(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            return new List<IdentityReviewResultRow>();

        string[] headers = lines[0].Split('\t');
        List<IdentityReviewResultRow> rows = new();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] cells = lines[i].Split('\t');
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < headers.Length; column++)
                values[headers[column]] = column < cells.Length ? cells[column] : "";
            rows.Add(new IdentityReviewResultRow(headers, values));
        }

        return rows;
    }

    private static void WriteIdentityReviewResultRows(string path, IReadOnlyList<IdentityReviewResultRow> rows)
    {
        if (rows.Count == 0)
            return;

        string[] headers = rows[0].Headers;
        StringBuilder builder = new();
        builder.AppendLine(string.Join('\t', headers));
        foreach (IdentityReviewResultRow row in rows)
            builder.AppendLine(string.Join('\t', headers.Select(header => EscapeTsv(row.Get(header)))));
        File.WriteAllText(path, builder.ToString());
    }

    private IdentityReviewResultRow? FindIdentityReviewResultRow(IReadOnlyList<IdentityReviewResultRow> rows, Moby selected, string levelKey)
    {
        string fingerprint = IdentityFingerprint(selected);
        string trueIndex = $"T{selected.TrueIndex}";
        return rows.FirstOrDefault(row =>
                string.Equals(row.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase) &&
                row.TrueIndexes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(index => string.Equals(index, trueIndex, StringComparison.OrdinalIgnoreCase))) ??
            rows.FirstOrDefault(row =>
                string.Equals(row.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(row.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase)) ??
            rows.FirstOrDefault(row => string.Equals(row.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));
    }

    private static List<ClusterReviewResultRow> LoadClusterReviewResultRows(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0)
            return new List<ClusterReviewResultRow>();

        string[] headers = lines[0].Split('\t');
        List<ClusterReviewResultRow> rows = new();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] cells = lines[i].Split('\t');
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < headers.Length; column++)
                values[headers[column]] = column < cells.Length ? cells[column] : "";
            rows.Add(new ClusterReviewResultRow(headers, values));
        }

        return rows;
    }

    private static void WriteClusterReviewResultRows(string path, IReadOnlyList<ClusterReviewResultRow> rows)
    {
        if (rows.Count == 0)
            return;

        string[] headers = rows[0].Headers;
        StringBuilder builder = new();
        builder.AppendLine(string.Join('\t', headers));
        foreach (ClusterReviewResultRow row in rows)
            builder.AppendLine(string.Join('\t', headers.Select(header => EscapeTsv(row.Get(header)))));
        File.WriteAllText(path, builder.ToString());
    }

    private ClusterReviewResultRow? FindClusterReviewResultRow(IReadOnlyList<ClusterReviewResultRow> rows, Moby selected, string levelKey)
    {
        string trueIndex = $"T{selected.TrueIndex}";
        return rows.FirstOrDefault(row =>
            string.Equals(row.LevelKey, levelKey, StringComparison.OrdinalIgnoreCase) &&
            row.UnknownRecords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(record => record.StartsWith(trueIndex, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> SplitClusterBatchPaths(string batchPaths)
    {
        return (batchPaths ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.IsNullOrWhiteSpace(path));
    }

    private static IEnumerable<int> ParseClusterTrueIndexes(string unknownRecords)
    {
        foreach (string record in (unknownRecords ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string text = record.Trim();
            if (!text.StartsWith("T", StringComparison.OrdinalIgnoreCase))
                continue;

            int end = 1;
            while (end < text.Length && char.IsDigit(text[end]))
                end++;
            if (end > 1 && int.TryParse(text[1..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trueIndex))
                yield return trueIndex;
        }
    }

    private static string EscapeTsv(string value)
    {
        return (value ?? "")
            .Replace('\t', ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    private static string FirstNonBlank(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "";
    }

    private string BuildIdentityFamilySummary(Moby moby)
    {
        int fingerprintCount = CountSameIdentityFingerprintMobys(moby);
        int saveScopeCount = CountLiveObservationScopeMobys(moby);
        if (fingerprintCount <= 1 && saveScopeCount <= 1)
            return "ID family: one object in this level.";

        if (fingerprintCount == saveScopeCount)
            return $"ID family: {fingerprintCount} matching object(s) in this level.";

        return $"ID family: {fingerprintCount} object(s) share these bytes; Save Observed ID applies to {saveScopeCount} in this model family.";
    }

    private int CountSameIdentityFingerprintMobys(Moby selected)
    {
        string fingerprint = IdentityFingerprint(selected);
        return _currentMobys.Count(moby =>
            !moby.IsRemoved &&
            string.Equals(IdentityFingerprint(moby), fingerprint, StringComparison.OrdinalIgnoreCase));
    }

    private int CountLiveObservationScopeMobys(Moby selected)
    {
        string fingerprint = IdentityFingerprint(selected);
        return _currentMobys.Count(moby =>
            !moby.IsRemoved &&
            SameLiveObservationScope(moby, selected, fingerprint));
    }

    private int CountSameModelFamilyMobys(Moby selected)
    {
        return _currentMobys.Count(moby =>
            !moby.IsRemoved &&
            SameModelFamilyScope(moby, selected));
    }

    private static bool SameLiveObservationScope(Moby moby, Moby selected, string fingerprint)
    {
        if (!string.Equals(IdentityFingerprint(moby), fingerprint, StringComparison.OrdinalIgnoreCase))
            return false;

        return selected.SpecialDataPointer == 0
            ? moby.TrueIndex == selected.TrueIndex
            : moby.SpecialDataPointer == selected.SpecialDataPointer;
    }

    private static bool SameModelFamilyScope(Moby moby, Moby selected)
    {
        if (moby.Type != selected.Type || moby.SourceByte36 != selected.SourceByte36)
            return false;

        return selected.SpecialDataPointer == 0
            ? moby.TrueIndex == selected.TrueIndex
            : moby.SpecialDataPointer == selected.SpecialDataPointer;
    }

    private static bool SameIdentityObservationScope(JsonElement observation, string fingerprint, Moby selected, string levelKey)
    {
        string existingFingerprint = GetJsonString(observation, "fingerprint");
        if (!string.Equals(existingFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!ObservationScopeMentionsLevel(observation, levelKey))
            return false;

        if (selected.SpecialDataPointer == 0)
        {
            int matchTrueIndex = GetJsonInt32(observation, "matchTrueIndex", -1);
            return matchTrueIndex < 0 || matchTrueIndex == selected.TrueIndex;
        }

        long pointer = FirstJsonInt64(observation, -1, "specialDataPointerHex", "specialDataPointer");
        return pointer < 0 || (uint)pointer == selected.SpecialDataPointer;
    }

    private static bool ObservationScopeMentionsLevel(JsonElement observation, string levelKey)
    {
        string directLevel = FirstJsonString(observation, "levelKey", "sampleLevelKey");
        if (!string.IsNullOrWhiteSpace(directLevel))
            return string.Equals(directLevel, levelKey, StringComparison.OrdinalIgnoreCase);

        if (!observation.TryGetProperty("levels", out JsonElement levels) || levels.ValueKind != JsonValueKind.Array)
            return true;

        return levels.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString())
            .Any(item => string.Equals(item, levelKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstJsonString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            string value = GetJsonString(element, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "";
    }

    private static int GetJsonInt32(JsonElement element, string name, int fallback)
    {
        if (!element.TryGetProperty(name, out JsonElement property))
            return fallback;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value))
            return value;

        if (property.ValueKind == JsonValueKind.String)
        {
            string text = property.GetString() ?? "";
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return value;
            if (int.TryParse(text, out value))
                return value;
        }

        return fallback;
    }

    private static long FirstJsonInt64(JsonElement element, long fallback, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
                continue;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long number))
                return number;

            if (property.ValueKind == JsonValueKind.String)
            {
                string text = (property.GetString() ?? "").Trim();
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
                    return hex;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                    return integer;
            }
        }

        return fallback;
    }

    private static List<Moby> ApplyMobyEditOverlay(string path, IList<Moby> mobys)
    {
        Dictionary<int, Moby> byTrueIndex = mobys
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        List<Moby> changed = new();

        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("edits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return changed;

        foreach (JsonElement edit in edits.EnumerateArray())
        {
            if (!edit.TryGetProperty("trueIndex", out JsonElement trueIndexElement) || !trueIndexElement.TryGetInt32(out int trueIndex))
                continue;
            if (!byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                continue;

            if (edit.TryGetProperty("edited", out JsonElement edited) && edited.ValueKind == JsonValueKind.Object)
            {
                moby.Position = new Vector3f(
                    GetJsonSingle(edited, "x", moby.Position.X),
                    GetJsonSingle(edited, "y", moby.Position.Y),
                    GetJsonSingle(edited, "z", moby.Position.Z));
            }

            string label = GetJsonString(edit, "labelEdited", GetJsonString(edit, "label", moby.Label));
            if (!string.IsNullOrWhiteSpace(label))
                moby.Label = label;

            moby.HasLoadedNativeEdit = true;
            moby.LoadedNativeEditSummary = "identity test batch";
            changed.Add(moby);
        }

        return changed;
    }

    private static string FormatIdentityBatchName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path)
            .Replace("-native-edits", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-identity-batch-", " batch ", StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ');
        if (IsQuickWinIdentityBatch(path))
            name = name.Replace(" batch qw", " quick win ", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name;
    }

    private static bool TryGetObservationArray(JsonElement root, out JsonElement observations)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            observations = root;
            return true;
        }

        if (root.TryGetProperty("observations", out observations) && observations.ValueKind == JsonValueKind.Array)
            return true;

        if (root.TryGetProperty("mobys", out observations) && observations.ValueKind == JsonValueKind.Array)
            return true;

        observations = default;
        return false;
    }

    private static bool IsUsableIdentityObservationLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        string text = label.ToLowerInvariant();
        return !text.Contains("?") &&
            !text.Contains("unknown") &&
            !text.Contains("candidate") &&
            !text.Contains("related") &&
            !IsVagueIdentityObservationLabel(text);
    }

    private static bool IsVagueIdentityObservationLabel(string text)
    {
        text = text.Trim();
        string[] vagueLabels =
        [
            "object",
            "moby",
            "prop",
            "scenery",
            "actor",
            "enemy",
            "chest",
            "control",
            "helper",
            "marker",
            "scenery/prop object",
            "actor/container object",
            "nonvisual control marker",
            "special object"
        ];

        return vagueLabels.Any(label => string.Equals(text, label, StringComparison.OrdinalIgnoreCase));
    }

    private static string IdentityFingerprint(Moby moby)
    {
        return $"type=0x{moby.Type:X2} b36=0x{moby.SourceByte36:X2} f4A=0x{moby.Flag4A:X2} f4B=0x{moby.Flag4B:X2} b4F=0x{moby.SourceByte4F:X2}";
    }

    private static string IdentityObservationKind(Moby moby, string label)
    {
        string text = $"{label} {moby.CandidateKind} {moby.DisplayLabel}".ToLowerInvariant();
        if (text.Contains("chest"))
            return "live-observed chest/object";
        if (text.Contains("gem"))
            return "live-observed gem/collectible";
        if (text.Contains("tree") || text.Contains("prop") || text.Contains("scenery"))
            return "live-observed scenery prop";
        if (text.Contains("flight") || text.Contains("airplane") || text.Contains("copter target") || text.Contains("ring") || text.Contains("arch") || text.Contains("timer number"))
            return "live-observed flight target";
        if (text.Contains("control") || text.Contains("helper") || text.Contains("marker"))
            return "live-observed control/helper";
        if (text.Contains("enemy") || moby.VisualKind == MobyVisualKind.Actor)
            return "live-observed actor/enemy";

        return moby.VisualKind switch
        {
            MobyVisualKind.Chest => "live-observed chest/object",
            MobyVisualKind.Gem => "live-observed gem/collectible",
            MobyVisualKind.Key => "live-observed key",
            MobyVisualKind.Scenery => "live-observed scenery prop",
            MobyVisualKind.FlightTarget => "live-observed flight target",
            MobyVisualKind.Control => "live-observed control/helper",
            _ => "live-observed moby identity"
        };
    }

    private static string IdentityObservationColor(Moby moby, string label)
    {
        string text = $"{label} {moby.CandidateKind} {moby.DisplayLabel}".ToLowerInvariant();
        ColorRgba color =
            text.Contains("chest") ? ColorRgba.FromRgb(230, 126, 34) :
            text.Contains("gem") && moby.Gem != GemValue.Unknown ? moby.Gem.Color :
            text.Contains("tree") || text.Contains("prop") || text.Contains("scenery") ? ColorRgba.FromRgb(88, 214, 141) :
            text.Contains("control") || text.Contains("helper") || text.Contains("marker") ? ColorRgba.FromRgb(143, 166, 184) :
            text.Contains("flight") || text.Contains("airplane") || text.Contains("copter target") ? ColorRgba.FromRgb(91, 196, 255) :
            moby.VisualKind == MobyVisualKind.FlightTarget ? ColorRgba.FromRgb(91, 196, 255) :
            moby.VisualKind == MobyVisualKind.Actor ? ColorRgba.FromRgb(230, 91, 67) :
            moby.Color;

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void RefreshMobyList(Moby? selected = null)
    {
        _syncingMobyList = true;
        List<Moby> all = _currentMobys.Where(moby => !moby.IsRemoved).ToList();
        string filter = _mobySearchBox.Text?.Trim() ?? "";
        int categoryIndex = _mobyCategoryBox.SelectedIndex;
        List<Moby> visible = all
            .Where(moby => MatchesMobyCategory(moby, categoryIndex))
            .Where(moby => string.IsNullOrWhiteSpace(filter) || MatchesMobyFilter(moby, filter))
            .ToList();
        visible = OrderMobysForList(all, visible, categoryIndex);

        _mobyList.ItemsSource = visible;
        _viewport.MobyFilter = moby =>
            !moby.IsRemoved &&
            MatchesMobyCategory(moby, categoryIndex) &&
            (string.IsNullOrWhiteSpace(filter) || MatchesMobyFilter(moby, filter));
        _mobyList.SelectedItem = selected != null && !selected.IsRemoved && visible.Any(moby => ReferenceEquals(moby, selected))
            ? selected
            : null;
        _mobyListHint.Text = BuildMobyListHint(all, visible, categoryIndex, filter);
        RefreshObjectReadinessHint();
        _syncingMobyList = false;
    }

    private static List<Moby> OrderMobysForList(IReadOnlyList<Moby> all, IReadOnlyList<Moby> visible, int categoryIndex)
    {
        if (!IsNeedsIdCategory(categoryIndex))
            return visible.ToList();

        Dictionary<string, int> familyCounts = BuildIdentityFamilyCounts(all);
        return visible
            .OrderByDescending(moby => familyCounts.GetValueOrDefault(IdentityFingerprint(moby), 1))
            .ThenBy(moby => MobyListBadge(moby), StringComparer.OrdinalIgnoreCase)
            .ThenBy(moby => moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(moby => moby.TrueIndex)
            .ToList();
    }

    private static Dictionary<string, int> BuildIdentityFamilyCounts(IEnumerable<Moby> mobys)
    {
        return mobys
            .Where(moby => !moby.IsRemoved)
            .GroupBy(IdentityFingerprint, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsNeedsIdCategory(int categoryIndex)
    {
        return categoryIndex >= 0 &&
            categoryIndex < MobyCategoryOptions.Length &&
            MobyCategoryOptions[categoryIndex].StartsWith("Needs ID", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildMobyListHint(IReadOnlyList<Moby> all, IReadOnlyList<Moby> visible, int categoryIndex, string filter)
    {
        int confirmed = all.Count(IsConfirmedMobyIdentity);
        int inferred = all.Count(IsInferredMobyIdentity);
        int questionable = all.Count(IsQuestionableMoby);
        int unknown = all.Count(moby => moby.VisualKind == MobyVisualKind.Unknown);
        int edited = all.Count(moby => moby.HasAnyEdit);
        int treasure = all.Sum(moby => moby.TreasureValue);
        string category = categoryIndex >= 0 && categoryIndex < MobyCategoryOptions.Length
            ? MobyCategoryOptions[categoryIndex]
            : MobyCategoryOptions[0];
        string scope = string.IsNullOrWhiteSpace(filter) && categoryIndex <= 0
            ? $"{all.Count} object(s)"
            : $"{visible.Count} of {all.Count} shown for {category.ToLowerInvariant()}";

        if (_releaseMode)
            return $"{scope}. Treasure {treasure}; edited {edited}.";

        string priorityHint = "";
        if (IsNeedsIdCategory(categoryIndex) && visible.Count > 0)
        {
            Dictionary<string, int> familyCounts = BuildIdentityFamilyCounts(all);
            int largest = visible.Max(moby => familyCounts.GetValueOrDefault(IdentityFingerprint(moby), 1));
            priorityHint = largest > 1
                ? $" Largest repeated ID family: {largest} object(s), shown first."
                : " Single-object ID checks shown first.";
        }

        return $"{scope}. Treasure {treasure}; confirmed {confirmed}; inferred {inferred}; needs ID {questionable}; unknown {unknown}; edited {edited}.{priorityHint}";
    }

    private static bool MatchesMobyCategory(Moby moby, int categoryIndex)
    {
        string category = categoryIndex >= 0 && categoryIndex < MobyCategoryOptions.Length
            ? MobyCategoryOptions[categoryIndex]
            : MobyCategoryOptions[0];

        return category switch
        {
            "Confirmed IDs" => IsConfirmedMobyIdentity(moby),
            "Observed IDs" => IsObservedMobyIdentity(moby),
            "Mapped IDs" => IsMappedMobyIdentity(moby),
            "Inferred IDs" => IsInferredMobyIdentity(moby),
            "Gems" => moby.VisualKind == MobyVisualKind.Gem,
            "Keys" => moby.VisualKind == MobyVisualKind.Key,
            "Treasure Objects" => IsTreasureObject(moby),
            "Enemies" => IsEnemyOrActor(moby),
            "Fodder" => IsFodderMoby(moby),
            "Egg Thieves" => IsEggThiefMoby(moby),
            "Chests" => moby.VisualKind == MobyVisualKind.Chest,
            "Reward Chests" => IsRewardChestMoby(moby),
            "Flight Targets" => moby.VisualKind == MobyVisualKind.FlightTarget,
            "Dragons" => moby.VisualKind == MobyVisualKind.Dragon,
            "Transport" => IsTransportMoby(moby),
            "Scenery" => moby.VisualKind == MobyVisualKind.Scenery,
            "Portals" => moby.VisualKind == MobyVisualKind.Portal,
            "Whirlwinds" => moby.VisualKind == MobyVisualKind.Whirlwind,
            "Controls" => moby.VisualKind == MobyVisualKind.Control,
            "Unknown" => moby.VisualKind == MobyVisualKind.Unknown,
            "Edited" => moby.HasAnyEdit,
            "Needs ID" => IsQuestionableMoby(moby),
            "Needs ID: Actors" => IsQuestionableMoby(moby) && string.Equals(MobyIdentityReviewLane(moby), "Actor ID", StringComparison.OrdinalIgnoreCase),
            "Needs ID: Scenery" => IsQuestionableMoby(moby) && string.Equals(MobyIdentityReviewLane(moby), "Prop ID", StringComparison.OrdinalIgnoreCase),
            "Needs ID: Controls" => IsQuestionableMoby(moby) && string.Equals(MobyIdentityReviewLane(moby), "Control ID", StringComparison.OrdinalIgnoreCase),
            "Needs ID: Special" => IsQuestionableMoby(moby) && (string.Equals(MobyIdentityReviewLane(moby), "Flight ID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(MobyIdentityReviewLane(moby), "Reward ID", StringComparison.OrdinalIgnoreCase)),
            _ => true
        };
    }

    private static bool IsQuestionableMoby(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
        return text.Contains("?") ||
            moby.VisualKind == MobyVisualKind.Unknown ||
            text.Contains("unknown") ||
            text.Contains("candidate") ||
            text.Contains("pattern-inferred") ||
            text.Contains("needs live") ||
            text.Contains("needs validation") ||
            text.Contains("not confirmed");
    }

    private static bool IsEnemyOrActor(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        if (moby.VisualKind == MobyVisualKind.FlightTarget)
            return false;
        if (IsTransportMoby(moby))
            return false;

        return moby.VisualKind == MobyVisualKind.Actor ||
            text.Contains("enemy") ||
            text.Contains("thief") ||
            text.Contains("gnorc") ||
            text.Contains("fodder") ||
            text.Contains("sheep") ||
            text.Contains("chicken") ||
            text.Contains("dog") ||
            text.Contains("ram") ||
            text.Contains("shepherd") ||
            text.Contains("boss");
    }

    private static bool IsTransportMoby(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        return text.Contains("balloonist") ||
            text.Contains("baloonist") ||
            text.Contains("transport npc") ||
            IsTransportBalloonText(text);
    }

    private static bool IsTransportBalloonText(string text)
    {
        return !text.Contains("balloonist") &&
            !text.Contains("baloonist") &&
            !text.Contains("balloon gnorc") &&
            !text.Contains("balloognorc") &&
            (text.Contains("transport balloon") ||
                text.Contains("balloon scenery") ||
                text.Contains("balloon object") ||
                text.Contains("balloon prop") ||
                text == "balloon");
    }

    private static bool IsTreasureObject(Moby moby)
    {
        return moby.VisualKind is MobyVisualKind.Gem or MobyVisualKind.Key or MobyVisualKind.Chest ||
            moby.TreasureValue > 0 ||
            IsRewardChestMoby(moby);
    }

    private static bool IsRewardChestMoby(Moby moby)
    {
        if (moby.VisualKind != MobyVisualKind.Chest)
            return false;

        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Evidence}".ToLowerInvariant();
        return moby.TreasureValue > 0 ||
            moby.Flag4B is 0x53 or 0x54 or 0x55 or 0x56 or 0x57 ||
            moby.SourceByte4F is 0x53 or 0x54 or 0x55 or 0x56 or 0x57 ||
            text.Contains("reward") ||
            text.Contains("gem") ||
            text.Contains("life chest") ||
            text.Contains("key chest");
    }

    private static bool IsFodderMoby(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        return text.Contains("fodder") ||
            text.Contains("sheep") ||
            text.Contains("chicken") ||
            text.Contains("rabbit") ||
            text.Contains("bat") ||
            text.Contains("rat") ||
            text.Contains("mushroom") ||
            text.Contains("goat");
    }

    private static bool IsEggThiefMoby(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        return text.Contains("egg thief") || text.Contains("egg theif");
    }

    private static string FormatMobyListItem(Moby moby)
    {
        string badge = MobyListBadge(moby);
        return $"{moby.TrueIndex,3}  [{badge}] {moby.DisplayLabel}  0x{moby.Type:X2}{(moby.HasAnyEdit ? " *" : "")}";
    }

    private Control BuildMobyListItemControl(Moby? moby)
    {
        if (moby == null)
            return new TextBlock();

        bool questionable = IsQuestionableMoby(moby);
        Border border = new()
        {
            BorderBrush = new SolidColorBrush(ToAvaloniaColor(moby.Color)),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Background = questionable
                ? new SolidColorBrush(Color.FromRgb(255, 246, 196))
                : Brushes.Transparent,
            Padding = new Thickness(8, 5),
            Margin = new Thickness(2, 3)
        };

        StackPanel stack = new() { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = $"T{moby.TrueIndex}  {MobyListBadge(moby)}  {moby.DisplayLabel}{(moby.HasAnyEdit ? "  edited" : "")}",
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45)),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = BuildMobyListSubtitle(moby),
            Foreground = new SolidColorBrush(Color.FromRgb(78, 88, 99)),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16
        });
        border.Child = stack;
        return border;
    }

    private string BuildMobyListSubtitle(Moby moby)
    {
        bool questionable = IsQuestionableMoby(moby);
        List<string> parts =
        [
            BuildMobyIdentityStatus(moby),
            BuildFriendlyMobySummary(moby)
        ];

        if (moby.TreasureValue > 0)
            parts.Add($"{moby.TreasureValue} gem value");
        if (moby.Links.Count > 0)
            parts.Add($"{moby.Links.Count} link(s)");
        if (questionable)
        {
            string reviewLane = MobyIdentityReviewLane(moby);
            if (!string.IsNullOrWhiteSpace(reviewLane))
                parts.Add(reviewLane);
            MobyIdentityFamilyObservation? observation = FindIdentityFamilyObservationForMoby(moby);
            if (observation?.RosterCandidates.Count > 0)
                parts.Add($"possible: {string.Join(", ", observation.RosterCandidates.Take(3))}");
            int familyCount = CountSameIdentityFingerprintMobys(moby);
            int modelCount = CountSameModelFamilyMobys(moby);
            if (familyCount > 1)
                parts.Add($"{familyCount} same ID family");
            if (modelCount > 1 && modelCount != familyCount)
                parts.Add($"{modelCount} same model");
            parts.Add($"type 0x{moby.Type:X2}, state 0x{moby.State:X2}");
            parts.Add($"bytes {MobySourceByteSummary(moby)}");
        }

        return string.Join("  |  ", parts);
    }

    private static string BuildFriendlyMobySummary(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        if (moby.VisualKind == MobyVisualKind.Gem && moby.Gem != GemValue.Unknown)
            return moby.Gem.DisplayName;
        if (moby.VisualKind == MobyVisualKind.Key)
            return "Key collectible";
        if (moby.VisualKind == MobyVisualKind.Chest)
            return moby.TreasureValue > 0 ? "Treasure chest" : "Chest object";
        if (moby.VisualKind == MobyVisualKind.Dragon)
            return "Dragon rescue";
        if (moby.VisualKind == MobyVisualKind.Portal)
            return "Portal / level entry";
        if (moby.VisualKind == MobyVisualKind.Whirlwind)
            return "Whirlwind lift";
        if (moby.VisualKind == MobyVisualKind.FlightTarget)
            return "Flight target";
        if (moby.VisualKind == MobyVisualKind.Control)
            return "Control/helper marker";
        if (moby.VisualKind == MobyVisualKind.Scenery)
            return "Scenery prop";
        if (moby.VisualKind == MobyVisualKind.Actor)
        {
            if (text.Contains("fodder") || text.Contains("sheep") || text.Contains("chicken") || text.Contains("mushroom"))
                return "Fodder";
            if (text.Contains("thief"))
                return "Thief/enemy";
            return "Enemy or actor";
        }

        return IsQuestionableMoby(moby) ? "Needs live identification" : "Mapped object";
    }

    private static string BuildMobyIdentityStatus(Moby moby)
    {
        string confidence = moby.Confidence.Trim();
        string text = $"{confidence} {moby.Evidence}".ToLowerInvariant();
        if (text.Contains("live-observed"))
            return "Live observed";
        if (text.Contains("source-proven"))
            return "Source proven";
        if (text.Contains("observed-fingerprint") || text.Contains("source-signature-observed"))
            return "Observed";
        if (text.Contains("cross-level-template"))
            return "Template";
        if (text.Contains("pattern-inferred"))
            return "Inferred";
        if (text.Contains("byte-pattern") || text.Contains("guide-roster-count"))
            return "Mapped";
        if (IsQuestionableMoby(moby))
            return "Needs ID";
        if (!string.IsNullOrWhiteSpace(confidence))
            return confidence;

        return moby.VisualKind == MobyVisualKind.Unknown ? "Needs ID" : "Mapped";
    }

    private static string MobySourceByteSummary(Moby moby)
    {
        return $"36/4A/4B/4F 0x{moby.SourceByte36:X2}/0x{moby.Flag4A:X2}/0x{moby.Flag4B:X2}/0x{moby.SourceByte4F:X2}";
    }

    private static string MobyListBadge(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind}".ToLowerInvariant();
        if (moby.VisualKind == MobyVisualKind.Key)
            return "Key";
        if (moby.VisualKind == MobyVisualKind.Gem && moby.Gem.Value > 0)
            return $"Gem {moby.Gem.Value}";
        if (moby.VisualKind == MobyVisualKind.FlightTarget)
            return FlightTargetBadge(text);
        if (text.Contains("dog"))
            return "Dog";
        if (text.Contains("bird"))
            return "Bird";
        if (text.Contains("chicken"))
            return "Chicken";
        if (text.Contains("gnorc"))
            return "Gnorc";
        if (text.Contains("sheep") || text.Contains("fodder"))
            return "Fodder";
        if (text.Contains("ram"))
            return "Ram";
        if (text.Contains("shepherd"))
            return "Shepherd";
        if (text.Contains("balloonist"))
            return "Balloonist";
        if (text.Contains("copter"))
            return "Copter";
        if (text.Contains("toasty") || text.Contains("boss"))
            return "Boss";
        if (text.Contains("egg thief"))
            return "Egg Thief";
        if (text.Contains("thief"))
            return "Thief";
        if (text.Contains("key chest") || text.Contains("key-required"))
            return "Key Chest";
        if (text.Contains("locked"))
            return "Locked Chest";
        if (text.Contains("spring"))
            return "Spring Chest";
        if (text.Contains("firework"))
            return "Firework";
        if (text.Contains("super flame"))
            return "Super Flame";
        if (text.Contains("3x flame"))
            return "3x Flame";
        if (text.Contains("life"))
            return "Life Chest";
        if (text.Contains("metal"))
            return "Metal Chest";
        if (text.Contains("regular"))
            return "Regular Chest";
        if (text.Contains("breakable"))
            return "Break Chest";
        if (text.Contains("flame/charge") || text.Contains("flame-or-charge"))
            return "Flame/Charge";
        if (text.Contains("flame"))
            return "Flame Chest";
        if (text.Contains("charge"))
            return "Charge Chest";
        if (text.Contains("tree"))
            return "Tree";
        if (text.Contains("flower"))
            return "Flower";
        if (text.Contains("grass"))
            return "Grass";
        if (text.Contains("lamp"))
            return "Lamp";
        if (text.Contains("flag"))
            return "Flag";
        if (text.Contains("return-home") || text.Contains("return home"))
            return "Return";
        if (text.Contains("whirlwind"))
            return "Whirlwind";
        if (IsQuestionableMoby(moby))
        {
            string reviewLane = MobyIdentityReviewLane(moby);
            if (!string.IsNullOrWhiteSpace(reviewLane))
                return reviewLane;
        }

        return moby.VisualKind switch
        {
            MobyVisualKind.Gem => "Gem",
            MobyVisualKind.Key => "Key",
            MobyVisualKind.Chest => "Chest",
            MobyVisualKind.Dragon => "Dragon",
            MobyVisualKind.Actor => "Actor",
            MobyVisualKind.FlightTarget => "Flight",
            MobyVisualKind.Portal => "Portal",
            MobyVisualKind.Scenery => "Scenery",
            MobyVisualKind.Whirlwind => "Whirlwind",
            MobyVisualKind.Control => "Control",
            _ => "Unknown"
        };
    }

    private static string FlightTargetBadge(string text)
    {
        if (text.Contains("timer"))
            return "Timer";
        if (text.Contains("airplane") || text.Contains("plane"))
            return "Airplane";
        if (text.Contains("copter"))
            return "Copter Target";
        if (text.Contains("train"))
            return "Train";
        if (text.Contains("lighthouse"))
            return "Lighthouse";
        if (text.Contains("boat"))
            return "Boat";
        if (text.Contains("ring"))
            return "Ring";
        if (text.Contains("arch"))
            return "Arch";
        if (text.Contains("chest"))
            return "Flight Chest";

        return "Flight";
    }

    private static string MobyIdentityReviewLane(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote} {moby.Evidence}".ToLowerInvariant();
        if (moby.VisualKind == MobyVisualKind.FlightTarget)
            return "Flight ID";

        if (text.Contains("actor/container") ||
            text.Contains("actor or interactive") ||
            text.Contains("enemy/fodder actor") ||
            text.Contains("actor-like"))
        {
            return "Actor ID";
        }

        if (text.Contains("nonvisual control") ||
            text.Contains("control/helper") ||
            text.Contains("control or helper") ||
            text.Contains("control marker") ||
            text.Contains("helper candidate"))
        {
            return "Control ID";
        }

        if (text.Contains("flight/special") ||
            text.Contains("flight target") ||
            text.Contains("flight record") ||
            text.Contains("special object"))
        {
            return "Flight ID";
        }

        if (text.Contains("interactive reward") ||
            text.Contains("reward object"))
        {
            return "Reward ID";
        }

        if (text.Contains("scenery/prop") ||
            text.Contains("visual scenery") ||
            text.Contains("scenery prop") ||
            text.Contains("large scenery"))
        {
            return "Prop ID";
        }

        return moby.VisualKind switch
        {
            MobyVisualKind.Actor => "Actor ID",
            MobyVisualKind.FlightTarget => "Flight ID",
            MobyVisualKind.Control => "Control ID",
            MobyVisualKind.Scenery => "Prop ID",
            _ => ""
        };
    }

    private bool MatchesMobyFilter(Moby moby, string filter)
    {
        string[] tokens = filter.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length == 0 || tokens.All(token => MatchesMobyFilterToken(moby, token));
    }

    private bool MatchesMobyFilterToken(Moby moby, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        string normalized = token.Trim();
        if (TryMatchIdentityFamilyFilter(moby, normalized))
            return true;
        if (IsIdentityFamilyFilter(normalized))
            return false;
        if (TryMatchModelFamilyFilter(moby, normalized))
            return true;
        if (IsModelFamilyFilter(normalized))
            return false;

        if (token.Equals("edited", StringComparison.OrdinalIgnoreCase))
            return moby.HasAnyEdit;
        if (token.Equals("added", StringComparison.OrdinalIgnoreCase))
            return moby.IsAdded;
        if (token.Equals("linked", StringComparison.OrdinalIgnoreCase))
            return moby.Links.Count > 0;
        if (token.Equals("confirmed", StringComparison.OrdinalIgnoreCase))
            return IsConfirmedMobyIdentity(moby);
        if (token.Equals("observed", StringComparison.OrdinalIgnoreCase))
            return IsObservedMobyIdentity(moby);
        if (token.Equals("mapped", StringComparison.OrdinalIgnoreCase))
            return IsMappedMobyIdentity(moby);
        if (token.Equals("inferred", StringComparison.OrdinalIgnoreCase))
            return IsInferredMobyIdentity(moby);
        if (token.Equals("needsid", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("needs-id", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("questionable", StringComparison.OrdinalIgnoreCase))
        {
            return IsQuestionableMoby(moby);
        }

        if (normalized.StartsWith("T", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalized[1..], out int trueIndex))
            return moby.TrueIndex == trueIndex;

        if (TryParseInt(normalized, out int number) && number == moby.TrueIndex)
            return true;

        string searchable =
            $"T{moby.TrueIndex} L{moby.LegacyIndex} {moby.DisplayLabel} {moby.OriginalLabel} {MobyListBadge(moby)} {MobyIdentityReviewLane(moby)} {BuildMobyIdentityStatus(moby)} {moby.VisualKind} " +
            $"0x{moby.Type:X2} {moby.Type} 0x{moby.State:X2} {moby.State} " +
            $"{moby.CandidateKind} {moby.Confidence} {moby.ZoneLabel} {moby.Evidence} {moby.BehaviorNote} {moby.PatchStatus} {moby.PatchLead} " +
            $"{moby.Gem.DisplayName} {string.Join(' ', moby.Links.Select(link => $"{link.DisplayName} {link.Kind} {link.Reason}"))}";

        return searchable.Contains(normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMatchIdentityFamilyFilter(Moby moby, string token)
    {
        if (!TryExtractIdentityFamilyFilter(token, out string familyKey))
            return false;

        return string.Equals(IdentityFilterKey(moby), familyKey, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryMatchModelFamilyFilter(Moby moby, string token)
    {
        if (!TryExtractModelFamilyFilter(token, out string modelKey))
            return false;

        return string.Equals(ModelFamilyFilterKey(moby), modelKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIdentityFamilyFilter(string token)
    {
        return token.StartsWith("family:", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("idfamily:", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("fingerprint:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModelFamilyFilter(string token)
    {
        return token.StartsWith("model:", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("modelfamily:", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("package:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractIdentityFamilyFilter(string token, out string familyKey)
    {
        familyKey = "";
        string prefix = "";
        if (token.StartsWith("family:", StringComparison.OrdinalIgnoreCase))
            prefix = "family:";
        else if (token.StartsWith("idfamily:", StringComparison.OrdinalIgnoreCase))
            prefix = "idfamily:";
        else if (token.StartsWith("fingerprint:", StringComparison.OrdinalIgnoreCase))
            prefix = "fingerprint:";

        if (string.IsNullOrWhiteSpace(prefix))
            return false;

        familyKey = NormalizeIdentityFilterKey(token[prefix.Length..]);
        return !string.IsNullOrWhiteSpace(familyKey);
    }

    private static bool TryExtractModelFamilyFilter(string token, out string modelKey)
    {
        modelKey = "";
        string prefix = "";
        if (token.StartsWith("model:", StringComparison.OrdinalIgnoreCase))
            prefix = "model:";
        else if (token.StartsWith("modelfamily:", StringComparison.OrdinalIgnoreCase))
            prefix = "modelfamily:";
        else if (token.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
            prefix = "package:";

        if (string.IsNullOrWhiteSpace(prefix))
            return false;

        modelKey = NormalizeIdentityFilterKey(token[prefix.Length..]);
        return !string.IsNullOrWhiteSpace(modelKey);
    }

    private static string IdentityFilterKey(Moby moby)
    {
        return NormalizeIdentityFilterKey($"{moby.Type:X2}-{moby.SourceByte36:X2}-{moby.Flag4A:X2}-{moby.Flag4B:X2}-{moby.SourceByte4F:X2}");
    }

    private string ModelFamilyFilterKey(Moby moby)
    {
        string levelKey = _currentLevel?.Key ?? "";
        string modelScope = moby.SpecialDataPointer == 0
            ? $"T{moby.TrueIndex:X4}"
            : $"{moby.SpecialDataPointer:X8}";
        return NormalizeIdentityFilterKey($"{levelKey}-{moby.Type:X2}-{moby.SourceByte36:X2}-{modelScope}");
    }

    private static string NormalizeIdentityFilterKey(string key)
    {
        string cleaned = key.Trim();
        if (cleaned.Contains('='))
        {
            List<string> values = new();
            foreach (string part in cleaned.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int index = part.IndexOf('=');
                if (index >= 0)
                    values.Add(part[(index + 1)..]);
            }
            cleaned = string.Join("-", values);
        }

        cleaned = cleaned.Replace("0x", "", StringComparison.OrdinalIgnoreCase)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal)
            .Replace(":", "-", StringComparison.Ordinal);
        return string.Join("-", cleaned
            .Split(['-', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.ToUpperInvariant()));
    }

    private void SelectMobyInList(Moby? moby)
    {
        _syncingMobyList = true;
        _mobyList.SelectedItem = moby != null && !moby.IsRemoved ? moby : null;
        _syncingMobyList = false;
    }

    private void RefreshLinkedMobyList(Moby? selected)
    {
        _syncingLinkedMobyList = true;
        List<LinkedMobyItem> linked = selected == null
            ? new List<LinkedMobyItem>()
            : BuildLinkedMobyItems(selected).ToList();
        _linkedMobyList.ItemsSource = linked;
        _linkedMobyList.SelectedItem = null;
        _linkedMobyHint.Text = selected == null
            ? "Select an object to see its related mobys."
            : linked.Count == 0
                ? "No linked objects mapped for this moby yet."
                : $"{linked.Count} related object(s). {BuildLinkedSummary(selected)}";
        _syncingLinkedMobyList = false;
    }

    private string BuildLinkedSummary(Moby selected)
    {
        List<string> groups = selected.Links
            .Where(MobyLinkTraversal.IsVisibleLink)
            .GroupBy(link => string.IsNullOrWhiteSpace(link.Kind) ? "linked group" : link.Kind, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.SelectMany(link => link.TrueIndexes).Distinct().Count()} mobys")
            .Take(3)
            .ToList();
        return groups.Count == 0 ? "" : string.Join("; ", groups);
    }

    private void FocusSelectedLinkedGroup()
    {
        if (_selectedMoby == null)
        {
            _statusText.Text = "Select an object with linked mobys first.";
            return;
        }

        List<Moby> group = BuildLinkedGroupMobys(_selectedMoby).ToList();
        if (group.Count <= 1)
        {
            _statusText.Text = "This object does not have a linked group yet.";
            return;
        }

        _viewport.FocusMobys(group);
        _statusText.Text = $"Framed {_selectedMoby.DisplayLabel} with {group.Count - 1} linked object(s).";
    }

    private IEnumerable<Moby> BuildLinkedGroupMobys(Moby selected)
    {
        yield return selected;
        HashSet<int> seen = new() { selected.TrueIndex };
        foreach (int trueIndex in MobyLinkTraversal.GetVisibleLinks(selected).SelectMany(link => MobyLinkTraversal.GetVisibleLinkTrueIndexes(selected, link)))
        {
            if (!seen.Add(trueIndex))
                continue;

            Moby? moby = _currentMobys.FirstOrDefault(item => !item.IsRemoved && item.TrueIndex == trueIndex);
            if (moby != null)
                yield return moby;
        }
    }

    private IEnumerable<LinkedMobyItem> BuildLinkedMobyItems(Moby selected)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (MobyLink link in MobyLinkTraversal.GetVisibleLinks(selected))
        {
            string relationship = string.IsNullOrWhiteSpace(link.DisplayName) ? link.Kind : link.DisplayName;
            foreach (int trueIndex in MobyLinkTraversal.GetVisibleLinkTrueIndexes(selected, link))
            {
                Moby? linked = _currentMobys.FirstOrDefault(moby => !moby.IsRemoved && moby.TrueIndex == trueIndex);
                if (linked == null)
                    continue;

                string key = $"{relationship}|{linked.TrueIndex}";
                if (seen.Add(key))
                    yield return new LinkedMobyItem(linked, relationship);
            }
        }
    }

    private GeometryCandidate? TryLoadGeometry(string levelKey)
    {
        TerrainGeometryLoadData loaded = LoadGeometryData(levelKey);
        _loadedTerrainEdits = loaded.LoadedTerrainEdits;
        _terrainCacheHealthMessage = loaded.TerrainCacheHealthMessage;
        return loaded.Geometry;
    }

    private LevelLoadData LoadLevelData(string levelKey)
    {
        TerrainGeometryLoadData geometry = LoadGeometryData(levelKey);
        MobyLoadData mobys = LoadMobyData(levelKey);
        TerrainCollisionLoadData collision = LoadTerrainCollisionData(levelKey, geometry.Geometry);
        IReadOnlyList<CustomTerrainTextureImport> customTextures = CustomTerrainTextureStore.Load(_workspace.RootPath, levelKey);
        CustomTerrainTexturePreview.Apply(geometry.Geometry, customTextures);

        return new LevelLoadData(
            geometry.Geometry,
            geometry.TerrainCacheHealthMessage,
            geometry.LoadedTerrainEdits,
            collision.TriangleKeys,
            collision.ReadinessMessage,
            collision.MatchedFaces,
            mobys.Mobys,
            mobys.LoadedMobyEdits,
            mobys.Metadata,
            customTextures);
    }

    private TerrainGeometryLoadData LoadGeometryData(string levelKey)
    {
        int loadedTerrainEdits = 0;
        string terrainCacheHealthMessage = "";
        string cachePath = Path.Combine(_workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        string sourceDerivedPath = SourceDerivedTerrainOverlayPath(levelKey);
        string path = File.Exists(cachePath)
            ? cachePath
            : _workspace.ResolveFile($"{levelKey}-runtime-scene-editor-overlay.json", "generated-research");
        if (!File.Exists(path))
        {
            if (TryRecoverGeometryCacheFromSourceQuiet(levelKey, sourceDerivedPath, out terrainCacheHealthMessage))
            {
                path = sourceDerivedPath;
            }
            else
            {
                string sourceImage = FirstExistingDiscImagePath(DiscImageLocator.FindImage(_workspace));
                if (File.Exists(sourceImage))
                    terrainCacheHealthMessage = string.IsNullOrWhiteSpace(terrainCacheHealthMessage)
                        ? _releaseMode
                            ? "This level map has not been built yet. Use Open BIN/CUE, wait for the build to finish, then choose the level again."
                            : "This level map has not been built yet. Use Open BIN/CUE or Advanced > Map > Build Local Cache, wait for it to finish, then choose the level again."
                        : terrainCacheHealthMessage;
                return new TerrainGeometryLoadData(null, loadedTerrainEdits, terrainCacheHealthMessage);
            }
        }

        try
        {
            GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay(levelKey, path);
            if (healthIssue?.BlocksLoading == true)
            {
                if (TryRecoverGeometryCacheFromSourceQuiet(levelKey, sourceDerivedPath, out terrainCacheHealthMessage))
                {
                    path = sourceDerivedPath;
                }
                else
                {
                    terrainCacheHealthMessage = string.IsNullOrWhiteSpace(terrainCacheHealthMessage)
                        ? healthIssue.Message
                        : terrainCacheHealthMessage;
                    return new TerrainGeometryLoadData(null, loadedTerrainEdits, terrainCacheHealthMessage);
                }
            }

            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(path);
            string ramPath = TerrainPatchDataLocator.FindRamDump(_workspace, levelKey);
            if (!File.Exists(ramPath) && !IsSourceDerivedGeometry(geometry) && TryRecoverGeometryCacheFromSourceQuiet(levelKey, sourceDerivedPath, out terrainCacheHealthMessage))
                geometry = GeometryOverlayLoader.LoadFirstCandidate(sourceDerivedPath);

            if (!File.Exists(ramPath) && IsSourceDerivedGeometry(geometry))
                terrainCacheHealthMessage = "Using a BIN/CUE-recovered terrain map for this level so terrain edits can use source WAD offsets without a RAM capture.";
            TerrainMaterialClassifier.Apply(levelKey, _workspace.RootPath, geometry);
            string editsPath = Path.Combine(_workspace.RootPath, $"{levelKey}-terrain-edits.json");
            loadedTerrainEdits = TerrainEditStore.Load(editsPath, geometry.Polygons);
            return new TerrainGeometryLoadData(geometry, loadedTerrainEdits, terrainCacheHealthMessage);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            return new TerrainGeometryLoadData(null, loadedTerrainEdits, $"Could not load this level map: {ex.Message}");
        }
    }

    private string SourceDerivedTerrainOverlayPath(string levelKey)
    {
        return Path.Combine(_workspace.RootPath, "_local", "terrain", "source-derived-overlays", $"{levelKey}-runtime-scene-editor-overlay.json");
    }

    private bool TryRecoverGeometryCacheFromSource(string levelKey, string outputPath)
    {
        bool recovered = TryRecoverGeometryCacheFromSourceQuiet(levelKey, outputPath, out string message);
        if (!recovered && !string.IsNullOrWhiteSpace(message))
            _terrainCacheHealthMessage = message;
        string wadAnalysisPath = WadAnalysisLocator.Find(_workspace);
        if (File.Exists(wadAnalysisPath))
            _skyboxWadAnalysisPathBox.Text = wadAnalysisPath;
        return recovered;
    }

    private bool TryRecoverGeometryCacheFromSourceQuiet(string levelKey, string outputPath, out string message)
    {
        message = "";
        LevelDefinition? level = _catalog.FindByKey(levelKey);
        if (level == null || level.SourceWadEntry < 0)
            return false;

        string sourceImage = FirstExistingDiscImagePath(DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
            return false;

        try
        {
            string wadAnalysisPath = WadAnalysisLocator.Find(_workspace);
            if (!File.Exists(wadAnalysisPath))
            {
                wadAnalysisPath = Path.Combine(_workspace.RootPath, "spyro-wad-analysis.json");
                WadAnalysisBuilder.BuildAsync(sourceImage, wadAnalysisPath).GetAwaiter().GetResult();
            }

            SourceSceneOverlayExporter.Export(sourceImage, wadAnalysisPath, level, outputPath);
            return File.Exists(outputPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            message = $"Could not build this level map from the selected BIN/CUE: {ex.Message}";
            return false;
        }
    }

    private List<Moby> TryLoadMobys(string levelKey)
    {
        MobyLoadData loaded = LoadMobyData(levelKey);
        _loadedMobyEdits = loaded.LoadedMobyEdits;
        _mobyMetadata = loaded.Metadata;
        return loaded.Mobys;
    }

    private MobyLoadData LoadMobyData(string levelKey)
    {
        int loadedMobyEdits = 0;
        string cachePath = Path.Combine(_workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");

        try
        {
            List<Moby> mobys;
            if (File.Exists(cachePath))
            {
                mobys = MobyLoader.LoadCached(cachePath).ToList();
            }
            else
            {
                LevelDefinition? level = _catalog.FindByKey(levelKey);
                if (level != null && TryRecoverMobyCacheFromSourceQuiet(level, cachePath))
                {
                    mobys = MobyLoader.LoadCached(cachePath).ToList();
                }
                else
                {
                    string ramPath = _workspace.ResolveFile($"{levelKey}-before-clean.bin", "game-and-capture-artifacts");
                    if (!File.Exists(ramPath))
                        ramPath = _workspace.ResolveFile($"{levelKey}-before-gem-clean.bin", "game-and-capture-artifacts");
                    if (!File.Exists(ramPath))
                        return new MobyLoadData(new List<Moby>(), loadedMobyEdits, default);

                    mobys = MobyLoader.LoadRamDump(ramPath).ToList();
                }
            }
            MobyMetadataResult metadata = MobyMetadataEnricher.Apply(_workspace, levelKey, mobys);
            string editsPath = Path.Combine(_workspace.RootPath, $"{levelKey}-native-edits.json");
            loadedMobyEdits = MobyEditStore.Load(editsPath, mobys);
            MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
            return new MobyLoadData(mobys, loadedMobyEdits, metadata);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or JsonException)
        {
            Debug.WriteLine(ex);
            return new MobyLoadData(new List<Moby>(), loadedMobyEdits, default);
        }
    }

    private bool TryRecoverMobyCacheFromSourceQuiet(LevelDefinition level, string outputPath)
    {
        if (!level.HasSourceTable)
            return false;

        string sourceImage = FirstExistingDiscImagePath(DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
            return false;

        try
        {
            SourceMobyCacheBuilder.BuildAsync(sourceImage, level, outputPath).GetAwaiter().GetResult();
            return File.Exists(outputPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }

    private string BuildMissingDataMessage(string levelKey)
    {
        string cacheDir = Path.Combine(_workspace.RootPath, "editor-cache");
        string overlayCachePath = Path.Combine(cacheDir, $"{levelKey}-runtime-scene-editor-overlay.json");
        string mobyCachePath = Path.Combine(cacheDir, $"{levelKey}-mobys.json");
        string overlayRootPath = _workspace.ResolveFile($"{levelKey}-runtime-scene-editor-overlay.json", "generated-research");
        string ramPath = _workspace.ResolveFile($"{levelKey}-before-clean.bin", "game-and-capture-artifacts");

        List<string> missing = new();
        if (_currentGeometry == null)
            missing.Add(!string.IsNullOrWhiteSpace(_terrainCacheHealthMessage)
                ? _terrainCacheHealthMessage
                : File.Exists(overlayRootPath) ? "terrain overlay failed to parse" : $"missing {Path.GetFileName(overlayCachePath)}");
        if (_currentMobys.Count == 0)
            missing.Add(File.Exists(ramPath) ? "moby RAM/cache failed to parse" : $"missing {Path.GetFileName(mobyCachePath)} or {Path.GetFileName(ramPath)}");

        if (missing.Count == 0)
            return "";

        return "This level is in the catalog, but captured editor data is not loaded yet: "
            + string.Join(", ", missing)
            + (_releaseMode
                ? ". Click Open BIN/CUE to build terrain and object maps from your Spyro disc."
                : ". Click Open BIN/CUE to build terrain and object maps from your Spyro disc. If you already selected a disc, run Build Local Editor Cache or Advanced > Map > Build Local Cache.");
    }

    private static string BuildSurfaceSummary(GeometryCandidate? geometry)
    {
        IReadOnlyDictionary<string, int> counts = TerrainMaterialClassifier.CountSurfaces(geometry);
        if (counts.Count == 0)
            return "none";

        return string.Join(", ", counts
            .Take(4)
            .Select(pair => $"{TerrainMaterialClassifier.FormatSurface(pair.Key)} {pair.Value}"));
    }

    private static string BuildTerrainSurfaceEditStatus(TerrainPolygon terrain)
    {
        if (terrain.IsTerrainRemoved)
            return "staged for removal; Create BIN neutralizes the visible face and matched collision";
        if (terrain.IsTerrainAddClone)
            return "staged as an added copy; Create BIN adds independent visible terrain and may add same-cell copied collision";

        string behaviorConfidence = terrain.BehaviorConfidence.ToLowerInvariant();
        string surfaceSource = terrain.SurfaceSource.ToLowerInvariant();
        bool hasBehaviorProof = behaviorConfidence.Contains("proof", StringComparison.OrdinalIgnoreCase) ||
            behaviorConfidence.Contains("live", StringComparison.OrdinalIgnoreCase) ||
            behaviorConfidence.Contains("observed", StringComparison.OrdinalIgnoreCase);
        bool hasMaterialProof = surfaceSource.Contains("proof", StringComparison.OrdinalIgnoreCase) ||
            surfaceSource.Contains("source", StringComparison.OrdinalIgnoreCase);

        if (hasBehaviorProof)
            return "behavior proof available for this face/surface";
        if (surfaceSource.Contains("override", StringComparison.OrdinalIgnoreCase))
            return "editor material override; use Material Look, Borrow In-Game Look, or selected-face/shared-texture color tools for texture art, and proof captures for water/goo/damage";
        if (TerrainBehaviorClassifier.FormatBehavior(terrain.Behavior).Contains("hazard", StringComparison.OrdinalIgnoreCase))
            return "hazard-looking surface, but keep using proof captures before trusting in-game damage";
        if (hasMaterialProof)
            return "visual/material label is mapped; in-game behavior still needs proof for water/goo/damage";

        return "visual/material label only; use terrain proof tools before treating it as water/goo/damage";
    }

    private string BuildMobyMetadataDetails(Moby moby)
    {
        List<string> lines = new();
        string identityStatus = BuildMobyIdentityStatus(moby);
        if (!string.IsNullOrWhiteSpace(identityStatus))
            lines.Add($"Identity: {identityStatus}");
        if (IsQuestionableMoby(moby))
        {
            lines.Add(BuildIdentityFamilySummary(moby));
            lines.Add($"ID fingerprint: {IdentityFingerprint(moby)}");
            lines.Add("ID note: use a test batch or Save Observed ID after checking the object in game.");
        }
        if (!string.IsNullOrWhiteSpace(moby.CandidateKind))
            lines.Add($"Kind: {moby.CandidateKind}");
        if (!string.IsNullOrWhiteSpace(moby.Confidence))
            lines.Add($"Confidence: {moby.Confidence}");
        if (!string.IsNullOrWhiteSpace(moby.ZoneLabel))
            lines.Add($"Area/group: {moby.ZoneLabel}");
        if (!string.IsNullOrWhiteSpace(moby.BehaviorNote))
            lines.Add($"Note: {moby.BehaviorNote}");
        if (!string.IsNullOrWhiteSpace(moby.Evidence))
            lines.Add($"Evidence: {moby.Evidence}");
        if (moby.Links.Count > 0)
        {
            foreach (MobyLink link in moby.Links.Take(3))
            {
                string members = string.Join(", ", link.TrueIndexes.Where(index => index != moby.TrueIndex).Select(index => $"T{index}"));
                lines.Add($"Link: {link.DisplayName}{(string.IsNullOrWhiteSpace(members) ? "" : $" ({members})")}");
            }
        }
        List<Moby> contents = GetChestContentMobys(moby).ToList();
        if (contents.Count > 0)
        {
            lines.Add($"Chest contents: {BuildGemSummary(contents)}");
            foreach (Moby content in contents.OrderBy(content => content.TrueIndex))
                lines.Add($"  T{content.TrueIndex}: {content.Gem.DisplayName}");
        }
        else if (moby.IsChestContent)
        {
            lines.Add($"Gem: {moby.Gem.DisplayName}");
        }

        if (lines.Count == 0)
            return "";

        return string.Join("\n", lines) + "\n";
    }

    private static string BuildGemSummary(IEnumerable<Moby> contents)
    {
        List<Moby> gems = contents.ToList();
        int total = gems.Sum(gem => gem.Gem.Value);
        return $"{gems.Count} gem record(s), total value {total}";
    }

    private static Button NewButton(string text, Action? onClick = null)
    {
        Button button = NewButtonShell(text);
        if (onClick != null)
        {
            button.Click += (_, _) =>
            {
                try
                {
                    onClick();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            };
        }

        return button;
    }

    private static Button NewAsyncButton(string text, Func<Task> onClick)
    {
        Button button = NewButtonShell(text);
        button.Click += async (_, _) =>
        {
            if (!button.IsEnabled)
                return;

            button.IsEnabled = false;
            try
            {
                await onClick();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                button.IsEnabled = true;
            }
        };

        return button;
    }

    private static void StyleObjectEditButton(Button button)
    {
        StyleActionButton(button, Color.FromRgb(39, 139, 83), Colors.White, 114);
    }

    private static void StyleCreateBinButton(Button button)
    {
        StyleActionButton(button, Color.FromRgb(31, 105, 191), Colors.White, 112);
    }

    private static void StyleRestoreLevelButton(Button button)
    {
        StyleActionButton(button, Color.FromRgb(188, 64, 64), Colors.White, 122);
    }

    private static void StyleActionButton(Button button, Color background, Color foreground, double minWidth)
    {
        button.Background = new SolidColorBrush(background);
        button.Foreground = new SolidColorBrush(foreground);
        button.MinWidth = minWidth;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    private static Button NewButtonShell(string text)
    {
        return new Button
        {
            Content = text,
            Padding = new Thickness(12, 7),
            Margin = new Thickness(0, 0, 8, 8),
            MinHeight = 34,
            Background = new SolidColorBrush(Color.FromRgb(237, 241, 245)),
            Foreground = new SolidColorBrush(Color.FromRgb(31, 38, 45))
        };
    }

    private static Border PanelShell()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(246, 248, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(213, 219, 226)),
            Padding = new Thickness(14)
        };
    }

    private static TextBlock SectionLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(88, 97, 108))
        };
    }

    private static Border NewDivider()
    {
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(216, 222, 228)),
            Margin = new Thickness(0, 2)
        };
    }

    private static Expander BuildCollapsedPanel(string header, Control content)
    {
        return new Expander
        {
            Header = header,
            IsExpanded = false,
            Content = content,
            Margin = new Thickness(0, 2, 0, 0)
        };
    }

    private sealed record TerrainLookClipboard(
        int TextureId,
        string Surface,
        ColorRgba SurfaceColor,
        string RuntimeKey,
        string SourceLabel,
        IReadOnlyList<CustomTerrainTextureImport> CustomTextures);

    private sealed record LevelLoadData(
        GeometryCandidate? Geometry,
        string TerrainCacheHealthMessage,
        int LoadedTerrainEdits,
        HashSet<string> TerrainCollisionTriangleKeys,
        string TerrainCollisionReadinessMessage,
        int TerrainCollisionMatchedFaces,
        List<Moby> Mobys,
        int LoadedMobyEdits,
        MobyMetadataResult MobyMetadata,
        IReadOnlyList<CustomTerrainTextureImport> CustomTerrainTextures);

    private sealed record TerrainGeometryLoadData(
        GeometryCandidate? Geometry,
        int LoadedTerrainEdits,
        string TerrainCacheHealthMessage);

    private sealed record EditedLevelExportTarget(
        LevelDefinition Level,
        bool HasObjectEdits,
        bool HasTerrainEdits,
        bool HasCustomTerrainTextures);

    private sealed record CombinedTestBinResult(
        string OutputImagePath,
        string OutputCuePath,
        int ObjectPatches,
        int TerrainPatches,
        int SkippedEdits,
        IReadOnlyList<string> PatchedLevelNames,
        bool WroteImage);

    private sealed record RestoredTerrainLoadData(
        TerrainGeometryLoadData Geometry,
        TerrainCollisionLoadData Collision,
        IReadOnlyList<CustomTerrainTextureImport> CustomTerrainTextures);

    private sealed record MobyLoadData(
        List<Moby> Mobys,
        int LoadedMobyEdits,
        MobyMetadataResult Metadata);

    private sealed record TerrainCollisionLoadData(
        HashSet<string> TriangleKeys,
        string ReadinessMessage,
        int MatchedFaces);

    private readonly record struct TerrainBrushResult(
        int AffectedFaces,
        int AffectedVertices,
        int TouchedFaces,
        int EditableVerticesInRadius,
        int SkippedVisualOnlyFaces,
        int SkippedVisualOnlyVertices,
        int SeamSyncedVertexGroups,
        int SeamAdjustedVertices,
        int FollowedVisualFaces,
        int FollowedVisualVertices);

    private readonly record struct TerrainEditSafetySummary(
        int LiveEdits,
        int HeightEdits,
        int PositionEdits,
        int TextureEdits,
        int StructuralEdits,
        int PlayableHeightEdits,
        int PartialHeightEdits,
        int VisualOnlyHeightEdits,
        int UnknownHeightEdits)
    {
        public bool HasPatchRisk => StructuralEdits > 0 || PartialHeightEdits > 0 || VisualOnlyHeightEdits > 0 || UnknownHeightEdits > 0;
    }

    private enum TerrainPatchRiskDecision
    {
        Cancel,
        Review,
        Create
    }

    private enum UnsavedTerrainDecision
    {
        Cancel,
        Discard,
        Save
    }

    private sealed record TerrainGeometryReadiness(
        string LevelKey,
        string LevelName,
        string Status,
        string RuntimeKey,
        string Edit,
        int SourceSectorCount,
        int MatchedSectorCount,
        int PatchCount,
        int VisualPatchCount,
        bool HadCollisionMatch,
        bool HasCollisionPatch,
        string Notes);

    private sealed record TerrainTextureReadiness(
        string LevelKey,
        string LevelName,
        string Status,
        string RuntimeKey,
        int SourceTextureId,
        int TargetTextureId,
        int SourceSectorCount,
        int MatchedSectorCount,
        int TextureSwapPatchCount,
        int CustomTexturePatchCount,
        int CustomTextureImportCount,
        int PreviewedFaceCount,
        string DescriptorTiers,
        string Notes);

    private sealed record TerrainStructureReadiness(
        string LevelKey,
        string LevelName,
        string Status,
        string RemoveRuntimeKey,
        int RemovePatchCount,
        bool RemoveHadCollision,
        bool RemoveHasCollisionPatch,
        string AddRuntimeKey,
        int AddSlackBytes,
        int AddRequiredSlackBytes,
        int AddPatchCount,
        bool AddHasIndependentVertices,
        bool AddHasCollisionPatch,
        bool AddHasFallbackAppend,
        string Notes);

    private sealed class TerrainCapabilityReviewItem
    {
        public int SortRank { get; init; }
        public string Feature { get; init; } = "";
        public string Status { get; init; } = "";
        public string Evidence { get; init; } = "";
        public string Details { get; init; } = "";

        public string ListText
        {
            get
            {
                string status = string.IsNullOrWhiteSpace(Status) ? "unknown" : Status;
                return $"{Feature,-24} {status,-15} {Evidence}";
            }
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private readonly record struct TerrainAddCopyFaceReadiness(
        bool IsHighDetail,
        bool HasSourceSearch,
        bool HasSourceSector,
        int AvailableSlackBytes,
        int RequiredSlackBytes,
        bool HasCollisionMatch,
        int MatchedCollisionTriangles,
        int TotalCollisionTriangles,
        bool CanAddIndependentVisibleTerrain);

    private readonly record struct TerrainRemoveFaceReadiness(
        bool HasSourceSearch,
        bool HasSourceSector,
        bool HasFaceOffset,
        bool HasCollisionData,
        int MatchedCollisionTriangles,
        int TotalCollisionTriangles,
        bool CanRemoveVisibleFace,
        bool CanRemoveCollision);

    private sealed class TerrainEditReviewItem
    {
        public int TerrainIndex { get; init; }
        public string RuntimeKey { get; init; } = "";
        public TerrainPatchSafetyKind Safety { get; init; }
        public string SafetyLabel { get; init; } = "";
        public string Surface { get; init; } = "";
        public int TextureId { get; init; }
        public int OriginalTextureId { get; init; }
        public bool HasHeightEdit { get; init; }
        public bool HasPositionEdit { get; init; }
        public bool HasTextureEdit { get; init; }
        public bool HasStructureEdit { get; init; }
        public bool HasCustomTextureArt { get; init; }
        public bool IsTextureArtOnly { get; init; }
        public int CustomTextureImportCount { get; init; }
        public int AffectedFaceCount { get; init; }
        public string EditSummary { get; init; } = "";
        public string ExportImpact { get; init; } = "";
        public string HeightEdit { get; init; } = "";
        public string CollisionReadiness { get; init; } = "";
        public string TextureChange { get; init; } = "";
        public string TextureExport { get; init; } = "";
        public string ProofStatus { get; init; } = "";

        public string ListText
        {
            get
            {
                if (IsTextureArtOnly)
                {
                    string affected = AffectedFaceCount > 0 ? $"{AffectedFaceCount} face(s)" : "no matching face";
                    return $"{SafetyLabel,-18} texture {TextureId,3}  {affected,-14} {TextureExport}";
                }

                string editKinds = string.Join("+", new[]
                {
                    HasStructureEdit ? "structure" : "",
                    HasHeightEdit ? "height" : "",
                    HasPositionEdit ? "position" : "",
                    HasTextureEdit ? "texture" : "",
                    HasCustomTextureArt ? "art" : ""
                }.Where(text => !string.IsNullOrWhiteSpace(text)));
                return $"{SafetyLabel,-18} face {TerrainIndex,5}  tex {TextureId,3}  {editKinds,-14} {RuntimeKey}";
            }
        }
    }

    private sealed class TerrainPatchPlanReviewItem
    {
        public int SortRank { get; init; }
        public string Category { get; init; } = "";
        public string RuntimeKey { get; init; } = "";
        public string Summary { get; init; } = "";
        public string Details { get; init; } = "";

        public string ListText
        {
            get
            {
                string key = string.IsNullOrWhiteSpace(RuntimeKey) ? "" : $" {RuntimeKey,-10}";
                return $"{Category,-17}{key} {Summary}";
            }
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private readonly record struct TerrainBrushStrokeFace(
        TerrainPolygon Polygon,
        IReadOnlyList<float> BeforeDeltas,
        IReadOnlyList<float> AfterDeltas);

    private readonly record struct TerrainCollisionCoverage(
        int MatchedTriangles,
        int TotalTriangles,
        IReadOnlySet<int> VertexIndexes)
    {
        public static TerrainCollisionCoverage Empty { get; } = new(0, 0, new HashSet<int>());
    }

    private sealed record TerrainBrushModeOption(string Label, TerrainBrushAction Action)
    {
        public static IReadOnlyList<TerrainBrushModeOption> All { get; } =
        [
            new("Off - select objects", TerrainBrushAction.Off),
            new("Raise terrain", TerrainBrushAction.Raise),
            new("Lower terrain", TerrainBrushAction.Lower),
            new("Blend/smooth terrain", TerrainBrushAction.Blend),
            new("Level terrain", TerrainBrushAction.Flatten),
            new("Restore original terrain", TerrainBrushAction.Restore)
        ];

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record PendingMobyAdd(
        AddMobyTemplate Template,
        string Label,
        int Type,
        int State,
        int SourceByte36,
        int SourceByte4F,
        int Flag4B,
        GemValue SelectedGem,
        MobyPlacementOption Placement,
        IReadOnlyList<AddMobyTemplate> AvailableTemplates)
    {
        public Vector3f FallbackPosition { get; init; }
    }

    private sealed record MobyClipboard(
        string Label,
        int Type,
        int State,
        int SourceByte36,
        int SourceByte37,
        int SourceByte4F,
        int Flag4A,
        int Flag4B,
        ColorRgba Color,
        string PatchStatus,
        string PatchLead,
        string CrossLevelTemplateId,
        string CrossLevelFamily,
        string CrossLevelSourceLevelKey,
        string CrossLevelSourceLevelName,
        int CrossLevelSourceTrueIndex,
        string CrossLevelRequiredExporterFeature,
        string CandidateKind,
        string Confidence,
        string Evidence,
        string BehaviorNote,
        string ZoneLabel,
        bool CopiedGemLike,
        bool CopiedVisibleGem,
        GemValue CopiedGem)
    {
        public static MobyClipboard From(Moby moby)
        {
            return new MobyClipboard(
                moby.DisplayLabel,
                moby.Type,
                moby.State,
                moby.SourceByte36,
                moby.SourceByte37,
                moby.SourceByte4F,
                moby.Flag4A,
                moby.Flag4B,
                moby.Color,
                moby.PatchStatus,
                moby.PatchLead,
                moby.CrossLevelTemplateId,
                moby.CrossLevelFamily,
                moby.CrossLevelSourceLevelKey,
                moby.CrossLevelSourceLevelName,
                moby.CrossLevelSourceTrueIndex,
                moby.CrossLevelRequiredExporterFeature,
                moby.CandidateKind,
                moby.Confidence,
                moby.Evidence,
                moby.BehaviorNote,
                moby.ZoneLabel,
                moby.IsGemLike,
                moby.IsVisibleGem,
                moby.Gem);
        }
    }

    private sealed record MobyPlacementOption(string Label, Vector3f Position, string Description, bool SnapOnAdd = true, bool PlaceAfterDialog = false)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record TerrainCopyPlacementOption(string Label, float XOffset, float YOffset, float ZOffset, string Description)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record AddMobyTemplate(
        string Name,
        int Type,
        int State,
        int SourceByte36,
        int SourceByte37,
        int SourceByte4F,
        int Flag4A,
        int Flag4B,
        bool UsesGem,
        string DefaultLabel,
        bool CopiesSelected = false,
        bool FromLevelTemplate = false,
        bool FromCrossLevelTemplate = false,
        GemValue? DefaultGem = null,
        ColorRgba? DefaultColor = null,
        string TemplateId = "",
        string Family = "",
        string SourceLevelKey = "",
        string SourceLevelName = "",
        int SourceTrueIndex = -1,
        string AddSupportStatus = "",
        string RequiredExporterFeature = "",
        string DependencyRisk = "",
        string TemplateNote = "",
        string CandidateKind = "",
        string CurrentLevelSupportLabel = "",
        string CurrentLevelRecipeId = "",
        bool CurrentLevelReady = false,
        bool CurrentLevelPlaceable = false,
        string CompanionTemplateId = "")
    {
        public static IReadOnlyList<AddMobyTemplate> Known { get; } =
        [
            new("Gem / treasure", 0x18, 0x00, 0x53, 0x00, 0x01, 0x40, 0xFF, true, "New Red gem"),
            new("Simple type 0x18", 0x18, 0x00, 0x53, 0x00, 0x01, 0x00, 0xFF, false, "New simple object"),
            new("Custom simple source record", 0x20, 0x00, 0x53, 0x00, 0x01, 0x00, 0x53, false, "New custom object")
        ];

        public override string ToString()
        {
            return Name;
        }
    }

    private sealed record CandidateResultItem(
        string Label,
        string LevelKey,
        string LevelName,
        string ResultPath,
        string PlanPath,
        string CuePath,
        string Status,
        bool? BootedLevel,
        bool? ObjectAppeared,
        bool? MobyRecordsPresent,
        bool? ActorRootsPresent,
        bool? BehaviorCorrect,
        bool? NoNearbyRegression,
        IReadOnlyList<CandidateBehaviorCheckItem> BehaviorChecks,
        string Tester,
        string Notes,
        DateTime LastWrittenUtc)
    {
        public override string ToString()
        {
            return $"{LevelName}: {Label} ({Status})";
        }
    }

    private sealed record CandidateBehaviorCheckItem(
        string Id,
        string Label,
        bool? Passed);

    private sealed record ExeStringPreset(string Name, string OriginalText, string ReplacementText)
    {
        public static IReadOnlyList<ExeStringPreset> Known { get; } =
        [
            new("Entering message", "ENTERING %s...", "LOADING %s..."),
            new("Toasty name", "TOASTY", "ROASTY"),
            new("Jacques name", "JACQUES", "JESTER")
        ];

        public override string ToString()
        {
            return Name;
        }
    }

    private sealed record LinkedMobyItem(Moby Moby, string Relationship);

    private sealed record ToolbarAction(string Label, Func<Task> Execute)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record TerrainSurfaceChoice(string Label, string Surface)
    {
        public static IReadOnlyList<TerrainSurfaceChoice> Known { get; } =
        [
            new("Grass", "grass"),
            new("Water", "water"),
            new("Sand", "sand"),
            new("Stone", "stone"),
            new("Ground / dirt", "ground"),
            new("Cliff / wall", "cliff"),
            new("Brick / castle", "brick"),
            new("Ice", "ice"),
            new("Lava", "lava"),
            new("Goo", "ooze"),
            new("Unknown", "unknown")
        ];

        public static TerrainSurfaceChoice Find(string surface)
        {
            string normalized = TerrainMaterialClassifier.NormalizeSurfaceName(surface);
            return Known.FirstOrDefault(choice => string.Equals(choice.Surface, normalized, StringComparison.OrdinalIgnoreCase))
                ?? Known[^1];
        }

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed class TerrainProofTargetReport
    {
        public string GeneratedAt { get; init; } = "";
        public string Summary { get; init; } = "";
        public IReadOnlyList<TerrainProofTargetItem> Targets { get; init; } = [];
    }

    private sealed record TerrainProofTargetSelection(TerrainProofTargetItem Target, bool UseCompanion);

    private sealed class TerrainProofSummaryItem
    {
        public string LevelKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Surface { get; init; } = "";
        public int FaceCount { get; init; }
        public int TextureCount { get; init; }
        public string BehaviorSummary { get; init; } = "";
        public string ProofSummary { get; init; } = "";
        public int ProofStatusRank { get; init; }
        public string NextStep { get; init; } = "";

        public string ListText =>
            $"{DisplayName,-16} {Surface,-6} faces {FaceCount,5}  tex {TextureCount,3}  proof {ProofSummary,-18}  {BehaviorSummary}";

        public static TerrainProofSummaryItem FromReport(TerrainBehaviorProofSurfaceSummary summary)
        {
            return new TerrainProofSummaryItem
            {
                LevelKey = summary.LevelKey,
                DisplayName = summary.DisplayName,
                Surface = summary.Surface,
                FaceCount = summary.FaceCount,
                TextureCount = summary.TextureCount,
                BehaviorSummary = summary.BehaviorSummary,
                ProofSummary = summary.ProofSummary,
                ProofStatusRank = summary.ProofStatusRank,
                NextStep = summary.NextStep
            };
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private sealed class TerrainSurfaceGroupItem
    {
        public string Surface { get; init; } = "";
        public int TextureId { get; init; }
        public int FaceCount { get; init; }
        public int EditedFaces { get; init; }
        public string BehaviorSummary { get; init; } = "";
        public string ProofSummary { get; init; } = "";
        public int ProofStatusRank { get; init; }
        public string ProofNextStep { get; init; } = "";
        public bool HasCustomTexture { get; init; }
        public int TerrainIndex { get; init; }
        public string RuntimeKey { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }

        public string ListText
        {
            get
            {
                string custom = HasCustomTexture ? " custom" : "";
                string edited = EditedFaces == 0 ? "" : $" edited {EditedFaces}";
                return $"{Surface,-7} tex {TextureId,3}  faces {FaceCount,5}  proof {ProofSummary,-18}{custom}{edited}  {BehaviorSummary}";
            }
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private sealed class TerrainTextureSwapChoice
    {
        public string Surface { get; init; } = "";
        public int TextureId { get; init; }
        public int FaceCount { get; init; }
        public string BehaviorSummary { get; init; } = "";
        public string ProofSummary { get; init; } = "";
        public int ProofStatusRank { get; init; }
        public bool HasCustomTexture { get; init; }
        public string RuntimeKey { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public ColorRgba Color { get; init; } = ColorRgba.FromRgb(120, 130, 120);

        public string ListText
        {
            get
            {
                string custom = HasCustomTexture ? " custom" : "";
                return $"{Surface,-7} tex {TextureId,3}  faces {FaceCount,5}  proof {ProofSummary,-18}{custom}  {BehaviorSummary}";
            }
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private sealed class TerrainCrossLevelLookChoice
    {
        public string LevelKey { get; init; } = "";
        public string LevelName { get; init; } = "";
        public string Surface { get; init; } = "";
        public int TextureId { get; init; }
        public int FaceCount { get; init; }
        public string BehaviorSummary { get; init; } = "";
        public string ProofSummary { get; init; } = "";
        public int ProofStatusRank { get; init; }
        public bool HasCustomTexture { get; init; }
        public string RuntimeKey { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public ColorRgba Color { get; init; } = ColorRgba.FromRgb(120, 130, 120);
        public int PreferredSurfaceRank { get; init; } = 1;
        public string CustomSourceImagePath { get; init; } = "";
        public string CustomSourceImageName { get; init; } = "";
        public string CustomPaletteName { get; init; } = "";
        public string CustomPaletteLowHex { get; init; } = "";
        public string CustomPaletteHighHex { get; init; } = "";
        public IReadOnlyList<string> CustomPaletteHexColors { get; init; } = Array.Empty<string>();

        public string ListText
        {
            get
            {
                string custom = HasCustomTexture ? " custom" : "";
                return $"{LevelName,-15} {Surface,-7} tex {TextureId,3}  faces {FaceCount,5}  proof {ProofSummary,-18}{custom}  {BehaviorSummary}";
            }
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private sealed record TerrainTextureSurfaceFilter(string Surface, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record TerrainMovePresetOption(string Label, float XOffset, float YOffset, float ZOffset, string Description)
    {
        public override string ToString()
        {
            return Label;
        }
    }

    private enum TerrainPaintModeKind
    {
        CustomColors,
        InGameLook,
        CrossLevelLook,
        ImportedPalette,
        PastedPalette
    }

    private sealed record TerrainPaintModeOption(TerrainPaintModeKind Kind, string Label, string Description)
    {
        public static IReadOnlyList<TerrainPaintModeOption> All { get; } =
        [
            new(
                TerrainPaintModeKind.CustomColors,
                "Custom colors",
                "Pick any low/high colors. The selected face gets its own local texture slot before the palette is staged."),
            new(
                TerrainPaintModeKind.InGameLook,
                "Borrow in-game look",
                "Use an existing terrain texture/palette from this level on the selected face."),
            new(
                TerrainPaintModeKind.CrossLevelLook,
                "Borrow from another level",
                "Use a cached terrain look from another level. The editor creates a local texture for this selected face instead of writing another level's texture ID directly."),
            new(
                TerrainPaintModeKind.ImportedPalette,
                "Import palette file",
                "Import a palette text file or PNG palette image and apply it only to this selected face."),
            new(
                TerrainPaintModeKind.PastedPalette,
                "Paste custom palette",
                "Paste #RRGGBB colors or RGB rows from another palette. At least two colors are required.")
        ];

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed record TerrainPaintDialogResult(
        TerrainPaintModeKind Mode,
        ColorRgba LowColor,
        ColorRgba HighColor,
        TerrainTextureSwapChoice? InGameLook,
        TerrainCrossLevelLookChoice? CrossLevelLook,
        TerrainPaletteImport? Palette)
    {
        public static TerrainPaintDialogResult ForCustomColors(ColorRgba low, ColorRgba high)
        {
            return new TerrainPaintDialogResult(TerrainPaintModeKind.CustomColors, low, high, null, null, null);
        }

        public static TerrainPaintDialogResult ForInGameLook(TerrainTextureSwapChoice choice)
        {
            return new TerrainPaintDialogResult(TerrainPaintModeKind.InGameLook, default, default, choice, null, null);
        }

        public static TerrainPaintDialogResult ForCrossLevelLook(TerrainCrossLevelLookChoice choice)
        {
            return new TerrainPaintDialogResult(TerrainPaintModeKind.CrossLevelLook, default, default, null, choice, null);
        }

        public static TerrainPaintDialogResult ForImportedPalette()
        {
            return new TerrainPaintDialogResult(TerrainPaintModeKind.ImportedPalette, default, default, null, null, null);
        }

        public static TerrainPaintDialogResult ForPalette(TerrainPaletteImport palette)
        {
            return new TerrainPaintDialogResult(TerrainPaintModeKind.PastedPalette, default, default, null, null, palette);
        }
    }

    private sealed record TerrainSurfaceGroupAction(TerrainSurfaceGroupItem Item, TerrainSurfaceGroupActionKind Kind);

    private enum TerrainSurfaceGroupActionKind
    {
        SelectFace,
        SelectProofTarget
    }

    private sealed class TerrainProofTargetItem
    {
        public string LevelKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string CaptureKind { get; init; } = "";
        public string Surface { get; init; } = "";
        public int TextureId { get; init; }
        public int TextureFaceCount { get; init; }
        public string RuntimeKey { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Z { get; init; }
        public string NearestControlId { get; init; } = "";
        public string NearestControlLabel { get; init; } = "";
        public double? NearestControlDistance { get; init; }
        public double? NearestControlZDelta { get; init; }
        public string NearestSolidOrHazardRuntimeKey { get; init; } = "";
        public double? NearestSolidOrHazardDistance { get; init; }
        public string ExistingProof { get; init; } = "";
        public int ProofStatusRank { get; init; }
        public string NextCapture { get; init; } = "";

        public string ListText =>
            $"{CaptureKind,-13} {DisplayName,-16} {Surface,-6} tex {TextureId,3}  {RuntimeKey,-10}  xyz {X,7:0}, {Y,7:0}, {Z,5:0}  {ExistingProof}";

        public static TerrainProofTargetItem FromReport(TerrainBehaviorProofTarget target)
        {
            return new TerrainProofTargetItem
            {
                LevelKey = target.LevelKey,
                DisplayName = target.DisplayName,
                CaptureKind = target.CaptureKind,
                Surface = target.Surface,
                TextureId = target.TextureId,
                TextureFaceCount = target.TextureFaceCount,
                RuntimeKey = target.RuntimeKey,
                X = target.X,
                Y = target.Y,
                Z = target.Z,
                NearestControlId = target.NearestControlId,
                NearestControlLabel = target.NearestControlLabel,
                NearestControlDistance = target.NearestControlDistance,
                NearestControlZDelta = target.NearestControlZDelta,
                NearestSolidOrHazardRuntimeKey = target.NearestSolidOrHazardRuntimeKey,
                NearestSolidOrHazardDistance = target.NearestSolidOrHazardDistance,
                ExistingProof = target.ExistingProof,
                ProofStatusRank = target.ProofStatusRank,
                NextCapture = target.NextCapture
            };
        }

        public override string ToString()
        {
            return ListText;
        }
    }

    private sealed record IdentityBatchFile(string Name, string Path, string Details)
    {
        public override string ToString()
        {
            return Name;
        }
    }

    private sealed record IdentityBatchCandidate(string Number, string LevelKey, string ManifestPath, string CurrentLabel, string Impact);

    private sealed class IdentityReviewResultRow
    {
        private readonly Dictionary<string, string> _values;

        public IdentityReviewResultRow(string[] headers, Dictionary<string, string> values)
        {
            Headers = headers;
            _values = values;
        }

        public string[] Headers { get; }
        public string Priority => Get("priority");
        public string BatchKind => Get("batchKind");
        public string BatchNumber => Get("batchNumber");
        public string Fingerprint => Get("fingerprint");
        public string LevelKey => Get("levelKey");
        public string LevelName => Get("levelName");
        public string TrueIndexes => Get("trueIndexes");
        public string CurrentLabel => Get("currentLabel");
        public string ProofStep => Get("proofStep");
        public string ResultStatus => Get("resultStatus");
        public string ObservedLabel => Get("observedLabel");
        public string CandidateKind => Get("candidateKind");
        public string EvidenceNotes => Get("evidenceNotes");
        public string PromoteScope => Get("promoteScope");

        public string Get(string name)
        {
            return _values.TryGetValue(name, out string? value) ? value : "";
        }

        public void Set(string name, string value)
        {
            _values[name] = value;
        }
    }

    private sealed class ClusterReviewResultRow
    {
        private readonly Dictionary<string, string> _values;

        public ClusterReviewResultRow(string[] headers, Dictionary<string, string> values)
        {
            Headers = headers;
            _values = values;
        }

        public string[] Headers { get; }
        public string Priority => Get("priority");
        public string ClusterKind => Get("clusterKind");
        public string ClusterKey => Get("clusterKey");
        public string LevelKey => Get("levelKey");
        public string LevelName => Get("levelName");
        public string UnknownCount => Get("unknownCount");
        public string UnknownRecords => Get("unknownRecords");
        public string KnownAnchors => Get("knownAnchors");
        public string ProofMove => Get("proofMove");
        public string BatchPaths => Get("batchPaths");
        public string ResultStatus => Get("resultStatus");
        public string ClusterVerdict => Get("clusterVerdict");
        public string ObservedLabel => Get("observedLabel");
        public string EvidenceNotes => Get("evidenceNotes");
        public string PromoteScope => Get("promoteScope");

        public string Get(string name)
        {
            return _values.TryGetValue(name, out string? value) ? value : "";
        }

        public void Set(string name, string value)
        {
            _values[name] = value;
        }
    }

    private sealed record ChestContentEditorRow(Moby Moby, TextBox LabelBox, ComboBox GemBox, CheckBox RemoveBox);

    private sealed record KnownMobyEntity(
        string LevelName,
        int TrueIndex,
        string Label,
        int Type,
        int State,
        int SourceByte36,
        int Flag4A,
        int Flag4B);

    private sealed record TerrainColorOption(string Name, ColorRgba Color);

    private sealed record MobyIdentityWorkbenchGroup(
        string LevelKey,
        string LevelName,
        int TotalMobys,
        int ExactOrObserved,
        int Questionable,
        string Fingerprint,
        int Count,
        string CurrentRead,
        string EvidenceState,
        string Lane,
        string ProofStep,
        string PointerGroups,
        string BatchPath,
        IReadOnlyList<MobyIdentityWorkbenchSample> Samples);

    private sealed record MobyIdentityWorkbenchSample(
        int TrueIndex,
        float X,
        float Y,
        float Z,
        string SpecialDataPointer,
        string NearestExactObjects);

    private sealed record MobyIdentityFamilyObservation(
        IReadOnlyList<string> CurrentReads,
        IReadOnlyList<string> RosterCandidates,
        string ProofStep,
        string TestBatch);

    private sealed record MobyIdentityModelFamily(
        string FamilyKey,
        string LevelKey,
        string LevelName,
        int Type,
        int SourceByte36,
        uint SpecialDataPointer,
        int Questionable,
        int Total,
        IReadOnlyList<string> CurrentReads,
        IReadOnlyList<string> RewardAndBehaviorVariants,
        IReadOnlyList<string> ExactLabelsInFamily,
        IReadOnlyList<MobyIdentityModelFamilySample> Samples,
        IReadOnlyList<string> BatchFiles);

    private sealed record MobyIdentityModelFamilySample(
        int TrueIndex,
        string Fingerprint,
        string Label,
        string NearestExactObjects);

    private sealed record MobyIdentityWorkbenchAction(
        MobyIdentityWorkbenchGroup Group,
        MobyIdentityWorkbenchActionKind Kind);

    private enum MobyIdentityWorkbenchActionKind
    {
        SelectSample,
        LoadBatch
    }

    private sealed class ColorPalettePicker
    {
        private readonly ComboBox _box;
        private readonly List<TerrainColorOption> _options;
        private readonly Border _swatch;
        private readonly TextBox _hex;
        private readonly Slider _red;
        private readonly Slider _green;
        private readonly Slider _blue;
        private readonly TextBlock _rgbText;
        private bool _syncing;

        public ColorPalettePicker(List<TerrainColorOption> options)
        {
            _options = options;
            _box = new ComboBox
            {
                ItemsSource = options,
                SelectedIndex = 0,
                MinWidth = 220,
                MinHeight = 34,
                ItemTemplate = new FuncDataTemplate<TerrainColorOption>((option, _) => BuildColorPaletteItem(option))
            };
            _swatch = new Border
            {
                Width = 42,
                Height = 30,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(92, 102, 112)),
                BorderThickness = new Thickness(1)
            };
            _hex = new TextBox
            {
                MinWidth = 92,
                PlaceholderText = "#RRGGBB"
            };
            _red = NewChannelSlider();
            _green = NewChannelSlider();
            _blue = NewChannelSlider();
            _rgbText = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(72, 81, 92)),
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel panel = new() { Spacing = 8 };
            StackPanel top = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            top.Children.Add(_box);
            top.Children.Add(_swatch);
            top.Children.Add(_hex);
            top.Children.Add(_rgbText);
            panel.Children.Add(top);
            panel.Children.Add(BuildChannelRow("R", _red));
            panel.Children.Add(BuildChannelRow("G", _green));
            panel.Children.Add(BuildChannelRow("B", _blue));
            Control = panel;

            _box.SelectionChanged += (_, _) =>
            {
                if (_syncing || _box.SelectedItem is not TerrainColorOption option)
                    return;

                SetSliderColor(option.Color, updatePreset: false);
            };
            _hex.LostFocus += (_, _) => TryApplyTypedHex(out _);
            _red.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) SyncFromSliders(); };
            _green.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) SyncFromSliders(); };
            _blue.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) SyncFromSliders(); };
            SetSelectedColor(options[0].Color, options[0].Name);
        }

        public Control Control { get; }

        public ColorRgba SelectedColor => ColorRgba.FromRgb(
            (byte)Math.Clamp((int)Math.Round(_red.Value), 0, 255),
            (byte)Math.Clamp((int)Math.Round(_green.Value), 0, 255),
            (byte)Math.Clamp((int)Math.Round(_blue.Value), 0, 255));

        public void SetSelectedColor(ColorRgba color, string name)
        {
            TerrainColorOption? existing = _options.FirstOrDefault(option => option.Color == color);
            if (existing != null)
            {
                _syncing = true;
                _box.SelectedItem = existing;
                _syncing = false;
                SetSliderColor(existing.Color, updatePreset: false);
                return;
            }

            _options[0] = new TerrainColorOption(name, color);
            _syncing = true;
            _box.ItemsSource = null;
            _box.ItemsSource = _options;
            _box.SelectedIndex = 0;
            _syncing = false;
            SetSliderColor(color, updatePreset: false);
        }

        public bool TryApplyTypedHex(out string error)
        {
            error = "";
            string text = _hex.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!ColorRgba.TryParseHex(text, out ColorRgba color))
            {
                error = "Use a six-digit color like #305830 or 305830.";
                return false;
            }

            SetSelectedColor(color, "Custom color");
            return true;
        }

        private static Slider NewChannelSlider()
        {
            return new Slider
            {
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                MinWidth = 240
            };
        }

        private static Control BuildChannelRow(string label, Slider slider)
        {
            Grid row = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(18)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(42, 49, 57))
            });
            AddGridControl(row, slider, 1, 0);
            return row;
        }

        private void SetSliderColor(ColorRgba color, bool updatePreset)
        {
            _syncing = true;
            _red.Value = color.R;
            _green.Value = color.G;
            _blue.Value = color.B;
            _syncing = false;
            if (updatePreset)
                SelectMatchingPresetOrCustom(color);
            RefreshSwatch();
        }

        private void SyncFromSliders()
        {
            if (_syncing)
                return;

            ColorRgba color = SelectedColor;
            SelectMatchingPresetOrCustom(color);
            RefreshSwatch();
        }

        private void SelectMatchingPresetOrCustom(ColorRgba color)
        {
            TerrainColorOption? existing = _options.FirstOrDefault(option => option.Color == color);
            _syncing = true;
            if (existing != null)
            {
                _box.SelectedItem = existing;
            }
            else
            {
                _options[0] = new TerrainColorOption("Custom color", color);
                _box.ItemsSource = null;
                _box.ItemsSource = _options;
                _box.SelectedIndex = 0;
            }
            _syncing = false;
        }

        private void RefreshSwatch()
        {
            ColorRgba color = SelectedColor;
            _swatch.Background = new SolidColorBrush(ToAvaloniaColor(color));
            _hex.Text = NormalizeHex(color);
            _rgbText.Text = $"RGB {color.R}, {color.G}, {color.B}";
        }
    }

    private sealed record TerrainFaceLocalTextureResult(int TextureId, int SourceTextureId, int SourceUseCount, string SourceSummary);
}
