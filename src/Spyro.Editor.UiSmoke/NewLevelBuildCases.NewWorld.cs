using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

/// <summary>File ▾ › New World… (the dialog answered with WORLD_NAME, entry homeworld WORLD_ENTRY, WORLD_LEVELS levels): the
/// world file and its scenes, the level picker listing them after the game's levels, a level opened from the picker,
/// painted with the Ground card and saved, and the homeworld opened again.</summary>
internal static partial class NewLevelBuildCases
{
    private static void NewWorldStep(MainWindow window, string outDir)
    {
        string name = Environment.GetEnvironmentVariable("WORLD_NAME") ?? "SKY ISLES";
        string entry = Environment.GetEnvironmentVariable("WORLD_ENTRY") ?? "artisans";
        int levels = int.Parse(Environment.GetEnvironmentVariable("WORLD_LEVELS") ?? "2");
        window.NewWorldChoiceForTesting = () => new(name, entry, levels);
        var task = (Task)Invoke(window, "NewWorldAsync")!;
        Wait(() => task.IsCompleted, "File > New World", 120);
        task.GetAwaiter().GetResult();
        Console.WriteLine("status: " + Status(window));
        string root = Workspace(window).RootPath;
        var world = NewWorldDocument.Read(root) ?? throw new InvalidOperationException("No world file was written.");
        Console.WriteLine($"world {world.Name}: entry {world.EntryHomeworldKey}, levels {string.Join(",", world.LevelIds)}");
        if (world.Name != name || world.LevelIds.Count != levels + 1) throw new InvalidOperationException("The world file does not match the dialog.");
        var open = window.FreshSceneForTesting ?? throw new InvalidOperationException("New World opened no level.");
        if (open.WorldLevelId != NewWorldDocument.Homeworld || open.Name != name) throw new InvalidOperationException("New World did not open its homeworld.");

        var picker = Get<ComboBox>(window, "_levelJumpBox");
        var listed = picker.ItemsSource!.Cast<LevelDefinition>().Where(l => NewWorldDocument.IsWorldKey(l.Key, out _)).ToList();
        Console.WriteLine("picker lists: " + string.Join(" | ", listed.Select(l => $"{l.Key} {l.DisplayName}")));
        if (listed.Count != levels + 1) throw new InvalidOperationException("The level picker does not list the world's levels.");
        Snapshot(window, Path.Combine(outDir, "01-new-world-homeworld.png"));

        // Open LEVEL ONE through the picker, paint a tile and save.
        picker.SelectedItem = listed.First(l => l.LevelId == 71); Pump(20);
        Wait(() => window.FreshSceneForTesting?.WorldLevelId == 71, "LEVEL ONE opened from the picker", 120);
        var geometry = Get<Spyro.Editor.Core.Scene.GeometryCandidate>(window, "_currentGeometry");
        var tile = geometry.Polygons.First(p => p.Points.Count == 4 && Math.Abs(p.Points.Average(v => v.X) - 1.5f * StarterLevel.Tile) < 1 &&
            Math.Abs(p.Points.Average(v => v.Y) - 1.5f * StarterLevel.Tile) < 1);
        var paint = (Task)Invoke(window, "PaintGroundAsync", new List<Spyro.Editor.Core.Scene.TerrainPolygon> { tile }, GroundTypes.Snow)!;
        Wait(() => paint.IsCompleted, "paint", 60);
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(71)));
        int snow = saved.Surfaces.Count(s => GroundTypes.Of(s, Get<LevelCatalog>(window, "_retailCatalog")) == GroundTypes.Snow);
        Console.WriteLine($"LEVEL ONE saved with {snow} snow tile(s); world level {saved.WorldLevelId}, name {saved.Name}");
        if (snow != 1 || saved.WorldLevelId != 71) throw new InvalidOperationException("The world level was not saved as its own scene.");
        if (File.Exists(SceneDocumentStore.PathForTarget(root, "authored65")) &&
            SceneDocument.Read(SceneDocumentStore.PathForTarget(root, "authored65")).Surfaces.Any(s => GroundTypes.Of(s, Get<LevelCatalog>(window, "_retailCatalog")) == GroundTypes.Snow))
            throw new InvalidOperationException("Painting the world level changed the created level.");
        Snapshot(window, Path.Combine(outDir, "02-level-one.png"));

        picker.SelectedItem = picker.ItemsSource!.Cast<LevelDefinition>().First(l => l.Key == NewWorldDocument.KeyFor(70)); Pump(20);
        Wait(() => window.FreshSceneForTesting?.WorldLevelId == 70, "homeworld opened again", 120);
        Console.WriteLine("NEW_LEVEL_NEW_WORLD_OK");
    }
}
