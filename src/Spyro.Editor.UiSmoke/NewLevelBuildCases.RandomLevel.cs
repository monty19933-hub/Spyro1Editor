using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Levels;

/// <summary>File ▾ › Random Level….
/// <list type="bullet">
/// <item><c>random-level</c>: the dialog answered with RANDOM_SEED / RANDOM_STYLE / RANDOM_PLACE (new, world:NN or open) /
/// RANDOM_PICTURES (1: painted pictures): the level is saved where asked, opened in the editor, and is the same level the
/// generator makes for those choices (its faces, start and sky); painted pictures are written into the project.</item>
/// <item><c>random-dialog</c>: the real dialog, its preview, another seed, the kind and look changed, the pictures window,
/// then Make Level into the project's New Level slot and the ready window, with screenshots of each.</item>
/// </list></summary>
internal static partial class NewLevelBuildCases
{
    private static void RandomLevelStep(MainWindow window, string outDir)
    {
        long seed = long.Parse(Environment.GetEnvironmentVariable("RANDOM_SEED") ?? "4242");
        var style = Environment.GetEnvironmentVariable("RANDOM_STYLE") is { Length: > 0 } s ? Enum.Parse<LevelStyle>(s) : (LevelStyle?)null;
        string place = Environment.GetEnvironmentVariable("RANDOM_PLACE") ?? "new";
        bool pictures = Environment.GetEnvironmentVariable("RANDOM_PICTURES") == "1";
        var options = new RandomLevelOptions { Seed = seed, Style = style, Textures = pictures ? LevelTextureMode.GeneratedPictures : LevelTextureMode.GameTextures };
        var (where, world) = place switch
        {
            "open" => (MainWindow.RandomLevelPlace.OpenLevel, 0),
            _ when place.StartsWith("world:") => (MainWindow.RandomLevelPlace.World, int.Parse(place[6..])),
            _ => (MainWindow.RandomLevelPlace.NewLevel, 0),
        };
        window.RandomLevelChoiceForTesting = () => new(options, where, world);
        var task = (Task)Invoke(window, "RandomLevelAsync")!;
        Wait(() => task.IsCompleted, "File > Random Level", 900);
        task.GetAwaiter().GetResult();
        Console.WriteLine("status: " + Status(window));
        var made = window.LastRandomLevelForTesting ?? throw new InvalidOperationException("No random level was made: " + Status(window));
        Console.WriteLine("summary: " + made.Report.Summary);
        string root = Workspace(window).RootPath;
        string key = where switch { MainWindow.RandomLevelPlace.World => NewWorldDocument.KeyFor(world), MainWindow.RandomLevelPlace.NewLevel => "authored65", _ => window.FreshSceneForTesting?.TargetKey ?? "" };
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(root, key));
        var open = window.FreshSceneForTesting ?? throw new InvalidOperationException("The random level was not opened.");
        Console.WriteLine($"saved {key}: {saved.Name}, {saved.Surfaces.Count} faces, start {saved.Spawn}, world {saved.WorldLevelId}, binding {saved.TargetBinding?.LevelKey}");
        bool same = saved.Surfaces.Count == made.Scene.Surfaces.Count && saved.Spawn == made.Scene.Spawn && saved.Name == made.Scene.Name &&
            saved.Surfaces.Zip(made.Scene.Surfaces).All(p => p.First.Vertices.SequenceEqual(p.Second.Vertices) && p.First.Material == p.Second.Material && p.First.Kind == p.Second.Kind) &&
            (saved.CustomSky == null) == (made.Scene.CustomSky == null);
        if (!same) throw new InvalidOperationException("The saved level is not the generated one.");
        if (open.Name != saved.Name) throw new InvalidOperationException("The editor opened another level.");
        if (where == MainWindow.RandomLevelPlace.World && saved.WorldLevelId != world) throw new InvalidOperationException("The world level lost its slot.");
        // The generator alone makes the same level for the same choices (painted pictures aside, whose paths differ).
        if (!pictures)
        {
            var again = RandomLevelGenerator.Generate(options with { Name = made.Scene.Name }, Get<LevelCatalog>(window, "_retailCatalog"));
            if (again.Scene.Surfaces.Count != saved.Surfaces.Count || !again.Scene.Surfaces.Zip(saved.Surfaces).All(p => p.First.Vertices.SequenceEqual(p.Second.Vertices)))
                throw new InvalidOperationException("The same seed did not make the same level.");
        }
        else
        {
            int missing = made.Pictures.Count(p => !File.Exists(p.Path));
            Console.WriteLine($"pictures: {made.Pictures.Count} written under {Path.GetDirectoryName(made.Pictures.FirstOrDefault()?.Path)}, {missing} missing");
            if (made.Pictures.Count == 0 || missing > 0 || !saved.Surfaces.Any(f => f.Material?.Image != null)) throw new InvalidOperationException("Painted pictures were not used.");
        }
        Snapshot(window, Path.Combine(outDir, $"random-level-{seed}.png"));
        Console.WriteLine("NEW_LEVEL_RANDOM_LEVEL_OK");
    }

    private static void RandomDialogStep(MainWindow window, string outDir)
    {
        var task = (Task)Invoke(window, "RandomLevelAsync")!;
        Wait(() => window.OwnedWindows.Any(w => w.Name == "RandomLevelDialog"), "Random Level dialog", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "RandomLevelDialog");
        bool Ready() => Find<TextBlock>(dialog, "RandomLevelSummary").Text is { } t && !t.StartsWith("Making", StringComparison.Ordinal);
        Wait(Ready, "first preview", 120); Pump(10);
        Console.WriteLine("preview: " + Find<TextBlock>(dialog, "RandomLevelSummary").Text);
        Snapshot(dialog, Path.Combine(outDir, "01-random-level-dialog.png"));
        Find<ComboBox>(dialog, "RandomLevelKind").SelectedIndex = 3;   // islands in the sky
        Find<ComboBox>(dialog, "RandomLevelLook").SelectedIndex = 1 + LevelThemes.All.ToList().FindIndex(t => t.Key == "sky");
        Find<TextBox>(dialog, "RandomLevelName").Text = "";
        Click(Find<Button>(dialog, "RandomLevelRebuild")); Pump(5);
        Wait(Ready, "sky islands preview", 120); Pump(10);
        Console.WriteLine("sky preview: " + Find<TextBlock>(dialog, "RandomLevelSummary").Text);
        Snapshot(dialog, Path.Combine(outDir, "02-random-level-sky.png"));
        Click(Find<Button>(dialog, "RandomLevelGenerate")); Pump(5);
        Wait(Ready, "another seed", 120); Pump(10);
        Snapshot(dialog, Path.Combine(outDir, "03-random-level-another.png"));
        Click(Find<Button>(dialog, "RandomLevelOwnPictures"));
        Wait(() => dialog.OwnedWindows.Any(w => w.Name == "RandomLevelPicturesDialog"), "pictures window", 30);
        var pictures = dialog.OwnedWindows.First(w => w.Name == "RandomLevelPicturesDialog"); Pump(10);
        Snapshot(pictures, Path.Combine(outDir, "04-random-level-pictures.png"));
        Click(Find<Button>(pictures, "RandomLevelPicturesDone")); Pump(10);
        var place = Find<ComboBox>(dialog, "RandomLevelPlace");
        int newLevel = place.ItemsSource!.Cast<string>().ToList().FindIndex(p => p.StartsWith("As the project's new level", StringComparison.Ordinal));
        place.SelectedIndex = newLevel; Pump(5);
        Click(Find<Button>(dialog, "RandomLevelMake"));
        Wait(() => window.OwnedWindows.Any(w => w.Name == "RandomLevelReadyDialog"), "ready window", 600);
        var ready = window.OwnedWindows.First(w => w.Name == "RandomLevelReadyDialog"); Pump(10);
        Snapshot(ready, Path.Combine(outDir, "05-random-level-ready.png"));
        Console.WriteLine("ready: " + Find<TextBlock>(ready, "RandomLevelReadySummary").Text);
        Click(Find<Button>(ready, "RandomLevelReadyOk"));
        Wait(() => task.IsCompleted, "Random Level finished", 60);
        Pump(20);
        Snapshot(window, Path.Combine(outDir, "06-random-level-opened.png"));
        Console.WriteLine("status: " + Status(window));
        Console.WriteLine("NEW_LEVEL_RANDOM_DIALOG_OK");
    }
}
