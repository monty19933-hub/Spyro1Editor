using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Exporting;

public static class WadAnalysisLocator
{
    public static string Find(EditorWorkspace workspace)
    {
        foreach (string root in CandidateRoots(workspace))
        {
            string path = Path.Combine(root, "spyro-wad-analysis.json");
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, "spyro-wad-analysis.json");
    }

    private static IEnumerable<string> CandidateRoots(EditorWorkspace workspace)
    {
        yield return workspace.RootPath;

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
