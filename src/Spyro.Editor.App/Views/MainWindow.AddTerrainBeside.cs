using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain › Tiles › Add beside ◀ ▲ ▼ ▶: one click adds a copy of the selected tile exactly against its west,
/// north, south or east edge (as the Map view shows them: north up), and selects the copy, so clicking again keeps
/// extending the row.</summary>
public sealed partial class MainWindow
{
    internal enum TerrainSide { West, North, South, East }

    /// <summary>The move that puts a copy of the tile against one of its sides: its full width or depth.</summary>
    internal static Vector2f FlushTerrainOffset(TerrainPolygon tile, TerrainSide side)
    {
        float width = tile.Points.Max(p => p.X) - tile.Points.Min(p => p.X);
        float depth = tile.Points.Max(p => p.Y) - tile.Points.Min(p => p.Y);
        return side switch
        {
            TerrainSide.East => new(width, 0),
            TerrainSide.West => new(-width, 0),
            TerrainSide.North => new(0, depth),
            _ => new(0, -depth)
        };
    }

    private static string SideWord(TerrainSide side) => side.ToString().ToLowerInvariant();

    private Control BuildAddBesideRow()
    {
        Grid row = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 6
        };
        row.Children.Add(new TextBlock { Text = "Add beside", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach ((TerrainSide side, string arrow, int column) in new[] { (TerrainSide.West, "◀", 1), (TerrainSide.North, "▲", 2), (TerrainSide.South, "▼", 3), (TerrainSide.East, "▶", 4) })
        {
            Button button = NewAsyncButton(arrow, () => AddTerrainBesideAsync(side));
            button.Name = "AddTerrain" + side;
            button.MinHeight = 32;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(button, $"Add a copy of the selected tile against its {SideWord(side)} edge ({(side is TerrainSide.North or TerrainSide.South ? "up or down" : "left or right")} in the Map view). The copy is selected, so you can keep clicking.");
            Grid.SetColumn(button, column);
            row.Children.Add(button);
        }
        return row;
    }

    internal async Task AddTerrainBesideAsync(TerrainSide side)
    {
        if (_currentGeometry == null) { _statusText.Text = "Open a level first."; return; }
        if (_freshSceneDocument != null)
        {
            if (!_currentGeometry.Polygons.Any(p => !p.IsTerrainRemoved)) { AddFreshTerrainFromEmpty(); return; }
            if (_selectedTerrain is not { IsTerrainRemoved: false } source)
            { _statusText.Text = "Select a tile first, then click an arrow to add a tile beside it."; return; }
            Vector2f move = FlushTerrainOffset(source, side);
            if (await MakeRoomBesideAsync(source, side, move) is not { } tile) return;
            if (AddFreshTerrainCopy(tile, move.X, move.Y, 0) != null)
                _statusText.Text = $"Added a tile against the {SideWord(side)} edge of the selected one. Click the arrow again to keep going; Save Level keeps it.";
            return;
        }
        await AddRetailTerrainBesideAsync(side);
    }

    /// <summary>Answers "move the whole level to make room?" in tests.</summary>
    internal Func<bool>? MakeRoomChoiceForTesting { get; set; }

    /// <summary>The tile to copy from: the selected one, or — when the copy would cross the west or south edge of the game's
    /// map (or east or north) and the level has room on the other side — the same tile after the whole level is moved
    /// away from that edge (asked first, then saved). Null when nothing is added.</summary>
    private async Task<TerrainPolygon?> MakeRoomBesideAsync(TerrainPolygon source, TerrainSide side, Vector2f move)
    {
        float minX = source.Points.Min(p => p.X) + move.X, minY = source.Points.Min(p => p.Y) + move.Y;
        float maxX = source.Points.Max(p => p.X) + move.X, maxY = source.Points.Max(p => p.Y) + move.Y;
        float need = side switch
        {
            TerrainSide.West => -minX, TerrainSide.South => -minY,
            TerrainSide.East => maxX - SceneMove.MapEdge, _ => maxY - SceneMove.MapEdge
        };
        if (need <= 0) return source;
        var scene = CaptureFreshScene();
        var room = SceneMove.Room(scene);
        float across = side switch { TerrainSide.West => room.East, TerrainSide.South => room.North, TerrainSide.East => room.West, _ => room.South };
        // A good step (eight meadow tiles) when there is room for it, so the next clicks don't ask again.
        int step = (int)MathF.Ceiling(need / 1024f) * 1024;
        if (step > across) step = (int)MathF.Ceiling(need);
        if (step > across)
        {
            _statusText.Text = $"There is no room for that tile: your level already reaches across the game's map from its {SideWord(side)} edge to the other side.";
            return null;
        }
        string away = side switch { TerrainSide.West => "east", TerrainSide.South => "north", TerrainSide.East => "west", _ => "south" };
        bool accepted = MakeRoomChoiceForTesting != null
            ? MakeRoomChoiceForTesting()
            : await ShowConfirmationAsync("MakeRoomDialog", "Make Room", $"Move the level {away} to make room?",
                $"Your level is against the {SideWord(side)} edge of the game's map, so a tile can't go further {SideWord(side)}.\n" +
                $"The editor can move the whole level {step} units {away} — every tile, object, doorway and the player start together, " +
                "so nothing changes in how it plays — and then add the tile. The level is saved after the move.", "Move Level and Add");
        if (!accepted) { _statusText.Text = $"Nothing was added: the level stays against the map's {SideWord(side)} edge."; return null; }
        string sourceId = _freshSceneDocument!.Surfaces[source.SectorIndex].Id;
        int dx = side switch { TerrainSide.West => step, TerrainSide.East => -step, _ => 0 };
        int dy = side switch { TerrainSide.South => step, TerrainSide.North => -step, _ => 0 };
        SceneDocument moved;
        try { moved = SceneMove.Moved(scene, dx, dy); }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException)
        { _statusText.Text = "Could not move the level: " + e.Message; return null; }
        ApplyFreshScene(moved);
        if (!await SaveFreshSceneAsync()) return null;
        int index = moved.Surfaces.ToList().FindIndex(s => s.Id == sourceId);
        int polygon = _currentGeometry!.Polygons.FindIndex(p => p.SectorIndex == index);
        if (polygon < 0) return null;
        _selectedTerrain = _currentGeometry.Polygons[polygon];
        _selectedTerrainIndex = polygon;
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(polygon, _selectedTerrain));
        return _selectedTerrain;
    }

    /// <summary>An original level: Add Terrain's copy (the selected face becomes a staged copy moved by the offsets; Create
    /// BIN keeps the original), placed against the chosen edge.</summary>
    private Task AddRetailTerrainBesideAsync(TerrainSide side)
    {
        if (TryBlockId65UnsupportedTerrainMutation(_selectedTerrain, "Add Terrain Copy", structural: true)) return Task.CompletedTask;
        if (!CanUseTerrainAddCopyInCurrentLevel()) { _statusText.Text = BuildTerrainAddCopyUnavailableHint(); return Task.CompletedTask; }
        TerrainPolygon? terrain = _selectedTerrain;
        int index = _selectedTerrainIndex;
        if (terrain == null || index < 0 || terrain.IsTerrainRemoved)
        { _statusText.Text = "Select a tile first, then click an arrow to add a tile beside it."; return Task.CompletedTask; }
        if (!CanStageTerrainAddCopy(terrain))
        { _statusText.Text = "This tile can't be copied in this level. " + BuildTerrainAddCopyUnavailableHint(); return Task.CompletedTask; }
        Vector2f move = FlushTerrainOffset(terrain, side);
        ClearTerrainBrushUndoHistory();
        Vector2f[] deltas = terrain.TerrainVertexXYDeltas().ToArray();
        if (deltas.Length == 0) deltas = terrain.OriginalPoints.Select(_ => new Vector2f(0, 0)).ToArray();
        terrain.ApplyTerrainVertexXYDeltas(deltas.Select(d => new Vector2f(d.X + move.X, d.Y + move.Y)).ToArray());
        terrain.StageTerrainAddClone();
        _viewport.NotifyTerrainPresentationDataChanged();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(index, terrain));
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
        RefreshTerrainSurfaceQuickState();
        _statusText.Text = $"Added a copy against the {SideWord(side)} edge of the selected tile. Save keeps it; Create BIN checks that the game can hold it.";
        return Task.CompletedTask;
    }
}
