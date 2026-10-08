using Spyro.Editor.App.Views;
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

    private static void WumpaObjects(MainWindow window)
    {
        Load(window, "darkhollow");
        if (window.FreshSceneForTesting is not { Name: WumpaVillage.Name }) throw new InvalidOperationException("Run wumpa-build first.");
        if (Mobys(window).Any(m => m.IsAdded && !m.IsRemoved)) throw new InvalidOperationException("Wumpa Village already has objects.");
        AddSkyObjects(window, WumpaVillage.Objects, removeAll: false);
        Console.WriteLine("NEW_LEVEL_WUMPAOBJECTS_OK");
    }
}
