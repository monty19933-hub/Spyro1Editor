namespace Spyro.Editor.Core.Persistence;

internal static class PersistencePathSafety
{
    public static void EnsureTreesDoNotOverlap(
        string firstRoot,
        string secondRoot,
        string message)
    {
        if (TreesOverlapLexically(firstRoot, secondRoot) ||
            TreesOverlapPhysically(firstRoot, secondRoot))
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void EnsureWritePathWithinRoot(
        string rootPath,
        string candidatePath,
        string? message = null)
    {
        if (!IsSameOrNestedLexically(candidatePath, rootPath) ||
            !IsSameOrNestedPhysically(candidatePath, rootPath))
        {
            throw new InvalidOperationException(
                message ?? $"A project write path escapes its project root: {candidatePath}");
        }
    }

    private static bool TreesOverlapLexically(string firstRoot, string secondRoot)
    {
        return IsSameOrNestedLexically(firstRoot, secondRoot) ||
            IsSameOrNestedLexically(secondRoot, firstRoot);
    }

    private static bool TreesOverlapPhysically(string firstRoot, string secondRoot)
    {
        return IsSameOrNestedPhysically(firstRoot, secondRoot) ||
            IsSameOrNestedPhysically(secondRoot, firstRoot);
    }

    private static bool IsSameOrNestedLexically(string candidate, string parent)
    {
        return IsSameOrNestedNormalized(Path.GetFullPath(candidate), Path.GetFullPath(parent));
    }

    private static bool IsSameOrNestedPhysically(string candidate, string parent)
    {
        return IsSameOrNestedNormalized(ResolvePhysicalPath(candidate), ResolvePhysicalPath(parent));
    }

    private static bool IsSameOrNestedNormalized(string candidate, string parent)
    {
        string candidateRoot = EnsureTrailingSeparator(Path.TrimEndingDirectorySeparator(candidate));
        string parentRoot = EnsureTrailingSeparator(Path.TrimEndingDirectorySeparator(parent));
        return candidateRoot.StartsWith(parentRoot, PathComparison);
    }

    private static string ResolvePhysicalPath(string path, int linkDepth = 0)
    {
        if (linkDepth > 64)
            throw new InvalidOperationException($"A persistence path contains too many symbolic-link or junction hops: {path}");
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath)
            ?? throw new InvalidOperationException($"A rooted persistence path is required: {path}");
        string relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".")
            return Path.GetFullPath(root);

        string current = Path.GetFullPath(root);
        foreach (string segment in relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.GetFullPath(Path.Combine(current, segment));
            FileSystemInfo? entry = FindExistingEntry(candidate);
            if (entry == null || string.IsNullOrWhiteSpace(entry.LinkTarget))
            {
                current = candidate;
                continue;
            }

            FileSystemInfo resolved;
            try
            {
                resolved = entry.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException($"The symbolic link has no resolvable target: {candidate}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"A persistence path contains an unreadable symbolic link or junction: {candidate}",
                    exception);
            }
            current = ResolvePhysicalPath(resolved.FullName, linkDepth + 1);
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    private static FileSystemInfo? FindExistingEntry(string path)
    {
        DirectoryInfo directory = new(path);
        try
        {
            if (directory.Exists || !string.IsNullOrWhiteSpace(directory.LinkTarget))
                return directory;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"A persistence path component cannot be inspected: {path}", exception);
        }

        FileInfo file = new(path);
        try
        {
            if (file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget))
                return file;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"A persistence path component cannot be inspected: {path}", exception);
        }

        return null;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
}
