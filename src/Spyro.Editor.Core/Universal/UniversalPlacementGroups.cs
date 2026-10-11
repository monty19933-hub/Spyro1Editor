using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Expands authenticated indexed companions as ordinary persistent placements.
/// Source pointers do not enroll companions through this path.</summary>
public static class UniversalPlacementGroups
{
    public static IReadOnlyList<UniversalMobyPlacement> Expand(
        Func<string, (LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay)> getSource,
        UniversalMobyPlacement rootPlacement)
    {
        if (rootPlacement.SpecialId != null || rootPlacement.Retired) return [rootPlacement];
        var source = getSource(rootPlacement.SourceLevelKey);
        var root = UniversalIndexedRows.Capture(source.Data, source.Overlay,
            source.Data.CaptureRow(rootPlacement.SourceLevelKey, rootPlacement.SourceRowIndex));
        // A dragon comes whole: its statue, pedestal and dragon name each other by row (UniversalDragonRows).
        var dragon = UniversalDragonRows.Family(source.Data, rootPlacement.SourceLevelKey, rootPlacement.SourceRowIndex);
        if (root.SourceRowIndices.Count == 0 && root.SourcePrerequisiteRows.Count == 0 && dragon.Count == 0) return [rootPlacement];
        List<UniversalRowTemplate> members = []; HashSet<int> seen = []; Queue<int> pending = new([root.SourceRowIndex]);
        while (pending.Count > 0)
        {
            int row = pending.Dequeue(); if (!seen.Add(row)) continue;
            var template = UniversalIndexedRows.Capture(source.Data, source.Overlay, source.Data.CaptureRow(rootPlacement.SourceLevelKey, row));
            members.Add(template);
            foreach (var link in template.SourceRowIndices) pending.Enqueue(link.SourceRow);
            foreach (int member in dragon) pending.Enqueue(member);
            foreach (var required in template.SourcePrerequisiteRows) pending.Enqueue(required.SourceRow);
        }
        string group = string.IsNullOrEmpty(rootPlacement.LinkGroupId) ? Guid.NewGuid().ToString("N") : rootPlacement.LinkGroupId;
        string rootId = string.IsNullOrEmpty(rootPlacement.EditorId) ? Guid.NewGuid().ToString("N") : rootPlacement.EditorId;
        int X(byte[] r) => BinaryPrimitives.ReadInt32LittleEndian(r.AsSpan(0x0C));
        int Y(byte[] r) => BinaryPrimitives.ReadInt32LittleEndian(r.AsSpan(0x10));
        int Z(byte[] r) => BinaryPrimitives.ReadInt32LittleEndian(r.AsSpan(0x14));
        List<UniversalMobyPlacement> placements = [rootPlacement with { EditorId = rootId, LinkGroupId = group }];
        for (int i = 1; i < members.Count; i++)
        {
            var child = members[i];
            placements.Add(new(rootPlacement.SourceLevelKey, child.SourceRowIndex,
                checked(rootPlacement.X + X(child.Row) - X(root.Row)),
                checked(rootPlacement.Y + Y(child.Row) - Y(root.Row)),
                checked(rootPlacement.Z + Z(child.Row) - Z(root.Row)),
                child.Row[0x46], EditorId: Guid.NewGuid().ToString("N"),
                PersistentSlot: rootPlacement.PersistentSlot is int slot ? checked(slot + i) : null) { LinkGroupId = group });
        }
        return placements;
    }
}
