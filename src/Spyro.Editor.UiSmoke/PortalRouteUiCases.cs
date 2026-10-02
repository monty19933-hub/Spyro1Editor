using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class PortalRouteUiCases
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        string root = Directory.CreateTempSubdirectory("spyro-portal-route-ui-").FullName;
        string previous = Environment.CurrentDirectory;
        string? workspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"), release = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), "{\"levels\":[]}");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = root; Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            window = new MainWindow(); window.Show(); Pump();
            var art = new LevelDefinition { Key = "artisans", LevelId = 10, SourceWadEntry = 10, DisplayName = "Artisans" };
            var stone = new LevelDefinition { Key = "stonehill", LevelId = 11, SourceWadEntry = 12, DisplayName = "Stone Hill" };
            var wizard = new LevelDefinition { Key = "wizardpeak", LevelId = 33, SourceWadEntry = 40, DisplayName = "Wizard Peak" };
            var catalog = new LevelCatalog([art, stone, wizard]);
            Set(window, "_retailCatalog", catalog); Set(window, "_catalog", catalog); Set(window, "_currentLevel", art);
            var mobys = Get<List<Moby>>(window, "_currentMobys"); mobys.Clear();
            var link = new MobyLink { Key = "test-portal", Name = "Stone Hill", Kind = "portal controls", LinkedMove = true, Confidence = "source-proven", TrueIndexes = [38, 144, 157] };
            foreach (var (row, actor) in new[] { (38, 0x18e), (144, 1), (157, 0x11e) })
            {
                var moby = new Moby { TrueIndex = row, Type = 0, SourceByte36 = actor & 255, SourceByte37 = actor >> 8, Flag4A = 0x10, Flag4B = 0xff };
                moby.Links.Add(link); mobys.Add(moby);
            }
            Invoke(window, "RefreshPortalControlPanel");
            var portals = Get<ComboBox>(window, "_portalControlBox");
            var destination = Get<ComboBox>(window, "_portalDestinationBox");
            var apply = Get<Button>(window, "_applyPortalRouteButton");
            var undo = Get<Button>(window, "_undoPortalRouteButton");
            Check(portals.SelectedItem!.ToString()!.Contains("Stone Hill doorway"), "release portal label is readable");
            Check(destination.SelectedItem!.ToString() == "Stone Hill (ID 11)" && apply.IsEnabled, "current destination selected with exact level ID");
            Set(window, "_loadingLevel", true); Invoke(window, "RefreshPortalControlPanel");
            Check(!apply.IsEnabled, "portal controls stay disabled while the selected level loads");
            Set(window, "_loadingLevel", false); Invoke(window, "RefreshLevelSelectionAvailability");
            Check(apply.IsEnabled && Get<Button>(window, "_applyReturnHomeButton").IsEnabled, "normal level-load completion enables portal controls without reselection");
            destination.SelectedItem = destination.Items.Single(i => i!.ToString() == "Wizard Peak (ID 33)"); Click(apply);
            var saved = PortalRouteStore.Capture(root);
            Check(saved.Document.DestinationFor(new("artisans", 38)) == 33 && saved.Document.ReturnFor(33) == new PortalEndpoint("artisans", 38), "actual button persists paired cross-home route");
            Check(undo.IsEnabled && File.Exists(Path.Combine(root, PortalRouteStore.PreviousFileName)), "route edit exposes undo and durable recovery");
            Invoke(window, "RefreshPortalControlPanel");
            Check(destination.SelectedItem!.ToString() == "Wizard Peak (ID 33)", "refresh reflects saved route");
            var targets = (System.Collections.IEnumerable)Invoke(window, "FindEditedLevelExportTargets")!;
            Check(targets.Cast<object>().Count() == 2, "route-only project discovers both affected levels for normal BIN export");
            Click(undo);
            Check(!PortalRouteStore.Capture(root).Document.HasEdits && destination.SelectedItem!.ToString() == "Stone Hill (ID 11)", "undo restores native route and visible selection");
            destination.SelectedItem = destination.Items.Single(i => i!.ToString() == "Wizard Peak (ID 33)"); Click(apply);
            Set(window, "_currentLevel", wizard); Invoke(window, "RefreshPortalControlPanel");
            var returns = Get<ComboBox>(window, "_returnHomePortalBox");
            Check(returns.SelectedItem!.ToString()!.Contains("Artisans — Stone Hill doorway"), "destination level shows paired return endpoint");
            returns.SelectedIndex = 0; Click(Get<Button>(window, "_applyReturnHomeButton"));
            Check(PortalRouteStore.Capture(root).Document.Returns.Count == 0, "original route choice clears explicit return override");
            Set(window, "_createBinBusy", true); Invoke(window, "RefreshPortalRoutingEditor");
            Check(!apply.IsEnabled && !Get<Button>(window, "_applyReturnHomeButton").IsEnabled, "exports lock both route mutation controls");
            Set(window, "_createBinBusy", false); Invoke(window, "RefreshPortalRoutingEditor");
            var external = PortalRouteStore.Capture(root); PortalRouteStore.Save(root, external.Document.WithReturn(33, new("artisans", 39)), external);
            Invoke(window, "UndoPortalRouteChange");
            Check(PortalRouteStore.Capture(root).Document.ReturnFor(33) == new PortalEndpoint("artisans", 39), "stale UI undo preserves external route edit");
            var fresh = SceneDocument.New("Portal Test"); Invoke(window, "ApplyFreshScene", fresh);
            Check(Get<Control>(window, "_homeworldTextGroup").IsVisible && Get<Button>(window, "_applyReturnHomeButton").IsEnabled && !apply.IsEnabled, "fresh level shares Return Home controls without inventing entry portals");
            returns.SelectedItem = returns.Items.First(i => i!.ToString()!.Contains("Artisans — Stone Hill doorway")); Click(Get<Button>(window, "_applyReturnHomeButton"));
            Check(PortalRouteStore.Capture(root).Document.ReturnFor(65) == new PortalEndpoint("artisans", 38), "fresh level Return Home setting persists");
            PortalRouteStore.Save(root, PortalRouteDocument.Empty.WithReturn(65, new("artisans", 38)), PortalRouteStore.Capture(root));
            targets = (System.Collections.IEnumerable)Invoke(window, "FindEditedLevelExportTargets")!;
            Check(targets.Cast<object>().Count() == 1, "a fresh-only return route reaches export validation instead of reporting no edits");
            File.WriteAllText(Path.Combine(root, PortalRouteStore.FileName), "broken"); Invoke(window, "RefreshPortalRoutingEditor");
            Check(!Get<Button>(window, "_applyReturnHomeButton").IsEnabled && Get<TextBlock>(window, "_portalRoutingHint").Text!.Contains("Could not read"), "malformed saved routes stop edits with an explanation");
            Check(File.ReadAllText(Path.Combine(root, PortalRouteStore.FileName)) == "broken" && !Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories).Any(), "UI checks preserve unknown saved data and create no disc image");
            Console.WriteLine($"PORTAL_ROUTE_UI_OK checks={checks} sourceOpened=false nativeOutputWritten=false fixtures={root}");
        }
        finally
        {
            window?.Close(); Pump(); Environment.CurrentDirectory = previous;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", release);
        }
    }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static object? Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
}
