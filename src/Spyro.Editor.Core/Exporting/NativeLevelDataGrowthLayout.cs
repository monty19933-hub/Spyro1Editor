using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeLevelDataSubfileGrowth(int Index, int OriginalOffset, int OriginalLength,
    int FinalOffset, int FinalLength, IReadOnlyList<NativeSkyRelocationPayload> Payloads)
{
    internal int Growth => FinalLength - OriginalLength;
}

/// <summary>
/// Outer level-entry geometry only. The producer still owns every reference
/// inside its replacement payloads, runtime RAM limits and resource semantics.
/// Descriptors and model roots are decoded fields; IDs and reserved bytes are
/// never searched for values that happen to resemble addresses.
/// </summary>
internal sealed class NativeLevelDataGrowthLayout
{
    private const int Sector = 0x800;
    private readonly byte[] _header;
    private readonly NativeRelocationMap _map;
    private readonly IReadOnlyList<NativeResolvedReference> _roots;
    internal IReadOnlyList<NativeLevelDataSubfileGrowth> Subfiles { get; }
    internal int OriginalLength { get; }
    internal int FinalLength { get; }
    internal int Growth => FinalLength - OriginalLength;

    internal NativeLevelDataGrowthLayout(ReadOnlySpan<byte> header, int entryLength,
        IReadOnlyList<NativeSkyRelocationPayload> payloads)
    {
        if (header.Length < Sector || entryLength < 5 * Sector || entryLength % Sector != 0)
            throw new InvalidDataException("Multi-subfile growth requires a complete sector-aligned native level entry.");
        _header = header[..Sector].ToArray();
        if (_header.AsSpan(0x1D0).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Multi-subfile growth cannot relocate unknown nonzero fields after the native 0x1D0-byte level header.");
        ArgumentNullException.ThrowIfNull(payloads);
        if (payloads.Count == 0 || payloads.Any(p => p == null || p.Bytes == null || p.PatchIndex < 0) ||
            payloads.Select(p => p.PatchIndex).Distinct().Count() != payloads.Count)
            throw new InvalidDataException("Native level growth requires distinct, complete payload identities.");
        var bySubfile = payloads.GroupBy(p => p.SubfileIndex).ToDictionary(g => g.Key,
            g => g.OrderBy(p => p.ModelBlockOffset).ThenBy(p => p.PatchIndex).Select(p => p with { Bytes = p.Bytes.ToArray() }).ToArray());
        List<NativeLevelDataSubfileGrowth> subfiles = [];
        int originalCursor = Sector, finalCursor = Sector;
        bool terminated = false;
        for (int index = 0; index < 10; index++)
        {
            uint rawOffset = Word(_header, index * 8), rawLength = Word(_header, index * 8 + 4);
            if (rawOffset == 0 && rawLength == 0) { terminated = true; continue; }
            if (terminated || rawOffset != originalCursor || rawLength == 0 || rawLength > int.MaxValue ||
                rawLength % Sector != 0 || rawLength > entryLength - (long)originalCursor)
                throw new InvalidDataException($"Native level descriptor {index} is not a complete packed sector-aligned extent.");
            int length = (int)rawLength;
            NativeSkyRelocationPayload[] changes = bySubfile.GetValueOrDefault(index) ?? [];
            int previousStart = -1, previousEnd = -1, exactGrowth = 0;
            foreach (NativeSkyRelocationPayload payload in changes)
            {
                if (payload.ModelBlockOffset < 0 || payload.OriginalLength < 0 || payload.Bytes.Length < payload.OriginalLength ||
                    payload.ModelBlockOffset + (long)payload.OriginalLength > length ||
                    (payload.ModelBlockOffset | payload.OriginalLength | payload.Bytes.Length) % 4 != 0 ||
                    payload.ModelBlockOffset <= previousStart || payload.ModelBlockOffset < previousEnd)
                    throw new InvalidDataException($"Native level subfile {index} has overlapping, misaligned or out-of-bounds payloads.");
                previousStart = payload.ModelBlockOffset;
                previousEnd = checked(payload.ModelBlockOffset + payload.OriginalLength);
                exactGrowth = checked(exactGrowth + payload.Bytes.Length - payload.OriginalLength);
            }
            int growth = checked((exactGrowth + Sector - 1) / Sector * Sector);
            subfiles.Add(new(index, originalCursor, length, finalCursor, checked(length + growth), Array.AsReadOnly(changes)));
            originalCursor = checked(originalCursor + length);
            finalCursor = checked(finalCursor + length + growth);
        }
        if (subfiles.Count < 4 || originalCursor != entryLength || bySubfile.Keys.Any(i => i < 0 || i >= subfiles.Count))
            throw new InvalidDataException("Multi-subfile growth requires the native VRAM/data/models/scene descriptors and complete entry coverage.");
        OriginalLength = entryLength; FinalLength = finalCursor;
        Subfiles = subfiles.AsReadOnly();
        NativeAddressSpace original = new(NativeAddressKind.LevelData, "native-level-entry", NativeAddressRevision.Original, entryLength);
        NativeAddressSpace final = new(NativeAddressKind.LevelData, "native-level-entry", NativeAddressRevision.Final, finalCursor);
        List<NativeGrowingExtent> extents = [new("header", new(original, 0, Sector), 0, [])];
        foreach (NativeLevelDataSubfileGrowth subfile in Subfiles)
        {
            // Growth follows the replaced source extent. A root entering that
            // replaced extent is separately rejected, so no target is guessed.
            var insertions = subfile.Payloads.Where(p => p.Bytes.Length > p.OriginalLength)
                .Select(p => new NativeExtentInsertion(p.ModelBlockOffset + p.OriginalLength, p.Bytes.Length - p.OriginalLength, 4)).ToList();
            int exact = subfile.Payloads.Sum(p => p.Bytes.Length - p.OriginalLength);
            if (subfile.Growth > exact) insertions.Add(new(subfile.OriginalLength, subfile.Growth - exact, 4));
            NativeExtentInsertion[] merged = insertions.GroupBy(p => p.RelativeOffset)
                .Select(g => new NativeExtentInsertion(g.Key, g.Sum(p => p.ByteLength), 4)).ToArray();
            extents.Add(new($"subfile-{subfile.Index}", new(original, subfile.OriginalOffset, subfile.OriginalLength), subfile.FinalOffset, merged, Sector));
        }
        _map = NativeRelocationMap.ForGrowth(original, final, extents);
        NativeLevelDataSubfileGrowth models = subfiles[2];
        List<NativeResolvedReference> roots = [];
        bool ended = false;
        uint previousRoot = 0;
        for (int index = 0; index < 64; index++)
        {
            int field = 0x50 + index * 4;
            uint root = Word(_header, field);
            ushort actorId = BinaryPrimitives.ReadUInt16LittleEndian(_header.AsSpan(0x150 + index * 2));
            if (root == 0)
            {
                ended = true;
                if (actorId != 0) throw new InvalidDataException("A native root terminator has a nonzero class ID.");
                continue;
            }
            if (ended || root <= previousRoot || root % 4 != 0 || root < models.OriginalOffset ||
                root + 4L > models.OriginalOffset + (long)models.OriginalLength)
                throw new InvalidDataException("Native model roots must be contiguous, ascending, aligned and owned by model subfile 2.");
            foreach (NativeSkyRelocationPayload payload in models.Payloads)
                if (payload.OriginalLength > 0 && root < models.OriginalOffset + (long)payload.ModelBlockOffset + payload.OriginalLength &&
                    root + 4L > models.OriginalOffset + (long)payload.ModelBlockOffset)
                    throw new InvalidDataException("A native model root enters replaced bytes; its new package binding requires a separate producer.");
            roots.Add(_map.ResolveReference(new(original, field, 4), new(original, root, 4), _header.AsSpan(field, 4), new()));
            previousRoot = root;
        }
        if (roots.Count == 0) throw new InvalidDataException("The native model root table is empty.");
        _roots = roots.AsReadOnly();
    }

    internal int PayloadOffset(NativeSkyRelocationPayload payload)
    {
        NativeLevelDataSubfileGrowth subfile = Subfiles.Single(s => s.Index == payload.SubfileIndex);
        NativeSkyRelocationPayload owned = subfile.Payloads.Single(p => p.PatchIndex == payload.PatchIndex);
        return checked(owned.ModelBlockOffset + subfile.Payloads.Where(p => p.ModelBlockOffset < owned.ModelBlockOffset)
            .Sum(p => p.Bytes.Length - p.OriginalLength));
    }

    internal byte[] Expand(byte[] entry)
    {
        if (entry.Length != OriginalLength || !entry.AsSpan(0, Sector).SequenceEqual(_header))
            throw new InvalidDataException("The native level entry no longer matches its planned header and extent.");
        byte[] output = new byte[FinalLength];
        _header.CopyTo(output, 0);
        foreach (NativeLevelDataSubfileGrowth subfile in Subfiles)
        {
            int sourceCursor = 0, outputCursor = 0;
            foreach (NativeSkyRelocationPayload payload in subfile.Payloads)
            {
                int unchanged = payload.ModelBlockOffset - sourceCursor;
                entry.AsSpan(subfile.OriginalOffset + sourceCursor, unchanged).CopyTo(output.AsSpan(subfile.FinalOffset + outputCursor));
                sourceCursor += unchanged; outputCursor += unchanged;
                if (payload.RequireLengthPrefix && (payload.OriginalLength < 4 ||
                    Word(entry, subfile.OriginalOffset + sourceCursor) != payload.OriginalLength))
                    throw new InvalidDataException($"Relocation payload {payload.PatchIndex} no longer matches its owned native length prefix.");
                payload.Bytes.CopyTo(output, subfile.FinalOffset + outputCursor);
                sourceCursor += payload.OriginalLength; outputCursor += payload.Bytes.Length;
            }
            entry.AsSpan(subfile.OriginalOffset + sourceCursor, subfile.OriginalLength - sourceCursor)
                .CopyTo(output.AsSpan(subfile.FinalOffset + outputCursor));
            // Each field belongs to its decoded offset/length descriptor. The
            // offset anchors the final subfile, including inserted prefixes.
            Put(output, subfile.Index * 8, checked((uint)subfile.FinalOffset));
            Put(output, subfile.Index * 8 + 4, checked((uint)subfile.FinalLength));
        }
        foreach (NativeResolvedReference root in _roots) Put(output, checked((int)root.Field.Final.Offset), root.After);
        return output;
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void Put(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
}
