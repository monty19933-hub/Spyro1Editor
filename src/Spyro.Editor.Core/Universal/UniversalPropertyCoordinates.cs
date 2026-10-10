using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>Moves the world points an object keeps in its own data along with the object. Many enemies store absolute
/// positions there: a copy of their spawn point, patrol path nodes (sheep, the bull), a point they walk or fly to (the
/// Fools) or the centre of the circle they skate (Ice Cavern's snowballers, through the shared routine 0x80038638,
/// which never checks walls). Copied or moved unchanged, those points stay where the object stood in its home level,
/// so the object heads there: off the map, into walls, where it can't be reached.
///
/// A point is three consecutive words (x, y, z) within <see cref="Reach"/> of the object's source position on every
/// axis; it moves by the same offset as the object. Pointers and row links are never read as coordinates, and
/// objects near the world origin are left alone (their small numbers could be anything).</summary>
public static class UniversalPropertyCoordinates
{
    /// <summary>The farthest a stored point lies from its object's spawn on one axis (the Town Square bull's path
    /// reaches 15770 units, Ice Cavern's snowballer centres 5231).</summary>
    public const int Reach = 0x4000;

    /// <summary>Source positions below this on x or y are too near the origin to tell coordinates from other numbers.</summary>
    public const int MinimumPosition = 2 * Reach;

    /// <summary>The template with its stored points moved by (destination row position - source position); the same
    /// template when the object did not move or keeps no points.</summary>
    public static UniversalRowTemplate Rebase(UniversalRowTemplate t, ReadOnlySpan<byte> row)
    {
        var points = Points(t);
        if (points.Count == 0) return t;
        int[] delta = new int[3];
        for (int axis = 0; axis < 3; axis++)
            delta[axis] = checked(BinaryPrimitives.ReadInt32LittleEndian(row[(0x0C + 4 * axis)..]) - BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(0x0C + 4 * axis)));
        if (delta.All(d => d == 0)) return t;
        byte[] props = t.Properties.ToArray();
        foreach (int at in points)
            for (int axis = 0; axis < 3; axis++)
                BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(at + 4 * axis), checked(BinaryPrimitives.ReadInt32LittleEndian(props.AsSpan(at + 4 * axis)) + delta[axis]));
        return t with { Properties = props };
    }

    /// <summary>Offsets of the stored points (each three words) in the template's properties.</summary>
    public static IReadOnlyList<int> Points(UniversalRowTemplate t)
    {
        int sx = BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(0x0C)), sy = BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(0x10)),
            sz = BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(0x14));
        if (sx < MinimumPosition || sy < MinimumPosition) return [];
        HashSet<int> reserved = [];
        foreach (int o in t.InternalPointers.Select(p => p.Offset).Concat(t.ExternalRowPointers.Select(p => p.Offset))
                     .Concat(t.ImportedRowPointers.Select(p => p.Offset)).Concat(t.SourceRowIndices.Select(p => p.Offset))
                     .Concat(t.ImportedRowIndices.Select(p => p.Offset)).Concat(t.OwnRowIndexOffsets))
            for (int b = o & ~3; b < o + 4; b += 4) reserved.Add(b);
        var p = t.Properties;
        List<int> points = [];
        for (int k = 0; k + 12 <= p.Length; k += 4)
        {
            if (reserved.Contains(k) || reserved.Contains(k + 4) || reserved.Contains(k + 8)) continue;
            int x = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(k)), y = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(k + 4)),
                z = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(k + 8));
            if (Math.Abs((long)x - sx) > Reach || Math.Abs((long)y - sy) > Reach || Math.Abs((long)z - sz) > Reach) continue;
            points.Add(k);
            k += 8;
        }
        return points;
    }
}
