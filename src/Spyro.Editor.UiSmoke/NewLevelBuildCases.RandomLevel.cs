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
        var options = new RandomLevelOptions
        {
            Seed = seed, Style = style, Textures = pictures ? LevelTextureMode.GeneratedPictures : LevelTextureMode.GameTextures,
            // RANDOM_LAYOUT: journey, loop, hub, arena, climb, gorge or web (unset: the seed's own); RANDOM_SIZE; RANDOM_LOOK.
            Layout = Environment.GetEnvironmentVariable("RANDOM_LAYOUT") is { Length: > 0 } layout ? layout : null,
            Size = Enum.TryParse<LevelSize>(Environment.GetEnvironmentVariable("RANDOM_SIZE"), out var size) ? size : LevelSize.Medium,
            Theme = Environment.GetEnvironmentVariable("RANDOM_LOOK") is { Length: > 0 } look ? look : null,
        };
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
            // With the same look source the window used: the look's own towers and trees are part of the level.
            var source = (Func<string, LevelPropSource?>)Invoke(window, "RandomLevelLookSource")!;
            var again = RandomLevelGenerator.Generate(options with { Name = made.Scene.Name }, Get<LevelCatalog>(window, "_retailCatalog"), source: source);
            if (again.Scene.Surfaces.Count != saved.Surfaces.Count || !again.Scene.Surfaces.Zip(saved.Surfaces).All(p => p.First.Vertices.SequenceEqual(p.Second.Vertices)))
                throw new InvalidOperationException("The same seed did not make the same level.");
        }
        else
        {
            int missing = made.Pictures.Count(p => !File.Exists(p.Path));
            Console.WriteLine($"pictures: {made.Pictures.Count} written under {Path.GetDirectoryName(made.Pictures.FirstOrDefault()?.Path)}, {missing} missing");
            if (made.Pictures.Count == 0 || missing > 0 || !saved.Surfaces.Any(f => f.Material?.Image != null)) throw new InvalidOperationException("Painted pictures were not used.");
        }
        // The sky below the horizon in the ground's colour (no sky through the game's hairline gaps), and the level's ways.
        Console.WriteLine($"sky below: {saved.SkyBelow?.ToString() ?? "as it is"}; ways: {saved.Ways?.Count ?? 0}; music track {saved.MusicTrack}");
        if (saved.SkyBelow == null && made.Report.Style != LevelStyle.SkyIslands) throw new InvalidOperationException("The level's sky is not painted the ground's colour below the horizon.");
        // Its music can be changed afterwards: Game Settings › Music shows for a level you built, and saves in its scene.
        var musicPanel = Get<Control?>(window, "_retailMusicDisclosure");
        var tracks = Get<ComboBox>(window, "_levelMusicTrackBox");
        if (musicPanel is not { IsVisible: true } || !tracks.IsEnabled) throw new InvalidOperationException("The Music settings are not there for a built level.");
        var other = tracks.ItemsSource!.Cast<Spyro.Editor.Core.Music.MusicTrackEntry>().First(t => t.TrackId != saved.MusicTrack);
        tracks.SelectedItem = other; Pump(5);
        var saveMusic = (Task)Invoke(window, "SaveLevelMusicPlanAsync")!;
        Wait(() => saveMusic.IsCompleted, "Save Music", 120);
        var resaved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, window.FreshSceneForTesting!.TargetKey));
        Console.WriteLine($"music changed to {other.DisplayName}: saved track {resaved.MusicTrack}");
        if (resaved.MusicTrack != other.TrackId) throw new InvalidOperationException("The built level's music did not save.");
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
        Find<ComboBox>(dialog, "RandomLevelLook").SelectedIndex = 1 + LevelThemes.All.ToList().FindIndex(t => t.Key == "loftycastle");
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

    /// <summary><c>random-homeworld</c>: File ▾ › Random Homeworld… answered with RANDOM_SEED / RANDOM_STYLE / RANDOM_LOOK /
    /// RANDOM_DIFFICULTY, RANDOM_LAYOUT and RANDOM_PLACE: <c>open</c> (in place of the homeworld RANDOM_HOME, default artisans) or
    /// <c>world:70</c> (the project's new world). The homeworld must come out with a doorway, frame and all, to every
    /// level its place leads to, each on the floor with open floor in front; RANDOM_FILL=1 then fills it with Random
    /// objects (a game homeworld gets its balloonist). Writes homeworld.json (start and doorways) for the in-game run.</summary>
    private static void RandomHomeworldStep(MainWindow window, string outDir)
    {
        long seed = long.Parse(Environment.GetEnvironmentVariable("RANDOM_SEED") ?? "4242");
        var style = Environment.GetEnvironmentVariable("RANDOM_STYLE") is { Length: > 0 } s ? Enum.Parse<LevelStyle>(s) : (LevelStyle?)null;
        string place = Environment.GetEnvironmentVariable("RANDOM_PLACE") ?? "open";
        string homeKey = Environment.GetEnvironmentVariable("RANDOM_HOME") is { Length: > 0 } h ? h : "artisans";
        var difficulty = Enum.TryParse<LevelDifficulty>(Environment.GetEnvironmentVariable("RANDOM_DIFFICULTY"), out var d) ? d : LevelDifficulty.Medium;
        var options = new RandomLevelOptions { Seed = seed, Style = style, Theme = Environment.GetEnvironmentVariable("RANDOM_LOOK") is { Length: > 0 } look ? look : null, Difficulty = difficulty, Doorways = 1,
            Layout = Environment.GetEnvironmentVariable("RANDOM_LAYOUT") is { Length: > 0 } layout ? layout : null };
        var (where, world) = place.StartsWith("world:") ? (MainWindow.RandomLevelPlace.World, int.Parse(place[6..])) : (MainWindow.RandomLevelPlace.OpenLevel, 0);
        if (where == MainWindow.RandomLevelPlace.OpenLevel) Load(window, homeKey);
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        var expected = (IReadOnlyList<string>)Invoke(window, "RandomHomeworldDoorways", where, world)!;
        Console.WriteLine($"doorways wanted: {string.Join(", ", expected)}");
        if (expected.Count == 0) throw new InvalidOperationException("The place is not a homeworld.");
        window.RandomLevelChoiceForTesting = () => new(options, where, world);
        var task = window.RandomHomeworldForTestingAsync();
        Wait(() => task.IsCompleted, "File > Random Homeworld", 900);
        task.GetAwaiter().GetResult();
        Console.WriteLine("status: " + Status(window));
        var made = window.LastRandomLevelForTesting ?? throw new InvalidOperationException("No homeworld was made: " + Status(window));
        Console.WriteLine("summary: " + made.Report.Summary);
        var open = window.FreshSceneForTesting ?? throw new InvalidOperationException("The homeworld was not opened.");
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, open.TargetKey));
        var doors = saved.Portals ?? [];
        Console.WriteLine($"saved {open.TargetKey}: {saved.Name}, {saved.Surfaces.Count} faces, {doors.Count} doorways, world {saved.WorldLevelId}, binding {saved.TargetBinding?.LevelKey}");
        if (made.Doorways.Count != expected.Count) throw new InvalidOperationException($"The generator kept {made.Doorways.Count} doorway places for {expected.Count} doorways.");
        if (doors.Count != expected.Count || (open.Portals?.Count ?? 0) != expected.Count) throw new InvalidOperationException($"The homeworld has {doors.Count} doorways, not {expected.Count}.");
        var faces = saved.Surfaces.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        var floors = new LevelFill.FloorGrid(saved);
        foreach (var door in doors.OrderBy(x => x.DestinationLevelId))
        {
            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
            var walk = Enumerable.Range(0, 12).Select(n => new Spyro.Editor.Core.Primitives.Vector3f(door.Position.X - (float)fx * n * 60, door.Position.Y - (float)fy * n * 60, door.Position.Z)).ToList();
            bool frame = door.Shape != null && door.FrameFaces is { Count: > 20 } ids && ids.All(faces.Contains), floor = floors.Holds(walk, 30);
            Console.WriteLine($"    to {door.DestinationLevelId}: {door.Shape} at {door.Position.X:0},{door.Position.Y:0},{door.Position.Z:0} heading {door.Heading}, frame {door.FrameFaces?.Count ?? 0} faces{(floor ? "" : ", NO FLOOR IN FRONT")}");
            if (!frame || !floor) throw new InvalidOperationException($"The doorway to {door.DestinationLevelId} has no frame or no floor in front of it.");
        }
        if (SceneDoorways.Destinations(saved, catalog, id => "LEVEL", doors.FirstOrDefault(x => x.DestinationLevelId % 10 == 0 && x.DestinationLevelId <= 60)?.DestinationLevelId).Any(x => !x.Taken && x.LevelId is not (>= 71 and <= 74)))
            throw new InvalidOperationException("A level the homeworld leads to has no doorway.");
        Snapshot(window, Path.Combine(outDir, $"random-homeworld-{seed}.png"));
        if (Environment.GetEnvironmentVariable("RANDOM_FILL") == "1")
        {
            string type = Environment.GetEnvironmentVariable("RANDOM_OBJECTS_LEVEL") is { Length: > 0 } t ? t : made.Report.Theme;
            var choice = new MainWindow.RandomObjectsChoice(type, "", 0, MainWindow.RandomObjectsEnemies(difficulty, int.Parse(Environment.GetEnvironmentVariable("RANDOM_TREASURE") ?? "100")), int.Parse(Environment.GetEnvironmentVariable("RANDOM_TREASURE") ?? "100"), 0, Seed: 7, Difficulty: difficulty);
            var fill = (Task)Invoke(window, "RandomizeObjectsAsync", choice)!;
            Wait(() => fill.IsCompleted, "Random objects", 1800);
            fill.GetAwaiter().GetResult();
            Console.WriteLine("objects: " + Status(window));
            var placed = window.LastRandomObjects;
            Console.WriteLine("    " + string.Join(", ", placed.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Value} {x.Key}")));
            if (where == MainWindow.RandomLevelPlace.OpenLevel && placed.GetValueOrDefault("balloonist") != 1) throw new InvalidOperationException("The homeworld has no balloonist.");
            if (window.LastRandomObjectsRefusal is { } refusal) throw new InvalidOperationException("Create BIN would refuse what Random objects placed: " + refusal);
            var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
            Wait(() => save.IsCompleted, "Save Level", 120);
            if (!save.GetAwaiter().GetResult()) throw new InvalidOperationException("The homeworld did not save: " + Status(window));
            Snapshot(window, Path.Combine(outDir, $"random-homeworld-{seed}-objects.png"));
            saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, open.TargetKey));
        }
        File.WriteAllText(Path.Combine(outDir, "homeworld.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            name = saved.Name, spawn = new[] { saved.Spawn.X, saved.Spawn.Y, saved.Spawn.Z },
            doorways = (saved.Portals ?? []).Select(x => new { to = x.DestinationLevelId, at = new[] { x.Position.X, x.Position.Y, x.Position.Z }, heading = x.Heading, shape = x.Shape }).ToArray(),
            objects = saved.Objects.Select(o => new { label = o.Value.Label, at = new[] { o.Value.Position.X, o.Value.Position.Y, o.Value.Position.Z } }).ToArray(),
        }));
        Console.WriteLine("NEW_LEVEL_RANDOM_HOMEWORLD_OK");
    }

    /// <summary><c>random-homeworld-dialog</c>: the real window opened by File ▾ › Random Homeworld… with the homeworld
    /// RANDOM_HOME (default artisans) open: it must offer only homeworld places, name the doorways, and preview a
    /// homeworld with a place for each; then Hard is chosen and it is rebuilt; then the window is set to make a level
    /// and back. Pictures of each; the window is cancelled.</summary>
    private static void RandomHomeworldDialogStep(MainWindow window, string outDir)
    {
        Load(window, Environment.GetEnvironmentVariable("RANDOM_HOME") is { Length: > 0 } h ? h : "artisans");
        window.RandomLevelChoiceForTesting = null;
        var task = window.RandomHomeworldForTestingAsync();
        Wait(() => window.OwnedWindows.Any(w => w.Name == "RandomLevelDialog"), "Random Homeworld window", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "RandomLevelDialog");
        bool Ready() => Find<TextBlock>(dialog, "RandomLevelSummary").Text is { } t && !t.StartsWith("Making", StringComparison.Ordinal);
        Wait(Ready, "homeworld preview", 120); Pump(10);
        var makes = Find<ComboBox>(dialog, "RandomLevelMakes"); var place = Find<ComboBox>(dialog, "RandomLevelPlace");
        string note = Find<TextBlock>(dialog, "RandomLevelMakesNote").Text ?? "", summary = Find<TextBlock>(dialog, "RandomLevelSummary").Text ?? "";
        Console.WriteLine("places: " + string.Join(" | ", place.ItemsSource!.Cast<string>()));
        Console.WriteLine("note: " + note);
        Console.WriteLine("preview: " + summary);
        if (makes.SelectedIndex != 1 || !note.Contains("doorway", StringComparison.Ordinal) || !summary.Contains("a homeworld", StringComparison.Ordinal))
            throw new InvalidOperationException("The window did not open set to make a homeworld.");
        if (place.ItemsSource!.Cast<string>().Any(p => p.StartsWith("As the project's new level", StringComparison.Ordinal)))
            throw new InvalidOperationException("A place that is no homeworld is offered for a homeworld.");
        // The place, Fill In and the buttons stay at the window's foot, outside the part that scrolls (a window taller
        // than the screen scrolls its choices, and Make Homeworld is never under the Dock).
        var scroll = Find<ScrollViewer>(dialog, "RandomLevelScroll");
        foreach (string pinned in new[] { "RandomLevelMake", "RandomLevelCancel", "RandomLevelPlace" })
            if (Avalonia.LogicalTree.LogicalExtensions.GetLogicalAncestors(Find<Control>(dialog, pinned)).Contains(scroll))
                throw new InvalidOperationException($"{pinned} scrolls with the choices instead of staying at the window's foot.");
        if (!dialog.CanResize) throw new InvalidOperationException("The Random Level window cannot be made smaller.");
        Console.WriteLine("RANDOM_LEVEL_WINDOW_FOOT_OK");
        // Music: the look's own by default, every song of the game to choose from (a game homeworld's place also takes
        // the songs brought in from other games).
        var music = Find<ComboBox>(dialog, "RandomLevelMusic");
        var songs = music.ItemsSource!.Cast<string>().ToList();
        Console.WriteLine($"music: {songs.Count} choices, first {songs[0]}");
        if (music.SelectedIndex != 0 || songs.Count < 30 || !songs[0].StartsWith("The look's own", StringComparison.Ordinal))
            throw new InvalidOperationException("The Random Level window has no music choice.");
        Console.WriteLine("RANDOM_LEVEL_MUSIC_OK");
        Snapshot(dialog, Path.Combine(outDir, "01-random-homeworld-dialog.png"));
        Find<ComboBox>(dialog, "RandomLevelDifficulty").SelectedIndex = 2;
        Click(Find<Button>(dialog, "RandomLevelRebuild")); Pump(5);
        Wait(Ready, "hard preview", 120); Pump(10);
        Console.WriteLine("hard: " + Find<TextBlock>(dialog, "RandomLevelSummary").Text);
        if (Find<TextBlock>(dialog, "RandomLevelSummary").Text?.Contains(", hard,", StringComparison.Ordinal) != true) throw new InvalidOperationException("The difficulty was not used.");
        Snapshot(dialog, Path.Combine(outDir, "02-random-homeworld-hard.png"));
        makes.SelectedIndex = 0; Pump(5);
        Wait(Ready, "level preview", 120); Pump(10);
        Console.WriteLine("as a level: " + string.Join(" | ", place.ItemsSource!.Cast<string>()) + " :: " + Find<TextBlock>(dialog, "RandomLevelSummary").Text);
        if (Find<TextBlock>(dialog, "RandomLevelSummary").Text?.Contains("a homeworld", StringComparison.Ordinal) == true) throw new InvalidOperationException("A level came out as a homeworld.");
        Snapshot(dialog, Path.Combine(outDir, "03-random-level-again.png"));
        makes.SelectedIndex = 1; Pump(5);
        Wait(Ready, "homeworld preview again", 120); Pump(10);
        Click(Find<Button>(dialog, "RandomLevelCancel"));
        Wait(() => task.IsCompleted, "window closed", 60);
        Console.WriteLine("NEW_LEVEL_RANDOM_HOMEWORLD_DIALOG_OK");
    }

    /// <summary><c>egg-thief</c>: a random level in Stone Hill's place (its place has an egg thief), filled by Random
    /// objects; the thief is selected: Edit Route… and the run tools must show; New Loop Here, Bigger, Turn and Other Way
    /// Round each change its run, every point on the floor and every step one the game can follow, and As It Came
    /// brings the first run back; the run is saved on the object. Then the egg thieves' colour is set (EGG_THIEF_COLOUR,
    /// default #C82828), saved, and the level saved. Writes thief.json (the thief and his run) for the in-game run.</summary>
    private static void EggThiefStep(MainWindow window, string outDir)
    {
        Load(window, "stonehill");
        Environment.SetEnvironmentVariable("RANDOM_PLACE", "open");
        RandomLevelStep(window, outDir);
        var fill = (Task)Invoke(window, "RandomizeObjectsAsync", new MainWindow.RandomObjectsChoice("stonehill", "", 0, 8, 150, 0, Seed: 5))!;
        Wait(() => fill.IsCompleted, "Random objects", 1800);
        fill.GetAwaiter().GetResult();
        Console.WriteLine("objects: " + Status(window));
        var mobys = Get<List<Spyro.Editor.Core.Scene.Moby>>(window, "_currentMobys");
        var thief = mobys.FirstOrDefault(m => !m.IsRemoved && m.IsAdded && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Random objects placed no egg thief in Stone Hill's place.");
        SetField(window, "_selectedMoby", thief);
        Invoke(window, "RefreshActionAvailability"); Pump(10);
        var card = Get<Border>(window, "_routeToolsCard"); var note = Get<TextBlock>(window, "_routeToolsNote");
        var edit = Get<Button>(window, "_objectNativeMovementButton");
        Console.WriteLine($"thief {thief.DisplayLabel} at {thief.Position}; card {card.IsVisible}; button {edit.IsVisible} \"{edit.Content}\"; note: {note.Text}");
        if (!card.IsVisible || !edit.IsVisible || edit.Content?.ToString() != "Edit Route…" || note.Text?.Contains("egg thief runs", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("The egg thief's run tools are not shown.");
        List<int[]> Nodes() => ((Spyro.Editor.Core.Scene.NativeMobyPath)Invoke(window, "SelectedNativeMobyPath")!).RawNodes();
        string Shape(List<int[]> nodes) => string.Join(" ", nodes.Select(n => $"{n[0] / 16},{n[1] / 16},{n[2] / 16}"));
        var first = Nodes();
        Console.WriteLine($"    as it came: {Shape(first)}");
        var floors = new LevelFill.FloorGrid((SceneDocument)Invoke(window, "CaptureFreshScene")!);
        string last = Shape(first);
        foreach (string how in new[] { "loop", "bigger", "turn", "reverse", "smaller" })
        {
            window.ReshapeSelectedRouteForTesting(how); Pump(5);
            var nodes = Nodes();
            string shape = Shape(nodes);
            Console.WriteLine($"    {how}: {shape} — {Status(window)}");
            if (shape == last) throw new InvalidOperationException($"{how} did not change the run: {Status(window)}");
            if (nodes.Count != first.Count) throw new InvalidOperationException("The run lost or gained points.");
            if (!floors.Holds(nodes.Select(n => new Spyro.Editor.Core.Primitives.Vector3f(n[0] / 16f, n[1] / 16f, n[2] / 16f)), 60)) throw new InvalidOperationException($"{how} left a point off the floor.");
            for (int i = 0; i < nodes.Count; i++)
            {
                var a = nodes[i]; var b = nodes[(i + 1) % nodes.Count];
                if (Math.Abs(a[0] - b[0]) > 32767 || Math.Abs(a[1] - b[1]) > 32767 || Math.Abs(a[2] - b[2]) > 32767) throw new InvalidOperationException($"{how} made a step the game cannot follow.");
            }
            if (thief.UniversalPathNodes == null) throw new InvalidOperationException("The run was not saved on the thief.");
            // A thief cannot jump: the run stays on one level of floor.
            if (nodes.Max(q => q[2]) - nodes.Min(q => q[2]) > 40 * 16) throw new InvalidOperationException($"{how} put the run up a ledge or down a drop.");
            last = shape;
        }
        Snapshot(window, Path.Combine(outDir, "egg-thief-run.png"));
        var kept = Nodes();
        window.ReshapeSelectedRouteForTesting("reset"); Pump(5);
        // The run it came with from its own level (Random objects had already fitted it to this floor), moved here.
        if (thief.UniversalPathNodes != null || ((Spyro.Editor.Core.Scene.NativeMobyPath)Invoke(window, "SelectedNativeMobyPath")!).HasEdits) throw new InvalidOperationException("As It Came did not bring the first run back.");
        // The shaped run again, for the game.
        window.ReshapeSelectedRouteForTesting("loop"); Pump(5);
        window.ReshapeSelectedRouteForTesting("bigger"); Pump(5);
        var run = Nodes();
        string colour = Environment.GetEnvironmentVariable("EGG_THIEF_COLOUR") is { Length: > 0 } c ? c : "#C82828";
        if (!Spyro.Editor.Core.Persistence.SpyroColourSettings.TryParse(colour, out var parsed)) throw new InvalidOperationException("EGG_THIEF_COLOUR is not #RRGGBB.");
        window.SetEggThiefColourForTesting(parsed); Pump(10);
        if (window.EggThiefColourForTesting != parsed || Spyro.Editor.Core.Persistence.EggThiefColourSettings.Load(Workspace(window).RootPath) != parsed)
            throw new InvalidOperationException("The egg thieves' colour was not saved.");
        Console.WriteLine("colour: " + Get<TextBlock>(window, "_eggThiefSummary").Text);
        var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
        Wait(() => save.IsCompleted, "Save Level", 120);
        if (!save.GetAwaiter().GetResult()) throw new InvalidOperationException("The level did not save: " + Status(window));
        File.WriteAllText(Path.Combine(outDir, "thief.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            at = new[] { thief.Position.X, thief.Position.Y, thief.Position.Z }, colour,
            run = run.Select(n => new[] { n[0] / 16f, n[1] / 16f, n[2] / 16f }).ToArray(),
        }));
        Console.WriteLine("NEW_LEVEL_EGG_THIEF_OK");
    }

    /// <summary><c>random-objects</c>: a random level (as <c>random-level</c>), then Objects › Random objects three times:
    /// RANDOM_OBJECTS_LEVEL (level type, default stonehill), RANDOM_ENEMY_LEVEL (default the same), RANDOM_TREASURE
    /// (default 300), RANDOM_EGGS (default 0). Each press must leave the level's treasure exactly as asked, with every
    /// gem colour, replace the last press's objects without using more save rows, and Remove Them must take them all
    /// away. The second press asks for teal and rainbow gems too (every press with RANDOM_HIGH_GEMS=1): the level then
    /// has as many of each as its treasure allows, none otherwise, and Create BIN's dry run still takes the
    /// arrangement. The level type's scenery is placed with every press. The last arrangement is saved (run the
    /// <c>export</c> step next to play it).</summary>
    private static void RandomObjectsStep(MainWindow window, string outDir)
    {
        RandomLevelStep(window, outDir);
        string type = Environment.GetEnvironmentVariable("RANDOM_OBJECTS_LEVEL") is { Length: > 0 } t ? t : "stonehill";
        string enemies = Environment.GetEnvironmentVariable("RANDOM_ENEMY_LEVEL") is { Length: > 0 } e ? e : "";
        int treasure = int.Parse(Environment.GetEnvironmentVariable("RANDOM_TREASURE") ?? "300"), eggs = int.Parse(Environment.GetEnvironmentVariable("RANDOM_EGGS") ?? "0");
        List<Spyro.Editor.Core.Scene.Moby> Live() => [.. Get<List<Spyro.Editor.Core.Scene.Moby>>(window, "_currentMobys").Where(m => !m.IsRemoved)];
        int Rows() => ((SceneDocument)Invoke(window, "CaptureFreshScene")!).Ledger.Entries.Count;
        string? firstShape = null; int firstRows = 0;
        for (int press = 1; press <= 3; press++)
        {
            if (press == 3)
            {
                int gone = (int)Invoke(window, "RemoveRandomObjects", true)!;
                Console.WriteLine($"removed {gone}: {Status(window)}");
                var after = Get<SceneDocument>(window, "_freshSceneDocument");
                if (gone == 0 || after.RandomObjects != null) throw new InvalidOperationException("Remove Them left random objects behind.");
            }
            bool rare = press == 2 || Environment.GetEnvironmentVariable("RANDOM_HIGH_GEMS") == "1";
            var choice = new MainWindow.RandomObjectsChoice(type, enemies, 0, MainWindow.RandomObjectsEnemies(LevelDifficulty.Medium, treasure), treasure, eggs, Seed: 100 + press, TealGems: rare, RainbowGems: rare);
            var task = (Task)Invoke(window, "RandomizeObjectsAsync", choice)!;
            Wait(() => task.IsCompleted, "Random objects", 1800);
            task.GetAwaiter().GetResult();
            Console.WriteLine($"press {press}: {Status(window)}");
            var placed = window.LastRandomObjects;
            Console.WriteLine("    " + string.Join(", ", placed.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Value} {p.Key}")));
            if (window.LastRandomObjectsRefusal is { } refusal) throw new InvalidOperationException("Create BIN would refuse what Random objects placed: " + refusal);
            var live = Live();
            int total = live.Sum(m => m.TreasureValue);
            var colours = live.Where(m => m.IsVisibleGem).Select(m => m.TreasureValue).Distinct().Order().ToList();
            int rows = Rows();
            Console.WriteLine($"    treasure {total}, gem colours {string.Join(",", colours)}, {live.Count} objects, {rows} save rows");
            if (total != treasure) throw new InvalidOperationException($"The level's treasure is {total}, not {treasure}.");
            if (treasure >= 150 && !new[] { 1, 2, 5, 10, 25 }.All(c => colours.Contains(c))) throw new InvalidOperationException("Not every gem colour is in the level.");
            if (placed.GetValueOrDefault("enemy") < 3) throw new InvalidOperationException("Hardly any enemies were placed.");
            // One extra life, two in a level of 400 gems or more.
            if (placed.GetValueOrDefault("basket") < 1 || placed.GetValueOrDefault("extra life chest") != (treasure >= 400 ? 2 : 1)) throw new InvalidOperationException("Chests are missing.");
            if (eggs > 0 && placed.GetValueOrDefault("dragonfly egg") != eggs) throw new InvalidOperationException("Dragonfly eggs are missing.");
            // Teal and rainbow gems: as many as the treasure allows when asked for, none otherwise; they are gems of
            // the level like any other (the treasure above counts them).
            // (None when the level type's objects leave no place for their sparkle: Random objects then says so and
            // places ordinary gems.)
            bool refusedRare = Status(window).Contains("Teal and rainbow gems were left out", StringComparison.Ordinal);
            if (refusedRare) Console.WriteLine("    teal and rainbow gems left out for this level type (said in the note)");
            var (teals, rainbows) = rare && !refusedRare ? LevelScatter.HighGemsFor(treasure, true, true) : (0, 0);
            int teal = live.Count(m => m.IsVisibleGem && m.TreasureValue == 50), rainbow = live.Count(m => m.IsVisibleGem && m.TreasureValue == 100);
            Console.WriteLine($"    teal gems {teal} (asked {teals}), rainbow gems {rainbow} (asked {rainbows})");
            if (teal != teals || rainbow != rainbows || placed.GetValueOrDefault("teal gem") != teals || placed.GetValueOrDefault("rainbow gem") != rainbows)
                throw new InvalidOperationException($"The level has {teal} teal and {rainbow} rainbow gems, not {teals} and {rainbows}.");
            int scenery = placed.GetValueOrDefault("tree") + placed.GetValueOrDefault("torch") + placed.GetValueOrDefault("wall lamp") + placed.GetValueOrDefault("plant");
            Console.WriteLine($"    scenery: {placed.GetValueOrDefault("tree")} trees, {placed.GetValueOrDefault("torch")} torches and lamps, {placed.GetValueOrDefault("wall lamp")} wall lamps, {placed.GetValueOrDefault("plant")} flowers and grasses");
            if (type is "stonehill" or "artisans" or "drycanyon" && scenery < 3) throw new InvalidOperationException("The level type's scenery was not placed.");
            string shape = string.Join(";", live.OrderBy(m => m.Position.X).ThenBy(m => m.Position.Y).Select(m => $"{m.Position.X:0},{m.Position.Y:0}"));
            if (press == 1) { firstShape = shape; firstRows = rows; }
            else if (shape == firstShape) throw new InvalidOperationException("Randomize placed the same arrangement again.");
            // A new arrangement takes the last one's save rows first.
            if (press > 1 && rows > firstRows + 24) throw new InvalidOperationException($"Randomize used {rows - firstRows} more save rows.");
            Snapshot(window, Path.Combine(outDir, $"random-objects-{press}.png"));
        }
        var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
        Wait(() => save.IsCompleted, "Save Level", 120);
        if (!save.GetAwaiter().GetResult()) throw new InvalidOperationException("The level did not save: " + Status(window));
        string key = window.FreshSceneForTesting?.TargetKey ?? "";
        var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, key));
        if (saved.RandomObjects == null || saved.RandomObjects.Count == 0 || saved.Objects.Count(o => o.Value.UniversalRewardByte != null) == 0)
            throw new InvalidOperationException("The saved level does not remember what Randomize placed.");
        Console.WriteLine($"saved {key}: {saved.Objects.Count} objects, {saved.RandomObjects.Count} from Randomize");
        Console.WriteLine("NEW_LEVEL_RANDOM_OBJECTS_OK");
    }
}
