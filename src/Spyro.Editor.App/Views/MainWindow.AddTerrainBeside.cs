using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
            if (AddFreshTerrainCopy(source, move.X, move.Y, 0) != null)
                _statusText.Text = $"Added a tile against the {SideWord(side)} edge of the selected one. Click the arrow again to keep going; Save Level keeps it.";
            return;
        }
        await AddRetailTerrainBesideAsync(side);
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
