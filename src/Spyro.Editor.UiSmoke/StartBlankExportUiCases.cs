using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>
/// Start Blank through the normal controls on a disposable clone of a real project: empty a level, add a
/// dragonfly egg, a sign with a message and an object from another level with the ordinary Add object
/// window, save, reopen the editor, and Create BIN. The BIN is kept beside the screenshots for play testing.
/// Usage: UiSmoke --start-blank-export-ui &lt;project folder&gt; &lt;output folder&gt; [level key, default stonehill]
/// </summary>
internal static class StartBlankExportUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, string levelKey)
    {
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode != 0 || !Directory.Exists(workspace)) throw new IOException("Could not clone the project folder.");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "logs"));
        Environment.CurrentDirectory = workspace;

        MainWindow window = Open();
        string blankName;
        try
        {
            LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(levelKey)!;
            Load(window, level);
            int objectsBefore = Mobys(window).Count(m => !m.IsRemoved && !m.IsEditorControl);
            int tilesBefore = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!.Polygons.Count;
            Check(objectsBefore > 20 && tilesBefore > 500, $"{level.DisplayName} is loaded with its original content");

            // 1. Start Blank asks first; Cancel changes nothing.
            var first = (Task)Invoke(window, "StartBlankCurrentLevelAsync")!;
            Window ask = WaitDialog(window, "StartBlankDialog");
            string question = string.Join(" ", ask.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Check(question.Contains(level.DisplayName, StringComparison.Ordinal) && question.Contains("recovery", StringComparison.OrdinalIgnoreCase),
                "the question names the level and the recovery copy: " + question);
            Snapshot(ask, Path.Combine(outDir, "1-start-blank-question.png"));
            Click(Find<Button>(ask, "StartBlankCancel"));
            Wait(() => first.IsCompleted, "Cancel ends Start Blank", 30);
            Check(window.FreshSceneForTesting == null && Mobys(window).Count(m => !m.IsRemoved && !m.IsEditorControl) == objectsBefore,
                "Cancel leaves the level as it was");

            // 2. Confirm: a small floor and nothing else.
            var second = (Task)Invoke(window, "StartBlankCurrentLevelAsync")!;
            ask = WaitDialog(window, "StartBlankDialog");
            Click(Find<Button>(ask, "StartBlankConfirm"));
            Wait(() => second.IsCompleted, "Start Blank finishes", 300);
            var scene = window.FreshSceneForTesting;
            Check(scene != null && scene.Surfaces.Count is > 0 and < 10 && Mobys(window).Count(m => !m.IsRemoved) == 0,
                "the level is now a small floor with no objects: " + Status(window));
            blankName = scene!.Name;
            Snapshot(window, Path.Combine(outDir, "2-blank-level.png"));

            // 3. The same Add object window fills it: an egg, a sign and an object from another level.
            AddFromBrowser(window, e => e.ClassId == 0x1E9, "dragonfly egg", UniversalDragonflyEggs.Variants.Single(v => v.Id == "dragonfly-egg-gold").Class);
            AddFromBrowser(window, e => e.ClassId == UniversalDragonflyEggs.SignClass, "sign", null);
            AddFromBrowser(window, e => e.ClassId == 0x0A, "sheep from another level", null);
            // Spread them over the starting floor the way a user would, with the ordinary nudge.
            foreach (var (template, dx, dy) in new[] { ("special:dragonfly-egg-gold", 44f, 40f), ("special:sign", -30f, 52f) })
            {
                Moby target = Mobys(window).Single(m => m.CrossLevelTemplateId == template && !m.IsRemoved);
                window.CaptureEditorShellSessionSnapshotForTesting().Viewport.SelectMoby(target, false); Pump(10);
                Invoke(window, "NudgeSelectedMoby", dx, dy, 0f); Pump(10);
            }
            Moby sheep = Mobys(window).Single(m => m.IsAdded && !m.IsRemoved && (m.CrossLevelTemplateId ?? "").StartsWith("special:", StringComparison.Ordinal) == false);
            window.CaptureEditorShellSessionSnapshotForTesting().Viewport.SelectMoby(sheep, false); Pump(10);
            Invoke(window, "NudgeSelectedMoby", 50f, -36f, 0f); Pump(10);
            Check(Mobys(window).Where(m => !m.IsRemoved).Select(m => (m.Position.X, m.Position.Y)).Distinct().Count() == 3, "the three objects stand apart");
            Moby sign = Mobys(window).Single(m => m.CrossLevelTemplateId == "special:sign" && !m.IsRemoved);
            Task signEdit = (Task)Invoke(window, "EditMobyAsync", sign)!;
            Window signDialog = WaitOwned(window, w => (w.Title ?? "").StartsWith("Edit ", StringComparison.Ordinal));
            Find<TextBox>(signDialog, "UniversalSignTextBox").Text = "Blank level\nbuilt in the editor"; Pump(10);
            Snapshot(signDialog, Path.Combine(outDir, "3-sign-message.png"));
            Click(signDialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Apply changes"));
            Wait(() => signEdit.IsCompleted, "sign message accepted", 60);
            Check(sign.SignText == "BLANK LEVEL\nBUILT IN THE EDITOR", "the sign keeps the typed message in the game's letters: " + sign.SignText);
            Save(window);
            Snapshot(window, Path.Combine(outDir, "4-blank-level-with-objects.png"));
        }
        finally { window.Close(); Pump(10); }

        // 4. A reopened editor shows the blank level with the three objects and the message.
        window = Open();
        try
        {
            LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(levelKey)!;
            Load(window, level);
            var reopened = Mobys(window).Where(m => !m.IsRemoved).ToList();
            Check(window.FreshSceneForTesting?.Name == blankName && reopened.Count == 3, "the reopened level is the blank one with three objects: " + reopened.Count);
            Check(reopened.Single(m => m.CrossLevelTemplateId == "special:sign").SignText == "BLANK LEVEL\nBUILT IN THE EDITOR", "the sign message survived reopening");
            Check(reopened.Any(m => m.CrossLevelTemplateId == "special:dragonfly-egg-gold"), "the egg kept its colour");
            Check(Find<MenuItem>(window, null, "Restore Before Clear…") != null, "Restore Before Clear is offered");

            // 5. Create BIN; New Game starts in the first level so the blank one is entered through its portal.
            string output = Path.Combine(workspace, "output");
            var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
            bool confirmed = false; var clock = Stopwatch.StartNew();
            while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(40))
            {
                Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
                Window? scope = window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope");
                if (scope != null && !confirmed)
                {
                    ComboBox start = Find<ComboBox>(scope, "ExportStartLevel");
                    Check(start.ItemsSource!.Cast<object>().Any(i => i.ToString() == blankName), "the blank level is offered as a starting level");
                    start.SelectedIndex = 0; Pump(5);
                    Snapshot(scope, Path.Combine(outDir, "5-export-question.png"));
                    Click(Find<Button>(scope, "ExportCreate")); confirmed = true;
                }
                foreach (Window other in window.OwnedWindows.Where(w => w.Name != "ProjectExportScope").ToArray())
                {
                    Console.WriteLine("start blank export: dialog '" + other.Title + "': " +
                        string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                    other.Close();
                }
            }
            Pump(100);
            Console.WriteLine("start blank export: " + Status(window));
            string[] bins = Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            Check(command.IsCompleted && bins.Length == 1 && Status(window).StartsWith("Created ", StringComparison.Ordinal),
                "Create BIN publishes the game: " + Status(window));
            var disc = UniversalDiscImage.Read(bins[0]);
            var data = new UniversalLevelData(disc.EntryBytes(level.SourceWadEntry));
            Check(data.Scene().RowCount == 3, "the exported level holds exactly the three placed objects: " + data.Scene().RowCount);
            string copy = Path.Combine(outDir, "blank.bin");
            File.Copy(bins[0], copy, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(copy, ".cue"), "FILE \"blank.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            using var stream = File.OpenRead(copy);
            Console.WriteLine($"START_BLANK_EXPORT_UI_OK checks={_checks} level={level.DisplayName} sha256={Convert.ToHexStringLower(SHA256.HashData(stream))}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static void AddFromBrowser(MainWindow window, Func<UniversalMobyCatalogEntry, bool> pick, string what, int? variantClass)
    {
        int before = Mobys(window).Count(m => m.IsAdded && !m.IsRemoved);
        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        Window library = WaitOwned(window, w => (w.Title ?? "").StartsWith("Add object", StringComparison.Ordinal));
        ListBox list = Find<ListBox>(library, "UniversalMobyList");
        Wait(() => list.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Any(e => pick(e) && e.RestrictionReason == null) == true, what + " is in the catalogue", 300);
        var entry = list.ItemsSource!.Cast<UniversalMobyCatalogEntry>().Single(pick);
        list.SelectedItem = entry; Pump(10);
        if (variantClass is int wanted)
        {
            TextBlock summary = Find<TextBlock>(library, "UniversalMobySummary");
            Wait(() => !(summary.Text ?? "").Contains("Checking", StringComparison.Ordinal), "fit check finished", 600);
            ComboBox variant = Find<ComboBox>(library, "UniversalMobyVariant");
            variant.SelectedItem = entry.Variants.Single(v => v.ClassId == wanted); Pump(10);
        }
        Button add = Find<Button>(library, "UniversalMobyAddHere");
        Wait(() => add.IsEnabled, what + " fits the blank level", 600);
        Click(add);
        Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == before + 1, what + " was added", 120);
        foreach (Window open in window.OwnedWindows.Where(w => (w.Title ?? "").StartsWith("Add object", StringComparison.Ordinal)).ToArray()) open.Close();
        Pump(20);
    }

    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static List<Moby> Mobys(MainWindow w) => Get<List<Moby>>(w, "_currentMobys");
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(5); }

    private static void Save(MainWindow window)
    {
        var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => save.IsCompleted, "save", 120);
        Check(save.Result, "save succeeded: " + Status(window));
    }

    private static void Load(MainWindow window, LevelDefinition level)
    {
        if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != level.Key)
        {
            var load = (Task)Invoke(window, "SelectLevelAsync", level, null)!;
            Wait(() => load.IsCompleted, "load " + level.DisplayName, 300);
        }
        WaitIdle(window);
    }

    private static Window WaitDialog(MainWindow window, string name) => WaitOwned(window, w => w.Name == name);
    private static Window WaitOwned(MainWindow window, Func<Window, bool> match)
    {
        Wait(() => window.OwnedWindows.Any(match), "dialog opened", 60);
        return window.OwnedWindows.Last(match);
    }

    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        WaitIdle(window);
        return window;
    }

    private static void WaitIdle(MainWindow window)
    {
        var quiet = Stopwatch.StartNew();
        var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }

    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static MenuItem? Find<T>(MainWindow window, string? unused, string header) where T : MenuItem =>
        ((MenuFlyout)Find<Button>(window, "EditorFileMenu").Flyout!).Items.OfType<MenuItem>().FirstOrDefault(i => i.Header?.ToString() == header);
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Wait(Func<bool> predicate, string label, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Check(predicate(), label);
        Dispatcher.UIThread.RunJobs();
    }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Start Blank export UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
