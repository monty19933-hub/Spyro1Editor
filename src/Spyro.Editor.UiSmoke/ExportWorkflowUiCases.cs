using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

/// <summary>
/// Source-bound Create BIN workflow on a disposable clone of a real project: a refused export must leave no
/// output and no working copies and must point at the terrain to fix; after the fix the export must publish
/// a BIN/CUE and again leave no working copies.
/// Usage: UiSmoke --export-workflow-ui &lt;project folder&gt; &lt;output folder&gt;
/// </summary>
internal static class ExportWorkflowUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        Clone(project, workspace);
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
            LevelDefinition artisans = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("artisans")!;
            if (!ReferenceEquals(window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel, artisans))
            {
                var load = (Task)Invoke(window, "SelectLevelAsync", artisans, null)!;
                Wait(() => load.IsCompleted, "load Artisans", 300);
            }
            WaitIdle(window);
            string output = Path.Combine(workspace, "output");
            string[] Published() => Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            string scratch = ExportScratchSpace.AggregateRoot(workspace);
            int Staged() => Directory.Exists(scratch) ? Directory.GetDirectories(scratch).Length : 0;
            TextBlock status = Get<TextBlock>(window, "_statusText");

            // 1. With the folded corner still saved, Create BIN must refuse cleanly.
            string terrainFile = Path.Combine(workspace, "artisans-terrain-edits.json");
            byte[]? refusingTerrain = File.Exists(terrainFile) ? File.ReadAllBytes(terrainFile) : null;
            bool hadProblem = RunCreateBin(window, out string firstStatus);
            bool refusedFirst = window.CurrentTerrainExportProblemForTesting != null;
            if (window.CurrentTerrainExportProblemForTesting != null)
            {
                Check(Published().Length == 0, "a refused export publishes no BIN");
                Check(Staged() == 0, "a refused export leaves no working copies behind");
                Check(firstStatus.StartsWith("Could not create BIN", StringComparison.Ordinal) && Find<Border>(window, "TerrainProblemBar").IsVisible,
                    "the refusal names the terrain problem and raises the red bar: " + firstStatus);
                Snapshot(window, Path.Combine(outDir, "1-export-refused.png"));
                Button undo = Find<Button>(window, "TerrainProblemUndo");
                undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
                Wait(() => window.CurrentTerrainExportProblemForTesting == null, "Undo that change clears the problem", 60);
                var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
                Wait(() => save.IsCompleted, "save after undo", 120);
                Check(save.Result, "save after undo succeeded");
                Pump(50);
            }
            else Console.WriteLine("export workflow: project had no terrain problem; first export status: " + firstStatus + " hadDialog=" + hadProblem);

            // 2. The fixed project exports, and cleans up after itself.
            if (Published().Length == 0)
            {
                RunCreateBin(window, out string secondStatus);
                Check(secondStatus.StartsWith("Created ", StringComparison.Ordinal), "Create BIN reports the created CUE: " + secondStatus);
            }
            string bin = Published().Single();
            string cue = Path.ChangeExtension(bin, ".cue");
            Check(File.Exists(cue) && new FileInfo(bin).Length > 600_000_000, "a full-size BIN and its CUE were published");
            Check(File.ReadAllText(cue).Contains(Path.GetFileName(bin), StringComparison.Ordinal), "the CUE names the published BIN");
            Check(Staged() == 0, "a successful export leaves no working copies behind");
            Check(!Directory.GetFiles(output, "*.pending-*").Any(), "no pending files remain beside the output");
            Snapshot(window, Path.Combine(outDir, "2-export-created.png"));
            string hash = Sha(bin);
            string cueBefore = File.ReadAllText(cue);

            // 3. A later export that fails must leave the last good game untouched.
            if (refusedFirst && refusingTerrain != null)
            {
                window.Close(); Pump(10);
                File.WriteAllBytes(terrainFile, refusingTerrain);
                window = new MainWindow { Width = 1440, Height = 900 };
                window.Show();
                WaitIdle(window);
                artisans = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("artisans")!;
                if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != "artisans")
                {
                    var load = (Task)Invoke(window, "SelectLevelAsync", artisans, null)!;
                    Wait(() => load.IsCompleted, "reload Artisans", 300);
                    WaitIdle(window);
                }
                RunCreateBin(window, out string failedStatus);
                Check(failedStatus.StartsWith("Could not create BIN", StringComparison.Ordinal),
                    "the failed export says so: " + failedStatus);
                Check(Published().Length == 1 && Sha(bin) == hash && File.ReadAllText(cue) == cueBefore,
                    "the last good BIN and CUE are byte-for-byte unchanged after a failed export");
                Check(Staged() == 0 && !Directory.GetFiles(output, "*.pending-*").Any(),
                    "the failed export leaves no working copies or pending files");
                Snapshot(window, Path.Combine(outDir, "3-later-export-refused.png"));
            }
            Console.WriteLine($"EXPORT_WORKFLOW_UI_OK checks={_checks} bin={bin} sha256={hash} status={Get<TextBlock>(window, "_statusText").Text}");
        }
        finally
        {
            window.Close(); Pump(10);
        }
    }

    private static string Sha(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Presses Create BIN, confirms the scope dialog when it appears and waits for the command to end.</summary>
    private static bool RunCreateBin(MainWindow window, out string statusText)
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
                // Any other dialog (export checks, warnings) is recorded and closed so the command can finish.
                Console.WriteLine("export workflow: dialog '" + other.Title + "': " +
                    string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                other.Close();
            }
        }
        Check(command.IsCompleted, "Create BIN finished");
        Pump(100);
        statusText = Get<TextBlock>(window, "_statusText").Text ?? "";
        return confirmed;
    }

    private static void Clone(string source, string target)
    {
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", source, target]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode != 0 || !Directory.Exists(target)) throw new IOException("Could not clone the project folder.");
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Export workflow UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
