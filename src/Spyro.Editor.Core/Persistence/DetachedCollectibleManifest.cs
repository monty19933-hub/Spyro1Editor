using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Neutral authored identity ledger, not a native row, RAM, card or publication owner.
/// Callers persist each instance GUID when first authored; placement order, position,
/// appearance and native level IDs never assign these bits. Retired IDs remain forever.
/// </summary>
internal sealed class DetachedCollectibleManifest
{
    internal const ushort FormatVersion = 1;
    internal const int MaximumLifetimeInstances = 4096;
    internal const int HeaderBytes = 80;
    internal const int EntryBytes = 24;
    internal const int MaximumEncodedBytes = HeaderBytes + EntryBytes * MaximumLifetimeInstances + 32;
    private static ReadOnlySpan<byte> Magic => "SPYCMAN\0"u8;
    private readonly Entry[] entries;
    private readonly byte[] encoded;
    private readonly Dictionary<Guid, int> bits;

    internal sealed record Entry(Guid InstanceId, int BitIndex, bool Retired);

    private DetachedCollectibleManifest(Guid contentId, uint revision, byte[] parentDigest, Entry[] entries)
    {
        ContentId = contentId;
        Revision = revision;
        ParentSha256 = revision == 1 ? null : Convert.ToHexStringLower(parentDigest);
        this.entries = entries.ToArray();
        Entries = Array.AsReadOnly(this.entries);
        bits = this.entries.ToDictionary(entry => entry.InstanceId, entry => entry.BitIndex);
        encoded = Encode(parentDigest);
        Sha256 = DetachedCollectibleBinary.Hash(encoded);
    }

    internal Guid ContentId { get; }
    internal uint Revision { get; }
    internal string? ParentSha256 { get; }
    internal string Sha256 { get; }
    internal IReadOnlyList<Entry> Entries { get; }
    internal int LifetimeCount => entries.Length;
    internal int ActiveCount => entries.Count(entry => !entry.Retired);

    internal static DetachedCollectibleManifest CreateNew(Guid contentId, IEnumerable<Guid> instanceIds)
    {
        if (contentId == Guid.Empty) throw new InvalidDataException("A nonempty content identity is required.");
        Guid[] ids = ReadUniqueIds(instanceIds, nameof(instanceIds));
        Array.Sort(ids, CompareIds);
        return new(contentId, 1, new byte[32], ids.Select((id, bit) => new Entry(id, bit, false)).ToArray());
    }

    /// <summary>Only explicit additions and retirements can update the ledger. No ID is removed or reused.</summary>
    internal DetachedCollectibleManifest Advance(IEnumerable<Guid> addedIds, IEnumerable<Guid> retiredIds)
    {
        Guid[] additions = ReadUniqueIds(addedIds, nameof(addedIds));
        Guid[] retirements = ReadUniqueIds(retiredIds, nameof(retiredIds));
        if (additions.Length == 0 && retirements.Length == 0)
            throw new InvalidDataException("A manifest update must explicitly add or retire an identity.");
        if (Revision == uint.MaxValue) throw new InvalidDataException("Manifest revision capacity is exhausted.");
        if (LifetimeCount + additions.Length > MaximumLifetimeInstances)
            throw new InvalidDataException("Lifetime identity capacity is exhausted; retired bits are not reusable.");
        foreach (Guid id in additions)
            if (bits.ContainsKey(id)) throw new InvalidDataException("An existing or retired identity cannot be added again.");
        foreach (Guid id in retirements)
            if (!bits.TryGetValue(id, out int index) || entries[index].Retired)
                throw new InvalidDataException("Only an existing active identity may be explicitly retired.");
        var retiring = retirements.ToHashSet();
        Array.Sort(additions, CompareIds);
        Entry[] next = entries.Select(entry => retiring.Contains(entry.InstanceId) ? entry with { Retired = true } : entry)
            .Concat(additions.Select((id, index) => new Entry(id, LifetimeCount + index, false))).ToArray();
        return new(ContentId, checked(Revision + 1), Convert.FromHexString(Sha256), next);
    }

    internal Entry GetEntry(Guid instanceId) => bits.TryGetValue(instanceId, out int index)
        ? entries[index] : throw new InvalidDataException("The instance identity is absent from this manifest.");

    /// <summary>
    /// Parsing proves a self-consistent document, not its historical authority. Migration additionally
    /// requires this exact trusted predecessor, immutable bit owners and non-reversible retirement.
    /// </summary>
    internal void RequireDirectSuccessorOf(DetachedCollectibleManifest previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (ContentId != previous.ContentId || previous.Revision == uint.MaxValue || Revision != previous.Revision + 1 ||
            ParentSha256 != previous.Sha256 || LifetimeCount < previous.LifetimeCount)
            throw new InvalidDataException("Manifest is not the direct successor of the supplied content ledger.");
        bool changed = LifetimeCount != previous.LifetimeCount;
        for (int index = 0; index < previous.LifetimeCount; index++)
        {
            Entry before = previous.entries[index], after = entries[index];
            if (before.InstanceId != after.InstanceId || before.BitIndex != after.BitIndex || (before.Retired && !after.Retired))
                throw new InvalidDataException("A manifest update removed/reassigned a bit or revived a retired identity.");
            changed |= before.Retired != after.Retired;
        }
        for (int index = previous.LifetimeCount; index < LifetimeCount; index++)
            if (entries[index].Retired || (index > previous.LifetimeCount && CompareIds(entries[index - 1].InstanceId, entries[index].InstanceId) >= 0))
                throw new InvalidDataException("New identities must be active and canonically ordered within their append batch.");
        if (!changed) throw new InvalidDataException("A manifest successor has no explicit identity change.");
    }

    internal byte[] SerializeCanonical() => encoded.ToArray();

    internal static DetachedCollectibleManifest Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes + 32 || bytes.Length > MaximumEncodedBytes || !bytes[..8].SequenceEqual(Magic))
            throw new InvalidDataException("Unknown or malformed detached collectible manifest.");
        if (DetachedCollectibleBinary.U16(bytes, 8) != FormatVersion)
            throw new InvalidDataException("Unsupported collectible manifest format version; no implicit migration is allowed.");
        uint count = DetachedCollectibleBinary.U32(bytes, 0x24);
        if (DetachedCollectibleBinary.U16(bytes, 0xA) != HeaderBytes || count > MaximumLifetimeInstances ||
            DetachedCollectibleBinary.U32(bytes, 0xC) != bytes.Length || bytes.Length != HeaderBytes + (int)count * EntryBytes + 32 ||
            !DetachedCollectibleBinary.IsZero(bytes.Slice(0x48, 8)))
            throw new InvalidDataException("Manifest lengths, count or reserved fields are invalid.");
        DetachedCollectibleBinary.RequireChecksum(bytes);
        Guid contentId = new(bytes.Slice(0x10, 16), bigEndian: true);
        uint revision = DetachedCollectibleBinary.U32(bytes, 0x20);
        byte[] parent = bytes.Slice(0x28, 32).ToArray();
        if (contentId == Guid.Empty || revision == 0 || (revision == 1) != DetachedCollectibleBinary.IsZero(parent))
            throw new InvalidDataException("Manifest content identity, revision or predecessor is invalid.");
        var entries = new Entry[(int)count];
        var seen = new HashSet<Guid>();
        for (int index = 0; index < entries.Length; index++)
        {
            ReadOnlySpan<byte> entry = bytes.Slice(HeaderBytes + index * EntryBytes, EntryBytes);
            Guid id = new(entry[..16], bigEndian: true);
            if (id == Guid.Empty || !seen.Add(id) || DetachedCollectibleBinary.U32(entry, 16) != index || entry[20] > 1 ||
                !DetachedCollectibleBinary.IsZero(entry.Slice(21, 3)))
                throw new InvalidDataException("Manifest contains an invalid, duplicate or noncontiguous bit owner.");
            entries[index] = new(id, index, entry[20] != 0);
            if (revision == 1 && (entries[index].Retired || (index > 0 && CompareIds(entries[index - 1].InstanceId, id) >= 0)))
                throw new InvalidDataException("A new manifest must contain only active, canonically sorted identities.");
        }
        return new(contentId, revision, parent, entries);
    }

    private byte[] Encode(byte[] parentDigest)
    {
        var bytes = new byte[HeaderBytes + entries.Length * EntryBytes + 32];
        Magic.CopyTo(bytes);
        DetachedCollectibleBinary.Put16(bytes, 8, FormatVersion);
        DetachedCollectibleBinary.Put16(bytes, 0xA, HeaderBytes);
        DetachedCollectibleBinary.Put32(bytes, 0xC, (uint)bytes.Length);
        DetachedCollectibleBinary.PutGuid(bytes, 0x10, ContentId);
        DetachedCollectibleBinary.Put32(bytes, 0x20, Revision);
        DetachedCollectibleBinary.Put32(bytes, 0x24, (uint)entries.Length);
        parentDigest.CopyTo(bytes, 0x28);
        foreach (Entry entry in entries)
        {
            int offset = HeaderBytes + entry.BitIndex * EntryBytes;
            DetachedCollectibleBinary.PutGuid(bytes, offset, entry.InstanceId);
            DetachedCollectibleBinary.Put32(bytes, offset + 16, (uint)entry.BitIndex);
            bytes[offset + 20] = entry.Retired ? (byte)1 : (byte)0;
        }
        DetachedCollectibleBinary.WriteChecksum(bytes);
        return bytes;
    }

    private static Guid[] ReadUniqueIds(IEnumerable<Guid> values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        var ids = new HashSet<Guid>();
        foreach (Guid id in values)
        {
            if (ids.Count == MaximumLifetimeInstances) throw new InvalidDataException("Identity input exceeds the bounded lifetime capacity.");
            if (id == Guid.Empty || !ids.Add(id)) throw new InvalidDataException("Identity input contains an empty or duplicate GUID.");
        }
        return ids.ToArray();
    }

    private static int CompareIds(Guid left, Guid right) => string.CompareOrdinal(left.ToString("N"), right.ToString("N"));
}

internal static class DetachedCollectibleBinary
{
    internal static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    internal static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    internal static void Put16(Span<byte> bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes[offset..], value);
    internal static void Put32(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    internal static void PutGuid(Span<byte> bytes, int offset, Guid value) => value.TryWriteBytes(bytes.Slice(offset, 16), bigEndian: true, out _);
    internal static bool IsZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes) if (value != 0) return false;
        return true;
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void WriteChecksum(Span<byte> bytes) => SHA256.HashData(bytes[..^32], bytes[^32..]);
    internal static void RequireChecksum(ReadOnlySpan<byte> bytes)
    {
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes[..^32]), bytes[^32..]))
            throw new InvalidDataException("Detached collectible integrity checksum does not match; preserve the original bytes.");
    }
}
