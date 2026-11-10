using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Spyro.Editor.App.Views;

/// <summary>Lab: <c>--tour-shots &lt;out&gt; [project]</c> takes pictures of what a newcomer sees, for review: the welcome
/// card on an empty project, then (on a disposable clone of a project) the editor with each side-panel tab, every card
/// open and the panel scrolled through, the Help window and the menus' entries as text. Nothing is exported.</summary>
internal static class TourShots
{
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    internal static void Run(string outDir, string? project)
    {
        Directory.CreateDirectory(outDir);
        string parent = Directory.CreateTempSubdirectory("spyro-tour-").FullName;
        foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        try
        {
            // 1. A new user: no disc chosen yet.
            string empty = Directory.CreateDirectory(Path.Combine(parent, "empty", "support")).FullName;
            string repo = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog found.");
            File.Copy(Path.Combine(repo, "spyro-level-catalog.json"), Path.Combine(empty, "spyro-level-catalog.json"));
            string emptyRoot = Path.GetDirectoryName(empty)!;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", emptyRoot);
            Environment.CurrentDirectory = emptyRoot;
            var first = new MainWindow { Width = 1440, Height = 900 };
            first.Show(); Pump(150);
            Snapshot(first, Path.Combine(outDir, "00-welcome.png"));
            first.Close(); Pump(10);
            if (project == null) return;

            // 2. The editor on a project.
            string workspace = Path.Combine(parent, "project");
            var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
            clone!.WaitForExit();
            if (clone.ExitCode != 0) throw new IOException("Could not clone the project folder.");
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
            Environment.CurrentDirectory = workspace;
            if (Path.GetFullPath(Spyro.Editor.Core.Workspace.EditorWorkspace.Find().RootPath).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
                throw new InvalidOperationException("The editor would open another folder; stopping.");
            MainWindow window = new() { Width = 1440, Height = 900 };
            window.Show();
            var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
            while (total.Elapsed < TimeSpan.FromSeconds(300))
            {
                Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
                var s = window.CaptureEditorShellSessionSnapshotForTesting();
                bool busy = Get<bool>(window, "_loadingLevel") || s.CurrentLevel == null || s.CurrentGeometry == null;
                if (busy) quiet.Restart(); else if (quiet.Elapsed > TimeSpan.FromSeconds(2)) break;
            }
            Snapshot(window, Path.Combine(outDir, "01-editor.png"));
            List<string> text = [];
            foreach (var (field, name) in new[] { ("_modernObjectWorkspaceTab", "objects"), ("_modernTerrainWorkspaceTab", "terrain"), ("_modernLevelWorkspaceTab", "game-settings"), ("_modernEnvironmentWorkspaceTab", "environment") })
            {
                var tab = Get<TabItem>(window, field);
                tab.IsSelected = true; Pump(30);
                Snapshot(window, Path.Combine(outDir, $"1-{name}-0-as-opened.png"));
                if (tab.Content is not Control content) continue;
                foreach (var expander in content.GetLogicalDescendants().OfType<Expander>()) expander.IsExpanded = true;
                Pump(30);
                // The panel's own scroller: the tallest one that scrolls (lists inside cards have small ones).
                var scroll = content.GetSelfAndVisualDescendants().OfType<ScrollViewer>().Concat(content.GetVisualAncestors().OfType<ScrollViewer>())
                    .Where(v => v.Extent.Height > v.Viewport.Height + 1).OrderByDescending(v => v.Viewport.Height).FirstOrDefault();
                int page = 0;
                if (scroll != null)
                    for (double y = 0; ; y += scroll.Viewport.Height - 40)
                    {
                        scroll.Offset = new Vector(0, Math.Min(y, scroll.Extent.Height - scroll.Viewport.Height)); Pump(15);
                        Snapshot(window, Path.Combine(outDir, $"1-{name}-{++page}-all-open.png"));
                        if (y >= scroll.Extent.Height - scroll.Viewport.Height || page > 14) break;
                    }
                else Snapshot(window, Path.Combine(outDir, $"1-{name}-1-all-open.png"));
                text.Add($"== {name}");
                text.AddRange(content.GetLogicalDescendants().Select(c => c switch
                {
                    Expander e => "[card] " + ((e.Header as TextBlock)?.Text ?? e.Header?.ToString()),
                    CheckBox k => "  (tick) " + k.Content,
                    RadioButton r => "  (choice) " + r.Content,
                    Button b => "  (button) " + (b.Content as string ?? (b.Content as TextBlock)?.Text ?? ""),
                    TextBlock t when !string.IsNullOrWhiteSpace(t.Text) && t.Parent is not Button => "  " + t.Text,
                    _ => ""
                }).Where(l => l.Trim().Length > 0));
            }
            // 3. Menus (their entries as text) and Help.
            foreach (string menu in new[] { "EditorFileMenu", "EditorMoreMenu" })
            {
                var button = window.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == menu);
                if (button?.Flyout is MenuFlyout flyout)
                {
                    text.Add("== menu " + menu);
                    text.AddRange(flyout.Items.Select(i => i is MenuItem m ? "  " + m.Header + (m.IsEnabled ? "" : "  (greyed out)") : "  ----"));
                }
            }
            var help = (Task)typeof(MainWindow).GetMethod("ShowHelpAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            Pump(60);
            if (window.OwnedWindows.FirstOrDefault() is { } dialog)
            {
                Snapshot(dialog, Path.Combine(outDir, "2-help-0.png"));
                var scroll = dialog.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v => v.Extent.Height > v.Viewport.Height + 1);
                for (int page = 1; scroll != null && page < 12; page++)
                {
                    double y = page * (scroll.Viewport.Height - 40);
                    if (y >= scroll.Extent.Height - scroll.Viewport.Height + scroll.Viewport.Height - 40) break;
                    scroll.Offset = new Vector(0, Math.Min(y, scroll.Extent.Height - scroll.Viewport.Height)); Pump(15);
                    Snapshot(dialog, Path.Combine(outDir, $"2-help-{page}.png"));
                }
                text.Add("== help");
                text.AddRange(dialog.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Trim().Length > 0));
                dialog.Close(); Pump(10);
            }
            // 4. The colour picker (a flyout: its content shown in a window of its own).
            var swatch = window.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "SpyroColourSwatch_body");
            if (swatch?.Flyout is Flyout picker && picker.Content is Control pickerContent)
            {
                var host = new Window { Width = 300, Height = 420, Content = new Border { Padding = new Thickness(14), Child = pickerContent } };
                host.Show(); Pump(30);
                Snapshot(host, Path.Combine(outDir, "3-colour-picker.png"));
                host.Close(); Pump(5);
            }
            File.WriteAllLines(Path.Combine(outDir, "tour-text.txt"), text);
            window.Close(); Pump(10);
            Console.WriteLine("TOUR_SHOTS_OK " + outDir);
        }
        finally { try { Directory.Delete(parent, recursive: true); } catch (IOException) { } }
    }

    /// <summary>Lab: the stand-in Spyro model of Gnasty Gnorc's level (class 0x19E) drawn by the Add object preview.</summary>
    internal static void StandIn(string disc, string outPath)
    {
        var image = Spyro.Editor.Core.Universal.UniversalDiscImage.Read(disc);
        var data = new Spyro.Editor.Core.Universal.UniversalLevelData(image.EntryBytes(76));
        byte[] package = data.Package(0x19E);
        Console.WriteLine($"package {package.Length} bytes, header {Convert.ToHexString(package.AsSpan(0, 0x40))}");
        int animation = BitConverter.ToInt32(package, 0x38);
        Console.WriteLine($"animation header {Convert.ToHexString(package.AsSpan(animation, 0x34))}");
        var frame = UniversalMobyModelPreview.Decode(package, data.Vram().ToArray());
        Console.WriteLine(frame.Description);
        if (frame.Pixels == null) return;
        using var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(UniversalMobyModelPreview.Size, UniversalMobyModelPreview.Size), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
            for (int y = 0; y < UniversalMobyModelPreview.Size; y++)
                System.Runtime.InteropServices.Marshal.Copy(frame.Pixels, y * UniversalMobyModelPreview.Size * 4, buffer.Address + y * buffer.RowBytes, UniversalMobyModelPreview.Size * 4);
        bitmap.Save(outPath);
    }

    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
