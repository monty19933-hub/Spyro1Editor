using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Source-bound check for a project whose saved terrain Create BIN would refuse (a corner dragged until the
/// ground folds). The export check must say so, the object browser must still load, and "Undo that change"
/// must clear it. Runs on a disposable clone of the given project folder.
/// Usage: UiSmoke --terrain-problem-ui &lt;project folder with a folded Artisans edit&gt; &lt;output folder&gt;
/// </summary>
internal static class TerrainProblemUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        CloneDirectory(project, workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.CurrentDirectory = workspace;
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            LevelDefinition artisans = catalog.FindByKey("artisans")!;
            if (!ReferenceEquals(window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel, artisans))
            {
                var load = (Task)Invoke(window, "SelectLevelAsync", artisans, null)!;
                Wait(() => load.IsCompleted, "load Artisans", 300);
            }
            WaitIdle(window);

            // 1. The export check reports the fold instead of "Stable".
            var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
            Wait(() => check.IsCompleted, "export check", 600);
            Check(check.Result, "export check ran");
            var problem = window.CurrentTerrainExportProblemForTesting;
            Check(problem != null && problem.FaceKeys.Count >= 1 && problem.ShapeOnly &&
                  problem.Summary.Contains("folds over itself", StringComparison.Ordinal),
                "the saved fold is reported in plain words with the tiles involved");
            Border bar = Find<Border>(window, "TerrainProblemBar");
            Check(bar.IsVisible && Find<TextBlock>(window, "TerrainProblemText").Text!.Contains("Artisans", StringComparison.Ordinal),
                "the red bar is visible and names the level");
            Check(Find<TextBlock>(window, "ExportCheckSummary").Text!.Contains("Blocked", StringComparison.OrdinalIgnoreCase) ||
                  Get<TextBlock>(window, "_buildSafetySummaryText").Text!.Contains("Blocked", StringComparison.OrdinalIgnoreCase),
                "the export check summary is blocked, not stable");
            Snapshot(window, Path.Combine(outDir, "1-problem-bar.png"));

            // 2. The object browser still loads its catalogue and says why space checks are approximate.
            Button add = Find<Button>(window, "UniversalMobyLibraryButton");
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, add));
            Wait(() => window.OwnedWindows.Any(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal)), "browser opened", 30);
            Window browser = window.OwnedWindows.Last(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal));
            ListBox list = Find<ListBox>(browser, "UniversalMobyList");
            Wait(() => list.ItemCount > 150, "catalogue loaded although the terrain cannot be exported", 300);
            Check(Find<TextBlock>(browser, "UniversalMobyTerrainNotice").IsVisible, "the browser explains that terrain changes are ignored for space checks");
            Snapshot(browser, Path.Combine(outDir, "2-browser-loaded.png"));
            browser.Close(); Pump(20);

            // 3. Show it selects the tiles; Undo that change restores only them.
            Button show = Find<Button>(window, "TerrainProblemShow");
            show.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, show));
            var viewport = Get<EditorViewport>(window, "_viewport");
            Wait(() => window.CaptureEditorShellSessionSnapshotForTesting().SelectedTerrain != null, "Show it selects a tile", 30);
            Check(problem!.FaceKeys.Contains(window.CaptureEditorShellSessionSnapshotForTesting().SelectedTerrain!.RuntimeKey) &&
                  (problem.FaceKeys.Count < 2 || viewport.MultiSelectedTerrainIndexes.Count == problem.FaceKeys.Count),
                "Show it selects exactly the tiles involved");
            var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            int editedBefore = geometry.Polygons.Count(p => p.IsTerrainEdited);
            int removedBefore = geometry.Polygons.Count(p => p.IsTerrainRemoved);
            Button undo = Find<Button>(window, "TerrainProblemUndo");
            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
            Wait(() => window.CurrentTerrainExportProblemForTesting == null, "Undo that change clears the bar", 30);
            Check(geometry.Polygons.Where(p => problem.FaceKeys.Contains(p.RuntimeKey)).All(p => !p.HasPositionEdit && !p.HasHeightEdit),
                "the tiles involved are back to their original shape");
            Check(geometry.Polygons.Count(p => p.IsTerrainRemoved) == removedBefore &&
                  geometry.Polygons.Count(p => p.IsTerrainEdited) >= editedBefore - problem.FaceKeys.Count,
                "other terrain changes (including removed tiles) are untouched");

            // 4. Save, check again: the level exports.
            var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
            Wait(() => save.IsCompleted, "save", 120);
            Check(save.Result, "save succeeded");
            check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
            Wait(() => check.IsCompleted, "second export check", 600);
            Pump(50);
            Check(window.CurrentTerrainExportProblemForTesting == null && !bar.IsVisible,
                "after the undo and a save the terrain exports and the bar is gone");
            Snapshot(window, Path.Combine(outDir, "3-fixed.png"));
            Console.WriteLine($"TERRAIN_PROBLEM_UI_OK checks={_checks} tiles={problem.FaceKeys.Count}");
        }
        finally
        {
            window.Close(); Pump(10);
        }
    }

    private static void CloneDirectory(string source, string target)
    {
        // APFS clone when available (instant, no extra space); plain copy otherwise.
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", source, target]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode == 0 && Directory.Exists(target)) return;
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, target));
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target), overwrite: true);
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
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Wait(Func<bool> predicate, string label, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Check(predicate(), label);
        Dispatcher.UIThread.RunJobs();
    }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Terrain problem UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
