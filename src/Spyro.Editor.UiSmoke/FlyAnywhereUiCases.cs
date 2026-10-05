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
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

/// <summary>
/// Fly Anywhere in the shipping editor, on a disposable clone of a real project: off by default, one tick
/// saves it, a reopened editor shows it, Create BIN writes a game with the flight change, and unticking it
/// and exporting again gives a game whose movement code is the original.
/// Usage: UiSmoke --fly-anywhere-ui &lt;project folder&gt; &lt;output folder&gt; [--settings-only]
/// </summary>
internal static class FlyAnywhereUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, bool settingsOnly)
    {
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        if (clone.ExitCode != 0 || !Directory.Exists(workspace)) throw new IOException("Could not clone the project folder.");
        string settingsPath = Path.Combine(workspace, NativeFreeFlightSettingsStore.FileName);
        if (File.Exists(settingsPath)) File.Delete(settingsPath);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "logs"));
        Environment.CurrentDirectory = workspace;

        // 1. Off by default; one tick saves it.
        MainWindow window = Open();
        string sourceImage;
        try
        {
            CheckBox box = Find<CheckBox>(window, "FreeFlightEnabled");
            Check(Find<Control>(window, "FlyAnywhereSection").GetLogicalDescendants().Contains(box), "Fly Anywhere lives in Game Settings");
            Check(box.IsChecked == false && box.IsEnabled && !File.Exists(settingsPath), "Fly Anywhere starts off and nothing is saved for it");
            Check(!window.GetLogicalDescendants().OfType<Button>().Any(b => b.Name is "FreeFlightSave" or "FreeFlightRevert"),
                "there is no separate Save Flight or Revert button to forget");
            TextBlock details = Find<TextBlock>(window, "FreeFlightDetails");
            Check(details.Text!.StartsWith("Off.", StringComparison.Ordinal) && details.Text.Contains("Game Camera", StringComparison.Ordinal),
                "the note says it is off and that the editor camera is something else: " + details.Text);
            SelectTab(window, "Game Settings");
            Snapshot(window, Path.Combine(outDir, "1-fly-anywhere-off.png"));
            box.IsChecked = true;
            Wait(() => File.Exists(settingsPath) && NativeFreeFlightSettingsStore.Capture(workspace).GlobalEnabled, "ticking the box saves the choice", 30);
            Wait(() => details.Text!.StartsWith("On.", StringComparison.Ordinal), "the note says it is on", 10);
            Check(Get<TextBlock>(window, "_statusText").Text!.Contains("Fly Anywhere is on", StringComparison.Ordinal), "the status line confirms it");
            Snapshot(window, Path.Combine(outDir, "2-fly-anywhere-on.png"));
            sourceImage = (string)Invoke(window, "ResolveUniversalSourceDisc")!;
        }
        finally { window.Close(); Pump(10); }

        // 2. A reopened editor shows the saved choice.
        window = Open();
        try
        {
            CheckBox box = Find<CheckBox>(window, "FreeFlightEnabled");
            Check(box.IsChecked == true, "a reopened editor shows Fly Anywhere on");
            if (settingsOnly)
            {
                box.IsChecked = false;
                Wait(() => !NativeFreeFlightSettingsStore.Capture(workspace).GlobalEnabled, "unticking the box saves the choice", 30);
                Console.WriteLine($"FLY_ANYWHERE_UI_OK checks={_checks} exports=0");
                return;
            }

            // 3. Create BIN with it on.
            FixTerrainProblemIfAny(window);
            string output = Path.Combine(workspace, "output");
            RunCreateBin(window, out string onStatus);
            string[] bins = Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            Check(bins.Length == 1, "Create BIN with Fly Anywhere on publishes one BIN: " + onStatus);
            // Off the UI thread: the capture awaits, and its continuations must not wait for this blocked thread.
            var plan = Task.Run(() => NativeFreeFlightCompositionPlan.CaptureAsync(sourceImage, true)).GetAwaiter().GetResult();
            byte[] onExe = UniversalDiscImage.Read(bins[0]).Exe;
            byte[] reverted = plan.ExecutablePatch.RevertExecutable(onExe);
            Check(!reverted.AsSpan().SequenceEqual(onExe), "the exported game contains the flight change");
            Check(Details(window).Contains("Fly Anywhere is on", StringComparison.Ordinal), "the export summary says Fly Anywhere is on: " + Details(window));
            string onCopy = Path.Combine(outDir, "fly-on.bin");
            File.Copy(bins[0], onCopy, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(onCopy, ".cue"), "FILE \"fly-on.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            Snapshot(window, Path.Combine(outDir, "3-exported-on.png"));

            // 4. Turn it off and export again: the movement code is the original again.
            box.IsChecked = false;
            Wait(() => !NativeFreeFlightSettingsStore.Capture(workspace).GlobalEnabled, "unticking the box saves the choice", 30);
            RunCreateBin(window, out string offStatus);
            bins = Directory.GetFiles(output, "*.bin");
            Check(bins.Length == 1, "Create BIN with Fly Anywhere off publishes one BIN: " + offStatus);
            byte[] offExe = UniversalDiscImage.Read(bins[0]).Exe;
            var offPlan = Task.Run(() => NativeFreeFlightCompositionPlan.CaptureAsync(sourceImage, false)).GetAwaiter().GetResult();
            _ = offPlan.ExecutablePatch.ApplyToExecutable(offExe); // throws unless every flight window holds its original bytes
            Check(!offExe.AsSpan().SequenceEqual(onExe), "the second export differs from the flight one");
            Check(Details(window).Contains("Fly Anywhere is off", StringComparison.Ordinal), "the export summary says Fly Anywhere is off: " + Details(window));
            string offCopy = Path.Combine(outDir, "fly-off.bin");
            File.Copy(bins[0], offCopy, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(offCopy, ".cue"), "FILE \"fly-off.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            Console.WriteLine($"FLY_ANYWHERE_UI_OK checks={_checks} exports=2 on={Sha(onCopy)} off={Sha(offCopy)}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static string Details(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static string Sha(string path) { using var s = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(s)); }

    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        WaitIdle(window);
        return window;
    }

    private static void SelectTab(MainWindow window, string header)
    {
        TabControl tabs = Find<TabControl>(window, "EditorWorkspaceToolTabs");
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == header);
        Pump(20);
    }

    private static void FixTerrainProblemIfAny(MainWindow window)
    {
        var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
        Wait(() => check.IsCompleted, "export check", 600);
        if (window.CurrentTerrainExportProblemForTesting == null) return;
        Button undo = Find<Button>(window, "TerrainProblemUndo");
        undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
        Wait(() => window.CurrentTerrainExportProblemForTesting == null, "Undo that change clears the terrain problem", 60);
        var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => save.IsCompleted && save.Result, "save after undo", 120);
        Pump(50);
    }

    private static void RunCreateBin(MainWindow window, out string statusText)
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
                Console.WriteLine("fly anywhere: dialog '" + other.Title + "': " +
                    string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                other.Close();
            }
        }
        Check(command.IsCompleted, "Create BIN finished");
        Pump(100);
        statusText = Get<TextBlock>(window, "_statusText").Text ?? "";
        Console.WriteLine("fly anywhere: Create BIN status: " + statusText);
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Fly Anywhere UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
