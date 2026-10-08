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
/// Tile Type…: a water tile becomes solid ground and a grass tile becomes water through the dialog, the
/// export check accepts it and Create BIN builds the game. Writes tile-type.json for the emulator check.
/// Usage: UiSmoke --tile-type-ui &lt;project folder&gt; &lt;output folder&gt; [level key, default artisans] [--no-bin]
/// </summary>
internal static class TileTypeUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, string levelKey, bool createBin)
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
            Button typeButton = Find<Button>(window, "TerrainTileType");
            Check(!typeButton.IsEnabled || viewport.MultiSelectedTerrainIndexes.Count > 0 || window.CaptureEditorShellSessionSnapshotForTesting().SelectedTerrain != null, "Tile Type… is disabled until a tile is selected");
            TextBlock selection = Find<TextBlock>(window, "TerrainPanelSelection");

            // 1. A water tile becomes solid ground.
            int water = PickTile(window, geometry, "water", "water", viewport, selection);
            Console.WriteLine($"water tile {water} ({geometry.Polygons[water].RuntimeKey}): {selection.Text}");
            Check(selection.Text!.Contains("costs a life", StringComparison.Ordinal), "the Tiles card names the water tile's kind: " + selection.Text);
            Check(typeButton.IsEnabled, "Tile Type… is enabled with a tile selected");
            ChooseTileType(window, typeButton, "Solid ground", out string intro, out List<string> offered);
            Console.WriteLine("dialog offered: " + string.Join(" | ", offered) + " — " + intro);
            Check(offered.Contains("Solid ground") && offered.Any(o => o.StartsWith("Water", StringComparison.Ordinal)), "the dialog offers solid ground and water");
            Check(intro.Contains("water", StringComparison.OrdinalIgnoreCase), "the dialog says the tile is water now: " + intro);
            Wait(() => status.Text!.StartsWith("Changed one tile to solid ground", StringComparison.Ordinal), "water tile changed: " + status.Text, 30);
            Console.WriteLine("after: " + status.Text + " / " + selection.Text);
            Check(geometry.Polygons[water].HasSurfaceBehaviorEdit && selection.Text!.Contains("solid ground", StringComparison.Ordinal), "the water tile now reads as solid ground");
            Snapshot(window, Path.Combine(outDir, "1-water-to-solid.png"));

            // 2. A grass tile becomes water (Spyro sinks). Not every tile maps 1:1 onto collision triangles, so try a few.
            string waterChoice = offered.First(o => o.StartsWith("Water", StringComparison.Ordinal) && o.Contains("sinks", StringComparison.Ordinal));
            int grass = -1;
            int tried = 0;
            foreach (int candidate in GrassCandidates(window, geometry, viewport, selection))
            {
                tried++;
                ChooseTileType(window, typeButton, waterChoice, out _, out _);
                Pump(10);
                Console.WriteLine($"grass tile {candidate} ({geometry.Polygons[candidate].RuntimeKey}): {status.Text}");
                if (status.Text!.StartsWith("Changed one tile to water", StringComparison.Ordinal)) { grass = candidate; break; }
                if (tried >= 30) break;
            }
            Check(grass >= 0, "a grass tile could be changed to water");
            Console.WriteLine("after: " + status.Text + " / " + selection.Text);
            Check(geometry.Polygons[grass].HasSurfaceBehaviorEdit, "the grass tile carries a water kind");

            // 3. Undo Terrain on the grass tile puts it back; redo the change for the export.
            Button undo = Get<Button>(window, "_terrainUndoButton");
            undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
            Wait(() => !geometry.Polygons[grass].HasSurfaceBehaviorEdit, "Undo Terrain clears the kind", 30);
            Pump(20);
            Console.WriteLine("undo: " + status.Text + " / " + selection.Text);
            Check(selection.Text!.Contains("solid ground", StringComparison.Ordinal), "after Undo Terrain the grass tile reads as solid ground again");
            viewport.SelectTerrainForTesting(grass); Pump(10);
            ChooseTileType(window, typeButton, waterChoice, out _, out _);
            Wait(() => status.Text!.StartsWith("Changed one tile to water", StringComparison.Ordinal), "grass tile changed again", 30);

            // 4. Export check and Create BIN.
            var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
            Wait(() => check.IsCompleted, "export check", 600); Pump(30);
            var problem = window.CurrentTerrainExportProblemForTesting;
            Console.WriteLine("export check: " + Find<TextBlock>(window, "ExportCheckSummary").Text?.Replace('\n', ' ') + (problem == null ? "" : " | " + problem.TechnicalMessage));
            Check(problem == null, "the export check accepts the tile type changes");
            string bin = "";
            if (createBin)
            {
                bin = RunCreateBin(window, workspace, out string createStatus);
                Console.WriteLine("create bin: " + createStatus);
                Check(bin.Length > 0, "Create BIN published a BIN: " + createStatus);
            }
            TerrainPolygon w = geometry.Polygons[water], g = geometry.Polygons[grass];
            File.WriteAllText(Path.Combine(outDir, "tile-type.json"), JsonSerializer.Serialize(new
            {
                level = levelKey,
                solidNow = new { tile = w.RuntimeKey, x = (int)w.Points.Average(p => p.X), y = (int)w.Points.Average(p => p.Y), z = (int)w.ZValues.Take(w.Points.Count).Average() },
                waterNow = new { tile = g.RuntimeKey, x = (int)g.Points.Average(p => p.X), y = (int)g.Points.Average(p => p.Y), z = (int)g.ZValues.Take(g.Points.Count).Average() },
                bin,
                cue = bin.Length == 0 ? "" : Path.ChangeExtension(bin, ".cue")
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"TILE_TYPE_UI_OK checks={_checks} water={w.RuntimeKey} grass={g.RuntimeKey} bin={bin}");
        }
        finally { window.Close(); Pump(10); }
    }

    /// <summary>Selects a textured, collision-backed tile of the given surface whose Tiles-card kind matches.</summary>
    private static int PickTile(MainWindow window, GeometryCandidate geometry, string surface, string expectedKind, EditorViewport viewport, TextBlock selection)
    {
        var collisionPoints = Get<HashSet<string>>(window, "_terrainCollisionTriangleKeys").SelectMany(k => k.Split('|')).ToHashSet(StringComparer.Ordinal);
        foreach (int i in Enumerable.Range(0, geometry.Polygons.Count))
        {
            var p = geometry.Polygons[i];
            if (p.IsTerrainRemoved || p.IsTerrainEdited || p.TextureId < 0 || p.Points.Count != 4 || !string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface), surface, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Enumerable.Range(0, 4).All(c => collisionPoints.Contains($"{MathF.Round(p.OriginalPoints[c].X)},{MathF.Round(p.OriginalPoints[c].Y)},{MathF.Round(p.OriginalZValues[c])}")))
                continue;
            if (p.OriginalZValues.Take(4).Max() - p.OriginalZValues.Take(4).Min() > 4)
                continue;
            viewport.SelectTerrainForTesting(i); Pump(10);
            if (selection.Text!.Contains(expectedKind, StringComparison.OrdinalIgnoreCase) || (expectedKind == "water" && selection.Text.Contains("costs a life", StringComparison.Ordinal)))
                return i;
        }
        throw new InvalidOperationException($"No {surface} tile with a known kind was found.");
    }

    private static IEnumerable<int> GrassCandidates(MainWindow window, GeometryCandidate geometry, EditorViewport viewport, TextBlock selection)
    {
        var collisionPoints = Get<HashSet<string>>(window, "_terrainCollisionTriangleKeys").SelectMany(k => k.Split('|')).ToHashSet(StringComparer.Ordinal);
        foreach (int i in Enumerable.Range(0, geometry.Polygons.Count))
        {
            var p = geometry.Polygons[i];
            if (p.IsTerrainRemoved || p.IsTerrainEdited || p.TextureId < 0 || p.Points.Count != 4 || !string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface), "grass", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Enumerable.Range(0, 4).All(c => collisionPoints.Contains($"{MathF.Round(p.OriginalPoints[c].X)},{MathF.Round(p.OriginalPoints[c].Y)},{MathF.Round(p.OriginalZValues[c])}")))
                continue;
            if (p.OriginalZValues.Take(4).Max() - p.OriginalZValues.Take(4).Min() > 4)
                continue;
            viewport.SelectTerrainForTesting(i); Pump(10);
            if (selection.Text!.Contains("solid ground", StringComparison.Ordinal))
                yield return i;
        }
    }

    private static void ChooseTileType(MainWindow window, Button typeButton, string choice, out string intro, out List<string> offered)
    {
        typeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, typeButton));
        Wait(() => window.OwnedWindows.Any(w => w.Name == "TileTypeDialog"), "Tile Type dialog opened", 30);
        Window dialog = window.OwnedWindows.First(w => w.Name == "TileTypeDialog");
        ListBox list = Find<ListBox>(dialog, "TileTypeList");
        offered = list.Items.OfType<ListBoxItem>().Select(item => item.Content?.ToString() ?? "").ToList();
        intro = dialog.GetLogicalDescendants().OfType<TextBlock>().First().Text ?? "";
        ListBoxItem wanted = list.Items.OfType<ListBoxItem>().First(item => string.Equals(item.Content?.ToString(), choice, StringComparison.Ordinal));
        list.SelectedItem = wanted; Pump(5);
        Button apply = Find<Button>(dialog, "TileTypeApply");
        apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
        Wait(() => !window.OwnedWindows.Any(w => w.Name == "TileTypeDialog"), "Tile Type dialog closed", 30);
        Pump(20);
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
                Check(other.Title != "Terrain Patch Check", "Create BIN must not warn about tile type changes");
                other.Close();
            }
        }
        Check(command.IsCompleted, "Create BIN finished");
        Pump(100);
        statusText = Get<TextBlock>(window, "_statusText").Text ?? "";
        string output = Path.Combine(workspace, "output");
        return Directory.Exists(output) ? Directory.GetFiles(output, "*.bin").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? "" : "";
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Tile type UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
