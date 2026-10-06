using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Drives the editor (release mode) on a project folder in place, to build a level the way a user would.
/// Usage: UiSmoke --new-level &lt;project folder&gt; &lt;output folder&gt; &lt;step&gt;
/// </summary>
internal static partial class NewLevelBuildCases
{
    internal static void Run(string project, string outDir, string step)
    {
        project = Path.GetFullPath(project); outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outDir);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", project);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "logs"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_PROJECTS_ROOT", Path.GetDirectoryName(project));
        Environment.CurrentDirectory = project;
        // Create BIN ends by opening its output folder in Finder; keep that off the desktop during these runs.
        Environment.SetEnvironmentVariable("PATH", "/nonexistent");
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            switch (step)
            {
                case "survey": Survey(window, outDir); break;
                default: RunStep(window, outDir, step); break;
            }
        }
        finally { window.Close(); Pump(10); }
    }

    static partial void RunStepCore(MainWindow window, string outDir, string step, ref bool handled);
    private static void RunStep(MainWindow window, string outDir, string step)
    {
        bool handled = false;
        RunStepCore(window, outDir, step, ref handled);
        if (!handled) throw new ArgumentException("Unknown step " + step);
    }

    private static void Survey(MainWindow window, string outDir)
    {
        Load(window, "stonehill");
        var snap = window.CaptureEditorShellSessionSnapshotForTesting();
        using (var w = new StreamWriter(Path.Combine(outDir, "stonehill-objects.tsv")))
            foreach (Moby m in snap.CurrentMobys.OrderBy(m => m.TrueIndex))
                w.WriteLine($"{m.TrueIndex}\t0x{m.Type:X2}\t{m.DisplayLabel}\t{m.CandidateKind}\t{m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}\tgem={m.TreasureValue}\t{m.EditorControlKind}");
        var g = snap.CurrentGeometry!;
        Console.WriteLine($"Stone Hill: {snap.CurrentMobys.Count} objects, {g.Polygons.Count} faces, bounds {g.Bounds.Left:0},{g.Bounds.Top:0} .. {g.Bounds.Right:0},{g.Bounds.Bottom:0}, z {g.MinZ:0}..{g.MaxZ:0}");
        foreach (var grp in snap.CurrentMobys.GroupBy(m => m.DisplayLabel).OrderByDescending(x => x.Count()).Take(40))
            Console.WriteLine($"  {grp.Count(),3} x {grp.Key}");
        foreach (var t in g.Polygons.GroupBy(p => (p.TextureId, TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface))).OrderByDescending(x => x.Count()).Take(25))
            Console.WriteLine($"  texture {t.Key.TextureId,3} {t.Key.Item2,-8} {t.Count()} faces");
        Snapshot(window, Path.Combine(outDir, "stonehill.png"));

        Load(window, "artisans");
        snap = window.CaptureEditorShellSessionSnapshotForTesting();
        ComboBox box = Get<ComboBox>(window, "_portalControlBox");
        foreach (object row in box.Items)
        {
            var t = row.GetType();
            Moby? trig = (Moby?)t.GetProperty("EntryTrigger")!.GetValue(row);
            Moby? loc = (Moby?)t.GetProperty("Location")!.GetValue(row);
            var members = (IReadOnlyList<Moby>)t.GetProperty("Members")!.GetValue(row)!;
            Console.WriteLine($"portal row: {row} trigger={(trig == null ? "-" : $"T{trig.TrueIndex} {trig.Position.X:0},{trig.Position.Y:0},{trig.Position.Z:0}")} location={(loc == null ? "-" : $"T{loc.TrueIndex} {loc.Position.X:0},{loc.Position.Y:0},{loc.Position.Z:0}")} members=[{string.Join(", ", members.Select(m => $"T{m.TrueIndex} {m.DisplayLabel}"))}]");
        }
        using (var w = new StreamWriter(Path.Combine(outDir, "artisans-objects.tsv")))
            foreach (Moby m in snap.CurrentMobys.OrderBy(m => m.TrueIndex))
                w.WriteLine($"{m.TrueIndex}\t0x{m.Type:X2}\t{m.DisplayLabel}\t{m.CandidateKind}\t{m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}\t{m.EditorControlKind}");
        Console.WriteLine("NEW_LEVEL_SURVEY_OK");
    }

    internal static void Load(MainWindow window, string key)
    {
        LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(key)!;
        if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != key)
        {
            var load = (Task)Invoke(window, "SelectLevelAsync", level, null)!;
            Wait(() => load.IsCompleted, "load " + key, 300);
            WaitIdle(window);
        }
    }
    internal static void Save(MainWindow window)
    {
        var save = (Task<bool>)Invoke(window, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => save.IsCompleted, "save", 300);
        Console.WriteLine("save: " + (save.Result ? "ok" : "FAILED") + " — " + Get<TextBlock>(window, "_statusText").Text);
        if (!save.Result) throw new InvalidOperationException("Save failed");
        Pump(20);
    }
    internal static string Status(MainWindow w) => Get<TextBlock>(w, "_statusText").Text ?? "";
    internal static void WaitIdle(MainWindow window)
    {
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(600))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart(); else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }
    internal static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    internal static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    internal static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    internal static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    internal static void Wait(Func<bool> predicate, string label, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        if (!predicate()) throw new InvalidOperationException("New level: " + label);
        Dispatcher.UIThread.RunJobs();
    }
    internal static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
