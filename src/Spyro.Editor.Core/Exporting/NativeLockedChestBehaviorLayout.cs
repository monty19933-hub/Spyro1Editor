using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.TownSquareLockedChestSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// The three exact historical donor reads, not a certificate for their external
/// routines, global state, destination overlay, or executable. Capture preserves
/// the Town wrapper's read-all-then-guard ordering and retains detached bytes.
/// </summary>
internal sealed class NativeLockedChestBehaviorSource
{
    private readonly byte[][] _blocks;
    private NativeLockedChestBehaviorSource(byte[][] blocks) => _blocks = blocks;

    internal static NativeLockedChestBehaviorSource Capture(Func<long, int, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        byte[] setup = read(0x188719C, 0x30);
        byte[] state = read(0x1887330, 0x70C);
        byte[] reward = read(0x1881B64, 0x434);
        // Preserve guard ordering even if a later read returned no bytes. No
        // caller enumeration/read runs after each verified snapshot is taken.
        byte[] retainedSetup = setup.ToArray();
        Guard(retainedSetup, "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad", "Peace Keepers Locked Chest setup");
        byte[] retainedState = state.ToArray();
        Guard(retainedState, "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f", "Peace Keepers Locked Chest state/open path");
        byte[] retainedReward = reward.ToArray();
        Guard(retainedReward, "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056", "Peace Keepers class 0x000D reward handler");
        return new([retainedSetup, retainedState, retainedReward]);
    }

    internal byte[] CopySetup() => _blocks[0].ToArray();
    internal byte[] CopyState() => _blocks[1].ToArray();
    internal byte[] CopyReward() => _blocks[2].ToArray();
    private static void Guard(byte[] bytes, string expected, string label)
    {
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, found {actual}.");
    }
}

internal sealed record NativeLockedChestCodeSegment(string Id, uint SourcePc, uint FinalPc, byte[] Bytes);
internal sealed record NativeLockedChestExternalTarget(uint SourceTarget, uint FinalTarget);
internal enum NativeLockedChestTargetKind { InternalSegment, ExplicitExternal, RetainedUnprovenCall }
internal sealed record NativeLockedChestTransfer(string SegmentId, int SegmentOffset, uint SourcePc, uint FinalPc,
    uint SourceTarget, uint FinalTarget, uint BeforeWord, uint AfterWord, uint? DelayWord, NativeLockedChestTargetKind TargetKind);

/// <summary>
/// The historical chest direct-transfer relocation grammar with a complete byte
/// inverse. Unlike the stricter native behavior fragment linker, this compatibility
/// kernel retains unmapped JALs explicitly unproven; it cannot issue a general
/// behavior/source certificate. It does not discover reachable instructions,
/// certify register transfers, invent veneers, or introduce Fresh admission.
/// </summary>
internal sealed class NativeLockedChestCodeRelocation
{
    private readonly NativeLockedChestCodeSegment[] _segments;
    private readonly byte[] _bytes;
    private NativeLockedChestCodeRelocation(NativeLockedChestCodeSegment[] segments, byte[] bytes,
        List<NativeLockedChestTransfer> transfers)
    { _segments = segments; _bytes = bytes; Transfers = transfers.AsReadOnly(); }

    internal IReadOnlyList<NativeLockedChestTransfer> Transfers { get; }
    internal bool CompleteExternalOwnersProven => false;
    internal byte[] CopyBytes() => _bytes.ToArray();

    internal static NativeLockedChestCodeRelocation Compile(IEnumerable<NativeLockedChestCodeSegment> segments,
        IEnumerable<NativeLockedChestExternalTarget> externalTargets)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(externalTargets);
        List<NativeLockedChestCodeSegment> retained = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        int total = 0;
        foreach (var segment in segments)
        {
            if (retained.Count == 16) throw new InvalidDataException("Locked-chest code segment budget exceeded.");
            if (segment is null || string.IsNullOrWhiteSpace(segment.Id) || !ids.Add(segment.Id) || segment.Bytes is null ||
                segment.Bytes.Length == 0 || segment.Bytes.Length % 4 != 0 || segment.Bytes.Length > 0x20000)
                throw new InvalidDataException("Locked-chest code segment identity or length is invalid.");
            RequireExtent(segment.SourcePc, segment.Bytes.Length);
            RequireExtent(segment.FinalPc, segment.Bytes.Length);
            total += segment.Bytes.Length;
            if (total > 0x20000) throw new InvalidDataException("Locked-chest code byte budget exceeded.");
            if (retained.Count != 0 && (ulong)retained[^1].FinalPc + (uint)retained[^1].Bytes.Length != segment.FinalPc)
                throw new InvalidDataException("Locked-chest final code segments must be adjacent without overlap or holes.");
            // Snapshot before MoveNext or a target enumerator can mutate bytes.
            retained.Add(segment with { Bytes = segment.Bytes.ToArray() });
        }
        if (retained.Count == 0) throw new InvalidDataException("Locked-chest code segments are absent.");
        ulong end = 0;
        foreach (var segment in retained.OrderBy(s => s.SourcePc))
        {
            if (segment.SourcePc < end) throw new InvalidDataException("Locked-chest source segments overlap.");
            end = (ulong)segment.SourcePc + (uint)segment.Bytes.Length;
        }
        var bindings = NativeMipsBehaviorFragmentCodec.BoundedSnapshot(externalTargets, 128, "locked-chest target");
        Dictionary<uint, uint> bound = [];
        foreach (var binding in bindings)
        {
            if (binding is null) throw new InvalidDataException("Locked-chest external target is absent.");
            RequireExtent(binding.SourceTarget, 4);
            RequireExtent(binding.FinalTarget, 4);
            if (!bound.TryAdd(binding.SourceTarget, binding.FinalTarget))
                throw new InvalidDataException("Locked-chest external source target is duplicate.");
        }
        HashSet<uint> consumed = [];
        byte[] output = new byte[total];
        List<NativeLockedChestTransfer> transfers = [];
        int cursor = 0;
        foreach (var segment in retained)
        {
            segment.Bytes.CopyTo(output, cursor);
            for (int at = 0; at < segment.Bytes.Length; at += 4)
            {
                uint sourcePc = segment.SourcePc + (uint)at, finalPc = segment.FinalPc + (uint)at;
                uint before = Word(segment.Bytes, at), after = before, opcode = before >> 26;
                bool branch = opcode is 0x01 or 0x04 or 0x05 or 0x06 or 0x07 or 0x14 or 0x15 or 0x16 or 0x17;
                if (!branch && opcode is not 0x02 and not 0x03) continue;
                uint sourceTarget = branch ? unchecked(sourcePc + 4 + (uint)((short)before * 4)) :
                    ((sourcePc + 4) & 0xF0000000u) | ((before & 0x03FFFFFFu) << 2);
                uint target = sourceTarget;
                NativeLockedChestTargetKind kind;
                var owner = retained.FirstOrDefault(s => sourceTarget >= s.SourcePc &&
                    (ulong)sourceTarget < (ulong)s.SourcePc + (uint)s.Bytes.Length);
                if (owner is not null)
                { target = owner.FinalPc + (sourceTarget - owner.SourcePc); kind = NativeLockedChestTargetKind.InternalSegment; }
                else if (bound.TryGetValue(sourceTarget, out target))
                { kind = NativeLockedChestTargetKind.ExplicitExternal; consumed.Add(sourceTarget); }
                else if (opcode == 0x03)
                { target = sourceTarget; kind = NativeLockedChestTargetKind.RetainedUnprovenCall; }
                else throw new InvalidDataException($"Unmapped locked-chest transfer target 0x{sourceTarget:X8} at donor PC 0x{sourcePc:X8}.");
                if (branch)
                {
                    long delta = (long)target - finalPc - 4;
                    if (delta % 4 != 0 || delta / 4 is < short.MinValue or > short.MaxValue)
                        throw new InvalidDataException($"Relocated branch at 0x{sourcePc:X8} cannot reach 0x{target:X8}.");
                    after = (before & 0xFFFF0000u) | unchecked((ushort)(short)(delta / 4));
                }
                else if (kind != NativeLockedChestTargetKind.RetainedUnprovenCall)
                {
                    if (((finalPc + 4) & 0xF0000000u) != (target & 0xF0000000u))
                        throw new InvalidDataException($"Relocated jump at 0x{sourcePc:X8} crosses a 256 MB region.");
                    after = (before & 0xFC000000u) | ((target >> 2) & 0x03FFFFFFu);
                }
                Put(output, cursor + at, after);
                // A truncated historical segment does not invent a delay-slot
                // source read. Null remains explicit missing ownership.
                uint? delay = at <= segment.Bytes.Length - 8 ? Word(segment.Bytes, at + 4) : null;
                transfers.Add(new(segment.Id, at, sourcePc, finalPc, sourceTarget, target, before, after, delay, kind));
            }
            cursor += segment.Bytes.Length;
        }
        if (consumed.Count != bound.Count) throw new InvalidDataException("Unused locked-chest external target binding.");
        return new(retained.ToArray(), output, transfers);
    }

    internal IReadOnlyList<byte[]> RestoreSources(ReadOnlySpan<byte> afterimage)
    {
        if (!afterimage.SequenceEqual(_bytes))
            throw new InvalidDataException("Locked-chest complete relocated afterimage changed.");
        List<byte[]> restored = [];
        int cursor = 0;
        foreach (var segment in _segments)
        {
            byte[] bytes = afterimage.Slice(cursor, segment.Bytes.Length).ToArray();
            foreach (var transfer in Transfers.Where(t => t.SegmentId == segment.Id))
                Put(bytes, transfer.SegmentOffset, transfer.BeforeWord);
            if (!bytes.AsSpan().SequenceEqual(segment.Bytes))
                throw new InvalidDataException("Locked-chest complete source inverse differs.");
            restored.Add(bytes);
            cursor += bytes.Length;
        }
        return restored.AsReadOnly();
    }

    private static void RequireExtent(uint pc, int length)
    {
        if (pc % 4 != 0 || pc < 0x80000000 || (ulong)pc + (uint)length > 0x80200000)
            throw new InvalidDataException("Locked-chest code extent must be word-aligned native main RAM.");
    }
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
    private static void Put(byte[] bytes, int at, uint word) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at, 4), word);
}

internal sealed record NativeLockedChestBehaviorOwner(string Id, int PayloadOffset, int ByteLength);
internal sealed record NativeLockedChestBehaviorEntry(string Id, string OwnerId, uint RuntimePc);

/// <summary>
/// Source-bound historical Town layout only. Its overwrite arena starts inside
/// the retained F800 allocation, so an append layout would misrepresent it.
/// Shared neutral materialization checks exact owners and explicit entries;
/// host admission, mutable globals, rendering and external routines stay outside.
/// </summary>
internal sealed class NativeLockedChestBehaviorLayout
{
    private readonly byte[] _payload;
    private readonly NativeLockedChestCodeRelocation _chest, _reward;
    private NativeLockedChestBehaviorLayout(NativeLockedChestBehaviorSource source)
    {
        _chest = NativeLockedChestCodeRelocation.Compile([
            new("setup", 0x80083BD4, 0x80089ED0, source.CopySetup()),
            new("state", 0x80083D68, 0x80089F00, source.CopyState())], [new(0x8008A20C, LoopContinuationPc)]);
        byte[] chest = [.. Words(0x0260A021), .. _chest.CopyBytes()];
        if (chest.Length != 0x740)
            throw new InvalidDataException($"Relocated Locked Chest payload is 0x{chest.Length:X}, expected 0x740.");
        _reward = NativeLockedChestCodeRelocation.Compile([
            new("reward", 0x8007E59C, 0x8008A610, source.CopyReward())], [
            new(0x8008A20C, LoopContinuationPc), new(0x80088CF4, 0x800858F8),
            new(0x8008A204, 0x8008AA6C), new(0x800880FC, 0x8008AA7C)]);
        byte[] reward = [.. Words(0x0260A021), .. _reward.CopyBytes()];
        if (reward.Length != 0x438)
            throw new InvalidDataException($"Relocated reward payload is 0x{reward.Length:X}, expected 0x438.");
        Owners = Array.AsReadOnly(new NativeLockedChestBehaviorOwner[] {
            new("chest", 0, 0x740), new("reward", 0x740, 0x438), new("reward-dispatch", 0xB78, 0x14),
            new("chest-dispatch", 0xB8C, 0x14), new("delete-thunk", 0xBA0, 0x10), new("sound-thunk", 0xBB0, 0x10) });
        Entries = Array.AsReadOnly(new NativeLockedChestBehaviorEntry[] {
            new("chest-adapter", "chest", 0x80089ECC), new("setup", "chest", 0x80089ED0), new("state", "chest", 0x80089F00),
            new("reward-adapter", "reward", 0x8008A60C), new("reward", "reward", 0x8008A610),
            new("reward-dispatch", "reward-dispatch", 0x8008AA44), new("chest-dispatch", "chest-dispatch", 0x8008AA58),
            new("delete-thunk", "delete-thunk", 0x8008AA6C), new("sound-thunk", "sound-thunk", 0x8008AA7C) });
        byte[][] bytes = [chest, reward,
            Words(0x2402000D, Branch(0x8008AA48, 0x8008A60C), 0, Jump(LoopContinuationPc), 0),
            Words(0x240200AE, Branch(0x8008AA5C, 0x80089ECC), 0, Jump(LoopContinuationPc), 0),
            Words(0x0C01495A, 0x02602021, Jump(LoopContinuationPc), 0),
            Words(0x0C01569E, 0x24A70054, Jump(LoopContinuationPc), 0)];
        var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "town-square.locked-chest.behavior", NativeAddressRevision.Final, 0xBC0);
        var entries = Entries.Select(e => new NativeOwnedOverlayEntry(e.Id, e.OwnerId, new(domain, e.RuntimePc - 0x80089ECC, 4))).ToArray();
        _payload = NativeOwnedOverlayMaterializer.Materialize(new(domain, 0, 0xBC0), 0x80089ECC,
            Owners.Select((o, i) => new NativeOwnedOverlayBlock(o.Id, new(domain, o.PayloadOffset, o.ByteLength), bytes[i])),
            entries, entries.Select(e => e.Owner));
        Transfers = Array.AsReadOnly(_chest.Transfers.Concat(_reward.Transfers).ToArray());
    }

    internal IReadOnlyList<NativeLockedChestBehaviorOwner> Owners { get; }
    internal IReadOnlyList<NativeLockedChestBehaviorEntry> Entries { get; }
    internal IReadOnlyList<NativeLockedChestTransfer> Transfers { get; }
    internal int PrivateAdapterFrameByteLength => 0;
    internal uint LoopContinuationPc => 0x80087258;
    internal bool CompleteExternalOwnersProven => false;
    internal bool RuntimeVerified => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyPayload() => _payload.ToArray();
    internal static NativeLockedChestBehaviorLayout CompileTown(NativeLockedChestBehaviorSource source)
    { ArgumentNullException.ThrowIfNull(source); return new(source); }
    internal IReadOnlyList<byte[]> RestoreSources(ReadOnlySpan<byte> afterimage)
    {
        if (!afterimage.SequenceEqual(_payload)) throw new InvalidDataException("Locked-chest complete owned payload afterimage changed.");
        return Array.AsReadOnly(_chest.RestoreSources(afterimage.Slice(4, 0x73C))
            .Concat(_reward.RestoreSources(afterimage.Slice(0x744, 0x434))).ToArray());
    }
    private static uint Branch(uint pc, uint target)
    {
        long delta = (long)target - pc - 4;
        if (delta % 4 != 0 || delta / 4 is < short.MinValue or > short.MaxValue)
            throw new InvalidDataException("Locked-chest dispatch branch target is out of range.");
        return 0x10820000u | unchecked((ushort)(short)(delta / 4));
    }
    private static uint Jump(uint target) => 0x08000000u | ((target >> 2) & 0x03FFFFFFu);
    private static byte[] Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), words[i]);
        return bytes;
    }
}
