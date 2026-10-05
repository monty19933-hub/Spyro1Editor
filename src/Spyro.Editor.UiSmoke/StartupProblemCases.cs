using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;

/// <summary>
/// Two safeguards that have no level to load: the window shown when the project cannot be prepared at start
/// (instead of the process ending with nothing on screen), and the free-disk-space rule of Create BIN.
/// Usage: UiSmoke --startup-problem-ui &lt;output folder&gt;
/// </summary>
internal static class StartupProblemCases
{
    private static int _checks;

    internal static void Run(string outDir)
    {
        Directory.CreateDirectory(outDir);
        string file = Path.Combine(outDir, "support", "docs", "known-limitations.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "older copy");

        // 1. A support file the editor will not overwrite: name the file, say what to do, offer to show it.
        StartupProblem owned = StartupProblem.For(new InvalidDataException(
            "Application support would overwrite a differing project-owned file: " + file));
        Check(owned.Heading == "Spyro Editor could not open your project" && owned.Explanation.Contains(file, StringComparison.Ordinal) &&
              owned.Explanation.Contains("Move that file out of the project folder", StringComparison.Ordinal) &&
              owned.Explanation.Contains("nothing was changed", StringComparison.OrdinalIgnoreCase) && owned.RevealPath == file,
            "a support-file conflict names the file and the way out");
        StartupProblemWindow window = new(owned, Path.Combine(outDir, "logs"));
        window.Show(); Pump(20);
        Check(Text(window, "StartupProblemHeading") == owned.Heading && Text(window, "StartupProblemExplanation") == owned.Explanation,
            "the window shows the heading and the explanation");
        Check(Find<Button>(window, "StartupProblemShow") != null && Find<Button>(window, "StartupProblemQuit")!.IsDefault,
            "it offers to show the file, and Quit is the default button");
        using (var frame = window.CaptureRenderedFrame()) frame?.Save(Path.Combine(outDir, "1-support-file.png"));
        Button quit = Find<Button>(window, "StartupProblemQuit")!;
        quit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, quit)); Pump(10);
        Check(!window.IsVisible, "Quit closes the window");

        // 2. No permission for the project folder.
        StartupProblem denied = StartupProblem.For(new UnauthorizedAccessException("Access to the path '/Users/someone/Documents/Spyro Editor/Projects' is denied."));
        Check(denied.Heading.Contains("not allowed", StringComparison.Ordinal) && denied.RevealPath == null &&
              (!OperatingSystem.IsMacOS() || denied.Explanation.Contains("Privacy & Security", StringComparison.Ordinal)),
            "a permission problem explains where to allow access");
        StartupProblemWindow deniedWindow = new(denied, null);
        deniedWindow.Show(); Pump(20);
        Check(Find<Button>(deniedWindow, "StartupProblemShow") == null, "with nothing to show there is only Quit");
        using (var frame = deniedWindow.CaptureRenderedFrame()) frame?.Save(Path.Combine(outDir, "2-permission.png"));
        deniedWindow.Close(); Pump(5);

        // 3. Anything else keeps its message and says nothing was changed.
        StartupProblem other = StartupProblem.For(new InvalidOperationException("The release installation root could not be located."));
        Check(other.Explanation.Contains("The release installation root could not be located.", StringComparison.Ordinal) &&
              other.Explanation.Contains("Nothing was changed", StringComparison.Ordinal) && other.TechnicalDetail.Contains("InvalidOperationException", StringComparison.Ordinal),
            "any other failure keeps its message and its technical detail");

        // 4. Create BIN's free-space rule.
        const long image = 661_547_040, gb = 1024L * 1024 * 1024;
        Check(ExportScratchSpace.DescribeShortage(null, image, 3) == null, "unknown free space never blocks an export");
        Check(ExportScratchSpace.DescribeShortage(10 * gb, image, 5) == null, "plenty of room: no message");
        Check(ExportScratchSpace.RequiredFreeBytes(image, 3) == image * 3 + 256L * 1024 * 1024 &&
              ExportScratchSpace.RequiredFreeBytes(image, 5) == image * 5 + 256L * 1024 * 1024,
            "three working copies for ordinary edits, five with levels you built, plus a margin");
        string? shortage = ExportScratchSpace.DescribeShortage(gb + gb / 5, image, 5);
        Check(shortage != null && shortage.StartsWith("Create BIN needs about 3.3 GB of free disk space", StringComparison.Ordinal) &&
              shortage.Contains("Only 1.2 GB is free", StringComparison.Ordinal) && shortage.EndsWith("Nothing was changed.", StringComparison.Ordinal),
            "too little room is said in gigabytes: " + shortage);
        Check(ExportScratchSpace.DescribeShortage(ExportScratchSpace.RequiredFreeBytes(image, 3), image, 3) == null &&
              ExportScratchSpace.DescribeShortage(ExportScratchSpace.RequiredFreeBytes(image, 3) - 1, image, 3) != null,
            "the limit is exact");
        Check(ExportScratchSpace.FreeBytes(outDir) is > 0, "free space of a real folder can be read");
        Console.WriteLine($"STARTUP_PROBLEM_UI_OK checks={_checks}");
    }

    private static string? Text(Window window, string name) => Find<TextBlock>(window, name)?.Text;
    private static T? Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
    private static void Pump(int times) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Startup problem UI: " + label); }
}
