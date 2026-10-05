using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Keys go where the keyboard focus is. Typing in a field must never move, remove or repaint anything in
/// the level; with the view focused, Delete removes the selected object and Ctrl/Cmd+Z brings it back.
/// Runs on a disposable clone of the given project folder; nothing is saved.
/// Usage: UiSmoke --keyboard-ui &lt;project folder&gt; &lt;output folder&gt;
/// </summary>
internal static class KeyboardUiCases
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
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            LevelDefinition artisans = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("artisans")!;
            if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != "artisans")
            {
                var load = (Task)Invoke(window, "SelectLevelAsync", artisans, null)!;
                Wait(() => load.IsCompleted, "load Artisans", 300);
                WaitIdle(window);
            }
            var viewport = Get<EditorViewport>(window, "_viewport");
            var session = window.CaptureEditorShellSessionSnapshotForTesting();
            TextBox search = Get<TextBox>(window, "_mobySearchBox");
            TextBlock status = Get<TextBlock>(window, "_statusText");
            Button remove = Get<Button>(window, "_objectRemoveButton");

            // An ordinary removable object: a gem.
            Moby gem = session.CurrentMobys.First(m => !m.IsRemoved && !m.IsEditorControl && !m.IsAdded && m.TreasureValue > 0);
            SelectTab(window, "Objects");
            viewport.SelectMoby(gem, focus: false); Pump(20);
            Check(remove.IsEnabled, "the Remove button is available for the selected gem");
            string editsBefore = EditSignature(session);

            // 1. Typing in the search field: Delete, Backspace, letters, digits and undo stay in the field.
            search.Focus(); Pump(5);
            Check(search.IsFocused, "the search field has the keyboard");
            Type(window, Key.C, "c"); Type(window, Key.V, "v"); Type(window, Key.D1, "1");
            Press(window, Key.Back); Press(window, Key.Delete);
            Press(window, Key.Z, RawInputModifiers.Meta); Press(window, Key.Z, RawInputModifiers.Control);
            Press(window, Key.W); Press(window, Key.Q);
            Pump(20);
            Check(!gem.IsRemoved && EditSignature(window.CaptureEditorShellSessionSnapshotForTesting()) == editsBefore,
                "typing in a field never removes or changes anything in the level");
            Check(viewport.TerrainBrushAction == TerrainBrushAction.Off, "typing a digit in a field does not switch a terrain brush on");
            search.Text = ""; Pump(10);
            viewport.SelectMoby(gem, focus: false); Pump(10);

            // 2. With the view focused, Delete removes the selected object and Ctrl/Cmd+Z brings it back.
            //    Selecting in the view must leave the side panel where it is (the Selected object card stays
            //    on screen), and neither the removal nor the undo may move the camera.
            ListBox objectList = Get<ListBox>(window, "_mobyList");
            ScrollViewer sidePanel = objectList.GetVisualAncestors().OfType<ScrollViewer>().First();
            sidePanel.Offset = default; Pump(10);
            viewport.ResetSelection(); Pump(10);
            Moby lateGem = session.CurrentMobys.Last(m => !m.IsRemoved && !m.IsEditorControl && !m.IsAdded && m.TreasureValue > 0);
            viewport.SelectMoby(lateGem, focus: false); Pump(40);
            Check(ReferenceEquals(objectList.SelectedItem, lateGem), "selecting in the view selects the row in the list");
            Check(sidePanel.Offset.Y < 1, $"selecting in the view keeps the Selected object card on screen (panel offset {sidePanel.Offset.Y:0})");
            viewport.SelectMoby(gem, focus: false); Pump(40);
            Check(sidePanel.Offset.Y < 1, $"selecting another object keeps the panel in place (panel offset {sidePanel.Offset.Y:0})");
            string cameraBefore = Camera(viewport);
            viewport.Focus(); Pump(5);
            Check(viewport.IsFocused, "the view has the keyboard");
            Press(window, Key.Delete); Pump(40);
            Check(gem.IsRemoved, "Delete in the view removes the selected object: " + status.Text);
            Check(typeof(MainWindow).GetField("_selectedMoby", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) == null &&
                  Get<TextBlock>(window, "_selectionTitle").Text == "Nothing selected" && objectList.SelectedItem == null,
                "after a removal nothing is selected");
            Check((status.Text ?? "").StartsWith("Removed ", StringComparison.Ordinal) && status.Text!.Contains("Undo last removal", StringComparison.Ordinal),
                "the removal says what was removed and how to get it back: " + status.Text);
            int removedCount = session.CurrentMobys.Count(m => m.IsRemoved);
            viewport.Focus(); Pump(5);
            Press(window, Key.Delete); Pump(40);
            Check(session.CurrentMobys.Count(m => m.IsRemoved) == removedCount, "a second Delete removes nothing else: " + status.Text);
            Check(Camera(viewport) == cameraBefore, "removing an object does not move the camera");
            Snapshot(window, Path.Combine(outDir, "1-removed-with-delete.png"));
            viewport.Focus(); Pump(5);
            Press(window, Key.Z, OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control); Pump(60);
            Check(!gem.IsRemoved, "Ctrl/Cmd+Z in the view brings the removed object back: " + status.Text);
            Check(Camera(viewport) == cameraBefore, "bringing the object back does not move the camera: " + Camera(viewport) + " was " + cameraBefore);
            Check(sidePanel.Offset.Y < 1, $"bringing the object back keeps the panel in place (panel offset {sidePanel.Offset.Y:0})");
            Press(window, Key.Z, OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control); Pump(10);
            Check(!gem.IsRemoved && (status.Text ?? "").StartsWith("No removal to undo", StringComparison.Ordinal),
                "a second undo changes nothing and says so: " + status.Text);

            // 2b. Camera keys are bare keys: with Command or Control held they belong to shortcuts and
            //     must not move the camera; without a modifier they do.
            viewport.Focus(); Pump(5);
            string cameraAtRest = Camera(viewport);
            foreach (Key key in new[] { Key.A, Key.D, Key.W, Key.E, Key.Left })
            {
                Press(window, key, RawInputModifiers.Meta); Press(window, key, RawInputModifiers.Control); Press(window, key, RawInputModifiers.Alt);
            }
            Pump(10);
            Check(Camera(viewport) == cameraAtRest, "camera keys with Command, Control or Alt held do not move the camera");
            Press(window, Key.A); Pump(10);
            Check(Camera(viewport) != cameraAtRest, "a bare A still moves the camera");
            Press(window, Key.D); Pump(10);
            Check(Camera(viewport) == cameraAtRest, "D undoes the step A made");

            // 3. An object the game needs cannot be removed from the keyboard either.
            Moby? helper = session.CurrentMobys.FirstOrDefault(m => !m.IsRemoved && m.IsEditorControl);
            if (helper != null)
            {
                viewport.SelectMoby(helper, focus: false); Pump(20);
                viewport.Focus(); Pump(5);
                Press(window, Key.Delete); Pump(20);
                Check(!helper.IsRemoved, "Delete does not remove an object the Remove button refuses: " + status.Text);
            }

            // 4. Terrain: a bare letter does not repaint a tile; brush digits work only in the Terrain tab.
            SelectTab(window, "Terrain");
            var geometry = session.CurrentGeometry!;
            int tile = Enumerable.Range(0, geometry.Polygons.Count).First(i =>
                !geometry.Polygons[i].IsTerrainRemoved && !geometry.Polygons[i].IsTerrainEdited &&
                string.Equals(geometry.Polygons[i].Detail, "hp", StringComparison.OrdinalIgnoreCase) && geometry.Polygons[i].TextureId >= 0);
            viewport.SelectTerrainForTesting(tile); Pump(10);
            int textureBefore = geometry.Polygons[tile].TextureId;
            viewport.Focus(); Pump(5);
            Type(window, Key.C, "c");
            int other = Enumerable.Range(0, geometry.Polygons.Count).First(i => i != tile &&
                !geometry.Polygons[i].IsTerrainRemoved && !geometry.Polygons[i].IsTerrainEdited &&
                string.Equals(geometry.Polygons[i].Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                geometry.Polygons[i].TextureId >= 0 && geometry.Polygons[i].TextureId != textureBefore);
            int otherTexture = geometry.Polygons[other].TextureId;
            viewport.SelectTerrainForTesting(other); Pump(10);
            viewport.Focus(); Pump(5);
            Type(window, Key.V, "v"); Pump(30);
            Check(geometry.Polygons[other].TextureId == otherTexture && !geometry.Polygons[other].IsTerrainEdited,
                "a bare V in the view does not repaint the selected tile");
            Type(window, Key.D1, "1"); Pump(10);
            Check(viewport.TerrainBrushAction == TerrainBrushAction.Raise, "1 switches the Raise brush on in the Terrain tab");
            SelectTab(window, "Objects"); Pump(10);
            Check(viewport.TerrainBrushAction == TerrainBrushAction.Off, "leaving the Terrain tab switches the brush off");
            viewport.Focus(); Pump(5);
            Type(window, Key.D1, "1"); Pump(10);
            Check(viewport.TerrainBrushAction == TerrainBrushAction.Off, "1 does not switch a brush on outside the Terrain tab");
            Check(EditSignature(window.CaptureEditorShellSessionSnapshotForTesting()) == editsBefore,
                "after all of that the level is exactly as it was");
            Console.WriteLine($"KEYBOARD_UI_OK checks={_checks}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static string Camera(EditorViewport viewport) =>
        typeof(EditorViewport).GetField("_flyCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport)!.ToString() + " " +
        typeof(EditorViewport).GetField("_pan", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewport);

    private static string EditSignature(EditorShellSessionSnapshot session) =>
        string.Join("|", session.CurrentMobys.Select(m => $"{m.TrueIndex}:{m.IsRemoved}:{m.Position.X:F2},{m.Position.Y:F2},{m.Position.Z:F2}")) + "#" +
        string.Join("|", session.CurrentGeometry!.Polygons.Select((p, i) => p.IsTerrainEdited || p.IsTerrainRemoved ? $"{i}:{p.TextureId}:{p.IsTerrainRemoved}:{string.Join(',', p.TerrainVertexDeltas())}" : ""));

    private static void Press(MainWindow window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null); Pump(2);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null); Pump(2);
    }

    private static void Type(MainWindow window, Key key, string text)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, text); Pump(2);
        window.KeyTextInput(text); Pump(2);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, text); Pump(2);
    }

    private static void SelectTab(MainWindow window, string header)
    {
        TabControl tabs = Find<TabControl>(window, "EditorWorkspaceToolTabs");
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().First(t => (t.Header as TextBlock)?.Text == header);
        Pump(20);
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
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Keyboard UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
