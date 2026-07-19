using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Source-exact environment sector lists plus the collision-triangle byte that
/// Spyro 1 uses to choose the current environment occlusion group.
/// </summary>
public sealed class NativeTerrainOcclusionData
{
    public NativeTerrainOcclusionData(
        IReadOnlyList<IReadOnlyList<int>> environmentGroups,
        IReadOnlyList<NativeTerrainOcclusionTriangle> collisionTriangles)
    {
        EnvironmentGroups = environmentGroups
            .Select(group => (IReadOnlyList<int>)group.ToArray())
            .ToArray();
        CollisionTriangles = collisionTriangles.ToArray();
    }

    public IReadOnlyList<IReadOnlyList<int>> EnvironmentGroups { get; }
    public IReadOnlyList<NativeTerrainOcclusionTriangle> CollisionTriangles { get; }

    public bool TryResolveGroup(
        double x,
        double y,
        double z,
        out int groupIndex,
        out int triangleIndex,
        out double floorZ)
    {
        groupIndex = -1;
        triangleIndex = -1;
        floorZ = double.NegativeInfinity;

        // Retail chooses the highest collision triangle strictly below the
        // camera and copies that triangle's one-byte occlusion assignment.
        // Iterating in source-table order also preserves the retail tie rule:
        // the first triangle at an already-selected height wins.
        foreach (NativeTerrainOcclusionTriangle triangle in CollisionTriangles)
        {
            if (!triangle.TryInterpolateZ(x, y, out double candidateZ) ||
                candidateZ <= 0 ||
                candidateZ >= z ||
                candidateZ <= floorZ)
            {
                continue;
            }

            floorZ = candidateZ;
            triangleIndex = triangle.Index;
            groupIndex = triangle.GroupIndex < EnvironmentGroups.Count
                ? triangle.GroupIndex
                : -1;
        }

        return triangleIndex >= 0;
    }

    public IReadOnlySet<int>? VisibleSectorsForGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= EnvironmentGroups.Count)
            return null;

        return EnvironmentGroups[groupIndex].ToHashSet();
    }
}

public sealed record NativeTerrainOcclusionTriangle(
    int Index,
    int GroupIndex,
    SpyroCollisionPoint P1,
    SpyroCollisionPoint P2,
    SpyroCollisionPoint P3)
{
    public static NativeTerrainOcclusionTriangle FromWords(
        int index,
        uint xWord,
        uint yWord,
        uint zWord,
        int groupIndex)
    {
        int p1X = (int)(xWord & 0x3FFF);
        int p1Y = (int)(yWord & 0x3FFF);
        int p1Z = (int)(zWord & 0x3FFF);
        return new NativeTerrainOcclusionTriangle(
            index,
            groupIndex & 0xFF,
            new SpyroCollisionPoint(p1X, p1Y, p1Z),
            new SpyroCollisionPoint(
                p1X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
                p1Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
                p1Z + (int)((zWord >> 16) & 0xFF)),
            new SpyroCollisionPoint(
                p1X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
                p1Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
                p1Z + (int)((zWord >> 24) & 0xFF)));
    }

    public bool TryInterpolateZ(double x, double y, out double z)
    {
        double denominator = ((P2.Y - P3.Y) * (double)(P1.X - P3.X)) +
            ((P3.X - P2.X) * (double)(P1.Y - P3.Y));
        if (Math.Abs(denominator) < 0.000001)
        {
            z = 0;
            return false;
        }

        double a = (((P2.Y - P3.Y) * (x - P3.X)) +
            ((P3.X - P2.X) * (y - P3.Y))) / denominator;
        double b = (((P3.Y - P1.Y) * (x - P3.X)) +
            ((P1.X - P3.X) * (y - P3.Y))) / denominator;
        double c = 1.0 - a - b;
        const double edgeTolerance = 0.000001;
        if (a < -edgeTolerance || b < -edgeTolerance || c < -edgeTolerance)
        {
            z = 0;
            return false;
        }

        z = (a * P1.Z) + (b * P2.Z) + (c * P3.Z);
        return true;
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        int modulus = 1 << bits;
        return (value & sign) != 0 ? value - modulus : value;
    }
}
