using Avalonia;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string version = AppReleaseIdentity.DiagnosticVersion;
        EditorDiagnostics.Initialize(version);
        try
        {
            ReleaseProjectContext? project = ReleaseProjectBootstrap.PrepareAsync(version).GetAwaiter().GetResult();
            if (project != null)
            {
                string migration = project.AutomaticMigration == null
                    ? "no portable project migration needed"
                    : $"migrated {project.AutomaticMigration.CopiedCount} project file(s) without overwriting the old folder";
                EditorDiagnostics.RecordAction(
                    "Persistent project storage ready",
                    $"Project: {project.Project.RootPath}; install: {project.InstallRoot}; {migration}");
            }
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordException("preparing persistent project storage", exception, fatal: true);
            throw;
        }
        RegisterExceptionLogging();

        bool cleanShutdown = false;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            cleanShutdown = true;
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordException("starting or running the editor", exception, fatal: true);
            throw;
        }
        finally
        {
            if (cleanShutdown)
                EditorDiagnostics.MarkCleanShutdown();
        }
    }

    private static void RegisterExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            Exception exception = eventArgs.ExceptionObject as Exception
                ?? new InvalidOperationException($"Unhandled runtime error: {eventArgs.ExceptionObject}");
            EditorDiagnostics.RecordException("an unhandled runtime operation", exception, fatal: eventArgs.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            EditorDiagnostics.RecordException("an unobserved background task", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            EditorDiagnostics.RecordException("a user-interface operation", eventArgs.Exception, fatal: true);
        };
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
