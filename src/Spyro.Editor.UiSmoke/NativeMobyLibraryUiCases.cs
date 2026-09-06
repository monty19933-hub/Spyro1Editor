using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Primitives;

internal static class NativeMobyLibraryUiCases
{
    internal static void Run(string outputDirectory)
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            Wait(() => window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count > 0 &&
                window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry != null, "retail scene load");
            var before = window.CaptureEditorShellSessionSnapshotForTesting();
            var files = Persistence(before.Workspace.RootPath);
            Click(Find<Button>(window, "RetailNativeMobyLibraryButton"));
            Window library = Library(window);
            ListBox list = Find<ListBox>(library, "NativeMobyLibraryList");
            Wait(() => Items(list).Length == 4877, "all native rows");
            Check(Items(list).Select(e => e.SourceVariantId).Distinct().Count() == 4877, "distinct source rows");
            ComboBox source = Find<ComboBox>(library, "NativeMobyLibrarySource");
            TextBox search = Find<TextBox>(library, "NativeMobyLibrarySearch");
            source.SelectedItem = source.ItemsSource!.Cast<object>().Single(x => x.ToString() == "Wizard Peak");
            search.Text = "0x011B";
            Pump();
            Check(Items(list).Length > 0 && Items(list).All(e => e.SourceLevelKey == "wizardpeak" && e.ActorId == 0x11B), "source and class filters");
            list.SelectedIndex = 0;
            Button use = Find<Button>(library, "NativeMobyLibraryUseTemplate");
            Check(!use.IsEnabled, "unproved Wizard cannot borrow a template");
            search.Text = "no-such-native-object";
            Pump();
            Check(Items(list).Length == 0 && !use.IsEnabled, "no-result selection cleared");
            search.Text = ""; source.SelectedIndex = 0;
            Pump();
            ComboBox view = Find<ComboBox>(library, "NativeMobyLibraryView");
            view.SelectedIndex = 1;
            Check(Items(list).Length == 1403, "resources are separate");
            list.SelectedItem = Items(list).First(e => e.IsSpecialPlayerStream);
            Check(!use.IsEnabled && Find<TextBlock>(library, "NativeMobyLibraryDetails").Text!.Contains("not a placed object", StringComparison.Ordinal), "player stream cannot become Moby");
            view.SelectedIndex = 0;
            source.SelectedItem = source.ItemsSource!.Cast<object>().Single(x => x.ToString() == before.CurrentLevel!.DisplayName);
            bool foundApproved = false;
            foreach (var entry in Items(list))
            {
                list.SelectedItem = entry;
                if (use.IsEnabled) { foundApproved = true; break; }
            }
            Check(foundApproved, "an exact already-approved native variant is usable");
            Snapshot(library, Path.Combine(outputDirectory, "native-moby-library-retail.png"));
            NoMutation(window, before, files);
            object approved = list.SelectedItem!;
            var moved = before.CurrentMobys[0];
            var originalPosition = moved.Position;
            try
            {
                moved.Position = new Vector3f(originalPosition.X + 1, originalPosition.Y, originalPosition.Z);
                Click(use);
                Check(!use.IsEnabled && !window.OwnedWindows.Any(w => w.Title == "Add Object"),
                    "a stale scene cannot use an earlier approved selection");
                Check(Find<TextBlock>(library, "NativeMobyLibraryDetails").Text!.Contains("changed while browsing", StringComparison.Ordinal),
                    "stale scene refusal is explained");
            }
            finally { moved.Position = originalPosition; }
            list.SelectedItem = null;
            list.SelectedItem = approved;
            Check(use.IsEnabled, "restoring the exact captured scene restores template eligibility");
            Click(use);
            Wait(() => window.OwnedWindows.Any(w => w.Title == "Add Object"), "approved selection enters existing Add dialog");
            Window add = window.OwnedWindows.Single(w => w.Title == "Add Object");
            Check(add.GetLogicalDescendants().OfType<Button>().Any(b => b.Name == "ObjectGalleryButton"), "existing Add workflow retained");
            add.Close(false); Pump();
            NoMutation(window, before, files);
            Console.WriteLine("NATIVE_MOBY_LIBRARY_UI_OK: 4877 exact variants, search/source/resources, special/unsupported/stale refusal, approved-template handoff and no-write/cancel checks passed.");
        }
        finally { window.Close(); Pump(); }
    }

    internal static void AssertBlankLibrary(MainWindow window, string outputDirectory)
    {
        var before = window.CaptureEditorShellSessionSnapshotForTesting();
        var files = Persistence(before.Workspace.RootPath);
        Click(Find<Button>(window, "Id65NativeMobyLibraryButton"));
        Window library = Library(window);
        ListBox list = Find<ListBox>(library, "NativeMobyLibraryList");
        Wait(() => Items(list).Length == 4877, "blank editor native catalogue");
        list.SelectedIndex = 0;
        Check(!Find<Button>(library, "NativeMobyLibraryUseTemplate").IsEnabled, "lab unsupported source row does not enable Apply");
        Check(Find<TextBlock>(library, "NativeMobyLibraryDetails").Text!.Contains("not certified by the retail inventory", StringComparison.Ordinal), "lab residency not borrowed");
        Snapshot(library, Path.Combine(outputDirectory, "native-moby-library-blank.png"));
        Click(Find<Button>(library, "NativeMobyLibraryCancel")); Pump();
        NoMutation(window, before, files);
        Console.WriteLine("NATIVE_MOBY_LIBRARY_BLANK_UI_OK: shared catalogue, explicit uncertified destination and canonical-state preservation passed.");
    }
    private static void NoMutation(MainWindow window, EditorShellSessionSnapshot before, Dictionary<string, string> files)
    {
        var after = window.CaptureEditorShellSessionSnapshotForTesting();
        Check(before == after, "catalogue must not change scene/selection/navigation/edit signatures");
        var now = Persistence(before.Workspace.RootPath);
        Check(files.Count == now.Count && files.All(p => now.GetValueOrDefault(p.Key) == p.Value), "catalogue must not change persisted JSON");
    }
    private static Dictionary<string, string> Persistence(string root) => Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
        .ToDictionary(p => p, p => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p))), StringComparer.Ordinal);
    private static NativeMobyCatalogueEntry[] Items(ListBox list) => list.ItemsSource?.Cast<NativeMobyCatalogueEntry>().ToArray() ?? [];
    private static Window Library(Window owner) { Wait(() => owner.OwnedWindows.Any(w => w.Title == "Native Moby Library"), "library window"); return owner.OwnedWindows.Single(w => w.Title == "Native Moby Library"); }
    private static T Find<T>(Control root, string name) where T : Control
    {
        var matches = root.GetLogicalDescendants().OfType<T>().Where(c => c.Name == name).ToArray();
        return matches.Length > 0 && matches.All(c => ReferenceEquals(c, matches[0]))
            ? matches[0]
            : throw new InvalidOperationException($"Expected one distinct control named {name}, found {matches.Length} logical matches.");
    }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(); }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Wait(Func<bool> predicate, string label) { var clock = Stopwatch.StartNew(); while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(60)) { Pump(); Thread.Sleep(10); } Check(predicate(), label); Pump(); }
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("Native library: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(); using var image = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No library frame."); image.Save(path); }
}
