using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

internal static class EditorNavigationUiCases
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        string root = Directory.CreateTempSubdirectory("spyro-navigation-ui-").FullName;
        string previous = Environment.CurrentDirectory;
        string? workspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"), release = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), "{\"levels\":[]}");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            window = new MainWindow();
            Invoke(window, "ApplyFreshScene", SceneDocument.New("Navigation Test"));
            window.Show(); Pump();
            var file = Find<Button>(window, "EditorFileMenu");
            var menu = (MenuFlyout)file.Flyout!;
            var scene = window.FreshSceneForTesting!;
            string path = Path.Combine(root, SceneDocument.FileName);
            menu.ShowAt(file); Pump();
            foreach (var item in menu.Items.OfType<MenuItem>())
            {
                item.Focus();
                item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
                Pump();
            }
            Check(window.FreshSceneForTesting!.Id == scene.Id && !File.Exists(path) && !window.OwnedWindows.Any(),
                "Navigating command menu cannot create, clear, open or save a level.");
            menu.Hide(); Pump();
            var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
            geometry.Polygons[0].ApplyTerrainDeltaZ(8);
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Meta };
            window.RaiseEvent(key);
            Wait(() => !Get<bool>(window, "_saveCommandRunning"));
            Check(key.Handled && File.Exists(path), "Cmd+S is consumed before camera input and writes the actual fresh document.");
            Check(SceneDocument.Read(path).Surfaces[0].Vertices[0].Z == 552, "Shortcut saves the edited floor geometry.");
            byte[] saved = File.ReadAllBytes(path);
            var plain = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S };
            window.RaiseEvent(plain); Pump();
            Check(File.ReadAllBytes(path).SequenceEqual(saved), "Unmodified S never invokes Save.");
            geometry.Polygons[0].ApplyTerrainDeltaZ(16);
            var ctrl = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control };
            window.RaiseEvent(ctrl); Wait(() => !Get<bool>(window, "_saveCommandRunning"));
            Check(ctrl.Handled && SceneDocument.Read(path).Surfaces[0].Vertices[0].Z == 560, "Ctrl+S saves current edits too.");
            var save = Find<Button>(window, "SaveLevelButton");
            geometry.Polygons[0].ApplyTerrainDeltaZ(24);
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(() => !Get<bool>(window, "_saveCommandRunning"));
            Check(SceneDocument.Read(path).Surfaces[0].Vertices[0].Z == 568, "Visible Save uses the same real persistence path.");
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int entered = 0;
            window.FreshSaveWrittenBarrierForTesting = () => { entered++; return barrier.Task; };
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(() => entered == 1);
            Check(!save.IsEnabled && !file.IsEnabled && !Get<ComboBox>(window,"_levelJumpBox").IsEnabled && !Get<Control>(window,"_viewport").IsEnabled,
                "Pending fresh Save fences editing, navigation and duplicate commands.");
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Meta }); Pump();
            Check(entered == 1, "A repeated Save shortcut cannot enter persistence twice.");
            window.Close(); Pump();
            Check(window.IsVisible, "Closing waits for the pending save to finish.");
            barrier.SetResult(); Wait(() => !Get<bool>(window,"_saveCommandRunning"));
            window.FreshSaveWrittenBarrierForTesting = null;
            Check(save.IsEnabled && file.IsEnabled && Get<Control>(window,"_viewport").IsEnabled, "Save completion restores the editor controls.");
            // A retail picker holds an outer load fence after the inner loader
            // finishes. Save must update at both releases without another edit.
            void SetFence(string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, value);
            SetFence("_loadingLevel", true); SetFence("_handlingLevelSelection", true); SetFence("_levelSelectionAsyncInFlight", 1);
            Invoke(window, "RefreshLevelSelectionAvailability");
            Check(!save.IsEnabled, "Save is disabled while the level picker owns its load.");
            SetFence("_levelSelectionAsyncInFlight", 0);
            Invoke(window, "RefreshLevelSelectionAvailability");
            Check(!save.IsEnabled, "Finishing the inner loader cannot enable Save before the outer picker completes.");
            SetFence("_loadingLevel", false); SetFence("_handlingLevelSelection", false);
            Invoke(window, "RefreshLevelSelectionAvailability");
            Check(save.IsEnabled, "Finishing the outer picker enables Save immediately, without another edit or command.");
            byte[] beforeBusy = File.ReadAllBytes(path);
            foreach (string owner in new[] { "_createBinBusy", "_regularEditorPersistenceBusy", "_workspaceTransitionBusy", "_loadingLevel", "_freshNativeResearchBusy", "_buildSafetyBusy", "_buildingPortableCache", "_projectDataImportBusy", "_terrainTexturePaintBusy", "_terrainTextureRelocationBusy", "_terrainTexturePaintHistoryRestoreBusy", "_id65BlankLabBusy", "_nativeLevelReplacementBusy", "_handlingLevelSelection" })
            {
                var field = typeof(MainWindow).GetField(owner, BindingFlags.NonPublic | BindingFlags.Instance)!;
                field.SetValue(window, true);
                geometry.Polygons[0].ApplyTerrainDeltaZ(32);
                window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Meta }); Pump();
                Check(!Get<bool>(window,"_saveCommandRunning") && File.ReadAllBytes(path).SequenceEqual(beforeBusy), $"Fresh Save shortcut cannot bypass {owner}.");
                field.SetValue(window, false);
            }
            geometry.Polygons[0].ApplyTerrainDeltaZ(24);
            var status = Get<TextBlock>(window, "_statusText");
            status.Text = "Created all-edits test game.cue: 2 object patch(es). Exported treasure target: 250."; Pump();
            Check(Find<TextBlock>(window, "EditorStatusSummary").Text!.StartsWith("Game exported.") && status.Text.Contains("250"), "Export summary is readable while Details retains exact totals.");
            status.Text = "Created all-edits test game.cue. Skipped 1 edit: missing source."; Pump();
            Check(Find<TextBlock>(window, "EditorStatusSummary").Text == status.Text, "Partial-export warnings remain visible.");
            status.Text = "Could not create BIN: missing floor."; Pump();
            Check(Find<TextBlock>(window, "EditorStatusSummary").Text == status.Text, "Failure text is never summarized away.");
            Get<TextBlock>(window,"_buildSafetySummaryText").Text = "Blocked: review 1 edited level"; Pump();
            Check(Find<TextBlock>(window,"ExportCheckSummary").Text!.Contains("Blocked"), "Collapsed export checks retain their warning in the visible header.");
            foreach (var size in new[] { new Size(980,640), new Size(1060,720), new Size(1320,860) })
            {
                window.Width = size.Width; window.Height = size.Height;
                window.Measure(size); window.Arrange(new Rect(size)); Pump();
                foreach (var button in new[] { file, save, Get<Button>(window, "_toolbarOpenDiscImageButton"), Get<Button>(window, "_toolbarCreateBinButton"), (Button)Get<Control>(window, "_toolbarMoreControl") })
                {
                    Point? origin = button.TranslatePoint(default, window);
                    Check(origin.HasValue && origin.Value.X >= 0 && origin.Value.X + button.Bounds.Width <= size.Width && origin.Value.Y + button.Bounds.Height <= size.Height,
                        $"Toolbar action {button.Content} remains reachable at {size}.");
                }
            }
            Check(!window.GetLogicalDescendants().OfType<Button>().Any(b => b.Name == "RetailNativeMobyLibraryButton"), "Release main browser has one clear Add Object entry instead of a second technical library.");
            Console.WriteLine($"EDITOR_NAVIGATION_UI_OK checks={checks}");
        }
        finally
        {
            window?.Close(); Pump();
            Environment.CurrentDirectory = previous;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", release);
        }
    }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target,args);
    private static T Find<T>(Control root, string name) where T: Control => root.GetLogicalDescendants().OfType<T>().Distinct().Single(c => c.Name == name);
    private static void Pump() { Dispatcher.UIThread.RunJobs(); }
    private static void Wait(Func<bool> predicate) { var until = DateTime.UtcNow.AddSeconds(20); while (!predicate()) { Pump(); if(DateTime.UtcNow > until) throw new TimeoutException(); Thread.Sleep(5); } Pump(); }
}
