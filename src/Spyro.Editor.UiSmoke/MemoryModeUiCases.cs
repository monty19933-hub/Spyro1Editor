using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

/// <summary>Game Settings › Memory ("Emulator only (8 MB RAM)").
/// <list type="bullet">
/// <item><c>--memory-mode-ui-only</c>: tiny throwaway projects; the card offers the two choices, each choice is saved at
/// once (8 MB as memory-mode.json, 2 MB by removing it), the editor's capacity checks follow it, a reopened editor shows
/// it, and a damaged file is reported (Create BIN then stops) rather than overwritten. No disc, no game.</item>
/// <item><c>--memory-mode-export-ui &lt;project&gt; &lt;output&gt; [level]</c>: on a disposable clone of a real project,
/// choose 8 MB, add enemies from other levels to a level with the ordinary Add object window until it holds more than
/// 2 MB allows, Create BIN (published, the status says the game needs 8 MB, the executable has the scene placement),
/// then choose 2 MB and Create BIN again (refused in the composer's own words). The 8 MB BIN is kept for the emulator.</item>
/// </list></summary>
internal static class MemoryModeUiCases
{
    private static int _checks;

    internal static void Run()
    {
        _checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-memory-mode-ui-").FullName;
        string root = Directory.CreateDirectory(Path.Combine(parent, "project")).FullName;
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
        {
            levels = new[] { new { key = "stonehill", displayName = "Stone Hill", levelId = 11 }, new { key = "toasty", displayName = "Toasty", levelId = 14 } }
        }));
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        string?[] previousRoots = [.. IsolatedRoots.Select(Environment.GetEnvironmentVariable)];
        string file = GameMemorySettings.PathFor(root);
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
            var window = Measure();
            var section = Find<Control>(window, "MemoryModeSection");
            Check(section.Parent != null && section.Parent == Find<Control>(window, "PlayAsCrashSection").Parent,
                "Memory is a card in Game Settings, beside Play as Crash");
            RadioButton ps = Find<RadioButton>(window, "MemoryModePlayStation"), em = Find<RadioButton>(window, "MemoryModeEmulator8MB");
            Check(ps.Content?.ToString() == "PlayStation (2 MB)" && em.Content?.ToString() == "Emulator only (8 MB RAM)", "the two choices are named plainly");
            string text = string.Join(" ", section.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Check(text.Contains("Emulator only (8 MB RAM): levels can hold more.") && text.Contains("DuckStation") && text.Contains("Enable 8MB RAM") &&
                text.Contains("levels that fit 2 MB still work"), "the card says what 8 MB needs and what still works without it");
            Check(ps.IsChecked == true && em.IsChecked == false && !File.Exists(file) && !GameRam.Extended, "PlayStation (2 MB) is the default and nothing is saved");
            Check(Summary(window) == "The games you create fit a PlayStation's 2 MB.", "the summary: " + Summary(window));
            em.IsChecked = true; Pump();
            Check(GameMemorySettings.Load(root) == GameMemoryMode.Emulator8MB && GameRam.Extended && ps.IsChecked == false,
                "choosing 8 MB saves it at once and the editor's capacity checks follow it");
            Check(Summary(window).StartsWith("On:", StringComparison.Ordinal) && Status(window).Contains("Emulator only (8 MB RAM)"), "summary and status say so: " + Status(window));
            var reopened = Measure();
            Check(Find<RadioButton>(reopened, "MemoryModeEmulator8MB").IsChecked == true && Find<RadioButton>(reopened, "MemoryModePlayStation").IsChecked == false,
                "a reopened editor shows the saved choice");
            Find<RadioButton>(reopened, "MemoryModePlayStation").IsChecked = true; Pump();
            Check(!File.Exists(file) && !GameRam.Extended, "choosing 2 MB again removes the file");
            File.WriteAllText(file, "{ not json");
            var damaged = Measure();
            Check(Summary(damaged).StartsWith("The saved choice could not be read", StringComparison.Ordinal) && File.ReadAllText(file) == "{ not json" &&
                Find<RadioButton>(damaged, "MemoryModePlayStation").IsChecked == false && Find<RadioButton>(damaged, "MemoryModeEmulator8MB").IsChecked == false,
                "a damaged file is reported, neither choice is shown, and the file is left as it was");
            var use = typeof(MainWindow).GetMethod("UseProjectMemoryMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool stops = false;
            try { ((IDisposable)use.Invoke(damaged, null)!).Dispose(); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidDataException d && d.Message.Contains("Memory setting")) { stops = true; }
            Check(stops, "Create BIN stops on the damaged choice instead of guessing");
            File.Delete(file);
            Check(File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected" &&
                !Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any(), "no other file is touched and no game is written");
            Console.WriteLine($"MEMORY_MODE_UI_OK checks={_checks}");
        }
        finally
        {
            GameRam.SetProjectMode(GameMemoryMode.PlayStation2MB);
            Environment.CurrentDirectory = previousDirectory;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
            for (int i = 0; i < IsolatedRoots.Length; i++) Environment.SetEnvironmentVariable(IsolatedRoots[i], previousRoots[i]);
            try { Directory.Delete(parent, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Lab: pictures of the Memory card (both choices) on a throwaway project; no disc, no game.</summary>
    internal static void Shots(string outDir, string? project)
    {
        Directory.CreateDirectory(outDir);
        string parent = Directory.CreateTempSubdirectory("spyro-memory-mode-shot-").FullName;
        string root = Path.Combine(parent, "project");
        if (project != null)
        {
            var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, root]) { RedirectStandardError = true });
            clone!.WaitForExit();
            if (clone.ExitCode != 0) throw new IOException("Could not clone the project folder.");
        }
        else
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
            {
                levels = new[] { new { key = "treetops", displayName = "Tree Tops", levelId = 43 } }
            }));
        }
        Environment.CurrentDirectory = root;
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
        try
        {
            var window = project != null ? Open() : new MainWindow { Width = 1440, Height = 900 };
            if (project == null) { window.Show(); Pump(50); }
            ShowMemoryCard(window);
            Snapshot(window, Path.Combine(outDir, "memory-card-playstation-2mb.png"));
            Find<RadioButton>(window, "MemoryModeEmulator8MB").IsChecked = true; Pump(20);
            Snapshot(window, Path.Combine(outDir, "memory-card-emulator-8mb.png"));
            window.Close(); Pump(10);
            Console.WriteLine("MEMORY_MODE_SHOTS_OK " + outDir);
        }
        finally
        {
            GameRam.SetProjectMode(GameMemoryMode.PlayStation2MB);
            try { Directory.Delete(parent, recursive: true); } catch (IOException) { }
        }
    }

    // The editor's own data, project and log folders go to the throwaway folder too.
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    /// <summary>The source-bound export case (see the class notes).</summary>
    internal static void RunExport(string project, string outDir, string levelKey)
    {
        _checks = 0;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode != 0 || !Directory.Exists(workspace)) throw new IOException("Could not clone the project folder.");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.CurrentDirectory = workspace;
        string output = Path.Combine(workspace, "output");
        MainWindow window = Open();
        try
        {
            LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(levelKey)!;
            Load(window, level);
            Check(!File.Exists(GameMemorySettings.PathFor(workspace)) && !GameRam.Extended, "the project starts with PlayStation (2 MB)");
            ShowMemoryCard(window);
            Snapshot(window, Path.Combine(outDir, "0-memory-card-default.png"));
            Find<RadioButton>(window, "MemoryModeEmulator8MB").IsChecked = true; Pump(10);
            Check(GameMemorySettings.Load(workspace) == GameMemoryMode.Emulator8MB && GameRam.Extended, "Emulator only (8 MB RAM) is chosen in Game Settings › Memory");
            Snapshot(window, Path.Combine(outDir, "1-memory-8mb-chosen.png"));
            Get<TabItem>(window, "_modernObjectWorkspaceTab").IsSelected = true; Pump(20);

            // Enemies from other levels, through the ordinary Add object window: Beast Makers' (0x1CD, 39 KB model) and
            // Lofty Castle's (0x3E, 22 KB), two of each. Tree Tops has 4.8 KB to spare with 2 MB.
            foreach (var (cls, source, dx) in new[] { (0x1CD, "beastmakers", 60f), (0x1CD, "beastmakers", -60f), (0x3E, "loftycastle", 0f), (0x3E, "loftycastle", 120f) })
                AddFromBrowser(window, cls, source, dx);
            Save(window);
            Snapshot(window, Path.Combine(outDir, "2-enemies-added.png"));

            string created = CreateBin(window);
            string[] bins = Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            Check(created.StartsWith("Created ", StringComparison.Ordinal) && bins.Length == 1, "8 MB: Create BIN publishes the game: " + created);
            Check(created.Contains($"this game needs 8 MB RAM. {level.DisplayName} does not fit 2 MB", StringComparison.Ordinal) && created.Contains("Enable 8MB RAM"),
                "the status says the game needs 8 MB and names the level");
            var disc = UniversalDiscImage.Read(bins[0]);
            byte[] exe = disc.Exe.ToArray();
            Check(NativeExtendedRam.IsInstalled(exe), "the exported executable has the 8 MB scene placement");
            var data = new UniversalLevelData(disc.EntryBytes(level.SourceWadEntry));
            uint copy = UniversalExeBindings.Read(exe, level.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
            Check(!GameRam.FitsPlayStation(data, copy) && GameRam.FitsExtended(data, copy) && UniversalMobyComposer.ArenaSlots(data) >= 91,
                $"{level.DisplayName} holds more than 2 MB allows ({UniversalMobyComposer.RuntimeEnd(data, copy) - GameRam.LowLimit:N0} bytes past it), fits 8 MB, keeps its spare slots");
            string copyPath = Path.Combine(outDir, "eight-mb.bin");
            File.Copy(bins[0], copyPath, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(copyPath, ".cue"), "FILE \"eight-mb.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            File.WriteAllText(Path.Combine(outDir, "status-8mb.txt"), created + "\n");
            string hash; using (var stream = File.OpenRead(copyPath)) hash = Convert.ToHexStringLower(SHA256.HashData(stream));

            // The same project with PlayStation (2 MB): the composer refuses the level in its own words.
            ShowMemoryCard(window);
            Find<RadioButton>(window, "MemoryModePlayStation").IsChecked = true; Pump(10);
            Check(!File.Exists(GameMemorySettings.PathFor(workspace)) && !GameRam.Extended, "PlayStation (2 MB) chosen again: " +
                $"file={File.Exists(GameMemorySettings.PathFor(workspace))} mode={GameRam.Mode} scope={GameRam.Current?.Mode} " +
                $"ps={Find<RadioButton>(window, "MemoryModePlayStation").IsChecked} em={Find<RadioButton>(window, "MemoryModeEmulator8MB").IsChecked} status={Status(window)}");
            Snapshot(window, Path.Combine(outDir, "3-memory-2mb-chosen.png"));
            DateTime published = File.GetLastWriteTimeUtc(bins[0]);
            string refused = CreateBin(window);
            File.WriteAllText(Path.Combine(outDir, "status-2mb.txt"), refused + "\n");
            Check(refused.StartsWith("Could not create", StringComparison.Ordinal) && refused.Contains($"{level.DisplayName} does not have enough game RAM for these objects"),
                "2 MB: the same project is refused: " + refused);
            Check(File.GetLastWriteTimeUtc(bins[0]) == published, "the refused export leaves the published game as it was");
            Console.WriteLine($"MEMORY_MODE_EXPORT_UI_OK checks={_checks} level={level.DisplayName} sha256={hash}");
        }
        finally { window.Close(); Pump(10); GameRam.SetProjectMode(GameMemoryMode.PlayStation2MB); }
    }

    /// <summary>A clone of a project with levels built in the editor (the authored export path): 8 MB chosen, Create BIN
    /// publishes the game with the scene placement and says whether it also works with 2 MB.</summary>
    internal static void RunAuthoredExport(string project, string outDir)
    {
        _checks = 0;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode != 0 || !Directory.Exists(workspace)) throw new IOException("Could not clone the project folder.");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.CurrentDirectory = workspace;
        MainWindow window = Open();
        try
        {
            Find<RadioButton>(window, "MemoryModeEmulator8MB").IsChecked = true; Pump(10);
            Check(GameMemorySettings.Load(workspace) == GameMemoryMode.Emulator8MB, "Emulator only (8 MB RAM) is chosen");
            string created = CreateBin(window);
            string image = Path.Combine(workspace, "output", "Spyro Editor - Authored Level.bin");
            File.WriteAllText(Path.Combine(outDir, "status-authored-8mb.txt"), created + "\n");
            Check(created.StartsWith("Created ", StringComparison.Ordinal) && File.Exists(image), "the authored export publishes the game: " + created);
            Check(created.Contains(" Memory: ", StringComparison.Ordinal), "the status says what the game needs: " + created);
            var disc = UniversalDiscImage.Read(image);
            Check(NativeExtendedRam.IsInstalled(disc.Exe.ToArray()), "the authored export's executable has the 8 MB scene placement");
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            using (GameRam.Use(GameMemoryMode.Emulator8MB))
            {
                // Against the project's own source disc, as the export compares (a level as shipped counts as fitting).
                using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace, "_local", "settings", "source-disc.json")));
                var original = UniversalDiscImage.Read(settings.RootElement.GetProperty("imagePath").GetString()!);
                var fits = NativeExtendedRam.Survey(disc.Exe.ToArray(), disc.EntryBytes, catalog.Levels, original);
                File.WriteAllLines(Path.Combine(outDir, "authored-levels.txt"), fits.Select(f =>
                    $"{f.LevelId} {f.Name}: 2 MB {(f.FitsPlayStation ? "fits" : "NO")}, 8 MB {(f.FitsExtended ? "fits" : "NO")}, end 0x{f.RuntimeEnd:X8}, below scene 0x{f.LowEnd:X8}, scene {f.SceneBytes:N0} bytes"));
                Check(fits.All(f => f.FitsExtended), "every level of the game fits the 8 MB layout");
            }
            string copyPath = Path.Combine(outDir, "authored-8mb.bin");
            File.Copy(image, copyPath, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(copyPath, ".cue"), "FILE \"authored-8mb.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            Console.WriteLine($"MEMORY_MODE_AUTHORED_EXPORT_UI_OK checks={_checks}");
        }
        finally { window.Close(); Pump(10); GameRam.SetProjectMode(GameMemoryMode.PlayStation2MB); }
    }

    /// <summary>Game Settings with the Memory card open and in view.</summary>
    private static void ShowMemoryCard(MainWindow window)
    {
        Get<TabItem>(window, "_modernLevelWorkspaceTab").IsSelected = true; Pump(20);
        var card = Find<Expander>(window, "MemoryModeSection");
        card.IsExpanded = true; Pump(20);
        card.BringIntoView(); Pump(20);
    }

    private static string CreateBin(MainWindow window)
    {
        var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
        var clock = Stopwatch.StartNew();
        while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(40))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope") is { } scope)
            {
                Click(Find<Button>(scope, "ExportCreate"));
                continue;
            }
            foreach (Window other in window.OwnedWindows.ToArray())
            {
                Console.WriteLine("memory mode export: dialog '" + other.Title + "': " +
                    string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                other.Close();
            }
        }
        Pump(100);
        Check(command.IsCompleted, "Create BIN finished");
        Console.WriteLine("memory mode export: " + Status(window));
        return Status(window);
    }

    private static void AddFromBrowser(MainWindow window, int classId, string sourceKey, float dx)
    {
        int before = Mobys(window).Count(m => m.IsAdded && !m.IsRemoved);
        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        Window library = WaitOwned(window, w => (w.Title ?? "").StartsWith("Add object", StringComparison.Ordinal));
        ListBox list = Find<ListBox>(library, "UniversalMobyList");
        bool Pick(UniversalMobyCatalogEntry e) => e.ClassId == classId && e.Variants.Any(v => v.SourceLevelKey == sourceKey);
        Wait(() => list.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Any(e => Pick(e) && e.RestrictionReason == null) == true, $"class 0x{classId:X} is in the catalogue", 600);
        var entry = list.ItemsSource!.Cast<UniversalMobyCatalogEntry>().First(Pick);
        list.SelectedItem = entry; Pump(10);
        TextBlock summary = Find<TextBlock>(library, "UniversalMobySummary");
        Wait(() => !(summary.Text ?? "").Contains("Checking", StringComparison.Ordinal), "fit check finished", 600);
        ComboBox variant = Find<ComboBox>(library, "UniversalMobyVariant");
        var wanted = entry.Variants.FirstOrDefault(v => v.SourceLevelKey == sourceKey && v.ClassId == classId);
        if (wanted != null && !Equals(variant.SelectedItem, wanted)) { variant.SelectedItem = wanted; Pump(10); }
        Wait(() => !(summary.Text ?? "").Contains("Checking", StringComparison.Ordinal), "fit check finished", 600);
        Button add = Find<Button>(library, "UniversalMobyAddHere");
        Wait(() => add.IsEnabled, $"class 0x{classId:X} fits with 8 MB: {summary.Text}", 600);
        Click(add);
        Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == before + 1, $"class 0x{classId:X} was added", 120);
        foreach (Window open in window.OwnedWindows.Where(w => (w.Title ?? "").StartsWith("Add object", StringComparison.Ordinal)).ToArray()) open.Close();
        Pump(20);
        var added = Mobys(window).Last(m => m.IsAdded && !m.IsRemoved);
        window.CaptureEditorShellSessionSnapshotForTesting().Viewport.SelectMoby(added, false); Pump(10);
        if (dx != 0) { Invoke(window, "NudgeSelectedMoby", dx, 40f, 0f); Pump(10); }
    }

    private static MainWindow Measure()
    {
        var window = new MainWindow { Width = 1200, Height = 760 };
        window.Measure(new Size(1200, 760)); window.Arrange(new Rect(0, 0, 1200, 760)); Pump();
        return window;
    }
    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        WaitIdle(window);
        return window;
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
    private static void Save(MainWindow window)
    {
        var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => save.IsCompleted, "save", 120);
        Check(save.Result, "save succeeded: " + Status(window));
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
    private static Window WaitOwned(MainWindow window, Func<Window, bool> match)
    {
        Wait(() => window.OwnedWindows.Any(match), "dialog opened", 60);
        return window.OwnedWindows.Last(match);
    }
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static string Summary(MainWindow window) => Find<TextBlock>(window, "MemoryModeSummary").Text ?? "";
    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static List<Spyro.Editor.Core.Scene.Moby> Mobys(MainWindow w) => Get<List<Spyro.Editor.Core.Scene.Moby>>(w, "_currentMobys");
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Memory mode UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
