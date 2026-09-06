using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal sealed class NativeResourceOriginalBytePatch
{
    private readonly byte[] _before, _after;
    internal NativeResourceOriginalBytePatch(string id, int offset, ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    { Id = id; Offset = offset; _before = before.ToArray(); _after = after.ToArray(); }
    internal string Id { get; }
    internal int Offset { get; }
    internal int ByteLength => _before.Length;
    internal int AfterByteLength => _after.Length;
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
}

/// <summary>One original-address structural layout; never a native source or publication issuer.</summary>
internal sealed class NativeLevelResourceCompositionLayout
{
    private readonly byte[] _original, _final;
    private readonly NativeLevelDataGrowthLayout _growth;
    private readonly IReadOnlyList<NativeResourceOriginalBytePatch> _patches;
    internal NativeLevelResourceCompositionLayout(NativeMobyModelRegistrationSource source, int dataEntry,
        long dataWadOffset, IReadOnlyList<NativeSkyRelocationPayload> payloads,
        IReadOnlyList<NativeResourceOriginalBytePatch> patches, Func<NativeByteExtent, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(read);
        if (dataEntry < 0 || dataWadOffset < 0 || dataWadOffset > long.MaxValue - 0x1000000)
            throw new InvalidDataException("Combined resource WAD owner exceeds its bounded address domain.");
        source.RequireSource(read); Source = source;
        var sourceBytes = read(new(source.Original, 0, source.Original.ByteLength));
        if (sourceBytes == null || sourceBytes.LongLength != source.Original.ByteLength)
            throw new InvalidDataException("Combined resource source read length differs from its exact owner.");
        _original = sourceBytes.ToArray();
        source.RequireSource(e => _original.AsSpan((int)e.Offset, (int)e.ByteLength).ToArray());
        var inputs = NativeGlobalSoundMergeLayout.BoundedSnapshot(payloads, 1, 1024, "resource payload");
        var changes = NativeGlobalSoundMergeLayout.BoundedSnapshot(patches, 0, 65536, "resource byte patch");
        if (inputs.Any(p => p == null || p.Bytes == null || p.SubfileIndex is < 0 or > 9 || p.PatchIndex < 0) ||
            inputs.Select(p => p.PatchIndex).Distinct().Count() != inputs.Length)
            throw new InvalidDataException("Combined resource payload identity/domain is missing or duplicated.");
        // Charge aliases separately before taking immutable copies. A bounded
        // request count alone does not bound byte amplification.
        if (inputs.Sum(p => (long)p.Bytes.Length) > 0x800000)
            throw new InvalidDataException("Combined resource payload byte budget exceeds 8 MiB before copying.");
        if (changes.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || p.Offset < 0x800 || p.ByteLength <= 0 ||
            p.Offset + (long)p.ByteLength > _original.Length || p.AfterByteLength != p.ByteLength))
            throw new InvalidDataException("Original-byte edits require bounded nonheader equal-width preimages.");
        if (changes.Sum(p => (long)p.ByteLength) > 0x800000)
            throw new InvalidDataException("Combined resource original patch byte budget exceeds 8 MiB.");
        // Upper-bound the later preservation partition, overlap and mapping
        // scans across every owner. Refuse before geometry or byte materialization.
        long work = changes.Length * (inputs.Length + 1L) + 64L * (inputs.Count(p => p.SubfileIndex == 2) + 1);
        for (int owner = 0; owner < 10; owner++)
        {
            int start = Integer(_original, owner * 8), length = Integer(_original, owner * 8 + 4);
            int payloadCount = inputs.Count(p => p.SubfileIndex == owner);
            int patchCount = changes.Count(p => p.Offset >= start && p.Offset < start + (long)length);
            work += (2L + 2L * payloadCount + 2L * patchCount) * (changes.Length + payloadCount + 1L);
        }
        if (work > 4_000_000)
            throw new InvalidDataException("Combined resource preservation work budget exceeds four million bounded scans.");
        inputs = inputs.Select(p => p with { Bytes = p.Bytes.ToArray() }).OrderBy(p => p.SubfileIndex)
            .ThenBy(p => p.ModelBlockOffset).ThenBy(p => p.PatchIndex).ToArray();
        foreach (var p in inputs)
        {
            int offset = Integer(_original, p.SubfileIndex * 8), length = Integer(_original, p.SubfileIndex * 8 + 4);
            if (p.StorageWadEntry != dataEntry || p.WadLba != 37 || p.OriginalWadOffset != dataWadOffset + offset + p.ModelBlockOffset ||
                p.ModelBlockOffset < 0 || p.ModelBlockOffset > length || p.OriginalLength < 0 || p.OriginalLength > length - p.ModelBlockOffset)
                throw new InvalidDataException("Resource payload does not name its original subfile/WAD owner.");
            if (p.OriginalLength != 0 && (p.SubfileIndex != 3 || p.ModelBlockOffset != 0 || p.OriginalLength != length || p.Bytes.Length != length))
                throw new InvalidDataException("Only an exact-size complete source-owned scene replacement is supported; other original resources cannot be replaced.");
            if (p.SubfileIndex == 2 && p.ModelBlockOffset != length)
            {
                // Player stream insertion has an explicit start owner. Do not
                // let the generic right-boundary rule move an empty root0 past it.
                int root0 = Integer(_original, 0x50) - offset, next = Integer(_original, 0x54) - offset;
                if (p.OriginalLength != 0 || p.ModelBlockOffset != next - 4 || p.ModelBlockOffset <= root0)
                    throw new InvalidDataException("Model prefix insertion requires a nonempty root0 stream and its owned terminator; generic packages cannot be split.");
            }
        }
        _growth = new(_original.AsSpan(0, 0x800), _original.Length, inputs);
        if (_growth.FinalLength > 0x800000 || dataWadOffset > long.MaxValue - _growth.FinalLength)
            throw new InvalidDataException("Combined resource final level exceeds its bounded byte/address domain.");
        Final = new(NativeAddressKind.LevelData, source.Original.OwnerIdentity + ":combined-final", NativeAddressRevision.Final, _growth.FinalLength);
        _final = _growth.Expand(_original);
        foreach (var root in source.Registrations)
        {
            long expected = root.Slot == 0 ? _growth.Subfiles[2].FinalOffset + (long)root.RootDataOffset - source.ModelOffset
                : MapOriginalRange((int)root.RootDataOffset, 4).Offset;
            if (Word(_final, 0x50 + root.Slot * 4) != expected ||
                !_original.AsSpan(0x150 + root.Slot * 2, 2).SequenceEqual(_final.AsSpan(0x150 + root.Slot * 2, 2)))
                throw new InvalidDataException("Combined layout did not preserve the separately owned player start or an original generic root/class.");
        }
        List<NativeResourceOriginalBytePatch> unique = [];
        foreach (var p in changes.OrderBy(p => p.Offset).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            if (p.Offset < source.ModelEnd && p.Offset + (long)p.ByteLength > source.ModelOffset)
                throw new InvalidDataException("Original model resources cannot be overwritten by a scalar/byte stage.");
            var previous = unique.LastOrDefault();
            if (previous != null && p.Offset < previous.Offset + previous.ByteLength)
            {
                if (p.Offset == previous.Offset && p.ByteLength == previous.ByteLength &&
                    p.CopyBefore().AsSpan().SequenceEqual(previous.CopyBefore()) && p.CopyAfter().AsSpan().SequenceEqual(previous.CopyAfter())) continue;
                throw new InvalidDataException("Original-byte stage owners overlap with contradictory or partial edits.");
            }
            if (!_original.AsSpan(p.Offset, p.ByteLength).SequenceEqual(p.CopyBefore()))
                throw new InvalidDataException("Original-byte stage preimage changed.");
            var mapped = MapOriginalRange(p.Offset, p.ByteLength);
            p.CopyAfter().CopyTo(_final, (int)mapped.Offset); unique.Add(p);
        }
        _patches = Array.AsReadOnly(unique.ToArray());
        List<NativeSoundProtectedExtent> preserved = [];
        foreach (var owner in _growth.Subfiles)
        {
            int start = owner.OriginalOffset, end = start + owner.OriginalLength;
            var cuts = new[] { start, end }.Concat(owner.Payloads.SelectMany(p => new[] { start + p.ModelBlockOffset, start + p.ModelBlockOffset + p.OriginalLength }))
                .Concat(unique.Where(p => p.Offset >= start && p.Offset < end).SelectMany(p => new[] { p.Offset, p.Offset + p.ByteLength })).Distinct().Order().ToArray();
            for (int i = 0; i + 1 < cuts.Length; i++)
            {
                int offset = cuts[i], length = cuts[i + 1] - offset;
                if (length == 0 || unique.Any(p => offset >= p.Offset && offset + length <= p.Offset + p.ByteLength) ||
                    owner.Payloads.Any(p => p.OriginalLength > 0 && offset >= start + p.ModelBlockOffset && offset + length <= start + p.ModelBlockOffset + p.OriginalLength)) continue;
                var mapped = MapOriginalRange(offset, length);
                if (!_original.AsSpan(offset, length).SequenceEqual(_final.AsSpan((int)mapped.Offset, length)))
                    throw new InvalidDataException("Combined resource layout changed an unowned original byte.");
                preserved.Add(new($"original-{offset:X}", offset, (int)mapped.Offset, length, NativeMobyModelLoaderCertificate.Hash(_original.AsSpan(offset, length))));
            }
        }
        Preservations = preserved.AsReadOnly();
    }
    internal NativeMobyModelRegistrationSource Source { get; }
    internal NativeAddressSpace Final { get; }
    internal IReadOnlyList<NativeLevelDataSubfileGrowth> Subfiles => _growth.Subfiles;
    internal IReadOnlyList<NativeSoundProtectedExtent> Preservations { get; }
    internal IReadOnlyList<NativeResourceOriginalBytePatch> Patches => _patches;
    internal byte[] CopyFinalBytes() => _final.ToArray();
    internal byte[] CopyHeader() => _final.AsSpan(0, 0x800).ToArray();
    internal NativeByteExtent PayloadExtent(int patchIndex)
    {
        var owner = Subfiles.Single(s => s.Payloads.Any(p => p.PatchIndex == patchIndex));
        var payload = owner.Payloads.Single(p => p.PatchIndex == patchIndex);
        return new(Final, owner.FinalOffset + _growth.PayloadOffset(payload), payload.Bytes.Length);
    }
    internal NativeByteExtent MapOriginalRange(int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset + (long)length > _original.Length)
            throw new InvalidDataException("Original mapped byte range is empty, overflowing or outside its owner.");
        if (offset < 0x800)
        {
            if (offset + length > 0x800) throw new InvalidDataException("Original mapped range crosses the header boundary.");
            return new(Final, offset, length);
        }
        var owner = _growth.Subfiles.SingleOrDefault(s => offset >= s.OriginalOffset && offset + (long)length <= s.OriginalOffset + s.OriginalLength)
            ?? throw new InvalidDataException("Original mapped range crosses subfile ownership.");
        int relative = offset - owner.OriginalOffset, delta = 0;
        foreach (var p in owner.Payloads)
        {
            if ((p.OriginalLength > 0 && relative < p.ModelBlockOffset + p.OriginalLength && relative + length > p.ModelBlockOffset) ||
                (p.ModelBlockOffset > relative && p.ModelBlockOffset < relative + length))
                throw new InvalidDataException("Original scalar/range conflicts with a replacement or is split by an insertion.");
            if (p.ModelBlockOffset + p.OriginalLength <= relative) delta = checked(delta + p.Bytes.Length - p.OriginalLength);
        }
        return new(Final, checked(owner.FinalOffset + relative + delta), length);
    }
    internal void RequireFinalBody(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != _final.Length || !bytes[0x800..].SequenceEqual(_final.AsSpan(0x800)))
            throw new InvalidDataException("Combined resource final body differs from its single owned layout.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static int Integer(ReadOnlySpan<byte> bytes, int offset) => Word(bytes, offset) <= int.MaxValue ? (int)Word(bytes, offset)
        : throw new InvalidDataException("Combined resource offset exceeds bounded signed geometry.");
}
