using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Universal;

/// <summary>File ▾ › Clear Entire Project to Base Game…, through the editor. <c>--clear-project-ui-only</c> needs no disc:
/// a project with a level of the user's own, changes to two of the game's levels, a game setting and an unsaved change.
/// The command asks first and says what is in the project; Cancel, Return and Escape clear nothing; a clear that fails
/// changes nothing and leaves the editor usable; "Yes, I am sure" moves the work to a dated folder beside the projects,
/// keeps the chosen game, its data and the games already made, and opens the project afresh in a new window without
/// asking about the unsaved change. <c>--clear-project-ui &lt;output&gt; &lt;usa disc&gt;</c> does the same on a project of the
/// game's own levels and makes a game before and after: the game made after the clear has nothing of the old work.</summary>
internal static class ClearProjectUiCases
{
    private static int _checks;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];
    private const string Command = "Clear Entire Project to Base Game…", ArtisansName = "ARTISAN HOME", StoneHillName = "ROCKY HILL", LaterName = "PEBBLE HILL";

    internal static void Run(string? outDir, string? disc)
    {
        _checks = 0;
        bool withGame = disc != null;
        if (withGame && !File.Exists(disc)) throw new ArgumentException("The test needs the original disc image.");
        string home = outDir ?? Directory.CreateTempSubdirectory("spyro-clear-project-ui-").FullName;
        Directory.CreateDirectory(home);
        var (workspace, keptRoot, discChoice, support, output, before, directoryBefore) = PrepareProject(home, disc);

        MainWindow window = Open(withGame);
        MainWindow? reopened = null;
        try
        {
            if (!SamePath(Get<Spyro.Editor.Core.Workspace.EditorWorkspace>(window, "_workspace").RootPath, workspace))
                throw new InvalidOperationException("The editor opened another folder than the test project; stopping before any change.");
            window.OpenPathOverrideForTesting = _ => { };
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            var texts = TextTargetCatalog.CreateDefault();
            LevelDefinition artisans = catalog.FindByKey("artisans")!, stoneHill = catalog.FindByKey("stonehill")!;
            string Bin() => Directory.GetFiles(output, "*.bin").Single();

            // ---- the project: changes to two of the game's levels, a game setting; without a disc also a level of the user's own ----
            Run(LevelTextEditStore.SaveAsync(workspace, texts.FindForLevel(artisans)!, ArtisansName), "save Artisans' name");
            Run(LevelTextEditStore.SaveAsync(workspace, texts.FindForLevel(stoneHill)!, StoneHillName), "save Stone Hill's name");
            int otherTrack = MusicTrackCatalog.SelectableTracks.First(t => t.TrackId != MusicTrackCatalog.GetNativeTrackId(stoneHill)).TrackId;
            Run(LevelMusicEditStore.SaveAsync(workspace, stoneHill, otherTrack), "save Stone Hill's music");
            SpyroMovesSettings.SaveHover(workspace, true);
            if (withGame)
            {
                string made = CreateBin(window);
                byte[] exe = UniversalDiscImage.Read(Bin()).Exe;
                Check(made.StartsWith("Created ", StringComparison.Ordinal) && Contains(exe, ArtisansName) && Contains(exe, StoneHillName),
                    "before the clear, Create BIN makes a game with the project's work in it: " + Short(made));
            }
            else
            {
                Invoke(window, "ApplyFreshScene", SceneDocument.New("My Level")); Pump(20);
                Run((Task)Invoke(window, "SaveFromToolbarAsync")!, "save the level of the user's own");
                Check(File.Exists(Path.Combine(workspace, SceneDocument.FileName)), "the project holds a saved level of the user's own");
            }
            long gameBytes = new FileInfo(Bin()).Length;
            string discChoiceBefore = File.ReadAllText(discChoice);
            // A change that is not saved: clearing must not stop to ask about it, and must not save it.
            Get<GeometryCandidate>(window, "_currentGeometry").Polygons[0].ApplyTerrainDeltaZ(8);
            Check((bool)Invoke(window, "HasUnsavedTerrainEdits")!, "the open level has a change that is not saved");
            string[] projectBefore = Listing(workspace);

            // ---- the command is in the File menu, asks first, and says what is in the project ----
            MenuItem item = ((MenuFlyout)Find<Button>(window, "EditorFileMenu").Flyout!).Items.OfType<MenuItem>().Single(i => i.Header?.ToString() == Command);
            Window Ask(MainWindow owner, MenuItem command)
            {
                command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var clock = Stopwatch.StartNew();
                while (!owner.OwnedWindows.Any(w => w.Name == "ClearProjectDialog") && clock.Elapsed < TimeSpan.FromSeconds(30)) Pump();
                return owner.OwnedWindows.Single(w => w.Name == "ClearProjectDialog");
            }
            static string Said(Window dialog) => string.Join(" | ", dialog.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Window ask = Ask(window, item);
            string said = Said(ask);
            Console.WriteLine("     " + said);
            Check(said.Contains("Clear the entire project back to the base Spyro game?", StringComparison.Ordinal) &&
                  said.Contains("Everything you have made is taken out of your project", StringComparison.Ordinal), "the question says what clearing does");
            Check(said.Contains("your changes to 2 of the game's levels (Artisans, Stone Hill)", StringComparison.Ordinal) && said.Contains("game settings (Hover)", StringComparison.Ordinal) &&
                  (withGame || said.Contains("1 level you built (My Level)", StringComparison.Ordinal)), "it lists what is in the project now");
            Check(said.Contains("Changes you have not saved yet are lost", StringComparison.Ordinal), "it warns that the unsaved change is lost");
            Check(said.Contains("moved, not deleted", StringComparison.Ordinal) && said.Contains(keptRoot, StringComparison.Ordinal) &&
                  said.Contains("Games you have already created stay", StringComparison.Ordinal) && said.Contains("do not have to choose your game again", StringComparison.Ordinal),
                "it says where the work goes and what stays");
            Button yes = Find<Button>(ask, "ClearProjectConfirm"), cancel = Find<Button>(ask, "ClearProjectCancel");
            Check(yes.Content as string == "Yes, I am sure" && cancel.Content as string == "Cancel" && cancel.IsDefault && cancel.IsCancel && !yes.IsDefault,
                "the buttons are Cancel and \"Yes, I am sure\", and Cancel is the one Return and Escape press");
            if (outDir != null) Snapshot(ask, Path.Combine(outDir, "clear-project-question.png"));

            // ---- Cancel, Return and Escape clear nothing ----
            void NothingCleared(string how)
            {
                Pump(40);
                Check(!window.OwnedWindows.Any() && window.IsVisible && window.ReopenedForTesting == null && !Directory.Exists(keptRoot) &&
                      Listing(workspace).SequenceEqual(projectBefore) && Status(window).StartsWith("Nothing was cleared", StringComparison.Ordinal) &&
                      (bool)Invoke(window, "HasUnsavedTerrainEdits")!, how + " clears nothing: the project, the unsaved change and the window are as they were");
            }
            Click(cancel); NothingCleared("Cancel");
            ask = Ask(window, item);
            ask.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); NothingCleared("Return");
            ask = Ask(window, item);
            ask.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); NothingCleared("Escape");

            // ---- a clear that fails changes nothing, says so, and the editor goes on working ----
            window.ClearProjectFaultForTesting = name => { if (name == "spyro-moves.json") throw new IOException("The disk refused."); };
            ask = Ask(window, item);
            Click(Find<Button>(ask, "ClearProjectConfirm"));
            var waited = Stopwatch.StartNew();
            while (!Status(window).StartsWith("Could not clear", StringComparison.Ordinal) && waited.Elapsed < TimeSpan.FromSeconds(30)) Pump();
            Pump(40);
            string failed = Status(window);
            Check(failed.StartsWith("Could not clear the project: The disk refused.", StringComparison.Ordinal) && failed.Contains("Nothing was changed: your project is as it was", StringComparison.Ordinal),
                "a clear that fails says so: " + failed);
            Check(window.IsVisible && window.ReopenedForTesting == null && !Directory.Exists(keptRoot) && Listing(workspace).SequenceEqual(projectBefore) &&
                  !Get<bool>(window, "_workspaceTransitionBusy") && (bool)Invoke(window, "HasUnsavedTerrainEdits")!,
                "and changes nothing: the project is whole, nothing was kept elsewhere, and the editor is not left busy");
            window.ClearProjectFaultForTesting = null;

            // ---- "Yes, I am sure" ----
            ask = Ask(window, item);
            Click(Find<Button>(ask, "ClearProjectConfirm"));
            waited.Restart();
            while (window.ReopenedForTesting == null && waited.Elapsed < TimeSpan.FromSeconds(60)) Pump();
            reopened = window.ReopenedForTesting ?? throw new InvalidOperationException("Clear project UI: the editor did not open the cleared project (" + Status(window) + ")");
            WaitIdle(reopened, withGame);
            reopened.OpenPathOverrideForTesting = _ => { };
            Check(!window.IsVisible && reopened.IsVisible && !reopened.OwnedWindows.Any() && SamePath(Get<Spyro.Editor.Core.Workspace.EditorWorkspace>(reopened, "_workspace").RootPath, workspace),
                "the editor opens the same project afresh in a new window and the old one closes without asking about the unsaved change");
            string kept = Directory.GetDirectories(keptRoot).Single();
            string status = Status(reopened);
            Console.WriteLine("     " + status);
            Check(status.StartsWith("Your project is back to the original Spyro game.", StringComparison.Ordinal) && status.Contains("moved, not deleted", StringComparison.Ordinal) &&
                  status.Contains(kept, StringComparison.Ordinal), "it says the project is the original game again and where the work went");
            Check(Path.GetFileName(kept).StartsWith("default-project (cleared " + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal),
                "the work is in a dated folder in \"Old projects\" beside the projects: " + Path.GetFileName(kept));
            Check(File.Exists(Path.Combine(kept, "artisans-level-text-edit-plan.json")) && File.Exists(Path.Combine(kept, "stonehill-level-text-edit-plan.json")) &&
                  File.Exists(Path.Combine(kept, "stonehill-level-music-edit-plan.json")) && File.Exists(Path.Combine(kept, "spyro-moves.json")) &&
                  (withGame || File.Exists(Path.Combine(kept, SceneDocument.FileName))) &&
                  File.ReadAllText(Path.Combine(kept, ProjectClear.NoteFileName)).Contains("To bring this work back", StringComparison.Ordinal),
                "all of the work is there, with a note that says how to bring it back");
            Check(!Directory.Exists(Path.Combine(kept, "_local", "authored-native")) && !Directory.Exists(Path.Combine(workspace, "_local", "authored-native")),
                "the compiled stages Create BIN keeps are not kept");
            Check(ProjectClear.WhatWouldGo(workspace).Count == 0 && LevelTextEditStore.Load(workspace, texts.FindForLevel(artisans)!) == null &&
                  LevelTextEditStore.Load(workspace, texts.FindForLevel(stoneHill)!) == null && LevelMusicEditStore.Load(workspace, stoneHill) == null &&
                  !SpyroMovesSettings.LoadHover(workspace) && !File.Exists(Path.Combine(workspace, SceneDocument.FileName)),
                "nothing of the work is left in the project" + string.Concat(ProjectClear.WhatWouldGo(workspace).Select(n => " [" + n + "]")));
            Check(File.ReadAllText(discChoice) == discChoiceBefore && File.Exists(Path.Combine(support, "spyro-level-catalog.json")) &&
                  new FileInfo(Bin()).Length == gameBytes && Directory.Exists(Path.Combine(workspace, "editor-cache")) && File.Exists(Path.Combine(workspace, "spyro-wad-analysis.json")),
                "the chosen game, what was read from it, the editor's own files and the game already made are still there");
            var shown = reopened.CaptureEditorShellSessionSnapshotForTesting();
            Check(reopened.FreshSceneForTesting == null && !(bool)Invoke(reopened, "HasUnsavedTerrainEdits")! && !(bool)Invoke(reopened, "HasUnsavedMobyEdits")! &&
                  (!withGame || shown.CurrentLevel?.Key == "artisans" && shown.CurrentGeometry != null && shown.CurrentGeometry.Polygons.All(p => !p.HasHeightEdit)),
                withGame ? "the new window shows the original Artisans, with nothing changed and nothing unsaved" : "the new window holds no level of the user's own and nothing unsaved");
            if (outDir != null) Snapshot(reopened, Path.Combine(outDir, "after-clear.png"));

            // ---- the cleared project is an ordinary project: the question finds nothing in it, and it takes new work ----
            MenuItem itemAgain = ((MenuFlyout)Find<Button>(reopened, "EditorFileMenu").Flyout!).Items.OfType<MenuItem>().Single(i => i.Header?.ToString() == Command);
            ask = Ask(reopened, itemAgain);
            Check(Said(ask).Contains("Nothing saved was found in your project", StringComparison.Ordinal) && !Said(ask).Contains("not saved yet", StringComparison.Ordinal),
                "asked again, the question finds nothing saved in the project");
            Click(Find<Button>(ask, "ClearProjectCancel")); Pump(40);
            Check(reopened.IsVisible && reopened.ReopenedForTesting == null && Directory.GetDirectories(keptRoot).Length == 1, "and Cancel leaves it alone");
            Run(LevelTextEditStore.SaveAsync(workspace, texts.FindForLevel(stoneHill)!, LaterName), "save Stone Hill's name after the clear");
            if (withGame)
            {
                string after = CreateBin(reopened);
                byte[] exe = UniversalDiscImage.Read(Bin()).Exe;
                Check(after.StartsWith("Created ", StringComparison.Ordinal) && Contains(exe, LaterName) && !Contains(exe, ArtisansName) && !Contains(exe, StoneHillName) &&
                      !after.Contains("LEFT OUT", StringComparison.Ordinal),
                    "a game made after the clear has the new work and nothing of the old: " + Short(after));
            }
            else
            {
                ask = Ask(reopened, itemAgain);
                Check(Said(ask).Contains("your changes to 1 of the game's levels (Stone Hill)", StringComparison.Ordinal), "work made after the clear is what the question lists next time");
                Click(Find<Button>(ask, "ClearProjectCancel")); Pump(40);
            }
            Console.WriteLine($"CLEAR_PROJECT_UI_OK checks={_checks}");
        }
        finally
        {
            foreach (MainWindow open in new[] { reopened, window }.OfType<MainWindow>())
            {
                foreach (Window other in open.OwnedWindows.ToArray()) other.Close();
                if (open.IsVisible) open.Close();
            }
            Pump(10);
            Environment.CurrentDirectory = directoryBefore;
            foreach (var (name, value) in before) Environment.SetEnvironmentVariable(name, value);
            if (outDir == null) try { Directory.Delete(home, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>A packaged-style project in <paramref name="home"/> (support files, a chosen game: the real one, or a
    /// stand-in path with stand-ins for what is read from a disc) and the environment that makes the editor open it.</summary>
    internal static (string Workspace, string KeptRoot, string DiscChoice, string Support, string Output, Dictionary<string, string?> EnvironmentBefore, string DirectoryBefore)
        PrepareProject(string home, string? disc)
    {
        bool withGame = disc != null;
        // The usual layout: the project in "Projects", so cleared work goes to "Old projects" beside it.
        string workspace = Path.Combine(home, "Projects", "default-project"), keptRoot = Path.Combine(home, "Old projects");
        foreach (string old in new[] { Path.Combine(home, "Projects"), keptRoot }) if (Directory.Exists(old)) Directory.Delete(old, recursive: true);
        string settings = Directory.CreateDirectory(Path.Combine(workspace, "_local", "settings")).FullName;
        string chosen = disc ?? Path.Combine(home, "games", "Spyro the Dragon (USA).bin");
        string discChoice = Path.Combine(settings, "source-disc.json");
        File.WriteAllText(discChoice, JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), selectedPath = chosen, imagePath = chosen, cuePath = Path.ChangeExtension(chosen, ".cue") }));
        File.WriteAllText(Path.Combine(settings, "last-level.json"), "{\"levelKey\":\"artisans\"}");
        string support = Directory.CreateDirectory(Path.Combine(workspace, "support")).FullName;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog found.");
        foreach (string name in new[] { "spyro-level-catalog.json", "spyro-object-templates.json" })
            if (File.Exists(Path.Combine(repo, name))) File.Copy(Path.Combine(repo, name), Path.Combine(support, name));
        string output = Directory.CreateDirectory(Path.Combine(workspace, "output")).FullName;
        if (!withGame)
        {
            // Stand-ins for what the editor reads from a disc and for a game already made: a clear must leave them.
            Directory.CreateDirectory(Path.Combine(workspace, "editor-cache"));
            File.WriteAllText(Path.Combine(workspace, "editor-cache", "index.json"), "read from the disc");
            File.WriteAllText(Path.Combine(workspace, "spyro-wad-analysis.json"), "read from the disc");
            File.WriteAllText(Path.Combine(output, "An Earlier Game.bin"), "a game already made");
        }
        Dictionary<string, string?> before = new();
        foreach (string name in IsolatedRoots.Append("SPYRO_EDITOR_WORKSPACE").Append("SPYRO_EDITOR_RELEASE")) before[name] = Environment.GetEnvironmentVariable(name);
        string directoryBefore = Environment.CurrentDirectory;
        foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(home, "isolated", name)).FullName);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.CurrentDirectory = workspace;
        string found = Spyro.Editor.Core.Workspace.EditorWorkspace.Find().RootPath;
        if (!SamePath(found, workspace)) throw new InvalidOperationException("The editor would open " + found + " instead of the test project; stopping before it opens.");

        return (workspace, keptRoot, discChoice, support, output, before, directoryBefore);
    }

    /// <summary>Every file of the project with its size, less the compiled stages (a clear removes those even when it
    /// then fails, by design).</summary>
    private static string[] Listing(string root) =>
        [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(r => !r.StartsWith("_local/authored-native/", StringComparison.Ordinal) && !r.StartsWith("_local/authored-aggregate/", StringComparison.Ordinal) &&
                        !r.StartsWith("output/_combined-build/", StringComparison.Ordinal))
            .OrderBy(r => r, StringComparer.Ordinal)
            .Select(r => r + "|" + new FileInfo(Path.Combine(root, r)).Length)];

    private static bool SamePath(string a, string b) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(b));
    private static string Short(string text) => text.Length <= 220 ? text : text[..220] + "…";
    private static bool Contains(byte[] bytes, string text) => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    private static void Run(Task task, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(10)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(15); }
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

    private static MainWindow Open(bool withGame)
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        WaitIdle(window, withGame);
        return window;
    }

    /// <summary>Until the window has shown its first level (with a game) or has settled on its first screen (without).</summary>
    private static void WaitIdle(MainWindow window, bool withGame)
    {
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") || Get<int>(window, "_levelSelectionAsyncInFlight") > 0 ||
                        withGame && (snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null);
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Clear project UI: " + label); Console.WriteLine("ok   " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
