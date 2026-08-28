namespace Spyro.Editor.Core.Persistence;

public sealed record BundledMacAppWorkspaceBridgeResult(
    bool Activated,
    string WorkspaceRoot,
    string Reason);

/// <summary>
/// Keeps a directly opened macOS app inside its signed bundle until the user
/// explicitly selects or opens external project data. This avoids blocking the
/// first window on macOS Documents/Downloads folder authorization merely to
/// read catalogs that are already bundled beside the executable.
/// </summary>
public static class BundledMacAppWorkspaceBridge
{
    public static BundledMacAppWorkspaceBridgeResult TryActivate(
        string? appBaseDirectory = null,
        bool? isMacOs = null)
    {
        string configuredWorkspace =
            Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) ?? "";
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
        {
            return new BundledMacAppWorkspaceBridgeResult(
                Activated: false,
                WorkspaceRoot: configuredWorkspace,
                Reason: "An explicit editor workspace is already configured.");
        }

        if (!(isMacOs ?? OperatingSystem.IsMacOS()))
        {
            return new BundledMacAppWorkspaceBridgeResult(
                Activated: false,
                WorkspaceRoot: "",
                Reason: "The bundled direct-launch bridge only applies on macOS.");
        }

        try
        {
            string baseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(appBaseDirectory)
                ? AppContext.BaseDirectory
                : appBaseDirectory);
            if (!IsMacAppExecutableDirectory(baseDirectory))
            {
                return new BundledMacAppWorkspaceBridgeResult(
                    Activated: false,
                    WorkspaceRoot: "",
                    Reason: "The executable is not running from a macOS app bundle.");
            }

            if (!File.Exists(Path.Combine(baseDirectory, "spyro-level-catalog.json")) ||
                !File.Exists(Path.Combine(baseDirectory, "spyro-object-templates.json")))
            {
                return new BundledMacAppWorkspaceBridgeResult(
                    Activated: false,
                    WorkspaceRoot: "",
                    Reason: "The app bundle is missing its embedded editor catalogs.");
            }

            Environment.SetEnvironmentVariable(
                ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
                baseDirectory);
            return new BundledMacAppWorkspaceBridgeResult(
                Activated: true,
                WorkspaceRoot: baseDirectory,
                Reason: "Using the catalogs inside the app bundle for direct launch.");
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            return new BundledMacAppWorkspaceBridgeResult(
                Activated: false,
                WorkspaceRoot: "",
                Reason: $"The bundled direct-launch workspace could not be selected: {exception.Message}");
        }
    }

    private static bool IsMacAppExecutableDirectory(string path)
    {
        DirectoryInfo macOsDirectory = new(path.TrimEnd(Path.DirectorySeparatorChar));
        DirectoryInfo? contentsDirectory = macOsDirectory.Parent;
        DirectoryInfo? appBundle = contentsDirectory?.Parent;
        return macOsDirectory.Name.Equals("MacOS", StringComparison.Ordinal) &&
            contentsDirectory?.Name.Equals("Contents", StringComparison.Ordinal) == true &&
            appBundle?.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) == true;
    }
}
