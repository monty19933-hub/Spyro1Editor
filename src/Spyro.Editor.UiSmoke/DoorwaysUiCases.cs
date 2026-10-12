using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary><c>--doorways-ui-only</c>: Terrain › Doorways on throwaway projects, no disc and no game. A rebuilt Artisans gets
/// a doorway from the Add Doorway window's answers on the selected tile (saved with its frame), a frame face clicked in
/// the view selects its doorway, then move, turn, Shape…, Leads To…, Front Texture… and Remove work through the card;
/// the Add Doorway window offers the six shapes as pictures; a level in Stone Hill's place says why it has none; the new
/// world's homeworld adds a doorway to its own level, marked playable after New World export, and Create BIN says
/// plainly that it can't build the new world yet.</summary>
internal static class DoorwaysUiCases
{
    private static int _checks;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    internal static void Run()
    {
        _checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-doorways-ui-").FullName;
        string root = Directory.CreateDirectory(Path.Combine(parent, "project")).FullName;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(repo) ?? throw new InvalidOperationException("No level catalog.");
        File.Copy(Path.Combine(repo, "spyro-level-catalog.json"), Path.Combine(root, "spyro-level-catalog.json"));
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        string?[] previousRoots = [.. IsolatedRoots.Select(Environment.GetEnvironmentVariable)];
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

            // A rebuilt Artisans: a floor of four tiles and no doorways yet.
            var binding = new SceneTargetBinding("artisans", 10, 10, 9, UniversalSourceLibrary.OriginalUsaSha256, 174);
            AuthoredSurface Tile(float x, float y) => new(Guid.NewGuid().ToString("N"), [new(x, y, 1000), new(x + 256, y, 1000), new(x + 256, y + 256, 1000), new(x, y + 256, 1000)], new(128, 128, 128), stone);
            var hub = SceneDocument.New("TEST HUB") with { TargetBinding = binding, Surfaces = [Tile(2744, 2744), Tile(3000, 2744), Tile(2744, 3000), Tile(3000, 3000)], Spawn = new(3000, 2900, 1032) };
            Invoke(window, "ApplyFreshScene", hub); Pump(20);
            SelectTab(window, "Terrain");
            var card = Find<Control>(window, "TerrainDoorwaysCard");
            Check(card.IsVisible && Find<Button>(window, "DoorwayAdd").IsVisible && Find<Button>(window, "DoorwayAdd").IsEnabled, "a rebuilt homeworld shows the Doorways card with Add Doorway…");
            Check(Text(window, "DoorwaysNote").Contains("no doorways yet"), "the card says the homeworld has none yet: " + Text(window, "DoorwaysNote"));
            Check(!Find<Control>(window, "DoorwayTools").IsVisible, "no doorway tools until one is selected");

            // Add Doorway… on the selected tile (the window's answers come from the test hook).
            var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            int tile = geometry.Polygons.FindIndex(p => p.Points.Any(q => q.X == 3256 && q.Y == 3256));
            SetField(window, "_selectedTerrain", geometry.Polygons[tile]); SetField(window, "_selectedTerrainIndex", tile);
            window.DoorwayChoiceForTesting = () => new(12, "stone-arch", 1f, null, false);
            Run(window.AddDoorwayForTestingAsync(), "add");
            var scene = window.FreshSceneForTesting!;
            var door = SceneDoorways.Find(scene, 12);
            Check(door is { Shape: "stone-arch" } && door.Position == new Vector3f(3128, 3128, 1000) && door.FrameFaces!.Count == 56 && scene.Surfaces.Count == 60,
                "Add Doorway puts a stone arch on the middle of the selected tile with its 56-face frame");
            Check(Status(window).StartsWith("Added a stone arch doorway to Dark Hollow on the selected tile.", StringComparison.Ordinal), "the status says what was added: " + Status(window));
            string saved = SceneDocumentStore.PathForTarget(root, "artisans");
            Check(File.Exists(saved) && SceneDoorways.Find(SceneDocument.Read(saved), 12)?.FrameFaces?.Count == 56, "the level is saved with the doorway and its frame");
            Check(window.SelectedDoorwayForTesting == 12 && Find<Control>(window, "DoorwayTools").IsVisible && Text(window, "DoorwaySelected").Contains("Dark Hollow — Stone Arch"),
                "the new doorway is selected: " + Text(window, "DoorwaySelected"));
            Check(Find<ListBox>(window, "DoorwayList").ItemsSource!.Cast<object>().Single().ToString()!.StartsWith("Dark Hollow — Stone Arch"), "the list shows it");
            Check(window.CaptureEditorShellSessionSnapshotForTesting().Viewport.MultiSelectedTerrainIndexes.Count == 56, "selecting a doorway selects its whole frame (so the Ground card paints it all)");

            // Clicking a frame face in the view selects its doorway.
            Invoke(window, "SelectDoorway", 12, false); SetField(window, "_selectedDoorway", null);
            geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            scene = window.FreshSceneForTesting!;
            int frameFace = geometry.Polygons.FindIndex(p => p.SectorIndex >= 0 && scene.Surfaces[p.SectorIndex].Id == door!.FrameFaces![10]);
            SetField(window, "_selectedTerrain", geometry.Polygons[frameFace]); SetField(window, "_selectedTerrainIndex", frameFace);
            Invoke(window, "RefreshDoorwaysCard");
            Check(window.SelectedDoorwayForTesting == 12, "a clicked frame face selects its doorway");

            // Move, turn, shape, destination, texture, remove.
            Run(window.MoveSelectedDoorwayForTestingAsync(16, 0, 0), "move");
            var moved = SceneDoorways.Find(window.FreshSceneForTesting!, 12)!;
            Check(moved.Position == new Vector3f(3144, 3128, 1000) && moved.FrameFaces!.SequenceEqual(door!.FrameFaces!) && Status(window).Contains("moved the doorway to Dark Hollow east", StringComparison.OrdinalIgnoreCase),
                "◀▲▼▶ moves the doorway and its frame 16 units: " + Status(window));
            Run(window.TurnSelectedDoorwayForTestingAsync(6), "turn");
            Check(SceneDoorways.Find(window.FreshSceneForTesting!, 12)!.Heading == SceneDoorways.Turn(moved.Heading, 6) && Status(window).Contains("Turned"), "⟲ 90° turns it: " + Status(window));
            window.DoorwayChoiceForTesting = () => new(12, "hex-gate", 1.25f, null, false);
            Run(window.ReshapeSelectedDoorwayForTestingAsync(), "reshape");
            var hex = SceneDoorways.Find(window.FreshSceneForTesting!, 12)!;
            Check(hex.Shape == "hex-gate" && hex.Scale == 1.25f && hex.FrameFaces!.All(id => door.FrameFaces.All(o => o != id)) && window.FreshSceneForTesting!.Surfaces.Count == 4 + hex.FrameFaces.Count &&
                hex.FaceMaterial == stone, "Shape… rebuilds the frame as a larger hex gate with the same texture");
            window.DoorwayChoiceForTesting = () => new(14, "hex-gate", 1.25f, null, false);
            Run(window.RetargetSelectedDoorwayForTestingAsync(), "retarget");
            Check(SceneDoorways.Find(window.FreshSceneForTesting!, 14)?.FrameFaces?.SequenceEqual(hex.FrameFaces) == true && Status(window).Contains("now leads to Toasty"),
                "Leads To… sends it to another level and keeps its frame: " + Status(window));
            Check(Find<ListBox>(window, "DoorwayList").ItemsSource!.Cast<object>().Single().ToString()!.StartsWith("Toasty — Hex Gate (larger)"), "the list follows");
            var sand = new AuthoredMaterial(catalog.FindByKey("drycanyon")!.SourceWadEntry, 20);
            var parts = DoorwayShapes.Frame(DoorwayShapes.HexGate, 1.25, SceneDoorways.Find(window.FreshSceneForTesting!, 14)!.Position, SceneDoorways.Find(window.FreshSceneForTesting!, 14)!.Heading);
            // Front Texture… needs the disc for its grid; the frame repaint underneath is what the card applies.
            var repainted = SceneDoorways.Update(window.FreshSceneForTesting!, 14, SceneDoorways.Find(window.FreshSceneForTesting!, 14)! with { FaceMaterial = sand }, repaint: DoorwayFramePart.Face);
            var frameIds = SceneDoorways.Find(repainted, 14)!.FrameFaces!;
            Check(frameIds.Select((id, i) => repainted.Surfaces.Single(s => s.Id == id).Material == (parts[i].Part == DoorwayFramePart.Face ? sand : stone)).All(x => x), "Front Texture repaints the front and back only");
            window.DoorwayRemoveConfirmForTesting = () => true;
            Run(window.RemoveSelectedDoorwayForTestingAsync(), "remove");
            Check(window.FreshSceneForTesting!.Portals == null && window.FreshSceneForTesting!.Surfaces.Count == 4 && Status(window) == "Removed the doorway to Toasty and its frame.",
                "Remove Doorway takes the doorway and its frame: " + Status(window));
            Check(SceneDocument.Read(saved).Portals == null && SceneDocument.Read(saved).Surfaces.Count == 4, "and the saved level follows");

            // The Add Doorway window: where it leads, six shapes drawn as pictures, size, frame texture, where it goes.
            var show = (Task)Invoke(window, "ShowDoorwayDialogAsync", "Add Doorway",
                SceneDoorways.Destinations(window.FreshSceneForTesting!, catalog).ToArray(), null, false)!;
            Wait(() => window.OwnedWindows.Any(w => w.Name == "DoorwayDialog"), "Add Doorway window", 30);
            var dialog = window.OwnedWindows.First(w => w.Name == "DoorwayDialog"); Pump(10);
            var gallery = Find<WrapPanel>(dialog, "DoorwayShapeGallery");
            Check(gallery.Children.OfType<Button>().Select(b => b.Name).SequenceEqual(DoorwayShapes.All.Select(s => "DoorwayShape-" + s.Id)) &&
                gallery.Children.OfType<Button>().All(b => b.GetLogicalDescendants().OfType<Avalonia.Controls.Shapes.Polygon>().Count() > 5),
                "the window offers the six shapes as pictures");
            Check(Find<ComboBox>(dialog, "DoorwayDestinationBox").ItemsSource!.Cast<object>().Count() == 5 && Find<RadioButton>(dialog, "DoorwayAtCamera").IsChecked == true &&
                Find<RadioButton>(dialog, "DoorwayAtTile").IsEnabled == false, "it lists Artisans' five levels and places in front of the camera when no tile is selected");
            Click(Find<Button>(dialog, "DoorwayShape-round-ring"));
            Find<ComboBox>(dialog, "DoorwaySize").SelectedIndex = 3;
            Click(Find<Button>(dialog, "DoorwayConfirm"));
            Wait(() => show.IsCompleted, "window answered", 30);
            var answer = (MainWindow.DoorwayChoice?)show.GetType().GetProperty("Result")!.GetValue(show);
            Check(answer is { Destination: 11, ShapeId: "round-ring", Scale: 1.5f, AtCamera: true, Texture: null }, "the window's answer is what was chosen");

            // A level in Stone Hill's place has no doorway code: the card says so instead of hiding.
            var other = SceneDocument.New("NOT A HUB") with { TargetBinding = new SceneTargetBinding("stonehill", 11, 12, 11, UniversalSourceLibrary.OriginalUsaSha256, 0), Surfaces = hub.Surfaces };
            Invoke(window, "ApplyFreshScene", other); Pump(20);
            Check(card.IsVisible && Text(window, "DoorwaysNote").Contains("only work in a homeworld") && !Find<Button>(window, "DoorwayAdd").IsVisible && !Find<ListBox>(window, "DoorwayList").IsVisible,
                "a level in another level's place explains why it has no doorways: " + Text(window, "DoorwaysNote"));

            // The new world: its homeworld's doorways lead to its own levels and are playable after New World export.
            File.WriteAllText(NewWorldDocument.PathFor(root), "{\"schemaVersion\":1,\"name\":\"SKY ISLES\",\"entryHomeworldKey\":\"artisans\",\"levelIds\":[70,71]}");
            var level71 = SceneDocument.New("CLOUD STEPS") with { WorldLevelId = 71 };
            Run(level71.SaveAsync(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(71))), "save world level");
            var worldHome = SceneDocument.New("SKY ISLES") with { WorldLevelId = 70, Surfaces = hub.Surfaces };
            Run(worldHome.SaveAsync(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(70))), "save world homeworld");
            Invoke(window, "ApplyFreshScene", worldHome); Pump(20);
            Check(Find<Button>(window, "DoorwayAdd").IsVisible && Text(window, "DoorwaysNote").Contains("playable after New World export"), "the new world's homeworld offers doorways: " + Text(window, "DoorwaysNote"));
            geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            SetField(window, "_selectedTerrain", geometry.Polygons[0]); SetField(window, "_selectedTerrainIndex", 0);
            window.DoorwayChoiceForTesting = () => new(71, "pointed-arch", 1f, null, false);
            Run(window.AddDoorwayForTestingAsync(), "add world doorway");
            Check(SceneDoorways.Find(window.FreshSceneForTesting!, 71)?.Shape == "pointed-arch" && Status(window).Contains("CLOUD STEPS") && Status(window).Contains("playable after New World export"),
                "a doorway to the world's own level is added and marked playable after New World export: " + Status(window));
            Run((Task)Invoke(window, "CreateFreshMainBinAsync")!, "create bin");
            Check(Status(window).StartsWith("Create BIN can't build your new world yet", StringComparison.Ordinal) && Status(window).Contains("1 doorway is saved"),
                "Create BIN says plainly that the new world can't go into a game yet: " + Status(window));

            Check(File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected" &&
                !Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any(), "no other file is touched and no game is written");
            window.Close(); Pump(10);
            Console.WriteLine($"DOORWAYS_UI_OK checks={_checks}");
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

    private static void Run(Task task, string label) { Wait(() => task.IsCompleted, label, 120); if (task.IsFaulted) throw task.Exception!; }
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
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(5); }
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Doorways UI: " + label); }
}
