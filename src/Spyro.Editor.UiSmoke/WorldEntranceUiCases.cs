using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

/// <summary><c>--world-entrance-ui-only</c>: Terrain › World Entrance on a throwaway project, no game written. The project
/// has a new world entered from Artisans; with Artisans open (as one of the game's levels) the card offers Place Entrance…,
/// which puts the doorway on the selected tile (saved as world-entrance.json and drawn over the level), then move, turn,
/// Shape…, the homeworld's own textures and Remove work through the card; another homeworld offers Use This Homeworld;
/// a project without a world, or a level that is not a homeworld, shows no card. Without a disc the card asks for one to
/// check the room; with WORLD_ENTRANCE_DISC (the original USA disc, read only) it says how much sky fits.</summary>
internal static class WorldEntranceUiCases
{
    private static int _checks;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    internal static void Run()
    {
        _checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-world-entrance-ui-").FullName;
        string root = Directory.CreateDirectory(Path.Combine(parent, "project")).FullName;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(repo) ?? throw new InvalidOperationException("No level catalog.");
        File.Copy(Path.Combine(repo, "spyro-level-catalog.json"), Path.Combine(root, "spyro-level-catalog.json"));
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        string?[] previousRoots = [.. IsolatedRoots.Select(Environment.GetEnvironmentVariable)];
        string? disc = Environment.GetEnvironmentVariable("WORLD_ENTRANCE_DISC");
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
            var catalog = LevelCatalog.Load(root);
            var window = new MainWindow { Width = 1440, Height = 900 };
            window.Show(); Pump(30);
            var stone = GroundTypes.Stone.Material(catalog);
            if (disc != null) Get<TextBox>(window, "_discImagePathBox").Text = disc;

            // The view needs ground to place on: a floor of four tiles (a scene), then the window is told it shows the game's
            // own Artisans (as it does after choosing Artisans in the level picker).
            AuthoredSurface Tile(float x, float y) => new(Guid.NewGuid().ToString("N"), [new(x, y, 384), new(x + 256, y, 384), new(x + 256, y + 256, 384), new(x, y + 256, 384)], new(128, 128, 128), stone);
            var floor = SceneDocument.New("FLOOR") with { Surfaces = [Tile(4200, 2800), Tile(4456, 2800), Tile(4200, 3056), Tile(4456, 3056)], Spawn = new(4500, 3000, 420) };
            Invoke(window, "ApplyFreshScene", floor); Pump(20);
            SelectTab(window, "Terrain");
            void ShowOriginal(string key)
            {
                SetField(window, "_freshSceneDocument", null);
                SetField(window, "_currentLevel", catalog.FindByKey(key));
                Invoke(window, "RefreshWorldEntranceCard"); Pump(5);
            }
            ShowOriginal("artisans");
            var card = Find<Control>(window, "TerrainWorldEntranceCard");
            Check(!card.IsVisible, "without a new world, Artisans shows no World Entrance card");

            // (file writes awaited on the UI thread deadlock in this harness: run them on the pool)
            Task.Run(() => new NewWorldDocument(1, "SKY ISLES", "artisans", [70, 71]).SaveAsync(root)).GetAwaiter().GetResult();
            Invoke(window, "RefreshWorldEntranceCard"); Pump(5);
            Check(card.IsVisible && Find<Button>(window, "WorldEntrancePlace").IsVisible && !Find<Control>(window, "WorldEntranceTools").IsVisible,
                "with a world entered from Artisans, Artisans offers Place Entrance…");
            Check(Text(window, "WorldEntranceNote").Contains("SKY ISLES has no doorway yet"), "the card says there is no doorway yet: " + Text(window, "WorldEntranceNote"));

            // Place Entrance… on the selected tile (the window's answers come from the test hook).
            var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            int tile = geometry.Polygons.FindIndex(p => p.Points.Any(q => q.X == 4712 && q.Y == 3312));
            SetField(window, "_selectedTerrain", geometry.Polygons[tile]); SetField(window, "_selectedTerrainIndex", tile);
            window.WorldEntranceChoiceForTesting = () => new("stone-arch", 1f, false);
            Run(window.PlaceWorldEntranceForTestingAsync(), "place");
            var saved = WorldEntranceDocument.Read(root);
            Check(saved is { HomeworldKey: "artisans", Shape: "stone-arch", FaceTexture: -1 } && saved.Position == new Vector3f(4584, 3184, 384) && Math.Abs(saved.Scale - 1) < 1e-6,
                "Place Entrance puts a stone arch on the middle of the selected tile and saves it (world-entrance.json)");
            Check(Status(window).StartsWith("Placed a stone arch doorway to SKY ISLES on the selected tile in Artisans.", StringComparison.Ordinal), "the status says what was placed: " + Status(window));
            Check(Find<Control>(window, "WorldEntranceTools").IsVisible && Text(window, "WorldEntranceNote").Contains("The doorway to SKY ISLES: Stone Arch"),
                "the card shows the entrance and its tools: " + Text(window, "WorldEntranceNote"));
            Check(Viewport(window).WorldEntrancePreviewFaceCountForTesting == 56, "the entrance's 56-face frame is drawn over the level");
            Run(window.WorldEntranceRoomCheckForTesting, "room check");
            Pump(10);
            string room = Text(window, "WorldEntranceRoom");
            if (disc == null) Check(room.Contains("Choose the original Spyro the Dragon (USA) disc"), "without a disc the card asks for one to check the room: " + room);
            else Check(room.StartsWith("There is room: the doorway carries the whole of SKY ISLES's sky", StringComparison.Ordinal), "with the disc the card says the whole sky fits in Artisans: " + room);

            // Move, turn, shape, textures.
            Run(window.MoveWorldEntranceForTestingAsync(16, 0, 0), "move");
            Check(WorldEntranceDocument.Read(root)!.Position == new Vector3f(4600, 3184, 384) && Status(window).StartsWith("Moved the entrance east", StringComparison.Ordinal), "▶ moves it 16 units east");
            Run(window.MoveWorldEntranceForTestingAsync(0, 0, 8), "raise");
            Check(WorldEntranceDocument.Read(root)!.Position.Z == 392, "Up raises it 8 units");
            int heading = WorldEntranceDocument.Read(root)!.Heading;
            Run(window.TurnWorldEntranceForTestingAsync(6), "turn");
            Check(WorldEntranceDocument.Read(root)!.Heading == ((heading + 64) & 255) && Status(window).Contains("Turned the entrance"), "⟲ 90° turns it a quarter");
            window.WorldEntranceChoiceForTesting = () => new("hex-gate", 1.25f, false);
            Run(window.ReshapeWorldEntranceForTestingAsync(), "shape");
            Check(WorldEntranceDocument.Read(root) is { Shape: "hex-gate" } e && Math.Abs(e.Scale - 1.25f) < 1e-6 && Viewport(window).WorldEntrancePreviewFaceCountForTesting > 0 &&
                  Status(window).StartsWith("The entrance is a hex gate now (larger size)", StringComparison.Ordinal), "Shape… makes it a larger hex gate: " + Status(window));
            window.WorldEntranceTextureForTesting = () => 6;
            Run(window.RetextureWorldEntranceForTestingAsync(false), "front texture");
            window.WorldEntranceTextureForTesting = () => 3;
            Run(window.RetextureWorldEntranceForTestingAsync(true), "side texture");
            Check(WorldEntranceDocument.Read(root) is { FaceTexture: 6, SideTexture: 3 } && Status(window).Contains("Artisans texture 4"), "Front/Side Texture… take Artisans' own textures 7 and 4");
            Check(WorldEntranceDocument.Read(root)!.Spec() is { HomeworldKey: "artisans", Shape: "hex-gate", FaceTexture: 6, SideTexture: 3 }, "the saved entrance is what Create BIN builds");

            // Off the map is refused.
            var before = File.ReadAllText(WorldEntranceDocument.PathFor(root));
            var far = WorldEntranceDocument.Read(root)! with { Position = new(20, 3184, 384) };
            Task.Run(() => far.SaveAsync(root)).GetAwaiter().GetResult();
            Invoke(window, "RefreshWorldEntranceCard"); Pump(5);
            Run(window.MoveWorldEntranceForTestingAsync(-16, 0, 0), "move off the map");
            Check(Status(window).Contains("outside the game's map") && WorldEntranceDocument.Read(root)!.Position.X == 20, "moving the entrance past the map's edge is refused: " + Status(window));
            File.WriteAllText(WorldEntranceDocument.PathFor(root), before);

            // Another homeworld: Use This Homeworld; a level that is not a homeworld: no card.
            ShowOriginal("peacekeepers");
            Check(card.IsVisible && Find<Button>(window, "WorldEntranceUseHere").IsVisible && !Find<Button>(window, "WorldEntrancePlace").IsVisible &&
                  Text(window, "WorldEntranceNote").Contains("entered from Artisans"), "Peace Keepers says the world is entered from Artisans and offers Use This Homeworld");
            Run(window.UseThisHomeworldForWorldForTestingAsync(), "use here");
            Check(NewWorldDocument.Read(root)!.EntryHomeworldKey == "peacekeepers" && WorldEntranceDocument.Read(root) == null && Find<Button>(window, "WorldEntrancePlace").IsVisible,
                "Use This Homeworld makes Peace Keepers the entry (the doorway placed in Artisans goes)");
            ShowOriginal("stonehill");
            Check(!card.IsVisible, "Stone Hill (not a homeworld) shows no World Entrance card");

            // Remove.
            ShowOriginal("peacekeepers");
            window.WorldEntranceChoiceForTesting = () => new("square-gate", 1f, false);
            Run(window.PlaceWorldEntranceForTestingAsync(), "place in peace keepers");
            Check(WorldEntranceDocument.Read(root) is { HomeworldKey: "peacekeepers", Shape: "square-gate" }, "Place Entrance works in the new entry homeworld");
            window.WorldEntranceRemoveConfirmForTesting = () => true;
            Run(window.RemoveWorldEntranceForTestingAsync(), "remove");
            Check(WorldEntranceDocument.Read(root) == null && Viewport(window).WorldEntrancePreviewFaceCountForTesting == 0 && Status(window).StartsWith("Removed the doorway to SKY ISLES", StringComparison.Ordinal),
                "Remove Entrance deletes it and its preview");

            Check(File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected" &&
                !Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any(), "no other file is touched and no game is written");
            window.Close(); Pump(10);
            Console.WriteLine($"WORLD_ENTRANCE_UI_OK checks={_checks}");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
            for (int i = 0; i < IsolatedRoots.Length; i++) Environment.SetEnvironmentVariable(IsolatedRoots[i], previousRoots[i]);
            try { Directory.Delete(parent, recursive: true); } catch (IOException) { }
        }
    }

    private static EditorViewport Viewport(MainWindow window) => Get<EditorViewport>(window, "_viewport");
    private static void Run(Task task, string label) { Wait(() => task.IsCompleted, label, 300); if (task.IsFaulted) throw task.Exception!; }
    private static string Text(MainWindow window, string name) => Find<TextBlock>(window, name).Text ?? "";
    private static void SelectTab(MainWindow window, string header)
    {
        var tabs = Find<TabControl>(window, "EditorWorkspaceToolTabs");
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == header); Pump(20);
    }
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static void SetField(MainWindow w, string name, object? value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(w, value);
    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Wait(Func<bool> predicate, string label, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Check(predicate(), label);
        Dispatcher.UIThread.RunJobs();
    }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("World entrance UI: " + label); }
}
