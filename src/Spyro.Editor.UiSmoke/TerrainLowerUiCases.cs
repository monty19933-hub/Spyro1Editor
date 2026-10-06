using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Lowers a floor tile that sits at the bottom of its scene sector's stored range (which the game's level
/// format could not hold before the sector origin re-base), checks that the export check accepts it, then
/// builds the game. Writes lowered-tile.json with the tile's position and heights for the emulator check.
/// Usage: UiSmoke --terrain-lower-ui &lt;project folder&gt; &lt;output folder&gt; [level key] [steps of 32, default 3]
/// </summary>
internal static class TerrainLowerUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, string levelKey, int steps)
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
            var collisionPoints = Get<HashSet<string>>(window, "_terrainCollisionTriangleKeys").SelectMany(k => k.Split('|')).ToHashSet(StringComparer.Ordinal);
            string Key(TerrainPolygon p, int i) => $"{MathF.Round(p.OriginalPoints[i].X)},{MathF.Round(p.OriginalPoints[i].Y)},{MathF.Round(p.OriginalZValues[i])}";
            bool Solid(TerrainPolygon p) => Enumerable.Range(0, p.Points.Count).All(i => collisionPoints.Contains(Key(p, i))) &&
                TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface) is not ("water" or "lava" or "ooze");
            // Candidates: textured, level quads with collision under every corner, lying at the floor of
            // their sector (lowest corner == the sector's stored origin height), and not at the level's edge.
            var candidates = Enumerable.Range(0, geometry.Polygons.Count).Where(i =>
            {
                var p = geometry.Polygons[i];
                if (p.IsTerrainRemoved || p.IsTerrainEdited || p.Points.Count != 4 || p.TextureId < 0 || !string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) || !Solid(p)) return false;
                var sector = geometry.SourceSectors.FirstOrDefault(s => s.SectorOffset == p.SectorOffset);
                if (sector?.HighPolyCoordinates is not { } c) return false;
                float minZ = p.OriginalZValues.Take(4).Min(), maxZ = p.OriginalZValues.Take(4).Max();
                return maxZ - minZ < 4 && Math.Abs(minZ - c.DecodedOriginZ) < 0.5f;
            }).OrderByDescending(i => geometry.Polygons[i].Bounds.Width * geometry.Polygons[i].Bounds.Height).ToList();
            Console.WriteLine($"candidates at their sector floor: {candidates.Count}");
            int chosen = -1;
            foreach (int i in candidates.Take(40))
            {
                viewport.SelectTerrainForTesting(i); Pump(10);
                bool ok = true;
                for (int n = 0; n < steps && ok; n++)
                {
                    Invoke(window, "NudgeSelectedTerrain", -32f); Pump(5);
                    ok = status.Text!.StartsWith("Lowered", StringComparison.Ordinal);
                }
                Console.WriteLine($"tile {i} ({geometry.Polygons[i].RuntimeKey}): {status.Text}");
                if (ok) { chosen = i; break; }
                // put it back before trying the next one
                var reset = (Task)Invoke(window, "UndoSelectedTerrainHeightAsync")!;
                Wait(() => reset.IsCompleted, "reset candidate", 60); Pump(5);
            }
            Check(chosen >= 0, "a floor tile could be lowered by " + (steps * 32));
            TerrainPolygon tile = geometry.Polygons[chosen];
            var sectorMeta = geometry.SourceSectors.First(s => s.SectorOffset == tile.SectorOffset).HighPolyCoordinates!.Value;
            float cx = tile.Points.Average(p => p.X), cy = tile.Points.Average(p => p.Y);
            float originalZ = tile.OriginalZValues.Take(4).Average(), loweredZ = tile.ZValues.Take(4).Average();
            Console.WriteLine($"chosen {tile.RuntimeKey}: centre ({cx:0},{cy:0}) Z {originalZ:0} -> {loweredZ:0}; sector origin Z {sectorMeta.DecodedOriginZ}");
            Check(loweredZ < sectorMeta.DecodedOriginZ, "the lowered tile is below its sector's original origin (re-base needed)");
            // A control spot: an unedited solid tile nearby with the same original height.
            int control = Enumerable.Range(0, geometry.Polygons.Count).Where(i =>
            {
                var p = geometry.Polygons[i];
                if (i == chosen || p.IsTerrainEdited || p.Points.Count != 4 || p.TextureId < 0 || !Solid(p) || p.OriginalZValues.Take(4).Max() - p.OriginalZValues.Take(4).Min() >= 4) return false;
                float distance = Math.Abs(p.Points.Average(q => q.X) - cx) + Math.Abs(p.Points.Average(q => q.Y) - cy);
                return distance > 500 && distance < 3000 && Math.Abs(p.OriginalZValues[0] - originalZ) < 200;
            }).OrderBy(i => Math.Abs(geometry.Polygons[i].Points.Average(q => q.X) - cx) + Math.Abs(geometry.Polygons[i].Points.Average(q => q.Y) - cy)).First();
            TerrainPolygon controlTile = geometry.Polygons[control];
            Save(window);
            var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
            Wait(() => check.IsCompleted, "export check", 600); Pump(30);
            var problem = window.CurrentTerrainExportProblemForTesting;
            Console.WriteLine("export check: " + Find<TextBlock>(window, "ExportCheckSummary").Text?.Replace('\n', ' '));
            Check(problem == null, "the export check accepts the lowered floor tile" + (problem == null ? "" : ": " + problem.TechnicalMessage));
            Snapshot(window, Path.Combine(outDir, "1-lowered.png"));

            string bin = RunCreateBin(window, workspace, out string createStatus);
            Console.WriteLine("create bin: " + createStatus);
            Check(bin.Length > 0, "Create BIN published a BIN: " + createStatus);
            File.WriteAllText(Path.Combine(outDir, "lowered-tile.json"), JsonSerializer.Serialize(new
            {
                level = levelKey,
                tile = tile.RuntimeKey,
                centre = new { x = (int)cx, y = (int)cy },
                originalZ = (int)originalZ,
                loweredZ = (int)loweredZ,
                sectorOriginZ = sectorMeta.DecodedOriginZ,
                control = new { tile = controlTile.RuntimeKey, x = (int)controlTile.Points.Average(q => q.X), y = (int)controlTile.Points.Average(q => q.Y), z = (int)controlTile.OriginalZValues[0] },
                bin,
                cue = Path.ChangeExtension(bin, ".cue")
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"TERRAIN_LOWER_UI_OK checks={_checks} tile={tile.RuntimeKey} z={originalZ:0}->{loweredZ:0} bin={bin}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static string RunCreateBin(MainWindow window, string workspace, out string statusText)
    {
        var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
        bool confirmed = false;
        var clock = Stopwatch.StartNew();
        while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(40))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
            Window? scope = window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope");
            if (scope != null && !confirmed)
            {
                Button create = Find<Button>(scope, "ExportCreate");
                create.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, create));
                confirmed = true;
            }
            foreach (Window other in window.OwnedWindows.Where(w => w.Name != "ProjectExportScope").ToArray())
            {
                Console.WriteLine("dialog '" + other.Title + "': " + string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                Check(other.Title != "Terrain Patch Check", "Create BIN must not warn about visual-only edits for a lowered floor tile");
                other.Close();
            }
        }
        Check(command.IsCompleted, "Create BIN finished");
        Pump(100);
        statusText = Get<TextBlock>(window, "_statusText").Text ?? "";
        string output = Path.Combine(workspace, "output");
        return Directory.Exists(output) ? Directory.GetFiles(output, "*.bin").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? "" : "";
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Terrain lower UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
