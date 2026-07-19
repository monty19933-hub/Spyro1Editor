using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Exporting;

public static class WadAnalysisLocator
{
    public static string Find(EditorWorkspace workspace)
    {
        foreach (string root in WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace))
        {
            string path = Path.Combine(root, "spyro-wad-analysis.json");
            if (File.Exists(path))
                return path;
        }

        return Path.Combine(workspace.RootPath, "spyro-wad-analysis.json");
    }
}
