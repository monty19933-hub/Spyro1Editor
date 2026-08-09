using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;
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
    private readonly Dictionary<string, List<TerrainTexturePaintHistoryCheckpoint>> _terrainTexturePaintHistory =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _terrainTexturePaintHistoryRestoreBusy;
    private string _terrainTexturePaintPaletteTargetLevelKey = "";
    private string _terrainTexturePaintPaletteSourceLevelKey = "";
    internal Action<string>? TerrainTextureDonorLoadObserverForTesting { get; set; }
    internal Action<string>? TerrainTexturePaintPersistenceFaultForTesting { get; set; }
    internal Action? AppendedPrivateReplacementCompactionFaultForTesting { get; set; }
    internal Func<string, int, int, bool, string, Task<TerrainTexturePaintScopeChoice>>?
        TerrainTexturePaintScopeConfirmationForTesting { get; set; }

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
        TerrainTexturePaintBrush? preferredBrush,
        TerrainPolygon? suggestionTarget)
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
        Button suggestButton = NewButton("Suggest Nearby Tiles");
        suggestButton.Name = "TerrainTextureSuggestButton";
        suggestButton.IsEnabled = suggestionTarget != null;

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
            })
        };
        ListBox suggestionGallery = new()
        {
            Name = "TerrainTextureSuggestionGallery",
            MinHeight = 168,
            MaxHeight = 190,
            ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid
            {
                Columns = galleryColumns,
                RowSpacing = 10,
                ColumnSpacing = 10
            })
        };
        TextBlock suggestionSummary = NewSmallNote("");
        StackPanel suggestionContent = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Suggested for the selected area",
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
                },
                suggestionSummary,
                suggestionGallery
            }
        };
        Border suggestionPanel = new()
        {
            Name = "TerrainTextureSuggestionPanel",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(246, 250, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(174, 204, 228)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = suggestionContent
        };
        Dictionary<TerrainTexturePaintGalleryItem, string> suggestionReasons =
            new(ReferenceEqualityComparer.Instance);

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
            suggestionPanel.IsVisible = false;
            suggestionGallery.ItemsSource = Array.Empty<TerrainTexturePaintGalleryItem>();
            gallery.MinHeight = 360;
            gallery.MaxHeight = 500;
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
                : $"{sourceName}: {items.Count:N0} texture tile(s), {blocked:N0} blocked. Double-click a usable tile to start painting.",
                error: items.Count == 0);
        }

        bool TryActivateSelectedTextureFromMouseDoubleClick(
            TerrainTexturePaintGalleryItem selected,
            Control tile,
            PointerPressedEventArgs args)
        {
            PointerPoint point = args.GetCurrentPoint(tile);
            if (!IsStrictTerrainTextureGalleryDoubleClick(
                    args.Pointer.Type,
                    args.Pointer.IsPrimary,
                    args.ClickCount,
                    point.Properties.IsLeftButtonPressed))
            {
                return false;
            }

            // Bind activation to the tile that received the physical second
            // left-mouse press. Selection, Enter/Space, touch taps, and routed
            // Tapped/DoubleTapped events may update details, but cannot close
            // this chooser or start paint mode.
            gallery.SelectedItem = selected;
            if (selected.IsBlocked)
            {
                SetMessage($"{selected.TileName} is blocked: {selected.BlockReason}", error: true);
                return false;
            }

            _terrainTexturePaintPaletteSourceLevelKey = LevelCatalog.NormalizeKey(selected.LevelKey);
            dialog.Close(selected.Result);
            return true;
        }

        Control BuildActivatingGalleryTile(TerrainTexturePaintGalleryItem item)
        {
            Control tile = BuildTerrainTexturePaintGalleryTile(item);
            tile.PointerPressed += (_, args) =>
            {
                if (TryActivateSelectedTextureFromMouseDoubleClick(item, tile, args))
                    args.Handled = true;
            };
            return tile;
        }

        gallery.ItemTemplate = new FuncDataTemplate<TerrainTexturePaintGalleryItem>((item, _) =>
            item == null ? new TextBlock() : BuildActivatingGalleryTile(item));
        suggestionGallery.ItemTemplate = new FuncDataTemplate<TerrainTexturePaintGalleryItem>((item, _) =>
            item == null ? new TextBlock() : BuildActivatingGalleryTile(item));
        gallery.SelectionChanged += (_, _) =>
        {
            if (gallery.SelectedItem is not TerrainTexturePaintGalleryItem selected)
                return;
            SetMessage(selected.IsBlocked
                ? $"{selected.TileName} is blocked: {selected.BlockReason}"
                : selected.Result.Mode == TerrainPaintModeKind.CrossLevelLook
                    ? _releaseMode
                        ? $"Selected {selected.TileName}. Double-click to start painting. The editor will offer a single-section or linked-section choice when needed."
                        : $"Selected {selected.TileName}. Double-click to use a private texture slot when one is available; otherwise the editor shows the exact shared texture ID and affected section count before asking permission."
                    : $"Selected {selected.TileName}. Double-click it to start painting.",
                selected.IsBlocked);
        };
        suggestionGallery.SelectionChanged += (_, _) =>
        {
            if (suggestionGallery.SelectedItem is not TerrainTexturePaintGalleryItem selected)
                return;

            gallery.SelectedItem = selected;
            string reason = suggestionReasons.TryGetValue(selected, out string? rankedReason)
                ? $" {rankedReason}"
                : "";
            SetMessage(
                $"Suggested {selected.TileName} (tile {selected.TextureId}).{reason} Double-click it to start painting; one click only selects it.");
        };
        gallery.KeyDown += (_, args) =>
        {
            if (args.Key is not (Key.Enter or Key.Space))
                return;

            args.Handled = true;
            if (gallery.SelectedItem is TerrainTexturePaintGalleryItem selected)
            {
                SetMessage(selected.IsBlocked
                    ? $"{selected.TileName} is blocked: {selected.BlockReason}"
                    : $"Selected {selected.TileName}. Painting starts only after a mouse double-click.",
                    selected.IsBlocked);
            }
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

        suggestButton.Click += (_, _) =>
        {
            if (suggestionTarget == null)
            {
                SetMessage(
                    "Select a terrain section first so suggestions can compare the tiles around it.",
                    error: true);
                return;
            }

            TerrainTexturePaintGalleryItem[] visibleItems =
                (gallery.ItemsSource as IEnumerable<TerrainTexturePaintGalleryItem>)?.ToArray() ?? [];
            TerrainTexturePaintSuggestionSet suggestions =
                BuildTerrainTexturePaintSuggestions(suggestionTarget, visibleItems);
            suggestionReasons.Clear();
            foreach (TerrainTexturePaintGallerySuggestion suggestion in suggestions.Suggestions)
                suggestionReasons[suggestion.Item] = suggestion.Reason;
            suggestionGallery.ItemsSource =
                suggestions.Suggestions.Select(suggestion => suggestion.Item).ToArray();
            suggestionGallery.SelectedIndex = suggestions.Suggestions.Count > 0 ? 0 : -1;
            suggestionPanel.IsVisible = suggestions.Suggestions.Count > 0;
            gallery.MinHeight = suggestions.Suggestions.Count > 0 ? 250 : 360;
            gallery.MaxHeight = suggestions.Suggestions.Count > 0 ? 300 : 500;
            suggestionSummary.Text = suggestions.Suggestions.Count == 0
                ? "No usable alternatives are loaded for this source level."
                : $"{suggestions.Suggestions.Count} best matches from the currently loaded source, using " +
                  $"{suggestions.AdjacentFaceCount} edge-neighbor(s) and " +
                  $"{suggestions.NearbyFaceCount} nearby section(s). Double-click a suggestion to paint.";
            SetMessage(suggestions.Suggestions.Count == 0
                ? "No usable suggested tiles were found in the currently loaded palette."
                : $"Suggested {suggestions.Suggestions.Count} nearby-fitting tile(s). One click selects; only a mouse double-click starts painting.",
                error: suggestions.Suggestions.Count == 0);
        };

        Grid sourceControls = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        sourceControls.Children.Add(sourceLevelBox);
        AddGridControl(sourceControls, loadLevelButton, 1, 0);
        AddGridControl(sourceControls, suggestButton, 2, 0);

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
        if (suggestionTarget == null)
        {
            body.Children.Add(NewSmallNote(
                "Select a terrain section before opening this palette to enable nearby-tile suggestions."));
        }
        body.Children.Add(suggestionPanel);
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
        cancel.Click += (_, _) => dialog.Close(null);
        buttons.Children.Add(cancel);
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

    internal static bool IsStrictTerrainTextureGalleryDoubleClick(
        PointerType pointerType,
        bool isPrimaryPointer,
        int clickCount,
        bool isLeftButtonPressed)
    {
        return pointerType == PointerType.Mouse &&
               isPrimaryPointer &&
               clickCount == 2 &&
               isLeftButtonPressed;
    }

    internal int SelectTerrainTextureSuggestionTargetForTesting()
    {
        if (_currentGeometry == null)
            throw new InvalidOperationException("Load terrain before selecting a suggestion target.");

        TerrainPolygon target = _currentGeometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
            .OrderByDescending(face => _currentGeometry.Polygons.Count(candidate =>
                !ReferenceEquals(candidate, face) &&
                !candidate.IsTerrainRemoved &&
                AreTerrainFacesEdgeAdjacent(face, candidate)))
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The loaded level has no textured suggestion target.");
        _selectedTerrain = target;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(target);
        _selectedTerrainPointIndex = -1;
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(
            _selectedTerrainIndex,
            target));
        return target.TextureId;
    }

    private TerrainTexturePaintSuggestionSet BuildTerrainTexturePaintSuggestions(
        TerrainPolygon selectedFace,
        IReadOnlyList<TerrainTexturePaintGalleryItem> galleryItems)
    {
        if (_currentLevel == null || _currentGeometry == null ||
            selectedFace.IsTerrainRemoved || selectedFace.TextureId < 0)
        {
            return TerrainTexturePaintSuggestionSet.Empty;
        }

        TerrainPolygon[] otherFaces = _currentGeometry.Polygons
            .Where(face =>
                !ReferenceEquals(face, selectedFace) &&
                !face.IsTerrainRemoved &&
                face.TextureId >= 0)
            .ToArray();
        TerrainPolygon[] adjacentFaces = otherFaces
            .Where(face => AreTerrainFacesEdgeAdjacent(selectedFace, face))
            .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        HashSet<string> adjacentRuntimeKeys = adjacentFaces
            .Select(face => face.RuntimeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        TerrainPolygon[] nearestFaces = otherFaces
            .Where(face => !adjacentRuntimeKeys.Contains(face.RuntimeKey))
            .OrderBy(face => TerrainFaceDistanceSquared(selectedFace, face))
            .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, 12 - adjacentFaces.Length))
            .ToArray();

        TerrainTextureSuggestionNeighbor[] neighbors =
        [
            .. adjacentFaces.Select(face => new TerrainTextureSuggestionNeighbor
            {
                Face = BuildTerrainTextureSuggestionFace(face, _currentLevel.Key),
                IsAdjacent = true,
                Distance = 1
            }),
            .. nearestFaces.Select((face, index) => new TerrainTextureSuggestionNeighbor
            {
                Face = BuildTerrainTextureSuggestionFace(face, _currentLevel.Key),
                IsAdjacent = false,
                Distance = Math.Clamp(index + 1, 1, 4)
            })
        ];

        List<(TerrainTextureSuggestionCandidate Candidate, TerrainTexturePaintGalleryItem Item)>
            candidatePairs = [];
        foreach (TerrainTexturePaintGalleryItem item in galleryItems)
        {
            string surface = item.Result.InGameLook?.Surface ??
                item.Result.CrossLevelLook?.Surface ??
                item.TileName;
            string behavior = item.Result.InGameLook?.NativeBehavior?.Label ??
                item.Result.CrossLevelLook?.NativeBehavior?.Label ??
                (item.Result.CrossLevelLook?.PreservesTargetNativeSurface == true
                    ? "preserve target native surface"
                    : "");
            TerrainTextureSuggestionCandidate candidate = new()
            {
                Texture = new TerrainTextureSuggestionTexture
                {
                    LevelKey = item.LevelKey,
                    TextureId = item.TextureId,
                    DisplayName = item.TileName,
                    Surface = surface,
                    PreviewColor = item.Color,
                    PreviewSignature = TerrainTextureSuggestionPreviewSignature(surface),
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["behavior"] = behavior,
                        ["source-kind"] = item.Result.Mode.ToString()
                    }
                },
                IsAvailable = !item.IsBlocked,
                IsBlocked = item.IsBlocked,
                AvailabilityNote = item.BlockReason
            };
            candidatePairs.Add((candidate, item));
        }

        IReadOnlyList<TerrainTextureSuggestion> ranked =
            TerrainTextureSuggestionEngine.Rank(new TerrainTextureSuggestionRequest
            {
                SelectedFace = BuildTerrainTextureSuggestionFace(
                    selectedFace,
                    _currentLevel.Key),
                NearbyFaces = neighbors,
                Candidates = candidatePairs.Select(pair => pair.Candidate).ToArray(),
                MaximumSuggestions = 6
            });
        TerrainTexturePaintGallerySuggestion[] suggestions = ranked
            .Select(result =>
            {
                TerrainTexturePaintGalleryItem item = candidatePairs
                    .First(pair => ReferenceEquals(pair.Candidate, result.Candidate))
                    .Item;
                return new TerrainTexturePaintGallerySuggestion(
                    item,
                    result.Score.Total,
                    string.Join(" ", result.Reasons.Take(2)));
            })
            .ToArray();
        return new TerrainTexturePaintSuggestionSet(
            suggestions,
            adjacentFaces.Length,
            nearestFaces.Length);
    }

    private static TerrainTextureSuggestionFace BuildTerrainTextureSuggestionFace(
        TerrainPolygon face,
        string levelKey)
    {
        string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
        return new TerrainTextureSuggestionFace
        {
            RuntimeKey = face.RuntimeKey,
            Texture = new TerrainTextureSuggestionTexture
            {
                LevelKey = levelKey,
                TextureId = face.TextureId,
                DisplayName = TerrainMaterialClassifier.FormatSurface(surface),
                Surface = surface,
                PreviewColor = face.SurfaceColor,
                PreviewSignature = TerrainTextureSuggestionPreviewSignature(surface),
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["behavior"] = face.Behavior,
                    ["detail"] = face.Detail,
                    ["corners"] = face.Points.Count.ToString(CultureInfo.InvariantCulture)
                }
            }
        };
    }

    private static string TerrainTextureSuggestionPreviewSignature(string surface)
    {
        return TerrainMaterialClassifier.NormalizeSurfaceName(surface);
    }

    private static bool AreTerrainFacesEdgeAdjacent(
        TerrainPolygon first,
        TerrainPolygon second)
    {
        int sharedVertices = 0;
        HashSet<int> matchedSecondVertices = [];
        for (int firstIndex = 0; firstIndex < first.Points.Count; firstIndex++)
        {
            Vector2f firstPoint = first.Points[firstIndex];
            float firstZ = firstIndex < first.ZValues.Length
                ? first.ZValues[firstIndex]
                : first.AvgZ;
            for (int secondIndex = 0; secondIndex < second.Points.Count; secondIndex++)
            {
                if (matchedSecondVertices.Contains(secondIndex))
                    continue;
                Vector2f secondPoint = second.Points[secondIndex];
                float secondZ = secondIndex < second.ZValues.Length
                    ? second.ZValues[secondIndex]
                    : second.AvgZ;
                if (Math.Abs(firstPoint.X - secondPoint.X) > 0.01f ||
                    Math.Abs(firstPoint.Y - secondPoint.Y) > 0.01f ||
                    Math.Abs(firstZ - secondZ) > 0.01f)
                {
                    continue;
                }

                matchedSecondVertices.Add(secondIndex);
                sharedVertices++;
                break;
            }
        }

        return sharedVertices >= 2;
    }

    private static double TerrainFaceDistanceSquared(
        TerrainPolygon first,
        TerrainPolygon second)
    {
        double dx = first.Center.X - second.Center.X;
        double dy = first.Center.Y - second.Center.Y;
        double dz = first.AvgZ - second.AvgZ;
        return (dx * dx) + (dy * dy) + (dz * dz);
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
                requestedSourceLevelKey: sourceLevel.Key,
                artOnlyPreserveTarget: true);
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
        string preservedSurface = reloaded.Surface;
        string preservedBehavior = reloaded.Behavior;
        ColorRgba preservedColor = reloaded.SurfaceColor;
        TerrainTexturePaintBrush artOnlyBrush =
            TerrainTexturePaintBrush.FromSameLevel(
                level.Key,
                level.DisplayName,
                source,
                preserveTargetNativeSurface: true);
        StartTerrainTexturePaintMode(artOnlyBrush);
        await ApplyTerrainTexturePaintBrushAsync(_selectedTerrainIndex, reloaded);
        if (reloaded.TextureId != source.TextureId ||
            reloaded.TextureVisualEdit != null ||
            reloaded.SurfaceBehaviorEdit != null ||
            !string.Equals(reloaded.Surface, preservedSurface, StringComparison.Ordinal) ||
            !string.Equals(reloaded.Behavior, preservedBehavior, StringComparison.Ordinal) ||
            reloaded.SurfaceColor != preservedColor)
        {
            throw new InvalidOperationException(
                "Resident art-only paint did not change exactly one texture id while preserving the destination tint, surface label, and collision behavior.");
        }
        StopTerrainTexturePaintMode(announce: false);
        await UndoSelectedTerrainTexturePaintAsync();
        await SelectLevelAsync(level);
        reloaded = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "The resident art-only target did not reload after texture-only Undo.");
        if (reloaded.TextureId != originalTextureId ||
            reloaded.TextureVisualEdit != null ||
            reloaded.SurfaceBehaviorEdit != null ||
            !string.Equals(reloaded.Surface, preservedSurface, StringComparison.Ordinal) ||
            !string.Equals(reloaded.Behavior, preservedBehavior, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Undo did not restore the resident art-only texture id while retaining the destination surface and behavior.");
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

        return $"no-selection catalog, 104px real preview, injected save-failure rollback, pre-existing tint/property refusal, persistent one-face-only {originalTextureId}->{source.TextureId} full-look apply, resident art-only apply preserving target tint/material/collision, Stop, and texture-only Undo preserving pre-edited height/XY/structure passed on {targetRuntimeKey}";
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
        TerrainTexturePaintBrush? replacementBrush = null;
        foreach (TerrainPolygon candidate in _currentGeometry.Polygons
                     .Where(face =>
                         !face.IsTerrainRemoved &&
                         face.TextureId == 55 &&
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
            TerrainCrossLevelLookChoice[] sources = BuildCrossLevelTerrainLookChoices(
                    candidate.Surface,
                    requestedSourceLevelKey: gnastysWorld.Key,
                    artOnlyPreserveTarget: true)
                .ToArray();
            TerrainCrossLevelLookChoice? source = sources.FirstOrDefault(choice =>
                    choice.TextureId == 17 &&
                    choice.CanApplyAtomically);
            TerrainCrossLevelLookChoice? replacementSource = sources.FirstOrDefault(choice =>
                choice.TextureId == 16 &&
                choice.CanApplyAtomically);
            if (source == null || replacementSource == null)
                continue;

            target = candidate;
            brush = TerrainTexturePaintBrush.FromCrossLevel(artisans.Key, source);
            replacementBrush = TerrainTexturePaintBrush.FromCrossLevel(
                artisans.Key,
                replacementSource);
            break;
        }

        if (target == null || brush == null || replacementBrush == null)
        {
            throw new InvalidOperationException(
                "No clean shared Artisans face exposed Gnasty's World texture 17 as a source-verified paint brush.");
        }

        _selectedTerrain = target;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(target);
        _selectedTerrainPointIndex = -1;
        int auditedCrossLevelDonorCount = 0;
        int expectedCrossLevelDonorCount = 0;
        foreach (LevelDefinition donorLevel in LevelRealmCatalog.OrderLevels(_catalog.Levels)
                     .Where(level => !string.Equals(
                         LevelCatalog.NormalizeKey(level.Key),
                         LevelCatalog.NormalizeKey(artisans.Key),
                         StringComparison.OrdinalIgnoreCase)))
        {
            TerrainTextureSlot[] sourceSlots = TerrainPatchExporter
                .InspectTextureSlots(
                    FirstExistingDiscImagePath(
                        _discImagePathBox.Text,
                        _skyboxDiscImagePathBox.Text,
                        DiscImageLocator.FindImage(_workspace)),
                    donorLevel)
                .OrderBy(slot => slot.TextureId)
                .ToArray();
            TerrainCrossLevelLookChoice[] choices = BuildCrossLevelTerrainLookChoices(
                    target.Surface,
                    requestedSourceLevelKey: donorLevel.Key,
                    artOnlyPreserveTarget: true)
                .OrderBy(choice => choice.TextureId)
                .ToArray();
            int[] missingOrBlocked = sourceSlots
                .Where(slot => !choices.Any(choice =>
                    choice.TextureId == slot.TextureId &&
                    choice.CanChooseAsPaintBrushSource &&
                    choice.CanApplyAtomically))
                .Select(slot => slot.TextureId)
                .ToArray();
            if (choices.Length != sourceSlots.Length || missingOrBlocked.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{donorLevel.DisplayName} exposes {choices.Length}/{sourceSlots.Length} complete native texture records for the stable Artisans texture 55 target; missing/blocked IDs: {string.Join(", ", missingOrBlocked)}.");
            }
            auditedCrossLevelDonorCount += choices.Length;
            expectedCrossLevelDonorCount += sourceSlots.Length;
        }
        if (auditedCrossLevelDonorCount != expectedCrossLevelDonorCount)
        {
            throw new InvalidOperationException(
                $"The cross-level texture palette audited {auditedCrossLevelDonorCount}/{expectedCrossLevelDonorCount} non-Artisans native records.");
        }

        int targetIndex = _currentGeometry.Polygons.IndexOf(target);
        int originalTextureId = target.TextureId;
        if (originalTextureId != 55)
            throw new InvalidOperationException($"The focused cross-level paint smoke selected Artisans texture {originalTextureId}, not the user's exact texture 55 target.");
        int originalSharedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == originalTextureId);
        if (originalSharedFaceCount <= 1)
            throw new InvalidOperationException("The focused cross-level paint target did not reproduce the shared-texture regression.");
        string? originalNormalPreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            80);
        string? originalClosePreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            79.999);
        if (string.IsNullOrWhiteSpace(originalNormalPreviewPath) ||
            string.IsNullOrWhiteSpace(originalClosePreviewPath))
        {
            throw new InvalidOperationException(
                "The focused Artisans texture 55 target did not have both native preview tiers before painting.");
        }

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
        bool deferredSelectedSectionProofWasExplained = false;
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, explanation) =>
        {
            deferredSelectedSectionProofWasExplained =
                textureId == originalTextureId &&
                faceCount == originalSharedFaceCount &&
                canPaintSelected &&
                explanation.Contains("run the exact private-record", StringComparison.OrdinalIgnoreCase) &&
                explanation.Contains("only if you choose", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(TerrainTexturePaintScopeChoice.Cancel);
        };
        StartTerrainTexturePaintMode(brush);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        if (!targetBeforeCanceledConfirmation.Matches(target) ||
            _nativeTerrainTextureRelocations.Count != 0 ||
            !File.ReadAllBytes(terrainEditsPath).SequenceEqual(terrainBeforeCanceledConfirmation) ||
            !deferredSelectedSectionProofWasExplained ||
            !(_statusText.Text ?? "").Contains("Canceled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The fast shared-texture scope choice did not explain its deferred Selected Section proof, or Cancel changed the face, relocation manifest, or prior terrain edit file.");
        }

        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, _) =>
            Task.FromResult(
                textureId == originalTextureId && faceCount == originalSharedFaceCount
                    ? TerrainTexturePaintScopeChoice.AllLinkedSections
                    : TerrainTexturePaintScopeChoice.Cancel);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        NativeTerrainTextureRelocationEdit relocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => brush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"Painting shared Artisans texture {originalTextureId} with Gnasty's World texture 17 did not stage the confirmed shared relocation. Status: {_statusText.Text}");
        string? paintedNormalPreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            80);
        string? paintedClosePreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            79.999);
        string expectedDonorPreviewDirectory = Path.GetFullPath(Path.Combine(
            _workspace.RootPath,
            "editor-cache",
            "terrain-textures",
            LevelCatalog.NormalizeKey(gnastysWorld.Key)));
        static bool IsWithinDirectory(string path, string directory) =>
            Path.GetFullPath(path).StartsWith(
                directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(paintedNormalPreviewPath) ||
            string.IsNullOrWhiteSpace(paintedClosePreviewPath) ||
            string.Equals(paintedNormalPreviewPath, originalNormalPreviewPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(paintedClosePreviewPath, originalClosePreviewPath, StringComparison.OrdinalIgnoreCase) ||
            !IsWithinDirectory(paintedNormalPreviewPath, expectedDonorPreviewDirectory) ||
            !IsWithinDirectory(paintedClosePreviewPath, expectedDonorPreviewDirectory) ||
            string.IsNullOrWhiteSpace(relocation.PreviewImagePath) ||
            !File.Exists(relocation.PreviewImagePath) ||
            !File.Exists(relocationPath))
        {
            throw new InvalidOperationException(
                "The shared relocation was saved, but Artisans texture 55 did not immediately switch both viewport tiers to Gnasty's World texture 17 or retain its staged preview/manifest.");
        }
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

        // Regression: donor art staged on existing-native T39 must not be
        // reused as a selected-only shortcut for a face whose retail material
        // template is T55. Doing so used to preserve the image while silently
        // changing the clicked face's native material contract. The source-safe
        // behavior is an explicit Selected-disabled scope choice; cancel must
        // retain both existing shared records byte-for-byte.
        TerrainPolygon selectedOnlyDonorHost = _currentGeometry.Polygons.First(face =>
            !face.IsTerrainRemoved &&
            face.TextureId == 39 &&
            !face.HasTextureEdit &&
            !face.HasTextureVisualEdit &&
            !face.HasSurfaceBehaviorEdit);
        int selectedOnlyDonorHostIndex = _currentGeometry.Polygons.IndexOf(selectedOnlyDonorHost);
        int selectedOnlyDonorHostFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == 39);
        _selectedTerrain = selectedOnlyDonorHost;
        _selectedTerrainIndex = selectedOnlyDonorHostIndex;
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, _) =>
            Task.FromResult(
                textureId == 39 && faceCount == selectedOnlyDonorHostFaceCount
                    ? TerrainTexturePaintScopeChoice.AllLinkedSections
                    : TerrainTexturePaintScopeChoice.Cancel);
        StartTerrainTexturePaintMode(replacementBrush);
        await ApplyTerrainTexturePaintBrushAsync(selectedOnlyDonorHostIndex, selectedOnlyDonorHost);
        NativeTerrainTextureRelocationEdit selectedOnlyDonorRelocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => edit.TargetTextureId == 39 && replacementBrush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"The second donor could not be staged on texture 39 for the selected-only replacement regression. Status: {_statusText.Text}");

        TerrainTexturePaintFaceAuditSnapshot crossTemplateTargetBefore =
            TerrainTexturePaintFaceAuditSnapshot.Capture(target);
        byte[] crossTemplateManifestBefore = File.ReadAllBytes(relocationPath);
        bool crossTemplateReuseWasNotOffered = false;
        _selectedTerrain = target;
        _selectedTerrainIndex = targetIndex;
        TerrainTexturePaintScopeConfirmationForTesting =
            (_, textureId, faceCount, canPaintSelected, explanation) =>
            {
                crossTemplateReuseWasNotOffered =
                    textureId == originalTextureId &&
                    faceCount == originalSharedFaceCount &&
                    canPaintSelected &&
                    explanation.Contains("run the exact private-record", StringComparison.OrdinalIgnoreCase) &&
                    !explanation.Contains(
                        "already loaded in texture record 39",
                        StringComparison.OrdinalIgnoreCase);
                return Task.FromResult(TerrainTexturePaintScopeChoice.Cancel);
            };
        StartTerrainTexturePaintMode(replacementBrush);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        NativeTerrainTextureRelocationEdit[] selectedOnlySavedRelocations =
            NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, artisans.Key).ToArray();
        if (!crossTemplateReuseWasNotOffered ||
            !crossTemplateTargetBefore.Matches(target) ||
            _nativeTerrainTextureRelocations.Count != 2 ||
            selectedOnlySavedRelocations.Length != 2 ||
            !File.ReadAllBytes(relocationPath).SequenceEqual(crossTemplateManifestBefore) ||
            !_nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == originalTextureId && brush.Matches(edit)) ||
            !selectedOnlySavedRelocations.Any(edit =>
                edit.TargetTextureId == originalTextureId && brush.Matches(edit)) ||
            !File.Exists(relocation.PreviewImagePath) ||
            !(_statusText.Text ?? "").Contains("previous", StringComparison.OrdinalIgnoreCase) ||
            !(_statusText.Text ?? "").Contains("remains staged", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A cross-template selected-only shortcut was offered, or canceling its explicit shared fallback changed the face, manifest, old relocation, or preview.");
        }
        if (!await RemoveStagedTerrainTextureAsync(selectedOnlyDonorRelocation) ||
            _nativeTerrainTextureRelocations.Count != 1 ||
            !_nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == originalTextureId && brush.Matches(edit)))
        {
            throw new InvalidOperationException(
                "The cross-template guard regression could not remove its temporary T39 donor without disturbing the original T55 shared record.");
        }
        _selectedTerrain = target;
        _selectedTerrainIndex = targetIndex;
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, _) =>
            Task.FromResult(
                textureId == originalTextureId && faceCount == originalSharedFaceCount
                    ? TerrainTexturePaintScopeChoice.AllLinkedSections
                    : TerrainTexturePaintScopeChoice.Cancel);

        string firstDonorPreviewPath = relocation.PreviewImagePath;
        StartTerrainTexturePaintMode(replacementBrush);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        NativeTerrainTextureRelocationEdit replacedRelocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => replacementBrush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"Painting a second donor directly over shared texture {originalTextureId} did not replace the prior donor atomically. Status: {_statusText.Text}");
        if (_nativeTerrainTextureRelocations.Count != 1 ||
            File.Exists(firstDonorPreviewPath) ||
            !(_statusText.Text ?? "").Contains("Replaced the previous", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Painting on top of an existing shared texture did not keep exactly one relocation, remove the superseded managed preview, or report the replacement.");
        }
        StartTerrainTexturePaintMode(brush);
        await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
        relocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => brush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"Replacing Artisans texture {originalTextureId} back to the original test donor failed. Status: {_statusText.Text}");

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
        string? reloadedNormalPreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            80);
        string? reloadedClosePreviewPath = _viewport.ResolveTerrainTextureImagePathForTesting(
            originalTextureId,
            79.999);
        if (string.IsNullOrWhiteSpace(reloadedNormalPreviewPath) ||
            string.IsNullOrWhiteSpace(reloadedClosePreviewPath) ||
            !IsWithinDirectory(reloadedNormalPreviewPath, expectedDonorPreviewDirectory) ||
            !IsWithinDirectory(reloadedClosePreviewPath, expectedDonorPreviewDirectory))
        {
            throw new InvalidOperationException(
                "The Gnasty's World texture 17 preview mapping did not survive the Artisans texture 55 save/reload round trip.");
        }

        TerrainPolygon reuseTarget = _currentGeometry.Polygons
            .Where(face =>
                !face.IsTerrainRemoved &&
                face.TextureId >= 0 &&
                face.TextureId != originalTextureId &&
                _currentGeometry.Polygons.Count(other =>
                    !other.IsTerrainRemoved &&
                    other.TextureId == face.TextureId) > 1 &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit)
            .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .First();
        int reuseTargetOriginalTextureId = reuseTarget.TextureId;
        TerrainTexturePaintFaceAuditSnapshot reuseTargetBefore =
            TerrainTexturePaintFaceAuditSnapshot.Capture(reuseTarget);
        byte[] reuseManifestBefore = File.ReadAllBytes(relocationPath);
        _selectedTerrain = reuseTarget;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(reuseTarget);
        bool loadedSharedRecordWasRejectedAcrossTemplates = false;
        TerrainTexturePaintScopeConfirmationForTesting = (_, _, _, canPaintSelected, explanation) =>
        {
            loadedSharedRecordWasRejectedAcrossTemplates =
                canPaintSelected &&
                explanation.Contains("run the exact private-record", StringComparison.OrdinalIgnoreCase) &&
                !explanation.Contains(
                    $"already loaded in texture record {originalTextureId}",
                    StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(TerrainTexturePaintScopeChoice.Cancel);
        };
        StartTerrainTexturePaintMode(brush);
        await ApplyTerrainTexturePaintBrushAsync(_selectedTerrainIndex, reuseTarget);
        if (!loadedSharedRecordWasRejectedAcrossTemplates ||
            !reuseTargetBefore.Matches(reuseTarget) ||
            !File.ReadAllBytes(relocationPath).SequenceEqual(reuseManifestBefore) ||
            _nativeTerrainTextureRelocations.Count != 1 ||
            !_nativeTerrainTextureRelocations.Any(edit => brush.Matches(edit)))
        {
            throw new InvalidOperationException(
                $"Reloaded shared donor T{originalTextureId} bypassed the T{reuseTargetOriginalTextureId} material contract or changed state after cancel.");
        }

        target = _currentGeometry.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase));
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
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, _) =>
            Task.FromResult(
                textureId == originalTextureId &&
                faceCount == originalSharedFaceCount
                    ? TerrainTexturePaintScopeChoice.AllLinkedSections
                    : TerrainTexturePaintScopeChoice.Cancel);
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

        TerrainPolygon overlapTarget = _currentGeometry?.Polygons.FirstOrDefault(face =>
                !face.IsTerrainRemoved &&
                face.TextureId == 39 &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit)
            ?? throw new InvalidOperationException("Artisans texture 39 is unavailable for the overlap-closure paint regression.");
        _selectedTerrain = overlapTarget;
        _selectedTerrainIndex = _currentGeometry!.Polygons.IndexOf(overlapTarget);
        _selectedTerrainPointIndex = -1;
        TerrainCrossLevelLookChoice overlapChoice = BuildCrossLevelTerrainLookChoices(
                overlapTarget.Surface,
                requestedSourceLevelKey: gnastysWorld.Key,
                artOnlyPreserveTarget: true)
            .Single(choice => choice.TextureId == 17 && choice.CanApplyAtomically);
        TerrainTexturePaintBrush overlapBrush = TerrainTexturePaintBrush.FromCrossLevel(
            artisans.Key,
            overlapChoice);
        string? overlapPreviewBefore = _viewport.ResolveTerrainTextureImagePathForTesting(39, 80);
        int overlapAffectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == 39);
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, _) =>
            Task.FromResult(
                textureId == 39 && faceCount == overlapAffectedFaceCount
                    ? TerrainTexturePaintScopeChoice.AllLinkedSections
                    : TerrainTexturePaintScopeChoice.Cancel);
        StartTerrainTexturePaintMode(overlapBrush);
        await ApplyTerrainTexturePaintBrushAsync(_selectedTerrainIndex, overlapTarget);
        NativeTerrainTextureRelocationEdit overlapRelocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => edit.TargetTextureId == 39 && overlapBrush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"Artisans texture 39 <- Gnasty's World texture 17 did not stage through its overlap closure. Status: {_statusText.Text}");
        string? overlapPreviewAfter = _viewport.ResolveTerrainTextureImagePathForTesting(39, 80);
        if (string.IsNullOrWhiteSpace(overlapPreviewAfter) ||
            string.Equals(overlapPreviewBefore, overlapPreviewAfter, StringComparison.OrdinalIgnoreCase) ||
            !IsWithinDirectory(overlapPreviewAfter, expectedDonorPreviewDirectory) ||
            !(_statusText.Text ?? "").Contains("overlapping record(s) 35", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(overlapRelocation.PreviewImagePath))
        {
            throw new InvalidOperationException(
                "Artisans texture 39 staged, but its viewport preview or atomic preservation proof for overlapping texture 35 was missing.");
        }
        StopTerrainTexturePaintMode(announce: false);
        string overlapRuntimeKey = overlapTarget.RuntimeKey;
        overlapTarget.ApplyTerrainDeltaZ(17);
        overlapTarget.ApplyTerrainTranslation(5, -3);
        overlapTarget.StageTerrainAddClone();
        float[] overlapEditedZ = overlapTarget.ZValues.ToArray();
        Vector2f[] overlapEditedPoints = overlapTarget.Points.ToArray();
        TerrainStructureEditKind overlapEditedStructure = overlapTarget.StructureEdit;
        await PersistCurrentTerrainEditsAsync();
        if (!await UndoLastTerrainTextureActionAsync() ||
            _nativeTerrainTextureRelocations.Count != 0)
        {
            throw new InvalidOperationException(
                "Undo Last did not remove the most recently staged overlap texture after later geometry edits.");
        }
        await SelectLevelAsync(artisans);
        overlapTarget = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, overlapRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The overlap target did not reload after Undo Last.");
        if (!overlapTarget.ZValues.SequenceEqual(overlapEditedZ) ||
            !overlapTarget.Points.SequenceEqual(overlapEditedPoints) ||
            overlapTarget.StructureEdit != overlapEditedStructure)
        {
            throw new InvalidOperationException(
                "Undo Last restored an older terrain-edits file and lost height, XY, or structure work made after texture staging.");
        }
        _selectedTerrain = overlapTarget;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(overlapTarget);
        StartTerrainTexturePaintMode(overlapBrush);
        await ApplyTerrainTexturePaintBrushAsync(_selectedTerrainIndex, overlapTarget);
        overlapRelocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => edit.TargetTextureId == 39 && overlapBrush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"The overlap texture could not be restaged for Manage Staged Textures. Status: {_statusText.Text}");
        StopTerrainTexturePaintMode(announce: false);
        if (!await RemoveStagedTerrainTextureAsync(overlapRelocation) ||
            _nativeTerrainTextureRelocations.Count != 0 ||
            File.Exists(overlapRelocation.PreviewImagePath))
        {
            throw new InvalidOperationException(
                "Manage Staged Textures did not transactionally remove the overlap relocation and its managed preview.");
        }
        if (!await UndoLastTerrainTextureActionAsync() ||
            !_nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == 39 && overlapBrush.Matches(edit)) ||
            !File.Exists(overlapRelocation.PreviewImagePath))
        {
            throw new InvalidOperationException(
                "Undo Last did not restore the managed overlap relocation manifest and preview.");
        }
        overlapTarget = _currentGeometry.Polygons.First(face =>
            !face.IsTerrainRemoved && face.TextureId == 39);
        _selectedTerrain = overlapTarget;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(overlapTarget);
        await UndoSelectedTerrainTexturePaintAsync();
        await UndoSelectedTerrainAsync();
        await SelectLevelAsync(artisans);
        TerrainPolygon overlapCleaned = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, overlapRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The overlap target did not reload after cleanup.");
        if (NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, artisans.Key).Count != 0 ||
            overlapCleaned.IsTerrainEdited)
        {
            throw new InvalidOperationException(
                "The Artisans texture 39 overlap-closure smoke left a relocation or geometry edit after cleanup.");
        }

        LevelDefinition peaceKeepers = _catalog.FindByKey("peacekeepers")
            ?? throw new InvalidOperationException("Peace Keepers is missing from the level catalog.");
        await SelectLevelAsync(peaceKeepers);
        if (_currentGeometry == null)
            throw new InvalidOperationException("Peace Keepers did not load for the private-section scope smoke.");
        string peaceKeepersTerrainEditsPath = Path.Combine(
            _workspace.RootPath,
            $"{peaceKeepers.Key}-terrain-edits.json");
        string peaceKeepersRelocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            peaceKeepers.Key);
        if (File.Exists(peaceKeepersTerrainEditsPath) || File.Exists(peaceKeepersRelocationPath))
        {
            throw new InvalidOperationException(
                "The private-section scope smoke requires isolated Peace Keepers terrain and relocation files.");
        }
        RefreshTerrainPrivateTextureCapacityUi();
        string peaceKeepersCapacityBefore =
            _terrainPrivateTextureCapacityText.Text ?? "";
        if (!peaceKeepersCapacityBefore.Contains(
                "1 of 1 single-tile cross-level slot(s) remain",
                StringComparison.OrdinalIgnoreCase) ||
            !peaceKeepersCapacityBefore.Contains(
                "1 of 1 proven native slot(s) remain",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The release capacity panel did not advertise Peace Keepers' proven native T11 slot: {peaceKeepersCapacityBefore}");
        }

        TerrainPolygon? privateScopeTarget = null;
        TerrainTexturePaintBrush? privateScopeBrush = null;
        foreach (TerrainPolygon candidate in _currentGeometry.Polygons
                     .Where(face =>
                         !face.IsTerrainRemoved &&
                         face.TextureId >= 0 &&
                         !face.HasTextureEdit &&
                         !face.HasTextureVisualEdit &&
                         !face.HasSurfaceBehaviorEdit &&
                         _currentGeometry.Polygons.Count(other =>
                             !other.IsTerrainRemoved && other.TextureId == face.TextureId) > 1)
                     .OrderBy(face => string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(face => face.SectorIndex)
                     .ThenBy(face => face.FaceIndex))
        {
            _selectedTerrain = candidate;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(candidate);
            _selectedTerrainPointIndex = -1;
            TerrainCrossLevelLookChoice? donorChoice = BuildCrossLevelTerrainLookChoices(
                    candidate.Surface,
                    requestedSourceLevelKey: gnastysWorld.Key,
                    artOnlyPreserveTarget: true)
                .FirstOrDefault(choice => choice.TextureId == 17 && choice.CanApplyAtomically);
            if (donorChoice == null)
                continue;
            privateScopeTarget = candidate;
            privateScopeBrush = TerrainTexturePaintBrush.FromCrossLevel(
                peaceKeepers.Key,
                donorChoice);
            break;
        }
        if (privateScopeTarget == null || privateScopeBrush == null)
        {
            throw new InvalidOperationException(
                "Peace Keepers exposed no clean shared target for the private-section texture scope smoke.");
        }

        int privateScopeTargetIndex = _currentGeometry.Polygons.IndexOf(privateScopeTarget);
        int privateScopeOriginalTextureId = privateScopeTarget.TextureId;
        string privateScopeRuntimeKey = privateScopeTarget.RuntimeKey;
        int privateScopeLinkedCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == privateScopeOriginalTextureId);
        bool selectedSectionWasOffered = false;
        TerrainTexturePaintScopeConfirmationForTesting = (_, textureId, faceCount, canPaintSelected, explanation) =>
        {
            selectedSectionWasOffered =
                textureId == privateScopeOriginalTextureId &&
                faceCount == privateScopeLinkedCount &&
                canPaintSelected &&
                explanation.Contains("runtime-stable texture record 11", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(selectedSectionWasOffered
                ? TerrainTexturePaintScopeChoice.SelectedSection
                : TerrainTexturePaintScopeChoice.Cancel);
        };
        StartTerrainTexturePaintMode(privateScopeBrush);
        await ApplyTerrainTexturePaintBrushAsync(privateScopeTargetIndex, privateScopeTarget);
        NativeTerrainTextureRelocationEdit privateScopeRelocation = _nativeTerrainTextureRelocations
            .SingleOrDefault(edit => privateScopeBrush.Matches(edit))
            ?? throw new InvalidOperationException(
                $"The private-section scope did not stage in Peace Keepers. Status: {_statusText.Text}");
        if (!selectedSectionWasOffered ||
            privateScopeRelocation.TargetTextureId != 11 ||
            privateScopeTarget.TextureId != 11 ||
            !privateScopeTarget.HasTextureEdit ||
            _currentGeometry.Polygons.Any(face =>
                !ReferenceEquals(face, privateScopeTarget) &&
                face.OriginalTextureId == privateScopeOriginalTextureId &&
                face.TextureId != privateScopeOriginalTextureId))
        {
            throw new InvalidOperationException(
                "Selected Section was not offered through Peace Keepers' proven private record 11, or it changed a linked neighbor.");
        }
        RefreshTerrainPrivateTextureCapacityUi();
        string peaceKeepersCapacityAfter =
            _terrainPrivateTextureCapacityText.Text ?? "";
        if (!peaceKeepersCapacityAfter.Contains(
                "0 of 1 single-tile cross-level slot(s) remain",
                StringComparison.OrdinalIgnoreCase) ||
            !peaceKeepersCapacityAfter.Contains(
                "0 of 1 proven native slot(s) remain",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The release capacity panel did not consume only Peace Keepers' staged native T11 slot: {peaceKeepersCapacityAfter}");
        }

        await SelectLevelAsync(peaceKeepers);
        TerrainPolygon privateScopeReloaded = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, privateScopeRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The private-section texture target did not reload.");
        if (privateScopeReloaded.TextureId != 11 ||
            !_nativeTerrainTextureRelocations.Any(edit =>
                edit.TargetTextureId == 11 && privateScopeBrush.Matches(edit)))
        {
            throw new InvalidOperationException(
                "The selected-section private texture did not survive save/reload.");
        }
        _selectedTerrain = privateScopeReloaded;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(privateScopeReloaded);
        await UndoSelectedTerrainTexturePaintAsync();
        await SelectLevelAsync(peaceKeepers);
        TerrainPolygon privateScopeCleaned = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, privateScopeRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The private-section texture target did not reload after Undo.");
        if (privateScopeCleaned.TextureId != privateScopeOriginalTextureId ||
            NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, peaceKeepers.Key).Count != 0)
        {
            throw new InvalidOperationException(
                "Undo did not restore the selected Peace Keepers section or release private texture record 11.");
        }
        RefreshTerrainPrivateTextureCapacityUi();
        string peaceKeepersCapacityRestored =
            _terrainPrivateTextureCapacityText.Text ?? "";
        if (!peaceKeepersCapacityRestored.Contains(
                "1 of 1 single-tile cross-level slot(s) remain",
                StringComparison.OrdinalIgnoreCase) ||
            !peaceKeepersCapacityRestored.Contains(
                "1 of 1 proven native slot(s) remain",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Undo did not restore Peace Keepers' native-slot capacity in the release panel: {peaceKeepersCapacityRestored}");
        }

        LevelDefinition beastMakers = _catalog.FindByKey("beastmakers")
            ?? throw new InvalidOperationException("Beast Makers is missing from the level catalog.");
        await SelectLevelAsync(beastMakers);
        RefreshTerrainPrivateTextureCapacityUi();
        string beastMakersCapacity =
            _terrainPrivateTextureCapacityText.Text ?? "";
        if (!beastMakersCapacity.Contains(
                "17 of 17 single-tile cross-level slot(s) remain",
                StringComparison.OrdinalIgnoreCase) ||
            !beastMakersCapacity.Contains(
                "17 of 17 proven native slot(s) remain",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The release capacity panel did not derive Beast Makers' 17 stable private slots from the native catalog: {beastMakersCapacity}");
        }

        // Appended-row undo identity regression: removing T68 compacts the
        // unrelated T69 donor into T68. The new donor must not be mistaken for
        // the removed row merely because it inherited the same numeric id.
        await SelectLevelAsync(artisans);
        if (_currentGeometry == null)
            throw new InvalidOperationException("Artisans did not reload for appended-row undo compaction.");
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        NativeTerrainTextureRecordAppendSourceBinding appendBinding =
            NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImage, artisans);
        TerrainPolygon[] compactionFaces = _currentGeometry.Polygons
            .Where(face =>
                !face.IsTerrainRemoved &&
                face.OriginalTextureId >= 0 &&
                face.OriginalTextureId < appendBinding.ExpectedSourceTextureCount &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit)
            .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (compactionFaces.Length != 2)
            throw new InvalidOperationException("Artisans exposed fewer than two clean faces for appended-row undo compaction.");
        TerrainPolygon removedPrivateFace = compactionFaces[0];
        TerrainPolygon retainedPrivateFace = compactionFaces[1];
        int removedOriginalTextureId = removedPrivateFace.OriginalTextureId;
        int retainedOriginalTextureId = retainedPrivateFace.OriginalTextureId;
        IReadOnlyList<NativeTerrainTextureRelocationEdit> appendedRows =
            await NativeTerrainTextureRelocationEditStore.AddOrReplaceAppendedPrivateArtOnlyAsync(
                _workspace.RootPath,
                artisans.Key,
                artisans.DisplayName,
                appendBinding.ExpectedSourceTextureCount,
                gnastysWorld.Key,
                gnastysWorld.DisplayName,
                gnastysWorld.SourceWadEntry,
                17,
                appendBinding.TargetWadEntry,
                appendBinding.SourceImageSha256,
                appendBinding.ExpectedSourceTextureCount,
                appendBinding.ExpectedTextureComponentSha256,
                appendBinding.ExpectedLevelDataSha256,
                removedOriginalTextureId);
        appendedRows = await NativeTerrainTextureRelocationEditStore.AddOrReplaceAppendedPrivateArtOnlyAsync(
            _workspace.RootPath,
            artisans.Key,
            artisans.DisplayName,
            appendBinding.ExpectedSourceTextureCount + 1,
            gnastysWorld.Key,
            gnastysWorld.DisplayName,
            gnastysWorld.SourceWadEntry,
            16,
            appendBinding.TargetWadEntry,
            appendBinding.SourceImageSha256,
            appendBinding.ExpectedSourceTextureCount,
            appendBinding.ExpectedTextureComponentSha256,
            appendBinding.ExpectedLevelDataSha256,
            retainedOriginalTextureId);
        _nativeTerrainTextureRelocations = appendedRows;
        NativeTerrainTextureRelocationEdit removedPrivateRow = appendedRows.Single(edit =>
            edit.TargetTextureId == appendBinding.ExpectedSourceTextureCount);
        removedPrivateFace.ApplyTextureOverride(appendBinding.ExpectedSourceTextureCount);
        retainedPrivateFace.ApplyTextureOverride(appendBinding.ExpectedSourceTextureCount + 1);
        await PersistCurrentTerrainEditsAsync();
        _selectedTerrain = removedPrivateFace;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(removedPrivateFace);
        _lastTerrainTexturePaintFace = removedPrivateFace.RuntimeKey;
        await UndoSelectedTerrainTexturePaintAsync();

        NativeTerrainTextureRelocationEdit compactedPrivateRow = _nativeTerrainTextureRelocations.Single();
        string compactionStatus = _statusText.Text ?? "";
        if (compactedPrivateRow.TargetTextureId != appendBinding.ExpectedSourceTextureCount ||
            compactedPrivateRow.DonorTextureId != 16 ||
            RepresentsSameStagedTerrainTexture(compactedPrivateRow, removedPrivateRow) ||
            removedPrivateFace.TextureId != removedOriginalTextureId ||
            retainedPrivateFace.TextureId != appendBinding.ExpectedSourceTextureCount ||
            !string.IsNullOrWhiteSpace(_lastTerrainTexturePaintFace) ||
            !compactionStatus.Contains("1 other cross-level texture paint", StringComparison.OrdinalIgnoreCase) ||
            !compactionStatus.Contains(
                $"texture record(s) {appendBinding.ExpectedSourceTextureCount}",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Middle-row undo confused removed T{appendBinding.ExpectedSourceTextureCount} with the unrelated donor compacted into the same id. Status: {compactionStatus}");
        }
        _selectedTerrain = retainedPrivateFace;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(retainedPrivateFace);
        await UndoSelectedTerrainTexturePaintAsync();
        await SelectLevelAsync(artisans);
        TerrainPolygon removedPrivateReloaded = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, removedPrivateFace.RuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The removed private face did not reload after compaction cleanup.");
        TerrainPolygon retainedPrivateReloaded = _currentGeometry.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, retainedPrivateFace.RuntimeKey, StringComparison.OrdinalIgnoreCase));
        if (removedPrivateReloaded.TextureId != removedOriginalTextureId ||
            retainedPrivateReloaded.TextureId != retainedOriginalTextureId ||
            NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, artisans.Key).Count != 0)
        {
            throw new InvalidOperationException("The appended-row compaction smoke did not clean up its two private face assignments.");
        }

        TerrainTexturePaintScopeConfirmationForTesting = null;
        return $"all {auditedCrossLevelDonorCount:N0} non-Artisans native records were selectable for stable target texture 55; Artisans shared texture {originalTextureId} ({originalSharedFaceCount} faces) opened the fast deferred-proof scope immediately, Cancel stayed non-mutating, All Linked Sections skipped private planning and staged Gnasty's World texture 17, and painting another donor on top atomically replaced it; an already-loaded shared donor could not bypass a clicked face's different retail material template before or after reload, and cancel retained both shared records; Peace Keepers offered Selected Section through private record 11 without changing its {privateScopeLinkedCount - 1:N0} linked neighbor(s), including save/reload and Undo; appended T68 removal compacted unrelated T69->T68 while preserving donor identity, both face assignments, status, and last-target state; overlapping Artisans texture 39 staged while atomically preserving texture 35, Manage Staged Textures removed it, and Undo Last restored its manifest and preview; unrelated texture IDs stayed unchanged, preview/save/reload, geometry preservation, and injected-failure rollback passed";
    }

    internal async Task<string> AssertAppendedPrivateTerrainTextureGateForTestingAsync()
    {
        string? previousGate = Environment.GetEnvironmentVariable(
            AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable);
        TerrainTexturePaintScopeConfirmationForTesting = null;
        try
        {
            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                null);
            if (AppendedPrivateTerrainTextureResearchGate.IsEnabled ||
                AppendedPrivateTerrainTextureResearchGate.TryAllow(
                    "artisans",
                    out string disabledReason) ||
                !disabledReason.Contains(
                    AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The appended-private texture gate did not default OFF with its exact opt-in switch in the reason.");
            }

            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                "1");
            LevelDefinition[] allowedLevels = _catalog.Levels
                .Where(level => AppendedPrivateTerrainTextureResearchGate.TryAllow(
                    level.Key,
                    out _))
                .ToArray();
            if (allowedLevels.Length != 32)
            {
                throw new InvalidOperationException(
                    $"The strict private-texture gate allowed {allowedLevels.Length}/35 destinations instead of 32/35.");
            }
            string[] expectedBlocked = ["loftycastle", "jacques", "gnorccove"];
            foreach (string blockedKey in expectedBlocked)
            {
                if (AppendedPrivateTerrainTextureResearchGate.TryAllow(
                        blockedKey,
                        out string reason) ||
                    !reason.Contains("alias-preserving packing proof", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{blockedKey} did not remain explicitly blocked by the strict alias-preserving packing proof.");
                }
            }
            if (AppendedPrivateTerrainTextureResearchGate.TryAllow(
                    "future-secret-level",
                    out string unknownReason) ||
                !unknownReason.Contains("not one of the exact 32", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An unknown/future level key escaped the literal 32-destination allow set.");
            }

            PrivateTextureAggregateSavedWriterScope[] textureOnlyGrowingTargets =
            [
                new("Stone Hill", true, false, true, false, true, false, false, false),
                new("Dark Hollow", true, false, true, false, true, false, false, false)
            ];
            if (FindGrowingPrivateTextureAggregateWriterBlocker(
                    2,
                    0x1000,
                    textureOnlyGrowingTargets) != null)
            {
                throw new InvalidOperationException(
                    "The aggregate +0x1000 texture-only guard rejected ordinary terrain/native texture edits already folded into its two private destinations.");
            }
            string? objectWriterBlock = FindGrowingPrivateTextureAggregateWriterBlocker(
                1,
                0x800,
                [new("Artisans", false, true, false, false, false, false, false, false)]);
            if (string.IsNullOrWhiteSpace(objectWriterBlock) ||
                !objectWriterBlock.Contains("Artisans", StringComparison.Ordinal) ||
                !objectWriterBlock.Contains("object/path/chest edits", StringComparison.Ordinal) ||
                !objectWriterBlock.Contains("+0x800", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A growing aggregate did not fail closed before an unrelated object/path/chest writer could use stale retail WAD offsets.");
            }
            if (FindGrowingPrivateTextureAggregateWriterBlocker(
                    0,
                    0,
                    [new("Artisans", false, true, true, true, false, true, true, true)]) != null)
            {
                throw new InvalidOperationException(
                    "The fixed-tail-only multi-level path incorrectly rejected unrelated saved writers even though no WAD offsets moved.");
            }

            string sourceImage = FirstExistingDiscImagePath(
                _discImagePathBox.Text,
                _skyboxDiscImagePathBox.Text,
                DiscImageLocator.FindImage(_workspace));
            string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
            string wadAnalysisPath = await EnsureSkyboxWadAnalysisAsync(sourceImage);
            LevelDefinition treeTops = _catalog.FindByKey("treetops")
                ?? throw new InvalidOperationException("Tree Tops is missing from the level catalog.");
            LevelDefinition darkHollow = _catalog.FindByKey("darkhollow")
                ?? throw new InvalidOperationException("Dark Hollow is missing from the level catalog.");
            await SelectLevelAsync(treeTops);
            NativeTerrainTextureRecordAppendSourceBinding treeBinding =
                NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
                    sourceImage,
                    treeTops);
            NativeTerrainTextureAppendedPrivateAllocation oversizedTreePair =
                PlanAppendedPrivateTerrainTextureAllocation(
                    treeBinding,
                    darkHollow,
                    donorTextureId: 0,
                    materialTemplateTextureId: 12);
            bool oversizedTreePairRejected = false;
            string oversizedTreePairReason = "";
            try
            {
                NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
                    new NativeTerrainTexturePrivateRecordBatchRequest(
                        sourceImage,
                        sourceCue,
                        Path.Combine(
                            _workspace.RootPath,
                            "_local",
                            "research",
                            "appended-private-ui-gate-smoke",
                            "treetops-darkhollow-T0-must-not-export"),
                        wadAnalysisPath,
                        treeTops,
                        oversizedTreePair.Edits,
                        OrdinaryLevelDataPatches: []));
            }
            catch (InvalidOperationException ex)
            {
                oversizedTreePairRejected = true;
                oversizedTreePairReason = ex.Message;
            }
            if (!oversizedTreePairRejected ||
                string.IsNullOrWhiteSpace(oversizedTreePairReason) ||
                File.Exists(NativeTerrainTextureRelocationEditStore.ManifestPath(
                    _workspace.RootPath,
                    treeTops.Key)) ||
                File.Exists(Path.Combine(
                    _workspace.RootPath,
                    $"{treeTops.Key}-terrain-edits.json")))
            {
                throw new InvalidOperationException(
                    "The exact Tree Tops <- Dark Hollow T0 oversized-pair preflight did not reject before a manifest/face edit was created.");
            }

            bool blockedDestinationFallbackWasExercised = false;
            foreach (string blockedKey in expectedBlocked)
            {
                LevelDefinition blockedLevel = _catalog.FindByKey(blockedKey)
                    ?? throw new InvalidOperationException(
                        $"The blocked destination {blockedKey} is missing from the level catalog.");
                await SelectLevelAsync(blockedLevel);
                if (_currentGeometry == null)
                    continue;

                IReadOnlyList<TerrainTextureSlot> blockedSlots =
                    TerrainPatchExporter.InspectTextureSlots(sourceImage, blockedLevel);
                HashSet<int> blockedUsedTextureIds = _currentGeometry.Polygons
                    .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
                    .Select(face => face.TextureId)
                    .ToHashSet();
                TerrainTextureCatalog blockedCatalog = BuildNativeTerrainTextureCatalog(
                    blockedLevel,
                    _currentGeometry,
                    out _);
                Dictionary<int, TerrainTextureCatalogEntry> blockedEntries = blockedCatalog.Entries
                    .ToDictionary(entry => entry.TextureId);
                bool hasExistingPrivateSlot = blockedSlots.Any(slot =>
                    !blockedUsedTextureIds.Contains(slot.TextureId) &&
                    slot.HasNormalDescriptors &&
                    slot.HasCloseDescriptors &&
                    blockedEntries.TryGetValue(slot.TextureId, out TerrainTextureCatalogEntry? entry) &&
                    entry.Readiness.RecordRole == TerrainTextureRecordRole.NativeUnreferencedStatic &&
                    entry.Readiness.TargetRuntime.CanPersist);
                if (hasExistingPrivateSlot)
                    continue;

                TerrainPolygon? blockedTarget = null;
                TerrainTexturePaintBrush? blockedBrush = null;
                foreach (TerrainPolygon candidate in _currentGeometry.Polygons
                             .Where(face =>
                                 !face.IsTerrainRemoved &&
                                 face.TextureId >= 0 &&
                                 _currentGeometry.Polygons.Count(other =>
                                     !other.IsTerrainRemoved &&
                                     other.TextureId == face.TextureId) > 1)
                             .OrderBy(face => face.SectorIndex)
                             .ThenBy(face => face.FaceIndex))
                {
                    _selectedTerrain = candidate;
                    _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(candidate);
                    _selectedTerrainPointIndex = -1;
                    TerrainCrossLevelLookChoice? donor =
                        BuildCrossLevelTerrainLookChoices(
                                candidate.Surface,
                                requestedSourceLevelKey: "gnastysworld",
                                artOnlyPreserveTarget: true)
                            .FirstOrDefault(choice =>
                                choice.CanChooseAsPaintBrushSource &&
                                choice.CanApplyAtomically);
                    if (donor == null)
                        continue;
                    blockedTarget = candidate;
                    blockedBrush = TerrainTexturePaintBrush.FromCrossLevel(
                        blockedLevel.Key,
                        donor);
                    break;
                }
                if (blockedTarget == null || blockedBrush == null)
                    continue;

                int blockedTargetIndex = _currentGeometry.Polygons.IndexOf(blockedTarget);
                TerrainTexturePaintFaceAuditSnapshot blockedFaceBefore =
                    TerrainTexturePaintFaceAuditSnapshot.Capture(blockedTarget);
                string blockedManifestPath =
                    NativeTerrainTextureRelocationEditStore.ManifestPath(
                        _workspace.RootPath,
                        blockedLevel.Key);
                byte[]? blockedManifestBefore = File.Exists(blockedManifestPath)
                    ? File.ReadAllBytes(blockedManifestPath)
                    : null;
                bool blockedInitialScopeSeen = false;
                bool blockedFallbackModalSeen = false;
                TerrainTexturePaintScopeConfirmationForTesting =
                    (_, textureId, faceCount, canPaintSelected, explanation) =>
                    {
                        if (textureId == blockedTarget.TextureId &&
                            faceCount > 1 &&
                            canPaintSelected &&
                            explanation.Contains(
                                "run the exact private-record",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            blockedInitialScopeSeen = true;
                            return Task.FromResult(
                                TerrainTexturePaintScopeChoice.SelectedSection);
                        }
                        blockedFallbackModalSeen =
                            textureId == blockedTarget.TextureId &&
                            faceCount > 1 &&
                            !canPaintSelected &&
                            explanation.Contains(
                                "alias-preserving packing proof",
                                StringComparison.OrdinalIgnoreCase);
                        return Task.FromResult(TerrainTexturePaintScopeChoice.Cancel);
                    };
                StartTerrainTexturePaintMode(blockedBrush);
                await ApplyTerrainTexturePaintBrushAsync(
                    blockedTargetIndex,
                    blockedTarget);
                byte[]? blockedManifestAfter = File.Exists(blockedManifestPath)
                    ? File.ReadAllBytes(blockedManifestPath)
                    : null;
                bool blockedManifestUnchanged =
                    blockedManifestBefore == null && blockedManifestAfter == null ||
                    blockedManifestBefore != null &&
                    blockedManifestAfter != null &&
                    blockedManifestBefore.SequenceEqual(blockedManifestAfter);
                if (!blockedInitialScopeSeen ||
                    !blockedFallbackModalSeen ||
                    !blockedFaceBefore.Matches(blockedTarget) ||
                    !blockedManifestUnchanged ||
                    _nativeTerrainTextureRelocations.Count != 0)
                {
                    throw new InvalidOperationException(
                        $"Gate ON did not defer {blockedLevel.DisplayName}'s Selected Section proof and then preserve the explicit, cancelable All Linked Sections fallback after rejection.");
                }
                blockedDestinationFallbackWasExercised = true;
                break;
            }
            if (!blockedDestinationFallbackWasExercised)
            {
                throw new InvalidOperationException(
                    "No explicitly blocked destination reproduced the gated no-private-slot shared-fallback modal.");
            }

            LevelDefinition artisans = _catalog.FindByKey("artisans")
                ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
            LevelDefinition gnastysWorld = _catalog.FindByKey("gnastysworld")
                ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
            if (!string.Equals(
                    LevelCatalog.NormalizeKey(_currentLevel?.Key ?? ""),
                    LevelCatalog.NormalizeKey(artisans.Key),
                    StringComparison.OrdinalIgnoreCase))
            {
                await SelectLevelAsync(artisans);
            }
            if (_currentLevel == null || _currentGeometry == null)
                throw new InvalidOperationException("Artisans did not load for the guarded private-texture smoke.");

            string terrainEditsPath = Path.Combine(
                _workspace.RootPath,
                $"{artisans.Key}-terrain-edits.json");
            string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
                _workspace.RootPath,
                artisans.Key);
            if (File.Exists(terrainEditsPath) || File.Exists(relocationPath))
            {
                throw new InvalidOperationException(
                    "The guarded private-texture smoke requires an isolated Artisans workspace.");
            }

            TerrainPolygon? target = null;
            TerrainTexturePaintBrush? brush = null;
            TerrainTexturePaintBrush? replacementPrivateBrush = null;
            foreach (TerrainPolygon candidate in _currentGeometry.Polygons
                         .Where(face =>
                             !face.IsTerrainRemoved &&
                             face.TextureId == 55 &&
                             string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                             !face.HasTextureEdit &&
                             !face.HasTextureVisualEdit &&
                             !face.HasSurfaceBehaviorEdit)
                         .OrderBy(face => face.SectorIndex)
                         .ThenBy(face => face.FaceIndex))
            {
                _selectedTerrain = candidate;
                _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(candidate);
                _selectedTerrainPointIndex = -1;
                TerrainCrossLevelLookChoice[] donorChoices = BuildCrossLevelTerrainLookChoices(
                        candidate.Surface,
                        requestedSourceLevelKey: gnastysWorld.Key,
                        artOnlyPreserveTarget: true)
                    .ToArray();
                TerrainCrossLevelLookChoice? donor = donorChoices.FirstOrDefault(choice =>
                        choice.TextureId == 17 &&
                        choice.CanChooseAsPaintBrushSource &&
                        choice.CanApplyAtomically);
                TerrainCrossLevelLookChoice? replacementDonor = donorChoices.FirstOrDefault(choice =>
                    choice.TextureId == 16 &&
                    choice.CanChooseAsPaintBrushSource &&
                    choice.CanApplyAtomically);
                if (donor == null || replacementDonor == null)
                    continue;
                target = candidate;
                brush = TerrainTexturePaintBrush.FromCrossLevel(artisans.Key, donor);
                replacementPrivateBrush = TerrainTexturePaintBrush.FromCrossLevel(
                    artisans.Key,
                    replacementDonor);
                break;
            }
            if (target == null || brush == null || replacementPrivateBrush == null)
            {
                throw new InvalidOperationException(
                    "No clean Artisans T55 face exposed Gnasty's World T17/T16 for the guarded private-texture smoke.");
            }

            int targetIndex = _currentGeometry.Polygons.IndexOf(target);
            string targetRuntimeKey = target.RuntimeKey;
            int originalTextureId = target.TextureId;
            int linkedFaceCount = _currentGeometry.Polygons.Count(face =>
                !face.IsTerrainRemoved && face.TextureId == originalTextureId);
            bool privateScopeOffered = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, textureId, faceCount, canPaintSelected, explanation) =>
                {
                    privateScopeOffered =
                        textureId == originalTextureId &&
                        faceCount == linkedFaceCount &&
                        canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase) &&
                        explanation.Contains(
                            "only if you choose",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(privateScopeOffered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(targetIndex, target);
            NativeTerrainTextureRelocationEdit privateEdit =
                _nativeTerrainTextureRelocations.SingleOrDefault(edit =>
                    edit.UsesAppendedPrivateRecord &&
                    brush.Matches(edit))
                ?? throw new InvalidOperationException(
                    $"The guarded selected-section paint did not allocate an appended row. Status: {_statusText.Text}");
            if (!privateScopeOffered ||
                privateEdit.TargetTextureId != 68 ||
                privateEdit.MaterialTemplateTextureId != 55 ||
                _nativeTerrainTextureRelocations.Count != 1 ||
                target.TextureId != 68 ||
                !target.HasTextureEdit ||
                _currentGeometry.Polygons.Any(face =>
                    !ReferenceEquals(face, target) &&
                    face.OriginalTextureId == originalTextureId &&
                    face.TextureId != originalTextureId))
            {
                throw new InvalidOperationException(
                    "The guarded Artisans allocation was not selected-only T68 with retail material template T55.");
            }

            TerrainPolygon gateOffReuseTarget = _currentGeometry.Polygons.First(face =>
                !ReferenceEquals(face, target) &&
                !face.IsTerrainRemoved &&
                face.OriginalTextureId == 55 &&
                face.TextureId == 55 &&
                !face.HasTextureEdit &&
                !face.HasTextureVisualEdit &&
                !face.HasSurfaceBehaviorEdit);
            string gateOffReuseRuntimeKey = gateOffReuseTarget.RuntimeKey;
            int gateOffReuseTargetIndex =
                _currentGeometry.Polygons.IndexOf(gateOffReuseTarget);
            bool gateOffPrivateWasOffered = false;
            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                null);
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, _) =>
                {
                    gateOffPrivateWasOffered |= canPaintSelected;
                    return Task.FromResult(canPaintSelected
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            await ApplyTerrainTexturePaintBrushAsync(
                gateOffReuseTargetIndex,
                gateOffReuseTarget);
            TerrainPolygon normalReleaseReusedFace = _currentGeometry.Polygons.Single(face =>
                string.Equals(
                    face.RuntimeKey,
                    gateOffReuseRuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
            if (normalReleaseReusedFace.TextureId != 68 ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 1 ||
                !_nativeTerrainTextureRelocations.Any(edit =>
                    edit.UsesAppendedPrivateRecord &&
                    edit.TargetTextureId == 68))
            {
                throw new InvalidOperationException(
                    "Normal release did not reuse its one runtime-proven Artisans private row on another selected face. " +
                    $"Staged: {string.Join(", ", _nativeTerrainTextureRelocations.Select(edit =>
                        $"T{edit.TargetTextureId}/{edit.TargetRecordKind}/M{edit.MaterialTemplateTextureId}"))}. " +
                    $"Offered={gateOffPrivateWasOffered}, face {gateOffReuseRuntimeKey}=T{normalReleaseReusedFace.TextureId}, " +
                    $"appended={_nativeTerrainTextureRelocations.Count(edit => edit.UsesAppendedPrivateRecord)}, " +
                    $"hasT68={_nativeTerrainTextureRelocations.Any(edit => edit.UsesAppendedPrivateRecord && edit.TargetTextureId == 68)}. " +
                    $"Status: {_statusText.Text}");
            }

            _selectedTerrain = normalReleaseReusedFace;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(normalReleaseReusedFace);
            await UndoSelectedTerrainTexturePaintAsync();
            TerrainPolygon normalReleaseRestoredFace = _currentGeometry.Polygons.Single(face =>
                string.Equals(
                    face.RuntimeKey,
                    gateOffReuseRuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
            if (_nativeTerrainTextureRelocations.Count != 1 ||
                !_nativeTerrainTextureRelocations[0].UsesAppendedPrivateRecord ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                normalReleaseRestoredFace.TextureId != 55)
            {
                throw new InvalidOperationException(
                    "Undoing the normal-release row reuse did not restore the T68-only private baseline.");
            }

            TerrainPolygon[] normalReleaseDistinctTargets = _currentGeometry.Polygons
                .Where(face =>
                    !face.IsTerrainRemoved &&
                    face.OriginalTextureId >= 0 &&
                    face.OriginalTextureId != 55 &&
                    face.TextureId == face.OriginalTextureId &&
                    !face.HasTextureEdit &&
                    !face.HasTextureVisualEdit &&
                    !face.HasSurfaceBehaviorEdit)
                .GroupBy(face => face.OriginalTextureId)
                .Select(group => group
                    .OrderBy(face => face.SectorIndex)
                    .ThenBy(face => face.FaceIndex)
                    .First())
                .OrderBy(face => face.OriginalTextureId)
                .Take(4)
                .ToArray();
            if (normalReleaseDistinctTargets.Length != 4)
            {
                throw new InvalidOperationException(
                    "Artisans did not expose four clean, distinct retail material templates for the four-row/fifth-row gate smoke.");
            }

            TerrainPolygon differentMaterialTarget = normalReleaseDistinctTargets[0];
            int differentMaterialOriginalId = differentMaterialTarget.OriginalTextureId;
            string differentMaterialRuntimeKey = differentMaterialTarget.RuntimeKey;
            List<string> normalReleaseAdditionalRuntimeKeys = [];
            bool normalReleaseFourRowsOffered = true;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    bool offered = canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase);
                    normalReleaseFourRowsOffered &= offered;
                    return Task.FromResult(offered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            for (int index = 0; index < 3; index++)
            {
                TerrainPolygon additionalTarget = normalReleaseDistinctTargets[index];
                int additionalTargetIndex =
                    _currentGeometry.Polygons.IndexOf(additionalTarget);
                StartTerrainTexturePaintMode(brush);
                await ApplyTerrainTexturePaintBrushAsync(
                    additionalTargetIndex,
                    additionalTarget);
                int expectedTextureId = 69 + index;
                NativeTerrainTextureRelocationEdit? additionalEdit =
                    _nativeTerrainTextureRelocations.SingleOrDefault(edit =>
                        edit.UsesAppendedPrivateRecord &&
                        edit.TargetTextureId == expectedTextureId);
                if (additionalEdit == null ||
                    additionalEdit.DonorTextureId != 17 ||
                    additionalEdit.MaterialTemplateTextureId !=
                        additionalTarget.OriginalTextureId ||
                    additionalTarget.TextureId != expectedTextureId)
                {
                    throw new InvalidOperationException(
                        $"Normal release did not allocate distinct Artisans private row T{expectedTextureId} for retail material template T{additionalTarget.OriginalTextureId}. " +
                        $"Status: {_statusText.Text}");
                }
                normalReleaseAdditionalRuntimeKeys.Add(additionalTarget.RuntimeKey);
            }
            if (!normalReleaseFourRowsOffered ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 4 ||
                !_nativeTerrainTextureRelocations
                    .Where(edit => edit.UsesAppendedPrivateRecord)
                    .Select(edit => edit.TargetTextureId)
                    .Order()
                    .SequenceEqual([68, 69, 70, 71]))
            {
                throw new InvalidOperationException(
                    "Normal release did not stage the exact contiguous four-row Artisans T68-T71 profile.");
            }

            TerrainPolygon normalReleaseFifthTarget = normalReleaseDistinctTargets[3];
            int normalReleaseFifthTargetIndex =
                _currentGeometry.Polygons.IndexOf(normalReleaseFifthTarget);
            TerrainTexturePaintFaceAuditSnapshot fifthFaceBefore =
                TerrainTexturePaintFaceAuditSnapshot.Capture(normalReleaseFifthTarget);
            byte[] fifthManifestBefore = File.ReadAllBytes(relocationPath);
            bool fifthInitialScopeSeen = false;
            bool fifthBlockedFallbackSeen = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    if (canPaintSelected)
                    {
                        fifthInitialScopeSeen = true;
                        return Task.FromResult(
                            TerrainTexturePaintScopeChoice.SelectedSection);
                    }
                    fifthBlockedFallbackSeen =
                        explanation.Contains(
                            "four",
                            StringComparison.OrdinalIgnoreCase) ||
                        explanation.Contains(
                            "runtime-proven",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(TerrainTexturePaintScopeChoice.Cancel);
                };
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(
                normalReleaseFifthTargetIndex,
                normalReleaseFifthTarget);
            if (!fifthInitialScopeSeen ||
                !fifthBlockedFallbackSeen ||
                !fifthFaceBefore.Matches(normalReleaseFifthTarget) ||
                !File.ReadAllBytes(relocationPath).SequenceEqual(fifthManifestBefore) ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 4)
            {
                throw new InvalidOperationException(
                    "Normal release did not reject the fifth Artisans private row atomically after staging T68-T71.");
            }

            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                "1");
            bool researchFifthRowOffered = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    researchFifthRowOffered =
                        canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(researchFifthRowOffered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(
                normalReleaseFifthTargetIndex,
                normalReleaseFifthTarget);
            if (!researchFifthRowOffered ||
                normalReleaseFifthTarget.TextureId != 72 ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 5)
            {
                throw new InvalidOperationException(
                    "Explicit research mode no longer admitted the fifth Artisans private row after the normal four-row cap.");
            }
            _selectedTerrain = normalReleaseFifthTarget;
            _selectedTerrainIndex = normalReleaseFifthTargetIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                null);
            if (normalReleaseFifthTarget.TextureId !=
                    normalReleaseFifthTarget.OriginalTextureId ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 4)
            {
                throw new InvalidOperationException(
                    "Undoing the research-only fifth row did not restore the normal T68-T71 profile. " +
                    $"Face T{normalReleaseFifthTarget.TextureId}, status: {_statusText.Text}, staged: " +
                    $"{string.Join(", ", _nativeTerrainTextureRelocations.Select(edit =>
                        $"T{edit.TargetTextureId}/{edit.TargetRecordKind}/M{edit.MaterialTemplateTextureId}"))}.");
            }

            await SelectLevelAsync(artisans);
            TerrainPolygon reloaded = _currentGeometry?.Polygons.Single(face =>
                string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The guarded private face did not reload.");
            if (reloaded.TextureId != 68 ||
                !_nativeTerrainTextureRelocations.Any(edit =>
                    edit.UsesAppendedPrivateRecord && brush.Matches(edit)) ||
                _nativeTerrainTextureRelocations.Count(edit =>
                    edit.UsesAppendedPrivateRecord) != 4 ||
                normalReleaseAdditionalRuntimeKeys
                    .Select((runtimeKey, index) =>
                        _currentGeometry.Polygons.Single(face =>
                            string.Equals(
                                face.RuntimeKey,
                                runtimeKey,
                                StringComparison.OrdinalIgnoreCase)).TextureId ==
                        69 + index)
                    .Any(matches => !matches))
            {
                throw new InvalidOperationException(
                    "The normal-release T68-T71 rows and selected faces did not survive save/reload.");
            }

            string smokeDirectory = Path.Combine(
                _workspace.RootPath,
                "_local",
                "research",
                "appended-private-ui-gate-smoke");
            Directory.CreateDirectory(smokeDirectory);
            TerrainPatchResult? ordinary = await TryPlanTerrainPatchAsync(
                artisans,
                sourceImage,
                sourceCue,
                smokeDirectory,
                "guarded-private-ordinary-preflight",
                writeImage: false,
                includeNativeTextureRelocations: false);
            if (ordinary == null)
                throw new InvalidOperationException("The guarded private face produced no ordinary terrain preflight.");
            IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches =
                NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
                    ordinary.Plan);
            NativeTerrainTexturePrivateRecordBatchPlan buildPlan =
                NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
                    new NativeTerrainTexturePrivateRecordBatchRequest(
                        sourceImage,
                        sourceCue,
                        Path.Combine(smokeDirectory, "not-exported"),
                        wadAnalysisPath,
                        artisans,
                        _nativeTerrainTextureRelocations,
                        ordinaryPatches,
                        HasCustomImageTexturePatches:
                            ordinary.Plan.CustomTextureBytePatchCount > 0));
            if (buildPlan.WriterKind != NativeTerrainTexturePrivateImageWriterKind.FixedTail ||
                buildPlan.FixedTailPlan == null ||
                buildPlan.SectorRelocationPlan != null ||
                !buildPlan.ExclusiveImageWriterSelected ||
                !buildPlan.AppendedPrivateEdits
                    .Select(edit => edit.TargetTextureId)
                    .SequenceEqual([68, 69, 70, 71]) ||
                !ordinaryPatches.Any(patch =>
                    string.Equals(patch.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "The guarded App build path did not select the sole fixed-tail composer for the runtime-proven T68-T71 profile.");
            }

            EditedLevelExportTarget buildTarget = new(
                artisans,
                HasObjectEdits: false,
                HasTerrainEdits: true,
                HasCustomTerrainTextures: false,
                HasNativeTerrainTextureRelocations: true,
                HasLevelTextEdit: false,
                HasLevelMusicEdit: false,
                HasSkyboxEdit: false);
            AppendedPrivateTerrainTexturePlanStep fourRowNormalReleasePlan =
                await PlanAppendedPrivateTerrainTextureStepAsync(
                    buildTarget,
                    sourceImage,
                    sourceCue,
                    smokeDirectory,
                    Path.Combine(smokeDirectory, "normal-release-four-row-not-exported"));
            if (fourRowNormalReleasePlan.Plan.WriterKind !=
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail ||
                fourRowNormalReleasePlan.Plan.AppendedPrivateEdits.Count != 4 ||
                !AppendedPrivateTerrainTextureResearchGate.TryAuthorizeNormalCreateBin(
                    fourRowNormalReleasePlan.Plan.SourceBinding,
                    fourRowNormalReleasePlan.Plan.WriterKind,
                    fourRowNormalReleasePlan.Plan.AppendedPrivateEdits.Count,
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string fourRowNormalReleaseReason) ||
                !fourRowNormalReleaseReason.Contains(
                    "Runtime-proven exact",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal Create BIN planning did not authorize the exact four-row Artisans T68-T71 profile.");
            }

            for (int index = normalReleaseAdditionalRuntimeKeys.Count - 1;
                 index >= 0;
                 index--)
            {
                TerrainPolygon additionalFace = _currentGeometry.Polygons.Single(face =>
                    string.Equals(
                        face.RuntimeKey,
                        normalReleaseAdditionalRuntimeKeys[index],
                        StringComparison.OrdinalIgnoreCase));
                _selectedTerrain = additionalFace;
                _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(additionalFace);
                await UndoSelectedTerrainTexturePaintAsync();
            }
            if (_nativeTerrainTextureRelocations.Count != 1 ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                _currentGeometry.Polygons.Any(face =>
                    normalReleaseAdditionalRuntimeKeys.Contains(
                        face.RuntimeKey,
                        StringComparer.OrdinalIgnoreCase) &&
                    face.TextureId != face.OriginalTextureId))
            {
                throw new InvalidOperationException(
                    "Four-row planning cleanup did not restore the original T68-only baseline.");
            }

            TerrainPolygon missingManifestFace = _currentGeometry?.Polygons.Single(face =>
                string.Equals(
                    face.RuntimeKey,
                    differentMaterialRuntimeKey,
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    "The inverse-manifest smoke target did not reload.");
            int missingManifestOriginalTextureId =
                missingManifestFace.OriginalTextureId;
            missingManifestFace.ApplyTextureOverride(69);
            await PersistCurrentTerrainEditsAsync();
            bool missingManifestRejected = false;
            try
            {
                await ExportAppendedPrivateTerrainTextureStepAsync(
                    buildTarget,
                    sourceImage,
                    sourceCue,
                    smokeDirectory,
                    Path.Combine(smokeDirectory, "missing-T69-must-not-export"));
            }
            catch (InvalidDataException ex)
            {
                missingManifestRejected =
                    ex.Message.Contains("T69", StringComparison.Ordinal) &&
                    ex.Message.Contains(
                        "no matching appended-private manifest row",
                        StringComparison.OrdinalIgnoreCase);
            }
            if (!missingManifestRejected ||
                File.Exists(Path.Combine(
                    smokeDirectory,
                    "missing-T69-must-not-export.bin")) ||
                File.Exists(Path.Combine(
                    smokeDirectory,
                    "missing-T69-must-not-export.cue")))
            {
                throw new InvalidOperationException(
                    "A live T69 face with only a T68 manifest was not rejected before the exclusive Create BIN writer.");
            }
            missingManifestFace.ApplyTextureOverride(
                missingManifestOriginalTextureId);
            await PersistCurrentTerrainEditsAsync();

            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                null);
            AppendedPrivateTerrainTexturePlanStep normalReleasePlan =
                await PlanAppendedPrivateTerrainTextureStepAsync(
                    buildTarget,
                    sourceImage,
                    sourceCue,
                    smokeDirectory,
                    Path.Combine(smokeDirectory, "normal-release-not-exported"));
            if (normalReleasePlan.Plan.WriterKind !=
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail ||
                normalReleasePlan.Plan.AppendedPrivateEdits.Count != 1 ||
                !AppendedPrivateTerrainTextureResearchGate.TryAuthorizeNormalCreateBin(
                    normalReleasePlan.Plan.SourceBinding,
                    normalReleasePlan.Plan.WriterKind,
                    normalReleasePlan.Plan.AppendedPrivateEdits.Count,
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string normalReleaseReason) ||
                !normalReleaseReason.Contains(
                    "Runtime-proven exact",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal Create BIN planning did not retain the one-row baseline after the four-row regression cleanup.");
            }

            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                "1");

            int reloadedIndex = _currentGeometry.Polygons.IndexOf(reloaded);
            TerrainTexturePaintFaceAuditSnapshot replacementFailureFaceBefore =
                TerrainTexturePaintFaceAuditSnapshot.Capture(reloaded);
            byte[] replacementFailureManifestBefore =
                File.ReadAllBytes(relocationPath);
            string replacementFailurePreviewBefore =
                _nativeTerrainTextureRelocations.Single().PreviewImagePath;
            AppendedPrivateReplacementCompactionFaultForTesting = () =>
                throw new IOException(
                    "Injected failure before obsolete private-row compaction.");
            StartTerrainTexturePaintMode(replacementPrivateBrush);
            try
            {
                await ApplyTerrainTexturePaintBrushAsync(reloadedIndex, reloaded);
            }
            finally
            {
                AppendedPrivateReplacementCompactionFaultForTesting = null;
            }
            if (!replacementFailureFaceBefore.Matches(reloaded) ||
                !File.ReadAllBytes(relocationPath).SequenceEqual(
                    replacementFailureManifestBefore) ||
                _nativeTerrainTextureRelocations.Count != 1 ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                _nativeTerrainTextureRelocations[0].DonorTextureId != 17 ||
                !File.Exists(replacementFailurePreviewBefore) ||
                !(_statusText.Text ?? "").Contains(
                    "restored; no texture changed",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A failed private-row replacement did not restore its face, manifest, donor row, and managed preview atomically.");
            }

            StartTerrainTexturePaintMode(replacementPrivateBrush);
            await ApplyTerrainTexturePaintBrushAsync(reloadedIndex, reloaded);
            if (reloaded.TextureId != 68 ||
                _nativeTerrainTextureRelocations is not
                [
                    {
                        TargetTextureId: 68,
                        DonorTextureId: 16,
                        MaterialTemplateTextureId: 55,
                        UsesAppendedPrivateRecord: true
                    }
                ])
            {
                throw new InvalidOperationException(
                    $"Painting a second donor over one private face left an orphan row or lost Selected Section. Status: {_statusText.Text}");
            }

            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(reloadedIndex, reloaded);
            if (reloaded.TextureId != 68 ||
                _nativeTerrainTextureRelocations.Count != 1 ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                _nativeTerrainTextureRelocations[0].DonorTextureId != 17 ||
                _nativeTerrainTextureRelocations[0].MaterialTemplateTextureId != 55)
            {
                throw new InvalidOperationException(
                    "Repainting the same private face back to its first donor did not reuse the compacted T68 contract exactly.");
            }

            _selectedTerrain = reloaded;
            _selectedTerrainIndex = reloadedIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            if (reloaded.TextureId != originalTextureId ||
                _nativeTerrainTextureRelocations.Count != 0)
            {
                throw new InvalidOperationException(
                    "Undo after repeated private overpaint did not release the sole appended row.");
            }

            bool repaintAfterUndoOffered = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    repaintAfterUndoOffered =
                        canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(repaintAfterUndoOffered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(reloadedIndex, reloaded);
            if (!repaintAfterUndoOffered ||
                reloaded.TextureId != 68 ||
                _nativeTerrainTextureRelocations.Count != 1)
            {
                throw new InvalidOperationException(
                    $"The same face was not available for private repaint immediately after Undo. Status: {_statusText.Text}");
            }

            TerrainPolygon repeatedDifferentMaterialFace =
                _currentGeometry.Polygons.Single(face =>
                    string.Equals(
                        face.RuntimeKey,
                        differentMaterialRuntimeKey,
                        StringComparison.OrdinalIgnoreCase));
            int repeatedDifferentMaterialIndex =
                _currentGeometry.Polygons.IndexOf(repeatedDifferentMaterialFace);
            bool secondRowOffered = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    secondRowOffered =
                        canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(secondRowOffered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            await ApplyTerrainTexturePaintBrushAsync(
                repeatedDifferentMaterialIndex,
                repeatedDifferentMaterialFace);
            if (!secondRowOffered ||
                repeatedDifferentMaterialFace.TextureId != 69 ||
                _nativeTerrainTextureRelocations.Count != 2)
            {
                throw new InvalidOperationException(
                    "A different material template was not available as the second private row after Undo/repaint.");
            }

            _selectedTerrain = reloaded;
            _selectedTerrainIndex = reloadedIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            if (reloaded.TextureId != originalTextureId ||
                repeatedDifferentMaterialFace.TextureId != 68 ||
                _nativeTerrainTextureRelocations.Count != 1 ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                _nativeTerrainTextureRelocations[0].MaterialTemplateTextureId !=
                    differentMaterialOriginalId)
            {
                throw new InvalidOperationException(
                    "Undoing the lower private row did not compact the surviving different-material row to T68 exactly.");
            }

            bool postCompactionRepaintOffered = false;
            TerrainTexturePaintScopeConfirmationForTesting =
                (_, _, _, canPaintSelected, explanation) =>
                {
                    postCompactionRepaintOffered =
                        canPaintSelected &&
                        explanation.Contains(
                            "run the exact private-record",
                            StringComparison.OrdinalIgnoreCase);
                    return Task.FromResult(postCompactionRepaintOffered
                        ? TerrainTexturePaintScopeChoice.SelectedSection
                        : TerrainTexturePaintScopeChoice.Cancel);
                };
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(reloadedIndex, reloaded);
            if (!postCompactionRepaintOffered ||
                reloaded.TextureId != 69 ||
                _nativeTerrainTextureRelocations.Count != 2)
            {
                throw new InvalidOperationException(
                    $"A same-face repaint was unavailable after lower-row compaction. Status: {_statusText.Text}");
            }

            _selectedTerrain = repeatedDifferentMaterialFace;
            _selectedTerrainIndex = repeatedDifferentMaterialIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            if (repeatedDifferentMaterialFace.TextureId != differentMaterialOriginalId ||
                reloaded.TextureId != 68 ||
                _nativeTerrainTextureRelocations.Count != 1 ||
                _nativeTerrainTextureRelocations[0].TargetTextureId != 68 ||
                _nativeTerrainTextureRelocations[0].DonorTextureId != 17 ||
                _nativeTerrainTextureRelocations[0].MaterialTemplateTextureId != 55)
            {
                throw new InvalidOperationException(
                    "Undoing the compacted lower row did not retain the repainted face as a valid T68 private contract.");
            }

            _selectedTerrain = reloaded;
            _selectedTerrainIndex = reloadedIndex;
            await UndoSelectedTerrainTexturePaintAsync();
            await SelectLevelAsync(artisans);
            TerrainPolygon cleaned = _currentGeometry?.Polygons.Single(face =>
                string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The guarded private face did not reload after Undo.");
            if (cleaned.TextureId != originalTextureId ||
                NativeTerrainTextureRelocationEditStore.Load(
                    _workspace.RootPath,
                    artisans.Key).Count != 0)
            {
                throw new InvalidOperationException(
                    "Undo did not remove and compact the guarded private row and restore the selected face.");
            }

            return
                "normal release staged, reloaded, and planned Create BIN for the runtime-proven four-row Artisans T68-T71 profile, rejected a fifth row atomically, and retained the one-row baseline after cleanup; research mode admitted the fifth row and retained the literal 32/35 allow set while rejecting an unknown key, explicitly blocked Lofty Castle/Jacques/Gnorc Cove, kept texture-only +0x800/+0x1000 aggregate destinations available while blocking unrelated stale-offset writers, rejected Tree Tops <- Dark Hollow T0 before manifest/face mutation, rejected a live T69 face lacking its manifest row, preserved linked neighbors, selected the sole fixed-tail build composer, replaced a one-face donor without leaving an orphan row, and kept same/different-material Selected Section available through repeated Undo, repaint, and lower-row compaction";
        }
        finally
        {
            TerrainTexturePaintScopeConfirmationForTesting = null;
            StopTerrainTexturePaintMode(announce: false);
            Environment.SetEnvironmentVariable(
                AppendedPrivateTerrainTextureResearchGate.EnvironmentVariable,
                previousGate);
        }
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
        if (TryBlockId65UnsupportedTerrainMutation(
                _selectedTerrain,
                "Terrain texture palette",
                textureOrSurface: true))
        {
            return;
        }

        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before choosing a terrain texture.";
            return;
        }

        string normalizedTargetLevel = LevelCatalog.NormalizeKey(_currentLevel.Key);
        bool paletteMatchesTarget =
            string.Equals(
                _terrainTexturePaintPaletteTargetLevelKey,
                normalizedTargetLevel,
                StringComparison.OrdinalIgnoreCase) &&
            _terrainTexturePaintLoadedPaletteItems.Count > 0;
        bool canReuseLoadedPalette = reuseLoadedPalette && paletteMatchesTarget;
        if (!paletteMatchesTarget)
        {
            // Palette catalogs and decoded previews belong to one destination
            // level. Keep every explicitly loaded donor alive for the entire
            // working session, and invalidate only when the destination changes.
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
        List<TerrainTextureSwapChoice> inGameChoices = [];
        List<TerrainCrossLevelLookChoice> crossLevelChoices = [];

        if (canReuseLoadedPalette &&
            _terrainTexturePaintLoadedPaletteItems.TryGetValue(
                normalizedTargetLevel,
                out IReadOnlyList<TerrainTexturePaintGalleryItem>? cachedCurrentItems))
        {
            inGameChoices = cachedCurrentItems
                .Select(item => item.Result.InGameLook)
                .OfType<TerrainTextureSwapChoice>()
                .ToList();
        }
        else
        {
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
            preferredPaintBrush: preferredBrush,
            suggestionTarget: previousTerrain is { IsTerrainRemoved: false, TextureId: >= 0 }
                ? previousTerrain
                : null);

        TerrainPaintDialogResult? result = await dialog.ShowDialog<TerrainPaintDialogResult?>(this);
        TerrainTexturePaintBrush? brush = result?.Mode switch
        {
            TerrainPaintModeKind.InGameLook when result.InGameLook != null =>
                TerrainTexturePaintBrush.FromSameLevel(
                    _currentLevel.Key,
                    _currentLevel.DisplayName,
                    result.InGameLook,
                    preserveTargetNativeSurface: true),
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
        string staged = _nativeTerrainTextureRelocations.Count == 0
            ? ""
            : $"\nStaged cross-level texture records: {_nativeTerrainTextureRelocations.Count:N0} " +
              $"({string.Join(", ", _nativeTerrainTextureRelocations.Select(edit => edit.TargetTextureId))}). " +
              "Undo Selected removes only the texture record under the selected terrain section.";
        _terrainTexturePaintModeText.Text =
            $"{state}\nSource: {_activeTerrainTexturePaintBrush.DisplayLabel}.{last}{staged}";
        if (_terrainTexturePaintStopButton != null)
            _terrainTexturePaintStopButton.IsEnabled = !_terrainTexturePaintBusy;
        if (_terrainTexturePaintReturnButton != null)
            _terrainTexturePaintReturnButton.IsEnabled = !_terrainTexturePaintBusy;
        RefreshTerrainPrivateTextureCapacityUi();
    }

    private async Task ApplyTerrainTexturePaintBrushAsync(int terrainIndex, TerrainPolygon terrain)
    {
        if (TryBlockId65UnsupportedTerrainMutation(
                terrain,
                "Terrain texture paint",
                textureOrSurface: true))
        {
            return;
        }

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

        _terrainTexturePaintBusy = true;
        _viewport.TerrainTexturePaintBusy = true;
        // Invalidate any level load that was already awaiting its background
        // cache parse before this paint click began. ApplyLoadedLevel also
        // rejects a late result while painting is busy.
        _levelLoadRequestId++;
        SyncLevelPickers(_currentLevel);
        RefreshLevelSelectionAvailability();
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
            TerrainTexturePaintHistoryCheckpoint? historyCheckpoint =
                brush.Kind == TerrainTexturePaintSourceKind.CrossLevel
                    ? CaptureTerrainTexturePaintHistoryCheckpoint(
                        $"Paint {terrain.RuntimeKey} with {brush.DisplayLabel}")
                    : null;
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
                bool preserveTargetNativeSurface =
                    brush.SurfacePropertyMode ==
                    TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface;
                if (preserveTargetNativeSurface
                    ? !choice.CanUseArt
                    : !choice.CanApplyAtomically)
                {
                    string reason = preserveTargetNativeSurface
                        ? choice.ArtReadinessNote
                        : choice.AtomicBlockReason;
                    _statusText.Text = $"{brush.DisplayLabel} cannot be painted on face {terrain.RuntimeKey}: {reason} No terrain changed; paint mode remains active.";
                    return;
                }

                if (preserveTargetNativeSurface)
                {
                    applied = await ApplySelectedTerrainResidentArtOnlyAsync(
                        choice,
                        terrain);
                }
                else
                {
                    await ApplySelectedTerrainInGameLookAsync(choice);
                    applied = terrain.TextureId == choice.TextureId &&
                        terrain.TextureVisualEdit == choice.NativeVisual;
                }
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
                    applied = await ReplaceExistingCrossLevelTerrainTexturePaintAsync(
                        brush,
                        terrain,
                        existingRelocation);
                }
                else
                {
                    if (terrain.HasTextureEdit || terrain.HasTextureVisualEdit || terrain.HasSurfaceBehaviorEdit)
                    {
                        _statusText.Text =
                            $"Face {terrain.RuntimeKey} already has a face-local texture, tint, or gameplay-property edit. " +
                            "Undo that texture paint first so the existing edit cannot be overwritten; no terrain changed.";
                        return;
                    }

                    applied = await ApplyCrossLevelTerrainTexturePaintToFaceAsync(brush, terrain);
                }
            }

            if (applied)
            {
                if (historyCheckpoint != null)
                    PushTerrainTexturePaintHistory(historyCheckpoint);
                _lastTerrainTexturePaintFace = terrain.RuntimeKey;
                EditorDiagnostics.RecordAction(
                    "Terrain texture paint staged",
                    $"Target: {_currentLevel.Key}:{terrain.RuntimeKey}/texture {terrain.TextureId}; source: {brush.DisplayLabel}; status: {_statusText.Text}");
            }
            else
            {
                EditorDiagnostics.RecordWarning(
                    "Terrain texture paint did not stage",
                    $"Target: {_currentLevel.Key}:{terrain.RuntimeKey}/texture {terrain.TextureId}; source: {brush.DisplayLabel}; status: {_statusText.Text}");
            }
        }
        catch (Exception ex)
        {
            EditorDiagnostics.RecordException("applying the active terrain texture paint source", ex);
            _statusText.Text = _releaseMode
                ? $"Could not apply {brush.DisplayLabel} to this terrain section. Try Undo, choose another texture, or use Linked Sections. Paint mode remains active."
                : $"Could not paint face {terrain.RuntimeKey}: {ex.Message} Paint mode remains active.";
        }
        finally
        {
            _terrainTexturePaintBusy = false;
            _viewport.TerrainTexturePaintBusy = false;
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

    private async Task<bool> ReplaceExistingCrossLevelTerrainTexturePaintAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        NativeTerrainTextureRelocationEdit existingRelocation)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return false;

        TerrainTexturePaintHistoryCheckpoint? appendedReplacementCheckpoint =
            existingRelocation.UsesAppendedPrivateRecord
                ? CaptureTerrainTexturePaintHistoryCheckpoint(
                    $"replace private texture {existingRelocation.TargetTextureId} on {terrain.RuntimeKey}")
                : null;

        // The normal native proof rejects an already-occupied target record.
        // Hide only that one old intent in memory while proving the replacement;
        // the on-disk manifest remains untouched until AddOrReplace commits the
        // newly proven donor. A failed/canceled proof restores the old preview
        // and intent, so painting on top never destroys the user's prior work.
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocationsBefore =
            _nativeTerrainTextureRelocations;
        _nativeTerrainTextureRelocations = relocationsBefore
            .Where(edit => edit.TargetTextureId != existingRelocation.TargetTextureId)
            .ToArray();
        ApplyCustomTerrainTexturePreviews();
        _viewport.NotifyTerrainPresentationDataChanged();

        bool applied = false;
        try
        {
            applied = await ApplyCrossLevelTerrainTexturePaintToFaceAsync(brush, terrain);
        }
        finally
        {
            if (!applied)
            {
                string failure = _statusText.Text ?? "The replacement did not pass its native proof.";
                _nativeTerrainTextureRelocations = relocationsBefore;
                ApplyCustomTerrainTexturePreviews();
                _viewport.NotifyTerrainPresentationDataChanged();
                _statusText.Text =
                    $"{failure} The previous {existingRelocation.DonorLevelName} texture " +
                    $"{existingRelocation.DonorTextureId} replacement on target texture " +
                    $"{existingRelocation.TargetTextureId} remains staged.";
            }
        }

        if (!applied)
            return false;

        // A one-face appended row is a private allocation, not a shared native
        // record. Painting another donor on that face can temporarily allocate
        // the next tail row while the old manifest preimage is still present.
        // If no live face consumes the old row after the new paint, remove it
        // immediately and compact the new row back into the gap. Leaving the
        // orphan behind made later Undo/repaint attempts fail the exact batch
        // proof with an apparently unavailable Selected Section.
        if (existingRelocation.UsesAppendedPrivateRecord &&
            !_currentGeometry.Polygons.Any(face =>
                !face.IsTerrainRemoved &&
                face.TextureId == existingRelocation.TargetTextureId))
        {
            string selectedOnlyResult = _statusText.Text ?? "The selected face was painted.";
            try
            {
                AppendedPrivateReplacementCompactionFaultForTesting?.Invoke();
                _nativeTerrainTextureRelocations =
                    await RemoveNativeTerrainTextureRelocationAndCompactAsync(
                        existingRelocation);
                int savedEdits = await PersistCurrentTerrainEditsAsync();
                if (!_nativeTerrainTextureRelocations.Any(edit =>
                        string.Equals(
                            edit.PreviewImagePath,
                            existingRelocation.PreviewImagePath,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    DeleteManagedNativeTerrainTexturePreview(
                        existingRelocation.PreviewImagePath);
                }
                ApplyCustomTerrainTexturePreviews();
                _viewport.NotifyTerrainPresentationDataChanged();
                ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(
                    _selectedTerrainIndex,
                    terrain));
                RefreshCurrentLevelDetails();
                RefreshTerrainReadinessHint();
                NativeTerrainTextureRelocationEdit? replacement =
                    _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                        brush.Matches(edit) &&
                        edit.MaterialTemplateTextureId == terrain.OriginalTextureId);
                _statusText.Text =
                    $"Replaced private texture {existingRelocation.TargetTextureId} on {terrain.RuntimeKey}, " +
                    $"released its now-unused donor row, and compacted the replacement to " +
                    $"T{replacement?.TargetTextureId ?? terrain.TextureId} ({savedEdits:N0} terrain edit(s) saved). " +
                    selectedOnlyResult;
                return true;
            }
            catch (Exception ex) when (
                appendedReplacementCheckpoint != null &&
                ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or OverflowException)
            {
                _terrainTexturePaintHistoryRestoreBusy = true;
                try
                {
                    await RestoreTerrainTexturePaintHistoryCheckpointAsync(
                        appendedReplacementCheckpoint);
                }
                finally
                {
                    _terrainTexturePaintHistoryRestoreBusy = false;
                }
                _statusText.Text =
                    $"Could not finish replacing private texture {existingRelocation.TargetTextureId}: {ex.Message} " +
                    "The prior donor row, face assignment, manifest, and preview were restored; no texture changed.";
                EditorDiagnostics.RecordException(
                    "compacting a replaced appended-private terrain texture",
                    ex);
                return false;
            }
        }

        // Applying on top of an existing shared record can legitimately end
        // as a selected-only paint. In that case the inner path either reused
        // another already-staged donor record or allocated a separate private
        // record; it did not replace this shared record. The old intent stayed
        // in the on-disk manifest while it was hidden from the in-memory proof,
        // so resynchronize before deciding whether its preview is obsolete.
        NativeTerrainTextureRelocationEdit? resultingTargetRelocation =
            _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                edit.TargetTextureId == existingRelocation.TargetTextureId);
        bool replacedSharedRecord = resultingTargetRelocation != null &&
            brush.Matches(resultingTargetRelocation);
        if (!replacedSharedRecord)
        {
            string selectedOnlyResult = _statusText.Text ?? "The selected face was painted.";
            Dictionary<int, NativeTerrainTextureRelocationEdit> retainedRelocations =
                _nativeTerrainTextureRelocations.ToDictionary(edit => edit.TargetTextureId);
            retainedRelocations[existingRelocation.TargetTextureId] = existingRelocation;
            _nativeTerrainTextureRelocations = retainedRelocations.Values
                .OrderBy(edit => edit.TargetTextureId)
                .ToArray();

            ApplyCustomTerrainTexturePreviews();
            _viewport.NotifyTerrainPresentationDataChanged();
            _statusText.Text =
                $"Kept the previous {existingRelocation.DonorLevelName} texture " +
                $"{existingRelocation.DonorTextureId} replacement on shared target texture " +
                $"{existingRelocation.TargetTextureId}. {selectedOnlyResult}";
            return true;
        }

        if (!_nativeTerrainTextureRelocations.Any(edit =>
                string.Equals(
                    edit.PreviewImagePath,
                    existingRelocation.PreviewImagePath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            DeleteManagedNativeTerrainTexturePreview(existingRelocation.PreviewImagePath);
        }
        _statusText.Text =
            $"Replaced the previous {existingRelocation.DonorLevelName} texture " +
            $"{existingRelocation.DonorTextureId} paint on target texture " +
            $"{existingRelocation.TargetTextureId}. {_statusText.Text}";
        return true;
    }

    private async Task<bool> ApplySelectedTerrainResidentArtOnlyAsync(
        TerrainTextureSwapChoice choice,
        TerrainPolygon terrain)
    {
        if (_currentLevel == null || _currentGeometry == null ||
            !choice.CanUseArt || terrain.IsTerrainRemoved)
        {
            return false;
        }

        NativeTerrainTextureRelocationEdit? donorRelocation =
            _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                edit.TargetTextureId == choice.TextureId);
        if (donorRelocation != null)
        {
            _statusText.Text =
                $"Texture {choice.TextureId} currently contains a shared replacement from " +
                $"{donorRelocation.DonorLevelName}. Undo that replacement before using the " +
                "resident texture on one section; no terrain changed.";
            return false;
        }

        TerrainTexturePaintStageSnapshot snapshot =
            TerrainTexturePaintStageSnapshot.Capture(terrain);
        string terrainEditsPath = Path.Combine(
            _workspace.RootPath,
            $"{_currentLevel.Key}-terrain-edits.json");
        byte[]? terrainEditsBefore = File.Exists(terrainEditsPath)
            ? File.ReadAllBytes(terrainEditsPath)
            : null;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;
        int originalTextureId = terrain.TextureId;

        try
        {
            // Only the face's native texture-id field changes. Its HP material,
            // semitransparency, near/fade tint, surface label, and collision
            // mapping remain exactly as they were on the destination face.
            terrain.ApplyTextureOverride(choice.TextureId);
            TerrainTexturePaintPersistenceFaultForTesting?.Invoke(terrainEditsPath);
            int savedEdits = await PersistCurrentTerrainEditsAsync();
            RefreshCurrentLevelDetails();
            RefreshTerrainTextureImageFiles();
            _viewport.NotifyTerrainPresentationDataChanged();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(
                _selectedTerrainIndex,
                terrain));
            _statusText.Text =
                $"Applied resident texture {choice.TextureId} to only {terrain.RuntimeKey} " +
                $"(was texture {originalTextureId}); preserved its native material, transparency, " +
                $"tint, surface label, and collision behavior; saved {savedEdits:N0} terrain edit(s).";
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
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
                Directory.CreateDirectory(
                    Path.GetDirectoryName(terrainEditsPath) ?? _workspace.RootPath);
                File.WriteAllBytes(terrainEditsPath, terrainEditsBefore);
            }
            _viewport.NotifyTerrainPresentationDataChanged();
            _statusText.Text =
                $"Could not apply resident texture {choice.TextureId} to {terrain.RuntimeKey}: " +
                $"{ex.Message} The face and saved terrain edits were restored.";
            return false;
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

        int originalTargetTextureId = terrain.TextureId;
        int affectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == originalTargetTextureId);
        TerrainCrossLevelLookChoice? sharedChoice = BuildCrossLevelTerrainLookChoices(
                terrain.Surface,
                brush,
                artOnlyPreserveTarget: true)
            .FirstOrDefault(brush.Matches);

        int clickedFaceMaterialTemplateTextureId = terrain.OriginalTextureId >= 0
            ? terrain.OriginalTextureId
            : originalTargetTextureId;
        NativeTerrainTextureRelocationEdit? reusablePrivateRelocation =
            _nativeTerrainTextureRelocations.FirstOrDefault(edit =>
                brush.Matches(edit) &&
                (edit.UsesAppendedPrivateRecord
                    ? clickedFaceMaterialTemplateTextureId >= 0 &&
                      clickedFaceMaterialTemplateTextureId == edit.MaterialTemplateTextureId
                    : edit.TargetTextureId == clickedFaceMaterialTemplateTextureId));
        if (reusablePrivateRelocation?.UsesAppendedPrivateRecord == true &&
            !await TryAllowExistingAppendedPrivateTerrainTextureForStagingAsync(
                sourceImage))
        {
            reusablePrivateRelocation = null;
        }
        if (reusablePrivateRelocation != null)
        {
            TerrainTexturePaintStageSnapshot reuseSnapshot = TerrainTexturePaintStageSnapshot.Capture(terrain);
            terrain.ApplyTextureOverride(reusablePrivateRelocation.TargetTextureId);
            TerrainCrossLevelLookChoice? reuseChoice = BuildCrossLevelTerrainLookChoices(
                    terrain.Surface,
                    brush,
                    allowSelectedFaceLocalTextureTarget: true,
                    artOnlyPreserveTarget: true)
                .FirstOrDefault(brush.Matches);
            reuseSnapshot.Restore();
            if (reuseChoice?.CanApplyAtomically == true)
            {
                TerrainTexturePaintScopeChoice scope = await ChooseTerrainTexturePaintScopeAsync(
                    brush,
                    terrain,
                    originalTargetTextureId,
                    affectedFaceCount,
                    canPaintSelectedSection: true,
                    selectedSectionExplanation:
                        $"Matching donor art is already loaded in texture record {reusablePrivateRelocation.TargetTextureId}, so only {terrain.RuntimeKey} can safely point to it without allocating another record.",
                    canPaintAllLinkedSections: sharedChoice?.CanApplyAtomically == true,
                    allLinkedSectionsExplanation: sharedChoice?.AtomicBlockReason ?? "The shared native record is unavailable.");
                if (scope == TerrainTexturePaintScopeChoice.Cancel)
                {
                    SetTerrainTexturePaintScopeCanceledStatus(brush, originalTargetTextureId);
                    return false;
                }
                if (scope == TerrainTexturePaintScopeChoice.AllLinkedSections)
                {
                    return await ApplyCrossLevelTerrainTexturePaintAsSharedReplacementAsync(
                        brush,
                        originalTargetTextureId,
                        sharedChoice);
                }
                return await AssignExistingPrivateRelocationToFaceAsync(
                    brush,
                    terrain,
                    reusablePrivateRelocation,
                    reuseChoice);
            }
        }

        int stagedPrivateRowCount = _nativeTerrainTextureRelocations
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .Select(edit => edit.TargetTextureId)
            .Distinct()
            .Count();
        if (AppendedPrivateTerrainTextureResearchGate.IsEnabled &&
            !AppendedPrivateTerrainTextureResearchGate.TryPlanResearchEditorAllocation(
                stagedPrivateRowCount,
                reuseExistingRecord: false,
                out _,
                out string researchAllocationReason))
        {
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                $"Selected Section cannot allocate another private texture row: " +
                $"{researchAllocationReason} No face or manifest was changed.",
                sharedChoice);
        }

        IReadOnlyList<TerrainTextureSlot> targetSlots;
        try
        {
            targetSlots = TerrainPatchExporter.InspectTextureSlots(sourceImage, _currentLevel);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
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
                entry.Readiness.RecordRole == TerrainTextureRecordRole.NativeUnreferencedStatic &&
                entry.Readiness.TargetRuntime.CanPersist)
            .OrderBy(slot => slot.TextureId)
            .ToArray();
        if (privateCandidates.Length == 0)
        {
            return await ApplyCrossLevelTerrainTexturePaintWithAppendedPrivateRecordAsync(
                brush,
                terrain,
                sourceImage,
                sourceLevel,
                originalTargetTextureId,
                affectedFaceCount,
                sharedChoice,
                targetCatalogError);
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
                    allowSelectedFaceLocalTextureTarget: true,
                    artOnlyPreserveTarget: true)
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
                    : firstBlockReason,
                sharedChoice);
        }


        TerrainTexturePaintScopeChoice selectedScope = await ChooseTerrainTexturePaintScopeAsync(
            brush,
            terrain,
            originalTargetTextureId,
            affectedFaceCount,
            canPaintSelectedSection: true,
            selectedSectionExplanation:
                $"Unused runtime-stable texture record {privateSlot.TextureId} passed the native relocation proof and can be assigned only to {terrain.RuntimeKey}.",
            canPaintAllLinkedSections: sharedChoice?.CanApplyAtomically == true,
            allLinkedSectionsExplanation: sharedChoice?.AtomicBlockReason ?? "The shared native record is unavailable.");
        if (selectedScope == TerrainTexturePaintScopeChoice.Cancel)
        {
            SetTerrainTexturePaintScopeCanceledStatus(brush, originalTargetTextureId);
            return false;
        }
        if (selectedScope == TerrainTexturePaintScopeChoice.AllLinkedSections)
        {
            return await ApplyCrossLevelTerrainTexturePaintAsSharedReplacementAsync(
                brush,
                originalTargetTextureId,
                sharedChoice);
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
                allowSelectedFaceLocalTextureTarget: true,
                artOnlyPreserveTarget: true)
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

    private async Task<bool> ApplyCrossLevelTerrainTexturePaintWithAppendedPrivateRecordAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        string sourceImage,
        LevelDefinition sourceLevel,
        int originalTargetTextureId,
        int affectedFaceCount,
        TerrainCrossLevelLookChoice? sharedChoice,
        string targetCatalogError)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return false;
        LevelDefinition targetLevel = _currentLevel;
        GeometryCandidate targetGeometry = _currentGeometry;

        TerrainTexturePaintScopeChoice requestedScope =
            await ChooseTerrainTexturePaintScopeAsync(
                brush,
                terrain,
                originalTargetTextureId,
                affectedFaceCount,
                canPaintSelectedSection: true,
                selectedSectionExplanation:
                    "The editor will now run the exact private-record, page-ownership, alias, " +
                    "capacity, and writer proof only if you choose this option. No face or " +
                    "manifest changes while that check runs.",
                canPaintAllLinkedSections: sharedChoice?.CanApplyAtomically == true,
                allLinkedSectionsExplanation:
                    sharedChoice?.CanApplyAtomically == true
                        ? $"Replace only native texture record {originalTargetTextureId}; exactly {affectedFaceCount:N0} linked terrain section(s) use it."
                        : sharedChoice?.AtomicBlockReason ?? "The shared native record is unavailable.");
        if (requestedScope == TerrainTexturePaintScopeChoice.Cancel)
        {
            SetTerrainTexturePaintScopeCanceledStatus(
                brush,
                originalTargetTextureId);
            return false;
        }
        if (requestedScope == TerrainTexturePaintScopeChoice.AllLinkedSections)
        {
            return await ApplyCrossLevelTerrainTexturePaintAsSharedReplacementAsync(
                brush,
                originalTargetTextureId,
                sharedChoice);
        }

        int proposedRowsForCapacity = checked(
            _nativeTerrainTextureRelocations
                .Where(edit => edit.UsesAppendedPrivateRecord)
                .Select(edit => edit.TargetTextureId)
                .Distinct()
                .Count() + 1);
        PrivateTexturePreflightOperationIdentity operationIdentity;
        try
        {
            operationIdentity = CapturePrivateTexturePreflightOperationIdentity(
                sourceImage,
                targetLevel);
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text = ex.Message;
            return false;
        }

        _statusText.Text =
            $"Checking whether only {terrain.RuntimeKey} can safely receive " +
            $"{brush.DisplayLabel}...";
        await Task.Yield();
        try
        {
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text = ex.Message;
            return false;
        }

        // The explicit research switch has a literal static allow set. Reject
        // known blocked/unknown destinations before running the structural
        // planner so the scope dialog keeps the actionable audit explanation.
        // Normal release cannot use this early level-only check because its
        // authorization is intentionally based on the exact source binding
        // and the writer selected by the planner below.
        if (AppendedPrivateTerrainTextureResearchGate.IsEnabled &&
            !AppendedPrivateTerrainTextureResearchGate.TryAllow(
                targetLevel.Key,
                out string researchDestinationReason))
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                researchDestinationReason);
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                $"Selected Section is unavailable for {targetLevel.DisplayName}: " +
                researchDestinationReason,
                sharedChoice);
        }

        NativeTerrainTextureRecordAppendSourceBinding binding;
        try
        {
            PrivateTextureFileStamp sourceStamp =
                CapturePrivateTextureFileStamp(sourceImage, "source BIN");
            binding = await GetPrivateTextureSourceBindingForStagingAsync(
                sourceStamp,
                targetLevel,
                operationIdentity);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                $"Selected Section is unavailable because {targetLevel.DisplayName}'s private texture table " +
                $"could not be bound to this BIN ({ex.Message}).",
                sharedChoice);
        }

        int materialTemplateTextureId = terrain.OriginalTextureId is >= 0 and < 128
            ? terrain.OriginalTextureId
            : originalTargetTextureId;
        if (materialTemplateTextureId < 0 ||
            materialTemplateTextureId >= binding.ExpectedSourceTextureCount)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                $"Face {terrain.RuntimeKey} no longer has a retail native material template.");
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                $"Selected Section is unavailable because face {terrain.RuntimeKey} no longer has a retail " +
                "native material template for a private texture row. Undo its current texture assignment first.",
                sharedChoice);
        }

        string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
        NativeTerrainTextureAppendedPrivateAllocation plannedAllocation;
        PrivateTextureFeasibilityResult feasibility;
        NativeTerrainTexturePrivateImageWriterKind writerKind;
        bool exactRejectionRecorded = false;
        try
        {
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            plannedAllocation = PlanAppendedPrivateTerrainTextureAllocation(
                binding,
                sourceLevel,
                brush.TextureId,
                materialTemplateTextureId);
            proposedRowsForCapacity = plannedAllocation.Edits.Count(edit =>
                edit.UsesAppendedPrivateRecord);
            string wadAnalysisPath = await EnsureSkyboxWadAnalysisAsync(sourceImage);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            feasibility = await GetAppendedPrivateTextureFeasibilityForStagingAsync(
                sourceImage,
                sourceCue,
                wadAnalysisPath,
                targetLevel,
                plannedAllocation.Edits,
                ordinaryLevelDataPatches: [],
                hasCustomImageTexturePatches: _customTerrainTextures.Any(import =>
                    targetGeometry.Polygons.Any(face =>
                        !face.IsTerrainRemoved && face.TextureId == import.TextureId)),
                operationIdentity: operationIdentity);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            if (!feasibility.Success)
            {
                RecordTerrainPrivateTextureCapacityRejection(
                    targetLevel.Key,
                    feasibility.AppendedPrivateEditCount);
                exactRejectionRecorded = true;
                throw new InvalidOperationException(feasibility.FailureReason);
            }
            writerKind = feasibility.WriterKind ??
                throw new InvalidOperationException(
                    "A successful private-texture feasibility result did not select a structural writer.");
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text = ex.Message;
            return false;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or OverflowException)
        {
            if (!exactRejectionRecorded)
            {
                RecordTerrainPrivateTextureCapacityIndeterminate(
                    targetLevel.Key,
                    proposedRowsForCapacity,
                    ex.Message);
            }
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                $"Selected Section is unavailable for this exact {targetLevel.DisplayName} <- {brush.DisplayLabel} pair: " +
                $"the exclusive global-packer preflight rejected it before any manifest or face was changed ({ex.Message}). " +
                "All Linked Sections remains optional only when the existing shared record passes its independent proof.",
                sharedChoice);
        }
        int nextPrivateTextureId = plannedAllocation.AssignedTextureId;
        RecordTerrainPrivateTextureCapacityPass(
            feasibility.SourceBinding,
            writerKind,
            feasibility.AppendedPrivateEditCount);
        if (!AppendedPrivateTerrainTextureResearchGate.TryAllow(
                feasibility.SourceBinding,
                writerKind,
                feasibility.AppendedPrivateEditCount,
                feasibility.StructuralGrowthPolicy,
                out string gateReason))
        {
            string privateSlotReason =
                "This level has no unused runtime-stable native texture record for a private face texture. " +
                "Only Selected Section needs one separate native record; All Linked Sections can still replace " +
                $"only texture record {originalTargetTextureId}, not every texture in the level. {gateReason}";
            return await ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
                brush,
                terrain,
                string.IsNullOrWhiteSpace(targetCatalogError)
                    ? privateSlotReason
                    : $"{targetCatalogError} {gateReason}",
                sharedChoice);
        }

        _statusText.Text =
            $"{gateReason} Private row T{nextPrivateTextureId} passed the exact " +
            $"{writerKind} preflight; applying only {terrain.RuntimeKey}...";
        try
        {
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text = ex.Message;
            return false;
        }

        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            operationIdentity.WorkspaceRootPath,
            targetLevel.Key);
        string terrainEditsPath = Path.Combine(
            operationIdentity.WorkspaceRootPath,
            $"{targetLevel.Key}-terrain-edits.json");
        string customTexturesPath = CustomTerrainTextureStore.ManifestPath(
            operationIdentity.WorkspaceRootPath,
            targetLevel.Key);
        string materialOverridesPath = Path.Combine(
            operationIdentity.WorkspaceRootPath,
            $"{targetLevel.Key}-terrain-material-overrides.json");
        Dictionary<string, byte[]?> filesBefore = new(StringComparer.OrdinalIgnoreCase)
        {
            [relocationPath] = File.Exists(relocationPath) ? File.ReadAllBytes(relocationPath) : null,
            [terrainEditsPath] = File.Exists(terrainEditsPath) ? File.ReadAllBytes(terrainEditsPath) : null,
            [customTexturesPath] = File.Exists(customTexturesPath) ? File.ReadAllBytes(customTexturesPath) : null,
            [materialOverridesPath] = File.Exists(materialOverridesPath) ? File.ReadAllBytes(materialOverridesPath) : null
        };
        TerrainTexturePaintStageSnapshot faceBefore =
            TerrainTexturePaintStageSnapshot.Capture(terrain);
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocationsBefore =
            _nativeTerrainTextureRelocations;
        IReadOnlyList<CustomTerrainTextureImport> customTexturesBefore =
            _customTerrainTextures;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;

        string generatedDirectory = Path.Combine(
            operationIdentity.WorkspaceRootPath,
            "_local",
            "custom-textures",
            "generated");
        Directory.CreateDirectory(generatedDirectory);
        string previewName =
            $"{SafeFilePart(targetLevel.Key)}-private-from-" +
            $"{SafeFilePart(sourceLevel.Key)}-{brush.TextureId:000}-" +
            $"{DateTime.Now:yyyyMMdd-HHmmssfff}.png";
        string previewPath = Path.Combine(generatedDirectory, previewName);
        filesBefore[previewPath] = null;

        try
        {
            TerrainTextureImageExport? preview =
                await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
                    sourceImage,
                    sourceLevel,
                    brush.TextureId,
                    previewPath,
                    "hqDataClose");
            if (preview == null || !File.Exists(previewPath))
            {
                throw new InvalidDataException(
                    $"Could not decode {brush.DisplayLabel}'s close-detail native art for a truthful preview.");
            }
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);

            NativeTerrainTextureAppendedPrivateAllocation allocation =
                await NativeTerrainTextureRelocationEditStore.AddOrReuseAppendedPrivateArtOnlyAsync(
                    operationIdentity.WorkspaceRootPath,
                    targetLevel.Key,
                    targetLevel.DisplayName,
                    sourceLevel.Key,
                    sourceLevel.DisplayName,
                    sourceLevel.SourceWadEntry,
                    brush.TextureId,
                    binding.TargetWadEntry,
                    binding.SourceImageSha256,
                    binding.ExpectedSourceTextureCount,
                    binding.ExpectedTextureComponentSha256,
                    binding.ExpectedLevelDataSha256,
                    materialTemplateTextureId,
                    previewPath,
                    previewName);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            if (allocation.AssignedTextureId != plannedAllocation.AssignedTextureId ||
                allocation.ReusedExistingRecord != plannedAllocation.ReusedExistingRecord ||
                allocation.AddedNewRecord != plannedAllocation.AddedNewRecord)
            {
                throw new InvalidDataException(
                    "The saved private texture allocation changed after its exact donor/destination batch preflight. The stale result was rejected.");
            }
            _nativeTerrainTextureRelocations = allocation.Edits;
            if (allocation.ReusedExistingRecord)
            {
                DeleteManagedNativeTerrainTexturePreview(previewPath);
                filesBefore.Remove(previewPath);
            }

            terrain.ApplyTextureOverride(allocation.AssignedTextureId);
            terrain.ClearTextureVisualEdit();
            terrain.ClearSurfaceBehaviorEdit();
            TerrainTexturePaintPersistenceFaultForTesting?.Invoke(terrainEditsPath);
            int savedEdits = await PersistCurrentTerrainEditsAsync();
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            int previewFaces = ApplyCustomTerrainTexturePreviews();
            _viewport.NotifyTerrainPresentationDataChanged();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(
                _selectedTerrainIndex,
                terrain));
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            _statusText.Text =
                $"Painted only face {terrain.RuntimeKey} with {brush.DisplayLabel} through " +
                $"{(allocation.ReusedExistingRecord ? "reused" : "new")} private texture T{allocation.AssignedTextureId}; " +
                $"retail material template T{materialTemplateTextureId} and every linked neighbor stayed unchanged. " +
                $"Previewed {previewFaces:N0} face(s), saved {savedEdits:N0} terrain edit(s). " +
                "The guarded Create BIN path will re-run the global packer and exact final readback.";
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or OverflowException)
        {
            foreach ((string path, byte[]? content) in filesBefore)
            {
                if (content == null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(path) ?? operationIdentity.WorkspaceRootPath);
                    File.WriteAllBytes(path, content);
                }
            }
            faceBefore.Restore();
            _nativeTerrainTextureRelocations = relocationsBefore;
            _customTerrainTextures = customTexturesBefore;
            _loadedTerrainEdits = loadedTerrainEditsBefore;
            _savedTerrainEditSignature = savedTerrainSignatureBefore;
            ApplyCustomTerrainTexturePreviews();
            _viewport.NotifyTerrainPresentationDataChanged();
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(
                _selectedTerrainIndex,
                terrain));
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            EditorDiagnostics.RecordException(
                "allocating an appended-private cross-level terrain texture",
                ex);
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                proposedRowsForCapacity,
                ex.Message);
            _statusText.Text =
                $"Could not allocate a private row for {brush.DisplayLabel}: {ex.Message} " +
                "The face, manifests, preview, and prior terrain edits were restored; no terrain changed.";
            return false;
        }
    }

    private async Task<bool> TryAllowExistingAppendedPrivateTerrainTextureForStagingAsync(
        string sourceImage)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return false;
        LevelDefinition targetLevel = _currentLevel;
        GeometryCandidate targetGeometry = _currentGeometry;
        int stagedRows = _nativeTerrainTextureRelocations
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .Select(edit => edit.TargetTextureId)
            .Distinct()
            .Count();

        try
        {
            PrivateTexturePreflightOperationIdentity operationIdentity =
                CapturePrivateTexturePreflightOperationIdentity(
                    sourceImage,
                    targetLevel);
            string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
            string wadAnalysisPath = await EnsureSkyboxWadAnalysisAsync(sourceImage);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            PrivateTextureFeasibilityResult feasibility =
                await GetAppendedPrivateTextureFeasibilityForStagingAsync(
                    sourceImage,
                    sourceCue,
                    wadAnalysisPath,
                    targetLevel,
                    _nativeTerrainTextureRelocations,
                    ordinaryLevelDataPatches: [],
                    hasCustomImageTexturePatches: _customTerrainTextures.Any(import =>
                        targetGeometry.Polygons.Any(face =>
                            !face.IsTerrainRemoved && face.TextureId == import.TextureId)),
                    operationIdentity: operationIdentity);
            EnsurePrivateTexturePreflightOperationIsCurrent(operationIdentity);
            if (!feasibility.Success)
            {
                RecordTerrainPrivateTextureCapacityRejection(
                    targetLevel.Key,
                    feasibility.AppendedPrivateEditCount);
                return false;
            }
            if (feasibility.WriterKind == null)
            {
                RecordTerrainPrivateTextureCapacityIndeterminate(
                    targetLevel.Key,
                    feasibility.AppendedPrivateEditCount,
                    "The successful private-texture check did not identify a structural writer.");
                return false;
            }
            return AppendedPrivateTerrainTextureResearchGate.TryAllow(
                feasibility.SourceBinding,
                feasibility.WriterKind.Value,
                feasibility.AppendedPrivateEditCount,
                feasibility.StructuralGrowthPolicy,
                out _);
        }
        catch (StalePrivateTexturePreflightException ex)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                stagedRows,
                ex.Message);
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException or OverflowException)
        {
            RecordTerrainPrivateTextureCapacityIndeterminate(
                targetLevel.Key,
                stagedRows,
                ex.Message);
            return false;
        }
    }

    private NativeTerrainTextureAppendedPrivateAllocation
        PlanAppendedPrivateTerrainTextureAllocation(
            NativeTerrainTextureRecordAppendSourceBinding binding,
            LevelDefinition sourceLevel,
            int donorTextureId,
            int materialTemplateTextureId)
    {
        if (_currentLevel == null)
            throw new InvalidOperationException("A destination level is required for a private texture allocation.");

        IReadOnlyList<NativeTerrainTextureRelocationEdit> current =
            NativeTerrainTextureRelocationEditStore.Load(
                _workspace.RootPath,
                _currentLevel.Key);
        NativeTerrainTextureAppendedPrivateCompaction compaction =
            NativeTerrainTextureRelocationEditStore.CompactAppendedPrivateRecords(
                current);
        if (compaction.Changed)
        {
            throw new InvalidDataException(
                "The saved private texture rows require face-ID compaction before another donor can be planned.");
        }
        int currentPrivateRowCount =
            current.Count(edit => edit.UsesAppendedPrivateRecord);

        NativeTerrainTextureRelocationEdit? reusable = current.FirstOrDefault(edit =>
            edit.UsesAppendedPrivateRecord &&
            edit.DonorWadEntry == sourceLevel.SourceWadEntry &&
            edit.DonorTextureId == donorTextureId &&
            edit.MaterialTemplateTextureId == materialTemplateTextureId &&
            edit.TargetWadEntry == binding.TargetWadEntry &&
            edit.SourceTextureRecordCount == binding.ExpectedSourceTextureCount &&
            string.Equals(edit.SourceImageSha256, binding.SourceImageSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edit.SourceTextureComponentSha256, binding.ExpectedTextureComponentSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edit.SourceLevelDataSha256, binding.ExpectedLevelDataSha256, StringComparison.OrdinalIgnoreCase));
        if (reusable != null)
        {
            if (AppendedPrivateTerrainTextureResearchGate.IsEnabled &&
                !AppendedPrivateTerrainTextureResearchGate.TryPlanResearchEditorAllocation(
                    currentPrivateRowCount,
                    reuseExistingRecord: true,
                    out _,
                    out string reuseLimitReason))
            {
                throw new InvalidOperationException(reuseLimitReason);
            }
            return new NativeTerrainTextureAppendedPrivateAllocation(
                current,
                reusable.TargetTextureId,
                ReusedExistingRecord: true,
                AddedNewRecord: false);
        }
        if (AppendedPrivateTerrainTextureResearchGate.IsEnabled &&
            !AppendedPrivateTerrainTextureResearchGate.TryPlanResearchEditorAllocation(
                currentPrivateRowCount,
                reuseExistingRecord: false,
                out _,
                out string allocationLimitReason))
        {
            throw new InvalidOperationException(allocationLimitReason);
        }

        int assignedTextureId = checked(
            binding.ExpectedSourceTextureCount +
            currentPrivateRowCount);
        if (assignedTextureId > 127)
        {
            throw new InvalidOperationException(
                "No seven-bit native terrain texture id remains for another private record.");
        }

        string normalizedSourceKey = LevelCatalog.NormalizeKey(sourceLevel.Key);
        NativeTerrainTextureRelocationEdit candidate = new(
            assignedTextureId,
            normalizedSourceKey,
            sourceLevel.DisplayName.Trim(),
            sourceLevel.SourceWadEntry,
            donorTextureId,
            NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
                normalizedSourceKey,
                donorTextureId),
            NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
            "",
            "",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
            NativeTerrainTextureTargetRecordKind.AppendedPrivate,
            binding.TargetWadEntry,
            binding.SourceImageSha256,
            binding.ExpectedSourceTextureCount,
            binding.ExpectedTextureComponentSha256,
            binding.ExpectedLevelDataSha256,
            materialTemplateTextureId,
            NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(
                assignedTextureId));
        IReadOnlyList<NativeTerrainTextureRelocationEdit> planned = current
            .Append(candidate)
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
        return new NativeTerrainTextureAppendedPrivateAllocation(
            planned,
            assignedTextureId,
            ReusedExistingRecord: false,
            AddedNewRecord: true);
    }

    private async Task<bool> ApplyCrossLevelTerrainTexturePaintAsSharedFallbackAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        string privateSlotReason,
        TerrainCrossLevelLookChoice? auditedSharedChoice = null)
    {
        if (_currentGeometry == null)
            return false;

        int targetTextureId = terrain.TextureId;
        int affectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == targetTextureId);
        TerrainCrossLevelLookChoice? sharedChoice = auditedSharedChoice ??
            BuildCrossLevelTerrainLookChoices(
                    terrain.Surface,
                    brush,
                    artOnlyPreserveTarget: true)
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

        TerrainTexturePaintScopeChoice scope = await ChooseTerrainTexturePaintScopeAsync(
            brush,
            terrain,
            targetTextureId,
            affectedFaceCount,
            canPaintSelectedSection: false,
            selectedSectionExplanation: privateSlotReason,
            canPaintAllLinkedSections: true,
            allLinkedSectionsExplanation:
                $"Replace only native texture record {targetTextureId}; exactly {affectedFaceCount:N0} linked terrain section(s) use it.");
        if (scope != TerrainTexturePaintScopeChoice.AllLinkedSections)
        {
            SetTerrainTexturePaintScopeCanceledStatus(brush, targetTextureId);
            return false;
        }

        return await ApplyCrossLevelTerrainTexturePaintAsSharedReplacementAsync(
            brush,
            targetTextureId,
            sharedChoice);
    }

    private async Task<bool> ApplyCrossLevelTerrainTexturePaintAsSharedReplacementAsync(
        TerrainTexturePaintBrush brush,
        int targetTextureId,
        TerrainCrossLevelLookChoice? sharedChoice)
    {
        if (sharedChoice?.CanApplyAtomically != true)
        {
            _statusText.Text =
                $"Shared texture {targetTextureId} cannot receive {brush.DisplayLabel}: " +
                $"{sharedChoice?.AtomicBlockReason ?? "the source is no longer available."} No terrain changed.";
            return false;
        }

        await ApplySelectedTerrainCrossLevelLookAsync(sharedChoice);
        return _nativeTerrainTextureRelocations.Any(edit =>
            edit.TargetTextureId == targetTextureId && brush.Matches(edit));
    }

    private void SetTerrainTexturePaintScopeCanceledStatus(
        TerrainTexturePaintBrush brush,
        int targetTextureId)
    {
        _statusText.Text =
            $"Canceled {brush.DisplayLabel} on texture {targetTextureId}; no terrain changed and paint mode remains active.";
    }

    private async Task<TerrainTexturePaintScopeChoice> ChooseTerrainTexturePaintScopeAsync(
        TerrainTexturePaintBrush brush,
        TerrainPolygon terrain,
        int targetTextureId,
        int affectedFaceCount,
        bool canPaintSelectedSection,
        string selectedSectionExplanation,
        bool canPaintAllLinkedSections,
        string allLinkedSectionsExplanation)
    {
        if (affectedFaceCount <= 1)
        {
            if (canPaintSelectedSection)
                return TerrainTexturePaintScopeChoice.SelectedSection;
            if (canPaintAllLinkedSections)
                return TerrainTexturePaintScopeChoice.AllLinkedSections;
            return TerrainTexturePaintScopeChoice.Cancel;
        }

        if (TerrainTexturePaintScopeConfirmationForTesting != null)
        {
            TerrainTexturePaintScopeChoice tested = await TerrainTexturePaintScopeConfirmationForTesting(
                brush.DisplayLabel,
                targetTextureId,
                affectedFaceCount,
                canPaintSelectedSection,
                selectedSectionExplanation);
            if (tested == TerrainTexturePaintScopeChoice.SelectedSection && !canPaintSelectedSection)
                throw new InvalidOperationException("The texture-paint scope test selected an unavailable private-section option.");
            if (tested == TerrainTexturePaintScopeChoice.AllLinkedSections && !canPaintAllLinkedSections)
                throw new InvalidOperationException("The texture-paint scope test selected an unavailable shared-section option.");
            return tested;
        }

        Window dialog = new()
        {
            Title = "Choose Texture Paint Scope",
            Width = 680,
            Height = 490,
            MinWidth = 600,
            MinHeight = 440,
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
            Text = _releaseMode
                ? $"This texture appears in {affectedFaceCount:N0} terrain sections"
                : $"Texture {targetTextureId} is used by {affectedFaceCount:N0} terrain sections",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
        });
        content.Children.Add(new TextBlock
        {
            Text = _releaseMode
                ? $"Choose whether {brush.DisplayLabel} should change only the section you clicked, or every matching section that currently shares this texture. " +
                  "Linked Sections does not replace every texture in the level."
                : $"Choose whether {brush.DisplayLabel} should change only selected section {terrain.RuntimeKey}, or every section linked to the same native texture record. " +
                  "All linked sections does not replace every texture in the level.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 67, 78)),
            LineHeight = 19
        });

        Button selectedSection = NewButton("Only Selected Section");
        selectedSection.IsEnabled = canPaintSelectedSection;
        selectedSection.Click += (_, _) => dialog.Close(TerrainTexturePaintScopeChoice.SelectedSection);
        StackPanel selectedContent = new() { Spacing = 5 };
        selectedContent.Children.Add(selectedSection);
        selectedContent.Children.Add(new TextBlock
        {
            Text = _releaseMode
                ? canPaintSelectedSection
                    ? "Changes only the terrain section you clicked."
                    : "Unavailable: this level has no free single-section texture slot for this edit."
                : canPaintSelectedSection
                    ? selectedSectionExplanation
                    : $"Unavailable: {selectedSectionExplanation}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(canPaintSelectedSection
                ? Color.FromRgb(56, 91, 67)
                : Color.FromRgb(151, 63, 63)),
            FontSize = 12,
            LineHeight = 17
        });
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(canPaintSelectedSection
                ? Color.FromRgb(238, 248, 240)
                : Color.FromRgb(246, 240, 240)),
            BorderBrush = new SolidColorBrush(canPaintSelectedSection
                ? Color.FromRgb(162, 204, 170)
                : Color.FromRgb(211, 172, 172)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = selectedContent
        });

        Button allLinkedSections = NewButton($"All {affectedFaceCount:N0} Linked Sections");
        allLinkedSections.IsEnabled = canPaintAllLinkedSections;
        allLinkedSections.Click += (_, _) => dialog.Close(TerrainTexturePaintScopeChoice.AllLinkedSections);
        StackPanel allContent = new() { Spacing = 5 };
        allContent.Children.Add(allLinkedSections);
        allContent.Children.Add(new TextBlock
        {
            Text = _releaseMode
                ? canPaintAllLinkedSections
                    ? $"Changes the {affectedFaceCount:N0} matching sections that currently share this texture."
                    : "Unavailable: this texture cannot safely be replaced on the selected disc."
                : canPaintAllLinkedSections
                    ? allLinkedSectionsExplanation
                    : $"Unavailable: {allLinkedSectionsExplanation}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(canPaintAllLinkedSections
                ? Color.FromRgb(56, 67, 78)
                : Color.FromRgb(151, 63, 63)),
            FontSize = 12,
            LineHeight = 17
        });
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(canPaintAllLinkedSections
                ? Color.FromRgb(241, 246, 251)
                : Color.FromRgb(246, 240, 240)),
            BorderBrush = new SolidColorBrush(canPaintAllLinkedSections
                ? Color.FromRgb(174, 195, 215)
                : Color.FromRgb(211, 172, 172)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = allContent
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        Button cancel = NewButton("Cancel");
        cancel.Click += (_, _) => dialog.Close(TerrainTexturePaintScopeChoice.Cancel);
        buttons.Children.Add(cancel);
        content.Children.Add(buttons);
        dialog.Content = content;
        // Paint-mode safety disables the main editor while the native proof is
        // running. Avalonia still needs an enabled owner when it opens the
        // explicit shared-record confirmation; the modal immediately owns and
        // disables this window again for the lifetime of the dialog.
        bool ownerWasEnabled = IsEnabled;
        if (!ownerWasEnabled)
            IsEnabled = true;
        try
        {
            return await dialog.ShowDialog<TerrainTexturePaintScopeChoice>(this);
        }
        finally
        {
            IsEnabled = ownerWasEnabled;
        }
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
                $"Painted only face {terrain.RuntimeKey} with {brush.DisplayLabel} by reusing loaded texture record {relocation.TargetTextureId}; " +
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
        NativeTerrainTextureRelocationEdit? relocation = _nativeTerrainTextureRelocations
            .FirstOrDefault(edit => edit.TargetTextureId == textureId);
        if (relocation != null)
        {
            TerrainTexturePaintHistoryCheckpoint? historyCheckpoint =
                CaptureTerrainTexturePaintHistoryCheckpoint(
                    $"Undo selected texture {textureId} on {_selectedTerrain.RuntimeKey}");
            string materialOverridesPath = Path.Combine(
                _workspace.RootPath,
                $"{_currentLevel.Key}-terrain-material-overrides.json");
            byte[]? materialOverridesBefore = !relocation.PreservesTargetNativeSurface &&
                File.Exists(materialOverridesPath)
                    ? File.ReadAllBytes(materialOverridesPath)
                    : null;
            bool materialOverridesExisted = File.Exists(materialOverridesPath);
            // The existing relocation undo is already scoped to the shared
            // texture record and its transferred native visual/property data.
            // It does not reset face height, XY, or structure edits.
            string selectedRuntimeKey = _selectedTerrain.RuntimeKey;
            try
            {
                await UndoSelectedTerrainAsync();
            }
            catch (Exception ex) when (
                historyCheckpoint != null &&
                ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                // Legacy ArtAndNativeSurface relocations can update the
                // material override file before a later terrain save fails.
                // Restore that operation-local preimage before replaying the
                // texture-only history checkpoint, so its classifier sees the
                // same material state as the faces it is restoring.
                if (!relocation.PreservesTargetNativeSurface)
                {
                    if (!materialOverridesExisted)
                    {
                        if (File.Exists(materialOverridesPath))
                            File.Delete(materialOverridesPath);
                    }
                    else if (materialOverridesBefore != null)
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(materialOverridesPath) ?? _workspace.RootPath);
                        File.WriteAllBytes(materialOverridesPath, materialOverridesBefore);
                    }
                }
                _terrainTexturePaintHistoryRestoreBusy = true;
                try
                {
                    await RestoreTerrainTexturePaintHistoryCheckpointAsync(historyCheckpoint);
                }
                finally
                {
                    _terrainTexturePaintHistoryRestoreBusy = false;
                }
                _statusText.Text =
                    $"Could not undo selected texture {textureId}: {ex.Message} " +
                    "The relocation manifest, preview, and face-local texture state were restored.";
                EditorDiagnostics.RecordException("undoing selected terrain texture paint", ex);
                RefreshTerrainTexturePaintModeUi();
                return;
            }
            bool relocationRemains = _nativeTerrainTextureRelocations.Any(edit =>
                RepresentsSameStagedTerrainTexture(edit, relocation));
            NativeTerrainTextureRelocationEdit[] otherPaints = _nativeTerrainTextureRelocations
                .Where(edit => !RepresentsSameStagedTerrainTexture(edit, relocation))
                .ToArray();
            int otherPaintCount = otherPaints.Length;
            string remaining = otherPaintCount == 0
                ? " No other cross-level texture paints remain staged."
                : $" {otherPaintCount:N0} other cross-level texture paint(s) remain staged on texture record(s) " +
                  $"{string.Join(", ", otherPaints.Select(edit => edit.TargetTextureId))}; " +
                  "they still participate in native texture-space safety checks.";
            if (!relocationRemains)
            {
                if (string.Equals(_lastTerrainTexturePaintFace, selectedRuntimeKey, StringComparison.OrdinalIgnoreCase))
                    _lastTerrainTexturePaintFace = "";
                _statusText.Text = $"{_statusText.Text}{remaining}";
                EditorDiagnostics.RecordAction(
                    "Terrain texture paint undo",
                    $"Removed target texture {textureId} <- {relocation.DonorLevelName} texture {relocation.DonorTextureId}; " +
                    $"{otherPaintCount} other cross-level texture paint(s) remain staged.");
            }
            else
            {
                EditorDiagnostics.RecordAction(
                    "Terrain texture paint undo",
                    $"Removed one face-local use of private target texture {textureId}; the private relocation remains loaded for another face.");
            }
            if (historyCheckpoint != null)
                PushTerrainTexturePaintHistory(historyCheckpoint);
            RefreshTerrainTexturePaintModeUi();
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

    private async Task ManageStagedTerrainTexturesAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before managing staged cross-level textures.";
            return;
        }

        ListBox list = new()
        {
            MinHeight = 250,
            ItemTemplate = new FuncDataTemplate<StagedTerrainTextureListItem>((item, _) =>
            {
                if (item == null)
                    return new TextBlock();
                return new StackPanel
                {
                    Spacing = 3,
                    Margin = new Thickness(5, 4),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Target texture {item.Edit.TargetTextureId}  <-  {item.Edit.DonorLevelName} texture {item.Edit.DonorTextureId}",
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(34, 45, 56))
                        },
                        new TextBlock
                        {
                            Text = item.Edit.PreservesTargetNativeSurface
                                ? "Art only; target material, tint, and collision stay native"
                                : "Art and native surface properties",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(82, 91, 101))
                        }
                    }
                };
            })
        };
        TextBlock summary = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(56, 67, 78)),
            LineHeight = 18
        };
        TextBlock result = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 71, 106)),
            LineHeight = 18,
            MinHeight = 38
        };
        Button removeSelected = NewButton("Remove Selected Staged Texture");
        Button removeMostRecent = NewButton("Remove Most Recently Staged Target");
        Button undoLast = NewButton("Undo Last Texture Action");
        Button close = NewButton("Close");

        StagedTerrainTextureListItem[] ReadItems() => _nativeTerrainTextureRelocations
            .OrderBy(edit => edit.TargetTextureId)
            .Select(edit => new StagedTerrainTextureListItem(edit))
            .ToArray();

        void RefreshList(int? preferredTargetTextureId = null)
        {
            StagedTerrainTextureListItem[] items = ReadItems();
            list.ItemsSource = items;
            int selection = preferredTargetTextureId.HasValue
                ? Array.FindIndex(items, item => item.Edit.TargetTextureId == preferredTargetTextureId.Value)
                : 0;
            list.SelectedIndex = items.Length == 0 ? -1 : Math.Max(0, selection);
            summary.Text = items.Length == 0
                ? "No cross-level texture records are staged in this level."
                : $"{items.Length:N0} cross-level texture record(s) are staged. Removing one restores that target record's native art and any private face assignments while preserving terrain height, XY, structure, and unrelated texture paints.";
            removeSelected.IsEnabled = items.Length > 0;
            removeMostRecent.IsEnabled = items.Length > 0;
            undoLast.IsEnabled = TerrainTexturePaintHistoryCountForCurrentLevel() > 0;
        }

        async Task RemoveAsync(NativeTerrainTextureRelocationEdit edit)
        {
            removeSelected.IsEnabled = false;
            removeMostRecent.IsEnabled = false;
            undoLast.IsEnabled = false;
            close.IsEnabled = false;
            result.Text = $"Removing target texture {edit.TargetTextureId} safely...";
            bool removed = await RemoveStagedTerrainTextureAsync(edit);
            result.Text = _statusText.Text ?? "";
            RefreshList();
            close.IsEnabled = true;
            if (!removed)
            {
                removeSelected.IsEnabled = _nativeTerrainTextureRelocations.Count > 0;
                removeMostRecent.IsEnabled = _nativeTerrainTextureRelocations.Count > 0;
                undoLast.IsEnabled = TerrainTexturePaintHistoryCountForCurrentLevel() > 0;
            }
        }

        removeSelected.Click += async (_, _) =>
        {
            if (list.SelectedItem is StagedTerrainTextureListItem selected)
                await RemoveAsync(selected.Edit);
        };
        removeMostRecent.Click += async (_, _) =>
        {
            NativeTerrainTextureRelocationEdit? newest = _nativeTerrainTextureRelocations
                .OrderByDescending(edit => ParseTerrainTextureCreatedAt(edit.CreatedAt))
                .ThenByDescending(edit => edit.TargetTextureId)
                .FirstOrDefault();
            if (newest != null)
                await RemoveAsync(newest);
        };
        undoLast.Click += async (_, _) =>
        {
            removeSelected.IsEnabled = false;
            removeMostRecent.IsEnabled = false;
            undoLast.IsEnabled = false;
            close.IsEnabled = false;
            await UndoLastTerrainTextureActionAsync();
            result.Text = _statusText.Text ?? "";
            RefreshList();
            close.IsEnabled = true;
        };

        Window dialog = new()
        {
            Title = "Manage Staged Cross-Level Textures",
            Width = 720,
            Height = 560,
            MinWidth = 620,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        close.Click += (_, _) => dialog.Close();
        WrapPanel buttons = new()
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            ItemSpacing = 8,
            LineSpacing = 8,
            Children = { removeSelected, removeMostRecent, undoLast, close }
        };
        RefreshList();
        dialog.Content = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = $"Staged textures in {_currentLevel.DisplayName}",
                    FontSize = 19,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(32, 38, 45))
                },
                new TextBlock
                {
                    Text = "Use this list when a new combination reaches the PlayStation's native texture-space limit. Removal is transactional: if saving fails, the prior staged texture and preview are restored.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 67, 78)),
                    LineHeight = 18
                },
                summary,
                list,
                result,
                buttons
            }
        };
        await dialog.ShowDialog(this);
    }

    private async Task<bool> RemoveStagedTerrainTextureAsync(
        NativeTerrainTextureRelocationEdit edit)
    {
        if (_currentLevel == null || _currentGeometry == null ||
            !_nativeTerrainTextureRelocations.Any(candidate => candidate.TargetTextureId == edit.TargetTextureId))
        {
            _statusText.Text = $"Target texture {edit.TargetTextureId} is no longer staged; nothing was removed.";
            return false;
        }

        TerrainTexturePaintHistoryCheckpoint? historyCheckpoint =
            CaptureTerrainTexturePaintHistoryCheckpoint(
                $"Remove staged target texture {edit.TargetTextureId}");

        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        string terrainEditsPath = Path.Combine(
            _workspace.RootPath,
            $"{_currentLevel.Key}-terrain-edits.json");
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{_currentLevel.Key}-terrain-material-overrides.json");
        Dictionary<string, byte[]?> filesBefore = new(StringComparer.OrdinalIgnoreCase)
        {
            [relocationPath] = File.Exists(relocationPath) ? File.ReadAllBytes(relocationPath) : null,
            [terrainEditsPath] = File.Exists(terrainEditsPath) ? File.ReadAllBytes(terrainEditsPath) : null,
            [materialOverridesPath] = File.Exists(materialOverridesPath) ? File.ReadAllBytes(materialOverridesPath) : null,
            [edit.PreviewImagePath] = !string.IsNullOrWhiteSpace(edit.PreviewImagePath) && File.Exists(edit.PreviewImagePath)
                ? File.ReadAllBytes(edit.PreviewImagePath)
                : null
        };
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocationsBefore =
            _nativeTerrainTextureRelocations;
        TerrainTexturePaintStageSnapshot[] facesBefore = _currentGeometry.Polygons
            .Select(TerrainTexturePaintStageSnapshot.Capture)
            .ToArray();
        TerrainPolygon? selectedBefore = _selectedTerrain;
        int selectedIndexBefore = _selectedTerrainIndex;
        int loadedTerrainEditsBefore = _loadedTerrainEdits;
        string savedTerrainSignatureBefore = _savedTerrainEditSignature;

        try
        {
            TerrainPolygon[] privatePaintFaces = _currentGeometry.Polygons
                .Where(face =>
                    !face.IsTerrainRemoved &&
                    face.TextureId == edit.TargetTextureId &&
                    face.OriginalTextureId >= 0 &&
                    face.OriginalTextureId != edit.TargetTextureId &&
                    face.HasTextureEdit)
                .ToArray();
            _nativeTerrainTextureRelocations =
                await RemoveNativeTerrainTextureRelocationAndCompactAsync(edit);
            if (!edit.PreservesTargetNativeSurface)
            {
                foreach (TerrainPolygon face in _currentGeometry.Polygons.Where(face =>
                             !face.IsTerrainRemoved && face.TextureId == edit.TargetTextureId))
                {
                    face.ClearSurfaceBehaviorEdit();
                    face.ClearTextureVisualEdit();
                }
                await TerrainMaterialClassifier.SaveOverrideAsync(
                    _currentLevel.Key,
                    _workspace.RootPath,
                    edit.TargetTextureId,
                    "unknown");
            }
            foreach (TerrainPolygon privatePaintFace in privatePaintFaces)
            {
                privatePaintFace.ClearSurfaceBehaviorEdit();
                privatePaintFace.ClearTextureVisualEdit();
                privatePaintFace.ApplyTextureOverride(privatePaintFace.OriginalTextureId);
            }
            TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
            ApplyCustomTerrainTexturePreviews();
            if (!edit.PreservesTargetNativeSurface || privatePaintFaces.Length > 0)
                await PersistCurrentTerrainEditsAsync();
            if (!_nativeTerrainTextureRelocations.Any(candidate =>
                    string.Equals(candidate.PreviewImagePath, edit.PreviewImagePath, StringComparison.OrdinalIgnoreCase)))
            {
                DeleteManagedNativeTerrainTexturePreview(edit.PreviewImagePath);
            }
            _viewport.NotifyTerrainPresentationDataChanged();
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            if (selectedBefore != null && selectedIndexBefore >= 0 &&
                selectedIndexBefore < _currentGeometry.Polygons.Count &&
                ReferenceEquals(_currentGeometry.Polygons[selectedIndexBefore], selectedBefore))
            {
                _selectedTerrain = selectedBefore;
                _selectedTerrainIndex = selectedIndexBefore;
                ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(selectedIndexBefore, selectedBefore));
            }
            int remaining = _nativeTerrainTextureRelocations.Count;
            _statusText.Text =
                $"Removed staged target texture {edit.TargetTextureId} <- {edit.DonorLevelName} texture {edit.DonorTextureId}. " +
                $"{remaining:N0} cross-level texture paint(s) remain staged; terrain height, XY, structure, and unrelated textures were preserved.";
            EditorDiagnostics.RecordAction(
                "Managed staged terrain texture removal",
                $"Removed target texture {edit.TargetTextureId}; {remaining} other cross-level texture paint(s) remain.");
            if (historyCheckpoint != null)
                PushTerrainTexturePaintHistory(historyCheckpoint);
            RefreshTerrainTexturePaintModeUi();
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            foreach ((string path, byte[]? content) in filesBefore)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;
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
            _nativeTerrainTextureRelocations = relocationsBefore;
            _loadedTerrainEdits = loadedTerrainEditsBefore;
            _savedTerrainEditSignature = savedTerrainSignatureBefore;
            foreach (TerrainTexturePaintStageSnapshot snapshot in facesBefore)
                snapshot.Restore();
            TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
            foreach (TerrainTexturePaintStageSnapshot snapshot in facesBefore)
                snapshot.Restore();
            ApplyCustomTerrainTexturePreviews();
            _selectedTerrain = selectedBefore;
            _selectedTerrainIndex = selectedIndexBefore;
            _viewport.NotifyTerrainPresentationDataChanged();
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            if (selectedBefore != null && selectedIndexBefore >= 0 &&
                selectedIndexBefore < _currentGeometry.Polygons.Count)
            {
                ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(selectedIndexBefore, selectedBefore));
            }
            _statusText.Text =
                $"Could not remove staged target texture {edit.TargetTextureId}: {ex.Message} " +
                "The relocation manifest, preview, terrain edits, and selection were restored.";
            EditorDiagnostics.RecordException("removing a managed staged terrain texture", ex);
            RefreshTerrainTexturePaintModeUi();
            return false;
        }
    }

    /// <summary>
    /// Appended native texture ids are physical table indexes, so removing a
    /// middle row must compact both the manifest and every face assignment in
    /// one transaction. Callers already snapshot the manifest and all faces;
    /// an exception therefore restores both sides instead of leaving a stale
    /// T-number behind. Existing-native relocations keep their original path.
    /// </summary>
    private async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>>
        RemoveNativeTerrainTextureRelocationAndCompactAsync(
            NativeTerrainTextureRelocationEdit edit)
    {
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("A level and its terrain must be loaded before removing a staged texture.");

        if (!edit.UsesAppendedPrivateRecord)
        {
            return await NativeTerrainTextureRelocationEditStore.RemoveAsync(
                _workspace.RootPath,
                _currentLevel.Key,
                _currentLevel.DisplayName,
                edit.TargetTextureId);
        }

        NativeTerrainTextureRelocationEdit[] remaining = _nativeTerrainTextureRelocations
            .Where(candidate => candidate.TargetTextureId != edit.TargetTextureId)
            .ToArray();
        NativeTerrainTextureAppendedPrivateCompaction compaction =
            NativeTerrainTextureRelocationEditStore.CompactAppendedPrivateRecords(remaining);
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (face.IsTerrainRemoved ||
                !compaction.TextureIdRemap.TryGetValue(face.TextureId, out int compactedId) ||
                compactedId == face.TextureId)
            {
                continue;
            }
            face.ApplyTextureOverride(compactedId);
        }

        string manifestPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        if (compaction.Edits.Count == 0)
        {
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
        }
        else
        {
            await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
                manifestPath,
                _currentLevel.Key,
                _currentLevel.DisplayName,
                compaction.Edits);
        }
        return compaction.Edits;
    }

    private static DateTimeOffset ParseTerrainTextureCreatedAt(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private static bool RepresentsSameStagedTerrainTexture(
        NativeTerrainTextureRelocationEdit candidate,
        NativeTerrainTextureRelocationEdit original)
    {
        if (candidate.TargetRecordKind != original.TargetRecordKind ||
            candidate.DonorWadEntry != original.DonorWadEntry ||
            candidate.DonorTextureId != original.DonorTextureId ||
            candidate.MaterialTemplateTextureId != original.MaterialTemplateTextureId ||
            candidate.ApplyMode != original.ApplyMode)
        {
            return false;
        }

        if (!original.UsesAppendedPrivateRecord)
            return candidate.TargetTextureId == original.TargetTextureId;

        // An appended row's numeric texture id and PrivateRecordEditId both
        // change when a lower row is removed. The source binding plus native
        // donor/material contract is its stable semantic identity through that
        // compaction; a different donor that reuses the old T-number is not.
        return candidate.TargetWadEntry == original.TargetWadEntry &&
            candidate.SourceTextureRecordCount == original.SourceTextureRecordCount &&
            string.Equals(candidate.SourceImageSha256, original.SourceImageSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.SourceTextureComponentSha256, original.SourceTextureComponentSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.SourceLevelDataSha256, original.SourceLevelDataSha256, StringComparison.OrdinalIgnoreCase);
    }

    private TerrainTexturePaintHistoryCheckpoint? CaptureTerrainTexturePaintHistoryCheckpoint(
        string actionLabel)
    {
        if (_terrainTexturePaintHistoryRestoreBusy || _currentLevel == null || _currentGeometry == null)
            return null;

        string normalizedLevelKey = LevelCatalog.NormalizeKey(_currentLevel.Key);
        Dictionary<string, byte[]?> files = new(StringComparer.OrdinalIgnoreCase);
        void CaptureFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || files.ContainsKey(path))
                return;
            files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        CaptureFile(NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            normalizedLevelKey));
        CaptureFile(CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, normalizedLevelKey));
        foreach (NativeTerrainTextureRelocationEdit relocation in _nativeTerrainTextureRelocations)
            CaptureFile(relocation.PreviewImagePath);

        return new TerrainTexturePaintHistoryCheckpoint(
            normalizedLevelKey,
            actionLabel,
            _nativeTerrainTextureRelocations.ToArray(),
            _customTerrainTextures.ToArray(),
            _currentGeometry.Polygons
                .Select(TerrainTexturePaintHistoryFaceSnapshot.Capture)
                .ToArray(),
            files,
            _selectedTerrain?.RuntimeKey ?? "",
            "");
    }

    private void PushTerrainTexturePaintHistory(
        TerrainTexturePaintHistoryCheckpoint checkpoint)
    {
        if (_terrainTexturePaintHistoryRestoreBusy)
            return;
        if (!_terrainTexturePaintHistory.TryGetValue(
                checkpoint.LevelKey,
                out List<TerrainTexturePaintHistoryCheckpoint>? history))
        {
            history = new List<TerrainTexturePaintHistoryCheckpoint>();
            _terrainTexturePaintHistory[checkpoint.LevelKey] = history;
        }
        history.Add(checkpoint with
        {
            ExpectedTextureStateSignature = BuildTerrainTexturePaintHistorySignature()
        });
        const int historyLimit = 32;
        if (history.Count > historyLimit)
            history.RemoveRange(0, history.Count - historyLimit);
    }

    private int TerrainTexturePaintHistoryCountForCurrentLevel()
    {
        string levelKey = LevelCatalog.NormalizeKey(_currentLevel?.Key ?? "");
        return _terrainTexturePaintHistory.TryGetValue(
            levelKey,
            out List<TerrainTexturePaintHistoryCheckpoint>? history)
                ? history.Count
                : 0;
    }

    private string BuildTerrainTexturePaintHistorySignature()
    {
        if (_currentGeometry == null)
            return "no-geometry";
        System.Text.StringBuilder signature = new();
        foreach (NativeTerrainTextureRelocationEdit edit in _nativeTerrainTextureRelocations
                     .OrderBy(edit => edit.TargetTextureId))
        {
            signature.Append("r:")
                .Append(edit.TargetTextureId).Append(':')
                .Append(LevelCatalog.NormalizeKey(edit.DonorLevelKey)).Append(':')
                .Append(edit.DonorTextureId).Append(':')
                .Append((int)edit.ApplyMode).Append(':')
                .Append((int)edit.TargetRecordKind).Append(':')
                .Append(edit.MaterialTemplateTextureId).Append(':')
                .Append(edit.PrivateRecordEditId).Append(':')
                .Append(edit.SourceTextureComponentSha256).Append(':')
                .Append(edit.SourceLevelDataSha256).Append(';');
        }
        foreach (CustomTerrainTextureImport import in _customTerrainTextures
                     .OrderBy(import => import.TextureId))
        {
            signature.Append("c:")
                .Append(import.TextureId).Append(':')
                .Append(import.SourceImagePath).Append(':')
                .Append(import.SourceLevelKey).Append(':')
                .Append(import.SourceTextureId).Append(';');
        }
        foreach (TerrainPolygon face in _currentGeometry.Polygons
                     .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase))
        {
            signature.Append("f:")
                .Append(face.RuntimeKey).Append(':')
                .Append(face.TextureId).Append(':')
                .Append(face.TextureVisualEdit?.ToString() ?? "-").Append(':')
                .Append(face.SurfaceBehaviorEdit?.ToString() ?? "-").Append(':')
                .Append(face.Surface).Append(':')
                .Append(face.SurfaceColor).Append(':')
                .Append(face.SurfaceSource).Append(':')
                .Append(face.Behavior).Append(':')
                .Append(face.BehaviorSource).Append(':')
                .Append(face.BehaviorConfidence).Append(':')
                .Append(face.BehaviorNote).Append(';');
        }
        return signature.ToString();
    }

    private async Task<bool> UndoLastTerrainTextureActionAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Choose a level before undoing a texture action.";
            return false;
        }
        string levelKey = LevelCatalog.NormalizeKey(_currentLevel.Key);
        if (!_terrainTexturePaintHistory.TryGetValue(
                levelKey,
                out List<TerrainTexturePaintHistoryCheckpoint>? history) ||
            history.Count == 0)
        {
            _statusText.Text = "There is no texture-paint action to undo in this level during this editor session.";
            return false;
        }

        TerrainTexturePaintHistoryCheckpoint target = history[^1];
        if (!string.Equals(
                target.ExpectedTextureStateSignature,
                BuildTerrainTexturePaintHistorySignature(),
                StringComparison.Ordinal))
        {
            history.Clear();
            _statusText.Text =
                "Undo Last was cleared because texture or material state changed outside the recorded Texture Paint action. " +
                "No saved file or terrain face was changed.";
            return false;
        }
        TerrainTexturePaintHistoryCheckpoint rollback =
            CaptureTerrainTexturePaintHistoryCheckpoint("rollback current texture state")
            ?? throw new InvalidOperationException("Could not snapshot the current texture state before Undo Last.");
        _terrainTexturePaintHistoryRestoreBusy = true;
        try
        {
            await RestoreTerrainTexturePaintHistoryCheckpointAsync(target);
            history.RemoveAt(history.Count - 1);
            _statusText.Text =
                $"Undid last texture action: {target.ActionLabel}. Restored {target.Relocations.Count:N0} staged cross-level texture record(s); " +
                "terrain height, XY, and structure edits were left untouched.";
            EditorDiagnostics.RecordAction(
                "Undo last terrain texture action",
                $"{target.ActionLabel}; restored {target.Relocations.Count} relocation(s).");
            RefreshTerrainTexturePaintModeUi();
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            try
            {
                await RestoreTerrainTexturePaintHistoryCheckpointAsync(rollback);
            }
            catch (Exception rollbackException)
            {
                EditorDiagnostics.RecordException(
                    "rolling back a failed Undo Last terrain texture action",
                    rollbackException);
            }
            _statusText.Text =
                $"Could not undo the last texture action: {ex.Message} The current relocation manifest and face-local texture state were restored.";
            EditorDiagnostics.RecordException("undoing the last terrain texture action", ex);
            RefreshTerrainTexturePaintModeUi();
            await Task.CompletedTask;
            return false;
        }
        finally
        {
            _terrainTexturePaintHistoryRestoreBusy = false;
        }
    }

    private async Task RestoreTerrainTexturePaintHistoryCheckpointAsync(
        TerrainTexturePaintHistoryCheckpoint checkpoint)
    {
        if (_currentLevel == null || _currentGeometry == null ||
            !string.Equals(
                LevelCatalog.NormalizeKey(_currentLevel.Key),
                checkpoint.LevelKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Texture history belongs to {checkpoint.LevelKey}, not the currently loaded level.");
        }

        HashSet<string> restoredPreviewPaths = checkpoint.Relocations
            .Select(edit => edit.PreviewImagePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] obsoletePreviewPaths = _nativeTerrainTextureRelocations
            .Select(edit => edit.PreviewImagePath)
            .Where(path =>
                !string.IsNullOrWhiteSpace(path) &&
                !restoredPreviewPaths.Contains(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach ((string path, byte[]? content) in checkpoint.Files)
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
        foreach (string obsoletePreviewPath in obsoletePreviewPaths)
            DeleteManagedNativeTerrainTexturePreview(obsoletePreviewPath);

        _nativeTerrainTextureRelocations = checkpoint.Relocations;
        _customTerrainTextures = checkpoint.CustomTextures;
        Dictionary<string, TerrainTexturePaintHistoryFaceSnapshot> faces = checkpoint.Faces
            .ToDictionary(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase);
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (faces.TryGetValue(face.RuntimeKey, out TerrainTexturePaintHistoryFaceSnapshot? snapshot))
                snapshot.Restore(face);
        }
        TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (faces.TryGetValue(face.RuntimeKey, out TerrainTexturePaintHistoryFaceSnapshot? snapshot))
                snapshot.Restore(face);
        }
        // The history snapshot intentionally excludes the whole terrain-edits
        // file. Rebuild it from the live geometry after restoring only texture
        // fields, so height, XY, and structure changes made later survive.
        await PersistCurrentTerrainEditsAsync();
        ApplyCustomTerrainTexturePreviews();
        TerrainPolygon? selected = _currentGeometry.Polygons.FirstOrDefault(face =>
            string.Equals(face.RuntimeKey, checkpoint.SelectedTerrainRuntimeKey, StringComparison.OrdinalIgnoreCase));
        if (selected != null)
        {
            _selectedTerrain = selected;
            _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(selected);
            ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, selected));
        }
        _viewport.NotifyTerrainPresentationDataChanged();
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
    }

    private sealed record StagedTerrainTextureListItem(
        NativeTerrainTextureRelocationEdit Edit);

    private sealed record TerrainTexturePaintHistoryCheckpoint(
        string LevelKey,
        string ActionLabel,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> Relocations,
        IReadOnlyList<CustomTerrainTextureImport> CustomTextures,
        IReadOnlyList<TerrainTexturePaintHistoryFaceSnapshot> Faces,
        IReadOnlyDictionary<string, byte[]?> Files,
        string SelectedTerrainRuntimeKey,
        string ExpectedTextureStateSignature);

    private sealed record TerrainTexturePaintHistoryFaceSnapshot(
        string RuntimeKey,
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
        public static TerrainTexturePaintHistoryFaceSnapshot Capture(TerrainPolygon face) =>
            new(
                face.RuntimeKey,
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

        public void Restore(TerrainPolygon face)
        {
            face.ApplyTextureOverride(TextureId);
            if (TextureVisualEdit == null)
                face.ClearTextureVisualEdit();
            else
                face.ApplyTextureVisualEdit(TextureVisualEdit);
            if (SurfaceBehaviorEdit == null)
                face.ClearSurfaceBehaviorEdit();
            else
                face.ApplySurfaceBehaviorEdit(SurfaceBehaviorEdit);
            face.SetSurface(Surface, SurfaceColor, SurfaceSource);
            face.SetBehavior(Behavior, BehaviorSource, BehaviorConfidence, BehaviorNote);
        }
    }

    private sealed record TerrainTexturePaintGallerySuggestion(
        TerrainTexturePaintGalleryItem Item,
        int Score,
        string Reason);

    private sealed record TerrainTexturePaintSuggestionSet(
        IReadOnlyList<TerrainTexturePaintGallerySuggestion> Suggestions,
        int AdjacentFaceCount,
        int NearbyFaceCount)
    {
        public static TerrainTexturePaintSuggestionSet Empty { get; } =
            new(Array.Empty<TerrainTexturePaintGallerySuggestion>(), 0, 0);
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
            // Paint-mode uses the source's indexed art while preserving the
            // destination face's tint/material/collision. Do not grey a native
            // resident tile merely because it cannot also donate those
            // unrelated properties.
            bool blocked = !choice.CanUseArt;
            return new TerrainTexturePaintGalleryItem(
                level.Key,
                choice.TextureId,
                TerrainMaterialClassifier.FormatSurface(choice.Surface),
                choice.Color,
                blocked,
                blocked
                    ? NonEmptyBlockReason(choice.ArtReadinessNote)
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

    internal enum TerrainTexturePaintScopeChoice
    {
        Cancel,
        SelectedSection,
        AllLinkedSections
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
            bool expectedKind = choice.RequiresPrivateCopy
                ? Kind == TerrainTexturePaintSourceKind.CrossLevel &&
                  string.Equals(
                      LevelCatalog.NormalizeKey(SourceLevelKey),
                      LevelCatalog.NormalizeKey(TargetLevelKey),
                      StringComparison.OrdinalIgnoreCase)
                : Kind == TerrainTexturePaintSourceKind.SameLevel;
            return expectedKind &&
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
            TerrainTextureSwapChoice choice,
            bool preserveTargetNativeSurface = false)
        {
            return new TerrainTexturePaintBrush(
                choice.RequiresPrivateCopy
                    ? TerrainTexturePaintSourceKind.CrossLevel
                    : TerrainTexturePaintSourceKind.SameLevel,
                targetLevelKey,
                targetLevelKey,
                sourceLevelName,
                choice.TextureId,
                choice.RuntimeKey,
                choice.VisualRuntimeKey,
                preserveTargetNativeSurface || choice.RequiresPrivateCopy
                    ? TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface
                    : TerrainTextureSurfacePropertyMode.TransferDonorNativeSurface,
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
