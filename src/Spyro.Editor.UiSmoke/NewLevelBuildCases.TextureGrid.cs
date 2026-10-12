using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>Terrain › Choose Texure… in a level you build, through the real dialog: the grid of an original level's
/// textures (Artisans), one clicked onto a selected pond tile (it keeps being water), a tile clicked in the view painted
/// with it, Esc; the saved scene read back.</summary>
internal static partial class NewLevelBuildCases
{
    private static void TextureGrid(MainWindow window, string outDir)
    {
        var task = (Task)Invoke(window, "NewMainLevelAsync")!;
        Wait(() =>
        {
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask) Click(Find<Button>(ask, "ConfirmationConfirm"));
            return task.IsCompleted;
        }, "File > New Level", 600);
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
        TerrainPolygon Tile(float x, float y) => geometry.Polygons.First(p => p.Points.Count == 4 &&
            Math.Abs(p.Points.Average(v => v.X) - (StarterLevel.Origin + x)) < 1 && Math.Abs(p.Points.Average(v => v.Y) - (StarterLevel.Origin + y)) < 1 && p.ZValues.All(z => Math.Abs(z - StarterLevel.Floor) < 1));
        var pond = Tile(8.5f * StarterLevel.Tile, 3.5f * StarterLevel.Tile);
        Set(window, "_selectedTerrain", pond);
        var choose = (Task)Invoke(window, "ChooseBuiltTextureAsync")!;
        Wait(() => window.OwnedWindows.Any(w => w.Name == "BuiltTextureDialog"), "the texture grid", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "BuiltTextureDialog");
        var level = Find<ComboBox>(dialog, "BuiltTextureLevel");
        level.SelectedItem = level.ItemsSource!.Cast<LevelDefinition>().First(l => l.Key == "artisans"); Pump(5);
        var grid = Find<WrapPanel>(dialog, "BuiltTextureGrid");
        Wait(() => grid.Children.Count > 30 && Find<TextBlock>(dialog, "BuiltTextureNote").Text?.StartsWith("Artisans") == true, "Artisans' textures in the grid", 120);
        Console.WriteLine($"grid: {grid.Children.Count} textures; note: {Find<TextBlock>(dialog, "BuiltTextureNote").Text}");
        Pump(60);
        Snapshot(dialog, Path.Combine(outDir, "01-texture-grid.png"));
        Click(Find<Button>(dialog, "BuiltTexture66"));
        Wait(() => choose.IsCompleted, "the texture chosen", 60);
        Console.WriteLine("status: " + Status(window));
        var painted = (Task<bool>)Invoke(window, "PaintBuiltTextureFromViewportAsync", Tile(1.5f * StarterLevel.Tile, 5.5f * StarterLevel.Tile))!;
        Wait(() => painted.IsCompleted, "paint click", 60);
        if (!painted.Result) throw new InvalidOperationException("The paint click was not taken by the texture grid.");
        Invoke(window, "StopBuiltTexturePainting", true); Pump(5);
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, "authored65"));
        int entry = catalog.FindByKey("artisans")!.SourceWadEntry;
        AuthoredSurface At(float x, float y) => saved.Surfaces.First(s => Math.Abs(s.Vertices.Average(v => v.X) - (StarterLevel.Origin + x)) < 1 && Math.Abs(s.Vertices.Average(v => v.Y) - (StarterLevel.Origin + y)) < 1 && s.Vertices.All(v => Math.Abs(v.Z - StarterLevel.Floor) < 1));
        var a = At(8.5f * StarterLevel.Tile, 3.5f * StarterLevel.Tile); var b = At(1.5f * StarterLevel.Tile, 5.5f * StarterLevel.Tile);
        Console.WriteLine($"pond tile: wad {a.Material?.SourceWadEntry} texture {a.Material?.TextureId} kind {a.Kind}; clicked tile: texture {b.Material?.TextureId} kind {b.Kind}");
        if (a.Material != new AuthoredMaterial(entry, 66) || a.Kind != AuthoredSurfaceKind.Water) throw new InvalidOperationException("The pond tile did not take the texture or lost its water.");
        if (b.Material != new AuthoredMaterial(entry, 66) || b.Kind != null) throw new InvalidOperationException("The clicked tile was not painted.");
        Console.WriteLine("NEW_LEVEL_TEXTURE_GRID_OK");
    }
}
