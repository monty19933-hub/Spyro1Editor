using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;

/// <summary>
/// File &gt; New Level… in a project that already has a created level must ask first, change nothing on
/// Cancel, and keep a byte-identical copy before the new level can replace the saved one.
/// Runs on a disposable clone of the given project folder.
/// Usage: UiSmoke --new-level-confirm-ui &lt;project folder&gt; &lt;output folder&gt;
/// </summary>
internal static class NewLevelConfirmUiCases
{
    private static int _checks;

    internal static void Run(string project, string outDir)
    {
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
        string scenePath = SceneDocumentStore.PathForTarget(workspace, "authored65");
        string backups = Path.Combine(workspace, "backups");
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            Button file = Find<Button>(window, "EditorFileMenu");
            MenuItem newLevel = ((MenuFlyout)file.Flyout!).Items.OfType<MenuItem>().Single(i => i.Header?.ToString() == "New Level…");
            Button save = Find<Button>(window, "SaveLevelButton");
            TextBlock status = Get<TextBlock>(window, "_statusText");

            if (!File.Exists(scenePath))
            {
                // No created level yet: New Level… must not ask anything.
                newLevel.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, newLevel));
                Wait(() => window.FreshSceneForTesting != null, "first New Level opens without a question", 30);
                Check(!window.OwnedWindows.Any(), "no dialog when the project has no created level");
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, save));
                Wait(() => File.Exists(scenePath), "the first created level is saved", 60);
                Pump(50);
            }
            byte[] savedBefore = File.ReadAllBytes(scenePath);
            string savedName = SceneDocument.Read(scenePath).Name;

            // Cancel: nothing changes.
            string? openBefore = window.FreshSceneForTesting?.Id;
            string? levelBefore = window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key;
            newLevel.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, newLevel));
            Wait(() => window.OwnedWindows.Any(w => w.Name == "NewLevelReplaceDialog"), "New Level asks before replacing a created level", 30);
            Window dialog = window.OwnedWindows.Single(w => w.Name == "NewLevelReplaceDialog");
            string text = string.Join(" ", dialog.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Check(text.Contains(savedName, StringComparison.Ordinal) && text.Contains("backups", StringComparison.Ordinal),
                "the question names the level and says where the copy goes: " + text);
            Snapshot(dialog, Path.Combine(outDir, "1-new-level-question.png"));
            Button cancel = Find<Button>(dialog, "ConfirmationCancel");
            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, cancel));
            Wait(() => !window.OwnedWindows.Any(), "Cancel closes the question", 30);
            Pump(30);
            Check(window.FreshSceneForTesting?.Id == openBefore &&
                  window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key == levelBefore,
                "Cancel leaves the open level alone");
            Check(File.ReadAllBytes(scenePath).AsSpan().SequenceEqual(savedBefore) && !Directory.Exists(backups),
                "Cancel leaves the saved level alone and makes no copy");

            // Confirm: a copy is kept, the new level opens, the saved file is replaced only by a later save.
            newLevel.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, newLevel));
            Wait(() => window.OwnedWindows.Any(w => w.Name == "NewLevelReplaceDialog"), "New Level asks again", 30);
            dialog = window.OwnedWindows.Single(w => w.Name == "NewLevelReplaceDialog");
            Button confirm = Find<Button>(dialog, "ConfirmationConfirm");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, confirm));
            Wait(() => !window.OwnedWindows.Any() && window.FreshSceneForTesting != null && window.FreshSceneForTesting.Id != openBefore,
                "Start New Level opens a new level", 30);
            string[] copies = Directory.Exists(backups) ? Directory.GetFiles(backups, "created-level-*.scene.json") : [];
            Check(copies.Length == 1 && File.ReadAllBytes(copies[0]).AsSpan().SequenceEqual(savedBefore),
                "one byte-identical copy of the previous created level is kept");
            Check(File.ReadAllBytes(scenePath).AsSpan().SequenceEqual(savedBefore), "the saved level is still the previous one until Save");
            Check((status.Text ?? "").Contains("backups/", StringComparison.Ordinal), "the status line says where the copy is: " + status.Text);
            Check(SceneDocument.Read(copies[0]).Name == savedName, "the copy opens as a valid level");
            Snapshot(window, Path.Combine(outDir, "2-new-level-started.png"));
            Console.WriteLine($"NEW_LEVEL_CONFIRM_UI_OK checks={_checks} copy={Path.GetFileName(copies[0])}");
        }
        finally
        {
            window.Close(); Pump(10);
        }
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("New Level confirm UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window); image?.Save(path); }
}
