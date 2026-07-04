namespace Spyro.Editor.Core.Workspace;

public sealed class EditorWorkspace
{
    private const string WorkspaceEnvironmentVariable = "SPYRO_EDITOR_WORKSPACE";

    public EditorWorkspace(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Workspace path is required.", nameof(rootPath));

        RootPath = Path.GetFullPath(rootPath);
    }

    public string RootPath { get; }

    public string ResolveFile(string fileName, params string[] cleanupBuckets)
    {
        string direct = Path.Combine(RootPath, fileName);
        if (File.Exists(direct))
            return direct;

        string bundled = Path.Combine(RootPath, "support", fileName);
        if (File.Exists(bundled))
            return bundled;

        string localRoot = Path.Combine(RootPath, "_local");
        if (!Directory.Exists(localRoot))
            return direct;

        string[] cleanupDirs = Directory.GetDirectories(localRoot, "cleanup-*");
        Array.Sort(cleanupDirs, StringComparer.OrdinalIgnoreCase);
        string[] buckets = cleanupBuckets.Length > 0
            ? cleanupBuckets
            : ["generated-research", "game-and-capture-artifacts"];

        for (int i = cleanupDirs.Length - 1; i >= 0; i--)
        {
            foreach (string bucket in buckets)
            {
                string candidate = Path.Combine(cleanupDirs[i], bucket, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return direct;
    }

    public static EditorWorkspace Find(string? startPath = null)
    {
        foreach (string candidate in EnumerateStartPaths(startPath))
        {
            string? workspace = FindWorkspaceRoot(candidate);
            if (!string.IsNullOrWhiteSpace(workspace))
                return new EditorWorkspace(workspace);
        }

        return new EditorWorkspace(AppContext.BaseDirectory);
    }

    private static bool IsWorkspaceCandidate(string path)
    {
        try
        {
            return HasReadableFile(Path.Combine(path, "spyro-level-catalog.json"))
                || HasReadableFile(Path.Combine(path, "support", "spyro-level-catalog.json"))
                || Directory.Exists(Path.Combine(path, "native")) && Directory.Exists(Path.Combine(path, "tools"));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateStartPaths(string? startPath)
    {
        string? configuredWorkspace = Environment.GetEnvironmentVariable(WorkspaceEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
        {
            string? fullPath = TryGetFullPath(configuredWorkspace);
            if (!string.IsNullOrWhiteSpace(fullPath))
                yield return fullPath;
        }

        if (!string.IsNullOrWhiteSpace(startPath))
        {
            string? fullPath = TryGetFullPath(startPath);
            if (!string.IsNullOrWhiteSpace(fullPath))
                yield return fullPath;
        }

        string? appBase = TryGetFullPath(AppContext.BaseDirectory);
        if (!string.IsNullOrWhiteSpace(appBase))
            yield return appBase;

        string? current = TryGetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(current))
            yield return current;

        string? userWorkspace = TryGetDefaultUserWorkspace();
        if (!string.IsNullOrWhiteSpace(userWorkspace))
            yield return userWorkspace;
    }

    private static string? TryGetDefaultUserWorkspace()
    {
        try
        {
            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(home))
                return Path.Combine(home, "Library", "Application Support", "Spyro Editor");

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(appData)
                ? null
                : Path.Combine(appData, "Spyro Editor");
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? FindWorkspaceRoot(string startPath)
    {
        string? current = TryGetFullPath(startPath);
        if (string.IsNullOrWhiteSpace(current))
            return null;

        DirectoryInfo? cursor = new DirectoryInfo(current);
        string? firstCandidate = null;
        while (cursor != null)
        {
            string path = cursor.FullName;
            if (IsWorkspaceCandidate(path))
            {
                firstCandidate ??= path;
                if (IsRepoWorkspaceCandidate(path) || IsPortablePackageCandidate(path))
                    return path;
            }

            cursor = cursor.Parent;
        }

        return firstCandidate;
    }

    private static bool IsRepoWorkspaceCandidate(string path)
    {
        try
        {
            return HasReadableFile(Path.Combine(path, "spyro-level-catalog.json"))
                && (Directory.Exists(Path.Combine(path, "src"))
                    || Directory.Exists(Path.Combine(path, "tools"))
                    || Directory.Exists(Path.Combine(path, "editor-cache"))
                    || Directory.Exists(Path.Combine(path, "native")));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsPortablePackageCandidate(string path)
    {
        try
        {
            return HasReadableFile(Path.Combine(path, "support", "spyro-level-catalog.json"))
                && Directory.Exists(Path.Combine(path, "support", "app"));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? TryGetCurrentDirectory()
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool HasReadableFile(string path)
    {
        try
        {
            using FileStream _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
