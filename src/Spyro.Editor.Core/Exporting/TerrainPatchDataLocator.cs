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
        foreach (string path in WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).SelectMany(root => names.Select(name => Path.Combine(root, name))))
        {
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, names[0]);
    }
}
