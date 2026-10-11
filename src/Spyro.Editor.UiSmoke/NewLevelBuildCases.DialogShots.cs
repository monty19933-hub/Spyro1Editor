using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;

/// <summary>The new commands driven the way a user drives them, through File ▾ and their dialogs (no test hooks), with a
/// picture of each step in the out folder: DIALOG_LEVEL (a level that can be replaced, e.g. icecavern) is rebuilt from
/// YOTD_LEVEL with File ▾ > New Level from Spyro: Year of the Dragon… (Make Level, then the ready dialog's OK), and
/// File ▾ > Fill In What the Level Needs… is opened on it and cancelled.</summary>
internal static partial class NewLevelBuildCases
{
    private static void DialogShots(MainWindow window, string outDir)
    {
        string key = Environment.GetEnvironmentVariable("DIALOG_LEVEL") ?? "icecavern";
        string yotd = Environment.GetEnvironmentVariable("YOTD_LEVEL") ?? "Molten Crater";
        Directory.CreateDirectory(outDir);
        Load(window, key);
        var line = Get<TextBlock>(window, "_completionText");
        Wait(() => line.IsVisible, "the 100% line", 180);
        Snapshot(window, Path.Combine(outDir, "01-level.png"));
        var file = Get<Button>(window, "_toolbarFileMenu");
        var menu = (MenuFlyout)file.Flyout!;
        menu.ShowAt(file); Pump(10);
        Snapshot(window, Path.Combine(outDir, "02-file-menu.png"));
        MenuItem Item(string header) => menu.Items.OfType<MenuItem>().First(i => (string)i.Header! == header);
        Console.WriteLine("File menu: " + string.Join(" | ", menu.Items.OfType<MenuItem>().Select(i => i.Header)));
        var import = Item("New Level from Spyro: Year of the Dragon…");
        menu.Hide(); Pump(5);
        import.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Wait(() => window.OwnedWindows.Any(w => w.Name == "YotdLevelDialog"), "level dialog", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "YotdLevelDialog");
        var pick = Find<ComboBox>(dialog, "YotdLevelChoice");
        pick.SelectedIndex = pick.ItemsSource!.Cast<object>().Select(o => o.ToString()).ToList().FindIndex(n => n == yotd); Pump(5);
        Snapshot(dialog, Path.Combine(outDir, "03-yotd-dialog.png"));
        Console.WriteLine("dialog: " + string.Join(" | ", Texts(dialog)));
        Click(Find<Button>(dialog, "YotdLevelConfirm"));
        bool shotProgress = false;
        Wait(() =>
        {
            if (!shotProgress && window.OwnedWindows.FirstOrDefault(w => w.Name == "OtherGameProgressDialog") is { } p)
            { Pump(30); Snapshot(p, Path.Combine(outDir, "04-progress.png")); shotProgress = true; }
            return window.OwnedWindows.Any(w => w.Name == "OtherGameLevelReadyDialog");
        }, "ready dialog", 1200);
        var ready = window.OwnedWindows.First(w => w.Name == "OtherGameLevelReadyDialog");
        Snapshot(ready, Path.Combine(outDir, "05-ready.png"));
        Console.WriteLine("ready: " + string.Join(" | ", Texts(ready)));
        Click(Find<Button>(ready, "OtherGameLevelReadyOk")); Pump(20);
        Snapshot(window, Path.Combine(outDir, "06-built.png"));
        Console.WriteLine("line: " + line.Text?.Replace("\n", " | "));
        menu.ShowAt(file); Pump(5); var fill = Item("Fill In What the Level Needs…"); menu.Hide(); Pump(5);
        fill.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Wait(() => window.OwnedWindows.Any(w => w.Name == "LevelFillDialog"), "fill dialog", 180);
        var fd = window.OwnedWindows.First(w => w.Name == "LevelFillDialog");
        Snapshot(fd, Path.Combine(outDir, "07-fill-dialog.png"));
        Console.WriteLine("fill dialog: " + string.Join(" | ", Texts(fd)));
        Click(Find<Button>(fd, "LevelFillCancel")); Pump(10);
        Console.WriteLine("NEW_LEVEL_DIALOG_SHOTS_OK");
    }

    private static IEnumerable<string> Texts(Control root) =>
        root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0)
            .Concat(root.GetLogicalDescendants().OfType<CheckBox>().Select(c => $"[{(c.IsChecked == true ? "x" : " ")}{(c.IsEnabled ? "" : " disabled")}] {c.Content}"));
}
