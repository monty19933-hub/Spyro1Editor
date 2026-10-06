using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Several ordinary terrain edits on neighbouring tiles of a real level, then the export check: what does the
/// editor say about each? Runs on a disposable clone of the given project folder.
/// Usage: UiSmoke --terrain-multi-edit-ui &lt;project folder&gt; &lt;output folder&gt; [level key, default artisans]
/// </summary>
internal static class TerrainMultiEditUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, string levelKey)
    {
        outDir = Path.GetFullPath(outDir);
        project = Path.GetFullPath(project);
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "logs"));
        Environment.CurrentDirectory = workspace;
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(levelKey)!;
            if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != levelKey)
            {
                var load = (Task)Invoke(window, "SelectLevelAsync", level, null)!;
                Wait(() => load.IsCompleted, "load " + level.DisplayName, 300);
                WaitIdle(window);
            }
            var viewport = Get<EditorViewport>(window, "_viewport");
            TextBlock status = Get<TextBlock>(window, "_statusText");
            var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            SelectTab(window, "Terrain");

            // Pick a cluster of neighbouring, unedited ground tiles Spyro can stand on (collision under every
            // corner): start from one and take those sharing a corner.
            var collisionPoints = Get<HashSet<string>>(window, "_terrainCollisionTriangleKeys").SelectMany(k => k.Split('|')).ToHashSet(StringComparer.Ordinal);
            bool Solid(TerrainPolygon p) => Enumerable.Range(0, p.Points.Count).All(i => collisionPoints.Contains($"{MathF.Round(p.OriginalPoints[i].X)},{MathF.Round(p.OriginalPoints[i].Y)},{MathF.Round(p.OriginalZValues[i])}"));
            bool Ground(int i) { var p = geometry.Polygons[i]; return !p.IsTerrainRemoved && !p.IsTerrainEdited && string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) && p.TextureId >= 0 && p.Points.Count == 4 && Solid(p); }
            int seed = Enumerable.Range(0, geometry.Polygons.Count).Where(Ground).OrderBy(i => Math.Abs(geometry.Polygons[i].ZValues[0] - geometry.Polygons[i].ZValues[2])).Skip(200).First();
            var seedPoints = geometry.Polygons[seed].Points;
            var cluster = Enumerable.Range(0, geometry.Polygons.Count).Where(i => i != seed && Ground(i) &&
                geometry.Polygons[i].Points.Any(q => seedPoints.Any(s => Math.Abs(s.X - q.X) < 0.5f && Math.Abs(s.Y - q.Y) < 0.5f))).Take(3).ToList();
            cluster.Insert(0, seed);
            Console.WriteLine("tiles: " + string.Join(", ", cluster.Select(i => $"{i} ({geometry.Polygons[i].RuntimeKey})")));
            void Report(string stage)
            {
                var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
                Wait(() => check.IsCompleted, "export check: " + stage, 600); Pump(30);
                var problem = window.CurrentTerrainExportProblemForTesting;
                Console.WriteLine($"[{stage}] export check: {Find<TextBlock>(window, "ExportCheckSummary").Text?.Replace('\n', ' ')}");
                Console.WriteLine($"[{stage}] terrain problem: {(problem == null ? "none" : problem.Summary + " tiles=" + string.Join(",", problem.FaceKeys) + " | " + problem.TechnicalMessage.Replace('\n', ' ')[..Math.Min(300, problem.TechnicalMessage.Length)])}");
            }

            // 0. A scenery tile (no collision under any corner) must be refused in plain words and stay unchanged.
            int scenery = Enumerable.Range(0, geometry.Polygons.Count).First(i => { var p = geometry.Polygons[i]; return !p.IsTerrainRemoved && !p.IsTerrainEdited && p.Points.Count == 4 && Enumerable.Range(0, 4).All(c => !collisionPoints.Contains($"{MathF.Round(p.OriginalPoints[c].X)},{MathF.Round(p.OriginalPoints[c].Y)},{MathF.Round(p.OriginalZValues[c])}")); });
            viewport.SelectTerrainForTesting(scenery); Pump(10);
            Invoke(window, "NudgeSelectedTerrain", 32f); Pump(5);
            Console.WriteLine($"scenery {scenery} ({geometry.Polygons[scenery].RuntimeKey}): {status.Text}");
            Check(status.Text!.Contains("scenery", StringComparison.Ordinal) && !geometry.Polygons[scenery].IsTerrainEdited, "a scenery tile is refused in plain words and stays unchanged");

            // 1. Raise three neighbouring tiles three times each.
            foreach (int i in cluster.Take(3))
            {
                viewport.SelectTerrainForTesting(i); Pump(10);
                for (int n = 0; n < 3; n++) { Invoke(window, "NudgeSelectedTerrain", 32f); Pump(5); }
                Console.WriteLine($"raised {i}: {status.Text}");
            }
            Save(window); Report("after raising three tiles");
            Snapshot(window, Path.Combine(outDir, "1-raised.png"));

            // 2. Lower the fourth tile three times.
            viewport.SelectTerrainForTesting(cluster[3]); Pump(10);
            for (int n = 0; n < 3; n++) { Invoke(window, "NudgeSelectedTerrain", -32f); Pump(5); }
            Console.WriteLine($"lowered {cluster[3]}: {status.Text}");
            Save(window); Report("after lowering a fourth tile");

            // 3. Move the first tile sideways by 64 units, the way a drag does.
            viewport.SelectTerrainForTesting(cluster[0]); Pump(10);
            Invoke(window, "MoveTerrainFromViewport", new ViewportTerrainMoveRequestedEventArgs(cluster[0], geometry.Polygons[cluster[0]], 64f, 0f, 0f, false));
            Pump(10); Console.WriteLine($"moved {cluster[0]}: {status.Text}");
            Save(window); Report("after moving a tile sideways");
            Snapshot(window, Path.Combine(outDir, "2-moved.png"));

            // 4. Raise brush strokes across the cluster (whole tiles).
            var center = geometry.Polygons[cluster[1]].Points.Aggregate(new Vector2f(0, 0), (a, b) => new Vector2f(a.X + b.X, a.Y + b.Y));
            center = new Vector2f(center.X / geometry.Polygons[cluster[1]].Points.Count, center.Y / geometry.Polygons[cluster[1]].Points.Count);
            viewport.TerrainBrushAction = TerrainBrushAction.Raise;
            Invoke(window, "ApplyViewportTerrainBrush", new ViewportTerrainBrushRequestedEventArgs(cluster[1], geometry.Polygons[cluster[1]], center, TerrainBrushAction.Raise, true, 1.0));
            for (int n = 0; n < 6; n++)
                Invoke(window, "ApplyViewportTerrainBrush", new ViewportTerrainBrushRequestedEventArgs(cluster[1], geometry.Polygons[cluster[1]], new Vector2f(center.X + n * 40, center.Y), TerrainBrushAction.Raise, false, 1.0));
            Pump(10); Console.WriteLine($"brush: {status.Text}");
            viewport.TerrainBrushAction = TerrainBrushAction.Off;
            Save(window); Report("after a brush stroke");
            Snapshot(window, Path.Combine(outDir, "3-brushed.png"));
            int edited = geometry.Polygons.Count(p => p.IsTerrainEdited);
            Console.WriteLine($"TERRAIN_MULTI_EDIT_UI_DONE editedTiles={edited} checks={_checks}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static void Save(MainWindow window)
    {
        var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => save.IsCompleted, "save", 120);
        Console.WriteLine("save: " + (save.Result ? "ok" : "FAILED") + " — " + Get<TextBlock>(window, "_statusText").Text);
        Pump(20);
    }
    private static void SelectTab(MainWindow window, string header)
    {
        TabControl tabs = Find<TabControl>(window, "EditorWorkspaceToolTabs");
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == header); Pump(20);
    }
    private static void WaitIdle(MainWindow window)
    {
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart(); else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).First(x => x.Name == name && x.GetParameters().Length == args.Length);
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
        Check(predicate(), label); Dispatcher.UIThread.RunJobs();
    }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Terrain multi edit UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
