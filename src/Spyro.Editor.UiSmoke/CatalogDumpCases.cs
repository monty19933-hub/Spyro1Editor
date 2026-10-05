using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Universal;

/// <summary>
/// Writes the Add object catalogue exactly as the window lists it (names, categories, source levels and
/// source versions), so reports can name objects the way the user sees them.
/// Usage: UiSmoke --catalog-dump &lt;project folder (read only)&gt; &lt;output json&gt;
/// </summary>
internal static class CatalogDumpCases
{
    internal static void Run(string project, string outputJson)
    {
        string outDir = Path.GetDirectoryName(Path.GetFullPath(outputJson))!;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "catalog-dump-project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "catalog-dump-data"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "catalog-dump-logs"));
        Environment.CurrentDirectory = workspace;
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(300))
            {
                Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
                var s = window.CaptureEditorShellSessionSnapshotForTesting();
                if (s.CurrentLevel != null && s.CurrentGeometry != null && clock.Elapsed > TimeSpan.FromSeconds(4)) break;
            }
            Button open = window.GetLogicalDescendants().OfType<Button>().First(b => b.Name == "UniversalMobyLibraryButton");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, open));
            ListBox? list = null;
            clock.Restart();
            while (clock.Elapsed < TimeSpan.FromSeconds(300))
            {
                Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
                Window? library = window.OwnedWindows.FirstOrDefault(w => (w.Title ?? "").StartsWith("Add object", StringComparison.Ordinal));
                list = library?.GetLogicalDescendants().OfType<ListBox>().FirstOrDefault(l => l.Name == "UniversalMobyList");
                if (list?.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Count() > 150) break;
            }
            var entries = list!.ItemsSource!.Cast<UniversalMobyCatalogEntry>().Select(e => new
            {
                e.ClassId, e.ClassHex, e.Name, e.Category, e.SubCategory, e.SourceLevels, e.RestrictionReason,
                Variants = e.Variants.Select(v => new { v.ClassId, v.SourceLevelKey, v.SourceRow, v.RestrictionReason }).ToArray()
            }).ToArray();
            File.WriteAllText(outputJson, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"CATALOG_DUMP_OK entries={entries.Length} file={outputJson}");
        }
        finally
        {
            foreach (Window w in window.OwnedWindows.ToArray()) w.Close();
            window.Close();
            for (int i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
            try { Directory.Delete(workspace, recursive: true); } catch (IOException) { }
        }
    }
}
