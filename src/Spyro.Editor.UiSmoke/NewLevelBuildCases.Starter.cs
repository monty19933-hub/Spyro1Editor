using Avalonia.Controls;
using Avalonia.Interactivity;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

/// <summary>File ▾ › New Level as the user runs it (answering "Start New Level" when the project has a created level),
/// then the Terrain tab's Ground card: the meadow it starts with (<see cref="StarterLevel"/>), a selected grass tile made
/// lava with the Lava button, a tile clicked in the view while painting water, Esc, and the saved scene read back.
/// Pictures of each step go to the out folder.</summary>
internal static partial class NewLevelBuildCases
{
    private static void Set(MainWindow w, string name, object? value) =>
        typeof(MainWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(w, value);

    private static void StarterGround(MainWindow window, string outDir)
    {
        var task = (Task)Invoke(window, "NewMainLevelAsync")!;
        Wait(() =>
        {
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask)
                Click(Find<Button>(ask, "ConfirmationConfirm"));
            return task.IsCompleted;
        }, "File > New Level", 600);
        task.GetAwaiter().GetResult();
        var scene = window.FreshSceneForTesting ?? throw new InvalidOperationException("New Level opened no level.");
        var catalog = Get<Spyro.Editor.Core.Levels.LevelCatalog>(window, "_retailCatalog");
        var grounds = scene.Surfaces.GroupBy(s => GroundTypes.Of(s, catalog)?.Name ?? "other").ToDictionary(g => g.Key, g => g.Count());
        Console.WriteLine($"starter: {scene.Surfaces.Count} faces ({string.Join(", ", grounds.Select(g => $"{g.Key} {g.Value}"))}), " +
            $"start ({scene.Spawn.X:0},{scene.Spawn.Y:0},{scene.Spawn.Z:0}) heading {scene.Heading}, sky from WAD entry {scene.SkySourceWadEntry}");
        if (grounds.GetValueOrDefault("Grass") < 100 || grounds.GetValueOrDefault("Water") != 4 || !grounds.ContainsKey("Stone") || !grounds.ContainsKey("Sand"))
            throw new InvalidOperationException("The new level does not start as the meadow.");
        if (scene.Surfaces.Any(s => s.Material == null)) throw new InvalidOperationException("A starter face has no texture.");

        Pump(20);
        var tabs = Find<TabControl>(window, "EditorWorkspaceToolTabs");
        string opened = ((tabs.SelectedItem as TabItem)?.Header as TextBlock)?.Text ?? "";
        Console.WriteLine("tab after New Level: " + opened);
        if (opened != "Terrain") throw new InvalidOperationException("New Level did not open the Terrain tab.");
        var card = Find<Border>(window, "TerrainGroundCard");
        Wait(() => card.IsVisible, "the Ground card", 30);
        var swatches = Find<WrapPanel>(window, "GroundSwatches").Children.OfType<Button>().Select(b => b.Name).ToList();
        Console.WriteLine("Ground buttons: " + string.Join(" ", swatches));
        Pump(200);
        Snapshot(window, Path.Combine(outDir, "01-new-level-terrain-tab.png"));

        // Select a grass tile (the one east of the start) and press Lava.
        var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
        TerrainPolygon Tile(float x, float y) => geometry.Polygons.First(p => p.Points.Count == 4 &&
            Math.Abs(p.Points.Average(v => v.X) - (StarterLevel.Origin + x)) < 1 && Math.Abs(p.Points.Average(v => v.Y) - (StarterLevel.Origin + y)) < 1 && p.ZValues.All(z => Math.Abs(z - StarterLevel.Floor) < 1));
        var lavaTile = Tile(8.5f * StarterLevel.Tile, 1.5f * StarterLevel.Tile);
        Set(window, "_selectedTerrain", lavaTile);
        Click(Find<Button>(window, "GroundLava"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Lava", "painting lava", 30);
        Pump(40);
        Console.WriteLine("status: " + Status(window));
        Console.WriteLine("Ground note: " + Find<TextBlock>(window, "GroundNote").Text);

        // While painting, a tile clicked in the view takes the ground (the viewport's paint click).
        Click(Find<Button>(window, "GroundWater"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Water", "painting water", 30);
        var waterTile = Tile(2.5f * StarterLevel.Tile, 2.5f * StarterLevel.Tile);
        var painted = (Task<bool>)Invoke(window, "PaintGroundFromViewportAsync", waterTile)!;
        Wait(() => painted.IsCompleted, "paint click", 60);
        if (!painted.Result) throw new InvalidOperationException("The paint click was not taken by the Ground card.");
        Pump(40);
        Snapshot(window, Path.Combine(outDir, "02-painting.png"));
        Invoke(window, "StopGroundPainting", true); Pump(10);
        var viewport = Get<EditorViewport>(window, "_viewport");
        viewport.SetViewMode(ViewportViewMode.Map); Pump(60);
        Snapshot(window, Path.Combine(outDir, "03-map-view.png"));
        viewport.SetViewMode(ViewportViewMode.Fly3D); Pump(10);
        if (window.ActiveGroundForTesting != null) throw new InvalidOperationException("Stop Painting left a ground chosen.");

        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, "authored65"));
        AuthoredSurface At(TerrainPolygon t) => saved.Surfaces.First(s => Math.Abs(s.Vertices.Average(v => v.X) - t.Points.Average(v => v.X)) < 1 &&
            Math.Abs(s.Vertices.Average(v => v.Y) - t.Points.Average(v => v.Y)) < 1 && s.Vertices.All(v => Math.Abs(v.Z - StarterLevel.Floor) < 1));
        var lava = At(lavaTile); var water = At(waterTile);
        Console.WriteLine($"saved: lava tile {GroundTypes.Of(lava, catalog)?.Name} kind {lava.Kind} shade {lava.Color}; water tile {GroundTypes.Of(water, catalog)?.Name} kind {water.Kind}");
        if (GroundTypes.Of(lava, catalog) != GroundTypes.Lava || lava.Kind != AuthoredSurfaceKind.Lava || lava.Color != GroundTypes.Lava.Shade)
            throw new InvalidOperationException("The lava tile was not saved as lava.");
        if (GroundTypes.Of(water, catalog) != GroundTypes.Water || water.Kind != AuthoredSurfaceKind.Water)
            throw new InvalidOperationException("The water tile was not saved as water.");
        var tileType = Find<Button>(window, "TerrainTileType");
        Set(window, "_selectedTerrain", Tile(4.5f * StarterLevel.Tile, 4.5f * StarterLevel.Tile));
        Invoke(window, "RefreshReleaseTerrainPanelTexts"); Pump(5);
        Console.WriteLine($"Tile Type… enabled in a built level: {tileType.IsEnabled}");
        if (!tileType.IsEnabled) throw new InvalidOperationException("Tile Type… is off in a level you build.");
        Console.WriteLine("NEW_LEVEL_STARTER_GROUND_OK");
    }
}
