using Avalonia;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using System.Text.Json;

namespace Spyro.Editor.App;

internal static class Program
{
    private const string PackageCompressionSmokeArgument = "--package-native-compression-smoke";
    private const string ResearchWorkspaceBridgeSmokeArgument = "--research-workspace-bridge-smoke";
    private const string ResearchProjectSnapshotArgument = "--snapshot-current-project-for-research";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], PackageCompressionSmokeArgument, StringComparison.Ordinal))
            return RunPackageCompressionSmoke();
        if (args.Length == 1 && string.Equals(args[0], ResearchWorkspaceBridgeSmokeArgument, StringComparison.Ordinal))
            return RunResearchWorkspaceBridgeSmoke();
        if (args.Length == 1 && string.Equals(args[0], ResearchProjectSnapshotArgument, StringComparison.Ordinal))
            return RunResearchProjectSnapshot();

        string version = AppReleaseIdentity.DiagnosticVersion;
        EditorDiagnostics.Initialize(version);
        try
        {
            ResearchProjectWorkspaceBridgeResult researchProject =
                ResearchProjectWorkspaceBridge.TryActivate();
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
            else if (researchProject.Enabled)
            {
                EditorDiagnostics.RecordAction(
                    researchProject.Activated
                        ? "Protected release project opened in research mode"
                        : "Research workspace bridge used package fallback",
                    $"Workspace: {researchProject.WorkspaceRoot}; {researchProject.Reason}");
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

        return 0;
    }

    private static int RunResearchWorkspaceBridgeSmoke()
    {
        ResearchProjectWorkspaceBridgeResult result = ResearchProjectWorkspaceBridge.TryActivate();
        if (!result.Enabled || !result.Activated)
        {
            Console.Error.WriteLine($"FAIL: research workspace bridge: {result.Reason}");
            return 1;
        }

        Console.WriteLine($"PASS: research workspace bridge selected {result.WorkspaceRoot}");
        return 0;
    }

    private static int RunResearchProjectSnapshot()
    {
        try
        {
            ResearchProjectWorkspaceBridgeResult bridge = ResearchProjectWorkspaceBridge.TryActivate();
            if (!bridge.Enabled || !bridge.Activated)
                throw new InvalidOperationException(bridge.Reason);
            string manifestPath = Path.Combine(bridge.WorkspaceRoot, "spyro-project.json");
            EditorProjectManifest manifest = JsonSerializer.Deserialize<EditorProjectManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The protected project manifest is empty.");
            EditorProjectLayout project = new(bridge.WorkspaceRoot, manifest.ProjectId);
            EditorUserDataLayout userData = EditorUserDataLayout.CreateDefault();
            ProjectSnapshotResult snapshot = ProjectSnapshotService.CreateAsync(
                    project,
                    userData.BackupsPath,
                    "before-v4-research-shared-workspace")
                .GetAwaiter()
                .GetResult();
            Console.WriteLine(
                $"PASS: protected project snapshot verified: {snapshot.SnapshotPath} ({snapshot.FileCount} files; SHA-256 {snapshot.Sha256})");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: protected project snapshot: {exception.Message}");
            return 1;
        }
    }

    private static int RunPackageCompressionSmoke()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"spyro-editor-package-compression-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            string pngPath = Path.Combine(temporaryDirectory, "roundtrip.png");
            Rgba32[] expected =
            [
                new(0x12, 0x34, 0x56, 0xFF),
                new(0xAB, 0xCD, 0xEF, 0x80),
                new(0xFE, 0xDC, 0xBA, 0x40),
                new(0x01, 0x23, 0x45, 0x00)
            ];
            TerrainTexturePngWriter.WriteRgbaAsync(pngPath, 2, 2, expected).GetAwaiter().GetResult();
            Rgba32[] actual = PngRgbaImage.ReadRgba(pngPath, out int width, out int height);
            if (width != 2 || height != 2 || !actual.SequenceEqual(expected))
                throw new InvalidDataException("The packaged PNG compression round trip changed its dimensions or RGBA pixels.");

            Console.WriteLine("PASS: packaged native PNG compression round trip.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: packaged native PNG compression round trip: {exception}");
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory))
                    Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
                // The smoke result is determined by the compression round trip,
                // not by best-effort cleanup of its private temporary directory.
            }
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
