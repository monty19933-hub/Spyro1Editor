namespace Spyro.Editor.Core.Editing;

public static class TerrainBrushPreviewAnalyzer
{
    public static TerrainBrushPreviewStats Summarize(IEnumerable<TerrainBrushPreviewVertexKind> vertices, bool clipped)
    {
        int editable = 0;
        int visualOnly = 0;
        foreach (TerrainBrushPreviewVertexKind vertex in vertices)
        {
            if (vertex == TerrainBrushPreviewVertexKind.VisualOnly)
                visualOnly++;
            else
                editable++;
        }

        return new TerrainBrushPreviewStats(editable, visualOnly, clipped);
    }

    public static string FormatSummary(TerrainBrushPreviewStats stats)
    {
        string suffix = stats.Clipped ? "+" : "";
        if (stats.TotalVertices == 0)
            return "No vertices";
        if (stats.VisualOnlyVertices == 0)
            return $"{stats.EditableVertices}{suffix} {Plural(stats.EditableVertices, "vertex", "vertices")}";
        if (stats.EditableVertices == 0)
            return $"{stats.VisualOnlyVertices}{suffix} visual-only";
        return $"{stats.EditableVertices} playable + {stats.VisualOnlyVertices}{suffix} visual";
    }

    private static string Plural(int count, string singular, string plural)
    {
        return count == 1 ? singular : plural;
    }
}

public enum TerrainBrushPreviewVertexKind
{
    Editable,
    VisualOnly
}

public readonly record struct TerrainBrushPreviewStats(int EditableVertices, int VisualOnlyVertices, bool Clipped)
{
    public int TotalVertices => EditableVertices + VisualOnlyVertices;
}
