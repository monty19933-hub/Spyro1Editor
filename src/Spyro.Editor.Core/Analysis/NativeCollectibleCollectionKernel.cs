using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Abi = Spyro.Editor.Core.Analysis.NativeCollectibleSelectedSlotResidence;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeCollectibleCollectionResult : uint
{
    Collected = 0, AlreadyEarned = 1, Retired = 2, OutOfRange = 3, InvalidLedger = 4, GenerationOverflow = 5
}

/// <summary>
/// Detached MIPS-I leaf: a0 must equal the compiled caller-owned ledger base;
/// a1 is an unsigned stable lifetime bit ID; v0 is NativeCollectibleCollectionResult.
/// Clobbers only v0/t0-t7/t8-t9. Preserves arguments, at/v1, s/fp/gp/sp/ra and HI/LO.
/// No stack, calls, allocation, installation, dispatch or persistent-save authority.
/// Caller must exclude concurrent mutation/IRQ writers for the whole invocation;
/// the four successful stores are not an atomic transaction or a native save.
/// </summary>
internal sealed class NativeCollectibleCollectionKernel
{
    private readonly byte[] bytes;
    private NativeCollectibleCollectionKernel(byte[] bytes, NativeByteExtent owner, uint codeAddress,
        uint ledgerAddress, string manifestSha256, int slot, int liveFootprintBytes)
    {
        this.bytes = bytes.ToArray(); Owner = owner; EntryPc = codeAddress; CallerLedgerAddress = ledgerAddress;
        ManifestSha256 = manifestSha256; OrdinarySlot = slot; LiveFootprintBytes = liveFootprintBytes;
    }

    internal NativeByteExtent Owner { get; }
    internal uint EntryPc { get; }
    internal uint CallerLedgerAddress { get; }
    internal string ManifestSha256 { get; }
    internal int OrdinarySlot { get; }
    internal int LedgerByteLength => Abi.ReservationBytes;
    internal int LiveFootprintBytes { get; }
    internal int StackByteLength => 0;
    internal bool NativeResidenceIssued => false;
    internal bool DispatchActivated => false;
    internal bool PublicationApproved => false;
    internal bool PersistentSaveChanges => false;
    internal bool ConcurrentMutationSafe => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeCollectibleCollectionKernel Compile(DetachedCollectibleManifest manifest, int ordinarySlot,
        NativeByteExtent codeCapacity, uint codeAddress, uint callerLedgerAddress)
        => CompileCore(manifest, ordinarySlot, codeCapacity, codeAddress, callerLedgerAddress, readOnlyQuery: false);

    internal static (byte[] Bytes, NativeByteExtent Owner, uint EntryPc, uint LedgerAddress,
        string ManifestSha256, int OrdinarySlot, int LiveFootprintBytes) CompileStateQuery(DetachedCollectibleManifest manifest, int ordinarySlot,
        NativeByteExtent codeCapacity, uint codeAddress, uint callerLedgerAddress)
    {
        var body = CompileCore(manifest, ordinarySlot, codeCapacity, codeAddress, callerLedgerAddress, readOnlyQuery: true);
        return (body.CopyBytes(), body.Owner, body.EntryPc, body.CallerLedgerAddress,
            body.ManifestSha256, body.OrdinarySlot, body.LiveFootprintBytes);
    }

    private static NativeCollectibleCollectionKernel CompileCore(DetachedCollectibleManifest manifest, int ordinarySlot,
        NativeByteExtent codeCapacity, uint codeAddress, uint callerLedgerAddress, bool readOnlyQuery)
    {
        ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(codeCapacity);
        if (ordinarySlot < 0 || ordinarySlot >= DetachedCollectibleSave.OrdinarySlotCount)
            throw new InvalidDataException("Collection kernel requires ordinary slot 0, 1 or 2.");
        var sizing = Abi.MeasureLifetime(manifest.LifetimeCount);
        if (!sizing.FitsProposedReservation)
            throw new InvalidDataException("Lifetime IDs exceed the selected-slot residence capacity; no truncation is permitted.");
        if (codeCapacity.Space.Kind != NativeAddressKind.LevelData || codeCapacity.Space.Revision != NativeAddressRevision.Final)
            throw new InvalidDataException("Collection kernel requires a typed final code capacity.");
        if (codeCapacity.Offset % 4 != 0 || codeAddress % 4 != 0 || callerLedgerAddress % 16 != 0)
            throw new InvalidDataException("Collection code must be word aligned and its ledger sixteen-byte aligned.");
        if (callerLedgerAddress < 0x80000000 || (ulong)callerLedgerAddress + Abi.ReservationBytes > 0x80200000)
            throw new InvalidDataException("The complete caller-owned ledger reservation must fit cached main RAM.");

        // This descriptor supplies the single active-mask/geometry definition. An
        // empty manifest needs no residence: compile only the range-refusal path.
        byte[] activeMask = manifest.LifetimeCount == 0 ? [] : Abi.Describe(manifest, ordinarySlot).CopyActiveMask();
        int activeMaskOffset = Abi.BitmapOffset + sizing.AlignedBitmapBytes;
        var e = new Emitter();
        const int zero = 0, v0 = 2, a0 = 4, a1 = 5, t0 = 8, t1 = 9, t2 = 10, t3 = 11,
            t4 = 12, t5 = 13, t6 = 14, t7 = 15, ra = 31;

        e.Constant(t0, callerLedgerAddress);
        e.Branch(5, a0, t0, "invalid"); // No memory access in either guard's delay slot.
        e.Constant(t0, (uint)manifest.LifetimeCount);
        e.R(0x2B, a1, t0, t1); // sltu, including high-bit and uint.MaxValue IDs.
        e.Branch(4, t1, zero, "range");
        if (manifest.LifetimeCount != 0)
        {
            Expect(Abi.MagicOffset, Abi.AbiMagic);
            Expect(Abi.VersionOffset, (uint)Abi.AbiVersion | ((uint)Abi.ReadyPhase << 16) | ((uint)ordinarySlot << 24));
            // Controller 0/1 and dirty 0/1 are legal; the native file identity is
            // opaque and preserved. Phase/status/operation/failure must be idle.
            foreach (int offset in new[] { Abi.ControllerOffset, Abi.DirtyOffset })
            {
                e.Load(0x24, t0, a0, offset); e.I(0x0B, t0, t1, 2);
                e.Branch(4, t1, zero, "invalid");
            }
            Expect(Abi.StatusOffset, 0);
            byte[] canonicalManifest = manifest.SerializeCanonical();
            for (int i = 0; i < 16; i += 4)
                Expect(Abi.ContentIdOffset + i, BinaryPrimitives.ReadUInt32LittleEndian(canonicalManifest.AsSpan(0x10 + i)));
            byte[] digest = Convert.FromHexString(manifest.Sha256);
            for (int i = 0; i < digest.Length; i += 4)
                Expect(Abi.ManifestDigestOffset + i, BinaryPrimitives.ReadUInt32LittleEndian(digest.AsSpan(i)));
            Expect(Abi.RevisionOffset, manifest.Revision);
            Expect(Abi.LifetimeCountOffset, (uint)manifest.LifetimeCount | ((uint)sizing.RawBitmapBytes << 16));
            Expect(Abi.ActiveCountOffset, (uint)manifest.ActiveCount, 0x25);
            Expect(Abi.OperationOffset, 0); Expect(Abi.FailureOffset, 0);

            // Exact compiled mask, including all padding, rather than trusting a
            // writable runtime mask or a cached active count as manifest authority.
            for (int i = 0; i < activeMask.Length; i += 4)
                Expect(activeMaskOffset + i, BinaryPrimitives.ReadUInt32LittleEndian(activeMask.AsSpan(i)));
            // Retired bits are legitimate historical earned state. Only genuinely
            // unassigned lifetime bits and byte-alignment padding are refused.
            for (int i = 0; i < sizing.AlignedBitmapBytes; i += 4)
            {
                int assignedBits = Math.Clamp(manifest.LifetimeCount - i * 8, 0, 32);
                if (assignedBits == 32) continue;
                if (assignedBits == 0) Expect(Abi.BitmapOffset + i, 0);
                else
                {
                    e.Load(0x23, t0, a0, Abi.BitmapOffset + i);
                    e.Constant(t1, uint.MaxValue << assignedBits); e.R(0x24, t0, t1, t0);
                    e.Branch(5, t0, zero, "invalid");
                }
            }

            // Independently recount earned AND active. This bounded byte loop
            // uses no table, helper, stack or HI/LO arithmetic.
            e.I(9, a0, t2, Abi.BitmapOffset); e.Constant(t3, 0);
            e.I(9, t2, t7, sizing.AlignedBitmapBytes);
            e.Label("count-byte");
            e.Load(0x24, t4, t2, 0);
            e.Load(0x24, t5, t2, sizing.AlignedBitmapBytes);
            e.R(0x24, t4, t5, t4);
            e.Branch(4, t4, zero, "next-byte");
            e.Label("count-bit");
            e.I(9, t4, t6, -1); e.R(0x24, t4, t6, t4); e.I(9, t3, t3, 1);
            e.Branch(5, t4, zero, "count-bit");
            e.Label("next-byte");
            e.I(9, t2, t2, 1); e.Branch(5, t2, t7, "count-byte");
            e.Load(0x25, t0, a0, Abi.EarnedActiveCountOffset);
            e.Branch(5, t0, t3, "invalid");

            e.R(2, zero, a1, t1, 3); e.R(0x21, a0, t1, t0);
            e.Load(0x24, t4, t0, Abi.BitmapOffset);
            e.Load(0x24, t5, t0, activeMaskOffset);
            e.I(0x0C, a1, t1, 7); e.Constant(t2, 1); e.R(4, t1, t2, t2);
            e.R(0x24, t5, t2, t6); e.Branch(4, t6, zero, "retired");
            e.R(0x24, t4, t2, t6); e.Branch(5, t6, zero, "duplicate");
            if (readOnlyQuery)
            {
                // A query must not turn checking an unearned ID into collection,
                // nor confuse mutation overflow with an earned/retired identity.
                // It has no player/row input: reload suppression precedes proximity.
                ReturnValue((uint)NativeCollectibleQueryResult.Available);
            }
            else
            {
                e.Load(0x23, t7, a0, Abi.MutationGenerationOffset);
                e.Constant(t1, uint.MaxValue); e.Branch(4, t7, t1, "overflow");

                // All possible refusals precede all stores. Caller exclusivity is
                // required: this leaf does not claim atomicity against interruption.
                e.R(0x25, t4, t2, t4); e.I(9, t3, t3, 1); e.I(9, t7, t7, 1);
                e.I(0x28, t0, t4, Abi.BitmapOffset);
                e.I(0x29, a0, t3, Abi.EarnedActiveCountOffset);
                e.I(0x2B, a0, t7, Abi.MutationGenerationOffset);
                e.Constant(t6, 1); e.I(0x28, a0, t6, Abi.DirtyOffset);
                Return(NativeCollectibleCollectionResult.Collected);
            }
        }
        foreach (var (label, result) in new[] {
            ("invalid", NativeCollectibleCollectionResult.InvalidLedger), ("range", NativeCollectibleCollectionResult.OutOfRange),
            ("retired", NativeCollectibleCollectionResult.Retired), ("duplicate", NativeCollectibleCollectionResult.AlreadyEarned) })
        { e.Label(label); Return(result); }
        if (!readOnlyQuery) { e.Label("overflow"); Return(NativeCollectibleCollectionResult.GenerationOverflow); }

        byte[] code = e.Finish();
        if (code.Length > codeCapacity.ByteLength)
            throw new InvalidDataException("Emitted collection code exceeds its supplied code capacity.");
        if (codeAddress < 0x80000000 || (ulong)codeAddress + (uint)code.Length > 0x80200000)
            throw new InvalidDataException("The complete detached collection code must fit cached main RAM.");
        if ((ulong)codeAddress < (ulong)callerLedgerAddress + Abi.ReservationBytes &&
            (ulong)callerLedgerAddress < (ulong)codeAddress + (uint)code.Length)
            throw new InvalidDataException("Detached code and caller ledger reservation overlap.");
        return new(code, new(codeCapacity.Space, codeCapacity.Offset, code.Length), codeAddress,
            callerLedgerAddress, manifest.Sha256, ordinarySlot, sizing.LiveFootprintBytes);

        void Expect(int offset, uint expected, int loadOp = 0x23)
        {
            e.Load(loadOp, t0, a0, offset); e.Constant(t1, expected);
            e.Branch(5, t0, t1, "invalid");
        }
        void Return(NativeCollectibleCollectionResult result)
            => ReturnValue((uint)result);
        void ReturnValue(uint result)
        { e.Constant(v0, result); e.R(8, ra, zero, zero); e.Word(0); }
    }

    // Deliberately local, restricted assembler. Every branch/load gets an
    // explicit NOP for the R3000 branch/load delays; no pseudoinstruction can
    // secretly consume at, change HI/LO, call a helper or allocate a stack frame.
    private sealed class Emitter
    {
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Index, string Label)> branches = [];
        internal void Word(uint word) => words.Add(word);
        internal void R(int fn, int rs, int rt, int rd, int shift = 0) =>
            Word((uint)(rs << 21 | rt << 16 | rd << 11 | shift << 6 | fn));
        internal void I(int op, int rs, int rt, int value) =>
            Word((uint)(op << 26 | rs << 21 | rt << 16) | (ushort)value);
        internal void Constant(int register, uint value)
        {
            if (value <= ushort.MaxValue) I(0x0D, 0, register, (int)value);
            else { I(0x0F, 0, register, (int)(value >> 16)); if ((value & 0xFFFF) != 0) I(0x0D, register, register, (int)(value & 0xFFFF)); }
        }
        internal void Load(int op, int register, int pointer, int offset)
        { I(op, pointer, register, offset); Word(0); }
        internal void Label(string label) => labels.Add(label, words.Count);
        internal void Branch(int op, int rs, int rt, string label)
        { branches.Add((words.Count, label)); I(op, rs, rt, 0); Word(0); }
        internal byte[] Finish()
        {
            foreach (var branch in branches)
            {
                int relative = labels[branch.Label] - branch.Index - 1;
                if (relative < short.MinValue || relative > short.MaxValue) throw new InvalidDataException("Collection code branch exceeds MIPS-I range.");
                words[branch.Index] |= (ushort)(short)relative;
            }
            byte[] result = new byte[checked(words.Count * 4)];
            for (int i = 0; i < words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), words[i]);
            return result;
        }
    }
}
