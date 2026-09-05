namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeWadMappedEntry(int Index, long Offset, int ByteLength);
internal sealed record NativeWadEntryInsertion(int EntryIndex, long EntryRelativeOffset, int GrowthBytes);

/// <summary>
/// Adapts the existing source-validated WAD relocator's geometry for its private
/// texture consumers. Archive headers remain excluded, and every insertion must
/// account for the producer's complete entry growth. This is not a WAD parser
/// or authority to apply a retail plan to an independently relocated image.
/// </summary>
internal static class NativeWadRelocationMap
{
    public static NativeRelocationMap Create(NativeSkyWadRelocationPlan relocation,
        IEnumerable<NativeWadMappedEntry> sourceEntries, IEnumerable<NativeWadEntryInsertion> sourceInsertions)
    {
        ArgumentNullException.ThrowIfNull(relocation);
        ArgumentNullException.ThrowIfNull(sourceEntries);
        ArgumentNullException.ThrowIfNull(sourceInsertions);
        if (relocation.RelocatedEntryOffsets == null || relocation.EntryGrowthBytes == null)
            throw new InvalidDataException("The WAD relocation plan is missing its entry ownership tables.");
        NativeWadMappedEntry[] entries = sourceEntries.ToArray();
        NativeWadEntryInsertion[] insertions = sourceInsertions.ToArray();
        if (entries.Length == 0 || entries.Any(item => item == null || item.Index < 0 || item.ByteLength <= 0) ||
            entries.Select(item => item.Index).Distinct().Count() != entries.Length ||
            relocation.RelocatedEntryOffsets.Count != entries.Length ||
            relocation.EntryGrowthBytes.Any(pair => pair.Value < 0 || pair.Value % 2048 != 0 || !entries.Any(item => item.Index == pair.Key)) ||
            insertions.Any(item => item == null || item.GrowthBytes <= 0 || item.GrowthBytes % 2048 != 0 || !entries.Any(entry => entry.Index == item.EntryIndex)))
            throw new InvalidDataException("The WAD relocation geometry has duplicate, missing or invalid entries/growth.");
        entries = entries.OrderBy(item => item.Offset).ToArray();
        NativeAddressSpace original = new(NativeAddressKind.WadFile, "WAD.WAD", NativeAddressRevision.Original, relocation.OriginalWadSize);
        NativeAddressSpace final = new(NativeAddressKind.WadFile, "WAD.WAD", NativeAddressRevision.Final, relocation.ExpandedWadSize);
        List<NativeGrowingExtent> bodies = [];
        long sourceCursor = 2048, finalCursor = 2048;
        foreach (NativeWadMappedEntry entry in entries)
        {
            int growth = relocation.EntryGrowthBytes.GetValueOrDefault(entry.Index);
            NativeWadEntryInsertion[] owned = insertions.Where(item => item.EntryIndex == entry.Index).ToArray();
            if (entry.Offset != sourceCursor || !relocation.RelocatedEntryOffsets.TryGetValue(entry.Index, out long output) || output != finalCursor ||
                owned.Sum(item => (long)item.GrowthBytes) != growth ||
                owned.Any(item => item.EntryRelativeOffset < 2048 || item.EntryRelativeOffset > entry.ByteLength))
                throw new InvalidDataException("WAD entry ownership, packed extents or insertion growth disagree with the structural producer.");
            _ = new NativeByteExtent(original, entry.Offset, entry.ByteLength);
            _ = new NativeByteExtent(final, output, checked((long)entry.ByteLength + growth));
            if (entry.ByteLength > 2048)
                bodies.Add(new($"wad-entry-{entry.Index}", new NativeByteExtent(original, checked(entry.Offset + 2048), entry.ByteLength - 2048),
                    checked(output + 2048), owned.Select(item => new NativeExtentInsertion(item.EntryRelativeOffset - 2048, item.GrowthBytes)).ToArray()));
            else if (growth != 0)
                throw new InvalidDataException("Growth cannot target a WAD entry without an owned body.");
            sourceCursor = checked(sourceCursor + entry.ByteLength);
            finalCursor = checked(finalCursor + entry.ByteLength + growth);
        }
        if (sourceCursor != original.ByteLength || finalCursor != final.ByteLength || finalCursor - sourceCursor != relocation.WadGrowthBytes)
            throw new InvalidDataException("The typed WAD map does not account for the complete original/final archive extents.");
        return NativeRelocationMap.ForGrowth(original, final, bodies);
    }
}
