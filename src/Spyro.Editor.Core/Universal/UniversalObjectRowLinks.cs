namespace Spyro.Editor.Core.Universal;

/// <summary>Source scene pointers resolve within one authored copy of a linked group.</summary>
public static class UniversalObjectRowLinks
{
    private static bool Owns(UniversalRowTemplate source, UniversalRowTemplate target, int row) =>
        target.SourceRowIndex == row &&
        string.Equals(source.SourceLevelKey, target.SourceLevelKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(source.LinkGroupId ?? "", target.LinkGroupId ?? "", StringComparison.Ordinal);

    public static UniversalRowTemplate Resolve(UniversalRowTemplate template, IReadOnlyList<UniversalRowTemplate> active)
    {
        if (template.ImportedRowPointers.Count != 0 || template.DroppedExternalPointers < template.ExternalRowPointers.Count ||
            template.ExternalRowPointers.Select(p => p.Offset).Distinct().Count() != template.ExternalRowPointers.Count)
            throw new InvalidDataException("Source object links lack unique unrelocated ownership.");
        List<(int Offset, int PlacementIndex, int RowOffset)> restored = [];
        foreach (var link in template.ExternalRowPointers)
        {
            int[] owners = Enumerable.Range(0, active.Count).Where(i => Owns(template, active[i], link.SourceRow)).ToArray();
            if (owners.Length == 1) restored.Add((link.Offset, owners[0], link.RowOffset));
        }
        return template with { ImportedRowPointers = restored, DroppedExternalPointers = template.DroppedExternalPointers - restored.Count };
    }

    public static void RequireTarget(UniversalRowTemplate owner, IReadOnlyList<UniversalRowTemplate> active,
        int offset, int placement, int rowOffset)
    {
        if (placement < 0 || placement >= active.Count || offset < 0 || offset % 4 != 0 ||
            offset + 4L > owner.Properties.Length || rowOffset < 0 || rowOffset >= UniversalLevelData.RowStride ||
            owner.ImportedRowPointers.Count(p => p.Offset == offset) != 1 || owner.InternalPointers.Any(p => p.Offset == offset))
            throw new InvalidDataException("Imported object-link fixup escaped its property/placement owner.");
        var target = active[placement];
        if (!owner.ExternalRowPointers.Contains((offset, target.SourceRowIndex, rowOffset)) ||
            !Owns(owner, target, target.SourceRowIndex) || active.Count(t => Owns(owner, t, target.SourceRowIndex)) != 1)
            throw new InvalidDataException("Imported object-link target changed or belongs to another group copy.");
    }
}
