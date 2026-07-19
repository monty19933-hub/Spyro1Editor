using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Persistence;

public sealed record ReleaseProjectContext(
    string InstallRoot,
    EditorUserDataLayout UserData,
    EditorProjectLayout Project,
    string AppVersion,
    PortableProjectMigrationResult? AutomaticMigration);

public sealed record CurrentProjectSettings(
    int Version,
    string ProjectId,
    string ProjectRoot,
    DateTimeOffset SavedAtUtc);

public sealed record AutomaticPortableMigrationMarker(
    int Version,
    string SourceRoot,
    DateTimeOffset CompletedAtUtc,
    string ReportPath);

internal sealed record SynchronizedSupportManifest(
    int Version,
    IReadOnlyList<string> Files);

public static class ReleaseProjectBootstrap
{
    public const string InstallRootEnvironmentVariable = "SPYRO_EDITOR_INSTALL_ROOT";
    public const string ReleaseEnvironmentVariable = "SPYRO_EDITOR_RELEASE";
    public const string WorkspaceEnvironmentVariable = "SPYRO_EDITOR_WORKSPACE";
    private const string CurrentProjectFileName = "current-project.json";

    public static ReleaseProjectContext? Current { get; private set; }

    public static async Task<ReleaseProjectContext?> PrepareAsync(
        string appVersion,
        string? appBaseDirectory = null,
        EditorUserDataLayout? userData = null,
        string? explicitInstallRoot = null,
        bool? forceReleaseMode = null,
        CancellationToken cancellationToken = default)
    {
        string baseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory);
        string installRoot = FindInstallRoot(explicitInstallRoot, baseDirectory);
        bool releaseMode = forceReleaseMode ?? ResolveReleaseMode(installRoot);
        if (!releaseMode)
            return null;
        if (string.IsNullOrWhiteSpace(installRoot))
            throw new InvalidOperationException("The release installation root could not be located.");

        userData ??= EditorUserDataLayout.CreateDefault();
        EnsureUserDataDoesNotOverlapInstall(installRoot, userData);
        PersistencePathSafety.EnsureWritePathWithinRoot(userData.RootPath, userData.RootPath);
        PersistencePathSafety.EnsureWritePathWithinRoot(userData.RootPath, userData.SettingsPath);
        PersistencePathSafety.EnsureWritePathWithinRoot(userData.RootPath, userData.UpdatesPath);
        PersistencePathSafety.EnsureWritePathWithinRoot(userData.RootPath, userData.BackupsPath);
        Directory.CreateDirectory(userData.RootPath);
        Directory.CreateDirectory(userData.SettingsPath);
        Directory.CreateDirectory(userData.UpdatesPath);
        Directory.CreateDirectory(userData.BackupsPath);

        string configuredWorkspace = Environment.GetEnvironmentVariable(WorkspaceEnvironmentVariable) ?? "";
        string registeredWorkspace = ReadCurrentProjectRoot(userData);
        string projectRoot = SelectConfiguredProjectRoot(
            installRoot,
            userData,
            configuredWorkspace,
            registeredWorkspace);
        EditorProjectLayout project;
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            (userData, project) = await EnsureDefaultProjectAsync(
                installRoot,
                userData,
                cancellationToken);
        }
        else
        {
            EnsureProjectDoesNotOverlapInstall(installRoot, projectRoot);
            EnsureProjectDoesNotOverlapUserServices(userData, projectRoot);
            project = await EnsureProjectAtPathAsync(
                projectRoot,
                Path.GetFileName(projectRoot),
                appVersion,
                cancellationToken);
        }

        PortableProjectMigrationResult? migration = null;
        string migrationMarkerPath = AutomaticMigrationMarkerPath(project, installRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(project.RootPath, migrationMarkerPath);
        if (!File.Exists(migrationMarkerPath) && HasPortableProjectData(installRoot))
        {
            migration = await PortableProjectMigration.MigrateAsync(
                installRoot,
                project,
                new PortableProjectMigrationOptions(
                    IncludeGeneratedOutputs: false,
                    IncludeRebuildableCache: false,
                    IncludeUserResearch: false),
                cancellationToken);
            await EditorUserDataLayout.WriteJsonAtomicallyAsync(
                migrationMarkerPath,
                new AutomaticPortableMigrationMarker(
                    Version: 1,
                    SourceRoot: Path.GetFullPath(installRoot),
                    CompletedAtUtc: DateTimeOffset.UtcNow,
                    ReportPath: migration.ReportPath),
                cancellationToken);
        }

        await SynchronizeSupportFilesAsync(installRoot, project.RootPath, cancellationToken);
        await TouchManifestAsync(project, appVersion, cancellationToken);
        await RememberProjectAsync(userData, project, cancellationToken);

        Environment.SetEnvironmentVariable(InstallRootEnvironmentVariable, installRoot);
        Environment.SetEnvironmentVariable(ReleaseEnvironmentVariable, "1");
        Environment.SetEnvironmentVariable(WorkspaceEnvironmentVariable, project.RootPath);
        Current = new ReleaseProjectContext(installRoot, userData, project, appVersion, migration);
        return Current;
    }

    public static async Task<EditorProjectLayout> PrepareAndRememberProjectAsync(
        string projectRoot,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ReleaseProjectContext context = Current
            ?? throw new InvalidOperationException("Release project storage is not initialized.");
        EnsureProjectDoesNotOverlapInstall(context.InstallRoot, projectRoot);
        EnsureProjectDoesNotOverlapUserServices(context.UserData, projectRoot);
        EditorProjectLayout project = await EnsureProjectAtPathAsync(
            projectRoot,
            string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(projectRoot) : displayName,
            context.AppVersion,
            cancellationToken);
        await SynchronizeSupportFilesAsync(context.InstallRoot, project.RootPath, cancellationToken);
        await RememberProjectAsync(context.UserData, project, cancellationToken);
        Environment.SetEnvironmentVariable(WorkspaceEnvironmentVariable, project.RootPath);
        Current = context with { Project = project, AutomaticMigration = null };
        return project;
    }

    public static async Task<PortableProjectMigrationResult> ImportPortableProjectAsync(
        string sourceRoot,
        bool includeGeneratedOutputs,
        bool includeUserResearch = true,
        CancellationToken cancellationToken = default)
    {
        ReleaseProjectContext context = Current
            ?? throw new InvalidOperationException("Release project storage is not initialized.");
        string portableInstallRoot = RequirePortableInstallRoot(sourceRoot);
        PortableProjectMigrationResult result = await PortableProjectMigration.MigrateAsync(
            portableInstallRoot,
            context.Project,
            new PortableProjectMigrationOptions(
                IncludeGeneratedOutputs: includeGeneratedOutputs,
                IncludeRebuildableCache: false,
                IncludeUserResearch: includeUserResearch),
            cancellationToken);
        await SynchronizeSupportFilesAsync(context.InstallRoot, context.Project.RootPath, cancellationToken);
        await TouchManifestAsync(context.Project, context.AppVersion, cancellationToken);
        return result;
    }

    public static string FindInstallRoot(string? explicitInstallRoot = null, string? appBaseDirectory = null)
    {
        string configured = string.IsNullOrWhiteSpace(explicitInstallRoot)
            ? Environment.GetEnvironmentVariable(InstallRootEnvironmentVariable) ?? ""
            : explicitInstallRoot;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string fullConfigured = Path.GetFullPath(configured);
            if (IsInstallRoot(fullConfigured))
                return fullConfigured;
        }

        string start = Path.GetFullPath(string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory);
        DirectoryInfo? cursor = new(start);
        string discoveredRoot = "";
        while (cursor != null)
        {
            if (IsInstallRoot(cursor.FullName))
                discoveredRoot = cursor.FullName;
            cursor = cursor.Parent;
        }

        return discoveredRoot;
    }

    private static bool ResolveReleaseMode(string installRoot)
    {
        string configured = Environment.GetEnvironmentVariable(ReleaseEnvironmentVariable) ?? "";
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return !string.Equals(configured, "0", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(configured, "false", StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(installRoot) &&
            !File.Exists(Path.Combine(installRoot, "Launch Spyro Editor Research.command")) &&
            !File.Exists(Path.Combine(installRoot, "Launch Spyro Editor Research.bat"));
    }

    private static bool IsInstallRoot(string path)
    {
        return Directory.Exists(path) &&
            File.Exists(Path.Combine(path, "support", "spyro-level-catalog.json")) &&
            File.Exists(Path.Combine(path, "support", "spyro-object-templates.json"));
    }

    private static string SelectConfiguredProjectRoot(
        string installRoot,
        EditorUserDataLayout userData,
        params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                continue;
            }
            EnsureProjectDoesNotOverlapInstall(installRoot, fullPath);
            EnsureProjectDoesNotOverlapUserServices(userData, fullPath);
            return fullPath;
        }
        return "";
    }

    private static void EnsureProjectDoesNotOverlapInstall(string installRoot, string projectRoot)
    {
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            installRoot,
            projectRoot,
            "Project storage must not be equal to, inside, physically linked into, or contain the replaceable editor installation.");
    }

    private static void EnsureProjectDoesNotOverlapUserServices(
        EditorUserDataLayout userData,
        string projectRoot)
    {
        const string message =
            "Project storage must not overlap editor settings, update downloads, or safety backups.";
        PersistencePathSafety.EnsureTreesDoNotOverlap(projectRoot, userData.SettingsPath, message);
        PersistencePathSafety.EnsureTreesDoNotOverlap(projectRoot, userData.UpdatesPath, message);
        PersistencePathSafety.EnsureTreesDoNotOverlap(projectRoot, userData.BackupsPath, message);
    }

    private static async Task<(EditorUserDataLayout UserData, EditorProjectLayout Project)> EnsureDefaultProjectAsync(
        string installRoot,
        EditorUserDataLayout userData,
        CancellationToken cancellationToken)
    {
        EditorProjectLayout primaryProject = userData.GetProject("default-project");
        EnsureProjectDoesNotOverlapInstall(installRoot, primaryProject.RootPath);
        EnsureProjectDoesNotOverlapUserServices(userData, primaryProject.RootPath);
        try
        {
            EditorProjectLayout project = await userData.EnsureProjectAsync(
                "default-project",
                "Default Project",
                cancellationToken: cancellationToken);
            return (userData, project);
        }
        catch (Exception exception) when (
            userData.AllowProjectsFallback && IsProjectStorageAccessFailure(exception))
        {
            string fallbackProjectsPath = Path.Combine(userData.RootPath, "Projects");
            if (PathsEqualLexically(userData.ProjectsPath, fallbackProjectsPath))
                throw;

            EditorUserDataLayout fallbackUserData = new(
                userData.RootPath,
                fallbackProjectsPath,
                allowProjectsFallback: false);
            EnsureUserDataDoesNotOverlapInstall(installRoot, fallbackUserData);
            EditorProjectLayout fallbackProject = fallbackUserData.GetProject("default-project");
            EnsureProjectDoesNotOverlapInstall(installRoot, fallbackProject.RootPath);
            EnsureProjectDoesNotOverlapUserServices(fallbackUserData, fallbackProject.RootPath);
            fallbackProject = await fallbackUserData.EnsureProjectAsync(
                "default-project",
                "Default Project",
                cancellationToken: cancellationToken);
            return (fallbackUserData, fallbackProject);
        }
    }

    private static bool IsProjectStorageAccessFailure(Exception exception)
    {
        if (exception is IOException or UnauthorizedAccessException)
            return true;
        return exception is InvalidDataException
        {
            InnerException: IOException or UnauthorizedAccessException
        };
    }

    private static string RequirePortableInstallRoot(string sourceRoot)
    {
        string root;
        try
        {
            root = Path.GetFullPath(sourceRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            throw new InvalidDataException("Choose a valid Spyro Editor Beta installation folder.", exception);
        }

        bool hasKnownLauncher =
            File.Exists(Path.Combine(root, "Launch Spyro Editor.command")) ||
            File.Exists(Path.Combine(root, "Launch Spyro Editor.bat"));
        if (!IsInstallRoot(root) || !hasKnownLauncher)
        {
            throw new InvalidDataException(
                "That folder is not a portable Spyro Editor Beta installation. Choose the folder containing the Spyro Editor launcher and support catalogs.");
        }
        return root;
    }

    private static void EnsureUserDataDoesNotOverlapInstall(
        string installRoot,
        EditorUserDataLayout userData)
    {
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            installRoot,
            userData.RootPath,
            "Editor settings and update storage must not overlap the replaceable editor installation.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            installRoot,
            userData.ProjectsPath,
            "The projects storage root must not overlap the replaceable editor installation.");
    }

    private static string ReadCurrentProjectRoot(EditorUserDataLayout userData)
    {
        string path = Path.Combine(userData.SettingsPath, CurrentProjectFileName);
        try
        {
            if (!File.Exists(path))
                return "";
            CurrentProjectSettings? settings = JsonSerializer.Deserialize<CurrentProjectSettings>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return settings?.ProjectRoot ?? "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return "";
        }
    }

    private static async Task RememberProjectAsync(
        EditorUserDataLayout userData,
        EditorProjectLayout project,
        CancellationToken cancellationToken)
    {
        CurrentProjectSettings settings = new(
            Version: 1,
            ProjectId: project.ProjectId,
            ProjectRoot: project.RootPath,
            SavedAtUtc: DateTimeOffset.UtcNow);
        string settingsPath = Path.Combine(userData.SettingsPath, CurrentProjectFileName);
        PersistencePathSafety.EnsureWritePathWithinRoot(userData.RootPath, settingsPath);
        await EditorUserDataLayout.WriteJsonAtomicallyAsync(
            settingsPath,
            settings,
            cancellationToken);
    }

    private static async Task<EditorProjectLayout> EnsureProjectAtPathAsync(
        string projectRoot,
        string? displayName,
        string appVersion,
        CancellationToken cancellationToken)
    {
        string root = Path.GetFullPath(projectRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(root, root);
        Directory.CreateDirectory(root);
        string manifestPath = Path.Combine(root, "spyro-project.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(root, manifestPath);
        EditorProjectLayout project;
        if (File.Exists(manifestPath))
        {
            EditorProjectManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<EditorProjectManifest>(
                    await File.ReadAllTextAsync(manifestPath, cancellationToken),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new JsonException("Project manifest is empty.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidDataException($"The existing project manifest cannot be read: {manifestPath}", exception);
            }

            string normalizedProjectId = EditorUserDataLayout.NormalizeProjectId(manifest.ProjectId);
            if (!string.Equals(manifest.ProjectId, normalizedProjectId, StringComparison.Ordinal))
                throw new InvalidDataException($"The existing project manifest has an unsafe project ID: {manifest.ProjectId}");
            if (manifest.SchemaVersion > EditorUserDataLayout.CurrentProjectSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Project schema {manifest.SchemaVersion} is newer than this editor supports ({EditorUserDataLayout.CurrentProjectSchemaVersion}).");
            }
            project = new EditorProjectLayout(root, manifest.ProjectId);
        }
        else
        {
            string projectId = EditorUserDataLayout.NormalizeProjectId(Path.GetFileName(root));
            project = new EditorProjectLayout(root, projectId);
            EditorProjectManifest manifest = new(
                SchemaVersion: EditorUserDataLayout.CurrentProjectSchemaVersion,
                ProjectId: project.ProjectId,
                DisplayName: string.IsNullOrWhiteSpace(displayName) ? "My Spyro Project" : displayName.Trim(),
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: appVersion,
                MigratedFrom: "");
            await EditorUserDataLayout.WriteJsonAtomicallyAsync(project.ManifestPath, manifest, cancellationToken);
        }
        PersistencePathSafety.EnsureWritePathWithinRoot(root, project.OutputPath);
        PersistencePathSafety.EnsureWritePathWithinRoot(root, project.LocalPath);
        Directory.CreateDirectory(project.OutputPath);
        Directory.CreateDirectory(project.LocalPath);
        return project;
    }

    private static async Task TouchManifestAsync(
        EditorProjectLayout project,
        string appVersion,
        CancellationToken cancellationToken)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(project.RootPath, project.ManifestPath);
        EditorProjectManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<EditorProjectManifest>(File.ReadAllText(project.ManifestPath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new JsonException("Project manifest is empty.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException(
                $"The project manifest changed or became unreadable before it could be updated: {project.ManifestPath}",
                exception);
        }

        await EditorUserDataLayout.WriteJsonAtomicallyAsync(
            project.ManifestPath,
            manifest with
            {
                SchemaVersion = EditorUserDataLayout.CurrentProjectSchemaVersion,
                ProjectId = project.ProjectId,
                DisplayName = string.IsNullOrWhiteSpace(manifest.DisplayName) ? "My Spyro Project" : manifest.DisplayName,
                CreatedAtUtc = manifest.CreatedAtUtc == default ? DateTimeOffset.UtcNow : manifest.CreatedAtUtc,
                LastOpenedAtUtc = DateTimeOffset.UtcNow,
                LastEditorVersion = appVersion,
                MigratedFrom = manifest.MigratedFrom ?? ""
            },
            cancellationToken);
    }

    private static async Task SynchronizeSupportFilesAsync(
        string installRoot,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        string sourceRoot = Path.Combine(installRoot, "support");
        string destinationRoot = Path.Combine(projectRoot, "support");
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            sourceRoot,
            destinationRoot,
            "Application support files cannot be synchronized between overlapping source and project trees.");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationRoot);

        string manifestPath = Path.Combine(projectRoot, "_local", ".app", "support-ownership.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, manifestPath);
        SynchronizedSupportManifest? previousManifest = await ReadSupportManifestAsync(manifestPath, cancellationToken);
        List<(string SourcePath, string RelativePath)> sourceFiles = Directory
            .EnumerateFiles(sourceRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false
            })
            .Select(sourcePath => (
                SourcePath: sourcePath,
                RelativePath: NormalizeSupportRelativePath(Path.GetRelativePath(sourceRoot, sourcePath))))
            .Where(file => !file.RelativePath.Equals("app", StringComparison.OrdinalIgnoreCase) &&
                !file.RelativePath.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach ((string sourcePath, string relativePath) in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destinationPath = SafeSupportDestinationPath(projectRoot, destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            string temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, temporaryPath);
            try
            {
                await using FileStream input = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                await using (FileStream output = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        HashSet<string> currentFiles = sourceFiles
            .Select(file => file.RelativePath)
            .ToHashSet(SupportPathComparer);
        if (previousManifest != null)
        {
            foreach (string retiredRelativePath in previousManifest.Files
                .Where(path => !currentFiles.Contains(NormalizeSupportRelativePath(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string retiredPath = SafeSupportDestinationPath(
                    projectRoot,
                    destinationRoot,
                    NormalizeSupportRelativePath(retiredRelativePath));
                if (Directory.Exists(retiredPath))
                {
                    throw new InvalidDataException(
                        $"A retired application-owned support file became a directory and was not removed: {retiredPath}");
                }
                if (!File.Exists(retiredPath))
                    continue;
                File.Delete(retiredPath);
                DeleteEmptySupportParents(projectRoot, destinationRoot, Path.GetDirectoryName(retiredPath));
            }
        }

        await EditorUserDataLayout.WriteJsonAtomicallyAsync(
            manifestPath,
            new SynchronizedSupportManifest(Version: 1, Files: sourceFiles.Select(file => file.RelativePath).ToArray()),
            cancellationToken);
    }

    private static async Task<SynchronizedSupportManifest?> ReadSupportManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
            return null;
        try
        {
            SynchronizedSupportManifest manifest = JsonSerializer.Deserialize<SynchronizedSupportManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new JsonException("Support ownership manifest is empty.");
            if (manifest.Version != 1 || manifest.Files == null)
                throw new JsonException("Support ownership manifest has an unsupported schema.");
            return manifest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException(
                $"The application support ownership manifest cannot be read safely: {manifestPath}",
                exception);
        }
    }

    private static string SafeSupportDestinationPath(
        string projectRoot,
        string destinationRoot,
        string relativePath)
    {
        string normalized = NormalizeSupportRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"The support ownership manifest contains an unsafe path: {relativePath}");
        }

        string destinationPath = Path.GetFullPath(Path.Combine(
            destinationRoot,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        PersistencePathSafety.EnsureWritePathWithinRoot(
            projectRoot,
            destinationPath,
            $"An application support write would escape the active project: {relativePath}");
        return destinationPath;
    }

    private static void DeleteEmptySupportParents(
        string projectRoot,
        string destinationRoot,
        string? directoryPath)
    {
        string cursor = directoryPath ?? "";
        while (!string.IsNullOrWhiteSpace(cursor) &&
            !PathsEqualLexically(cursor, destinationRoot))
        {
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, cursor);
            if (!Directory.Exists(cursor) || Directory.EnumerateFileSystemEntries(cursor).Any())
                return;
            Directory.Delete(cursor);
            cursor = Path.GetDirectoryName(cursor) ?? "";
        }
    }

    private static string NormalizeSupportRelativePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static StringComparer SupportPathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static bool HasPortableProjectData(string installRoot)
    {
        return PortableProjectMigration.Inventory(installRoot).Any(artifact => artifact.Kind is
            PortableProjectArtifactKind.ProjectEdit or
            PortableProjectArtifactKind.ProjectAsset or
            PortableProjectArtifactKind.ProjectSetting);
    }

    private static string AutomaticMigrationMarkerPath(EditorProjectLayout project, string installRoot)
    {
        string normalizedSource = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            normalizedSource = normalizedSource.ToUpperInvariant();
        string sourceId = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource)))
            .ToLowerInvariant()[..16];
        return Path.Combine(project.MigrationPath, "automatic-sources", $"{sourceId}.json");
    }

    private static bool PathsEqualLexically(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            comparison);
    }

}
