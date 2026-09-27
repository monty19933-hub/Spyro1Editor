using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeJapaneseEggLifecycleResult : uint
{
    Collected = 0, AlreadyEarned = 1, Retired = 2, OutOfRange = 3, InvalidLedger = 4,
    GenerationOverflow = 5, UnboundRow = 6, InvalidRow = 7, NotLive = 8, NotEligible = 9,
    Available = 10, Suppressed = 11
}

/// <summary>
/// Detached pair of MIPS-I callables, not an installed dispatcher. Both accept
/// a0=exact bound row and a1=exact ledger. BeforeVisual queries earned state
/// without proximity; AfterVisual attempts pickup. Earned/collected rows use
/// the source-pinned placed-delete helper, never the generic kill/effect path.
/// Both preserve a0/a1, s0-s7, k0/k1, gp/fp/sp/ra and HI/LO. Other caller
/// registers may change. The caller exclusively owns scene, ledger, globals
/// and 0x60 bytes below SP across the complete operation; this does not make
/// the leaf's bitmap/count/generation stores atomic or issue a Ready state.
/// Only live active-manifest rows are bound; retired manifest identities need
/// a separate binding policy and are not silently admitted by this wrapper.
/// </summary>
internal sealed class NativeJapaneseEggLifecycleCallables
{
    private const uint PlacedDeletePc = 0x80052568;
    private const int PlacedDeleteBytes = 0x64;
    private readonly byte[] bytes;
    private NativeJapaneseEggLifecycleCallables(byte[] bytes, NativeByteExtent owner, uint before, uint after,
        NativeJapaneseEggCollectionAttempt attempt, uint dynamicBase)
    {
        this.bytes = bytes.ToArray(); Owner = owner; BeforeVisualEntryPc = before; AfterVisualEntryPc = after;
        Attempt = attempt; DynamicBase = dynamicBase;
    }
    internal NativeByteExtent Owner { get; }
    internal uint BeforeVisualEntryPc { get; }
    internal uint AfterVisualEntryPc { get; }
    internal uint DynamicBase { get; }
    internal NativeJapaneseEggCollectionAttempt Attempt { get; }
    internal int StackByteLength => 0x20;
    internal int MaximumStackByteLength => 0x60;
    internal bool SourceBound => Attempt.SourceBound;
    internal bool DispatchActivated => false;
    internal bool NativeResidenceIssued => false;
    internal bool PersistentSaveChanges => false;
    internal bool ConcurrentMutationSafe => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeJapaneseEggLifecycleCallables Compile(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, NativeJapaneseEggCollectionAttempt attempt,
        NativeJapaneseEggPickupPredicateKernel predicate, NativeCollectibleCollectionKernel collector,
        NativeCollectibleStateQueryKernel query, NativeByteExtent capacity, uint codeAddress) =>
        CompileCore(authored, manifest, attempt, predicate, collector, query, capacity, codeAddress, true);

    internal static NativeJapaneseEggLifecycleCallables CompileFixture(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, NativeJapaneseEggCollectionAttempt attempt,
        NativeJapaneseEggPickupPredicateKernel predicate, NativeCollectibleCollectionKernel collector,
        NativeCollectibleStateQueryKernel query, NativeByteExtent capacity, uint codeAddress) =>
        CompileCore(authored, manifest, attempt, predicate, collector, query, capacity, codeAddress, false);

    private static NativeJapaneseEggLifecycleCallables CompileCore(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, NativeJapaneseEggCollectionAttempt attempt,
        NativeJapaneseEggPickupPredicateKernel predicate, NativeCollectibleCollectionKernel collector,
        NativeCollectibleStateQueryKernel query, NativeByteExtent capacity, uint codeAddress, bool source)
    {
        ArgumentNullException.ThrowIfNull(attempt); ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(collector); ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(capacity);
        var binding = attempt.Binding; binding.RequireCurrent(authored, manifest);
        if (source != authored.SourceCertified || source != attempt.SourceBound || source != predicate.SourceBound)
            throw new InvalidDataException("Lifecycle callables cannot upgrade or downgrade source ancestry.");
        if (attempt.ManifestSha256 != collector.ManifestSha256 || attempt.ManifestSha256 != query.ManifestSha256 ||
            attempt.PredicateEntryPc != predicate.EntryPc || attempt.CollectionEntryPc != collector.EntryPc ||
            attempt.CallerLedgerAddress != collector.CallerLedgerAddress || attempt.CallerLedgerAddress != query.CallerLedgerAddress ||
            attempt.OrdinarySlot != collector.OrdinarySlot || attempt.OrdinarySlot != query.OrdinarySlot)
            throw new InvalidDataException("Lifecycle dependencies require the same exact row/manifest/slot/ledger and callable entries.");
        if (attempt.StackByteLength != 0x20 || attempt.MaximumStackByteLength != 0x40 || predicate.StackByteLength != 0x20 ||
            collector.StackByteLength != 0 || query.StackByteLength != 0 ||
            attempt.Owner.ByteLength != attempt.CopyBytes().Length || predicate.Owner.ByteLength != predicate.CopyBytes().Length ||
            collector.Owner.ByteLength != collector.CopyBytes().Length || query.Owner.ByteLength != query.CopyBytes().Length ||
            collector.LedgerByteLength != query.LedgerByteLength)
            throw new InvalidDataException("Lifecycle code or stack ABI changed.");
        if (capacity.Space.Kind != NativeAddressKind.LevelData || capacity.Space.Revision != NativeAddressRevision.Final ||
            capacity.Offset % 4 != 0 || codeAddress % 4 != 0)
            throw new InvalidDataException("Lifecycle code requires a word-aligned typed final capacity.");
        if (source)
        {
            byte[] executable = authored.Integration.CopyOriginalExecutable();
            const int offset = (int)(PlacedDeletePc - 0x8000F800);
            if (executable.Length < offset + PlacedDeleteBytes ||
                Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan(offset, PlacedDeleteBytes))) !=
                    "602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505")
                throw new InvalidDataException("Complete native placed-delete source owner changed.");
        }
        // The loader creates the runtime sentinel; source-form unused rows are
        // zero, not FF. Derive its boundary from the final placed count and the
        // existing scene allocator, and require our last row to end there.
        byte[] scene = authored.CopyScene();
        int rowTable = authored.Integration.OriginalSceneSource.RowTableOffset;
        int count = authored.Integration.SceneAppend.PlacedCountAfter;
        long dynamicOffset = rowTable + (long)count * 0x58;
        uint dynamicBase = checked(binding.SceneAddress + (uint)dynamicOffset);
        if (rowTable < 4 || dynamicOffset > scene.Length - 0x58 ||
            BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(rowTable - 4)) != count ||
            authored.Integration.SceneAppend.DynamicCapacityAfter < 1 ||
            checked(binding.Rows.Max(r => r.RowAddress) + 0x58) != dynamicBase)
            throw new InvalidDataException("Authored placed rows require their exact final placed/dynamic boundary.");

        const int zero = 0, v0 = 2, a0 = 4, a1 = 5, t0 = 8, t1 = 9, sp = 29, ra = 31;
        var e = new Emitter(codeAddress);
        uint before = e.Pc; EmitEntry(true, "pre-");
        uint after = e.Pc; EmitEntry(false, "post-");
        byte[] emitted = e.Finish();
        if (emitted.Length > capacity.ByteLength) throw new InvalidDataException("Lifecycle code exceeds its final capacity.");
        // Whole-set disjointness includes existing leaf pairs and native helper
        // globals. A typed code extent alone never allocates runtime memory.
        (string Name, uint Start, long Bytes)[] owners = [
            ("lifecycle", codeAddress, emitted.Length), ("attempt", attempt.EntryPc, attempt.Owner.ByteLength),
            ("predicate", predicate.RuntimeBase, predicate.Owner.ByteLength), ("collector", collector.EntryPc, collector.Owner.ByteLength),
            ("query", query.EntryPc, query.Owner.ByteLength), ("ledger", collector.CallerLedgerAddress, collector.LedgerByteLength),
            ("models", binding.ModelAddress, binding.ModelByteLength), ("scene", binding.SceneAddress, binding.SceneByteLength),
            ("placed delete", PlacedDeletePc, PlacedDeleteBytes), ("grid base", 0x80075778, 4), ("dynamic base", 0x80075890, 4),
            ("player position", 0x80078A58, 12), ("player state", 0x80078AD0, 8), ("player flags", 0x80078C4C, 4) ];
        foreach (var owner in owners)
            if (owner.Bytes <= 0 || owner.Start < 0x80000000 || (ulong)owner.Start + (ulong)owner.Bytes > 0x80200000)
                throw new InvalidDataException("Complete lifecycle " + owner.Name + " owner must fit cached main RAM.");
        for (int i = 0; i < owners.Length; i++)
        for (int j = 0; j < i; j++)
            if ((ulong)owners[i].Start < (ulong)owners[j].Start + (ulong)owners[j].Bytes &&
                (ulong)owners[j].Start < (ulong)owners[i].Start + (ulong)owners[i].Bytes)
                throw new InvalidDataException("Lifecycle owners overlap: " + owners[i].Name + " and " + owners[j].Name + ".");
        return new(emitted, new(capacity.Space, capacity.Offset, emitted.Length), before, after, attempt, dynamicBase);

        void EmitEntry(bool pre, string p)
        {
            e.Constant(t0, collector.CallerLedgerAddress); e.Branch(5, a1, t0, p + "ledger");
            for (int i = 0; i < binding.Rows.Count; i++)
            { e.Constant(t0, binding.Rows[i].RowAddress); e.Branch(4, a0, t0, p + "row-" + i); }
            Return(NativeJapaneseEggLifecycleResult.UnboundRow);
            for (int i = 0; i < binding.Rows.Count; i++)
            {
                var row = binding.Rows[i]; e.Label(p + "row-" + i);
                e.Load(0x23, t0, a0, 0); e.Constant(t1, row.PrivateAddress); e.Branch(5, t0, t1, p + "invalid");
                e.Load(0x25, t0, a0, 0x36); e.Constant(t1, row.AuthoredClass); e.Branch(5, t0, t1, p + "invalid");
                e.Load(0x24, t0, a0, 0x48); e.Branch(5, t0, zero, p + "not-live");
                // Native delete can otherwise follow a grid chain or dynamic
                // free-list. Refuse both before query OR collection mutation.
                e.Load(0x25, t0, a0, 0x34); e.Constant(t1, 0xFFFF); e.Branch(5, t0, t1, p + "invalid");
                e.Constant(t0, 0x80075890); e.Load(0x23, t0, t0, 0);
                e.Constant(t1, dynamicBase); e.Branch(5, t0, t1, p + "invalid");
                e.Constant(t0, row.BitIndex); e.Branch(4, zero, zero, p + "call");
            }
            e.Label(p + "call"); e.I(9, sp, sp, -0x20);
            e.I(0x2B, sp, ra, 0x10); e.I(0x2B, sp, a0, 0x14); e.I(0x2B, sp, a1, 0x18);
            if (pre)
            {
                e.I(9, a1, a0, 0); e.I(9, t0, a1, 0); e.Call(query.EntryPc);
                e.Branch(4, v0, zero, p + "available");
                e.Constant(t0, 1); e.Branch(5, v0, t0, p + "restore");
                e.Constant(v0, (uint)NativeJapaneseEggLifecycleResult.Suppressed);
            }
            else
            {
                e.Call(attempt.EntryPc);
                e.I(0x0B, v0, t0, 2); e.Branch(4, t0, zero, p + "restore");
            }
            e.I(0x2B, sp, v0, 0x1C); e.Load(0x23, a0, sp, 0x14);
            e.Call(PlacedDeletePc); e.Load(0x23, v0, sp, 0x1C);
            e.Branch(4, zero, zero, p + "restore");
            if (pre) { e.Label(p + "available"); e.Constant(v0, (uint)NativeJapaneseEggLifecycleResult.Available); }
            e.Label(p + "restore");
            e.Load(0x23, a0, sp, 0x14); e.Load(0x23, a1, sp, 0x18); e.Load(0x23, ra, sp, 0x10);
            e.Jr(ra); e.I(9, sp, sp, 0x20);
            e.Label(p + "ledger"); Return(NativeJapaneseEggLifecycleResult.InvalidLedger);
            e.Label(p + "invalid"); Return(NativeJapaneseEggLifecycleResult.InvalidRow);
            e.Label(p + "not-live"); Return(NativeJapaneseEggLifecycleResult.NotLive);
        }
        void Return(NativeJapaneseEggLifecycleResult result) { e.Constant(v0, (uint)result); e.Jr(ra); e.Word(0); }
    }

    // Explicit MIPS-I load/branch delay slots; no assembler pseudo-registers.
    private sealed class Emitter(uint codeAddress)
    {
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Index, string Label)> branches = [];
        internal uint Pc => checked(codeAddress + (uint)words.Count * 4);
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
        internal void Branch(int op, int rs, int rt, string label) { branches.Add((words.Count, label)); I(op, rs, rt, 0); Word(0); }
        internal void Call(uint target)
        {
            if (target % 4 != 0 || ((ulong)Pc + 4) > uint.MaxValue || ((Pc + 4) & 0xF0000000) != (target & 0xF0000000))
                throw new InvalidDataException("Lifecycle target leaves the aligned MIPS call region.");
            Word(0x0C000000 | ((target >> 2) & 0x3FFFFFF)); Word(0);
        }
        internal byte[] Finish()
        {
            foreach (var b in branches)
            {
                int delta = labels[b.Label] - b.Index - 1;
                if (delta < short.MinValue || delta > short.MaxValue) throw new InvalidDataException("Lifecycle branch leaves MIPS-I range.");
                words[b.Index] |= (ushort)(short)delta;
            }
            byte[] result = new byte[checked(words.Count * 4)];
            for (int i = 0; i < words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), words[i]);
            return result;
        }
    }
}
