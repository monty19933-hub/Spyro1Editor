using System.Text.Json;

namespace Spyro.Editor.Core.Persistence;

public sealed record ResearchProjectWorkspaceBridgeResult(
    bool Enabled,
    bool Activated,
    string WorkspaceRoot,
    string Reason);

/// <summary>
/// Lets an explicitly opted-in packaged research build open the same protected
/// project already selected by the release editor. The bridge is read-only:
/// it validates existing registration, project, and support files, then changes
/// only the process-local workspace environment variable.
/// </summary>
public static class ResearchProjectWorkspaceBridge
{
    public const string UseCurrentReleaseProjectEnvironmentVariable =
        "SPYRO_EDITOR_USE_CURRENT_RELEASE_PROJECT";

    private const string CurrentProjectFileName = "current-project.json";

    public static ResearchProjectWorkspaceBridgeResult TryActivate(
        string? appBaseDirectory = null,
        EditorUserDataLayout? userData = null,
        string? explicitInstallRoot = null)
    {
        string fallbackWorkspace =
            Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) ?? "";
        if (!IsEnabled())
        {
            return new ResearchProjectWorkspaceBridgeResult(
                Enabled: false,
                Activated: false,
                WorkspaceRoot: fallbackWorkspace,
                Reason: "The current-release-project bridge was not requested.");
        }

        string releaseMode =
            Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable) ?? "";
        if (!IsExplicitResearchMode(releaseMode))
        {
            return new ResearchProjectWorkspaceBridgeResult(
                Enabled: true,
                Activated: false,
                WorkspaceRoot: fallbackWorkspace,
                Reason: "The bridge only runs when SPYRO_EDITOR_RELEASE is explicitly 0 or false.");
        }

        try
        {
            userData ??= EditorUserDataLayout.CreateDefault();
            string settingsPath = Path.Combine(userData.SettingsPath, CurrentProjectFileName);
            if (!File.Exists(settingsPath))
                return Fallback(fallbackWorkspace, "No protected release project is registered yet.");

            CurrentProjectSettings settings = JsonSerializer.Deserialize<CurrentProjectSettings>(
                    File.ReadAllText(settingsPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The current project registration is empty.");
            if (settings.Version != 1)
                throw new InvalidDataException($"Current project registration version {settings.Version} is unsupported.");
            if (string.IsNullOrWhiteSpace(settings.ProjectRoot) ||
                string.IsNullOrWhiteSpace(settings.ProjectId))
            {
                throw new InvalidDataException("The current project registration is incomplete.");
            }
            if (!Path.IsPathFullyQualified(settings.ProjectRoot))
                throw new InvalidDataException("The registered project path is not absolute.");

            string projectRoot = Path.GetFullPath(settings.ProjectRoot);
            if (!Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException($"The registered project folder does not exist: {projectRoot}");

            string installRoot = ReleaseProjectBootstrap.FindInstallRoot(
                explicitInstallRoot,
                appBaseDirectory);
            if (!string.IsNullOrWhiteSpace(installRoot))
            {
                PersistencePathSafety.EnsureTreesDoNotOverlap(
                    installRoot,
                    projectRoot,
                    "The registered project overlaps the replaceable research installation.");
            }
            PersistencePathSafety.EnsureTreesDoNotOverlap(
                projectRoot,
                userData.SettingsPath,
                "The registered project overlaps editor settings.");
            PersistencePathSafety.EnsureTreesDoNotOverlap(
                projectRoot,
                userData.UpdatesPath,
                "The registered project overlaps editor updates.");
            PersistencePathSafety.EnsureTreesDoNotOverlap(
                projectRoot,
                userData.BackupsPath,
                "The registered project overlaps editor backups.");

            string manifestPath = ValidateProjectFile(projectRoot, "spyro-project.json");
            EditorProjectManifest manifest = JsonSerializer.Deserialize<EditorProjectManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The registered project manifest is empty.");
            if (manifest.SchemaVersion < 1 ||
                manifest.SchemaVersion > EditorUserDataLayout.CurrentProjectSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Project schema {manifest.SchemaVersion} is unsupported by this editor.");
            }

            string normalizedProjectId = EditorUserDataLayout.NormalizeProjectId(manifest.ProjectId);
            if (!string.Equals(manifest.ProjectId, normalizedProjectId, StringComparison.Ordinal) ||
                !string.Equals(settings.ProjectId, manifest.ProjectId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The registered project identity does not match its protected manifest.");
            }

            ValidateProjectFile(projectRoot, Path.Combine("support", "spyro-level-catalog.json"));
            ValidateProjectFile(projectRoot, Path.Combine("support", "spyro-object-templates.json"));

            Environment.SetEnvironmentVariable(
                ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
                projectRoot);
            return new ResearchProjectWorkspaceBridgeResult(
                Enabled: true,
                Activated: true,
                WorkspaceRoot: projectRoot,
                Reason: "Opened the protected project currently selected by the release editor.");
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException or
            InvalidOperationException or
            NotSupportedException)
        {
            return Fallback(
                fallbackWorkspace,
                $"Protected release project was not used: {exception.Message}");
        }
    }

    private static string ValidateProjectFile(string projectRoot, string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        PersistencePathSafety.EnsureWritePathWithinRoot(
            projectRoot,
            path,
            $"A registered project file escapes the protected project: {relativePath}");
        if (!File.Exists(path))
            throw new FileNotFoundException($"The registered project is missing {relativePath}.", path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (!stream.CanRead)
            throw new IOException($"The registered project file is unreadable: {relativePath}");
        return path;
    }

    private static bool IsEnabled()
    {
        string value = Environment.GetEnvironmentVariable(
            UseCurrentReleaseProjectEnvironmentVariable) ?? "";
        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitResearchMode(string value) =>
        value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("false", StringComparison.OrdinalIgnoreCase);

    private static ResearchProjectWorkspaceBridgeResult Fallback(
        string fallbackWorkspace,
        string reason) =>
        new(
            Enabled: true,
            Activated: false,
            WorkspaceRoot: fallbackWorkspace,
            Reason: reason);
}
