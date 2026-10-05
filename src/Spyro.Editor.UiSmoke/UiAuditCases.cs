using System.Diagnostics;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>Release review aid: opens the real main window in release mode on a project copy, walks every
/// workspace tab, menu and the dialogs reachable from them, and writes screenshots plus a text inventory of
/// everything a user can read or press. It never writes outside <c>outDir</c> and the given project copy.</summary>
internal static class UiAuditCases
{
    private static readonly StringBuilder Log = new();
    private static string _out = "";
    private static int _shot;

    internal static void Run(string workspace, string outDir, string[] levelKeys)
    {
        _out = Path.GetFullPath(outDir);
        Directory.CreateDirectory(_out);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", Path.GetFullPath(workspace));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(_out, "data-root"));
        Environment.CurrentDirectory = Path.GetFullPath(workspace);
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            Section("STARTUP");
            Line("title: " + window.Title);
            Line("status: " + Get<TextBlock>(window, "_statusText").Text);
            Shot(window, "main-startup");
            DumpToolbar(window);

            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            foreach (string key in levelKeys)
            {
                LevelDefinition? level = catalog.FindByKey(key);
                if (level == null) { Line("!! unknown level " + key); continue; }
                Step("load " + key, () =>
                {
                    if (!ReferenceEquals(window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel, level))
                    {
                        var t = (Task)Invoke(window, "SelectLevelAsync", level, null)!;
                        Wait(() => t.IsCompleted, 300);
                        if (t.IsFaulted) throw t.Exception!;
                    }
                    WaitIdle(window);
                    if (!ReferenceEquals(window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel, level))
                        throw new InvalidOperationException("level did not load: " + Get<TextBlock>(window, "_statusText").Text);
                });
                AuditLevel(window, key);
            }
            Section("MENUS");
            foreach (string menuName in new[] { "EditorFileMenu", "EditorMoreMenu" })
            {
                Button? button = window.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == menuName);
                if (button?.Flyout is not MenuFlyout menu) { Line("!! missing menu " + menuName); continue; }
                Line($"[{menuName}] {button.Content}");
                foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                    Line($"   item: {item.Header}{(item.IsEnabled ? "" : " [disabled]")}");
            }
        }
        finally
        {
            File.WriteAllText(Path.Combine(_out, "inventory.txt"), Log.ToString());
            try { window.Close(); Pump(5); } catch { }
        }
        Console.WriteLine($"UI_AUDIT_OK shots={_shot} inventory={Path.Combine(_out, "inventory.txt")}");
    }

    private static void AuditLevel(MainWindow window, string key)
    {
        Section("LEVEL " + key);
        Line("status: " + Get<TextBlock>(window, "_statusText").Text);
        var tabs = Get<TabControl>(window, "_modernWorkspaceTabs");
        foreach (TabItem tab in tabs.Items.OfType<TabItem>().ToArray())
        {
            string header = ContentText(tab.Header);
            string name = Slug(header);
            Step($"tab {name}", () =>
            {
                tabs.SelectedItem = tab; Pump(10);
                Section($"TAB {header} ({key}) — collapsed");
                Shot(window, $"{key}-tab-{name}");
                Dump(tab.Content as Control);
                foreach (Expander expander in (tab.Content as Control)?.GetLogicalDescendants().OfType<Expander>().ToArray() ?? [])
                    expander.IsExpanded = true;
                Pump(10);
                Section($"TAB {header} ({key}) — all sections open");
                double height = window.Height;
                window.Height = 2600; Pump(10);
                Shot(window, $"{key}-tab-{name}-open");
                window.Height = height; Pump(5);
                Dump(tab.Content as Control);
            });
        }
        tabs.SelectedIndex = 0; Pump(5);

        // Selection states.
        var mobys = Get<List<Moby>>(window, "_currentMobys");
        var viewport = Get<EditorViewport>(window, "_viewport");
        Moby? enemy = mobys.FirstOrDefault(m => !m.IsRemoved && !m.IsEditorControl);
        if (enemy != null)
            Step("select object", () =>
            {
                viewport.SelectMoby(enemy, true); Pump(10);
                Section($"OBJECT SELECTED ({key}): {enemy.Label}");
                Line("status: " + Get<TextBlock>(window, "_statusText").Text);
                window.Height = 2600; Pump(10);
                Shot(window, $"{key}-object-selected");
                window.Height = 900; Pump(5);
                Dump(tabs.Items.OfType<TabItem>().First().Content as Control);
            });
        Step("select terrain", () =>
        {
            TabItem terrain = Get<TabItem>(window, "_modernTerrainWorkspaceTab");
            tabs.SelectedItem = terrain; Pump(5);
            viewport.FocusTerrain(Math.Min(40, Get<GeometryCandidate>(window, "_currentGeometry").Polygons.Count - 1)); Pump(10);
            Section($"TERRAIN SELECTED ({key})");
            Line("status: " + Get<TextBlock>(window, "_statusText").Text);
            window.Height = 2600; Pump(10);
            Shot(window, $"{key}-terrain-selected");
            window.Height = 900; Pump(5);
            Dump(terrain.Content as Control);
            tabs.SelectedIndex = 0; Pump(5);
        });

        // Dialogs reachable from the toolbar and the Objects tab.
        foreach (string buttonText in new[] { "Add object…", "Add dragonfly egg", "Add sign", "Quick Add…", "Edit", "Replace" })
            Dialog(window, $"{key}-dialog-{Slug(buttonText)}", () =>
            {
                Button? b = window.GetLogicalDescendants().OfType<Button>()
                    .FirstOrDefault(x => x.IsEffectivelyVisible && x.IsEffectivelyEnabled && (x.Content as string) == buttonText);
                if (b == null) { Line($"   (button '{buttonText}' not available)"); return; }
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
            });
        foreach (string method in new[] { "ShowProjectDataAsync", "ShowBuildSafetyInspectorAsync", "ShowHelpAsync", "StartBlankCurrentLevelAsync",
                     "RestoreBeforeClearAsync", "CreateObjectTestBinAsync" })
            Dialog(window, $"{key}-dialog-{Slug(method)}", () => Invoke(window, method));
    }

    private static void DumpToolbar(MainWindow window)
    {
        Section("WINDOW CHROME (outside the tabs)");
        var tabs = Get<TabControl>(window, "_modernWorkspaceTabs");
        HashSet<Control> inTabs = [.. tabs.GetLogicalDescendants().OfType<Control>()];
        foreach (Control c in window.GetLogicalDescendants().OfType<Control>().Where(c => !inTabs.Contains(c) && !ReferenceEquals(c, tabs)))
            Describe(c);
    }

    private static void Dialog(MainWindow window, string name, Action open)
    {
        Step("dialog " + name, () =>
        {
            HashSet<Window> before = [.. AllOwned(window)];
            Task? pending = null;
            try { open(); } catch (TargetInvocationException e) { Line("   !! threw: " + e.InnerException?.Message); return; }
            Stopwatch clock = Stopwatch.StartNew();
            List<Window> opened = [];
            while (clock.Elapsed < TimeSpan.FromSeconds(25))
            {
                Pump(3); Thread.Sleep(30);
                opened = AllOwned(window).Where(w => !before.Contains(w)).ToList();
                if (opened.Count > 0 && clock.Elapsed > TimeSpan.FromSeconds(2)) break;
            }
            _ = pending;
            Section("DIALOG " + name);
            Line("status after: " + Get<TextBlock>(window, "_statusText").Text);
            if (opened.Count == 0) { Line("   (no window opened)"); return; }
            int n = 0;
            foreach (Window w in opened)
            {
                Line($"window: '{w.Title}' {w.Width}x{w.Height} resizable={w.CanResize}");
                Shot(w, name + (n++ == 0 ? "" : "-" + n));
                Dump(w);
            }
            foreach (Window w in opened.AsEnumerable().Reverse())
                try { w.Close(); Pump(5); } catch (Exception e) { Line("   !! close threw: " + e.Message); }
            Pump(20);
        });
    }

    private static IEnumerable<Window> AllOwned(Window root)
    {
        foreach (Window w in root.OwnedWindows.ToArray())
        {
            yield return w;
            foreach (Window nested in AllOwned(w)) yield return nested;
        }
    }

    private static void Dump(Control? root)
    {
        if (root == null) { Line("   (no content)"); return; }
        foreach (Control c in root.GetLogicalDescendants().OfType<Control>())
            Describe(c);
    }

    private static void Describe(Control c)
    {
        bool visible = c.IsEffectivelyVisible;
        string flags = (visible ? "" : " [hidden]") + (c.IsEffectivelyEnabled ? "" : " [disabled]");
        string tip = ToolTip.GetTip(c) is string s && s.Length > 0 ? $"  tip=\"{Flat(s)}\"" : "";
        string? text = c switch
        {
            TextBlock t when t.Parent is not Button and not CheckBox and not RadioButton and not ComboBoxItem && !string.IsNullOrWhiteSpace(t.Text) => $"text: \"{Flat(t.Text!)}\"",
            CheckBox cb => $"check: \"{Flat(cb.Content?.ToString() ?? "")}\" ={cb.IsChecked}",
            RadioButton rb => $"radio: \"{Flat(rb.Content?.ToString() ?? "")}\" ={rb.IsChecked}",
            ToggleButton tb when tb.Parent is not Expander => $"toggle: \"{Flat(ContentText(tb.Content))}\" ={tb.IsChecked}",
            Button b => $"button: \"{Flat(ContentText(b.Content))}\"",
            Expander e => $"section: \"{Flat(ContentText(e.Header))}\" {(e.IsExpanded ? "open" : "closed")}",
            ComboBox combo => $"choice: selected=\"{Flat(combo.SelectedItem?.ToString() ?? "")}\" options=[{string.Join(" | ", combo.Items.Cast<object?>().Take(14).Select(i => Flat(ItemText(i))))}{(combo.ItemCount > 14 ? $" | …{combo.ItemCount} total" : "")}]",
            TextBox box => $"input: \"{Flat(box.Text ?? "")}\" placeholder=\"{Flat(box.PlaceholderText ?? "")}\"",
            Slider slider => $"slider: {slider.Minimum}..{slider.Maximum} ={slider.Value:0.##}",
            ListBox list => $"list: {list.ItemCount} items: {string.Join(" | ", list.Items.Cast<object?>().Take(6).Select(i => Flat(ItemText(i))))}",
            TabItem tab => $"tab: \"{tab.Header}\"",
            NumericUpDown num => $"number: {num.Value}",
            _ => null
        };
        if (text == null && tip.Length == 0) return;
        Line($"   {(text ?? c.GetType().Name)}{flags}{(string.IsNullOrEmpty(c.Name) ? "" : "  #" + c.Name)}{tip}");
    }

    private static string ItemText(object? item) => item switch
    {
        null => "",
        ComboBoxItem c => ContentText(c.Content),
        ListBoxItem l => ContentText(l.Content),
        Control c => string.Join(" / ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(3)),
        _ => item.ToString() ?? ""
    };

    private static string ContentText(object? content) => content switch
    {
        null => "",
        string s => s,
        TextBlock t => t.Text ?? "",
        Control c => string.Join(" / ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(4)),
        _ => content.ToString() ?? ""
    };

    private static string Flat(string s) => s.Replace("\r", "").Replace("\n", " ⏎ ");
    private static string Slug(string s) => new string(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
    private static void Section(string title) { Log.AppendLine(); Log.AppendLine("=== " + title); }
    private static void Line(string text) => Log.AppendLine(text);

    private static void Step(string label, Action action)
    {
        try { action(); }
        catch (Exception e)
        {
            Exception inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
            Line($"!! step '{label}' failed: {inner.GetType().Name}: {inner.Message}");
            Console.WriteLine($"audit step '{label}' failed: {inner.Message}");
        }
    }

    private static void Shot(Window window, string name)
    {
        window.InvalidateVisual(); Pump(3);
        using var image = window.CaptureRenderedFrame();
        string file = $"{++_shot:000}-{name}.png";
        image?.Save(Path.Combine(_out, file));
        Line("[screenshot] " + file);
    }

    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static T Get<T>(MainWindow w, string name) =>
        (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void WaitIdle(MainWindow window)
    {
        // A level is loaded and no load/selection work has been pending for a moment.
        var quiet = Stopwatch.StartNew();
        var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 ||
                        window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel == null ||
                        window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry == null;
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }
    private static void Wait(Func<bool> predicate, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        if (!predicate()) throw new TimeoutException("UI audit wait timed out.");
        Dispatcher.UIThread.RunJobs();
    }
}
