using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Editing;

public static class TerrainVertexSeamAligner
{
    public static TerrainVertexSeamAlignResult Align(
        IDictionary<TerrainPolygon, float[]> pendingDeltas,
        IReadOnlyDictionary<TerrainPolygon, IReadOnlySet<int>>? editableVertexIndexes = null)
    {
        if (pendingDeltas.Count <= 1)
            return new TerrainVertexSeamAlignResult(0, 0);

        Dictionary<string, List<VertexEdit>> groups = new(StringComparer.Ordinal);
        foreach ((TerrainPolygon polygon, float[] deltas) in pendingDeltas)
        {
            int count = Math.Min(Math.Min(polygon.Points.Count, polygon.OriginalZValues.Length), deltas.Length);
            for (int index = 0; index < count; index++)
            {
                if (editableVertexIndexes != null &&
                    (!editableVertexIndexes.TryGetValue(polygon, out IReadOnlySet<int>? editableIndexes) || !editableIndexes.Contains(index)))
                {
                    continue;
                }

                string key = VertexKey(polygon, index);
                if (!groups.TryGetValue(key, out List<VertexEdit>? edits))
                {
                    edits = new List<VertexEdit>();
                    groups[key] = edits;
                }

                edits.Add(new VertexEdit(deltas, index));
            }
        }

        int adjustedVertices = 0;
        int syncedGroups = 0;
        foreach (List<VertexEdit> edits in groups.Values)
        {
            if (edits.Count <= 1)
                continue;

            float average = edits.Average(edit => edit.Deltas[edit.Index]);
            bool changed = false;
            foreach (VertexEdit edit in edits)
            {
                if (Math.Abs(edit.Deltas[edit.Index] - average) <= 0.001f)
                    continue;

                edit.Deltas[edit.Index] = average;
                adjustedVertices++;
                changed = true;
            }

            if (changed)
                syncedGroups++;
        }

        return new TerrainVertexSeamAlignResult(syncedGroups, adjustedVertices);
    }

    private static string VertexKey(TerrainPolygon polygon, int index)
    {
        return $"{MathF.Round(polygon.Points[index].X)},{MathF.Round(polygon.Points[index].Y)},{MathF.Round(polygon.OriginalZValues[index])}";
    }

    private readonly record struct VertexEdit(float[] Deltas, int Index);
}

public readonly record struct TerrainVertexSeamAlignResult(int SyncedVertexGroups, int AdjustedVertices);
