using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Text;

/// <summary><c>--clear-project-desktop &lt;output&gt; &lt;usa disc&gt;</c>: File ▾ › Clear Entire Project to Base Game… with REAL
/// windows. The other tests of the command run on a windowless test platform; this one starts the editor the way the
/// packaged app does (the computer's own window system, the app's own lifetime) and drives the command from inside:
/// the question is a real window, Cancel leaves the editor's window in place, "Yes, I am sure" replaces it by a new
/// window in the same place, the app keeps running with exactly that one window, and closing it ends the app. Not in
/// the gate (it puts windows on the screen for about half a minute); no mouse or keyboard is used.</summary>
internal static class ClearProjectDesktopCase
{
    internal static int Run(string outDir, string disc)
    {
        if (!File.Exists(disc)) throw new ArgumentException("The test needs the original disc image.");
        Directory.CreateDirectory(outDir);
        var (workspace, keptRoot, _, _, _, _, _) = ClearProjectUiCases.PrepareProject(outDir, disc);
        int checks = 0;
        string? failure = null;
        bool finished = false;
        void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Clear project (real windows): " + label);
            Console.WriteLine("ok   " + label);
        }

        async Task Scenario()
        {
            var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                MainWindow window = (MainWindow)desktop.MainWindow!;
                await Idle(window);
                window.OpenPathOverrideForTesting = _ => { };
                Check(window.IsVisible && window.PlatformImpl != null && desktop.Windows.Count == 1 &&
                      !window.PlatformImpl.GetType().FullName!.Contains("Headless", StringComparison.OrdinalIgnoreCase),
                    "the editor is open in a real window (" + window.PlatformImpl.GetType().Name + ")");
                var catalog = Get<LevelCatalog>(window, "_retailCatalog");
                await LevelTextEditStore.SaveAsync(workspace, TextTargetCatalog.CreateDefault().FindForLevel(catalog.FindByKey("artisans")!)!, "ARTISAN HOME");
                SpyroMovesSettings.SaveHover(workspace, true);
                Get<GeometryCandidate>(window, "_currentGeometry").Polygons[0].ApplyTerrainDeltaZ(8);   // an unsaved change
                PixelPoint place = window.Position;
                Size size = window.ClientSize;

                // Cancel: the question is a real window of its own; the editor's window stays.
                Task command = window.ClearEntireProjectForTestingAsync();
                Window ask = await For(() => window.OwnedWindows.FirstOrDefault(w => w.Name == "ClearProjectDialog"), "the question");
                await Task.Delay(400);
                Check(ask.IsVisible && ask.PlatformImpl != null && Find<Button>(ask, "ClearProjectConfirm").Content as string == "Yes, I am sure",
                    "the question is a real window with \"Yes, I am sure\"");
                Find<Button>(ask, "ClearProjectCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await command;
                await Task.Delay(300);
                Check(window.IsVisible && window.ReopenedForTesting == null && ReferenceEquals(desktop.MainWindow, window) && desktop.Windows.Count == 1 &&
                      !Directory.Exists(keptRoot) && SpyroMovesSettings.LoadHover(workspace), "Cancel leaves the window and the project as they were");

                // "Yes, I am sure": a new window takes the old one's place and the app goes on with exactly that one.
                command = window.ClearEntireProjectForTestingAsync();
                ask = await For(() => window.OwnedWindows.FirstOrDefault(w => w.Name == "ClearProjectDialog"), "the question");
                await Task.Delay(400);
                Find<Button>(ask, "ClearProjectConfirm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await command;
                MainWindow reopened = window.ReopenedForTesting ?? throw new InvalidOperationException("Clear project (real windows): no new window: " + Status(window));
                await Idle(reopened);
                await Task.Delay(1500);   // long enough for a lifetime that meant to end the app to have ended it
                Check(ReferenceEquals(desktop.MainWindow, reopened) && reopened.IsVisible && reopened.PlatformImpl != null && !window.IsVisible && window.PlatformImpl == null &&
                      desktop.Windows.Count == 1 && ReferenceEquals(desktop.Windows[0], reopened),
                    "the new window is the app's main and only window, the old one is gone, and the app is still running");
                Check(Math.Abs(reopened.Position.X - place.X) <= 2 && Math.Abs(reopened.Position.Y - place.Y) <= 2 &&
                      Math.Abs(reopened.ClientSize.Width - size.Width) <= 2 && Math.Abs(reopened.ClientSize.Height - size.Height) <= 2,
                    $"it opens where the old window was, at its size ({reopened.Position} {reopened.ClientSize}; was {place} {size})");
                var shown = reopened.CaptureEditorShellSessionSnapshotForTesting();
                string kept = Directory.GetDirectories(keptRoot).Single();
                Check(Status(reopened).StartsWith("Your project is back to the original Spyro game.", StringComparison.Ordinal) && Status(reopened).Contains(kept, StringComparison.Ordinal) &&
                      shown.CurrentLevel?.Key == "artisans" && shown.CurrentGeometry != null && shown.CurrentGeometry.Polygons.All(p => !p.HasHeightEdit) &&
                      !SpyroMovesSettings.LoadHover(workspace) && File.Exists(Path.Combine(kept, "spyro-moves.json")) && File.Exists(Path.Combine(kept, "artisans-level-text-edit-plan.json")) &&
                      ProjectClear.WhatWouldGo(workspace).Count == 0,
                    "it shows the original Artisans and says where the work went; the work is in the dated folder and not in the project");
                finished = true;
                reopened.Close();   // the last window: the app must end by itself
            }
            catch (Exception e)
            {
                failure = e.ToString();
                desktop.Shutdown(1);
            }
        }

        var watchdog = new System.Threading.Timer(_ =>
        {
            failure ??= "The editor did not finish within five minutes.";
            Dispatcher.UIThread.Post(() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(2));
        }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        var clock = Stopwatch.StartNew();
        int exit = AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()
            .AfterSetup(builder => Dispatcher.UIThread.Post(() => { Task running = Scenario(); }, DispatcherPriority.Background))
            .StartWithClassicDesktopLifetime([]);
        watchdog.Dispose();
        if (failure != null || !finished)
        {
            Console.Error.WriteLine(failure ?? "The editor ended before the test finished.");
            return 1;
        }
        checks++;
        Console.WriteLine($"ok   closing the new window ends the app by itself (exit {exit}, {clock.Elapsed.TotalSeconds:F0} s in all)");
        Console.WriteLine($"CLEAR_PROJECT_DESKTOP_OK checks={checks}");
        return 0;
    }

    private static async Task Idle(MainWindow window)
    {
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(240))
        {
            await Task.Delay(50);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") || Get<int>(window, "_levelSelectionAsyncInFlight") > 0 ||
                        snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }

    private static async Task<T> For<T>(Func<T?> find, string what) where T : class
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (find() is { } found) return found;
            await Task.Delay(50);
        }
        throw new TimeoutException(what + " did not appear.");
    }

    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
}
