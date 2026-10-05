using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Spyro.Editor.App.Views;

/// <summary>
/// What the editor shows when it cannot get to its project at start. Without this the process simply ended
/// and nothing appeared on screen; the reasons are ordinary ones (a folder permission, a file in the way)
/// that the user can fix once told.
/// </summary>
public sealed record StartupProblem(string Heading, string Explanation, string? RevealPath, string TechnicalDetail)
{
    public static StartupProblem For(Exception exception)
    {
        Exception root = exception is AggregateException { InnerExceptions.Count: 1 } aggregate ? aggregate.InnerExceptions[0] : exception;
        string message = root.Message ?? "";
        const string ownedByProject = "Application support would overwrite a differing project-owned file: ";
        const string changedInProject = "An application-owned support file was modified in the project and was preserved: ";
        foreach (string prefix in new[] { ownedByProject, changedInProject })
        {
            if (!message.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            string path = message[prefix.Length..].Trim();
            return new(
                "Spyro Editor could not open your project",
                "A file in your project's support folder is different from the one this version of the editor provides, " +
                "and the editor does not overwrite files it did not put there:\n\n" + path + "\n\n" +
                "Move that file out of the project folder, then open the editor again; the editor puts its own copy in place. " +
                "Your levels and saved changes are not affected, and nothing was changed.",
                path, root.ToString());
        }
        if (root is UnauthorizedAccessException || root.InnerException is UnauthorizedAccessException)
        {
            return new(
                "Spyro Editor is not allowed to use its project folder",
                OperatingSystem.IsMacOS()
                    ? "macOS has not allowed Spyro Editor to use the folder where your projects are kept (Documents/Spyro Editor).\n\n" +
                      "Open System Settings › Privacy & Security › Files and Folders, find Spyro Editor and switch on Documents Folder. " +
                      "Then open the editor again. Nothing was changed."
                    : "The editor could not read or write the folder where your projects are kept (Documents/Spyro Editor). " +
                      "Check that you may read and write that folder, then open the editor again. Nothing was changed.",
                null, root.ToString());
        }
        return new(
            "Spyro Editor could not open your project",
            "Something stopped the editor while it was preparing your project:\n\n" + message + "\n\n" +
            "Nothing was changed. If this keeps happening, the technical details below help to find the cause.",
            null, root.ToString());
    }
}

public sealed class StartupProblemWindow : Window
{
    public StartupProblemWindow(StartupProblem problem, string? logFolder)
    {
        Title = "Spyro Editor";
        Width = 620;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        StackPanel content = new() { Spacing = 14, Margin = new Thickness(24) };
        content.Children.Add(new TextBlock
        {
            Name = "StartupProblemHeading", Text = problem.Heading, FontSize = 20, FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Name = "StartupProblemExplanation", Text = problem.Explanation, TextWrapping = TextWrapping.Wrap, LineHeight = 21
        });
        if (!string.IsNullOrWhiteSpace(logFolder))
        {
            content.Children.Add(new TextBlock
            {
                Text = "A log of this start was written to:\n" + logFolder, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 106, 118))
            });
        }
        content.Children.Add(new Expander
        {
            Header = "Technical details", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new ScrollViewer
            {
                MaxHeight = 160,
                Content = new SelectableTextBlock { Text = problem.TechnicalDetail, TextWrapping = TextWrapping.Wrap, FontSize = 11 }
            }
        });
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        string? reveal = problem.RevealPath is { Length: > 0 } path && (File.Exists(path) || Directory.Exists(path)) ? path : null;
        if (reveal != null)
        {
            Button show = new() { Name = "StartupProblemShow", Content = OperatingSystem.IsMacOS() ? "Show in Finder" : "Show the File" };
            show.Click += (_, _) => Reveal(reveal);
            buttons.Children.Add(show);
        }
        Button quit = new() { Name = "StartupProblemQuit", Content = "Quit", IsDefault = true, IsCancel = true, MinWidth = 90 };
        quit.Click += (_, _) => Close();
        buttons.Children.Add(quit);
        content.Children.Add(buttons);
        Content = content;
    }

    private static void Reveal(string path)
    {
        try
        {
            System.Diagnostics.ProcessStartInfo start;
            if (OperatingSystem.IsMacOS())
            {
                start = new("open") { UseShellExecute = false };
                start.ArgumentList.Add("-R"); start.ArgumentList.Add(path);
            }
            else if (OperatingSystem.IsWindows())
                start = new("explorer.exe") { UseShellExecute = false, Arguments = "/select,\"" + path + "\"" };
            else
            {
                start = new("xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
            }
            System.Diagnostics.Process.Start(start)?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { }
    }
}
