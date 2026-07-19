using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Persistence;

public sealed class EditorUserDataLayout
{
    public const int CurrentProjectSchemaVersion = 1;
    public const string DataRootEnvironmentVariable = "SPYRO_EDITOR_DATA_ROOT";
    public const string ProjectsRootEnvironmentVariable = "SPYRO_EDITOR_PROJECTS_ROOT";

    public EditorUserDataLayout(
        string rootPath,
        string? projectsPath = null,
        bool allowProjectsFallback = false)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("A user-data root is required.", nameof(rootPath));

        RootPath = Path.GetFullPath(rootPath);
        ProjectsPath = Path.GetFullPath(string.IsNullOrWhiteSpace(projectsPath)
            ? Path.Combine(RootPath, "Projects")
            : projectsPath);
        AllowProjectsFallback = allowProjectsFallback;
    }

    public string RootPath { get; }
    public string ProjectsPath { get; }
    public bool AllowProjectsFallback { get; }
    public string SettingsPath => Path.Combine(RootPath, "Settings");
    public string UpdatesPath => Path.Combine(RootPath, "Updates");
    public string BackupsPath => Path.Combine(RootPath, "Backups");

    public static EditorUserDataLayout CreateDefault(string? overrideRoot = null)
    {
        string configured = string.IsNullOrWhiteSpace(overrideRoot)
            ? Environment.GetEnvironmentVariable(DataRootEnvironmentVariable) ?? ""
            : overrideRoot;
        string configuredProjects = Environment.GetEnvironmentVariable(ProjectsRootEnvironmentVariable) ?? "";
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new EditorUserDataLayout(
                configured,
                string.IsNullOrWhiteSpace(configuredProjects) ? null : configuredProjects);
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Path.Combine(home, "Documents");
        string projects = string.IsNullOrWhiteSpace(configuredProjects)
            ? Path.Combine(documents, "Spyro Editor", "Projects")
            : configuredProjects;
        if (OperatingSystem.IsMacOS())
        {
            return new EditorUserDataLayout(
                Path.Combine(home, "Library", "Application Support", "Spyro Editor"),
                projects,
                allowProjectsFallback: string.IsNullOrWhiteSpace(configuredProjects));
        }

        if (OperatingSystem.IsWindows())
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
                local = Path.Combine(home, "AppData", "Local");
            return new EditorUserDataLayout(
                Path.Combine(local, "Spyro Editor"),
                projects,
                allowProjectsFallback: string.IsNullOrWhiteSpace(configuredProjects));
        }

        string xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "";
        if (string.IsNullOrWhiteSpace(xdg))
            xdg = Path.Combine(home, ".local", "share");
        return new EditorUserDataLayout(
            Path.Combine(xdg, "spyro-editor"),
            projects,
            allowProjectsFallback: string.IsNullOrWhiteSpace(configuredProjects));
    }

    public EditorProjectLayout GetProject(string projectId)
    {
        string normalizedId = NormalizeProjectId(projectId);
        return new EditorProjectLayout(Path.Combine(ProjectsPath, normalizedId), normalizedId);
    }

    public async Task<EditorProjectLayout> EnsureProjectAsync(
        string projectId,
        string displayName,
        string? migratedFrom = null,
        CancellationToken cancellationToken = default)
    {
        EditorProjectLayout project = GetProject(projectId);
        Directory.CreateDirectory(project.RootPath);
        Directory.CreateDirectory(project.OutputPath);
        Directory.CreateDirectory(project.LocalPath);

        if (!File.Exists(project.ManifestPath))
        {
            EditorProjectManifest manifest = new(
                SchemaVersion: CurrentProjectSchemaVersion,
                ProjectId: project.ProjectId,
                DisplayName: string.IsNullOrWhiteSpace(displayName) ? "My Spyro Project" : displayName.Trim(),
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: "",
                MigratedFrom: migratedFrom ?? "");
            await WriteJsonAtomicallyAsync(project.ManifestPath, manifest, cancellationToken);
        }
        else
        {
            EditorProjectManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<EditorProjectManifest>(
                    await File.ReadAllTextAsync(project.ManifestPath, cancellationToken),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new JsonException("Project manifest is empty.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidDataException($"The existing project manifest cannot be read: {project.ManifestPath}", exception);
            }

            if (!string.Equals(manifest.ProjectId, project.ProjectId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Project folder identity mismatch. Expected '{project.ProjectId}', but the manifest identifies '{manifest.ProjectId}'.");
            }
            if (manifest.SchemaVersion > CurrentProjectSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Project schema {manifest.SchemaVersion} is newer than this editor supports ({CurrentProjectSchemaVersion}).");
            }
        }

        return project;
    }

    public static string NormalizeProjectId(string projectId)
    {
        string value = string.IsNullOrWhiteSpace(projectId)
            ? "default"
            : projectId.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        Span<char> buffer = stackalloc char[value.Length];
        int length = 0;
        bool pendingSeparator = false;
        foreach (char character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator && length > 0)
                    buffer[length++] = '-';
                buffer[length++] = character;
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = length > 0;
            }
        }

        string normalized = new(buffer[..length]);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "default";
        bool alreadySafe = value.Length <= 64 && string.Equals(value, normalized, StringComparison.Ordinal);
        if (alreadySafe)
            return normalized;

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..10];
        const int maximumLength = 64;
        int maximumSlugLength = maximumLength - hash.Length - 1;
        string slug = normalized.Length <= maximumSlugLength
            ? normalized
            : normalized[..maximumSlugLength].TrimEnd('-');
        if (string.IsNullOrWhiteSpace(slug))
            slug = "project";
        return $"{slug}-{hash}";
    }

    internal static async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(output, value, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

public sealed record EditorProjectLayout(string RootPath, string ProjectId)
{
    public string ManifestPath => Path.Combine(RootPath, "spyro-project.json");
    public string OutputPath => Path.Combine(RootPath, "output");
    public string CachePath => Path.Combine(RootPath, "editor-cache");
    public string LocalPath => Path.Combine(RootPath, "_local");
    public string CustomSkyboxesPath => Path.Combine(RootPath, "custom-skyboxes");
    public string MigrationPath => Path.Combine(RootPath, "_migration");
};

public sealed record EditorProjectManifest(
    int SchemaVersion,
    string ProjectId,
    string DisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastOpenedAtUtc,
    string LastEditorVersion,
    string MigratedFrom);
