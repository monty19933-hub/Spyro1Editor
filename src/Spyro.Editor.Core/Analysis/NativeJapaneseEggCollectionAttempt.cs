using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeJapaneseEggCollectionAttemptResult : uint
{
    Collected = NativeCollectibleCollectionResult.Collected,
    AlreadyEarned = NativeCollectibleCollectionResult.AlreadyEarned,
    Retired = NativeCollectibleCollectionResult.Retired,
    OutOfRange = NativeCollectibleCollectionResult.OutOfRange,
    InvalidLedger = NativeCollectibleCollectionResult.InvalidLedger,
    GenerationOverflow = NativeCollectibleCollectionResult.GenerationOverflow,
    UnboundRow = 6, InvalidRow = 7, NotLive = 8, NotEligible = 9
}

/// <summary>
/// Detached composed callable: a0=exact final authored row; a1=exact ledger base.
/// v0 returns NativeJapaneseEggCollectionAttemptResult. Preserves a0/a1, s0-s7,
/// k0/k1, fp/gp/sp/ra and HI/LO. Other caller temporaries/arguments may change.
/// Caller owns 0x20 stack bytes plus the predicate's nested 0x20 bytes, and
/// exclusively holds the bound scene and ledger throughout the entire attempt.
/// Does not mutate/hide/retire a row, execute visuals, or install native dispatch.
/// </summary>
internal sealed class NativeJapaneseEggCollectionAttempt
{
    private readonly byte[] bytes;
    private NativeJapaneseEggCollectionAttempt(byte[] bytes, NativeByteExtent owner, uint entry,
        NativeJapaneseEggCollectibleBinding binding, NativeJapaneseEggPickupPredicateKernel predicate,
        NativeCollectibleCollectionKernel collector)
    {
        this.bytes = bytes.ToArray(); Owner = owner; EntryPc = entry; Binding = binding;
        SourceBound = binding.SourceCertified && predicate.SourceBound;
        PredicateEntryPc = predicate.EntryPc; CollectionEntryPc = collector.EntryPc;
        CallerLedgerAddress = collector.CallerLedgerAddress; OrdinarySlot = collector.OrdinarySlot;
    }
    internal NativeByteExtent Owner { get; }
    internal uint EntryPc { get; }
    internal NativeJapaneseEggCollectibleBinding Binding { get; }
    internal string ManifestSha256 => Binding.ManifestSha256;
    internal uint PredicateEntryPc { get; }
    internal uint CollectionEntryPc { get; }
    internal uint CallerLedgerAddress { get; }
    internal int OrdinarySlot { get; }
    internal int StackByteLength => 0x20;
    internal int MaximumStackByteLength => 0x40;
    internal bool SourceBound { get; }
    internal bool DispatchActivated => false;
    internal bool RowRetirementApproved => false;
    internal bool NativeResidenceIssued => false;
    internal bool PersistentSaveChanges => false;
    internal bool ConcurrentMutationSafe => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeJapaneseEggCollectionAttempt Compile(NativeJapaneseEggCollectibleBinding binding,
        NativeJapaneseEggAuthoredClassesPlan authored, DetachedCollectibleManifest manifest,
        NativeJapaneseEggPickupPredicateKernel predicate, NativeCollectibleCollectionKernel collector,
        NativeByteExtent capacity, uint codeAddress)
    {
        ArgumentNullException.ThrowIfNull(binding); ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(collector); ArgumentNullException.ThrowIfNull(capacity);
        binding.RequireCurrent(authored, manifest);
        if (binding.ManifestSha256 != collector.ManifestSha256)
            throw new InvalidDataException("Collection kernel and authored-row binding require the same exact manifest.");
        if (binding.SourceCertified != predicate.SourceBound)
            throw new InvalidDataException("Caller cannot combine fixture and exact-source predicate/row ancestry.");
        if (capacity.Space.Kind != NativeAddressKind.LevelData || capacity.Space.Revision != NativeAddressRevision.Final)
            throw new InvalidDataException("Collection attempt requires a typed final code capacity.");
        if (capacity.Offset % 4 != 0 || codeAddress % 4 != 0)
            throw new InvalidDataException("Detached attempt code must be word aligned.");
        if (predicate.StackByteLength != 0x20 || collector.StackByteLength != 0 || predicate.Owner.ByteLength != predicate.CopyBytes().Length ||
            collector.Owner.ByteLength != collector.CopyBytes().Length)
            throw new InvalidDataException("Composed callable code or stack ABI changed.");

        const int zero = 0, v0 = 2, a0 = 4, a1 = 5, t0 = 8, t1 = 9, sp = 29, ra = 31;
        var e = new Emitter(codeAddress);
        e.Constant(t0, collector.CallerLedgerAddress); e.Branch(5, a1, t0, "ledger");
        for (int i = 0; i < binding.Rows.Count; i++)
        {
            e.Constant(t0, binding.Rows[i].RowAddress); e.Branch(4, a0, t0, "row-" + i);
        }
        Return(NativeJapaneseEggCollectionAttemptResult.UnboundRow);
        for (int i = 0; i < binding.Rows.Count; i++)
        {
            var row = binding.Rows[i]; e.Label("row-" + i);
            // Only exact row pointers reach these bounded structural reads.
            e.Load(0x23, t0, a0, 0); e.Constant(t1, row.PrivateAddress); e.Branch(5, t0, t1, "row-invalid");
            e.Load(0x25, t0, a0, 0x36); e.Constant(t1, row.AuthoredClass); e.Branch(5, t0, t1, "row-invalid");
            e.Load(0x24, t0, a0, 0x48); e.Branch(5, t0, zero, "not-live");
            e.Constant(t0, row.BitIndex); e.Branch(4, zero, zero, "attempt");
        }
        e.Label("attempt");
        e.I(9, sp, sp, -0x20);
        e.I(0x2B, sp, ra, 0x10); e.I(0x2B, sp, a0, 0x14); e.I(0x2B, sp, a1, 0x18); e.I(0x2B, sp, t0, 0x1C);
        e.Call(predicate.EntryPc);
        e.Branch(4, v0, zero, "ineligible");
        e.Load(0x23, a0, sp, 0x18); e.Load(0x23, a1, sp, 0x1C);
        e.Call(collector.EntryPc);
        e.Branch(4, zero, zero, "restore");
        e.Label("ineligible"); e.Constant(v0, (uint)NativeJapaneseEggCollectionAttemptResult.NotEligible);
        e.Label("restore");
        e.Load(0x23, a0, sp, 0x14); e.Load(0x23, a1, sp, 0x18); e.Load(0x23, ra, sp, 0x10);
        e.Jr(ra); e.I(9, sp, sp, 0x20);
        foreach (var (label, result) in new[] {
            ("ledger", NativeJapaneseEggCollectionAttemptResult.InvalidLedger),
            ("row-invalid", NativeJapaneseEggCollectionAttemptResult.InvalidRow), ("not-live", NativeJapaneseEggCollectionAttemptResult.NotLive) })
        { e.Label(label); Return(result); }

        byte[] emitted = e.Finish();
        if (emitted.Length > capacity.ByteLength) throw new InvalidDataException("Emitted attempt exceeds its final code capacity.");
        // Validate the whole composed set, not only the wrapper against each
        // dependency. A caller cannot place the predicate inside its collector,
        // or put a ledger/code package into the final model, scene or player state.
        (string Name, uint Start, long Bytes)[] owners = [
            ("caller code", codeAddress, emitted.Length), ("predicate code", predicate.RuntimeBase, predicate.Owner.ByteLength),
            ("collection code", collector.EntryPc, collector.Owner.ByteLength), ("ledger", collector.CallerLedgerAddress, collector.LedgerByteLength),
            ("authored models", binding.ModelAddress, binding.ModelByteLength), ("authored scene", binding.SceneAddress, binding.SceneByteLength),
            ("player position", 0x80078A58, 12), ("player state", 0x80078AD0, 8), ("player flags", 0x80078C4C, 4) ];
        foreach (var owner in owners)
            if (owner.Bytes <= 0 || owner.Start < 0x80000000 || (ulong)owner.Start + (ulong)owner.Bytes > 0x80200000)
                throw new InvalidDataException("Complete composed " + owner.Name + " owner must fit cached main RAM.");
        for (int i = 0; i < owners.Length; i++)
        for (int j = 0; j < i; j++)
            if ((ulong)owners[i].Start < (ulong)owners[j].Start + (ulong)owners[j].Bytes &&
                (ulong)owners[j].Start < (ulong)owners[i].Start + (ulong)owners[i].Bytes)
                throw new InvalidDataException("Composed owners overlap: " + owners[i].Name + " and " + owners[j].Name + ".");
        return new(emitted, new(capacity.Space, capacity.Offset, emitted.Length), codeAddress, binding, predicate, collector);

        void Return(NativeJapaneseEggCollectionAttemptResult result)
        { e.Constant(v0, (uint)result); e.Jr(ra); e.Word(0); }
    }

    // Fixed-target MIPS-I emission only; no borrowed at register, implicit
    // callback, generic kill/effect helper, row store or stack pseudoinstruction.
    private sealed class Emitter(uint codeAddress)
    {
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Index, string Label)> branches = [];
        internal void Word(uint value) => words.Add(value);
        internal void I(int op, int rs, int rt, int value) => Word((uint)(op << 26 | rs << 21 | rt << 16) | (ushort)value);
        internal void Constant(int register, uint value)
        {
            if (value <= ushort.MaxValue) I(0x0D, 0, register, (int)value);
            else { I(0x0F, 0, register, (int)(value >> 16)); if ((value & 65535) != 0) I(0x0D, register, register, (int)(value & 65535)); }
        }
        internal void Load(int op, int register, int pointer, int offset) { I(op, pointer, register, offset); Word(0); }
        internal void Jr(int register) => Word((uint)(register << 21) | 8);
        internal void Label(string name) => labels.Add(name, words.Count);
        internal void Branch(int op, int rs, int rt, string label)
        { branches.Add((words.Count, label)); I(op, rs, rt, 0); Word(0); }
        internal void Call(uint target)
        {
            ulong nextPc = (ulong)codeAddress + (ulong)words.Count * 4 + 4;
            if (target % 4 != 0 || nextPc > uint.MaxValue || ((uint)nextPc & 0xF0000000) != (target & 0xF0000000))
                throw new InvalidDataException("Detached callable target is outside the aligned MIPS call region.");
            Word(0x0C000000 | ((target >> 2) & 0x3FFFFFF)); Word(0);
        }
        internal byte[] Finish()
        {
            foreach (var b in branches)
            {
                int delta = labels[b.Label] - b.Index - 1;
                if (delta < short.MinValue || delta > short.MaxValue) throw new InvalidDataException("Attempt branch exceeds MIPS-I range.");
                words[b.Index] |= (ushort)(short)delta;
            }
            byte[] result = new byte[checked(words.Count * 4)];
            for (int i = 0; i < words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), words[i]);
            return result;
        }
    }
}
