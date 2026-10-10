using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Persistence;

// Play as Crash in the shipping editor's Game Settings, on tiny throwaway projects: one box per level Crash can play
// (none for flight levels), each tick saved at once to crash-player.json, the summary follows, a reopened editor shows
// the saved choice, and a damaged file is reported rather than overwritten. The Window is measured, not opened; no
// disc is read and no game is written.
internal static class CrashPlayerUiCases
{
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-crash-player-ui-").FullName;
        string root = Directory.CreateDirectory(Path.Combine(parent, "project")).FullName;
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
        {
            levels = new[] { new { key = "stonehill", displayName = "Stone Hill", levelId = 11 },
                new { key = "toasty", displayName = "Toasty", levelId = 14 },
                new { key = "sunnyflight", displayName = "Sunny Flight", levelId = 15 } }
        }));
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        string?[] previousRoots = [.. IsolatedRoots.Select(Environment.GetEnvironmentVariable)];
        string file = CrashPlayerSettings.PathFor(root);
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
            var window = Open();
            Check(Find<Control>(window, "PlayAsCrashSection").GetLogicalDescendants().OfType<CheckBox>().Count(b => b.Name?.StartsWith("CrashPlayerLevel", StringComparison.Ordinal) == true) == 2,
                "Play as Crash in Game Settings has a box for each level Crash can play");
            Check(window.GetLogicalDescendants().OfType<CheckBox>().All(b => b.Name != "CrashPlayerLevel15"), "flight levels have no box");
            CheckBox stone = Find<CheckBox>(window, "CrashPlayerLevel11"), toasty = Find<CheckBox>(window, "CrashPlayerLevel14");
            Check(stone.IsChecked == false && toasty.IsChecked == false && !File.Exists(file), "nothing is ticked or saved at first");
            Check(Summary(window) == "Spyro plays every level.", "the summary says Spyro plays every level: " + Summary(window));
            stone.IsChecked = true; Pump();
            Check(CrashPlayerSettings.Load(root).SetEquals(["stonehill"]), "ticking Stone Hill saves it");
            Check(Summary(window) == "Crash plays Stone Hill.", "the summary names it: " + Summary(window));
            toasty.IsChecked = true; Pump();
            Check(CrashPlayerSettings.Load(root).SetEquals(["stonehill", "toasty"]) && Summary(window) == "Crash plays Stone Hill and Toasty.",
                "a second tick adds Toasty: " + Summary(window));
            stone.IsChecked = false; Pump();
            Check(CrashPlayerSettings.Load(root).SetEquals(["toasty"]), "unticking Stone Hill removes it");
            var reopened = Open();
            Check(Find<CheckBox>(reopened, "CrashPlayerLevel14").IsChecked == true && Find<CheckBox>(reopened, "CrashPlayerLevel11").IsChecked == false,
                "a reopened editor shows the saved choice");
            Find<CheckBox>(reopened, "CrashPlayerLevel14").IsChecked = false; Pump();
            Check(!File.Exists(file), "unticking the last level removes the settings file");
            File.WriteAllText(file, "{ not json");
            var damaged = Open();
            Check(Summary(damaged).StartsWith("The saved choice could not be read", StringComparison.Ordinal) &&
                File.ReadAllText(file) == "{ not json", "a damaged file is reported and left as it was");
            Check(File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected" &&
                !Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any(), "no other file is touched and no game is written");
            Console.WriteLine($"PLAY_AS_CRASH_UI_OK checks={checks}");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
            for (int i = 0; i < IsolatedRoots.Length; i++) Environment.SetEnvironmentVariable(IsolatedRoots[i], previousRoots[i]);
            try { Directory.Delete(parent, recursive: true); } catch (IOException) { }
        }
    }

    // The editor's own data, project and log folders go to the throwaway folder too.
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    private static MainWindow Open()
    {
        var window = new MainWindow { Width = 1200, Height = 760 };
        window.Measure(new Size(1200, 760)); window.Arrange(new Rect(0, 0, 1200, 760)); Pump();
        return window;
    }
    private static string Summary(MainWindow window) => Find<TextBlock>(window, "CrashPlayerSummary").Text ?? "";
    private static T Find<T>(MainWindow window, string name) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException("Missing Play as Crash control: " + name);
    private static void Pump() { Dispatcher.UIThread.RunJobs(); }
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); checks++; }
}
