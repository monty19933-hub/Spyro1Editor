namespace Spyro.Editor.Core.Scene;

public static class TerrainSnapper
{
    private const float SurfaceLayerEpsilon = 0.5f;

    public static bool TryFindZAt(
        IReadOnlyList<TerrainPolygon> polygons,
        float x,
        float y,
        float referenceZ,
        out float z,
        int preferredTerrainIndex = -1,
        bool preferTopSurface = false,
        Func<TerrainPolygon, bool>? includePolygon = null)
    {
        z = 0;
        if (polygons.Count == 0)
            return false;

        TerrainSnapCandidate? best = null;
        if (preferredTerrainIndex >= 0 &&
            preferredTerrainIndex < polygons.Count &&
            (includePolygon == null || includePolygon(polygons[preferredTerrainIndex])) &&
            TryGetTerrainZOnPolygon(polygons[preferredTerrainIndex], x, y, out float preferredZ))
        {
            if (!preferTopSurface)
            {
                z = preferredZ;
                return true;
            }

            best = new TerrainSnapCandidate(preferredZ, Math.Abs(preferredZ - referenceZ));
        }

        for (int i = 0; i < polygons.Count; i++)
        {
            if (i == preferredTerrainIndex)
                continue;
            if (includePolygon != null && !includePolygon(polygons[i]))
                continue;
            if (!TryGetTerrainZOnPolygon(polygons[i], x, y, out float terrainZ))
                continue;

            double distance = Math.Abs(terrainZ - referenceZ);
            TerrainSnapCandidate candidate = new(terrainZ, distance);
            if (best == null || IsBetterCandidate(candidate, best.Value, preferTopSurface))
                best = candidate;
        }

        if (best == null)
            return false;

        z = best.Value.Z;
        return true;
    }

    public static bool TryFindAdjacentZAt(
        IReadOnlyList<TerrainPolygon> polygons,
        float x,
        float y,
        float currentZ,
        int direction,
        out float z,
        Func<TerrainPolygon, bool>? includePolygon = null)
    {
        z = 0;
        if (direction == 0)
            return false;

        bool moveUp = direction > 0;
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < polygons.Count; i++)
        {
            if (includePolygon != null && !includePolygon(polygons[i]))
                continue;
            if (!TryGetTerrainZOnPolygon(polygons[i], x, y, out float candidateZ))
                continue;

            float delta = candidateZ - currentZ;
            if (moveUp ? delta <= SurfaceLayerEpsilon : delta >= -SurfaceLayerEpsilon)
                continue;

            float distance = Math.Abs(delta);
            if (distance >= bestDistance)
                continue;

            z = candidateZ;
            bestDistance = distance;
            found = true;
        }

        return found;
    }

    private static bool TryGetTerrainZOnPolygon(TerrainPolygon polygon, float x, float y, out float z)
    {
        z = 0;
        return !polygon.IsTerrainRemoved && polygon.TryGetZ(x, y, out z);
    }

    private static bool IsBetterCandidate(TerrainSnapCandidate candidate, TerrainSnapCandidate best, bool preferTopSurface)
    {
        return preferTopSurface
            ? candidate.Z > best.Z
            : candidate.DistanceToReference < best.DistanceToReference ||
              Math.Abs(candidate.DistanceToReference - best.DistanceToReference) <= 0.001 && candidate.Z > best.Z;
    }

    private readonly record struct TerrainSnapCandidate(float Z, double DistanceToReference);
}
