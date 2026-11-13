using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Universal;

/// <summary><c>--fix-it-ui &lt;output&gt; &lt;usa disc&gt;</c>: Fix It For Me on every Create BIN stop, through the editor, in a new
/// project (Artisans renamed; Stone Hill renamed and given other music; Hover on). Create BIN is made to stop in
/// different ways (a test hook that refuses for as long as a chosen part of the project is in the game) and one press of
/// Fix It For Me must get the game made each time: a stop that names its level, one that needs only one kind of a
/// level's changes out, one that names nothing, two causes behind the same words, a stop that passes by itself; a
/// failure of the computer's offers nothing, and a stop nothing in the project causes leaves nothing out. What was left
/// out is said, remembered in the project, kept out of the next game too, shown in More › Left Out of the Game… and
/// put back from there; and the games really are without what was left out and with everything else.</summary>
internal static class FixItUiCases
{
    private static int _checks, _runs;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];
    private const string ArtisansName = "ARTISAN HOME", StoneHillName = "ROCKY HILL";

    internal static void Run(string outDir, string disc)
    {
        _checks = 0; _runs = 0;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        if (!File.Exists(disc)) throw new ArgumentException("The test needs the original disc image.");
        string settings = Directory.CreateDirectory(Path.Combine(workspace, "_local", "settings")).FullName;
        File.WriteAllText(Path.Combine(settings, "source-disc.json"), JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), selectedPath = disc, imagePath = disc, cuePath = Path.ChangeExtension(disc, ".cue") }));
        File.WriteAllText(Path.Combine(settings, "last-level.json"), "{\"levelKey\":\"artisans\"}");
        string support = Directory.CreateDirectory(Path.Combine(workspace, "support")).FullName;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog found.");
        foreach (string name in new[] { "spyro-level-catalog.json", "spyro-object-templates.json" })
            if (File.Exists(Path.Combine(repo, name))) File.Copy(Path.Combine(repo, name), Path.Combine(support, name));
        foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(outDir, "isolated", name)).FullName);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.CurrentDirectory = workspace;
        string found = Spyro.Editor.Core.Workspace.EditorWorkspace.Find().RootPath;
        if (Path.GetFullPath(found).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
            throw new InvalidOperationException("The editor would open " + found + " instead of the test project; stopping before it opens.");
        MainWindow window = Open();
        try
        {
            string opened = Get<Spyro.Editor.Core.Workspace.EditorWorkspace>(window, "_workspace").RootPath;
            if (Path.GetFullPath(opened).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
                throw new InvalidOperationException("The editor opened " + opened + " instead of the test project; stopping before any change.");
            window.OpenPathOverrideForTesting = _ => { };
            // (The window that opens by itself when something was left out is tried in the export step of --new-level;
            // here More › Left Out of the Game… is opened by hand.)
            window.ShowLeftOutAfterFixIt = false;
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            var texts = TextTargetCatalog.CreateDefault();
            LevelDefinition artisans = catalog.FindByKey("artisans")!, stoneHill = catalog.FindByKey("stonehill")!;
            // The project: two of the game's levels changed, and a game setting.
            Run(LevelTextEditStore.SaveAsync(workspace, texts.FindForLevel(artisans)!, ArtisansName), "save Artisans' name");
            Run(LevelTextEditStore.SaveAsync(workspace, texts.FindForLevel(stoneHill)!, StoneHillName), "save Stone Hill's name");
            int otherTrack = MusicTrackCatalog.SelectableTracks.First(t => t.TrackId != MusicTrackCatalog.GetNativeTrackId(stoneHill)).TrackId;
            Run(LevelMusicEditStore.SaveAsync(workspace, stoneHill, otherTrack), "save Stone Hill's music");
            SpyroMovesSettings.SaveHover(workspace, true);
            var fixIt = Find<Button>(window, "FixItForMe");
            string output = Path.Combine(workspace, "output");
            string Bin() => Directory.GetFiles(output, "*.bin").Single();
            bool Game(bool artisansRenamed, bool stoneHillRenamed, string what)
            {
                byte[] exe = UniversalDiscImage.Read(Bin()).Exe;
                bool a = Contains(exe, ArtisansName), s = Contains(exe, StoneHillName);
                Console.WriteLine($"     game: Artisans renamed {a}, Stone Hill renamed {s} ({what})");
                return a == artisansRenamed && s == stoneHillRenamed;
            }
            // A part is in the game while neither it nor the whole it belongs to is left out.
            static bool In(IReadOnlyList<string> leftOut, string part) => !leftOut.Any(l => l == part || part.StartsWith(l + ":", StringComparison.Ordinal));
            void StopWhile(Func<IReadOnlyList<string>, Exception?> rule) => window.CreateBinFaultForTesting = () => { _runs++; if (rule(window.LeftOutForTesting) is { } refusal) throw refusal; };
            string FixIt() { Run(window.ApplyPendingRepairForTestingAsync(), "Fix It For Me"); Pump(50); return Status(window); }
            void PutEverythingBack() { foreach (string part in window.LeftOutForTesting.ToArray()) window.PutBackForTesting(part); }

            // ---- nothing wrong: no button, nothing left out ----
            StopWhile(_ => null);
            string made = CreateBin(window);
            Check(made.StartsWith("Created ", StringComparison.Ordinal) && !fixIt.IsVisible && !window.FixItOnOfferForTesting && !made.Contains("LEFT OUT", StringComparison.Ordinal) &&
                  Game(true, true, "everything in"), "a Create BIN that goes through offers nothing and leaves nothing out: " + Short(made));

            // ---- a stop that names its level: that level's changes are left out, and no more of the project ----
            StopWhile(l => In(l, ExportParts.Level("stonehill")) ? new InvalidDataException("Stone Hill: something in it cannot go into the game.") : null);
            string stopped = CreateBin(window);
            Check(stopped.StartsWith("Could not create BIN: Stone Hill: something in it", StringComparison.Ordinal) && stopped.Contains("Press Fix It For Me", StringComparison.Ordinal) &&
                  fixIt.IsVisible && window.FixItOnOfferForTesting && window.PendingRepairForTesting == null, "any stop in the project puts Fix It For Me on offer, with the words that point to it");
            int before = _runs;
            string fixedNamed = FixIt();
            Console.WriteLine("     " + fixedNamed);
            Check(fixedNamed.StartsWith("Created ", StringComparison.Ordinal) && fixedNamed.Contains("Fix It For Me got it made", StringComparison.Ordinal) &&
                  fixedNamed.Contains("Left out your changes to Stone Hill", StringComparison.Ordinal) && fixedNamed.Contains("still in your project", StringComparison.Ordinal),
                "one press gets the game made and says what it left out and why");
            Check(window.LeftOutForTesting.SequenceEqual([ExportParts.Level("stonehill")]) && !fixIt.IsVisible && !window.FixItOnOfferForTesting,
                "the level the stop names is what is left out, and the button goes: " + string.Join(", ", window.LeftOutForTesting));
            Check(_runs - before <= 5, $"a named stop takes few runs ({_runs - before})");
            Check(Game(true, false, "Stone Hill left out"), "the game has Artisans' new name and not Stone Hill's");
            Check(File.Exists(ExportLeftOutSettings.PathFor(workspace)) && ExportLeftOutSettings.Load(workspace, "another version").Count == 0 &&
                  LevelTextEditStore.Load(workspace, texts.FindForLevel(stoneHill)!) != null && LevelMusicEditStore.Load(workspace, stoneHill) != null,
                "what was left out is remembered in the project, nothing of the level's is deleted, and another version of the editor starts afresh");
            // The next Create BIN leaves it out too, says so, and does not stop.
            string again = CreateBin(window);
            Check(again.StartsWith("Created ", StringComparison.Ordinal) && again.Contains("LEFT OUT of this game by Fix It For Me", StringComparison.Ordinal) &&
                  again.Contains("your changes to Stone Hill", StringComparison.Ordinal) && !fixIt.IsVisible, "the next Create BIN leaves it out too and says so: " + Short(again));

            // ---- More › Left Out of the Game…: what, why, Put Back ----
            var leftOutWindow = (Task)Invoke(window, "ShowLeftOutAsync")!;
            Pump(40);
            Window list = window.OwnedWindows.First(w => w.Name == "LeftOutWindow");
            string listed = string.Join(" | ", list.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Check(listed.Contains("Your changes to Stone Hill", StringComparison.Ordinal) && listed.Contains("something in it cannot go into the game", StringComparison.Ordinal),
                "the window lists the part and what Create BIN said");
            Snapshot(list, Path.Combine(outDir, "left-out-window.png"));
            Click(list.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "LeftOutPutBack")); Pump(20);
            Check(window.LeftOutForTesting.Count == 0 && !File.Exists(ExportLeftOutSettings.PathFor(workspace)) &&
                  string.Join(" | ", list.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)).Contains("Nothing is left out", StringComparison.Ordinal),
                "Put Back takes it off the list (and the file goes with the last one)");
            Click(list.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "LeftOutClose")); Pump(20);
            Check(leftOutWindow.IsCompleted, "the window closes");

            // ---- only one kind of a level's changes is the cause: only that kind is left out ----
            StopWhile(l => In(l, ExportParts.Level("stonehill", ExportParts.Music)) ? new InvalidDataException("Stone Hill's music cannot be used.") : null);
            CreateBin(window);
            string fixedKind = FixIt();
            Console.WriteLine("     " + fixedKind);
            Check(fixedKind.StartsWith("Created ", StringComparison.Ordinal) && window.LeftOutForTesting.SequenceEqual([ExportParts.Level("stonehill", ExportParts.Music)]) &&
                  fixedKind.Contains("Stone Hill's new music", StringComparison.Ordinal), "when one kind of a level's changes is the cause, only that kind is left out");
            Check(Game(true, true, "only Stone Hill's music left out"), "the game has both new names");
            PutEverythingBack();

            // ---- a stop that names nothing: found by trying ----
            StopWhile(l => In(l, ExportParts.Hover) ? new InvalidDataException("The game's program has no room.") : null);
            CreateBin(window);
            string fixedUnnamed = FixIt();
            Console.WriteLine("     " + fixedUnnamed);
            Check(fixedUnnamed.StartsWith("Created ", StringComparison.Ordinal) && window.LeftOutForTesting.SequenceEqual([ExportParts.Hover]) &&
                  fixedUnnamed.Contains("Left out Hover (Game tab)", StringComparison.Ordinal), "a stop that names nothing is traced to its part by trying: " + string.Join(", ", window.LeftOutForTesting));
            Check(Game(true, true, "Hover left out"), "the game has both new names");
            PutEverythingBack();

            // ---- two causes behind the same words: both are left out, the rest is put back ----
            StopWhile(l => In(l, ExportParts.Level("artisans")) || In(l, ExportParts.Hover) ? new InvalidDataException("Two things are wrong.") : null);
            CreateBin(window);
            string fixedTwo = FixIt();
            Console.WriteLine("     " + fixedTwo);
            Check(fixedTwo.StartsWith("Created ", StringComparison.Ordinal) && window.LeftOutForTesting.Order().SequenceEqual(new[] { ExportParts.Level("artisans"), ExportParts.Hover }.Order()),
                "two causes behind the same words: both are left out and the rest is in: " + string.Join(", ", window.LeftOutForTesting));
            Check(Game(false, true, "Artisans and Hover left out"), "the game on disk is the one without them: Stone Hill's new name, not Artisans'");
            PutEverythingBack();

            // ---- a stop that passes by itself: nothing is left out ----
            int times = 0;
            window.CreateBinFaultForTesting = () => { _runs++; if (times++ == 0) throw new InvalidDataException("The project changed during export."); };
            CreateBin(window);
            string passing = FixIt();
            Check(passing.StartsWith("Created ", StringComparison.Ordinal) && window.LeftOutForTesting.Count == 0 && !passing.Contains("LEFT OUT", StringComparison.Ordinal),
                "a stop that passes by itself is tried again, and nothing is left out: " + Short(passing));

            // ---- the computer's failures offer nothing ----
            foreach (Exception failure in new Exception[] { new FileNotFoundException("Select your original disc image first."), new IOException("There is not enough space on the disk."), new UnauthorizedAccessException("Access is denied.") })
            {
                StopWhile(_ => failure);
                string refused = CreateBin(window);
                Check(refused.StartsWith("Could not create BIN: " + failure.Message, StringComparison.Ordinal) && !refused.Contains("Fix It For Me", StringComparison.Ordinal) &&
                      !fixIt.IsVisible && !window.FixItOnOfferForTesting, $"a failure of the computer's ({failure.GetType().Name}) offers no Fix It For Me");
            }

            // ---- a stop nothing in the project causes: says so, leaves nothing out ----
            StopWhile(_ => new InvalidDataException("It never works."));
            CreateBin(window);
            string never = FixIt();
            Console.WriteLine("     " + never);
            Check(never.StartsWith("Fix It For Me could not get the game made. Even with nothing of yours in the game, Create BIN stops: It never works.", StringComparison.Ordinal) &&
                  never.Contains("Nothing was left out", StringComparison.Ordinal) && window.LeftOutForTesting.Count == 0 && !fixIt.IsVisible,
                "a stop nothing in the project causes: it says so and leaves nothing out");
            Check(Game(true, true, "the last game made"), "the last game made is still there, whole");

            Console.WriteLine($"FIX_IT_UI_OK checks={_checks} createBinRuns={_runs}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static string Short(string text) => text.Length <= 220 ? text : text[..220] + "…";

    private static bool Contains(byte[] bytes, string text) => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    private static void Run(Task task, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(60)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(15); }
        if (!task.IsCompleted) throw new TimeoutException(what + " did not finish.");
        task.GetAwaiter().GetResult();
    }

    private static string CreateBin(MainWindow window)
    {
        var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
        var clock = Stopwatch.StartNew();
        while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(40))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(15);
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope") is { } scope) { Click(Find<Button>(scope, "ExportCreate")); continue; }
            foreach (Window other in window.OwnedWindows.ToArray()) other.Close();
        }
        Pump(60);
        Check(command.IsCompleted, "Create BIN finished");
        return Status(window);
    }

    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return window;
        }
        throw new TimeoutException("The editor did not become idle.");
    }

    private static object? Invoke(MainWindow w, string name, params object?[] args) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(w, args);
    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(5); }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Fix It UI: " + label); Console.WriteLine("ok   " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
