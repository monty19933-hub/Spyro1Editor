using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

internal static partial class NewLevelBuildCases
{
    // WUMPA VILLAGE replaces Dark Hollow: Start Blank (Dark Hollow's saved work goes to Recovery > Restore Before
    // Clear), the village's terrain with its Crash pictures from the project's custom-terrain-textures/wumpa-village,
    // then its objects. wumpa-objects adds objects for good (each keeps a save row): run it once.
    private static void WumpaBuild(MainWindow window)
    {
        StartBlank(window, "darkhollow");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        string pictures = Path.Combine(Workspace(window).RootPath, "custom-terrain-textures", "wumpa-village");
        var doc = WumpaVillage.Scene(window.FreshSceneForTesting!, catalog.FindByKey("darkhollow")!.SourceWadEntry,
            catalog.FindByKey("townsquare")!.SourceWadEntry, pictures);
        Console.WriteLine($"faces {doc.Surfaces.Count}, pictures from {pictures}");
        ApplyScene(window, doc);
        Console.WriteLine("NEW_LEVEL_WUMPABUILD_OK");
    }

    // Tile Type in a built level, as the editor's dialog applies it: a pond tile is shown as water, set to solid
    // ground and back to water, and the saved scene keeps its water tiles.
    private static void WumpaTileType(MainWindow window)
    {
        Load(window, "darkhollow");
        var geometry = Get<Spyro.Editor.Core.Scene.GeometryCandidate>(window, "_currentGeometry");
        var water = geometry.Polygons.Where(p => p.SurfaceBehaviorEdit is { SurfaceType: 0, Param1: 0, Param2: 0 }).ToList();
        if (water.Count == 0) throw new InvalidOperationException("No water tiles in Wumpa Village.");
        string described = (string)Invoke(window, "DescribeSelectedTileType", water[0])!;
        Console.WriteLine($"water tiles {water.Count}; the Tiles card says: {described}");
        if (!described.Contains("costs a life", StringComparison.Ordinal)) throw new InvalidOperationException("The Tiles card does not name the water.");
        var solid = Spyro.Editor.Core.Analysis.NativeTerrainSurfaceSignature.Ordinary;
        var wet = new Spyro.Editor.Core.Analysis.NativeTerrainSurfaceSignature(0, 0, 0);
        Invoke(window, "ApplyBuiltTileType", new List<Spyro.Editor.Core.Scene.TerrainPolygon> { water[0] }, solid);
        var saved = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!; Wait(() => saved.IsCompleted, "save", 60);
        int afterSolid = window.FreshSceneForTesting!.Surfaces.Count(f => f.Kind != null);
        Invoke(window, "ApplyBuiltTileType", new List<Spyro.Editor.Core.Scene.TerrainPolygon> { water[0] }, wet);
        saved = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!; Wait(() => saved.IsCompleted, "save", 60);
        int afterWater = window.FreshSceneForTesting!.Surfaces.Count(f => f.Kind == AuthoredSurfaceKind.Water);
        Console.WriteLine($"saved water faces: {afterSolid} after making one solid, {afterWater} after making it water again");
        if (afterSolid != water.Count - 1 || afterWater != water.Count) throw new InvalidOperationException("Tile Type did not round-trip through the saved scene.");
        Console.WriteLine("NEW_LEVEL_WUMPATILETYPE_OK");
    }

    private static void WumpaObjects(MainWindow window)
    {
        Load(window, "darkhollow");
        if (window.FreshSceneForTesting is not { Name: WumpaVillage.Name }) throw new InvalidOperationException("Run wumpa-build first.");
        if (Mobys(window).Any(m => m.IsAdded && !m.IsRemoved)) throw new InvalidOperationException("Wumpa Village already has objects.");
        AddSkyObjects(window, WumpaVillage.Objects, removeAll: false);
        Console.WriteLine("NEW_LEVEL_WUMPAOBJECTS_OK");
    }
}
