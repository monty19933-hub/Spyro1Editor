using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Exporting;

public static class TerrainPatchDataLocator
{
    public static string FindRamDump(EditorWorkspace workspace, string levelKey)
    {
        return Find(workspace, $"{levelKey}-before-clean.bin", $"{levelKey}-before-gem-clean.bin");
    }

    public static string FindSourceSearch(EditorWorkspace workspace, string levelKey)
    {
        return Find(workspace, $"{levelKey}-runtime-terrain-source-search.json");
    }

    private static string Find(EditorWorkspace workspace, params string[] names)
    {
        foreach (string path in CandidateRoots(workspace).SelectMany(root => names.Select(name => Path.Combine(root, name))))
        {
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, names[0]);
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
