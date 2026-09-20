using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeOwnedOverlayBlockSize(string Id, int ByteLength);
internal sealed record NativeOwnedOverlayAllocation(string Id, NativeByteExtent Owner, bool IsPadding);
internal sealed record NativeOwnedOverlayBlock(string Id, NativeByteExtent Owner, byte[] Bytes, bool IsPadding = false);
internal sealed record NativeOwnedOverlayEntry(string Id, string BlockId, NativeByteExtent Owner);

/// <summary>
/// Neutral append geometry over a snapshot of the complete retained allocation.
/// It neither discovers free bytes nor authenticates source, behavior or hooks.
/// The source-owning consumer supplies its actual complete prefix, not a used
/// length or a trimmed zero tail, and retains all source/profile admission.
/// </summary>
internal sealed class NativeOwnedOverlayAppendLayout
{
    private const int SectorSize = 0x800;
    private readonly byte[] _prefix;

    private NativeOwnedOverlayAppendLayout(string identity, byte[] prefix, uint runtimeBase,
        int allocationCeiling, NativeOwnedOverlayBlockSize[] sizes, string paddingId)
    {
        if (sizes.Length == 0) throw new InvalidDataException("Native overlay requires at least one code block.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        long cursor = prefix.Length;
        foreach (var size in sizes)
        {
            if (size is null || string.IsNullOrWhiteSpace(size.Id))
                throw new InvalidDataException("Native overlay block identity is absent.");
            if (!ids.Add(size.Id)) throw new InvalidDataException("Native overlay block identity is duplicate.");
            if (size.Id == paddingId) throw new InvalidDataException("Native overlay padding identity is reserved.");
            if (size.ByteLength <= 0 || size.ByteLength % 4 != 0)
                throw new InvalidDataException("Native overlay block length must be positive and word aligned.");
            cursor += size.ByteLength; // Widened and bounded before any output allocation.
            if (cursor - prefix.Length > NativeOwnedOverlayMaterializer.MaximumAppendByteLength)
                throw new InvalidDataException("Native overlay append byte budget exceeded.");
        }
        long finalLength = (cursor + SectorSize - 1) & ~(long)(SectorSize - 1);
        if (finalLength > allocationCeiling)
            throw new InvalidDataException("Native overlay final padded capacity is exhausted.");
        if ((ulong)runtimeBase + (ulong)finalLength > 0x80200000UL)
            throw new InvalidDataException("Native overlay runtime extent exceeds native main RAM.");
        if (finalLength - prefix.Length > NativeOwnedOverlayMaterializer.MaximumAppendByteLength ||
            sizes.Length + (finalLength == cursor ? 0 : 1) > NativeOwnedOverlayMaterializer.MaximumBlocks)
            throw new InvalidDataException("Native overlay padding exceeds the append ownership budget.");
        _prefix = prefix;
        RetainedByteLength = prefix.Length;
        UsedByteLength = checked((int)cursor);
        FinalByteLength = checked((int)finalLength);
        CopyAnchor = checked(runtimeBase + (uint)FinalByteLength);
        RuntimeBase = runtimeBase;
        Domain = new(NativeAddressKind.LevelData, identity, NativeAddressRevision.Final, FinalByteLength);
        Arena = new(Domain, RetainedByteLength, FinalByteLength - RetainedByteLength);
        var blocks = new List<NativeOwnedOverlayAllocation>();
        cursor = RetainedByteLength;
        foreach (var size in sizes)
        {
            blocks.Add(new(size.Id, new(Domain, cursor, size.ByteLength), false));
            cursor += size.ByteLength;
        }
        if (cursor != FinalByteLength)
            blocks.Add(new(paddingId, new(Domain, cursor, FinalByteLength - cursor), true));
        Blocks = blocks.AsReadOnly();
    }

    internal NativeAddressSpace Domain { get; }
    internal NativeByteExtent Arena { get; }
    internal uint RuntimeBase { get; }
    internal int RetainedByteLength { get; }
    internal int UsedByteLength { get; }
    internal int FinalByteLength { get; }
    internal uint CopyAnchor { get; }
    internal IReadOnlyList<NativeOwnedOverlayAllocation> Blocks { get; }
    internal bool SourceCertified => false;
    internal byte[] CopyRetainedPrefix() => _prefix.ToArray();

    internal static NativeOwnedOverlayAppendLayout Create(string ownerIdentity, ReadOnlySpan<byte> completeRetainedPrefix,
        uint runtimeBase, int finalAllocationCeiling, IEnumerable<NativeOwnedOverlayBlockSize> blocks, string paddingId)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (string.IsNullOrWhiteSpace(ownerIdentity) || string.IsNullOrWhiteSpace(paddingId))
            throw new InvalidDataException("Native overlay owner and padding identity are required.");
        if (completeRetainedPrefix.Length == 0)
            throw new InvalidDataException("Native overlay retained prefix must be nonempty.");
        if (completeRetainedPrefix.Length % 4 != 0)
            throw new InvalidDataException("Native overlay retained prefix must be word aligned.");
        if (runtimeBase < 0x80000000 || runtimeBase >= 0x80200000 || runtimeBase % 4 != 0)
            throw new InvalidDataException("Native overlay runtime base must be word aligned native main RAM.");
        if (finalAllocationCeiling <= completeRetainedPrefix.Length || finalAllocationCeiling > 0x200000)
            throw new InvalidDataException("Native overlay allocation capacity must bound a positive append.");
        if ((ulong)runtimeBase + (ulong)completeRetainedPrefix.Length >= 0x80200000UL)
            throw new InvalidDataException("Native overlay retained runtime extent leaves no append capacity.");
        // Snapshot before invoking any caller code: even the sizes enumerator
        // can mutate the supplied prefix. It must never become retained state.
        byte[] prefix = completeRetainedPrefix.ToArray();
        var sizes = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(blocks, NativeOwnedOverlayMaterializer.MaximumBlocks, "overlay block");
        return new(ownerIdentity, prefix, runtimeBase, finalAllocationCeiling, sizes, paddingId);
    }

    internal byte[] Materialize(IEnumerable<NativeOwnedOverlayBlock> blocks,
        IEnumerable<NativeOwnedOverlayEntry> entries, IEnumerable<NativeByteExtent> reservations)
    {
        var payloads = NativeOwnedOverlayMaterializer.SnapshotBlocks(blocks);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in payloads)
        {
            if (!ReferenceEquals(block.Owner.Space, Domain))
                throw new InvalidDataException("Native overlay payload has another final domain.");
            if (!ids.Add(block.Id)) throw new InvalidDataException("Native overlay payload identity is duplicate.");
            var owner = Blocks.SingleOrDefault(b => b.Id == block.Id);
            if (block.IsPadding || owner?.IsPadding == true)
                throw new InvalidDataException("Native overlay padding is generated, not a caller payload.");
            if (owner is null || owner.Owner != block.Owner)
                throw new InvalidDataException("Native overlay payload does not match its assigned owner.");
        }
        if (payloads.Length != Blocks.Count(b => !b.IsPadding))
            throw new InvalidDataException("Native overlay payload coverage is incomplete.");
        var complete = payloads.ToList();
        foreach (var padding in Blocks.Where(b => b.IsPadding))
            complete.Add(new(padding.Id, padding.Owner, new byte[checked((int)padding.Owner.ByteLength)], true));
        byte[] appended = NativeOwnedOverlayMaterializer.Materialize(Arena, RuntimeBase, complete, entries, reservations);
        byte[] result = new byte[FinalByteLength];
        _prefix.CopyTo(result, 0);
        appended.CopyTo(result, RetainedByteLength);
        return result;
    }
}

/// <summary>
/// Neutral exact arena coverage and explicit-entry checks shared with the
/// historical retail wrapper. The arena may be a subextent of its Final domain.
/// Returned bytes are detached; neither labels nor entries confer source proof.
/// </summary>
internal static class NativeOwnedOverlayMaterializer
{
    internal const int MaximumAppendByteLength = 0x20000, MaximumBlocks = 64;

    internal static NativeOwnedOverlayBlock[] SnapshotBlocks(IEnumerable<NativeOwnedOverlayBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var result = new List<NativeOwnedOverlayBlock>();
        foreach (var block in blocks)
        {
            if (result.Count == MaximumBlocks) throw new InvalidDataException("Native overlay code block budget exceeded.");
            if (block is null || block.Owner is null || string.IsNullOrWhiteSpace(block.Id))
                throw new InvalidDataException("Native overlay code owner identity is absent.");
            if (block.Bytes is null || block.Bytes.LongLength != block.Owner.ByteLength || block.Bytes.Length > MaximumAppendByteLength)
                throw new InvalidDataException("Native overlay code owner payload length differs or exceeds its budget.");
            // Clone before MoveNext can mutate this payload, and before any
            // later caller-provided entries/reservations enumerator is invoked.
            result.Add(block with { Bytes = block.Bytes.ToArray() });
        }
        return result.ToArray();
    }

    internal static byte[] Materialize(NativeByteExtent arena, uint runtimeBase,
        IEnumerable<NativeOwnedOverlayBlock> blocks, IEnumerable<NativeOwnedOverlayEntry> entries,
        IEnumerable<NativeByteExtent> reservations)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(reservations);
        if (arena.Space.Kind != NativeAddressKind.LevelData || arena.Space.Revision != NativeAddressRevision.Final ||
            arena.Offset % 4 != 0 || arena.ByteLength % 4 != 0 || runtimeBase % 4 != 0 || runtimeBase < 0x80000000 ||
            runtimeBase >= 0x80200000 || (ulong)arena.EndExclusive > 0x80200000UL - runtimeBase)
            throw new InvalidDataException("Native code arena requires an aligned bounded final runtime domain.");
        if (arena.ByteLength > MaximumAppendByteLength)
            throw new InvalidDataException("Native code materialization byte budget exceeded.");
        var snapshot = SnapshotBlocks(blocks);
        var entrySnapshot = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(entries, 256, "code entry");
        var reservationSnapshot = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(reservations, 1024, "code reservation");
        if (entrySnapshot.Any(e => e is null)) throw new InvalidDataException("Native code entry is absent.");
        long cursor = arena.Offset;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in snapshot.OrderBy(b => b.Owner.Offset))
        {
            Within(block.Owner);
            if (!ids.Add(block.Id) || block.Owner.Offset < cursor)
                throw new InvalidDataException("Native code owners overlap or duplicate their identity.");
            if (block.Owner.Offset != cursor) throw new InvalidDataException("Native code coverage contains an unowned hole.");
            if (block.IsPadding && block.Bytes.AsSpan().ContainsAnyExcept((byte)0))
                throw new InvalidDataException("Native code padding contains nonzero bytes.");
            cursor = block.Owner.EndExclusive;
        }
        if (cursor != arena.EndExclusive) throw new InvalidDataException("Native code coverage does not fill its exact arena.");
        var entryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entrySnapshot)
        {
            Within(entry.Owner);
            if (string.IsNullOrWhiteSpace(entry.Id) || !entryIds.Add(entry.Id))
                throw new InvalidDataException("Native code entry identity is absent or duplicate.");
            var block = snapshot.SingleOrDefault(b => b.Id == entry.BlockId);
            if (block?.IsPadding == true) throw new InvalidDataException("Native code entry cannot target padding.");
            if (block is null || entry.Owner.Offset < block.Owner.Offset || entry.Owner.EndExclusive > block.Owner.EndExclusive)
                throw new InvalidDataException("Native code entry has no unique owning materialized block.");
        }
        foreach (var target in reservationSnapshot)
        {
            Within(target);
            if (!entrySnapshot.Any(e => e.Owner.Offset == target.Offset && e.Owner.EndExclusive >= target.EndExclusive))
                throw new InvalidDataException("Native code reservation lacks a declared materialized entry.");
        }
        var result = new byte[checked((int)arena.ByteLength)];
        foreach (var block in snapshot) block.Bytes.CopyTo(result, checked((int)(block.Owner.Offset - arena.Offset)));
        return result;

        void Within(NativeByteExtent? owner)
        {
            if (owner is null || !ReferenceEquals(owner.Space, arena.Space))
                throw new InvalidDataException("Native code owner has another or absent final domain.");
            if (owner.Offset < arena.Offset || owner.EndExclusive > arena.EndExclusive || owner.Offset % 4 != 0 || owner.ByteLength % 4 != 0)
                throw new InvalidDataException("Native code owner is unaligned or outside its exact arena.");
        }
    }
}
