using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Spyro.Editor.App.Views;

namespace Spyro.Editor.App;

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
    }

    /// <summary>Set by Program when the project could not be prepared: the editor then explains instead of opening.</summary>
    internal static StartupProblem? StartupProblem { get; set; }
    internal static string? StartupLogFolder { get; set; }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = StartupProblem != null
                ? new StartupProblemWindow(StartupProblem, StartupLogFolder)
                : new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
