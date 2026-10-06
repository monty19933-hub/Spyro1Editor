using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

/// <summary>
/// Source-bound timing of "Find space for this object…" on a disposable clone of a real project: how long the
/// search takes, what it reports, and whether the window stays responsive meanwhile.
/// Usage: UiSmoke --find-space-ui &lt;project folder&gt; &lt;output folder&gt; [object name, default "Armored Gnorc"] [level key, default artisans] [minutes, default 20]
/// </summary>
internal static class FindSpaceUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir, string objectName, string levelKey, int minutes, int stopAfterSeconds = 0)
    {
        outDir = Path.GetFullPath(outDir);
        project = Path.GetFullPath(project);
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
            Button add = Find<Button>(window, "UniversalMobyLibraryButton");
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, add));
            Wait(() => window.OwnedWindows.Any(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal)), "browser opened", 60);
            Window browser = window.OwnedWindows.Last(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal));
            ListBox list = Find<ListBox>(browser, "UniversalMobyList");
            Wait(() => list.ItemCount > 150, "catalogue loaded", 300);
            TextBox search = Find<TextBox>(browser, "UniversalMobySearch");
            search.Text = objectName; Pump(30);
            var entry = list.Items.OfType<UniversalMobyCatalogEntry>().FirstOrDefault(e => string.Equals(e.Name, objectName, StringComparison.OrdinalIgnoreCase))
                ?? list.Items.OfType<UniversalMobyCatalogEntry>().FirstOrDefault();
            Check(entry != null, "an object named " + objectName + " is in the catalogue");
            list.SelectedItem = entry; Pump(30);
            TextBlock fit = Find<TextBlock>(browser, "UniversalMobyFitText");
            Button findRoom = Find<Button>(browser, "UniversalMobyFindRoom");
            Wait(() => findRoom.IsEnabled || !(fit.Text ?? "").Contains("Checking", StringComparison.Ordinal), "fit check finished", 600);
            Console.WriteLine("fit text: " + (fit.Text ?? "").Replace('\n', ' '));
            Console.WriteLine("Find space enabled: " + findRoom.IsEnabled);
            TextBlock explanation = Find<TextBlock>(browser, "UniversalMobyRoomExplanation");
            TextBlock technical = Find<TextBlock>(browser, "UniversalMobyTechnical");
            var clock = Stopwatch.StartNew();
            findRoom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, findRoom));
            string last = "";
            double nextReport = 10;
            long maxGap = 0;
            bool stopPressed = false;
            while (clock.Elapsed < TimeSpan.FromMinutes(minutes))
            {
                var tick = Stopwatch.StartNew();
                Dispatcher.UIThread.RunJobs(); Thread.Sleep(50);
                maxGap = Math.Max(maxGap, tick.ElapsedMilliseconds - 50);
                if (stopAfterSeconds > 0 && !stopPressed && clock.Elapsed.TotalSeconds >= stopAfterSeconds)
                {
                    Check(findRoom.IsEnabled && (findRoom.Content as string) == "Stop searching", "while searching, the button offers Stop searching");
                    Console.WriteLine($"[{clock.Elapsed.TotalSeconds:0}s] pressing Stop searching");
                    findRoom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, findRoom)); stopPressed = true;
                    var stopClock = Stopwatch.StartNew();
                    while (!(findRoom.IsEnabled && (findRoom.Content as string) != "Stop searching") && stopClock.Elapsed < TimeSpan.FromSeconds(120)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(50); }
                    Console.WriteLine($"[{clock.Elapsed.TotalSeconds:0}s] stopped within {stopClock.Elapsed.TotalSeconds:0.0} s; explanation: {(explanation.Text ?? "").Replace('\n', ' ')}");
                    Check((explanation.Text ?? "").StartsWith("Stopped after", StringComparison.Ordinal) || (explanation.Text ?? "").Contains("found", StringComparison.Ordinal), "the stop is reported");
                    break;
                }
                string text = explanation.Text ?? "";
                if (text != last)
                {
                    Console.WriteLine($"[{clock.Elapsed.TotalSeconds:0}s] explanation: {text.Replace('\n', ' ')}");
                    last = text;
                }
                if (clock.Elapsed.TotalSeconds >= nextReport)
                {
                    Console.WriteLine($"[{clock.Elapsed.TotalSeconds:0}s] still searching; Find space enabled={findRoom.IsEnabled}; longest UI stall so far {maxGap} ms");
                    nextReport += 30;
                }
                if (findRoom.IsEnabled && (findRoom.Content as string) != "Stop searching" && clock.Elapsed.TotalSeconds > 1) break;
            }
            Pump(20);
            Console.WriteLine("final explanation: " + (explanation.Text ?? "").Replace('\n', ' '));
            Check(maxGap < 1500, $"the window stayed responsive (longest stall {maxGap} ms)");
            Console.WriteLine($"FIND_SPACE_UI_DONE seconds={clock.Elapsed.TotalSeconds:0} finished={findRoom.IsEnabled} longestStallMs={maxGap} checks={_checks}");
            Console.WriteLine("technical: " + string.Join(" | ", (technical.Text ?? "").Split('\n').Where(l => l.Contains("Space search", StringComparison.Ordinal) || l.Contains(" T", StringComparison.Ordinal)).Take(6)));
            ListBox plans = Find<ListBox>(browser, "UniversalMobyRemovalPlans");
            Console.WriteLine("plans visible: " + plans.IsVisible + " count: " + plans.ItemCount);
            Snapshot(browser, Path.Combine(outDir, "find-space.png"));
            browser.Close(); Pump(20);
        }
        finally { window.Close(); Pump(10); }
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Find space UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
