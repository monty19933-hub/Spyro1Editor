using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

/// <summary>The new world's homeworld (after new-world) gets a doorway to each of its levels, on meadow tiles in front of
/// the start: 71 a Stone Arch, 72 a Pointed Arch (WORLD_SHAPES to change them). Prints each doorway's position and heading
/// for the emulator script, and saves the homeworld.</summary>
internal static partial class NewLevelBuildCases
{
    private static void WorldDoorwaysStep(MainWindow window, string outDir)
    {
        var world = NewWorldDocument.Read(Workspace(window).RootPath) ?? throw new InvalidOperationException("The project has no new world (run new-world first).");
        var open = (Task)Invoke(window, "OpenWorldLevelAsync", NewWorldDocument.Homeworld)!;
        Wait(() => open.IsCompleted, "open the homeworld", 120);
        if (window.FreshSceneForTesting?.WorldLevelId != NewWorldDocument.Homeworld) throw new InvalidOperationException("The world's homeworld did not open.");
        string[] shapes = (Environment.GetEnvironmentVariable("WORLD_SHAPES") ?? "stone-arch,pointed-arch").Split(',');
        float t = StarterLevel.Tile, o = StarterLevel.Origin;
        var levels = world.LevelIds.Where(id => id != NewWorldDocument.Homeworld).ToArray();
        for (int i = 0; i < levels.Length; i++)
        {
            int destination = levels[i];
            // Tiles on the grass ahead of the start, a few tiles apart.
            float cx = o + (3.5f + 3 * i) * t, cy = o + 5.5f * t;
            var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
            var tile = geometry.Polygons.First(p => p.Points.Count == 4 && Math.Abs(p.Points.Average(v => v.X) - cx) < 1 && Math.Abs(p.Points.Average(v => v.Y) - cy) < 1);
            Set(window, "_selectedTerrain", tile); Set(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(tile));
            window.DoorwayChoiceForTesting = () => new(destination, shapes[i % shapes.Length], 1f, null, false);
            var add = window.AddDoorwayForTestingAsync();
            Wait(() => add.IsCompleted, "add doorway to " + destination, 120);
            window.DoorwayChoiceForTesting = null;
            Console.WriteLine("status: " + Status(window));
        }
        Save(window);
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld)));
        foreach (var portal in saved.Portals ?? [])
            Console.WriteLine($"doorway {portal.DestinationLevelId}: {portal.Position.X:0},{portal.Position.Y:0},{portal.Position.Z:0},{portal.Heading}:{portal.Shape}");
        if ((saved.Portals?.Count ?? 0) != levels.Length) throw new InvalidOperationException("The homeworld did not keep a doorway to each of its levels.");
        Snapshot(window, Path.Combine(outDir, "01-world-doorways.png"));
        Console.WriteLine("NEW_LEVEL_WORLD_DOORWAYS_OK");
    }
}
