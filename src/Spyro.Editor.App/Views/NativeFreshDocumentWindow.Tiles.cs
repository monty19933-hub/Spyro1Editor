using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentWindow
{
    private const string TiledCapabilityText = "Schema-4 tiled authoring supports Save/Undo/Reopen. Research export requires a clean saved document, zero placements, and complete native data within 0x1000 bytes (N16). Use Base floor color throughout, or a bounded selection of static retail textures; selections may repeat on any tile and mix with Base-colored faces. Export checks every source, GPU allocation and the complete 64 KiB evidence report; not every combination fits. Placed Mobys remain authoring-only on tiled levels. No legacy or partial level is exported. Normal Create BIN and general gameplay certification remain unavailable; this schematic is not gameplay proof.";
    private readonly StackPanel _tilePanel = new() { Spacing = 7, IsVisible = false };
    private readonly ListBox _tiles = new() { Name = "FreshLevelTiles", Height = 110 };
    private readonly TextBlock _tileNotice = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBox _tiledSpawnZ = new() { Name = "FreshSpawnZ", MaxLength = 16, MinHeight = 32 };
    private readonly StackPanel _tiledSpawnRow = new() { Spacing = 4, IsVisible = false };
    private Button _upgradeTiles = null!, _addTile = null!, _removeTile = null!, _tileUp = null!, _tileDown = null!;
    private NativeFreshFlatTileIntent? SelectedTile => CurrentAggregateIntent?.TiledContent?.Tiles
        .FirstOrDefault(t => t.EditorId == _aggregateSession?.SelectedTileId);
    private NativeFreshFloorMaterialsIntent? ActiveMaterialPair => SelectedTile?.Materials ?? CurrentAggregateIntent?.FloorMaterials;

    private Control BuildTileFields()
    {
        _upgradeTiles = Button("FreshTilesUpgrade", "Use tiled geometry (schema 4)", () => ChangeTiles(() => _aggregateSession!.UpgradeToTiles()));
        _addTile = Button("FreshTileAddAdjacent", "Add tile to right", () => ChangeTiles(() => _aggregateSession!.AddAdjacentTile()));
        _removeTile = Button("FreshTileRemove", "Remove tile", () => ChangeTiles(() => _aggregateSession!.RemoveSelectedTile()));
        _tileUp = Button("FreshTileMoveUp", "Move tile up", () => ChangeTiles(() => _aggregateSession!.MoveSelectedTile(-1)));
        _tileDown = Button("FreshTileMoveDown", "Move tile down", () => ChangeTiles(() => _aggregateSession!.MoveSelectedTile(1)));
        _tiles.ItemTemplate = new FuncDataTemplate<NativeFreshFlatTileIntent>((tile, _) => new TextBlock
        {
            Text = tile == null ? "" : $"{tile.X}, {tile.Y}, {tile.Z} · {tile.Width}×{tile.Depth}\n{tile.EditorId}", FontSize = 11
        });
        _tiles.SelectionChanged += (_, _) =>
        {
            if (_refreshing || !_aggregateMode || CurrentAggregateIntent?.TiledContent == null) return;
            if (AggregateCommandsBlocked || HasUnappliedFields || _tiles.SelectedItem is not NativeFreshFlatTileIntent selected)
            { RestoreTileSelection(); return; }
            _aggregateSession!.SelectTile(selected.EditorId);
            WriteFields();
        };
        _tilePanel.Children.Add(_upgradeTiles); _tilePanel.Children.Add(_tileNotice); _tilePanel.Children.Add(_tiles);
        _tilePanel.Children.Add(new WrapPanel { Children = { _addTile, _removeTile, _tileUp, _tileDown } });
        _tiledSpawnRow.Children.Add(new TextBlock { Text = "Spawn Z · 512 above a supporting tile plane", FontSize = 12 });
        _tiledSpawnRow.Children.Add(_tiledSpawnZ);
        _tiledSpawnZ.TextChanging += (_, _) => FieldsChanged();
        return _tilePanel;
    }

    private void ChangeTiles(Action change)
    {
        if (!_aggregateMode || CurrentAggregateIntent == null || AggregateCommandsBlocked || HasPendingPlacementAdd || HasUnappliedFields) return;
        try
        {
            RequireCurrentProject(); change(); WriteFields();
            _status.Text = "Applied tiled geometry change. Save to persist; Undo restores the complete document. " +
                (CurrentAggregateIntent?.SchemaVersion == 5 ? FamilyCapabilityText : TiledCapabilityText);
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        { _status.Text = "Tile change refused: " + error.Message + " The previous document and history are retained."; RefreshChrome(); }
    }

    private void WriteTileFields()
    {
        _tiles.ItemsSource = CurrentAggregateIntent?.TiledContent?.Tiles;
        RestoreTileSelection();
        _tiledSpawnZ.Text = CurrentAggregateIntent?.TiledContent?.SpawnZ.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private void RestoreTileSelection()
    {
        bool previous = _refreshing; _refreshing = true;
        try { _tiles.SelectedItem = SelectedTile; }
        finally { _refreshing = previous; }
    }

    private void RefreshTileChrome(bool editable)
    {
        var tiled = CurrentAggregateIntent?.TiledContent;
        _tilePanel.IsVisible = _aggregateMode && CurrentAggregateIntent != null;
        _upgradeTiles.IsVisible = tiled == null;
        _upgradeTiles.Content = CurrentAggregateIntent?.SchemaVersion == 5 ? "Use tiled geometry (keeps schema 5)" : "Use tiled geometry (schema 4)";
        _tiles.IsVisible = _addTile.IsVisible = _removeTile.IsVisible = _tileUp.IsVisible = _tileDown.IsVisible = tiled != null;
        _tiledSpawnRow.IsVisible = tiled != null;
        bool canEdit = editable && !HasUnappliedFields && !HasPendingPlacementAdd;
        _upgradeTiles.IsEnabled = canEdit && tiled == null;
        _tiles.IsEnabled = canEdit;
        _addTile.IsEnabled = canEdit && tiled != null && tiled.Tiles.Count < NativeFreshTiledLevelContent.MaximumTiles;
        _removeTile.IsEnabled = canEdit && tiled?.Tiles.Count > 1;
        int index = tiled?.Tiles.ToList().FindIndex(t => t.EditorId == _aggregateSession?.SelectedTileId) ?? -1;
        _tileUp.IsEnabled = canEdit && index > 0;
        _tileDown.IsEnabled = canEdit && index >= 0 && index + 1 < tiled!.Tiles.Count;
        _tiledSpawnZ.IsEnabled = editable && tiled != null && !HasPendingPlacementAdd;
        _tileNotice.Text = CurrentAggregateIntent?.SchemaVersion == 5 ?
            (tiled == null ? "Explicit tiled upgrade preserves this floor, its materials and every placement in schema 5. Tiled family documents remain authoring-only. "
                : $"{tiled.Tiles.Count} ordered tiles. Geometry/color/material fields edit the selected tile; spawn, sky and music belong to the whole level. ") + FamilyCapabilityText
            : tiled == null ? "Explicit upgrade preserves this floor and its material choices as the first tile. Tiled research supports all-Base faces or bounded static retail texture selections, optionally mixed with Base. It requires zero placements, complete native data within 0x1000 bytes, validated source/GPU ownership, and a complete report within 64 KiB; other tiled content remains authoring-only."
            : $"{tiled.Tiles.Count} ordered tiles. Geometry/color/material fields edit the selected tile; spawn, sky and music belong to the whole level. Shared seams are allowed; overlapping flat interiors are not.";
    }

    private NativeFreshLevelDocumentIntent ReadTiledFields(NativeFreshPlacementIntent[] rows)
    {
        var content = CurrentAggregateIntent!.TiledContent!;
        var selected = SelectedTile ?? throw new InvalidDataException("Select an actual tile before applying geometry.");
        if (!int.TryParse(_tiledSpawnZ.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int spawnZ))
            throw new InvalidDataException("Spawn Z requires an integer.");
        var materials = MaterialFieldSignature() == _appliedMaterialFields ? selected.Materials :
            NativeFreshFloorMaterialsIntent.Create(ReadMaterialSlot(_materialSlots[0]), ReadMaterialSlot(_materialSlots[1]));
        var replacement = NativeFreshFlatTileIntent.Create(selected.EditorId,
            Number("FreshFloorX"), Number("FreshFloorY"), Number("FreshFloorZ"), Number("FreshFloorWidth"), Number("FreshFloorDepth"),
            Rgb("FreshFloorColor"), materials);
        // Geometry, spawn and all placements validate together, so moving a tile
        // and its route does not pass through an invalid intermediate document.
        var next = NativeFreshTiledLevelContent.Create(content.Tiles.Select(t => t.EditorId == selected.EditorId ? replacement : t),
            Number("FreshSpawnX"), Number("FreshSpawnY"), spawnZ, Number("FreshHeading"), Rgb("FreshSkyColor"),
            (_music.SelectedItem as MusicTrackEntry)?.TrackId ?? -1);
        return CurrentAggregateIntent!.WithTiledContent(next, rows);
    }
}
