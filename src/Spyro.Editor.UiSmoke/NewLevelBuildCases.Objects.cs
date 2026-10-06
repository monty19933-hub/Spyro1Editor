using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Primitives;

internal static partial class NewLevelBuildCases
{
    internal static UniversalSourceLibrary Library(MainWindow w)
    {
        var value = typeof(MainWindow).GetField("_universalCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
        return (UniversalSourceLibrary)value.GetType().GetField("Item2")!.GetValue(value)!;
    }
    internal static List<Moby> Mobys(MainWindow w) => Get<List<Moby>>(w, "_currentMobys");
    internal static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b)); Pump(); }
    internal static void SetField(MainWindow w, string name, object? value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(w, value);

    /// <summary>Opens Add object…, waits for the catalogue, returns the window and its entries.</summary>
    internal static (Window Browser, UniversalMobyCatalogEntry[] Entries) OpenBrowser(MainWindow window)
    {
        Console.WriteLine("opening Add object…");
        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        Wait(() => window.OwnedWindows.Any(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal)), "browser", 60);
        Console.WriteLine("browser open; waiting for the catalogue");
        Window browser = window.OwnedWindows.Last(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal));
        ListBox list = Find<ListBox>(browser, "UniversalMobyList");
        Wait(() => (list.ItemsSource?.Cast<object>().Count() ?? 0) > 150, "catalogue", 600);
        return (browser, list.ItemsSource!.Cast<UniversalMobyCatalogEntry>().ToArray());
    }

    internal static UniversalMobyPlacement Trial(MainWindow window, UniversalMobyCatalogEntry e, int slot, Vector3f? at = null)
    {
        byte[] row = Library(window).Get(e.Default.SourceLevelKey).Data.Row(e.Default.SourceRow);
        int x = at == null ? BitConverter.ToInt32(row, 12) : (int)(at.Value.X * 16);
        int y = at == null ? BitConverter.ToInt32(row, 16) : (int)(at.Value.Y * 16);
        int z = at == null ? BitConverter.ToInt32(row, 20) : (int)(at.Value.Z * 16);
        return new UniversalMobyPlacement(e.Default.SourceLevelKey, e.Default.SourceRow, x, y, z, EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: slot);
    }

    static partial void RunStepCore(MainWindow window, string outDir, string step, ref bool handled)
    {
        handled = true;
        switch (step)
        {
            case "fit": Fit(window); break;
            default: handled = RunMoreSteps(window, outDir, step); break;
        }
    }

    private static partial bool RunMoreSteps(MainWindow window, string outDir, string step);

    private static readonly (string Hex, string Source, string Name)[] EnemyIdeas =
    [
        ("0x0C6", "icecavern", "Snow Gnorc"), ("0x0D3", "icecavern", "Snowballer"), ("0x0D4", "icecavern", "Agile Snowballer"),
        ("0x0E7", "icecavern", "Armored Gnorc"), ("0x12F", "highcaves", "Tornado Wizard"), ("0x131", "highcaves", "Metalback Spider"),
        ("0x11C", "wizardpeak", "Armored Druid"), ("0x0A3", "icyflight", "Copter Gnorc"), ("0x03E", "loftycastle", "Fat Bat"),
        ("0x0CB", "hauntedtowers", "Tin Soldier"), ("0x065", "treetops", "Strongarm"), ("0x0A5", "darkhollow", "Small Gnorc"),
    ];

    internal static UniversalMobyCatalogEntry Entry(UniversalMobyCatalogEntry[] entries, string hex, string source) =>
        entries.First(e => e.ClassId == Convert.ToInt32(hex, 16) && e.SourceLevels.Contains(source));

    internal static (UniversalComposeResult? Result, string? Error) DryRun(MainWindow window, LevelDefinition level, IReadOnlyList<UniversalMobyPlacement> placements, IReadOnlyList<int> removed)
    {
        var library = Library(window);
        var task = Task.Run(() => UniversalMobyCatalogBuilder.DryRun(library, level, placements, null, removed));
        Wait(() => task.IsCompleted, "dry run", 900);
        return task.Result;
    }

    private static void Fit(MainWindow window)
    {
        Load(window, "stonehill");
        var (browser, entries) = OpenBrowser(window);
        browser.Close(); Pump(10);
        LevelDefinition stone = Get<LevelCatalog>(window, "_retailCatalog").FindByKey("stonehill")!;
        List<int> removed = Mobys(window).Where(m => !m.IsAdded && (m.DisplayLabel is "Ram" or "Shepherd")).Select(m => m.TrueIndex).ToList();
        Console.WriteLine($"removing {removed.Count} rows (rams and shepherds): {string.Join(",", removed)}");
        foreach (var idea in EnemyIdeas)
        {
            var e = Entry(entries, idea.Hex, idea.Source);
            var single = DryRun(window, stone, [Trial(window, e, 0)], removed);
            var three = DryRun(window, stone, [Trial(window, e, 0), Trial(window, e, 1), Trial(window, e, 2)], removed);
            Console.WriteLine($"{idea.Name,-18} {idea.Source,-13} model {e.Default.ModelBytes,7} behavior {e.Default.BehaviorBytes,6}  x1: {(single.Error ?? "fits")[..Math.Min(70, (single.Error ?? "fits").Length)]}  x3: {(three.Error ?? "fits")[..Math.Min(50, (three.Error ?? "fits").Length)]}");
        }
        // Greedy mix: add enemy kinds in order while everything still fits.
        List<UniversalMobyPlacement> mix = [];
        foreach (var idea in EnemyIdeas)
        {
            var e = Entry(entries, idea.Hex, idea.Source);
            var trial = mix.Concat(Enumerable.Range(0, 3).Select(i => Trial(window, e, mix.Count + i))).ToList();
            var r = DryRun(window, stone, trial, removed);
            if (r.Error == null) { mix = trial; Console.WriteLine($"  mix + 3 x {idea.Name}: fits"); }
            else Console.WriteLine($"  mix + 3 x {idea.Name}: no ({r.Error[..Math.Min(90, r.Error.Length)]})");
        }
        Console.WriteLine("NEW_LEVEL_FIT_OK");
    }
}
