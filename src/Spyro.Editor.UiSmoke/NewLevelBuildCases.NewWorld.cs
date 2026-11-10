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
        var tile = geometry.Polygons.First(p => p.Points.Count == 4 && Math.Abs(p.Points.Average(v => v.X) - (StarterLevel.Origin + 1.5f * StarterLevel.Tile)) < 1 &&
            Math.Abs(p.Points.Average(v => v.Y) - (StarterLevel.Origin + 1.5f * StarterLevel.Tile)) < 1);
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

    /// <summary><c>balloon-route</c>: File ▾ › World's Balloon Route… answered with BALLOON_AFTER (a homeworld key, or
    /// "off"), BALLOON_TO and BALLOON_ON (kind:count, kinds Nothing, Dragons, Treasure, DragonEggs) and BALLOON_NAME. The
    /// world file must hold the place, a place the game cannot have must be refused, and the status line must say who
    /// flies Spyro where. Without BALLOON_AFTER the dialog itself is opened, read and cancelled.</summary>
    private static void BalloonRouteStep(MainWindow window, string outDir)
    {
        string root = Workspace(window).RootPath;
        var world = NewWorldDocument.Read(root) ?? throw new InvalidOperationException("The project has no world: run new-world first.");
        static Spyro.Editor.Core.Exporting.BalloonRequirement Price(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Spyro.Editor.Core.Exporting.BalloonRequirement.Nothing;
            string[] parts = text.Split(':');
            var kind = Enum.Parse<Spyro.Editor.Core.Exporting.BalloonRequirementKind>(parts[0], true);
            return kind == Spyro.Editor.Core.Exporting.BalloonRequirementKind.Nothing ? Spyro.Editor.Core.Exporting.BalloonRequirement.Nothing : new(kind, int.Parse(parts[1]));
        }
        Task Run() { var t = (Task)Invoke(window, "BalloonRouteAsync")!; Wait(() => t.IsCompleted, "World's Balloon Route", 120); t.GetAwaiter().GetResult(); return t; }
        if (Environment.GetEnvironmentVariable("BALLOON_AFTER") is not { Length: > 0 } after)
        {
            // The dialog: its choices and what it says for each place; cancelled, the world keeps its place.
            var open = (Task)Invoke(window, "BalloonRouteAsync")!;
            Window? dialog = null;
            Wait(() => (dialog = window.OwnedWindows.FirstOrDefault(w => w.Name == "BalloonRouteDialog")) != null || open.IsCompleted, "the Balloon Route dialog", 60);
            if (dialog == null) throw new InvalidOperationException("The Balloon Route dialog did not open: " + Status(window));
            T Find<T>(string name) where T : Control => Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(dialog).OfType<T>().First(c => c.Name == name);
            var place = Find<ComboBox>("BalloonRouteAfter");
            var places = place.ItemsSource!.Cast<object>().Select(o => o.ToString()!).ToList();
            Console.WriteLine("places: " + string.Join(" | ", places));
            if (places.Count != 7 || !places[4].Contains("Dream Weavers") || !places[4].Contains("Gnasty") || !places[6].Contains("Not on")) throw new InvalidOperationException("The dialog does not offer the six places and off.");
            for (int i = 0; i < places.Count; i++)
            {
                place.SelectedIndex = i; Pump(10);
                Console.WriteLine($"  {places[i]}: {Find<TextBlock>("BalloonRouteNote").Text}");
                bool asks = Find<Grid>("BalloonRouteToWorldRow").IsVisible;
                if (asks != (i < 5)) throw new InvalidOperationException("The prices show for the wrong places.");
                if (!Find<Button>("BalloonRouteSave").IsEnabled) throw new InvalidOperationException("A place of the list cannot be saved: " + Find<TextBlock>("BalloonRouteProblem").Text);
            }
            // After Dream Weavers the game's own price there (6000 treasure) is offered for flying on.
            place.SelectedIndex = 4; Pump(10);
            if (Find<ComboBox>("BalloonRouteOnwardKind").SelectedIndex != 2 || (int)(Find<NumericUpDown>("BalloonRouteOnwardCount").Value ?? 0) != 6000 || Find<ComboBox>("BalloonRouteToWorldKind").SelectedIndex != 0)
                throw new InvalidOperationException("A new place does not start from the game's own pace.");
            Find<ComboBox>("BalloonRouteToWorldKind").SelectedIndex = 3; Pump(5);
            Find<NumericUpDown>("BalloonRouteToWorldCount").Value = 40; Pump(5);
            if (Find<NumericUpDown>("BalloonRouteToWorldCount").Value != 12) throw new InvalidOperationException("More dragon eggs than the game has can be asked.");
            Find<TextBox>("BalloonRouteBalloonist").Text = "nils 2"; Pump(5);
            if (Find<Button>("BalloonRouteSave").IsEnabled) throw new InvalidOperationException("A name the game cannot write can be saved.");
            Console.WriteLine("  problem shown: " + Find<TextBlock>("BalloonRouteProblem").Text);
            Snapshot(dialog, Path.Combine(outDir, "balloon-route-dialog.png"));
            Find<Button>("BalloonRouteCancel").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Wait(() => open.IsCompleted, "cancel", 60);
            if (NewWorldDocument.Read(root)!.Balloon != world.Balloon) throw new InvalidOperationException("Cancel changed the world's place.");
            Console.WriteLine("NEW_LEVEL_BALLOON_ROUTE_DIALOG_OK");
            return;
        }
        // A place the game cannot have is refused and nothing is saved.
        window.BalloonRouteChoiceForTesting = () => new("gnastysworld", new(Spyro.Editor.Core.Exporting.BalloonRequirementKind.Dragons, 5), Spyro.Editor.Core.Exporting.BalloonRequirement.Nothing);
        Run();
        Console.WriteLine("refused: " + Status(window));
        if (NewWorldDocument.Read(root)!.Balloon != world.Balloon || !Status(window).Contains("not changed")) throw new InvalidOperationException("A price was accepted around the last place.");
        var chosen = new WorldBalloonPlace(after, Price(Environment.GetEnvironmentVariable("BALLOON_TO")), Price(Environment.GetEnvironmentVariable("BALLOON_ON")),
            Environment.GetEnvironmentVariable("BALLOON_NAME") is { Length: > 0 } name ? name : WorldBalloonPlace.DefaultBalloonist);
        window.BalloonRouteChoiceForTesting = () => chosen;
        Run();
        Console.WriteLine("status: " + Status(window));
        var saved = NewWorldDocument.Read(root)!;
        if (saved.Balloon != chosen) throw new InvalidOperationException("The world file does not hold the chosen place.");
        if (chosen.OnRoute && !Status(window).Contains(world.Name)) throw new InvalidOperationException("The status line does not say who flies Spyro where.");
        Console.WriteLine($"world {saved.Name}: after {saved.Balloon!.After}, to the world {saved.Balloon.ToWorld.Describe()}, onward {saved.Balloon.Onward.Describe()}, balloonist {saved.Balloon.Balloonist}");
        Console.WriteLine("NEW_LEVEL_BALLOON_ROUTE_OK");
    }
}
