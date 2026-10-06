using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// The shipping Terrain tab. The same working controls as before, arranged as four plain groups
/// (Tiles, Brush, Textures, corner-by-corner) with one home for each action: saving belongs to the
/// toolbar's Save, and engine vocabulary stays in Details and the logs.
/// </summary>
public sealed partial class MainWindow
{
    private TextBlock? _terrainPanelSelectionText;
    private TextBlock? _terrainPanelCornerText;
    private TextBlock? _terrainPanelPaintText;
    private TextBlock? _terrainPanelSlotsText;
    private Control? _terrainPanelHeightRow;
    private Control? _terrainPanelSoftnessRow;
    private bool _terrainTextureSlotsKnown;
    private int _terrainTextureSlotsRemaining;
    private int _terrainTextureSlotsTotal;

    private Control BuildReleaseTerrainWorkspace()
    {
        // Every terrain control and its field is created by the original builders, because the rest of
        // the editor enables, disables and reads them. Only their old containers are left behind.
        Control legacy = BuildReleaseTerrainTextureWorkspace();
        // The original builder puts the research-lab controls first; they stay hidden outside that document.
        Control? labControls = legacy is StackPanel shared && shared.Children.Count > 0 ? shared.Children[0] : null;

        StackPanel root = new() { Name = "TerrainPanel", Spacing = 12, Margin = new Thickness(0, 10, 0, 0) };
        if (labControls != null)
            root.Children.Add(Detach(labControls));
        root.Children.Add(BuildTerrainTilesCard());
        root.Children.Add(BuildTerrainBrushCard());
        root.Children.Add(BuildTerrainTexturesCard());
        root.Children.Add(BuildTerrainCornerSection());

        Button restore = NewAsyncButton("Restore Original Terrain…", RestoreCurrentTerrainAsync);
        restore.Name = "TerrainRestoreOriginal";
        StyleModernSecondaryButton(restore, ModernRed);
        restore.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(restore, "Throw away every terrain change in this level and go back to the original. You are asked to confirm first.");
        root.Children.Add(restore);
        RefreshReleaseTerrainPanelTexts();
        return root;
    }

    private static T Detach<T>(T control) where T : Control
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl content:
                content.Content = null;
                break;
        }
        return control;
    }

    private static Border TerrainCard(string title, StackPanel content)
    {
        content.Spacing = 8;
        content.Children.Insert(0, new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = content
        };
    }

    private static TextBlock TerrainNote() => new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        LineHeight = 17,
        Foreground = new SolidColorBrush(ModernMutedInk)
    };

    private static Grid TwoColumnGrid(int rows)
    {
        Grid grid = new()
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 6,
            RowSpacing = 6
        };
        for (int i = 0; i < rows; i++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        return grid;
    }

    private static void Place(Grid grid, Button? button, int column, int row, string? label = null, string? tip = null, int span = 1)
    {
        if (button == null)
            return;
        Detach(button);
        if (label != null)
            button.Content = label;
        if (tip != null)
            ToolTip.SetTip(button, tip);
        button.Margin = new Thickness(0);
        button.MinHeight = 32;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(button, column);
        Grid.SetRow(button, row);
        Grid.SetColumnSpan(button, span);
        grid.Children.Add(button);
    }

    private Control BuildTerrainTilesCard()
    {
        StackPanel content = new();
        _terrainPanelSelectionText = TerrainNote();
        _terrainPanelSelectionText.Name = "TerrainPanelSelection";
        // Room for two lines, so the buttons below do not jump when the note gets longer on selection.
        _terrainPanelSelectionText.MinHeight = 36;
        content.Children.Add(_terrainPanelSelectionText);

        Grid shape = TwoColumnGrid(2);
        Place(shape, _terrainTaskMoveFaceButton, 0, 0, "Move Tile…", "Move the whole selected tile by exact amounts. You can also drag it in the view.");
        Place(shape, _terrainTaskSinglePointButton, 1, 0, "Edit Corner…", "Move one corner of the selected tile by exact amounts.");
        _terrainTileTypeButton ??= NewAsyncButton("Tile Type…", ChangeSelectedTileTypeAsync);
        _terrainTileTypeButton.Name = "TerrainTileType";
        Place(shape, _terrainTileTypeButton, 0, 1, "Tile Type…", "Choose what Spyro finds on the selected tiles: solid ground, water that hurts, lava, a supercharge ramp… The picture stays; the ground behaves differently.", span: 2);
        content.Children.Add(shape);

        Grid height = TwoColumnGrid(1);
        height.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.35, GridUnitType.Star)));
        Place(height, _releaseTerrainRaiseFaceButton, 0, 0, "Raise", "Lift the selected tile a little, keeping its shape.");
        Place(height, _releaseTerrainLowerFaceButton, 1, 0, "Lower", "Drop the selected tile a little, keeping its shape.");
        Place(height, _releaseTerrainUndoHeightButton, 2, 0, "Reset Height", "Put the selected tile back at its original height. Its texture and other changes are kept.");
        _terrainPanelHeightRow = height;
        content.Children.Add(height);

        Grid structure = TwoColumnGrid(2);
        Place(structure, _terrainTaskAddCopyButton, 0, 0, "Add Terrain", "Add a copy of the selected tile next to it, then move it into place.");
        Place(structure, _terrainTaskRemoveFaceButton, 1, 0, null, "Remove the selected tile (or all selected tiles) and the ground Spyro stands on there.");
        Place(structure, _terrainUndoButton, 0, 1, "Undo Terrain", "Put the selected tile back the way it was, or bring removed tiles back.", span: 2);
        content.Children.Add(structure);
        return TerrainCard("Tiles", content);
    }

    private Control BuildTerrainBrushCard()
    {
        StackPanel content = new();
        Detach(_terrainBrushModeBox);
        _terrainBrushModeBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        _terrainBrushModeBox.MinWidth = 0;
        content.Children.Add(_terrainBrushModeBox);
        content.Children.Add(BuildTerrainBrushSlider("Size", Detach(_terrainBrushRadiusSlider), Detach(_terrainBrushRadiusText)));
        content.Children.Add(BuildTerrainBrushSlider("Strength", Detach(_terrainBrushStrengthSlider), Detach(_terrainBrushStrengthText)));
        _terrainPanelSoftnessRow = BuildTerrainBrushSlider("Soft edge", Detach(_terrainBrushFeatherSlider), Detach(_terrainBrushFeatherText));
        content.Children.Add(_terrainPanelSoftnessRow);

        Detach(_terrainWholeTilesBox);
        _terrainWholeTilesBox.Content = "Whole tiles";
        ToolTip.SetTip(_terrainWholeTilesBox, "Raise, Lower and Level move every tile the circle touches evenly, instead of pulling single corners into spikes.");
        _terrainWholeTilesBox.IsCheckedChanged += (_, _) => RefreshReleaseTerrainPanelTexts();
        content.Children.Add(_terrainWholeTilesBox);

        Grid strokes = TwoColumnGrid(1);
        Place(strokes, _terrainUndoStrokeButton, 0, 0, "Undo Stroke");
        Place(strokes, _terrainRedoStrokeButton, 1, 0, "Redo Stroke");
        content.Children.Add(strokes);

        StackPanel more = new() { Spacing = 8 };
        Detach(_terrainPlayableOnlyBox);
        _terrainPlayableOnlyBox.Content = "Only change ground Spyro can stand on";
        ToolTip.SetTip(_terrainPlayableOnlyBox, "Leave walls, roofs and scenery alone while sculpting.");
        more.Children.Add(_terrainPlayableOnlyBox);
        Detach(_terrainJoinSeamsBox);
        _terrainJoinSeamsBox.Content = "Keep neighbouring tiles joined";
        ToolTip.SetTip(_terrainJoinSeamsBox, "Corners shared with tiles outside the brush move too, so no gaps open up.");
        more.Children.Add(_terrainJoinSeamsBox);
        Grid presets = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 6
        };
        Button fine = NewButton("Small", () => SetTerrainBrushPreset("Small brush", 176, 24, 56));
        Button normal = NewButton("Medium", () => SetTerrainBrushPreset("Medium brush", DefaultTerrainBrushRadius, DefaultTerrainBrushStrength, DefaultTerrainBrushFeather));
        Button wide = NewButton("Large", () => SetTerrainBrushPreset("Large brush", 1024, 72, 78));
        foreach ((Button button, int column) in new[] { (fine, 0), (normal, 1), (wide, 2) })
        {
            button.Margin = new Thickness(0);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(button, column);
            presets.Children.Add(button);
        }
        more.Children.Add(presets);
        Button reset = NewButton("Reset Brush", ResetTerrainBrush);
        reset.Margin = new Thickness(0);
        reset.HorizontalAlignment = HorizontalAlignment.Left;
        more.Children.Add(reset);
        Expander options = BuildCollapsedPanel("More brush options", more);
        options.Name = "TerrainBrushMoreOptions";
        options.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Children.Add(options);
        return TerrainCard("Brush", content);
    }

    private Control BuildTerrainTexturesCard()
    {
        StackPanel content = new();
        Grid choose = TwoColumnGrid(1);
        Place(choose, _terrainTaskPaintFaceButton, 0, 0, "Choose Texture…",
            "Pick a texture from this or another level, then click tiles in the view to paint them.", span: 2);
        if (_terrainTaskPaintFaceButton != null)
            StyleModernPrimaryButton(_terrainTaskPaintFaceButton, ModernTeal);
        content.Children.Add(choose);

        // Everything below is for the original levels; an authored level picks materials in its own chooser.
        StackPanel retail = new() { Name = "RetailTerrainDetails", Spacing = 8 };
        _retailTerrainDetailsPanel = retail;
        if (_terrainTexturePaintActivePreviewFrame != null)
        {
            Detach(_terrainTexturePaintActivePreviewFrame);
            _terrainTexturePaintActivePreviewFrame.HorizontalAlignment = HorizontalAlignment.Left;
            retail.Children.Add(_terrainTexturePaintActivePreviewFrame);
        }
        _terrainPanelPaintText = TerrainNote();
        _terrainPanelPaintText.Name = "TerrainPanelPaint";
        retail.Children.Add(_terrainPanelPaintText);

        Grid painting = TwoColumnGrid(2);
        Place(painting, _terrainTexturePaintStopButton, 0, 0, "Stop Painting", "Go back to selecting tiles. Esc does the same.");
        Place(painting, _terrainTexturePaintReturnButton, 1, 0, "Another Texture…", "Open the texture chooser again without losing the textures already loaded.");
        Place(painting, _terrainSelectedLinkedPaintButton, 0, 1, "Paint Selected Tile", "Paint the selected tile with the chosen texture.");
        Button undoPaint = NewAsyncButton("Undo Paint on Tile", UndoSelectedTerrainTexturePaintAsync);
        undoPaint.Name = "TerrainUndoPaint";
        Place(painting, undoPaint, 1, 1, null, "Give the selected tile its original texture back.");
        retail.Children.Add(painting);

        Button manage = NewAsyncButton("Manage Added Textures…", ManageStagedTerrainTexturesAsync);
        manage.Name = "TerrainManageTextures";
        manage.Margin = new Thickness(0);
        manage.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(manage, "See the textures you brought in from other levels and remove ones you no longer use.");
        retail.Children.Add(manage);
        _terrainPanelSlotsText = TerrainNote();
        _terrainPanelSlotsText.Name = "TerrainPanelSlots";
        retail.Children.Add(_terrainPanelSlotsText);
        content.Children.Add(retail);
        return TerrainCard("Textures", content);
    }

    private Control BuildTerrainCornerSection()
    {
        StackPanel content = new() { Spacing = 8, Margin = new Thickness(2, 4, 2, 2) };
        _terrainPanelCornerText = TerrainNote();
        _terrainPanelCornerText.Name = "TerrainPanelCorner";
        content.Children.Add(_terrainPanelCornerText);
        Grid grid = TwoColumnGrid(5);
        Place(grid, _terrainPointPreviousButton, 0, 0, "Previous Corner");
        Place(grid, _terrainPointNextButton, 1, 0, "Next Corner");
        Place(grid, _terrainPointRaiseButton, 0, 1, "Corner Up");
        Place(grid, _terrainPointLowerButton, 1, 1, "Corner Down");
        Place(grid, _terrainPointXMinusButton, 0, 2, "West (X −)");
        Place(grid, _terrainPointXPlusButton, 1, 2, "East (X +)");
        Place(grid, _terrainPointYMinusButton, 0, 3, "South (Y −)");
        Place(grid, _terrainPointYPlusButton, 1, 3, "North (Y +)");
        Place(grid, _terrainPointResetButton, 0, 4, "Reset Corner", "Put this corner back where the original level has it.");
        Place(grid, _terrainPointEditButton, 1, 4, "Exact Values…", "Type this corner's position.");
        content.Children.Add(grid);
        Expander section = BuildCollapsedPanel("Corner by corner", content);
        section.Name = "TerrainCornerSection";
        section.HorizontalAlignment = HorizontalAlignment.Stretch;
        return section;
    }

    /// <summary>Plain-language lines of the Terrain tab; called whenever selection, brush or paint state changes.</summary>
    private void RefreshReleaseTerrainPanelTexts()
    {
        if (_terrainPanelSelectionText == null)
            return;
        string several = OperatingSystem.IsMacOS() ? "⌘-click" : "Ctrl-click";
        int multi = _viewport.MultiSelectedTerrainIndexes.Count;
        if (_currentGeometry == null)
            _terrainPanelSelectionText.Text = "Open a level to edit its terrain.";
        else if (multi >= 2)
            _terrainPanelSelectionText.Text = $"{multi} tiles selected. Tile Type… changes them together; Remove Terrain removes them; Undo Terrain brings removed ones back.";
        else if (_selectedTerrain == null)
            _terrainPanelSelectionText.Text = $"Click a tile in the view to select it. {several} selects several.";
        else
        {
            string tile = EditorViewport.PlainTileName(_selectedTerrain.Surface);
            string state = _selectedTerrain.IsTerrainRemoved
                ? " It is removed; Undo Terrain brings it back."
                : _selectedTerrain.IsTerrainAddClone
                    ? " It is a new tile you added."
                    : _selectedTerrain.IsTerrainEdited
                        ? " You have changed it."
                        : "";
            if (_freshSceneDocument == null && !_selectedTerrain.IsTerrainAddClone)
                TileTypeCatalog(out _);
            string? kind = _selectedTerrain.IsTerrainAddClone ? null : DescribeSelectedTileType(_selectedTerrain);
            _terrainPanelSelectionText.Text = $"{tile} selected{(kind == null ? "" : $" — {kind}")}.{state} Drag it in the view to move it, or use the buttons.";
        }
        if (_terrainTileTypeButton != null)
            _terrainTileTypeButton.IsEnabled = _currentGeometry != null && _freshSceneDocument == null && SelectedTilesForTypeChange().Count > 0;

        if (_terrainPanelHeightRow != null)
            _terrainPanelHeightRow.IsVisible = _freshSceneDocument == null;
        if (_terrainPanelSoftnessRow != null)
        {
            // Whole tiles move evenly, so the soft edge only matters for the smoothing brushes.
            bool evenBrush = _terrainWholeTilesBox.IsChecked == true &&
                _viewport.TerrainBrushAction is TerrainBrushAction.Raise or TerrainBrushAction.Lower or TerrainBrushAction.Flatten;
            _terrainPanelSoftnessRow.IsEnabled = !evenBrush;
            _terrainPanelSoftnessRow.Opacity = evenBrush ? 0.45 : 1;
        }

        if (_terrainPanelCornerText != null)
        {
            if (_selectedTerrain == null)
                _terrainPanelCornerText.Text = "Select a tile, then step through its corners here.";
            else
            {
                int index = NormalizeTerrainPointIndex(_selectedTerrain, _selectedTerrainPointIndex);
                int count = Math.Min(_selectedTerrain.Points.Count, _selectedTerrain.ZValues.Length);
                if (index < 0 || index >= count)
                    _terrainPanelCornerText.Text = "This tile has no corners that can be moved.";
                else
                {
                    Vector2f point = _selectedTerrain.Points[index];
                    Vector2f original = _selectedTerrain.OriginalPoints[index];
                    float height = _selectedTerrain.ZValues[index];
                    float dx = point.X - original.X, dy = point.Y - original.Y, dz = height - _selectedTerrain.OriginalZValues[index];
                    bool moved = Math.Abs(dx) >= 0.5f || Math.Abs(dy) >= 0.5f || Math.Abs(dz) >= 0.5f;
                    _terrainPanelCornerText.Text =
                        $"Corner {index + 1} of {count}: X {point.X:0}, Y {point.Y:0}, height {height:0}." +
                        (moved ? $" Moved by {dx:+0;-0;0}, {dy:+0;-0;0}, {dz:+0;-0;0}." : " Not moved.");
                }
            }
        }

        if (_terrainPanelPaintText != null)
        {
            _terrainPanelPaintText.Text = _activeTerrainTexturePaintBrush == null
                ? "No texture chosen. Choose one, then click tiles to paint them."
                : _terrainTexturePaintBusy
                    ? $"Painting with {_activeTerrainTexturePaintBrush.DisplayLabel}…"
                    : $"Painting with {_activeTerrainTexturePaintBrush.DisplayLabel}. Click tiles in the view; Esc stops.";
        }

        if (_terrainPanelSlotsText != null)
        {
            _terrainPanelSlotsText.Text = !_terrainTextureSlotsKnown
                ? ""
                : _terrainTextureSlotsTotal == 0
                    ? "In this level a texture from another level replaces every tile that shares the old one. The editor tells you how many before it changes anything."
                    : $"{_terrainTextureSlotsRemaining} of {_terrainTextureSlotsTotal} single-tile texture slot{(_terrainTextureSlotsTotal == 1 ? "" : "s")} free in this level. Reusing a texture you already added needs no new slot.";
            _terrainPanelSlotsText.IsVisible = _terrainPanelSlotsText.Text.Length > 0;
        }
    }
}
