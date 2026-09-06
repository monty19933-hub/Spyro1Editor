using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;

internal static class NativeFreshDocumentUiCases
{
    internal static void Run(string outputDirectory)
    {
        int checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-fresh-document-ui-").FullName;
        var project = new EditorProjectLayout(root, "fresh-ui");
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"fresh-ui\",\"displayName\":\"fresh UI sentinel\"}");
        string legacy = Path.Combine(root, "unusedlevel65blank-authoring-document-v1.json");
        string source = Path.Combine(root, "source-disc.json");
        File.WriteAllText(legacy, "old Lab bytes must remain untouched");
        File.WriteAllText(source, "source bytes must remain untouched");
        var sentinels = new[] { project.ManifestPath, legacy, source }.ToDictionary(p => p, File.ReadAllBytes);
        string path = Path.Combine(root, NativeId65FreshDocumentStore.FileName);
        NativeFreshDocumentWindow? window = null;
        try
        {
            Check(MainWindow.IsNativeFreshDocumentProjectCurrent(project, new(root, "fresh-ui"), root), "same root and project ID remains current");
            Check(!MainWindow.IsNativeFreshDocumentProjectCurrent(project, new(root, "different-id"), root), "same root with different project identity refuses");
            Check(!MainWindow.IsNativeFreshDocumentProjectCurrent(project, new(Path.Combine(root, "other"), "fresh-ui"), root) &&
                !MainWindow.IsNativeFreshDocumentProjectCurrent(project, project, Path.Combine(root, "other")) &&
                !MainWindow.IsNativeFreshDocumentProjectCurrent(project, null, root), "root/workspace/missing-context mismatch refuses");
            bool currentProject = true;
            window = new NativeFreshDocumentWindow(project, () => currentProject);
            window.Show();
            Pump();
            Check(window.CurrentIntent == null && !window.HasUnsavedChanges && !File.Exists(path), "absent open offers no implicit defaults or write");
            Check(Find<Button>(window, "FreshDocumentNew").IsEnabled && !Find<Button>(window, "FreshDocumentSave").IsEnabled,
                "explicit starter required before editing/saving");
            Check(Find<TextBlock>(window, "FreshDocumentCapability").Text!.Contains("Normal Create BIN is unavailable", StringComparison.Ordinal), "unsupported output is explicit");
            Check(window.GetLogicalDescendants().OfType<Button>().All(b => !string.Equals(b.Content?.ToString(), "Create BIN", StringComparison.Ordinal)), "no misleading output action");
            Click("FreshDocumentNew");
            Check(window.CurrentIntent?.FloorX == 8448 && window.CurrentIntent.SpawnZ == 9216 && window.HasUnsavedChanges,
                "explicit starter creates only unsaved neutral intent");
            Check(ReferenceEquals(window.CurrentIntent, window.Preview.Intent), "preview and document use same immutable intent");
            Check(!File.Exists(path), "new/apply/undo do not persist implicitly");
            Check(Find<ComboBox>(window, "FreshDocumentMusic").ItemsSource!.Cast<MusicTrackEntry>().Count() == 46, "all and only selectable native tracks");
            Click("FreshDocumentUndo");
            Check(window.CurrentIntent == null && !window.HasUnsavedChanges && Find<Button>(window, "FreshDocumentNew").IsEnabled,
                "creation can be undone before first save");
            Click("FreshDocumentRedo");
            Check(window.CurrentIntent != null && window.HasUnsavedChanges, "creation redo remains available before save");
            Click("FreshDocumentSave");
            byte[] baseline = File.ReadAllBytes(path);
            Check(!window.HasUnsavedChanges && NativeId65FreshDocumentStore.Capture(project).Intent?.MusicTrackId == 19, "explicit save returns actual reopened baseline");
            Check(!Find<Button>(window, "FreshDocumentUndo").IsEnabled && !Find<Button>(window, "FreshDocumentNew").IsEnabled,
                "first save prunes absence history; Undo cannot imply unsupported document deletion");
            Click("FreshDocumentReopen");
            Check(window.CurrentIntent != null && !window.HasUnsavedChanges, "creation save/reopen stays an existing editable document");

            Text("FreshFloorX", "8464");
            Text("FreshFloorZ", "8720");
            Text("FreshFloorWidth", "1536");
            Text("FreshFloorDepth", "512");
            Text("FreshSpawnX", "9000");
            Text("FreshSpawnY", "8700");
            Text("FreshHeading", "128");
            Text("FreshFloorColor", "#C05080");
            Text("FreshSkyColor", "#203060");
            Find<ComboBox>(window, "FreshDocumentMusic").SelectedItem = MusicTrackCatalog.Find(20);
            Check(window.CurrentIntent!.FloorX == 8448 && window.HasUnappliedFields, "unapplied edits cannot silently alter the preview");
            Click("FreshDocumentApply");
            var changed = window.CurrentIntent!;
            Check(changed.FloorX == 8464 && changed.FloorZ == 8720 && changed.FloorWidth == 1536 && changed.FloorDepth == 512 &&
                changed.SpawnX == 9000 && changed.SpawnY == 8700 && changed.SpawnZ == 9232 && changed.Heading == 128 && changed.MusicTrackId == 20 &&
                changed.FloorColor.R == 192 && changed.SkyColor.B == 96, "all form values become actual intent");
            Check(ReferenceEquals(window.Preview.Intent, changed) && window.Preview.Intent!.FloorWidth == 1536, "same changed intent drives schematic");
            Check(File.ReadAllBytes(path).SequenceEqual(baseline), "apply is not save");
            Click("FreshDocumentUndo");
            Check(NativeId65FreshDocumentCodec.SerializeCanonical(window.CurrentIntent!).SequenceEqual(baseline) && !window.HasUnsavedChanges, "undo restores saved semantic state");
            Click("FreshDocumentRedo");
            Check(ReferenceEquals(window.CurrentIntent, changed) && window.HasUnsavedChanges, "redo restores exact immutable applied intent");
            Snapshot(window, "fresh-native-document-edited.png");

            Text("FreshFloorWidth", "17");
            Click("FreshDocumentApply");
            Check(ReferenceEquals(window.CurrentIntent, changed) && window.HasUnappliedFields &&
                Find<TextBlock>(window, "FreshDocumentStatus").Text!.Contains("not applied", StringComparison.OrdinalIgnoreCase), "invalid native grammar refuses atomically");
            Click("FreshDocumentSave");
            Check(File.ReadAllBytes(path).SequenceEqual(baseline) && Find<TextBox>(window, "FreshFloorWidth").Text == "17", "invalid save retains raw field text and old file");
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Cancel);
            window.Close();
            Pump();
            Check(window.IsVisible && Find<TextBox>(window, "FreshFloorWidth").Text == "17", "window-manager Close cancellation retains invalid draft");
            Click("FreshDocumentReopen");
            Check(window.IsVisible && Find<TextBox>(window, "FreshFloorWidth").Text == "17", "Reopen cancellation retains invalid draft");
            var pendingDecision = new TaskCompletionSource<FreshDocumentDecision>();
            int decisionCalls = 0;
            window.DecisionForTesting = _ => { decisionCalls++; return pendingDecision.Task; };
            window.Close();
            window.Close();
            Pump();
            Check(window.IsVisible && decisionCalls == 1 && !Find<Button>(window, "FreshDocumentSave").IsEnabled,
                "duplicate manager Close shares one pending decision and disables document actions");
            pendingDecision.SetResult(FreshDocumentDecision.Cancel);
            Pump();
            Check(window.IsVisible && Find<TextBox>(window, "FreshFloorWidth").Text == "17" && Find<Button>(window, "FreshDocumentSave").IsEnabled,
                "async cancellation restores actions without losing invalid text");
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Save);
            window.Close();
            Pump();
            Check(window.IsVisible && File.ReadAllBytes(path).SequenceEqual(baseline), "dirty Close Save cannot bypass validation");
            Click("FreshDocumentUndo");
            Check(!window.HasUnappliedFields && ReferenceEquals(window.CurrentIntent, changed), "first undo discards only un-applied fields");

            File.AppendAllText(path, " ");
            byte[] external = File.ReadAllBytes(path);
            Click("FreshDocumentSave");
            Check(File.ReadAllBytes(path).SequenceEqual(external) && ReferenceEquals(window.CurrentIntent, changed) && window.HasUnsavedChanges,
                "stale-save refusal preserves external document and authored draft");
            Check(Find<TextBlock>(window, "FreshDocumentStatus").Text!.Contains("Reopen", StringComparison.Ordinal), "stale-save action is explained");
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
            Click("FreshDocumentReopen");
            Check(!window.HasUnsavedChanges && window.CurrentIntent!.FloorX == 8448 && !Find<Button>(window, "FreshDocumentUndo").IsEnabled,
                "explicit discard/reopen reads external bytes and resets undo history");
            Text("FreshHeading", "90");
            Click("FreshDocumentSave");
            Check(!window.HasUnsavedChanges && NativeId65FreshDocumentStore.Capture(project).Intent?.Heading == 90, "Save applies valid pending form and persists it");

            currentProject = false;
            Text("FreshHeading", "91");
            byte[] beforeTransition = File.ReadAllBytes(path);
            Click("FreshDocumentSave");
            Check(File.ReadAllBytes(path).SequenceEqual(beforeTransition) && window.HasUnsavedChanges, "project transition forbids writes and keeps pending intent");
            Click("FreshDocumentReopen");
            Check(Find<TextBlock>(window, "FreshDocumentStatus").Text!.Contains("project", StringComparison.OrdinalIgnoreCase), "project transition refusal visible");
            currentProject = true;
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
            Click("FreshDocumentReopen");
            string writer = Path.Combine(root, ".native-id65-fresh-document.writer.lock");
            File.WriteAllText(writer, "foreign lease");
            Text("FreshHeading", "92");
            Click("FreshDocumentSave");
            Check(File.ReadAllText(writer) == "foreign lease" && File.ReadAllBytes(path).SequenceEqual(beforeTransition) && window.HasUnsavedChanges,
                "actual I/O refusal preserves draft/file/foreign lease");
            File.Delete(writer);
            Click("FreshDocumentSave");
            Check(!window.HasUnsavedChanges && NativeId65FreshDocumentStore.Capture(project).Intent?.Heading == 92, "valid retry uses existing store");

            File.WriteAllText(path, "malformed existing document");
            var lastValid = window.CurrentIntent;
            Click("FreshDocumentReopen");
            Check(ReferenceEquals(lastValid, window.CurrentIntent) && File.ReadAllText(path) == "malformed existing document",
                "malformed Reopen cannot replace content with starter/default");
            bool refused = false;
            try { _ = new NativeFreshDocumentWindow(project, () => true); }
            catch (InvalidDataException) { refused = true; }
            Check(refused, "new window refuses existing malformed document rather than defaults");
            foreach (var sentinel in sentinels) Check(File.ReadAllBytes(sentinel.Key).SequenceEqual(sentinel.Value), "project/source/old Lab sentinel exact");
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
            window.Close();
            Pump();
            Check(!window.IsVisible, "explicit discard closes without file mutation");
        }
        finally
        {
            if (window?.IsVisible == true)
            {
                window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
                window.Close();
                Pump();
            }
            Directory.Delete(root, recursive: true);
        }
        Console.WriteLine($"FRESH_NATIVE_DOCUMENT_UI_OK checks={checks} sourceRequired=false publication=false");

        void Text(string name, string value) { Find<TextBox>(window!, name).Text = value; Pump(); }
        void Click(string name)
        {
            Button button = Find<Button>(window!, name);
            Check(button.IsEnabled, $"enabled {name}");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
            Pump();
        }
        void Check(bool value, string label) { if (!value) throw new InvalidDataException("Fresh document UI: " + label); checks++; }
        void Snapshot(Window target, string name)
        {
            target.InvalidateVisual(); Pump();
            using var image = target.CaptureRenderedFrame() ?? throw new InvalidOperationException("No fresh document frame.");
            image.Save(Path.Combine(outputDirectory, name));
        }
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static T Find<T>(Control root, string name) where T : Control
    {
        T[] matches = root.GetLogicalDescendants().OfType<T>().Where(c => c.Name == name).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"Expected one {name}, found {matches.Length}.");
    }
}
