namespace Spyro.Editor.Core.Workspace;

public static class WorkspaceArtifactSearchPolicy
{
    public const string ReleaseEnvironmentVariable = "SPYRO_EDITOR_RELEASE";

    public static bool IsReleaseMode
    {
        get
        {
            string value = Environment.GetEnvironmentVariable(ReleaseEnvironmentVariable) ?? "";
            return !string.IsNullOrWhiteSpace(value) &&
                !value.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                !value.Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static IEnumerable<string> EnumerateCandidateRoots(EditorWorkspace workspace)
    {
        yield return workspace.RootPath;
        if (IsReleaseMode)
            yield break;

        DirectoryInfo? parent = Directory.GetParent(workspace.RootPath);
        if (parent == null)
            yield break;

        foreach (DirectoryInfo sibling in SafeEnumerateDirectories(parent))
        {
            yield return sibling.FullName;
            foreach (DirectoryInfo nested in SafeEnumerateDirectories(sibling))
                yield return nested.FullName;
        }
    }

    private static IEnumerable<DirectoryInfo> SafeEnumerateDirectories(DirectoryInfo directory)
    {
        try
        {
            return directory.EnumerateDirectories().ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<DirectoryInfo>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DirectoryInfo>();
        }
    }
}
