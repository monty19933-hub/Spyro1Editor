using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeAddressKind { WadFile, LevelData, Scene, ExecutableFile, DiscLogical }
internal enum NativeAddressRevision { Original, Final }

/// <summary>
/// An identity-bearing address domain. Labels correlate an already validated
/// producer's evidence; they do not establish source identity or publication
/// authority. Distinct instances remain distinct even when labels match.
/// </summary>
internal sealed class NativeAddressSpace
{
    public NativeAddressSpace(NativeAddressKind kind, string ownerIdentity, NativeAddressRevision revision, long byteLength)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(revision) || string.IsNullOrWhiteSpace(ownerIdentity) || byteLength <= 0)
            throw new InvalidDataException("An address space requires a known domain/revision, owner identity and positive bounded length.");
        Kind = kind; OwnerIdentity = ownerIdentity; Revision = revision; ByteLength = byteLength;
    }
    public NativeAddressKind Kind { get; }
    public string OwnerIdentity { get; }
    public NativeAddressRevision Revision { get; }
    public long ByteLength { get; }
}

internal sealed record NativeByteExtent
{
    public NativeByteExtent(NativeAddressSpace space, long offset, long byteLength)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (offset < 0 || byteLength <= 0 || byteLength > space.ByteLength || offset > space.ByteLength - byteLength)
            throw new InvalidDataException("An extent is empty, negative, overflowing or outside its exact address space.");
        Space = space; Offset = offset; ByteLength = byteLength;
    }
    public NativeAddressSpace Space { get; }
    public long Offset { get; }
    public long ByteLength { get; }
    public long EndExclusive => Offset + ByteLength; // Constructor proves this cannot overflow.
}

internal sealed record NativeRelocationRegion(string Id, NativeByteExtent? Original, NativeByteExtent Final, int Alignment = 1);
internal sealed record NativeMappedExtent(string RegionId, NativeByteExtent Original, NativeByteExtent Final);
internal sealed record NativeExtentInsertion(long RelativeOffset, long ByteLength, int Alignment = 1);
internal sealed record NativeGrowingExtent(string Id, NativeByteExtent Original, long FinalOffset,
    IReadOnlyList<NativeExtentInsertion> Insertions, int Alignment = 1);
internal sealed record NativeTableGrowth(string Id, NativeByteExtent Original, long FinalOffset,
    int RowByteLength, int CountBefore, int CountAfter, int Alignment = 1);
internal sealed record NativeReferenceEncoding(uint OriginalBase = 0, uint FinalBase = 0, int AddressUnitBytes = 1,
    bool ZeroIsValidAddress = false);
internal sealed record NativeResolvedReference(NativeMappedExtent Field, NativeMappedExtent Target, uint Before, uint After);

/// <summary>
/// Immutable geometry of explicitly owned preserved/inserted byte ranges. It
/// does not move bytes, discover pointer ownership, inspect hashes or authorize
/// growth. Unowned holes and insertion crossings fail closed. Original and
/// final extents must be supplied by the same validated structural producer.
/// </summary>
internal sealed class NativeRelocationMap
{
    private readonly IReadOnlyList<NativeRelocationRegion> _regions;
    public NativeRelocationMap(NativeAddressSpace original, NativeAddressSpace final,
        IEnumerable<NativeRelocationRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(regions);
        if (original.Revision != NativeAddressRevision.Original || final.Revision != NativeAddressRevision.Final)
            throw new InvalidDataException("Relocation must map an original domain to a final domain.");
        NativeRelocationRegion[] snapshot = regions.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(region => region == null || string.IsNullOrWhiteSpace(region.Id)) ||
            snapshot.Select(region => region.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new InvalidDataException("Relocation regions require unique nonempty ownership identities.");
        foreach (NativeRelocationRegion region in snapshot)
        {
            if (region.Final == null || region.Final.Space != final ||
                region.Original != null && (region.Original.Space != original || region.Original.ByteLength != region.Final.ByteLength))
                throw new InvalidDataException("A preserved extent must retain its length and exact original/final address domains.");
            RequireAlignment(region.Final.Offset, region.Alignment);
            if (region.Original != null) RequireAlignment(region.Original.Offset, region.Alignment);
        }
        RequireDisjoint(snapshot.Where(region => region.Original != null).Select(region => region.Original!));
        RequireDisjoint(snapshot.Select(region => region.Final));
        Original = original; Final = final;
        _regions = Array.AsReadOnly(snapshot.OrderBy(region => region.Final.Offset).ToArray());
    }
    public NativeAddressSpace Original { get; }
    public NativeAddressSpace Final { get; }
    public IReadOnlyList<NativeRelocationRegion> Regions => _regions;

    public NativeMappedExtent Map(NativeByteExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);
        if (extent.Space != Original)
            throw new InvalidDataException("The requested extent belongs to another owner or address revision.");
        NativeRelocationRegion? region = _regions.SingleOrDefault(candidate => candidate.Original != null &&
            extent.Offset >= candidate.Original.Offset && extent.EndExclusive <= candidate.Original.EndExclusive);
        if (region == null)
            throw new InvalidDataException("The requested span is unowned or crosses an insertion/ownership boundary.");
        return new(region.Id, extent,
            new NativeByteExtent(Final, checked(region.Final.Offset + (extent.Offset - region.Original!.Offset)), extent.ByteLength));
    }

    /// <summary>Resolve one declared little-endian u32 reference; zero/null pointers need an explicit separate policy.</summary>
    public NativeResolvedReference ResolveReference(NativeByteExtent field, NativeByteExtent target,
        ReadOnlySpan<byte> fieldPreimage, NativeReferenceEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(encoding);
        if (field.ByteLength != 4 || fieldPreimage.Length != 4)
            throw new InvalidDataException("A native u32 reference requires one exact four-byte field/preimage.");
        RequireAlignment(field.Offset, 4);
        NativeMappedExtent mappedField = Map(field);
        NativeMappedExtent mappedTarget = Map(target);
        RequireAlignment(mappedField.Final.Offset, 4);
        uint before = Encode(target.Offset, encoding.OriginalBase, encoding.AddressUnitBytes);
        uint after = Encode(mappedTarget.Final.Offset, encoding.FinalBase, encoding.AddressUnitBytes);
        if (!encoding.ZeroIsValidAddress && (before == 0 || after == 0))
            throw new InvalidDataException("A zero-encoded address requires an explicit non-null reference policy.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(fieldPreimage) != before)
            throw new InvalidDataException("The declared reference target does not match its exact source field preimage.");
        return new(mappedField, mappedTarget, before, after);
    }

    public static NativeRelocationMap ForGrowth(NativeAddressSpace original, NativeAddressSpace final,
        IEnumerable<NativeGrowingExtent> extents)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(extents);
        List<NativeRelocationRegion> regions = [];
        foreach (NativeGrowingExtent extent in extents)
        {
            if (extent == null || extent.Original == null || extent.Insertions == null ||
                extent.Original.Space != original || string.IsNullOrWhiteSpace(extent.Id) || extent.Insertions.Any(item => item == null))
                throw new InvalidDataException("A growing extent has no exact original owner.");
            RequireAlignment(extent.Original.Offset, extent.Alignment);
            RequireAlignment(extent.FinalOffset, extent.Alignment);
            NativeExtentInsertion[] insertions = extent.Insertions.OrderBy(item => item.RelativeOffset).ToArray();
            long previous = 0, growth = 0;
            int part = 0;
            foreach (NativeExtentInsertion insertion in insertions)
            {
                if (insertion.RelativeOffset < previous || insertion.RelativeOffset > extent.Original.ByteLength || insertion.ByteLength <= 0 ||
                    part > 0 && insertion.RelativeOffset == previous)
                    throw new InvalidDataException("Insertion positions must be unique, positive-length and inside the owned extent.");
                RequireAlignment(insertion.ByteLength, insertion.Alignment);
                long output = checked(extent.FinalOffset + insertion.RelativeOffset + growth);
                RequireAlignment(output, insertion.Alignment);
                AddPreserved(insertion.RelativeOffset);
                regions.Add(new($"{extent.Id}:inserted:{part}", null, new NativeByteExtent(final, output, insertion.ByteLength), insertion.Alignment));
                growth = checked(growth + insertion.ByteLength);
                previous = insertion.RelativeOffset;
                part++;
            }
            AddPreserved(extent.Original.ByteLength);

            void AddPreserved(long end)
            {
                if (end == previous) return;
                long length = end - previous;
                regions.Add(new($"{extent.Id}:preserved:{part}",
                    new NativeByteExtent(original, checked(extent.Original.Offset + previous), length),
                    new NativeByteExtent(final, checked(extent.FinalOffset + previous + growth), length)));
            }
        }
        return new(original, final, regions);
    }

    public static NativeGrowingExtent Table(NativeTableGrowth table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Original == null || string.IsNullOrWhiteSpace(table.Id) ||
            table.RowByteLength <= 0 || table.CountBefore <= 0 || table.CountAfter < table.CountBefore ||
            table.Original.ByteLength != checked((long)table.RowByteLength * table.CountBefore))
            throw new InvalidDataException("Table growth must preserve complete existing rows with a nondecreasing checked row count.");
        long added = checked((long)(table.CountAfter - table.CountBefore) * table.RowByteLength);
        return new(table.Id, table.Original, table.FinalOffset,
            added == 0 ? [] : [new(table.Original.ByteLength, added, table.Alignment)], table.Alignment);
    }

    public static void RequireDisjoint(IEnumerable<NativeByteExtent> extents)
    {
        ArgumentNullException.ThrowIfNull(extents);
        NativeByteExtent[] snapshot = extents.ToArray();
        if (snapshot.Any(extent => extent == null))
            throw new InvalidDataException("A write/ownership extent is missing.");
        foreach (IGrouping<NativeAddressSpace, NativeByteExtent> group in snapshot.GroupBy(extent => extent.Space))
        {
            NativeByteExtent[] ordered = group.OrderBy(extent => extent.Offset).ToArray();
            for (int index = 1; index < ordered.Length; index++)
                if (ordered[index].Offset < ordered[index - 1].EndExclusive)
                    throw new InvalidDataException("Owned extents or writes conflict in the same address domain.");
        }
    }

    private static uint Encode(long offset, uint baseValue, int unit)
    {
        RequireAlignment(offset, unit);
        long encoded = checked(offset / unit + baseValue);
        if (encoded > uint.MaxValue) throw new InvalidDataException("A reference exceeds its u32 encoding capacity.");
        return (uint)encoded;
    }
    private static void RequireAlignment(long value, int alignment)
    {
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0 || value < 0 || value % alignment != 0)
            throw new InvalidDataException("An address, extent or reference violates its positive power-of-two alignment.");
    }
}
