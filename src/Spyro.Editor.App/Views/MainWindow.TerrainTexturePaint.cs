using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly TextBlock _terrainTexturePaintModeText = new();
    private readonly Image _terrainTexturePaintActivePreview = new()
    {
        Width = 96,
        Height = 96,
        Stretch = Stretch.Uniform
    };
    private readonly TextBlock _terrainTexturePaintActivePreviewFallback = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = Brushes.White,
        FontWeight = FontWeight.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center
    };
    private Border? _terrainTexturePaintActivePreviewFrame;
    private Button? _terrainTexturePaintStopButton;
    private Button? _terrainTexturePaintReturnButton;
    private TerrainTexturePaintBrush? _activeTerrainTexturePaintBrush;
    private bool _terrainTexturePaintBusy;
    private bool _terrainTextureRelocationBusy;
    private bool _terrainTexturePaintStopRequested;
    private bool _terrainTexturePaintStopRequestedAnnounce;
    private string _lastTerrainTexturePaintFace = "";
    private readonly Dictionary<string, IReadOnlyDictionary<int, string>> _terrainTextureCatalogPreviewPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Bitmap?> _terrainTextureCatalogPreviewBitmaps =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<TerrainTexturePaintGalleryItem>> _terrainTexturePaintLoadedPaletteItems =
        new(StringComparer.OrdinalIgnoreCase);
    private string _terrainTexturePaintPaletteTargetLevelKey = "";
    private string _terrainTexturePaintPaletteSourceLevelKey = "";
    internal Action<string>? TerrainTextureDonorLoadObserverForTesting { get; set; }
    internal Action<string>? TerrainTexturePaintPersistenceFaultForTesting { get; set; }
    internal Func<string, int, int, Task<bool>>? TerrainTextureSharedReplacementConfirmationForTesting { get; set; }

    private Control BuildTerrainTexturePaintModePanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 248, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(153, 196, 226)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };

        StackPanel content = new() { Spacing = 7 };
        content.Children.Add(new TextBlock
        {
            Text = "Texture Paint Mode",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 71, 106))
        });
        _terrainTexturePaintModeText.TextWrapping = TextWrapping.Wrap;
        _terrainTexturePaintModeText.FontSize = 12;
        _terrainTexturePaintModeText.LineHeight = 17;
        _terrainTexturePaintModeText.Foreground = new SolidColorBrush(Color.FromRgb(47, 72, 94));
        _terrainTexturePaintActivePreview.IsVisible = false;
        RenderOptions.SetBitmapInterpolationMode(_terrainTexturePaintActivePreview, BitmapInterpolationMode.None);
        _terrainTexturePaintActivePreviewFallback.IsVisible = false;
        Grid previewContent = new();
        previewContent.Children.Add(_terrainTexturePaintActivePreviewFallback);
        previewContent.Children.Add(_terrainTexturePaintActivePreview);
        _terrainTexturePaintActivePreviewFrame = new Border
        {
            Width = 104,
            Height = 104,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(174, 188, 201)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(24, 31, 38)),
            Child = previewContent,
            IsVisible = false
        };
        content.Children.Add(_terrainTexturePaintActivePreviewFrame);
        content.Children.Add(_terrainTexturePaintModeText);

        StackPanel paintActions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        _terrainTexturePaintStopButton = NewButton(
            "Stop Painting",
            () => StopTerrainTexturePaintMode(announce: true));
        _terrainTexturePaintReturnButton = NewAsyncButton(
            "Return to Texture Palette",
            ReturnToTerrainTexturePaletteAsync);
        _terrainTexturePaintReturnButton.Name = "TerrainTextureReturnToPaletteButton";
        paintActions.Children.Add(_terrainTexturePaintStopButton);
        paintActions.Children.Add(_terrainTexturePaintReturnButton);
        content.Children.Add(paintActions);
        border.Child = content;
        RefreshTerrainTexturePaintModeUi();
        return border;
    }

    private Control BuildTerrainTextureCatalogPreview(string levelKey, int textureId, ColorRgba fallbackColor)
    {
        Bitmap? bitmap = LoadTerrainTextureCatalogPreview(levelKey, textureId);
        Grid content = new();
        if (bitmap != null)
        {
            Image image = new()
            {
                Source = bitmap,
                Width = 96,
                Height = 96,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
            content.Children.Add(image);
        }
        else
        {
            content.Background = new SolidColorBrush(ToAvaloniaColor(fallbackColor));
            content.Children.Add(new TextBlock
            {
                Text = $"Texture {textureId}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Width = 104,
            Height = 104,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(5),
            BorderBrush = new SolidColorBrush(Color.FromRgb(174, 188, 201)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(24, 31, 38)),
            Child = content
        };
    }

    private Control BuildTerrainTexturePaintChooserDialogContent(
        Window dialog,
        TerrainPolygon reference,
        IReadOnlyList<TerrainTextureSwapChoice> currentLevelChoices,
        TextBlock message,
        TerrainTexturePaintBrush? preferredBrush)
    {
        const int galleryColumns = 6;
        LevelDefinition currentLevel = _currentLevel
            ?? throw new InvalidOperationException("A level must be loaded before opening the texture gallery.");
        List<TerrainTexturePaintSourceLevelOption> sourceLevels =
        [
            new TerrainTexturePaintSourceLevelOption(currentLevel, true),
            .. LevelRealmCatalog.OrderLevels(_catalog.Levels)
                .Where(level => !string.Equals(
                    LevelCatalog.NormalizeKey(level.Key),
                    LevelCatalog.NormalizeKey(currentLevel.Key),
                    StringComparison.OrdinalIgnoreCase))
                .Select(level => new TerrainTexturePaintSourceLevelOption(level, false))
        ];

        ComboBox sourceLevelBox = new()
        {
            Name = "TerrainTextureSourceLevelPicker",
            ItemsSource = sourceLevels,
            MinWidth = 250,
            MinHeight = 36
        };
        Button loadLevelButton = NewButton("Load Level Textures");
        loadLevelButton.Name = "TerrainTextureLoadLevelButton";

        ListBox gallery = new()
        {
            Name = "TerrainTextureGallery",
            MinHeight = 360,
            MaxHeight = 500,
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid
            {
                Columns = galleryColumns,
                RowSpacing = 10,
                ColumnSpacing = 10
            }),
            ItemTemplate = new FuncDataTemplate<TerrainTexturePaintGalleryItem>((item, _) =>
                item == null ? new TextBlock() : BuildTerrainTexturePaintGalleryTile(item))
        };

        List<TerrainTexturePaintGalleryItem> currentItems = currentLevelChoices
            .Select(choice => TerrainTexturePaintGalleryItem.FromCurrentLevel(currentLevel, choice))
            .ToList();
        string normalizedCurrentLevel = LevelCatalog.NormalizeKey(currentLevel.Key);
        _terrainTexturePaintLoadedPaletteItems[normalizedCurrentLevel] = currentItems;

        void SetMessage(string text, bool error = false)
        {
            message.Text = text;
            message.Foreground = new SolidColorBrush(error
                ? Color.FromRgb(160, 48, 48)
                : Color.FromRgb(72, 81, 92));
        }

        void ShowGallery(
            IReadOnlyList<TerrainTexturePaintGalleryItem> items,
            string sourceName,
            string sourceLevelKey)
        {
            gallery.ItemsSource = items;
            int selectedIndex = preferredBrush == null
                ? -1
                : items
                    .Select((item, index) => (item, index))
                    .Where(pair => !pair.item.IsBlocked && preferredBrush.Matches(pair.item))
                    .Select(pair => pair.index)
                    .DefaultIfEmpty(-1)
                    .First();
            if (selectedIndex < 0)
            {
                selectedIndex = items
                .Select((item, index) => (item, index))
                .Where(pair => !pair.item.IsBlocked)
                .Select(pair => pair.index)
                .DefaultIfEmpty(-1)
                .First();
            }
            gallery.SelectedIndex = selectedIndex;
            _terrainTexturePaintPaletteSourceLevelKey = LevelCatalog.NormalizeKey(sourceLevelKey);
            int blocked = items.Count(item => item.IsBlocked);
            SetMessage(items.Count == 0
                ? $"No cached native terrain textures were found for {sourceName}."
                : $"{sourceName}: {items.Count:N0} texture tile(s), {blocked:N0} blocked. Double-click a usable tile, or select it and choose Start Painting.",
                error: items.Count == 0);
        }

        bool TryActivateSelectedTexture()
        {
            if (gallery.SelectedItem is not TerrainTexturePaintGalleryItem selected)
            {
                SetMessage("Choose a texture tile first.", error: true);
                return false;
            }
            if (selected.IsBlocked)
            {
                SetMessage($"{selected.TileName} is blocked: {selected.BlockReason}", error: true);
                return false;
            }

            _terrainTexturePaintPaletteSourceLevelKey = LevelCatalog.NormalizeKey(selected.LevelKey);
            dialog.Close(selected.Result);
            return true;
        }

        gallery.SelectionChanged += (_, _) =>
        {
            if (gallery.SelectedItem is not TerrainTexturePaintGalleryItem selected)
                return;
            SetMessage(selected.IsBlocked
                ? $"{selected.TileName} is blocked: {selected.BlockReason}"
                : selected.Result.Mode == TerrainPaintModeKind.CrossLevelLook
                    ? $"Selected {selected.TileName}. A click uses a private texture slot when one is available; otherwise the editor shows the exact shared texture ID and affected section count before asking permission."
                    : $"Selected {selected.TileName}. Double-click it or choose Start Painting.",
                selected.IsBlocked);
        };
        gallery.DoubleTapped += (_, args) =>
        {
            args.Handled = true;
            TryActivateSelectedTexture();
        };

        void ShowSelectedSourceLevel()
        {
            if (sourceLevelBox.SelectedItem is not TerrainTexturePaintSourceLevelOption selected)
                return;
            string normalizedSource = LevelCatalog.NormalizeKey(selected.Level.Key);
            _terrainTexturePaintPaletteSourceLevelKey = normalizedSource;
            if (_terrainTexturePaintLoadedPaletteItems.TryGetValue(
                    normalizedSource,
                    out IReadOnlyList<TerrainTexturePaintGalleryItem>? loadedItems))
            {
                ShowGallery(loadedItems, selected.Level.DisplayName, selected.Level.Key);
                return;
            }

            gallery.ItemsSource = Array.Empty<TerrainTexturePaintGalleryItem>();
            gallery.SelectedIndex = -1;
            SetMessage($"{selected.Level.DisplayName} is selected. Choose Load Level Textures to load only that level.");
        }

        sourceLevelBox.SelectionChanged += (_, _) =>
        {
            ShowSelectedSourceLevel();
        };

        loadLevelButton.Click += async (_, _) =>
        {
            if (sourceLevelBox.SelectedItem is not TerrainTexturePaintSourceLevelOption selected)
                return;
            if (selected.IsCurrent)
            {
                ShowGallery(currentItems, selected.Level.DisplayName, selected.Level.Key);
                return;
            }

            loadLevelButton.IsEnabled = false;
            sourceLevelBox.IsEnabled = false;
            SetMessage($"Loading {selected.Level.DisplayName} terrain textures...");
            await Task.Yield();
            try
            {
                IReadOnlyList<TerrainCrossLevelLookChoice> choices =
                    BuildTerrainTexturePaintChoicesForSourceLevel(reference, selected.Level);
                List<TerrainTexturePaintGalleryItem> items = choices
                    .Select(TerrainTexturePaintGalleryItem.FromCrossLevel)
                    .ToList();
                string normalizedSource = LevelCatalog.NormalizeKey(selected.Level.Key);
                _terrainTexturePaintLoadedPaletteItems[normalizedSource] = items;
                ShowGallery(items, selected.Level.DisplayName, selected.Level.Key);
            }
            catch (Exception ex)
            {
                EditorDiagnostics.RecordException(
                    $"loading the {selected.Level.DisplayName} terrain texture gallery",
                    ex);
                gallery.ItemsSource = Array.Empty<TerrainTexturePaintGalleryItem>();
                gallery.SelectedIndex = -1;
                SetMessage($"Could not load {selected.Level.DisplayName}: {ex.Message}", error: true);
            }
            finally
            {
                loadLevelButton.IsEnabled = true;
                sourceLevelBox.IsEnabled = true;
            }
        };

        Grid sourceControls = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        sourceControls.Children.Add(sourceLevelBox);
        AddGridControl(sourceControls, loadLevelButton, 1, 0);

        StackPanel body = new()
        {
            Spacing = 12,
            Margin = new Thickness(18)
        };
        body.Children.Add(new TextBlock
        {
            Text = "Choose a texture",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        body.Children.Add(sourceControls);
        body.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(211, 219, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = gallery
        });
        body.Children.Add(message);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button startPainting = NewButton("Start Painting");
        cancel.Click += (_, _) => dialog.Close(null);
        startPainting.Click += (_, _) => TryActivateSelectedTexture();
        buttons.Children.Add(cancel);
        buttons.Children.Add(startPainting);
        body.Children.Add(buttons);

        string requestedSource = LevelCatalog.NormalizeKey(
            preferredBrush?.SourceLevelKey ?? _terrainTexturePaintPaletteSourceLevelKey);
        int requestedSourceIndex = sourceLevels.FindIndex(option => string.Equals(
            LevelCatalog.NormalizeKey(option.Level.Key),
            requestedSource,
            StringComparison.OrdinalIgnoreCase));
        sourceLevelBox.SelectedIndex = requestedSourceIndex >= 0 ? requestedSourceIndex : 0;
        ShowSelectedSourceLevel();
        return body;
    }

    private Control BuildTerrainTexturePaintGalleryTile(TerrainTexturePaintGalleryItem item)
    {
        Control preview = BuildTerrainTextureCatalogPreview(item.LevelKey, item.TextureId, item.Color);
        preview.Opacity = item.IsBlocked ? 0.34 : 1;
        Grid previewLayer = new();
        previewLayer.Children.Add(preview);
        if (item.IsBlocked)
        {
            Border blocked = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(178, 80, 84, 88)),
                CornerRadius = new CornerRadius(5),
                Child = new TextBlock
                {
                    Text = "BLOCKED",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    FontSize = 13
                }
            };
            blocked.Classes.Add("terrain-texture-blocked-overlay");
            previewLayer.Children.Add(blocked);
        }

        StackPanel content = new()
        {
            Spacing = 5,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        content.Children.Add(previewLayer);
        content.Children.Add(new TextBlock
        {
            Text = item.TileName,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(item.IsBlocked
                ? Color.FromRgb(108, 112, 116)
                : Color.FromRgb(31, 38, 45))
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Tile {item.TextureId}",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(105, 113, 122))
        });

        Border tile = new()
        {
            DataContext = item,
            MinHeight = 148,
            Padding = new Thickness(6),
            Background = new SolidColorBrush(item.IsBlocked
                ? Color.FromRgb(225, 227, 229)
                : Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(item.IsBlocked
                ? Color.FromRgb(176, 180, 184)
                : Color.FromRgb(214, 220, 227)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = content
        };
        tile.Classes.Add("terrain-texture-card");
        if (item.IsBlocked)
            tile.Classes.Add("blocked");
        ToolTip.SetTip(tile, item.IsBlocked
            ? item.BlockReason
            : $"Double-click to paint with {item.TileName}.");
        return tile;
    }

    private IReadOnlyList<TerrainCrossLevelLookChoice> BuildTerrainTexturePaintChoicesForSourceLevel(
        TerrainPolygon reference,
        LevelDefinition sourceLevel)
    {
        if (_currentGeometry == null)
            return Array.Empty<TerrainCrossLevelLookChoice>();

        TerrainPolygon? previousTerrain = _selectedTerrain;
        int previousTerrainIndex = _selectedTerrainIndex;
        int previousTerrainPointIndex = _selectedTerrainPointIndex;
        try
        {
            _selectedTerrain = reference;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(reference);
            _selectedTerrainPointIndex = -1;
            return BuildCrossLevelTerrainLookChoices(
                reference.Surface,
                requestedSourceLevelKey: sourceLevel.Key);
        }
        finally
        {
            _selectedTerrain = previousTerrain;
            _selectedTerrainIndex = previousTerrainIndex;
            _selectedTerrainPointIndex = previousTerrainPointIndex;
        }
    }

    private Bitmap? LoadTerrainTextureCatalogPreview(string levelKey, int textureId)
    {
        string normalizedLevel = LevelCatalog.NormalizeKey(levelKey);
        if (string.IsNullOrWhiteSpace(normalizedLevel) || textureId < 0)
            return null;

        if (!_terrainTextureCatalogPreviewPaths.TryGetValue(normalizedLevel, out IReadOnlyDictionary<int, string>? paths))
        {
            Dictionary<int, string> resolved = new();
            string directory = Path.Combine(
                _workspace.RootPath,
                "editor-cache",
                "terrain-textures",
                normalizedLevel);
            string manifest = Path.Combine(directory, "manifest.json");
            if (PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(_workspace.RootPath, normalizedLevel) &&
                TryReadNativeTerrainTexturePreviewFiles(
                    manifest,
                    directory,
                    out IReadOnlyDictionary<int, string> normalFiles,
                    out IReadOnlyDictionary<int, string> closeFiles))
            {
            foreach ((int id, string path) in normalFiles)
                resolved[id] = path;
            // Prefer the close-detail native preview deliberately: this is the
            // largest, clearest representation for choosing a paint source.
            foreach ((int id, string path) in closeFiles)
                resolved[id] = path;
            }

            paths = resolved;
            _terrainTextureCatalogPreviewPaths[normalizedLevel] = paths;
        }

        if (!paths.TryGetValue(textureId, out string? previewPath) || !File.Exists(previewPath))
            return null;

        string cacheKey = $"{normalizedLevel}:{textureId}:{previewPath}";
        if (_terrainTextureCatalogPreviewBitmaps.TryGetValue(cacheKey, out Bitmap? cached))
            return cached;

        try
        {
            Bitmap bitmap = new(previewPath);
            _terrainTextureCatalogPreviewBitmaps[cacheKey] = bitmap;
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _terrainTextureCatalogPreviewBitmaps[cacheKey] = null;
            return null;
        }
    }

    private void ClearTerrainTextureCatalogPreviewCache(bool rebindActivePreview = true)
    {
        _terrainTexturePaintActivePreview.Source = null;
        foreach (Bitmap bitmap in _terrainTextureCatalogPreviewBitmaps.Values
                     .OfType<Bitmap>()
                     .Distinct())
        {
            bitmap.Dispose();
        }
        _terrainTextureCatalogPreviewBitmaps.Clear();
        _terrainTextureCatalogPreviewPaths.Clear();
        _terrainTexturePaintLoadedPaletteItems.Clear();
        _terrainTexturePaintPaletteTargetLevelKey = "";
        _terrainTexturePaintPaletteSourceLevelKey = "";
        if (rebindActivePreview && _activeTerrainTexturePaintBrush != null)
            RefreshTerrainTexturePaintActivePreview();
    }

    private void RefreshTerrainTexturePaintActivePreview()
    {
        TerrainTexturePaintBrush? brush = _activeTerrainTexturePaintBrush;
        if (brush == null)
        {
            _terrainTexturePaintActivePreview.Source = null;
            _terrainTexturePaintActivePreview.IsVisible = false;
            _terrainTexturePaintActivePreviewFallback.IsVisible = false;
            if (_terrainTexturePaintActivePreviewFrame != null)
                _terrainTexturePaintActivePreviewFrame.IsVisible = false;
            return;
        }

        Bitmap? bitmap = LoadTerrainTextureCatalogPreview(brush.SourceLevelKey, brush.TextureId);
        _terrainTexturePaintActivePreview.Source = bitmap;
        _terrainTexturePaintActivePreview.IsVisible = bitmap != null;
        _terrainTexturePaintActivePreviewFallback.Text = $"Texture {brush.TextureId}";
        _terrainTexturePaintActivePreviewFallback.IsVisible = bitmap == null;
        if (_terrainTexturePaintActivePreviewFrame != null)
        {
            _terrainTexturePaintActivePreviewFrame.Background = bitmap == null
                ? new SolidColorBrush(ToAvaloniaColor(brush.PreviewColor))
                : new SolidColorBrush(Color.FromRgb(24, 31, 38));
            _terrainTexturePaintActivePreviewFrame.IsVisible = true;
        }
    }

    internal async Task<string> AssertTerrainTexturePaintModeForTestingAsync()
    {
        LevelDefinition level = _catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
            await SelectLevelAsync(level);
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Artisans did not load for the texture paint-mode smoke.");

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{level.Key}-terrain-edits.json");
        if (File.Exists(terrainEditsPath))
        {
            throw new InvalidOperationException(
                "The texture paint-mode smoke requires an isolated Artisans workspace with no existing terrain edit file.");
        }

        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _selectedTerrainPointIndex = -1;
        TerrainPolygon reference = FindTerrainTexturePaintCatalogReferenceFace()
            ?? throw new InvalidOperationException("The no-selection texture catalog could not find a clean reference face.");
        if (_selectedTerrain != null || _selectedTerrainIndex != -1)
            throw new InvalidOperationException("Opening the no-selection texture catalog mutated the real terrain selection.");

        _selectedTerrain = reference;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(reference);
        IReadOnlyList<TerrainTextureSwapChoice> sourceCatalog = BuildTerrainTextureSwapChoices()
            .Where(choice => choice.CanChooseAsPaintBrushSource)
            .ToArray();
        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _selectedTerrainPointIndex = -1;
        if (sourceCatalog.Count == 0)
            throw new InvalidOperationException("The texture-first catalog did not expose any source-verified native textures.");

        TerrainPolygon? target = null;
        TerrainTextureSwapChoice? source = null;
        IEnumerable<TerrainPolygon> candidateTargets = _currentGeometry.Polygons
            .Where(face =>
                !face.IsTerrainRemoved &&
                face.TextureId >= 0 &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit)
            .OrderBy(face => string.Equals(face.RuntimeKey, "47:6:hp", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(face => string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(face => face.Points.Count == 4 ? 0 : 1)
            .Take(48);
        foreach (TerrainPolygon candidateTarget in candidateTargets)
        {
            _selectedTerrain = candidateTarget;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(candidateTarget);
            TerrainTextureSwapChoice? candidateSource = BuildTerrainTextureSwapChoices()
                .Where(choice => choice.CanApplyAtomically && choice.TextureId != candidateTarget.TextureId)
                .FirstOrDefault(choice =>
                    sourceCatalog.Any(catalogChoice =>
                        catalogChoice.TextureId == choice.TextureId &&
                        string.Equals(catalogChoice.RuntimeKey, choice.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(catalogChoice.VisualRuntimeKey, choice.VisualRuntimeKey, StringComparison.OrdinalIgnoreCase)) &&
                    LoadTerrainTextureCatalogPreview(level.Key, choice.TextureId) != null);
            if (candidateSource == null)
                continue;

            target = candidateTarget;
            source = candidateSource;
            break;
        }
        if (target == null || source == null)
            throw new InvalidOperationException("No clean Artisans face/source pair passed the per-click atomic texture proof.");

        Control preview = BuildTerrainTextureCatalogPreview(level.Key, source.TextureId, source.Color);
        if (preview.Width < 104 || preview.Height < 104 ||
            _terrainTexturePaintActivePreview.Width < 96 ||
            _terrainTexturePaintActivePreview.Height < 96)
        {
            throw new InvalidOperationException("The texture catalog or active-brush preview regressed to a small swatch.");
        }

        int targetIndex = _currentGeometry.Polygons.IndexOf(target);
        int originalTextureId = target.TextureId;
        string targetRuntimeKey = target.RuntimeKey;
        target.ApplyTerrainDeltaZ(32);
        target.ApplyTerrainTranslation(16, -8);
        target.StageTerrainAddClone();
        float[] expectedEditedZ = target.ZValues.ToArray();
        Vector2f[] expectedEditedPoints = target.Points.ToArray();
        TerrainStructureEditKind expectedStructureEdit = target.StructureEdit;
        await PersistCurrentTerrainEditsAsync();
        byte[] prePaintTerrainEdits = File.ReadAllBytes(terrainEditsPath);
        TerrainTexturePaintStageSnapshot prePaintSnapshot = TerrainTexturePaintStageSnapshot.Capture(target);
        Dictionary<string, (int TextureId, TerrainTextureVisualEdit? Visual, TerrainSurfaceBehaviorEdit? Behavior, string Surface, string BehaviorLabel)> untouchedFaces =
            _currentGeometry.Polygons
                .Where(face => !ReferenceEquals(face, target))
                .ToDictionary(
                    face => face.RuntimeKey,
                    face => (
                        face.TextureId,
                        face.TextureVisualEdit,
                        face.SurfaceBehaviorEdit,
                        face.Surface,
                        face.Behavior),
                    StringComparer.OrdinalIgnoreCase);
        TerrainTexturePaintBrush brush = TerrainTexturePaintBrush.FromSameLevel(
            level.Key,
            level.DisplayName,
            source);
        StartTerrainTexturePaintMode(brush);
        if (!_viewport.TerrainTexturePaintMode ||
            _viewport.TerrainTexturePaintBusy ||
            !string.Equals(_viewport.TerrainTexturePaintHint, brush.DisplayLabel, StringComparison.Ordinal) ||
            _terrainTexturePaintActivePreview.Source == null ||
            target.TextureId != originalTextureId)
        {
            throw new InvalidOperationException("Choosing a texture did not enter a clean persistent paint mode with its real preview.");
        }

        TerrainTexturePaintPersistenceFaultForTesting = path =>
        {
            File.WriteAllText(path, "injected incomplete terrain edit file");
            throw new IOException("Injected terrain texture persistence failure.");
        };
        try
        {
            await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        }
        finally
        {
            TerrainTexturePaintPersistenceFaultForTesting = null;
        }
        if (target.TextureId != prePaintSnapshot.TextureId ||
            !Equals(target.TextureVisualEdit, prePaintSnapshot.TextureVisualEdit) ||
            !Equals(target.SurfaceBehaviorEdit, prePaintSnapshot.SurfaceBehaviorEdit) ||
            !string.Equals(target.Surface, prePaintSnapshot.Surface, StringComparison.Ordinal) ||
            !string.Equals(target.Behavior, prePaintSnapshot.Behavior, StringComparison.Ordinal) ||
            !target.ZValues.SequenceEqual(expectedEditedZ) ||
            !target.Points.SequenceEqual(expectedEditedPoints) ||
            target.StructureEdit != expectedStructureEdit ||
            !File.ReadAllBytes(terrainEditsPath).SequenceEqual(prePaintTerrainEdits) ||
            HasUnsavedTerrainEdits() ||
            !IsEnabled ||
            !_viewport.TerrainTexturePaintMode ||
            _viewport.TerrainTexturePaintBusy ||
            _terrainTexturePaintBusy ||
            !string.IsNullOrWhiteSpace(_lastTerrainTexturePaintFace))
        {
            throw new InvalidOperationException(
                "An injected face-local persistence failure did not restore the in-memory face, its unrelated pre-edits, and the prior terrain edit file atomically.");
        }

        target.ApplyTextureVisualEdit(source.NativeVisual!);
        if (source.BehaviorTargetTriangleCount > 0)
        {
            ApplyNativeTerrainBehaviorToFace(
                target,
                source.NativeBehavior!,
                level.Key,
                source.RuntimeKey);
        }
        else
        {
            ApplyVisualOnlyNativeTerrainBehaviorToFace(
                target,
                source.NativeBehavior!,
                level.Key,
                source.RuntimeKey);
        }
        TerrainTexturePaintStageSnapshot preExistingTextureEdit = TerrainTexturePaintStageSnapshot.Capture(target);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        if (!Equals(target.TextureVisualEdit, preExistingTextureEdit.TextureVisualEdit) ||
            !Equals(target.SurfaceBehaviorEdit, preExistingTextureEdit.SurfaceBehaviorEdit) ||
            !string.Equals(target.Behavior, preExistingTextureEdit.Behavior, StringComparison.Ordinal) ||
            !File.ReadAllBytes(terrainEditsPath).SequenceEqual(prePaintTerrainEdits) ||
            !string.IsNullOrWhiteSpace(_lastTerrainTexturePaintFace))
        {
            throw new InvalidOperationException(
                "Texture Paint did not safely refuse a face with pre-existing tint/property edits.");
        }
        prePaintSnapshot.Restore();

        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        if (target.TextureId != source.TextureId ||
            target.TextureVisualEdit == null ||
            target.SurfaceBehaviorEdit == null ||
            !File.Exists(terrainEditsPath) ||
            HasUnsavedTerrainEdits() ||
            !_viewport.TerrainTexturePaintMode ||
            _viewport.TerrainTexturePaintBusy ||
            _terrainTexturePaintBusy ||
            !IsEnabled ||
            !string.Equals(_lastTerrainTexturePaintFace, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A texture paint click did not visibly stage, save, and retain the persistent paint source.");
        }
        TerrainPolygon? changedNeighbor = _currentGeometry.Polygons
            .Where(face => !ReferenceEquals(face, target))
            .FirstOrDefault(face =>
            {
                (int TextureId, TerrainTextureVisualEdit? Visual, TerrainSurfaceBehaviorEdit? Behavior, string Surface, string BehaviorLabel) before =
                    untouchedFaces[face.RuntimeKey];
                return face.TextureId != before.TextureId ||
                    !Equals(face.TextureVisualEdit, before.Visual) ||
                    !Equals(face.SurfaceBehaviorEdit, before.Behavior) ||
                    !string.Equals(face.Surface, before.Surface, StringComparison.Ordinal) ||
                    !string.Equals(face.Behavior, before.BehaviorLabel, StringComparison.Ordinal);
            });
        if (changedNeighbor != null)
        {
            throw new InvalidOperationException(
                $"Face-local texture painting also changed untouched face {changedNeighbor.RuntimeKey}.");
        }

        StopTerrainTexturePaintMode(announce: false);
        if (_activeTerrainTexturePaintBrush != null ||
            _viewport.TerrainTexturePaintMode ||
            _viewport.TerrainTexturePaintBusy ||
            !string.IsNullOrWhiteSpace(_viewport.TerrainTexturePaintHint) ||
            _terrainTexturePaintActivePreview.Source != null)
        {
            throw new InvalidOperationException("Stopping texture paint mode left its source, preview, or viewport interception active.");
        }

        _selectedTerrain = target;
        _selectedTerrainIndex = targetIndex;
        await UndoSelectedTerrainTexturePaintAsync();
        await SelectLevelAsync(level);
        TerrainPolygon reloaded = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The texture paint target did not reload after persisted Undo.");
        if (reloaded.TextureId != originalTextureId ||
            reloaded.TextureVisualEdit != null ||
            reloaded.SurfaceBehaviorEdit != null ||
            !reloaded.ZValues.SequenceEqual(expectedEditedZ) ||
            !reloaded.Points.SequenceEqual(expectedEditedPoints) ||
            reloaded.StructureEdit != expectedStructureEdit ||
            HasUnsavedTerrainEdits())
        {
            throw new InvalidOperationException(
                "Texture-only persisted Undo did not restore the original texture fields while preserving height, XY, and structure edits.");
        }

        _selectedTerrain = reloaded;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(reloaded);
        await UndoSelectedTerrainAsync();
        await SelectLevelAsync(level);
        TerrainPolygon cleaned = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The texture paint target did not reload after smoke cleanup.");
        if (cleaned.IsTerrainEdited || HasUnsavedTerrainEdits())
            throw new InvalidOperationException("The texture paint smoke did not clean up its isolated pre-edits.");

        return $"no-selection catalog, 104px real preview, injected save-failure rollback, pre-existing tint/property refusal, persistent one-face-only {originalTextureId}->{source.TextureId} apply, Stop, and texture-only Undo preserving pre-edited height/XY/structure passed on {targetRuntimeKey}";
    }

    internal async Task<string> AssertCrossLevelTerrainTexturePaintForTestingAsync()
    {
        LevelDefinition artisans = _catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        LevelDefinition gnastysWorld = _catalog.FindByKey("gnastysworld")
            ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
        if (!string.Equals(_currentLevel?.Key, artisans.Key, StringComparison.OrdinalIgnoreCase))
            await SelectLevelAsync(artisans);
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Artisans did not load for the cross-level texture paint smoke.");

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{artisans.Key}-terrain-edits.json");
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, artisans.Key);
        if (File.Exists(terrainEditsPath) || File.Exists(relocationPath))
        {
            throw new InvalidOperationException(
                "The cross-level texture paint smoke requires an isolated Artisans workspace without terrain or relocation edits.");
        }

        NativeTerrainSurfaceLevelCatalog? nativeCatalog = TryBuildNativeTerrainSurfaceCatalog(
            artisans,
            _currentGeometry,
            out string nativeCatalogError);
        if (nativeCatalog == null)
            throw new InvalidOperationException($"Artisans native terrain bindings are unavailable: {nativeCatalogError}");

        TerrainPolygon? target = null;
        TerrainTexturePaintBrush? brush = null;
        foreach (TerrainPolygon candidate in _currentGeometry.Polygons
                     .Where(face =>
                         !face.IsTerrainRemoved &&
                         face.TextureId >= 0 &&
                         string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                         !face.HasTextureEdit &&
                         !face.HasTextureVisualEdit &&
                         !face.HasSurfaceBehaviorEdit &&
                         _currentGeometry.Polygons.Count(other =>
                             !other.IsTerrainRemoved && other.TextureId == face.TextureId) > 1)
                     .OrderBy(face => face.SectorIndex)
                     .ThenBy(face => face.FaceIndex))
        {
            NativeTerrainFaceSurfaceBinding? binding = nativeCatalog.FindFace(candidate.RuntimeKey);
            if (binding is not { HasExactTriangleMapping: true } &&
                binding is not { MatchedVisualTriangleCount: 0, HasAnyCollisionCandidate: false })
            {
                continue;
            }

            _selectedTerrain = candidate;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(candidate);
            _selectedTerrainPointIndex = -1;
            TerrainCrossLevelLookChoice? source = BuildCrossLevelTerrainLookChoices(
                    candidate.Surface,
                    requestedSourceLevelKey: gnastysWorld.Key)
                .FirstOrDefault(choice =>
                    choice.TextureId == 17 &&
                    choice.CanApplyAtomically);
            if (source == null)
                continue;

            target = candidate;
            brush = TerrainTexturePaintBrush.FromCrossLevel(artisans.Key, source);
            break;
        }

        if (target == null || brush == null)
        {
            throw new InvalidOperationException(
                "No clean shared Artisans face exposed Gnasty's World texture 17 as a source-verified paint brush.");
        }

        int targetIndex = _currentGeometry.Polygons.IndexOf(target);
        int originalTextureId = target.TextureId;
        int originalSharedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == originalTextureId);
        if (originalSharedFaceCount <= 1)
            throw new InvalidOperationException("The focused cross-level paint target did not reproduce the shared-texture regression.");

        target.ApplyTerrainDeltaZ(24);
        target.ApplyTerrainTranslation(12, -6);
        target.StageTerrainAddClone();
        float[] prePaintZ = target.ZValues.ToArray();
        Vector2f[] prePaintPoints = target.Points.ToArray();
        TerrainStructureEditKind prePaintStructure = target.StructureEdit;
        await PersistCurrentTerrainEditsAsync();
        Dictionary<string, TerrainTexturePaintFaceAuditSnapshot> facesBefore = _currentGeometry.Polygons
            .ToDictionary(
                face => face.RuntimeKey,
                TerrainTexturePaintFaceAuditSnapshot.Capture,
                StringComparer.OrdinalIgnoreCase);

        byte[] terrainBeforeCanceledConfirmation = File.ReadAllBytes(terrainEditsPath);
        TerrainTexturePaintFaceAuditSnapshot targetBeforeCanceledConfirmation =
            TerrainTexturePaintFaceAuditSnapshot.Capture(target);
        TerrainTextureSharedReplacementConfirmationForTesting = (_, textureId, faceCount) =>
            Task.FromResult(false);
        StartTerrainTexturePaintMode(brush);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        if (!targetBeforeCanceledConfirmation.Matches(target) ||
            _nativeTerrainTextureRelocations.Count != 0 ||
            !File.ReadAllBytes(terrainEditsPath).SequenceEqual(terrainBeforeCanceledConfirmation) ||
            !(_statusText.Text ?? "").Contains("Canceled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Canceling the explicit shared-texture confirmation changed the face, relocation manifest, or prior terrain edit file.");
        }

        TerrainTextureSharedReplacementConfirmationForTesting = (_, textureId, faceCount) =>
            Task.FromResult(textureId == originalTextureId && faceCount == originalSharedFaceCount);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        NativeTerrainTextureRelocationEdit relocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => brush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"Painting shared Artisans texture {originalTextureId} with Gnasty's World texture 17 did not stage the confirmed shared relocation. Status: {_statusText.Text}");
        if (relocation.TargetTextureId != originalTextureId ||
            target.TextureId != originalTextureId ||
            target.HasTextureEdit ||
            !target.ZValues.SequenceEqual(prePaintZ) ||
            !target.Points.SequenceEqual(prePaintPoints) ||
            target.StructureEdit != prePaintStructure)
        {
            throw new InvalidOperationException(
                "The confirmed shared replacement did not preserve the target texture ID and its unrelated height, XY, and structure edits.");
        }

        TerrainPolygon? changedUnrelatedFace = _currentGeometry.Polygons
            .Where(face => face.OriginalTextureId != originalTextureId)
            .FirstOrDefault(face => !facesBefore[face.RuntimeKey].Matches(face));
        if (changedUnrelatedFace != null)
        {
            throw new InvalidOperationException(
                $"The shared replacement also changed unrelated Artisans texture {changedUnrelatedFace.OriginalTextureId} on face {changedUnrelatedFace.RuntimeKey}.");
        }
        if (_currentGeometry.Polygons
            .Where(face => face.OriginalTextureId == originalTextureId)
            .Any(face => face.TextureId != originalTextureId))
        {
            throw new InvalidOperationException(
                "The shared replacement unexpectedly remapped an affected face's texture ID instead of replacing only the audited native record.");
        }

        string targetRuntimeKey = target.RuntimeKey;
        await SelectLevelAsync(artisans);
        target = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The painted Artisans face did not reload.");
        if (target.TextureId != originalTextureId ||
            !target.ZValues.SequenceEqual(prePaintZ) ||
            !target.Points.SequenceEqual(prePaintPoints) ||
            target.StructureEdit != prePaintStructure ||
            !_nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == originalTextureId && brush.Matches(edit)))
        {
            throw new InvalidOperationException(
                "The confirmed shared cross-level replacement or unrelated geometry edits did not survive save/reload.");
        }

        _selectedTerrain = target;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(target);
        await UndoSelectedTerrainTexturePaintAsync();
        if (target.TextureId != originalTextureId ||
            _nativeTerrainTextureRelocations.Any(edit => edit.TargetTextureId == originalTextureId) ||
            !target.ZValues.SequenceEqual(prePaintZ) ||
            !target.Points.SequenceEqual(prePaintPoints) ||
            target.StructureEdit != prePaintStructure)
        {
            throw new InvalidOperationException(
                "Undoing the shared replacement did not release its relocation and preserve unrelated target geometry edits.");
        }

        byte[] terrainBeforeFailure = File.ReadAllBytes(terrainEditsPath);
        TerrainTexturePaintFaceAuditSnapshot faceBeforeFailure = TerrainTexturePaintFaceAuditSnapshot.Capture(target);
        TerrainTexturePaintPersistenceFaultForTesting = path =>
        {
            File.WriteAllText(path, "injected incomplete cross-level terrain edit file");
            throw new IOException("Injected cross-level texture persistence failure.");
        };
        StartTerrainTexturePaintMode(brush);
        try
        {
            await ApplyTerrainTexturePaintBrushAsync(_selectedTerrainIndex, target);
        }
        finally
        {
            TerrainTexturePaintPersistenceFaultForTesting = null;
        }
        if (!faceBeforeFailure.Matches(target) ||
            _nativeTerrainTextureRelocations.Count != 0 ||
            !File.ReadAllBytes(terrainEditsPath).SequenceEqual(terrainBeforeFailure))
        {
            throw new InvalidOperationException(
                "An injected cross-level save failure did not roll back the shared relocation manifest and prior terrain edit file atomically.");
        }

        StopTerrainTexturePaintMode(announce: false);
        await UndoSelectedTerrainAsync();
        await SelectLevelAsync(artisans);
        TerrainPolygon cleaned = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The cross-level paint target did not reload after cleanup.");
        if (cleaned.IsTerrainEdited ||
            NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, artisans.Key).Count != 0)
        {
            throw new InvalidOperationException("The cross-level texture paint smoke did not clean up its isolated edits.");
        }

        TerrainTextureSharedReplacementConfirmationForTesting = null;
        return $"Artisans shared texture {originalTextureId} ({originalSharedFaceCount} faces) <- Gnasty's World texture 17 confirmation Cancel stayed non-mutating, Replace Shared Texture staged; unrelated texture IDs stayed unchanged, save/reload, Undo, geometry preservation, and injected-failure rollback passed";
    }

    private async Task ChooseTerrainTexturePaintBrushAsync()
    {
        await ChooseTerrainTexturePaintBrushCoreAsync(
            reuseLoadedPalette: false,
            preferredBrush: null);
    }

    private async Task ReturnToTerrainTexturePaletteAsync()
    {
        TerrainTexturePaintBrush? previousBrush = _activeTerrainTexturePaintBrush;
        if (previousBrush == null || !_viewport.TerrainTexturePaintMode)
        {
            _statusText.Text = "Choose a texture before returning to its palette.";
            return;
        }
        if (_terrainTexturePaintBusy)
        {
            _statusText.Text = "Wait for the current texture to finish applying before returning to the palette.";
            return;
        }

        StopTerrainTexturePaintMode(announce: false);
        _statusText.Text = $"Returning to the {previousBrush.SourceLevelName} texture palette...";
        await ChooseTerrainTexturePaintBrushCoreAsync(
            reuseLoadedPalette: true,
            preferredBrush: previousBrush);
    }

    private async Task ChooseTerrainTexturePaintBrushCoreAsync(
        bool reuseLoadedPalette,
        TerrainTexturePaintBrush? preferredBrush)
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before choosing a terrain texture.";
            return;
        }

        string normalizedTargetLevel = LevelCatalog.NormalizeKey(_currentLevel.Key);
        bool canReuseLoadedPalette = reuseLoadedPalette &&
            string.Equals(
                _terrainTexturePaintPaletteTargetLevelKey,
                normalizedTargetLevel,
                StringComparison.OrdinalIgnoreCase) &&
            _terrainTexturePaintLoadedPaletteItems.Count > 0;
        if (!canReuseLoadedPalette)
        {
            // A fresh chooser must reflect a newly built or replaced portable
            // cache. Returning from active paint mode deliberately skips this
            // invalidation so already-loaded donor catalogs stay in memory.
            ClearTerrainTextureCatalogPreviewCache();
            preferredBrush = null;
        }
        _terrainTexturePaintPaletteTargetLevelKey = normalizedTargetLevel;

        TerrainPolygon? reference = FindTerrainTexturePaintCatalogReferenceFace();
        if (reference == null)
        {
            _statusText.Text = "This level has no editable textured terrain face to use for the texture catalog.";
            return;
        }

        int referenceIndex = _currentGeometry.Polygons.IndexOf(reference);
        TerrainPolygon? previousTerrain = _selectedTerrain;
        int previousTerrainIndex = _selectedTerrainIndex;
        int previousTerrainPointIndex = _selectedTerrainPointIndex;
        List<TerrainTextureSwapChoice> inGameChoices;
        List<TerrainCrossLevelLookChoice> crossLevelChoices = [];

        _statusText.Text = $"Loading original terrain textures from {_currentLevel.DisplayName}...";
        await Task.Yield();
        try
        {
            // Catalog construction needs a target for its preview notes. This temporary reference
            // is never selected or edited; the donor identity is re-audited against each real click.
            _selectedTerrain = reference;
            _selectedTerrainIndex = referenceIndex;
            _selectedTerrainPointIndex = -1;
            inGameChoices = BuildTerrainTextureSwapChoices()
                .OrderBy(choice => TerrainSummarySurfaceRank(choice.Surface))
                .ThenByDescending(choice => choice.FaceCount)
                .ThenBy(choice => choice.TextureId)
                .ToList();
        }
        finally
        {
            _selectedTerrain = previousTerrain;
            _selectedTerrainIndex = previousTerrainIndex;
            _selectedTerrainPointIndex = previousTerrainPointIndex;
        }

        ColorPalettePicker lowPicker = CreateTerrainColorPicker(
            ScaleColor(reference.SurfaceColor, 0.58),
            "Source shadow");
        ColorPalettePicker highPicker = CreateTerrainColorPicker(
            ScaleColor(reference.SurfaceColor, 1.28),
            "Source highlight");
        TextBlock message = NewSmallNote("");
        Window dialog = new()
        {
            Title = "Choose Terrain Texture",
            Width = 1050,
            Height = 760,
            MinWidth = 900,
            MinHeight = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildTerrainPaintDialogContent(
            dialog,
            reference,
            lowPicker,
            highPicker,
            inGameChoices,
            crossLevelChoices,
            message,
            chooseForPaintMode: true,
            preferredPaintBrush: preferredBrush);

        TerrainPaintDialogResult? result = await dialog.ShowDialog<TerrainPaintDialogResult?>(this);
        TerrainTexturePaintBrush? brush = result?.Mode switch
        {
            TerrainPaintModeKind.InGameLook when result.InGameLook != null =>
                TerrainTexturePaintBrush.FromSameLevel(_currentLevel.Key, _currentLevel.DisplayName, result.InGameLook),
            TerrainPaintModeKind.CrossLevelLook when result.CrossLevelLook != null =>
                TerrainTexturePaintBrush.FromCrossLevel(_currentLevel.Key, result.CrossLevelLook),
            _ => null
        };
        if (brush == null)
        {
            if (canReuseLoadedPalette && preferredBrush != null)
            {
                StartTerrainTexturePaintMode(preferredBrush);
                _statusText.Text = $"Texture selection canceled; resumed painting with {preferredBrush.DisplayLabel}.";
            }
            else
            {
                _statusText.Text = "Texture selection canceled; the terrain was not changed.";
            }
            return;
        }

        StartTerrainTexturePaintMode(brush);
    }

    private TerrainPolygon? FindTerrainTexturePaintCatalogReferenceFace()
    {
        if (_currentGeometry == null)
            return null;
        HashSet<int> relocatedTextureIds = _nativeTerrainTextureRelocations
            .Select(edit => edit.TargetTextureId)
            .ToHashSet();
        if (_selectedTerrain is { TextureId: >= 0, IsTerrainRemoved: false } selected &&
            !relocatedTextureIds.Contains(selected.TextureId) &&
            !selected.HasTextureEdit &&
            !selected.HasTextureVisualEdit &&
            !selected.HasSurfaceBehaviorEdit)
        {
            return _selectedTerrain;
        }

        return _currentGeometry.Polygons
            .Where(face =>
                !face.IsTerrainRemoved &&
                face.TextureId >= 0 &&
                !relocatedTextureIds.Contains(face.TextureId) &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit)
            .OrderBy(face => string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(face => face.Points.Count == 4 ? 0 : 1)
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .FirstOrDefault();
    }

    private void StartTerrainTexturePaintMode(TerrainTexturePaintBrush brush)
    {
        if (_pendingMobyAdd != null)
            CancelPendingMobyPlacement();

        _activeTerrainTexturePaintBrush = brush;
        _terrainTexturePaintPaletteTargetLevelKey = LevelCatalog.NormalizeKey(brush.TargetLevelKey);
        _terrainTexturePaintPaletteSourceLevelKey = LevelCatalog.NormalizeKey(brush.SourceLevelKey);
        _terrainTexturePaintBusy = false;
        _terrainTexturePaintStopRequested = false;
        _terrainTexturePaintStopRequestedAnnounce = false;
        _lastTerrainTexturePaintFace = "";
        _viewport.TerrainBrushAction = TerrainBrushAction.Off;
        _viewport.ObjectPlacementMode = false;
        _viewport.TerrainTexturePaintHint = brush.DisplayLabel;
        _viewport.TerrainTexturePaintMode = true;
        _viewport.TerrainTexturePaintBusy = false;
        RefreshTerrainTexturePaintActivePreview();
        RefreshTerrainTexturePaintModeUi();
        _statusText.Text = $"Texture Paint mode active with {brush.DisplayLabel}. Click terrain faces to paint; press Esc or Stop Painting when finished.";
    }

    private void StopTerrainTexturePaintMode(bool announce)
    {
        if (_terrainTexturePaintBusy)
        {
            _terrainTexturePaintStopRequested = true;
            _terrainTexturePaintStopRequestedAnnounce |= announce;
            if (announce)
                _statusText.Text = "Texture Paint mode will stop as soon as the current texture finishes applying.";
            return;
        }

        bool wasActive = _activeTerrainTexturePaintBrush != null || _viewport.TerrainTexturePaintMode;
        _activeTerrainTexturePaintBrush = null;
        _terrainTexturePaintBusy = false;
        _terrainTexturePaintStopRequested = false;
        _terrainTexturePaintStopRequestedAnnounce = false;
        _lastTerrainTexturePaintFace = "";
        _viewport.TerrainTexturePaintBusy = false;
        _viewport.TerrainTexturePaintMode = false;
        _viewport.TerrainTexturePaintHint = "";
        _terrainTexturePaintActivePreview.Source = null;
        _terrainTexturePaintActivePreview.IsVisible = false;
        _terrainTexturePaintActivePreviewFallback.IsVisible = false;
        if (_terrainTexturePaintActivePreviewFrame != null)
            _terrainTexturePaintActivePreviewFrame.IsVisible = false;
        RefreshTerrainTexturePaintModeUi();
        if (announce && wasActive)
            _statusText.Text = "Texture Paint mode stopped. Terrain clicks now select and move normally.";
    }

    private void RefreshTerrainTexturePaintModeUi()
    {
        if (_activeTerrainTexturePaintBrush == null)
        {
            _terrainTexturePaintModeText.Text =
                "No texture selected. Choose a texture to enter paint mode; you do not need to select a terrain face first.";
            if (_terrainTexturePaintStopButton != null)
                _terrainTexturePaintStopButton.IsEnabled = false;
            if (_terrainTexturePaintReturnButton != null)
                _terrainTexturePaintReturnButton.IsEnabled = false;
            return;
        }

        string state = _terrainTexturePaintBusy
            ? "Applying and checking safety..."
            : "Active — click terrain faces to paint. Press Esc to stop.";
        string last = string.IsNullOrWhiteSpace(_lastTerrainTexturePaintFace)
            ? ""
            : $" Last target: {_lastTerrainTexturePaintFace}.";
        _terrainTexturePaintModeText.Text = $"{state}\nSource: {_activeTerrainTexturePaintBrush.DisplayLabel}.{last}";
        if (_terrainTexturePaintStopButton != null)
            _terrainTexturePaintStopButton.IsEnabled = !_terrainTexturePaintBusy;
        if (_terrainTexturePaintReturnButton != null)
            _terrainTexturePaintReturnButton.IsEnabled = !_terrainTexturePaintBusy;
    }

    private async Task ApplyTerrainTexturePaintBrushAsync(int terrainIndex, TerrainPolygon terrain)
    {
        TerrainTexturePaintBrush? brush = _activeTerrainTexturePaintBrush;
        if (brush == null || !_viewport.TerrainTexturePaintMode)
            return;
        if (_terrainTexturePaintBusy)
        {
            _statusText.Text = "The previous texture paint is still being checked and applied.";
            return;
        }
        if (_currentLevel == null || _currentGeometry == null ||
            !string.Equals(_currentLevel.Key, brush.TargetLevelKey, StringComparison.OrdinalIgnoreCase))
        {
            StopTerrainTexturePaintMode(announce: false);
            _statusText.Text = "Texture Paint mode stopped because the loaded level changed.";
            return;
        }
        if (terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count ||
            !ReferenceEquals(_currentGeometry.Polygons[terrainIndex], terrain) ||
            terrain.IsTerrainRemoved || terrain.TextureId < 0)
        {
            _statusText.Text = "That terrain face cannot receive a native texture.";
            return;
        }

        bool editorWasEnabled = IsEnabled;
        _terrainTexturePaintBusy = true;
        _viewport.TerrainTexturePaintBusy = true;
        // Invalidate any level load that was already awaiting its background
        // cache parse before this paint click began. ApplyLoadedLevel also
        // rejects a late result while painting is busy.
        _levelLoadRequestId++;
        SyncLevelPickers(_currentLevel);
        RefreshLevelSelectionAvailability();
        IsEnabled = false;
        _selectedTerrain = terrain;
        _selectedTerrainIndex = terrainIndex;
        _selectedTerrainPointIndex = -1;
        RefreshTerrainTexturePaintModeUi();
        EditorDiagnostics.RecordAction(
            "Terrain texture paint click",
            $"Target: {_currentLevel.Key}:{terrain.RuntimeKey}/texture {terrain.TextureId}; source: {brush.DisplayLabel}");

        try
        {
            bool applied = false;
            if (brush.Kind == TerrainTexturePaintSourceKind.SameLevel)
            {
                if (terrain.HasTextureEdit ||
                    terrain.HasTextureVisualEdit ||
                    terrain.HasSurfaceBehaviorEdit)
                {
                    _statusText.Text =
                        $"Face {terrain.RuntimeKey} already has a face-local texture, tint, or gameplay-property edit. " +
                        "Undo that texture paint first so the existing edit cannot be overwritten; no terrain changed.";
                    return;
                }

                TerrainTextureSwapChoice? choice = BuildTerrainTextureSwapChoices()
                    .FirstOrDefault(brush.Matches);
                if (choice == null)
                {
                    _statusText.Text = $"The selected source {brush.DisplayLabel} is no longer available. Choose the texture again; no terrain changed.";
                    return;
                }
                if (!choice.CanApplyAtomically)
                {
                    _statusText.Text = $"{brush.DisplayLabel} cannot be painted on face {terrain.RuntimeKey}: {choice.AtomicBlockReason} No terrain changed; paint mode remains active.";
                    return;
                }

                await ApplySelectedTerrainInGameLookAsync(choice);
                applied = terrain.TextureId == choice.TextureId &&
                    terrain.TextureVisualEdit == choice.NativeVisual;
            }
            else
            {
                NativeTerrainTextureRelocationEdit? existingRelocation =
                    _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                        edit.TargetTextureId == terrain.TextureId);
                if (existingRelocation != null && brush.Matches(existingRelocation))
                {
                    _lastTerrainTexturePaintFace = terrain.RuntimeKey;
                    bool privateFaceAssignment = terrain.HasTextureEdit &&
                        terrain.OriginalTextureId != terrain.TextureId;
                    _statusText.Text = privateFaceAssignment
                        ? $"Face {terrain.RuntimeKey} already uses {brush.DisplayLabel} through private texture {terrain.TextureId}; " +
                          "no duplicate edit was created. Paint mode remains active."
                        : $"Shared texture {terrain.TextureId} already uses {brush.DisplayLabel}; " +
                          "no duplicate replacement was created. Paint mode remains active.";
                    return;
                }
                if (existingRelocation != null)
                {
                    _statusText.Text =
                        $"Face {terrain.RuntimeKey} already uses relocated texture {terrain.TextureId} from {existingRelocation.DonorLevelName}. " +
                        "Undo that texture paint before applying a different source; no terrain changed.";
                    return;
                }

                if (terrain.HasTextureEdit || terrain.HasTextureVisualEdit || terrain.HasSurfaceBehaviorEdit)
                {
                    _statusText.Text =
                        $"Face {terrain.RuntimeKey} already has a face-local texture, tint, or gameplay-property edit. " +
                        "Undo that texture paint first so the existing edit cannot be overwritten; no terrain changed.";
                    return;
                }

                applied = await ApplyCrossLevelTerrainTexturePaintToFaceAsync(brush, terrain);
            }

            if (applied)
                _lastTerrainTexturePaintFace = terrain.RuntimeKey;
        }
        catch (Exception ex)
        {
            EditorDiagnostics.RecordException("applying the active terrain texture paint source", ex);
            _statusText.Text = $"Could not paint face {terrain.RuntimeKey}: {ex.Message} Paint mode remains active.";
        }
        finally
        {
            _terrainTexturePaintBusy = false;
            _viewport.TerrainTexturePaintBusy = false;
            IsEnabled = editorWasEnabled;
            RefreshLevelSelectionAvailability();
            if (_terrainTexturePaintStopRequested)
            {
                bool announceStop = _terrainTexturePaintStopRequestedAnnounce;
                _terrainTexturePaintStopRequested = false;
                _terrainTexturePaintStopRequestedAnnounce = false;
                StopTerrainTexturePaintMode(announceStop);
            }
            else
            {
                RefreshTerrainTexturePaintModeUi();
            }
        }
    }

    private async Task<bool> ApplyCrossLevelTerrainTexturePaintToFaceAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain)
    {
        if (_currentLevel == null || _currentGeometry == null || _selectedTerrainIndex < 0)
            return false;

        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        LevelDefinition? sourceLevel = _catalog.FindByKey(brush.SourceLevelKey);
        if (sourceLevel == null || string.IsNullOrWhiteSpace(sourceImage) || !File.Exists(sourceImage))
        {
            _statusText.Text =
                $"Could not inspect {brush.DisplayLabel}; choose the retail Spyro BIN/CUE first. No terrain changed.";
            return false;
        }

        NativeTerrainTextureRelocationEdit? reusablePrivateRelocation =
            _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                brush.Matches(edit) &&
                !_currentGeometry.Polygons.Any(face =>
                    face.OriginalTextureId == edit.TargetTextureId));
        if (reusablePrivateRelocation != null)
        {
            TerrainTexturePaintStageSnapshot reuseSnapshot = TerrainTexturePaintStageSnapshot.Capture(terrain);
            terrain.ApplyTextureOverride(reusablePrivateRelocation.TargetTextureId);
            TerrainCrossLevelLookChoice? reuseChoice = BuildCrossLevelTerrainLookChoices(
                    terrain.Surface,
                    brush,
                    allowSelectedFaceLocalTextureTarget: true)
                .FirstOrDefault(brush.Matches);
            reuseSnapshot.Restore();
            if (reuseChoice?.CanApplyAtomically == true &&
                await AssignExistingPrivateRelocationToFaceAsync(
                    brush,
                    terrain,
                    reusablePrivateRelocation,
                    reuseChoice))
            {
                return true;
            }
        }

        IReadOnlyList<TerrainTextureSlot> targetSlots;
        try
        {
            targetSlots = TerrainPatchExporter.InspectTextureSlots(sourceImage, _currentLevel);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            _statusText.Text = $"Could not inspect {_currentLevel.DisplayName}'s private texture slots: {ex.Message} No terrain changed.";
            return false;
        }

        HashSet<int> usedTextureIds = _currentGeometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
            .Select(face => face.TextureId)
            .ToHashSet();
        foreach (CustomTerrainTextureImport import in _customTerrainTextures)
            usedTextureIds.Add(import.TextureId);
        foreach (NativeTerrainTextureRelocationEdit relocation in _nativeTerrainTextureRelocations)
            usedTextureIds.Add(relocation.TargetTextureId);

        TerrainTextureCatalog targetCatalog = BuildNativeTerrainTextureCatalog(
            _currentLevel,
            _currentGeometry,
            out string targetCatalogError);
        Dictionary<int, TerrainTextureCatalogEntry> targetEntries = targetCatalog.Entries
            .ToDictionary(entry => entry.TextureId);
        TerrainTextureSlot[] privateCandidates = targetSlots
            .Where(slot =>
                !usedTextureIds.Contains(slot.TextureId) &&
                slot.HasNormalDescriptors &&
                slot.HasCloseDescriptors &&
                targetEntries.TryGetValue(slot.TextureId, out TerrainTextureCatalogEntry? entry) &&
                entry.Readiness.TargetRuntime.CanPersist)
            .OrderBy(slot => slot.TextureId)
            .ToArray();
        if (privateCandidates.Length == 0)
        {
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                string.IsNullOrWhiteSpace(targetCatalogError)
                    ? "This level has no unused runtime-stable native texture record for a private face texture."
                    : targetCatalogError);
        }

        TerrainTexturePaintStageSnapshot originalFace = TerrainTexturePaintStageSnapshot.Capture(terrain);
        TerrainTextureSlot? privateSlot = null;
        TerrainCrossLevelLookChoice? auditedChoice = null;
        string firstBlockReason = "";
        foreach (TerrainTextureSlot candidate in privateCandidates)
        {
            terrain.ApplyTextureOverride(candidate.TextureId);
            TerrainCrossLevelLookChoice? choice = BuildCrossLevelTerrainLookChoices(
                    terrain.Surface,
                    brush,
                    allowSelectedFaceLocalTextureTarget: true)
                .FirstOrDefault(brush.Matches);
            originalFace.Restore();
            if (choice?.CanApplyAtomically == true)
            {
                privateSlot = candidate;
                auditedChoice = choice;
                break;
            }

            if (string.IsNullOrWhiteSpace(firstBlockReason) && choice != null)
                firstBlockReason = choice.AtomicBlockReason;
        }

        if (privateSlot == null || auditedChoice == null)
        {
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                string.IsNullOrWhiteSpace(firstBlockReason)
                    ? "No private target slot passed the native art, property, and runtime proof."
                    : firstBlockReason);
        }

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{_currentLevel.Key}-terrain-material-overrides.json");
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        Dictionary<string, byte[]?> filesBefore = new(StringComparer.OrdinalIgnoreCase)
        {
            [terrainEditsPath] = File.Exists(terrainEditsPath) ? File.ReadAllBytes(terrainEditsPath) : null,
            [relocationPath] = File.Exists(relocationPath) ? File.ReadAllBytes(relocationPath) : null,
            [materialOverridesPath] = File.Exists(materialOverridesPath) ? File.ReadAllBytes(materialOverridesPath) : null,
            [customTexturesPath] = File.Exists(customTexturesPath) ? File.ReadAllBytes(customTexturesPath) : null
        };
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocationsBefore = _nativeTerrainTextureRelocations;
        IReadOnlyList<CustomTerrainTextureImport> customTexturesBefore = _customTerrainTextures;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;

        terrain.ApplyTextureOverride(privateSlot.TextureId);
        auditedChoice = BuildCrossLevelTerrainLookChoices(
                terrain.Surface,
                brush,
                allowSelectedFaceLocalTextureTarget: true)
            .FirstOrDefault(brush.Matches);
        if (auditedChoice?.CanApplyAtomically != true)
        {
            originalFace.Restore();
            _statusText.Text =
                $"The private target proof changed before {brush.DisplayLabel} could be applied. No terrain changed; click the face again.";
            return false;
        }

        try
        {
            await ApplySelectedTerrainCrossLevelLookAsync(
                auditedChoice,
                allowFaceLocalTextureTarget: true);
        }
        catch (Exception ex)
        {
            EditorDiagnostics.RecordException("applying a face-local cross-level terrain texture", ex);
        }

        bool applied =
            terrain.TextureId == privateSlot.TextureId &&
            _nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == privateSlot.TextureId && brush.Matches(edit));
        if (applied)
            return true;

        foreach ((string path, byte[]? content) in filesBefore)
        {
            if (content == null)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _workspace.RootPath);
                File.WriteAllBytes(path, content);
            }
        }
        originalFace.Restore();
        _nativeTerrainTextureRelocations = relocationsBefore;
        _customTerrainTextures = customTexturesBefore;
        _loadedTerrainEdits = loadedTerrainEditsBefore;
        _savedTerrainEditSignature = savedTerrainSignatureBefore;
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        originalFace.Restore();
        ApplyCustomTerrainTexturePreviews();
        _viewport.NotifyTerrainPresentationDataChanged();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        if (string.IsNullOrWhiteSpace(_statusText.Text) ||
            !(_statusText.Text ?? "").Contains("No terrain changed", StringComparison.OrdinalIgnoreCase))
        {
            _statusText.Text =
                $"Could not apply {brush.DisplayLabel} to face {terrain.RuntimeKey}; its private texture assignment was rolled back. No terrain changed.";
        }
        return false;
    }

    private async Task<bool> ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        string privateSlotReason)
    {
        if (_currentGeometry == null)
            return false;

        int targetTextureId = terrain.TextureId;
        int affectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == targetTextureId);
        TerrainCrossLevelLookChoice? sharedChoice = BuildCrossLevelTerrainLookChoices(
                terrain.Surface,
                brush)
            .FirstOrDefault(brush.Matches);
        if (sharedChoice == null)
        {
            _statusText.Text =
                $"Cannot paint face {terrain.RuntimeKey} with {brush.DisplayLabel}: the selected source is no longer available. " +
                $"{privateSlotReason} No terrain changed.";
            return false;
        }
        if (!sharedChoice.CanApplyAtomically)
        {
            _statusText.Text =
                $"Cannot paint face {terrain.RuntimeKey} privately, and shared texture {targetTextureId} is also blocked: " +
                $"{sharedChoice.AtomicBlockReason} No terrain changed.";
            return false;
        }

        bool accepted = TerrainTextureSharedReplacementConfirmationForTesting != null
            ? await TerrainTextureSharedReplacementConfirmationForTesting(
                brush.DisplayLabel,
                targetTextureId,
                affectedFaceCount)
            : await ShowSharedTerrainTextureReplacementConfirmationAsync(
                brush,
                terrain,
                targetTextureId,
                affectedFaceCount,
                privateSlotReason);
        if (!accepted)
        {
            _statusText.Text =
                $"Canceled {brush.DisplayLabel} on shared texture {targetTextureId}; no terrain changed and paint mode remains active.";
            return false;
        }

        await ApplySelectedTerrainCrossLevelLookAsync(sharedChoice);
        return _nativeTerrainTextureRelocations.Any(edit =>
            edit.TargetTextureId == targetTextureId && brush.Matches(edit));
    }

    private async Task<bool> ShowSharedTerrainTextureReplacementConfirmationAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        int targetTextureId,
        int affectedFaceCount,
        string privateSlotReason)
    {
        Window dialog = new()
        {
            Title = "Replace Shared Terrain Texture?",
            Width = 620,
            Height = 330,
            MinWidth = 520,
            MinHeight = 290,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        StackPanel content = new()
        {
            Spacing = 12,
            Margin = new Thickness(18)
        };
        content.Children.Add(new TextBlock
        {
            Text = "This face cannot receive a private texture slot",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        content.Children.Add(new TextBlock
        {
            Text =
                $"Face {terrain.RuntimeKey} uses {(_currentLevel?.DisplayName ?? "the destination level")} texture record {targetTextureId}, which is shared by {affectedFaceCount:N0} terrain sections. " +
                $"{privateSlotReason}\n\nReplacing the shared record with {brush.DisplayLabel} will change exactly those {affectedFaceCount:N0} sections together. " +
                "No other texture IDs will be changed. The normal native relocation proof and Create BIN readback checks still apply.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 67, 78)),
            LineHeight = 19
        });
        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        Button replace = NewButton("Replace Shared Texture");
        cancel.Click += (_, _) => dialog.Close(false);
        replace.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(replace);
        content.Children.Add(buttons);
        dialog.Content = content;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task<bool> AssignExistingPrivateRelocationToFaceAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        NativeTerrainTextureRelocationEdit relocation,
        TerrainCrossLevelLookChoice choice)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return false;

        TerrainTexturePaintStageSnapshot snapshot = TerrainTexturePaintStageSnapshot.Capture(terrain);
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        byte[]? terrainEditsBefore = File.Exists(terrainEditsPath)
            ? File.ReadAllBytes(terrainEditsPath)
            : null;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;
        try
        {
            terrain.ApplyTextureOverride(relocation.TargetTextureId);
            if (!choice.PreservesTargetNativeSurface)
            {
                NativeTerrainSurfaceSignature nativeBehavior = choice.NativeBehavior
                    ?? throw new InvalidOperationException("The reusable texture has no native surface-property proof.");
                ApplyNativeTerrainBehaviorToFace(
                    terrain,
                    nativeBehavior,
                    choice.LevelKey,
                    choice.RuntimeKey);
            }

            TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
            TerrainTexturePaintPersistenceFaultForTesting?.Invoke(terrainEditsPath);
            int savedEdits = await PersistCurrentTerrainEditsAsync();
            ApplyCustomTerrainTexturePreviews();
            RefreshCurrentLevelDetails();
            _viewport.NotifyTerrainPresentationDataChanged();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, terrain));
            _statusText.Text =
                $"Painted only face {terrain.RuntimeKey} with {brush.DisplayLabel} by reusing private texture {relocation.TargetTextureId}; " +
                $"the other terrain sections kept their texture IDs and {savedEdits} terrain edit(s) are saved.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            snapshot.Restore();
            _loadedTerrainEdits = loadedTerrainEditsBefore;
            _savedTerrainEditSignature = savedTerrainSignatureBefore;
            if (terrainEditsBefore == null)
            {
                if (File.Exists(terrainEditsPath))
                    File.Delete(terrainEditsPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(terrainEditsPath) ?? _workspace.RootPath);
                File.WriteAllBytes(terrainEditsPath, terrainEditsBefore);
            }
            TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
            snapshot.Restore();
            ApplyCustomTerrainTexturePreviews();
            _viewport.NotifyTerrainPresentationDataChanged();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, terrain));
            EditorDiagnostics.RecordException("reusing a private cross-level terrain texture", ex);
            _statusText.Text =
                $"Could not paint face {terrain.RuntimeKey} with {brush.DisplayLabel}: {ex.Message} " +
                "The face and prior terrain edit file were restored; no terrain changed.";
            return false;
        }
    }

    private static bool IsCleanFaceLocalTextureTarget(TerrainPolygon face, int targetTextureId)
    {
        return face.TextureId == targetTextureId &&
            face.OriginalTextureId >= 0 &&
            face.OriginalTextureId != targetTextureId &&
            face.HasTextureEdit &&
            !face.HasTextureVisualEdit &&
            !face.HasSurfaceBehaviorEdit;
    }

    private static bool IsFaceLocalTextureTargetCompatibleWithRelocation(
        TerrainPolygon face,
        NativeTerrainTextureRelocationEdit relocation)
    {
        if (face.TextureId != relocation.TargetTextureId ||
            face.OriginalTextureId < 0 ||
            face.OriginalTextureId == relocation.TargetTextureId ||
            !face.HasTextureEdit ||
            face.HasTextureVisualEdit)
        {
            return false;
        }

        if (relocation.PreservesTargetNativeSurface)
            return !face.HasSurfaceBehaviorEdit;

        TerrainSurfaceBehaviorEdit? behavior = face.SurfaceBehaviorEdit;
        return behavior == null ||
            (string.Equals(
                 LevelCatalog.NormalizeKey(behavior.SourceLevelKey),
                 LevelCatalog.NormalizeKey(relocation.DonorLevelKey),
                 StringComparison.OrdinalIgnoreCase) &&
             string.Equals(
                 behavior.SourceRuntimeKey,
                 relocation.DonorRuntimeKey,
                 StringComparison.OrdinalIgnoreCase));
    }

    private async Task UndoSelectedTerrainTexturePaintAsync()
    {
        if (_selectedTerrain == null || _currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Select a terrain face before undoing its texture paint.";
            return;
        }

        int textureId = _selectedTerrain.TextureId;
        if (_nativeTerrainTextureRelocations.Any(edit => edit.TargetTextureId == textureId))
        {
            // The existing relocation undo is already scoped to the shared
            // texture record and its transferred native visual/property data.
            // It does not reset face height, XY, or structure edits.
            await UndoSelectedTerrainAsync();
            return;
        }

        TerrainPolygon terrain = _selectedTerrain;
        int previousTextureId = terrain.TextureId;
        bool hadTexturePaint =
            terrain.HasTextureEdit ||
            terrain.HasTextureVisualEdit ||
            terrain.HasSurfaceBehaviorEdit;

        terrain.ApplyTextureOverride(terrain.OriginalTextureId);
        terrain.ClearTextureVisualEdit();
        terrain.ClearSurfaceBehaviorEdit();
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        ApplyCustomTerrainTexturePreviews();
        int savedEdits = await PersistCurrentTerrainEditsAsync();
        _viewport.NotifyTerrainPresentationDataChanged();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        _statusText.Text = hadTexturePaint
            ? $"Undid texture paint {previousTextureId}->{terrain.OriginalTextureId} on face {terrain.RuntimeKey} while preserving its height, XY, and structure edits ({savedEdits} terrain edit(s) remain)."
            : $"Face {terrain.RuntimeKey} had no texture paint to undo; its height, XY, and structure edits were left unchanged.";
    }

    private enum TerrainTexturePaintSourceKind
    {
        SameLevel,
        CrossLevel
    }

    private sealed record TerrainTexturePaintSourceLevelOption(
        LevelDefinition Level,
        bool IsCurrent)
    {
        public override string ToString()
        {
            return IsCurrent ? $"{Level.DisplayName} (Current Level)" : Level.DisplayName;
        }
    }

    private sealed record TerrainTexturePaintGalleryItem(
        string LevelKey,
        int TextureId,
        string TileName,
        ColorRgba Color,
        bool IsBlocked,
        string BlockReason,
        TerrainPaintDialogResult Result)
    {
        public static TerrainTexturePaintGalleryItem FromCurrentLevel(
            LevelDefinition level,
            TerrainTextureSwapChoice choice)
        {
            bool blocked = !choice.CanChooseAsPaintBrushSource;
            return new TerrainTexturePaintGalleryItem(
                level.Key,
                choice.TextureId,
                TerrainMaterialClassifier.FormatSurface(choice.Surface),
                choice.Color,
                blocked,
                blocked
                    ? NonEmptyBlockReason(choice.PaintBrushSourceBlockReason)
                    : "",
                TerrainPaintDialogResult.ForInGameLook(choice));
        }

        public static TerrainTexturePaintGalleryItem FromCrossLevel(
            TerrainCrossLevelLookChoice choice)
        {
            bool blocked = !choice.CanChooseAsPaintBrushSource;
            return new TerrainTexturePaintGalleryItem(
                choice.LevelKey,
                choice.TextureId,
                TerrainMaterialClassifier.FormatSurface(choice.Surface),
                choice.Color,
                blocked,
                blocked
                    ? NonEmptyBlockReason(choice.PaintBrushSourceBlockReason)
                    : "",
                TerrainPaintDialogResult.ForCrossLevelLook(choice));
        }

        private static string NonEmptyBlockReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason)
                ? "This native texture record has not passed the source-safety proof."
                : reason;
        }
    }

    private sealed record TerrainTexturePaintBrush(
        TerrainTexturePaintSourceKind Kind,
        string TargetLevelKey,
        string SourceLevelKey,
        string SourceLevelName,
        int TextureId,
        string RuntimeKey,
        string VisualRuntimeKey,
        TerrainTextureSurfacePropertyMode SurfacePropertyMode,
        ColorRgba PreviewColor,
        string Surface)
    {
        public string DisplayLabel =>
            $"{SourceLevelName} texture {TextureId} ({TerrainMaterialClassifier.FormatSurface(Surface)})";

        public bool Matches(TerrainTextureSwapChoice choice)
        {
            return Kind == TerrainTexturePaintSourceKind.SameLevel &&
                choice.TextureId == TextureId &&
                string.Equals(choice.RuntimeKey, RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(choice.VisualRuntimeKey, VisualRuntimeKey, StringComparison.OrdinalIgnoreCase);
        }

        public bool Matches(TerrainCrossLevelLookChoice choice)
        {
            return Kind == TerrainTexturePaintSourceKind.CrossLevel &&
                choice.TextureId == TextureId &&
                string.Equals(choice.LevelKey, SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(choice.RuntimeKey, RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                choice.SurfacePropertyMode == SurfacePropertyMode;
        }

        public bool Matches(NativeTerrainTextureRelocationEdit edit)
        {
            NativeTerrainTextureRelocationApplyMode expectedMode = SurfacePropertyMode ==
                TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface
                    ? NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget
                    : NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface;
            return Kind == TerrainTexturePaintSourceKind.CrossLevel &&
                edit.DonorTextureId == TextureId &&
                string.Equals(
                    LevelCatalog.NormalizeKey(edit.DonorLevelKey),
                    LevelCatalog.NormalizeKey(SourceLevelKey),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(edit.DonorRuntimeKey, RuntimeKey, StringComparison.Ordinal) &&
                edit.ApplyMode == expectedMode;
        }

        public bool Matches(TerrainTexturePaintGalleryItem item)
        {
            return item.Result.Mode switch
            {
                TerrainPaintModeKind.InGameLook when item.Result.InGameLook != null =>
                    Matches(item.Result.InGameLook),
                TerrainPaintModeKind.CrossLevelLook when item.Result.CrossLevelLook != null =>
                    Matches(item.Result.CrossLevelLook),
                _ => false
            };
        }

        public static TerrainTexturePaintBrush FromSameLevel(
            string targetLevelKey,
            string sourceLevelName,
            TerrainTextureSwapChoice choice)
        {
            return new TerrainTexturePaintBrush(
                TerrainTexturePaintSourceKind.SameLevel,
                targetLevelKey,
                targetLevelKey,
                sourceLevelName,
                choice.TextureId,
                choice.RuntimeKey,
                choice.VisualRuntimeKey,
                TerrainTextureSurfacePropertyMode.TransferDonorNativeSurface,
                choice.Color,
                choice.Surface);
        }

        public static TerrainTexturePaintBrush FromCrossLevel(
            string targetLevelKey,
            TerrainCrossLevelLookChoice choice)
        {
            return new TerrainTexturePaintBrush(
                TerrainTexturePaintSourceKind.CrossLevel,
                targetLevelKey,
                choice.LevelKey,
                choice.LevelName,
                choice.TextureId,
                choice.RuntimeKey,
                "",
                choice.SurfacePropertyMode,
                choice.Color,
                choice.Surface);
        }
    }

    private sealed record TerrainTexturePaintStageSnapshot(
        TerrainPolygon Face,
        int TextureId,
        TerrainTextureVisualEdit? TextureVisualEdit,
        TerrainSurfaceBehaviorEdit? SurfaceBehaviorEdit,
        string Surface,
        ColorRgba SurfaceColor,
        string SurfaceSource,
        string Behavior,
        string BehaviorSource,
        string BehaviorConfidence,
        string BehaviorNote)
    {
        public static TerrainTexturePaintStageSnapshot Capture(TerrainPolygon face)
        {
            return new TerrainTexturePaintStageSnapshot(
                face,
                face.TextureId,
                face.TextureVisualEdit,
                face.SurfaceBehaviorEdit,
                face.Surface,
                face.SurfaceColor,
                face.SurfaceSource,
                face.Behavior,
                face.BehaviorSource,
                face.BehaviorConfidence,
                face.BehaviorNote);
        }

        public void Restore()
        {
            Face.ApplyTextureOverride(TextureId);
            if (TextureVisualEdit == null)
                Face.ClearTextureVisualEdit();
            else
                Face.ApplyTextureVisualEdit(TextureVisualEdit);
            if (SurfaceBehaviorEdit == null)
                Face.ClearSurfaceBehaviorEdit();
            else
                Face.ApplySurfaceBehaviorEdit(SurfaceBehaviorEdit);
            Face.SetSurface(Surface, SurfaceColor, SurfaceSource);
            Face.SetBehavior(Behavior, BehaviorSource, BehaviorConfidence, BehaviorNote);
        }
    }

    private sealed record TerrainTexturePaintFaceAuditSnapshot(
        int TextureId,
        TerrainTextureVisualEdit? TextureVisualEdit,
        TerrainSurfaceBehaviorEdit? SurfaceBehaviorEdit,
        string Surface,
        ColorRgba SurfaceColor,
        string SurfaceSource,
        string Behavior,
        string BehaviorSource,
        string BehaviorConfidence,
        string BehaviorNote,
        IReadOnlyList<Vector2f> Points,
        IReadOnlyList<float> ZValues,
        TerrainStructureEditKind StructureEdit)
    {
        public static TerrainTexturePaintFaceAuditSnapshot Capture(TerrainPolygon face)
        {
            return new TerrainTexturePaintFaceAuditSnapshot(
                face.TextureId,
                face.TextureVisualEdit,
                face.SurfaceBehaviorEdit,
                face.Surface,
                face.SurfaceColor,
                face.SurfaceSource,
                face.Behavior,
                face.BehaviorSource,
                face.BehaviorConfidence,
                face.BehaviorNote,
                face.Points.ToArray(),
                face.ZValues.ToArray(),
                face.StructureEdit);
        }

        public bool Matches(TerrainPolygon face)
        {
            return face.TextureId == TextureId &&
                Equals(face.TextureVisualEdit, TextureVisualEdit) &&
                Equals(face.SurfaceBehaviorEdit, SurfaceBehaviorEdit) &&
                string.Equals(face.Surface, Surface, StringComparison.Ordinal) &&
                face.SurfaceColor == SurfaceColor &&
                string.Equals(face.SurfaceSource, SurfaceSource, StringComparison.Ordinal) &&
                string.Equals(face.Behavior, Behavior, StringComparison.Ordinal) &&
                string.Equals(face.BehaviorSource, BehaviorSource, StringComparison.Ordinal) &&
                string.Equals(face.BehaviorConfidence, BehaviorConfidence, StringComparison.Ordinal) &&
                string.Equals(face.BehaviorNote, BehaviorNote, StringComparison.Ordinal) &&
                face.Points.SequenceEqual(Points) &&
                face.ZValues.SequenceEqual(ZValues) &&
                face.StructureEdit == StructureEdit;
        }
    }
}
