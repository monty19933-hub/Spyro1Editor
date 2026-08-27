using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace Spyro.Editor.Core.Persistence;

public sealed record ReleaseProjectContext(
    string InstallRoot,
    string ApplicationSupportRoot,
    EditorUserDataLayout UserData,
    EditorProjectLayout Project,
    string AppVersion,
    PortableProjectMigrationResult? AutomaticMigration);

public sealed record CurrentProjectSettings(
    int Version,
    string ProjectId,
    string ProjectRoot,
    DateTimeOffset SavedAtUtc);

public sealed record PreparedReleaseProjectTransition(
    ReleaseProjectContext SourceContext,
    EditorProjectLayout Project);

public sealed record AutomaticPortableMigrationMarker(
    int Version,
    string SourceRoot,
    DateTimeOffset CompletedAtUtc,
    string ReportPath);

public sealed record ReleaseProjectBootstrapObservation(
    string Stage,
    TimeSpan StageElapsed,
    TimeSpan TotalElapsed);

internal sealed record SynchronizedSupportManifest(
    int Version,
    IReadOnlyList<SynchronizedSupportManifestEntry> Entries);

internal sealed record SynchronizedSupportManifestEntry(
    string RelativePath,
    long ByteLength,
    string Sha256);

internal sealed record SynchronizedSupportOwnership(
    int Version,
    IReadOnlyList<SynchronizedSupportOwnedFile> Files);

internal sealed record SynchronizedSupportOwnedFile(
    string RelativePath,
    long? ByteLength,
    string? Sha256);

internal sealed record SupportFileFingerprint(
    long ByteLength,
    byte[] Sha256);

public static class ReleaseProjectBootstrap
{
    public const string InstallRootEnvironmentVariable = "SPYRO_EDITOR_INSTALL_ROOT";
    public const string ReleaseEnvironmentVariable = "SPYRO_EDITOR_RELEASE";
    public const string WorkspaceEnvironmentVariable = "SPYRO_EDITOR_WORKSPACE";
    private const string CurrentProjectFileName = "current-project.json";
    private const long MaximumSupportManifestBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan SupportSynchronizationLeaseTimeout = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim ProjectCommitGate = new(1, 1);
    private static readonly SemaphoreSlim SupportSynchronizationGate = new(1, 1);
    private static readonly JsonSerializerOptions SupportManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ReleaseProjectContext? Current { get; private set; }

    public static Task<ReleaseProjectContext?> PrepareAsync(
        string appVersion,
        string? appBaseDirectory = null,
        EditorUserDataLayout? userData = null,
        string? explicitInstallRoot = null,
        bool? forceReleaseMode = null,
        CancellationToken cancellationToken = default)
    {
        return PrepareCoreAsync(
            appVersion,
            appBaseDirectory,
            userData,
            explicitInstallRoot,
            forceReleaseMode,
            cancellationToken,
            observer: null);
    }

    public static Task<ReleaseProjectContext?> PrepareAsync(
        string appVersion,
        Action<ReleaseProjectBootstrapObservation> observer,
        string? appBaseDirectory = null,
        EditorUserDataLayout? userData = null,
        string? explicitInstallRoot = null,
        bool? forceReleaseMode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return PrepareCoreAsync(
            appVersion,
            appBaseDirectory,
            userData,
            explicitInstallRoot,
            forceReleaseMode,
            cancellationToken,
            observer);
    }

    private static async Task<ReleaseProjectContext?> PrepareCoreAsync(
        string appVersion,
        string? appBaseDirectory,
        EditorUserDataLayout? userData,
        string? explicitInstallRoot,
        bool? forceReleaseMode,
        CancellationToken cancellationToken,
        Action<ReleaseProjectBootstrapObservation>? observer)
    {
        long prepareStarted = Stopwatch.GetTimestamp();
        long stageStarted = prepareStarted;
        void Observe(string stage)
        {
            long now = Stopwatch.GetTimestamp();
            ReleaseProjectBootstrapObservation observation = new(
                stage,
                Stopwatch.GetElapsedTime(stageStarted, now),
                Stopwatch.GetElapsedTime(prepareStarted, now));
            NotifyBootstrapObserver(observer, observation);
            stageStarted = Stopwatch.GetTimestamp();
        }

        Observe("prepare-started");
        string baseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppContext.BaseDirectory
            : appBaseDirectory);
        string installRoot = FindInstallRoot(explicitInstallRoot, baseDirectory);
        bool releaseMode = forceReleaseMode ?? ResolveReleaseMode(installRoot);
        if (!releaseMode)
        {
            Observe("release-mode-skipped");
            return null;
        }
        if (string.IsNullOrWhiteSpace(installRoot))
            throw new InvalidOperationException("The release installation root could not be located.");
        string applicationSupportRoot = FindApplicationSupportRoot(baseDirectory, installRoot);
        Observe("release-root-resolved");

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
        Observe("user-data-ready");

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
        Observe("project-ready");

        PortableProjectMigrationResult? migration = null;
        string migrationMarkerPath = AutomaticMigrationMarkerPath(project, installRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(project.RootPath, migrationMarkerPath);
        if (!File.Exists(migrationMarkerPath) && HasPortableProjectData(installRoot))
        {
            Observe("portable-migration-started");
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
            Observe("portable-migration-completed");
        }

        Observe("support-sync-started");
        await SynchronizeSupportFilesAsync(applicationSupportRoot, project.RootPath, cancellationToken);
        Observe("support-sync-completed");
        await TouchManifestAsync(project, appVersion, cancellationToken);
        Observe("project-manifest-ready");
        await RememberProjectAsync(userData, project, cancellationToken);
        Observe("project-remembered");

        Environment.SetEnvironmentVariable(InstallRootEnvironmentVariable, installRoot);
        Environment.SetEnvironmentVariable(ReleaseEnvironmentVariable, "1");
        Environment.SetEnvironmentVariable(WorkspaceEnvironmentVariable, project.RootPath);
        Current = new ReleaseProjectContext(
            installRoot,
            applicationSupportRoot,
            userData,
            project,
            appVersion,
            migration);
        Observe("prepare-completed");
        return Current;
    }

    public static async Task<EditorProjectLayout> PrepareAndRememberProjectAsync(
        string projectRoot,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        PreparedReleaseProjectTransition prepared = await PrepareProjectTransitionAsync(
            projectRoot,
            displayName,
            cancellationToken);
        return await CommitPreparedProjectTransitionAsync(
            prepared,
            cancellationToken);
    }

    public static async Task<PreparedReleaseProjectTransition> PrepareProjectTransitionAsync(
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
        await SynchronizeSupportFilesAsync(
            context.ApplicationSupportRoot,
            project.RootPath,
            cancellationToken);
        return new PreparedReleaseProjectTransition(context, project);
    }

    public static Task<EditorProjectLayout> CommitPreparedProjectTransitionAsync(
        PreparedReleaseProjectTransition prepared,
        CancellationToken cancellationToken = default)
    {
        return CommitPreparedProjectTransitionCoreAsync(
            prepared,
            cancellationToken,
            commitFaultForTesting: null);
    }

    public static Task<EditorProjectLayout> CommitPreparedProjectTransitionForEditorAsync(
        PreparedReleaseProjectTransition prepared,
        CancellationToken cancellationToken,
        Action<string>? commitFaultForTesting)
    {
        return CommitPreparedProjectTransitionCoreAsync(
            prepared,
            cancellationToken,
            commitFaultForTesting);
    }

    private static async Task<EditorProjectLayout> CommitPreparedProjectTransitionCoreAsync(
        PreparedReleaseProjectTransition prepared,
        CancellationToken cancellationToken,
        Action<string>? commitFaultForTesting)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        await ProjectCommitGate.WaitAsync(cancellationToken);
        try
        {
            ReleaseProjectContext sourceContext = prepared.SourceContext;
            if (!ReferenceEquals(Current, sourceContext))
            {
                throw new OperationCanceledException(
                    "The active release project changed before the prepared workspace could commit.",
                    cancellationToken);
            }

            string settingsPath = Path.Combine(
                sourceContext.UserData.SettingsPath,
                CurrentProjectFileName);
            byte[]? settingsBefore = File.Exists(settingsPath)
                ? await File.ReadAllBytesAsync(settingsPath, cancellationToken)
                : null;
            string? workspaceEnvironmentBefore =
                Environment.GetEnvironmentVariable(WorkspaceEnvironmentVariable);
            ReleaseProjectContext? currentBefore = Current;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(Current, sourceContext))
                {
                    throw new OperationCanceledException(
                        "The active release project changed before the prepared workspace could be remembered.",
                        cancellationToken);
                }

                await RememberProjectAsync(
                    sourceContext.UserData,
                    prepared.Project,
                    cancellationToken);
                commitFaultForTesting?.Invoke("after-release-settings-publish");
                cancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(Current, sourceContext))
                {
                    throw new OperationCanceledException(
                        "The active release project changed while the prepared workspace was being remembered.",
                        cancellationToken);
                }

                Environment.SetEnvironmentVariable(
                    WorkspaceEnvironmentVariable,
                    prepared.Project.RootPath);
                commitFaultForTesting?.Invoke("after-release-environment-publish");
                Current = sourceContext with
                {
                    Project = prepared.Project,
                    AutomaticMigration = null
                };
                commitFaultForTesting?.Invoke("after-release-context-publish");
                return prepared.Project;
            }
            catch (Exception commitFailure)
            {
                List<Exception> failures = [commitFailure];
                try
                {
                    await RestoreOptionalFileSnapshotAsync(
                        settingsPath,
                        settingsBefore);
                }
                catch (Exception rollbackFailure)
                {
                    failures.Add(rollbackFailure);
                }

                try
                {
                    Environment.SetEnvironmentVariable(
                        WorkspaceEnvironmentVariable,
                        workspaceEnvironmentBefore);
                }
                catch (Exception rollbackFailure)
                {
                    failures.Add(rollbackFailure);
                }
                Current = currentBefore;

                if (failures.Count > 1)
                {
                    throw new AggregateException(
                        "The prepared workspace commit failed and its release-context rollback was incomplete.",
                        failures);
                }
                throw;
            }
        }
        finally
        {
            ProjectCommitGate.Release();
        }
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
        await SynchronizeSupportFilesAsync(
            context.ApplicationSupportRoot,
            context.Project.RootPath,
            cancellationToken);
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

    private static string FindApplicationSupportRoot(
        string appBaseDirectory,
        string installRoot)
    {
        DirectoryInfo? cursor = new(Path.GetFullPath(appBaseDirectory));
        while (cursor != null)
        {
            if (IsInstallRoot(cursor.FullName))
                return Path.Combine(cursor.FullName, "support");
            cursor = cursor.Parent;
        }

        // FindInstallRoot already validated this fallback. Keeping it separate
        // from the migration root lets a packaged Mac app read support inside
        // its own bundle without traversing the enclosing Documents folder.
        return Path.Combine(installRoot, "support");
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

        if (!IsInstallRoot(root) || !HasKnownPortableLauncher(root))
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

    private static async Task RestoreOptionalFileSnapshotAsync(
        string path,
        byte[]? contents)
    {
        if (contents == null)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        string temporaryPath = $"{path}.{Guid.NewGuid():N}.restore";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            await File.WriteAllBytesAsync(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
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
        string applicationSupportRoot,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        await SupportSynchronizationGate.WaitAsync(cancellationToken);
        try
        {
            await SynchronizeSupportFilesCoreAsync(
                applicationSupportRoot,
                projectRoot,
                cancellationToken,
                captureRaceForTesting: null);
        }
        finally
        {
            SupportSynchronizationGate.Release();
        }
    }

    private static async Task SynchronizeSupportFilesCoreAsync(
        string applicationSupportRoot,
        string projectRoot,
        CancellationToken cancellationToken,
        Func<string, string, string, Task>? captureRaceForTesting)
    {
        string sourceRoot = Path.GetFullPath(applicationSupportRoot);
        string destinationRoot = Path.Combine(projectRoot, "support");
        PersistencePathSafety.EnsureTreesDoNotOverlap(
            sourceRoot,
            destinationRoot,
            "Application support files cannot be synchronized between overlapping source and project trees.");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationRoot);

        string applicationStateRoot = Path.Combine(projectRoot, "_local", ".app");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, applicationStateRoot);
        Directory.CreateDirectory(applicationStateRoot);
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, applicationStateRoot);
        string synchronizationLeasePath = Path.Combine(applicationStateRoot, "support-sync.lock");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, synchronizationLeasePath);
        await using FileStream synchronizationLease = await AcquireSupportSynchronizationLeaseAsync(
            projectRoot,
            synchronizationLeasePath,
            cancellationToken);

        string manifestPath = Path.Combine(applicationStateRoot, "support-ownership.json");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, manifestPath);
        SynchronizedSupportOwnership? previousOwnership = await ReadSupportManifestAsync(
            manifestPath,
            cancellationToken);
        Dictionary<string, SynchronizedSupportOwnedFile> previousOwnedFiles = previousOwnership?.Files
            .ToDictionary(file => file.RelativePath, SupportPathComparer)
            ?? new Dictionary<string, SynchronizedSupportOwnedFile>(SupportPathComparer);

        HashSet<string> sourcePathIdentities = new(SupportPathComparer);
        List<(string SourcePath, string RelativePath)> sourceFiles = [];
        foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        }))
        {
            string relativePath = ValidateAndNormalizeSupportRelativePath(
                Path.GetRelativePath(sourceRoot, sourcePath));
            if (relativePath.Equals("app", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("app/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (Path.GetFileName(relativePath)
                .EndsWith(".spyro-support-capture", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Application support uses the reserved capture-file namespace: {relativePath}");
            }
            if (!sourcePathIdentities.Add(relativePath))
            {
                throw new InvalidDataException(
                    $"Application support contains colliding relative paths: {relativePath}");
            }
            sourceFiles.Add((sourcePath, relativePath));
        }
        sourceFiles.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));

        List<SynchronizedSupportManifestEntry> currentEntries = new(sourceFiles.Count);
        foreach ((string sourcePath, string relativePath) in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string destinationPath = SafeSupportDestinationPath(projectRoot, destinationRoot, relativePath);
            string destinationDirectory = Path.GetDirectoryName(destinationPath) ?? destinationRoot;
            Directory.CreateDirectory(destinationDirectory);
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationPath);
            await ReconcileInterruptedSupportCaptureAsync(
                projectRoot,
                applicationStateRoot,
                relativePath,
                destinationPath,
                cancellationToken);

            await using FileStream source = OpenSupportFileForHashing(sourcePath);
            SupportFileFingerprint sourceFingerprint = await ComputeFingerprintAsync(
                source,
                sourcePath,
                cancellationToken);
            if (Directory.Exists(destinationPath))
            {
                throw new InvalidDataException(
                    $"An application-owned support file destination became a directory: {destinationPath}");
            }
            SupportFileFingerprint? destinationFingerprint = File.Exists(destinationPath)
                ? await ComputeFileFingerprintAsync(destinationPath, cancellationToken)
                : null;
            bool destinationMatches = destinationFingerprint != null &&
                FingerprintsEqual(destinationFingerprint, sourceFingerprint);
            if (!destinationMatches)
            {
                previousOwnedFiles.TryGetValue(
                    relativePath,
                    out SynchronizedSupportOwnedFile? previousOwnedFile);
                if (destinationFingerprint != null && previousOwnedFile == null)
                {
                    throw new InvalidDataException(
                        $"Application support would overwrite a differing project-owned file: {destinationPath}");
                }
                if (destinationFingerprint != null &&
                    previousOwnership?.Version == 2 &&
                    !FingerprintsEqual(destinationFingerprint, ExpectedFingerprint(previousOwnedFile!)))
                {
                    throw new InvalidDataException(
                        $"An application-owned support file was modified in the project and was preserved: {destinationPath}");
                }
                SupportFileFingerprint? expectedCapturedFingerprint = destinationFingerprint == null
                    ? null
                    : previousOwnership?.Version == 2
                        ? ExpectedFingerprint(previousOwnedFile!)
                        : destinationFingerprint;

                string temporaryPath = Path.Combine(
                    destinationDirectory,
                    $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
                PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, temporaryPath);
                try
                {
                    source.Position = 0;
                    await using (FileStream output = new(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 128 * 1024,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        await source.CopyToAsync(output, 128 * 1024, cancellationToken);
                        await output.FlushAsync(cancellationToken);
                        output.Flush(flushToDisk: true);
                    }

                    SupportFileFingerprint temporaryFingerprint = await ComputeFileFingerprintAsync(
                        temporaryPath,
                        cancellationToken);
                    if (!FingerprintsEqual(sourceFingerprint, temporaryFingerprint))
                    {
                        throw new InvalidDataException(
                            $"A staged application support file failed length/SHA-256 verification: {relativePath}");
                    }

                    await PublishStagedSupportFileAsync(
                        projectRoot,
                        applicationStateRoot,
                        relativePath,
                        destinationPath,
                        temporaryPath,
                        sourceFingerprint,
                        expectedCapturedFingerprint,
                        captureRaceForTesting,
                        cancellationToken);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }

            currentEntries.Add(new SynchronizedSupportManifestEntry(
                relativePath,
                sourceFingerprint.ByteLength,
                FormatSha256(sourceFingerprint.Sha256)));
        }

        byte[] canonicalManifestBytes = SerializeCanonicalSupportManifest(
            new SynchronizedSupportManifest(Version: 2, Entries: currentEntries));
        HashSet<string> currentFiles = currentEntries
            .Select(entry => entry.RelativePath)
            .ToHashSet(SupportPathComparer);
        if (previousOwnership != null)
        {
            foreach (SynchronizedSupportOwnedFile retired in previousOwnership.Files
                .Where(file => !currentFiles.Contains(file.RelativePath))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string retiredPath = SafeSupportDestinationPath(
                    projectRoot,
                    destinationRoot,
                    retired.RelativePath);
                await ReconcileInterruptedSupportCaptureAsync(
                    projectRoot,
                    applicationStateRoot,
                    retired.RelativePath,
                    retiredPath,
                    cancellationToken);
                if (Directory.Exists(retiredPath))
                {
                    throw new InvalidDataException(
                        $"A retired application-owned support file became a directory and was not removed: {retiredPath}");
                }
                if (!File.Exists(retiredPath))
                    continue;

                SupportFileFingerprint retiredFingerprint = await ComputeFileFingerprintAsync(
                    retiredPath,
                    cancellationToken);
                SupportFileFingerprint expectedRetiredFingerprint = previousOwnership.Version == 2
                    ? ExpectedFingerprint(retired)
                    : retiredFingerprint;
                if (previousOwnership.Version == 2)
                {
                    if (!FingerprintsEqual(retiredFingerprint, expectedRetiredFingerprint))
                        continue;
                }

                await RetireSupportFileSafelyAsync(
                    projectRoot,
                    applicationStateRoot,
                    retired.RelativePath,
                    retiredPath,
                    expectedRetiredFingerprint,
                    captureRaceForTesting,
                    cancellationToken);
                DeleteEmptySupportParents(projectRoot, destinationRoot, Path.GetDirectoryName(retiredPath));
            }
        }

        await PublishSupportManifestAsync(
            projectRoot,
            manifestPath,
            canonicalManifestBytes,
            cancellationToken);
    }

    private static async Task PublishStagedSupportFileAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string destinationPath,
        string stagedPath,
        SupportFileFingerprint sourceFingerprint,
        SupportFileFingerprint? expectedCapturedFingerprint,
        Func<string, string, string, Task>? captureRaceForTesting,
        CancellationToken cancellationToken)
    {
        EnsureSupportCaptureAndRecoveryShareVolume(
            projectRoot,
            destinationPath,
            applicationStateRoot,
            relativePath);
        (string captureMarkerPath, string captureNonce, string capturePath) =
            await BeginSupportCaptureAsync(
                projectRoot,
                applicationStateRoot,
                relativePath,
                destinationPath,
                cancellationToken);
        bool captured = false;
        try
        {
            captured = TryCaptureSupportFile(
                projectRoot,
                destinationPath,
                capturePath);
            if (captured)
            {
                EnsureCapturedSupportFileIsRegular(projectRoot, capturePath);
                if (captureRaceForTesting != null)
                {
                    await captureRaceForTesting(
                        "after-current-capture",
                        destinationPath,
                        capturePath);
                }
                EnsureCapturedSupportFileIsRegular(projectRoot, capturePath);
                SupportFileFingerprint capturedFingerprint = await ComputeFileFingerprintAsync(
                    capturePath,
                    cancellationToken);
                if (FingerprintsEqual(capturedFingerprint, sourceFingerprint))
                {
                    string? conflictArchive = await RestoreCapturedSupportFileAsync(
                        projectRoot,
                        applicationStateRoot,
                        relativePath,
                        destinationPath,
                        capturePath,
                        cancellationToken);
                    captured = SupportPathEntryExists(capturePath);
                    if (conflictArchive != null)
                    {
                        throw new InvalidDataException(
                            $"A project support file was recreated while matching release bytes were being adopted. " +
                            $"The recreated file was preserved at {destinationPath}; the displaced bytes were archived at {conflictArchive}.");
                    }
                    return;
                }
                if (expectedCapturedFingerprint == null ||
                    !FingerprintsEqual(capturedFingerprint, expectedCapturedFingerprint))
                {
                    string? conflictArchive = await RestoreCapturedSupportFileAsync(
                        projectRoot,
                        applicationStateRoot,
                        relativePath,
                        destinationPath,
                        capturePath,
                        cancellationToken);
                    captured = SupportPathEntryExists(capturePath);
                    string recovery = conflictArchive == null
                        ? "The changed file was restored to its original project path."
                        : $"A concurrent replacement remained at the project path; the displaced file was archived at {conflictArchive}.";
                    throw new InvalidDataException(
                        $"A project support file changed before its release update could be published: {destinationPath}. {recovery}");
                }
            }

            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationPath);
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, stagedPath);
            if (Directory.Exists(destinationPath))
            {
                throw new InvalidDataException(
                    $"An application-owned support file destination became a directory: {destinationPath}");
            }
            try
            {
                // No overwrite is intentional. The project lease serializes editor instances,
                // but it does not grant authority over arbitrary external writers.
                File.Move(stagedPath, destinationPath);
            }
            catch (IOException exception) when (SupportPathEntryExists(destinationPath))
            {
                throw new InvalidDataException(
                    $"A project support file was recreated before release bytes could be published and was preserved: {destinationPath}",
                    exception);
            }

            SupportFileFingerprint publishedFingerprint = await ComputeFileFingerprintAsync(
                destinationPath,
                cancellationToken);
            if (!FingerprintsEqual(publishedFingerprint, sourceFingerprint))
            {
                throw new InvalidDataException(
                    $"A published application support file did not retain its verified release identity: {destinationPath}");
            }

            if (captured)
            {
                await ArchiveCapturedSupportFileAsync(
                    projectRoot,
                    applicationStateRoot,
                    relativePath,
                    capturePath,
                    cancellationToken);
                captured = false;
            }
        }
        catch (Exception operationException) when (IsRecoverableSupportSynchronizationException(operationException))
        {
            if (captured && SupportPathEntryExists(capturePath))
            {
                try
                {
                    await RestoreCapturedSupportFileAsync(
                        projectRoot,
                        applicationStateRoot,
                        relativePath,
                        destinationPath,
                        capturePath,
                        CancellationToken.None);
                }
                catch (Exception recoveryException) when (IsRecoverableSupportSynchronizationException(recoveryException))
                {
                    throw new InvalidDataException(
                        $"Support publication failed and its displaced file could not be restored or archived safely: {destinationPath}",
                        new AggregateException(operationException, recoveryException));
                }
            }
            throw;
        }
        finally
        {
            if (!captured || !SupportPathEntryExists(capturePath))
            {
                await RemoveCompletedSupportCaptureMarkerAsync(
                    projectRoot,
                    captureMarkerPath,
                    captureNonce,
                    CancellationToken.None);
            }
        }
    }

    private static async Task RetireSupportFileSafelyAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string retiredPath,
        SupportFileFingerprint expectedFingerprint,
        Func<string, string, string, Task>? captureRaceForTesting,
        CancellationToken cancellationToken)
    {
        EnsureSupportCaptureAndRecoveryShareVolume(
            projectRoot,
            retiredPath,
            applicationStateRoot,
            relativePath);
        (string captureMarkerPath, string captureNonce, string capturePath) =
            await BeginSupportCaptureAsync(
                projectRoot,
                applicationStateRoot,
                relativePath,
                retiredPath,
                cancellationToken);
        bool captured = false;
        try
        {
            captured = TryCaptureSupportFile(projectRoot, retiredPath, capturePath);
            if (!captured)
                return;

            EnsureCapturedSupportFileIsRegular(projectRoot, capturePath);
            if (captureRaceForTesting != null)
            {
                await captureRaceForTesting(
                    "after-retired-capture",
                    retiredPath,
                    capturePath);
            }
            EnsureCapturedSupportFileIsRegular(projectRoot, capturePath);
            SupportFileFingerprint capturedFingerprint = await ComputeFileFingerprintAsync(
                capturePath,
                cancellationToken);
            if (!FingerprintsEqual(capturedFingerprint, expectedFingerprint))
            {
                string? conflictArchive = await RestoreCapturedSupportFileAsync(
                    projectRoot,
                    applicationStateRoot,
                    relativePath,
                    retiredPath,
                    capturePath,
                    cancellationToken);
                captured = SupportPathEntryExists(capturePath);
                if (conflictArchive != null)
                {
                    throw new InvalidDataException(
                        $"A retired project support file changed while it was being captured. " +
                        $"A concurrent replacement was preserved at {retiredPath}; the displaced file was archived at {conflictArchive}.");
                }

                // A changed retired file remains user data. Restoring it and omitting it
                // from the new ownership manifest retires application authority safely.
                return;
            }

            await ArchiveCapturedSupportFileAsync(
                projectRoot,
                applicationStateRoot,
                relativePath,
                capturePath,
                cancellationToken);
            captured = false;
        }
        catch (Exception operationException) when (IsRecoverableSupportSynchronizationException(operationException))
        {
            if (captured && SupportPathEntryExists(capturePath))
            {
                try
                {
                    await RestoreCapturedSupportFileAsync(
                        projectRoot,
                        applicationStateRoot,
                        relativePath,
                        retiredPath,
                        capturePath,
                        CancellationToken.None);
                }
                catch (Exception recoveryException) when (IsRecoverableSupportSynchronizationException(recoveryException))
                {
                    throw new InvalidDataException(
                        $"Support retirement failed and its displaced file could not be restored or archived safely: {retiredPath}",
                        new AggregateException(operationException, recoveryException));
                }
            }
            throw;
        }
        finally
        {
            if (!captured || !SupportPathEntryExists(capturePath))
            {
                await RemoveCompletedSupportCaptureMarkerAsync(
                    projectRoot,
                    captureMarkerPath,
                    captureNonce,
                    CancellationToken.None);
            }
        }
    }

    private static async Task ReconcileInterruptedSupportCaptureAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        string captureMarkerPath = GetSupportCaptureMarkerPath(
            projectRoot,
            applicationStateRoot,
            relativePath,
            createDirectory: false);
        if (!SupportPathEntryExists(captureMarkerPath))
            return;
        if (Directory.Exists(captureMarkerPath))
        {
            throw new InvalidDataException(
                $"An interrupted support capture marker became a directory: {captureMarkerPath}");
        }
        string captureNonce = await ReadSupportCaptureNonceAsync(
            projectRoot,
            captureMarkerPath,
            cancellationToken);
        string capturePath = GetSupportCapturePath(
            projectRoot,
            relativePath,
            destinationPath,
            captureNonce);
        if (!SupportPathEntryExists(capturePath))
        {
            await RemoveCompletedSupportCaptureMarkerAsync(
                projectRoot,
                captureMarkerPath,
                captureNonce,
                CancellationToken.None);
            return;
        }
        if (Directory.Exists(capturePath))
        {
            throw new InvalidDataException(
                $"An interrupted support capture path became a directory: {capturePath}");
        }

        string destinationDirectory = Path.GetDirectoryName(destinationPath) ?? projectRoot;
        Directory.CreateDirectory(destinationDirectory);
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationDirectory);
        if (!SupportPathEntryExists(destinationPath))
        {
            try
            {
                PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, capturePath);
                PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationPath);
                File.Move(capturePath, destinationPath);
                await RemoveCompletedSupportCaptureMarkerAsync(
                    projectRoot,
                    captureMarkerPath,
                    captureNonce,
                    CancellationToken.None);
                return;
            }
            catch (IOException) when (SupportPathEntryExists(destinationPath))
            {
                // A writer recreated the live name; preserve it and archive the capture below.
            }
        }

        if (Directory.Exists(destinationPath))
        {
            throw new InvalidDataException(
                $"An interrupted support capture and a directory now occupy the same project identity. " +
                $"The capture was preserved at {capturePath}; the directory was not changed: {destinationPath}");
        }
        await ArchiveCapturedSupportFileAsync(
            projectRoot,
            applicationStateRoot,
            relativePath,
            capturePath,
            cancellationToken);
        await RemoveCompletedSupportCaptureMarkerAsync(
            projectRoot,
            captureMarkerPath,
            captureNonce,
            CancellationToken.None);
    }

    private static bool TryCaptureSupportFile(
        string projectRoot,
        string sourcePath,
        string capturePath)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, sourcePath);
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, capturePath);
        if (SupportPathEntryExists(capturePath))
        {
            throw new InvalidDataException(
                $"A support recovery capture already exists and was not overwritten: {capturePath}");
        }
        if (Directory.Exists(sourcePath))
        {
            throw new InvalidDataException(
                $"A support file path became a directory before it could be captured: {sourcePath}");
        }
        if (!SupportPathEntryExists(sourcePath))
            return false;

        try
        {
            // This no-overwrite rename atomically removes exactly the path identity
            // that will be validated; it never relies on the earlier fingerprint alone.
            File.Move(sourcePath, capturePath);
            return true;
        }
        catch (IOException) when (
            !SupportPathEntryExists(sourcePath) &&
            !SupportPathEntryExists(capturePath))
        {
            return false;
        }
    }

    private static async Task<string?> RestoreCapturedSupportFileAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string destinationPath,
        string capturePath,
        CancellationToken cancellationToken)
    {
        if (!SupportPathEntryExists(capturePath))
            return null;
        if (!SupportPathEntryExists(destinationPath))
        {
            try
            {
                PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, capturePath);
                PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, destinationPath);
                File.Move(capturePath, destinationPath);
                return null;
            }
            catch (IOException) when (SupportPathEntryExists(destinationPath))
            {
                // Preserve the concurrent live replacement and archive the capture.
            }
        }

        return await ArchiveCapturedSupportFileAsync(
            projectRoot,
            applicationStateRoot,
            relativePath,
            capturePath,
            cancellationToken);
    }

    private static async Task<string> ArchiveCapturedSupportFileAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string capturePath,
        CancellationToken cancellationToken)
    {
        EnsureCapturedSupportFileIsRegular(projectRoot, capturePath);
        EnsureSupportCaptureAndRecoveryShareVolume(
            projectRoot,
            capturePath,
            applicationStateRoot,
            relativePath);
        SupportFileFingerprint capturedFingerprint = await ComputeFileFingerprintAsync(
            capturePath,
            cancellationToken);
        string archiveDirectory = GetSupportArchiveDirectory(
            projectRoot,
            applicationStateRoot,
            relativePath,
            createDirectory: true);
        string archivePath = Path.Combine(
            archiveDirectory,
            $"{capturedFingerprint.ByteLength}-{FormatSha256(capturedFingerprint.Sha256)}.support-recovery");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, archivePath);

        try
        {
            File.Move(capturePath, archivePath);
        }
        catch (IOException) when (File.Exists(archivePath))
        {
            SupportFileFingerprint existingFingerprint = await ComputeFileFingerprintAsync(
                archivePath,
                cancellationToken);
            SupportFileFingerprint currentCaptureFingerprint = await ComputeFileFingerprintAsync(
                capturePath,
                cancellationToken);
            bool identitiesWereStable =
                FingerprintsEqual(existingFingerprint, capturedFingerprint) &&
                FingerprintsEqual(currentCaptureFingerprint, capturedFingerprint);
            string eventArchivePath = await MoveSupportRecoveryToEventArchiveAsync(
                projectRoot,
                archiveDirectory,
                capturePath,
                currentCaptureFingerprint,
                cancellationToken);
            if (!identitiesWereStable)
            {
                throw new InvalidDataException(
                    $"A support recovery identity changed during archival. Existing and captured bytes were preserved at " +
                    $"{archivePath} and {eventArchivePath}.");
            }

            // Do not delete an apparently identical capture: an arbitrary writer could
            // replace it after the hash and turn that delete into data loss. The primary
            // archive is deterministic/content-addressed; repeated captured events get a
            // collision-resistant event suffix. A retry after successful publication sees
            // the already-correct live source and therefore creates no further archive.
            // Disk use is consequently bounded by captured history, not retry count.
            return eventArchivePath;
        }

        SupportFileFingerprint archivedFingerprint = await ComputeFileFingerprintAsync(
            archivePath,
            cancellationToken);
        if (!FingerprintsEqual(archivedFingerprint, capturedFingerprint))
        {
            string eventArchivePath = await MoveSupportRecoveryToEventArchiveAsync(
                projectRoot,
                archiveDirectory,
                archivePath,
                archivedFingerprint,
                cancellationToken);
            throw new InvalidDataException(
                $"An archived support recovery file changed after publication and was preserved at a unique event identity: {eventArchivePath}");
        }
        return archivePath;
    }

    private static async Task<string> MoveSupportRecoveryToEventArchiveAsync(
        string projectRoot,
        string archiveDirectory,
        string sourcePath,
        SupportFileFingerprint observedFingerprint,
        CancellationToken cancellationToken)
    {
        string eventArchivePath;
        do
        {
            eventArchivePath = Path.Combine(
                archiveDirectory,
                $"{observedFingerprint.ByteLength}-{FormatSha256(observedFingerprint.Sha256)}." +
                $"{Guid.NewGuid():N}.event.support-recovery");
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, eventArchivePath);
        }
        while (File.Exists(eventArchivePath));
        File.Move(sourcePath, eventArchivePath);

        SupportFileFingerprint eventFingerprint = await ComputeFileFingerprintAsync(
            eventArchivePath,
            cancellationToken);
        if (!FingerprintsEqual(eventFingerprint, observedFingerprint))
        {
            // The GUID identity cannot collide with a future canonical archive even if
            // a retained writer changes this inode again. Preserve it and fail closed.
            throw new InvalidDataException(
                $"A support recovery event continued changing after archival and was preserved at {eventArchivePath}.");
        }
        return eventArchivePath;
    }

    private static string GetSupportCapturePath(
        string projectRoot,
        string relativePath,
        string livePath,
        string captureNonce)
    {
        if (captureNonce.Length != 32 || !captureNonce.All(IsLowerHexCharacter))
            throw new InvalidDataException("A support capture nonce is not canonical lowercase hexadecimal.");
        string captureDirectory = Path.GetDirectoryName(livePath) ?? projectRoot;
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, captureDirectory);
        string capturePath = Path.Combine(
            captureDirectory,
            $".{SupportRecoveryPathIdentity(relativePath)}.{captureNonce}.spyro-support-capture");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, capturePath);
        return capturePath;
    }

    private static async Task<(string MarkerPath, string Nonce, string CapturePath)> BeginSupportCaptureAsync(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        string livePath,
        CancellationToken cancellationToken)
    {
        string markerPath = GetSupportCaptureMarkerPath(
            projectRoot,
            applicationStateRoot,
            relativePath,
            createDirectory: true);
        if (SupportPathEntryExists(markerPath))
        {
            throw new InvalidDataException(
                $"An unresolved support capture marker was not overwritten: {markerPath}");
        }

        string nonce = Guid.NewGuid().ToString("N");
        byte[] markerBytes = Encoding.ASCII.GetBytes(nonce + "\n");
        string markerDirectory = Path.GetDirectoryName(markerPath) ?? applicationStateRoot;
        string temporaryMarkerPath = Path.Combine(
            markerDirectory,
            $".{Path.GetFileName(markerPath)}.{Guid.NewGuid():N}.tmp");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, temporaryMarkerPath);
        try
        {
            await using (FileStream output = new(
                temporaryMarkerPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(markerBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }
            byte[] stagedMarker = await File.ReadAllBytesAsync(
                temporaryMarkerPath,
                cancellationToken);
            if (!stagedMarker.AsSpan().SequenceEqual(markerBytes))
                throw new InvalidDataException("A staged support capture marker failed verification.");
            File.Move(temporaryMarkerPath, markerPath);
        }
        finally
        {
            if (File.Exists(temporaryMarkerPath))
                File.Delete(temporaryMarkerPath);
        }

        return (
            markerPath,
            nonce,
            GetSupportCapturePath(projectRoot, relativePath, livePath, nonce));
    }

    private static string GetSupportCaptureMarkerPath(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        bool createDirectory)
    {
        string inflightRoot = Path.Combine(
            GetSupportRecoveryRoot(projectRoot, applicationStateRoot),
            "inflight");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, inflightRoot);
        if (createDirectory)
        {
            Directory.CreateDirectory(inflightRoot);
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, inflightRoot);
        }
        string markerPath = Path.Combine(
            inflightRoot,
            $"{SupportRecoveryPathIdentity(relativePath)}.capture-marker");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, markerPath);
        return markerPath;
    }

    private static async Task<string> ReadSupportCaptureNonceAsync(
        string projectRoot,
        string markerPath,
        CancellationToken cancellationToken)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, markerPath);
        FileAttributes markerAttributes = File.GetAttributes(markerPath);
        if ((markerAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
            new FileInfo(markerPath).Length != 33)
        {
            throw new InvalidDataException(
                $"A support capture marker is not a regular canonical-size file and was preserved: {markerPath}");
        }
        byte[] markerBytes = await File.ReadAllBytesAsync(markerPath, cancellationToken);
        if (markerBytes.Length != 33 || markerBytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException(
                $"A support capture marker is malformed and was preserved: {markerPath}");
        }
        string nonce = Encoding.ASCII.GetString(markerBytes.AsSpan(0, 32));
        if (!nonce.All(IsLowerHexCharacter))
        {
            throw new InvalidDataException(
                $"A support capture marker has a non-canonical nonce and was preserved: {markerPath}");
        }
        return nonce;
    }

    private static async Task RemoveCompletedSupportCaptureMarkerAsync(
        string projectRoot,
        string markerPath,
        string expectedNonce,
        CancellationToken cancellationToken)
    {
        if (!SupportPathEntryExists(markerPath))
            return;
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, markerPath);
        string actualNonce = await ReadSupportCaptureNonceAsync(
            projectRoot,
            markerPath,
            cancellationToken);
        if (!string.Equals(actualNonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"A support capture marker changed before cleanup and was preserved: {markerPath}");
        }
        File.Delete(markerPath);
    }

    private static bool IsLowerHexCharacter(char character)
    {
        return (character >= '0' && character <= '9') ||
            (character >= 'a' && character <= 'f');
    }

    private static void EnsureSupportCaptureAndRecoveryShareVolume(
        string projectRoot,
        string captureOrLivePath,
        string applicationStateRoot,
        string relativePath)
    {
        string captureDirectory = Path.GetDirectoryName(captureOrLivePath)
            ?? throw new InvalidDataException(
                $"A support capture path has no containing directory: {captureOrLivePath}");
        string archiveDirectory = GetSupportArchiveDirectory(
            projectRoot,
            applicationStateRoot,
            relativePath,
            createDirectory: true);
        string captureVolume = FindContainingVolumeRoot(captureDirectory);
        string recoveryVolume = FindContainingVolumeRoot(archiveDirectory);
        if (!PathsEqualLexically(captureVolume, recoveryVolume))
        {
            throw new InvalidDataException(
                "Project support and application recovery storage are on different mounted volumes. " +
                "Support synchronization was refused before a non-atomic cross-volume capture/archive move.");
        }
    }

    private static string GetSupportArchiveDirectory(
        string projectRoot,
        string applicationStateRoot,
        string relativePath,
        bool createDirectory)
    {
        string archiveDirectory = Path.Combine(
            GetSupportRecoveryRoot(projectRoot, applicationStateRoot),
            "archive",
            SupportRecoveryPathIdentity(relativePath));
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, archiveDirectory);
        if (createDirectory)
        {
            Directory.CreateDirectory(archiveDirectory);
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, archiveDirectory);
        }
        return archiveDirectory;
    }

    private static string FindContainingVolumeRoot(string path)
    {
        string fullPath = ResolveSupportPhysicalPath(path);
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string? bestMatch = null;
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            string root = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(drive.RootDirectory.FullName));
            string rootWithSeparator = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!string.Equals(fullPath, root, comparison) &&
                !fullPath.StartsWith(rootWithSeparator, comparison))
            {
                continue;
            }
            if (bestMatch == null || root.Length > bestMatch.Length)
                bestMatch = root;
        }
        return bestMatch ?? throw new InvalidDataException(
            $"The mounted volume containing support storage could not be identified safely: {path}");
    }

    private static string ResolveSupportPhysicalPath(string path, int linkDepth = 0)
    {
        if (linkDepth > 64)
        {
            throw new InvalidDataException(
                $"Support storage contains too many symbolic-link or junction hops: {path}");
        }
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidDataException($"Support storage has no rooted path: {path}");
        string relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".")
            return Path.GetFullPath(root);

        string current = Path.GetFullPath(root);
        foreach (string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.GetFullPath(Path.Combine(current, segment));
            FileSystemInfo? entry = FindExistingSupportPathEntry(candidate);
            if (entry == null || string.IsNullOrWhiteSpace(entry.LinkTarget))
            {
                current = candidate;
                continue;
            }
            try
            {
                FileSystemInfo resolved = entry.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException($"The support-storage link has no target: {candidate}");
                current = ResolveSupportPhysicalPath(resolved.FullName, linkDepth + 1);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException(
                    $"A support-storage link cannot be resolved safely: {candidate}",
                    exception);
            }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    private static FileSystemInfo? FindExistingSupportPathEntry(string path)
    {
        DirectoryInfo directory = new(path);
        if (directory.Exists || !string.IsNullOrWhiteSpace(directory.LinkTarget))
            return directory;
        FileInfo file = new(path);
        if (file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget))
            return file;
        return null;
    }

    private static string GetSupportRecoveryRoot(
        string projectRoot,
        string applicationStateRoot)
    {
        string recoveryRoot = Path.Combine(applicationStateRoot, "support-recovery");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, recoveryRoot);
        return recoveryRoot;
    }

    private static string SupportRecoveryPathIdentity(string relativePath)
    {
        string normalized = ValidateAndNormalizeSupportRelativePath(relativePath);
        string identity = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? normalized.ToUpperInvariant()
            : normalized;
        return FormatSha256(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static void EnsureCapturedSupportFileIsRegular(
        string projectRoot,
        string capturePath)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, capturePath);
        FileAttributes attributes = File.GetAttributes(capturePath);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            throw new InvalidDataException(
                $"A captured support path is not a regular project-local file: {capturePath}");
        }
    }

    private static bool SupportPathEntryExists(string path)
    {
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool IsRecoverableSupportSynchronizationException(Exception exception)
    {
        return exception is not OutOfMemoryException and
            not StackOverflowException and
            not AccessViolationException;
    }

    private static async Task<FileStream> AcquireSupportSynchronizationLeaseAsync(
        string projectRoot,
        string leasePath,
        CancellationToken cancellationToken)
    {
        long waitStarted = Stopwatch.GetTimestamp();
        IOException? lastContention = null;
        while (Stopwatch.GetElapsedTime(waitStarted) < SupportSynchronizationLeaseTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, leasePath);
            if (Directory.Exists(leasePath))
            {
                throw new InvalidDataException(
                    $"The support synchronization lease path became a directory: {leasePath}");
            }
            try
            {
                FileStream lease = new(
                    leasePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.Asynchronous);
                try
                {
                    PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, leasePath);
                    return lease;
                }
                catch
                {
                    await lease.DisposeAsync();
                    throw;
                }
            }
            catch (IOException exception) when (
                IsSupportSynchronizationLeaseContention(exception, leasePath))
            {
                lastContention = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
        }

        throw new IOException(
            $"Another Spyro Editor process did not release project support synchronization within " +
            $"{SupportSynchronizationLeaseTimeout.TotalSeconds:F0} seconds: {leasePath}",
            lastContention);
    }

    private static bool IsSupportSynchronizationLeaseContention(
        IOException exception,
        string leasePath)
    {
        if (!SupportPathEntryExists(leasePath) || Directory.Exists(leasePath))
            return false;

        int nativeError = exception.HResult & 0xffff;
        if (OperatingSystem.IsWindows())
            return nativeError is 32 or 33;
        if (OperatingSystem.IsLinux())
            return nativeError == 11;
        if (OperatingSystem.IsMacOS())
            return nativeError == 35;
        return false;
    }

    private static async Task<SynchronizedSupportOwnership?> ReadSupportManifestAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(manifestPath))
            return null;
        try
        {
            long manifestLength = new FileInfo(manifestPath).Length;
            if (manifestLength < 0 || manifestLength > MaximumSupportManifestBytes)
                throw new JsonException("Support ownership manifest exceeds its safe size limit.");

            await using FileStream input = new(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            using JsonDocument document = await JsonDocument.ParseAsync(
                input,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8
                },
                cancellationToken);
            Dictionary<string, JsonElement> root = ReadStrictJsonObject(
                document.RootElement,
                "version",
                "files",
                "entries");
            if (!root.TryGetValue("version", out JsonElement versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out int version))
            {
                throw new JsonException("Support ownership manifest has no valid version.");
            }

            IReadOnlyList<SynchronizedSupportOwnedFile> files = version switch
            {
                1 => ReadV1SupportOwnership(root),
                2 => ReadV2SupportOwnership(root),
                _ => throw new JsonException("Support ownership manifest has an unsupported schema.")
            };
            return new SynchronizedSupportOwnership(version, files);
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException)
        {
            throw new InvalidDataException(
                $"The application support ownership manifest cannot be read safely: {manifestPath}",
                exception);
        }
    }

    private static IReadOnlyList<SynchronizedSupportOwnedFile> ReadV1SupportOwnership(
        IReadOnlyDictionary<string, JsonElement> root)
    {
        if (root.Count != 2 ||
            root.ContainsKey("entries") ||
            !root.TryGetValue("files", out JsonElement filesElement) ||
            filesElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Support ownership manifest v1 must contain only version and files.");
        }

        List<SynchronizedSupportOwnedFile> files = [];
        foreach (JsonElement fileElement in filesElement.EnumerateArray())
        {
            if (fileElement.ValueKind != JsonValueKind.String || fileElement.GetString() is not string relativePath)
                throw new JsonException("Support ownership manifest v1 contains a non-string path.");
            files.Add(new SynchronizedSupportOwnedFile(
                ValidateAndNormalizeSupportRelativePath(relativePath),
                ByteLength: null,
                Sha256: null));
        }
        return ValidateOwnedSupportPathIdentities(files);
    }

    private static IReadOnlyList<SynchronizedSupportOwnedFile> ReadV2SupportOwnership(
        IReadOnlyDictionary<string, JsonElement> root)
    {
        if (root.Count != 2 ||
            root.ContainsKey("files") ||
            !root.TryGetValue("entries", out JsonElement entriesElement) ||
            entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Support ownership manifest v2 must contain only version and entries.");
        }

        List<SynchronizedSupportOwnedFile> files = [];
        foreach (JsonElement entryElement in entriesElement.EnumerateArray())
        {
            Dictionary<string, JsonElement> entry = ReadStrictJsonObject(
                entryElement,
                "relativePath",
                "byteLength",
                "sha256");
            if (entry.Count != 3 ||
                !entry.TryGetValue("relativePath", out JsonElement relativePathElement) ||
                relativePathElement.ValueKind != JsonValueKind.String ||
                relativePathElement.GetString() is not string relativePath ||
                !entry.TryGetValue("byteLength", out JsonElement byteLengthElement) ||
                byteLengthElement.ValueKind != JsonValueKind.Number ||
                !byteLengthElement.TryGetInt64(out long byteLength) ||
                byteLength < 0 ||
                !entry.TryGetValue("sha256", out JsonElement sha256Element) ||
                sha256Element.ValueKind != JsonValueKind.String ||
                sha256Element.GetString() is not string sha256 ||
                !IsCanonicalSha256(sha256))
            {
                throw new JsonException("Support ownership manifest v2 contains an invalid entry.");
            }
            files.Add(new SynchronizedSupportOwnedFile(
                ValidateAndNormalizeSupportRelativePath(relativePath),
                byteLength,
                sha256));
        }
        return ValidateOwnedSupportPathIdentities(files);
    }

    private static IReadOnlyList<SynchronizedSupportOwnedFile> ValidateOwnedSupportPathIdentities(
        IEnumerable<SynchronizedSupportOwnedFile> files)
    {
        HashSet<string> identities = new(SupportPathComparer);
        List<SynchronizedSupportOwnedFile> validated = [];
        foreach (SynchronizedSupportOwnedFile file in files)
        {
            if (!identities.Add(file.RelativePath))
                throw new JsonException($"Support ownership manifest contains a colliding path: {file.RelativePath}");
            validated.Add(file);
        }
        validated.Sort((left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return validated;
    }

    private static Dictionary<string, JsonElement> ReadStrictJsonObject(
        JsonElement element,
        params string[] allowedPropertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("Support ownership manifest contains a non-object value.");
        HashSet<string> allowed = new(allowedPropertyNames, StringComparer.Ordinal);
        Dictionary<string, JsonElement> properties = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !properties.TryAdd(property.Name, property.Value))
                throw new JsonException($"Support ownership manifest contains an unexpected or duplicate property: {property.Name}");
        }
        return properties;
    }

    private static async Task PublishSupportManifestAsync(
        string projectRoot,
        string manifestPath,
        byte[] canonicalBytes,
        CancellationToken cancellationToken)
    {
        if (File.Exists(manifestPath))
        {
            byte[] existingBytes = await File.ReadAllBytesAsync(manifestPath, cancellationToken);
            if (existingBytes.AsSpan().SequenceEqual(canonicalBytes))
                return;
        }

        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? projectRoot;
        Directory.CreateDirectory(manifestDirectory);
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, manifestPath);
        string temporaryPath = Path.Combine(
            manifestDirectory,
            $".{Path.GetFileName(manifestPath)}.{Guid.NewGuid():N}.tmp");
        PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, temporaryPath);
        try
        {
            await using (FileStream output = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(canonicalBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(flushToDisk: true);
            }

            byte[] stagedBytes = await File.ReadAllBytesAsync(temporaryPath, cancellationToken);
            if (!stagedBytes.AsSpan().SequenceEqual(canonicalBytes))
                throw new InvalidDataException("The staged support ownership manifest failed verification.");
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, manifestPath);
            PersistencePathSafety.EnsureWritePathWithinRoot(projectRoot, temporaryPath);
            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static byte[] SerializeCanonicalSupportManifest(SynchronizedSupportManifest manifest)
    {
        byte[] canonicalBytes = JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            SupportManifestJsonOptions);
        if (canonicalBytes.LongLength > MaximumSupportManifestBytes)
        {
            throw new InvalidDataException(
                "The generated support ownership manifest exceeds its safe size limit.");
        }
        return canonicalBytes;
    }

    private static async Task<SupportFileFingerprint> ComputeFileFingerprintAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream input = OpenSupportFileForHashing(path);
        return await ComputeFingerprintAsync(input, path, cancellationToken);
    }

    private static FileStream OpenSupportFileForHashing(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static async Task<SupportFileFingerprint> ComputeFingerprintAsync(
        FileStream input,
        string path,
        CancellationToken cancellationToken)
    {
        input.Position = 0;
        long expectedLength = input.Length;
        byte[] hash = await SHA256.HashDataAsync(input, cancellationToken);
        if (input.Position != expectedLength || input.Length != expectedLength)
            throw new InvalidDataException($"A support file changed while it was being hashed: {path}");
        return new SupportFileFingerprint(expectedLength, hash);
    }

    private static bool FingerprintsEqual(
        SupportFileFingerprint left,
        SupportFileFingerprint right)
    {
        return left.ByteLength == right.ByteLength &&
            CryptographicOperations.FixedTimeEquals(left.Sha256, right.Sha256);
    }

    private static SupportFileFingerprint ExpectedFingerprint(SynchronizedSupportOwnedFile ownedFile)
    {
        return new SupportFileFingerprint(
            ownedFile.ByteLength ?? throw new InvalidDataException(
                "A v2 support ownership entry omitted its byte length."),
            Convert.FromHexString(ownedFile.Sha256 ?? throw new InvalidDataException(
                "A v2 support ownership entry omitted its SHA-256.")));
    }

    private static string FormatSha256(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsCanonicalSha256(string value)
    {
        return value.Length == 64 && value.All(character =>
            (character >= '0' && character <= '9') ||
            (character >= 'a' && character <= 'f'));
    }

    private static string SafeSupportDestinationPath(
        string projectRoot,
        string destinationRoot,
        string relativePath)
    {
        string normalized = ValidateAndNormalizeSupportRelativePath(relativePath);
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

    private static string ValidateAndNormalizeSupportRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.IndexOf('\0') >= 0 ||
            Path.IsPathRooted(path) ||
            path.StartsWith('/') ||
            path.StartsWith('\\') ||
            (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'))
        {
            throw new InvalidDataException(
                $"The support ownership manifest contains an unsafe path: {path}");
        }

        string normalized = path.Replace('\\', '/');
        if (normalized.Split('/').Any(segment =>
            string.IsNullOrEmpty(segment) || segment is "." or ".."))
        {
            throw new InvalidDataException(
                $"The support ownership manifest contains an unsafe path: {path}");
        }
        return normalized;
    }

    private static void NotifyBootstrapObserver(
        Action<ReleaseProjectBootstrapObservation>? observer,
        ReleaseProjectBootstrapObservation observation)
    {
        if (observer == null)
            return;
        try
        {
            observer(observation);
        }
        catch
        {
            // Startup observations are diagnostic-only and must never block the editor window.
        }
    }

    private static StringComparer SupportPathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static bool HasPortableProjectData(string installRoot)
    {
        // A packaged Mac app can discover the outer release folder as its install
        // root. Do not recursively enumerate that folder (and the app bundle inside
        // it) unless it is an actual older portable installation eligible for
        // automatic migration. Besides avoiding needless work, this prevents macOS
        // privacy checks on a Documents-hosted release folder from blocking startup
        // before the first window is created.
        if (!HasKnownPortableLauncher(installRoot))
            return false;

        return PortableProjectMigration.Inventory(installRoot).Any(artifact => artifact.Kind is
            PortableProjectArtifactKind.ProjectEdit or
            PortableProjectArtifactKind.ProjectAsset or
            PortableProjectArtifactKind.ProjectSetting);
    }

    private static bool HasKnownPortableLauncher(string root) =>
        File.Exists(Path.Combine(root, "Launch Spyro Editor.command")) ||
        File.Exists(Path.Combine(root, "Launch Spyro Editor.bat"));

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
