using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Detached MIPS-I SHA-256 leaf over caller-owned immutable bytes. a0 must be
/// the compiled input base; a1 is an unsigned byte length <= MaximumInputBytes.
/// v0 is 0 on success (32 standard big-endian digest bytes), 1 on invalid ABI.
/// Preserves a0/a1, s0-s7, k0/k1, gp/fp/sp/ra and HI/LO. Other caller registers
/// may be clobbered. All argument/stack checks precede the first memory access.
/// The caller excludes concurrent input/code mutation and owns separate output,
/// workspace and stack. No allocation, helper calls, installation or save I/O.
/// FIPS 180-4 sections 4.1.2, 4.2.2, 5.1.1, 5.3.3 and 6.2.2 define the math:
/// https://csrc.nist.gov/files/pubs/fips/180-4/final/docs/fips180-4.pdf
/// This bounded implementation is not a NIST validation or runtime authority.
/// </summary>
internal sealed class NativeSha256Kernel
{
    internal const int RequiredWorkspaceByteLength = 288;
    internal const int RequiredStackByteLength = 64;
    internal const int DigestByteLength = 32;
    internal const int MaximumSupportedInputBytes = 1024;
    private readonly byte[] bytes;
    private NativeSha256Kernel(byte[] bytes, NativeByteExtent owner, int codeCapacityByteLength, uint entryPc, uint input,
        int maximumInputBytes, uint digest, uint workspace)
    {
        this.bytes = bytes.ToArray(); Owner = owner; CodeCapacityByteLength = codeCapacityByteLength; EntryPc = entryPc;
        CallerInputAddress = input; MaximumInputBytes = maximumInputBytes;
        DigestAddress = digest; WorkspaceAddress = workspace;
    }
    internal NativeByteExtent Owner { get; }
    internal int CodeCapacityByteLength { get; }
    internal uint EntryPc { get; }
    internal uint CallerInputAddress { get; }
    internal int MaximumInputBytes { get; }
    internal uint DigestAddress { get; }
    internal uint WorkspaceAddress { get; }
    internal int WorkspaceByteLength => RequiredWorkspaceByteLength;
    internal int StackByteLength => RequiredStackByteLength;
    internal bool NativeResidenceIssued => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeSha256Kernel Compile(NativeByteExtent codeCapacity, uint codeAddress,
        uint inputAddress, int maximumInputBytes, uint digestAddress, uint workspaceAddress)
    {
        ArgumentNullException.ThrowIfNull(codeCapacity);
        if (codeCapacity.Space.Kind != NativeAddressKind.LevelData || codeCapacity.Space.Revision != NativeAddressRevision.Final ||
            codeCapacity.Offset % 4 != 0 || codeCapacity.ByteLength % 4 != 0)
            throw new InvalidDataException("SHA-256 requires an aligned final LevelData code capacity.");
        if (maximumInputBytes is < 1 or > MaximumSupportedInputBytes)
            throw new InvalidDataException("SHA-256 input capacity must be between 1 and 1024 bytes.");
        (uint Start, ulong Length)[] ranges = [
            (codeAddress, (ulong)codeCapacity.ByteLength), (inputAddress, (uint)maximumInputBytes),
            (digestAddress, DigestByteLength), (workspaceAddress, RequiredWorkspaceByteLength) ];
        foreach (var range in ranges)
            if (range.Start % 4 != 0 || range.Start < 0x80000000 || range.Length > 0x200000 ||
                (ulong)range.Start + range.Length > 0x80200000)
                throw new InvalidDataException("Every complete SHA-256 range must be word aligned in cached main RAM.");
        if (workspaceAddress % 16 != 0)
            throw new InvalidDataException("SHA-256 workspace must be sixteen-byte aligned.");
        for (int i = 0; i < ranges.Length; i++)
            for (int j = 0; j < i; j++)
                if (ranges[i].Start < (ulong)ranges[j].Start + ranges[j].Length &&
                    ranges[j].Start < (ulong)ranges[i].Start + ranges[i].Length)
                    throw new InvalidDataException("SHA-256 code capacity, input capacity, digest and workspace must be disjoint.");

        var e = new Emitter(codeAddress);
        const int z = 0, v0 = 2, a0 = 4, a1 = 5, t0 = 8, t1 = 9, t2 = 10, t3 = 11,
            t4 = 12, t5 = 13, t6 = 14, t7 = 15, t8 = 24, t9 = 25, sp = 29, ra = 31;
        // Refusal path uses no loads/stores, including no speculative delay-slot access.
        e.Constant(t0, inputAddress); e.Branch(5, a0, t0, "invalid");
        e.Constant(t0, (uint)maximumInputBytes); e.R(0x2B, t0, a1, t1); e.Branch(5, t1, z, "invalid");
        e.I(0x0C, sp, t0, 7); e.Branch(5, t0, z, "invalid");
        e.Constant(t0, 0x80000000 + RequiredStackByteLength); e.R(0x2B, sp, t0, t1); e.Branch(5, t1, z, "invalid");
        e.Constant(t0, 0x80200000); e.R(0x2B, t0, sp, t1); e.Branch(5, t1, z, "invalid");
        e.I(9, sp, t0, -RequiredStackByteLength);
        for (int i = 0; i < ranges.Length; i++)
        {
            e.Constant(t1, checked((uint)(ranges[i].Start + ranges[i].Length)));
            e.R(0x2B, t0, t1, t2); e.Branch(4, t2, z, "stack-disjoint-" + i);
            e.Constant(t1, ranges[i].Start); e.R(0x2B, t1, sp, t2); e.Branch(5, t2, z, "invalid");
            e.Label("stack-disjoint-" + i);
        }
        e.I(9, sp, sp, -RequiredStackByteLength);
        for (int i = 0; i < 8; i++) e.I(0x2B, sp, 16 + i, i * 4);
        e.I(0x2B, sp, z, 32); // Next message block offset.
        e.I(9, a1, t0, 72); e.R(2, z, t0, t0, 6); e.R(0, z, t0, t0, 6);
        e.I(0x2B, sp, t0, 36); // Padded byte length, always 64..1088.
        e.Constant(t8, workspaceAddress);
        uint[] initial = [0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A, 0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19];
        for (int i = 0; i < initial.Length; i++) { e.Constant(t0, initial[i]); e.I(0x2B, t8, t0, 256 + i * 4); }

        e.Label("block");
        e.Load(0x23, t0, sp, 32); e.I(9, t0, t2, 64); e.Move(t1, t8);
        // Build the first sixteen big-endian words directly. Bytes past input
        // are synthetic padding; no input over-read or temporary padded copy.
        e.Label("word"); e.Constant(t3, 0); e.Constant(t4, 4);
        e.Label("byte");
        e.R(0x2B, t0, a1, t6); e.Branch(5, t6, z, "input-byte");
        e.Constant(t5, 0); e.Branch(5, t0, a1, "append-byte");
        e.Constant(t5, 0x80); e.Branch(4, z, z, "append-byte");
        e.Label("input-byte"); e.R(0x21, a0, t0, t6); e.Load(0x24, t5, t6, 0);
        e.Label("append-byte"); e.R(0, z, t3, t3, 8); e.R(0x25, t3, t5, t3);
        e.I(9, t0, t0, 1); e.I(9, t4, t4, -1); e.Branch(5, t4, z, "byte");
        e.I(0x2B, t1, t3, 0); e.I(9, t1, t1, 4); e.Branch(5, t0, t2, "word");
        e.I(0x2B, sp, t0, 32); e.Load(0x23, t1, sp, 36); e.Branch(5, t0, t1, "schedule");
        // Capacity bounds prove the high 32 bits of bit length are zero.
        e.I(0x2B, t8, z, 56); e.R(0, z, a1, t1, 3); e.I(0x2B, t8, t1, 60);

        e.Label("schedule"); e.I(9, t8, t0, 64); e.I(9, t8, t9, 256);
        e.Label("schedule-word");
        e.Load(0x23, t1, t0, -60); SmallSigma(t1, t2, t3, 7, 18, 3);
        e.Load(0x23, t1, t0, -8); SmallSigma(t1, t4, t3, 17, 19, 10);
        e.R(0x21, t2, t4, t2); e.Load(0x23, t1, t0, -64); e.R(0x21, t2, t1, t2);
        e.Load(0x23, t1, t0, -28); e.R(0x21, t2, t1, t2); e.I(0x2B, t0, t2, 0);
        e.I(9, t0, t0, 4); e.Branch(5, t0, t9, "schedule-word");
        for (int i = 0; i < 8; i++) e.Load(0x23, 16 + i, t8, 256 + i * 4);
        e.Move(t0, t8); e.Address(t1, "round-constants");
        e.Label("round");
        // s0..s7 are a..h. t3=T1, t4=T2; all additions wrap modulo 2^32.
        BigSigma(20, t3, t5, 6, 11, 25); e.R(0x21, t3, 23, t3);
        // Ch(e,f,g) = g XOR (e AND (f XOR g)), avoiding NOT/pseudo-ops.
        e.R(0x26, 21, 22, t5); e.R(0x24, 20, t5, t5); e.R(0x26, 22, t5, t5); e.R(0x21, t3, t5, t3);
        e.Load(0x23, t5, t1, 0); e.R(0x21, t3, t5, t3); e.Load(0x23, t5, t0, 0); e.R(0x21, t3, t5, t3);
        BigSigma(16, t4, t5, 2, 13, 22);
        // Maj(a,b,c) = (a AND b) XOR (c AND (a XOR b)).
        e.R(0x24, 16, 17, t5); e.R(0x26, 16, 17, t6); e.R(0x24, 18, t6, t6);
        e.R(0x26, t5, t6, t5); e.R(0x21, t4, t5, t4);
        e.Move(23, 22); e.Move(22, 21); e.Move(21, 20); e.R(0x21, 19, t3, 20);
        e.Move(19, 18); e.Move(18, 17); e.Move(17, 16); e.R(0x21, t3, t4, 16);
        e.I(9, t0, t0, 4); e.I(9, t1, t1, 4); e.Branch(5, t0, t9, "round");
        for (int i = 0; i < 8; i++)
        {
            e.Load(0x23, t0, t8, 256 + i * 4); e.R(0x21, t0, 16 + i, t0); e.I(0x2B, t8, t0, 256 + i * 4);
        }
        e.Load(0x23, t0, sp, 32); e.Load(0x23, t1, sp, 36); e.Branch(5, t0, t1, "block");
        e.Constant(t1, digestAddress);
        for (int i = 0; i < 8; i++)
        {
            e.Load(0x23, t0, t8, 256 + i * 4);
            for (int b = 0; b < 4; b++)
            { e.R(2, z, t0, t2, 24 - b * 8); e.I(0x28, t1, t2, i * 4 + b); }
        }
        for (int i = 0; i < 8; i++) e.Load(0x23, 16 + i, sp, i * 4);
        e.I(9, sp, sp, RequiredStackByteLength); Return(0);
        e.Label("invalid"); Return(1);
        e.Label("round-constants");
        uint[] constants = [
            0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5, 0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
            0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3, 0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
            0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC, 0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
            0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7, 0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
            0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13, 0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
            0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3, 0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
            0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5, 0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
            0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208, 0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2 ];
        foreach (uint constant in constants) e.Word(constant);
        byte[] code = e.Finish();
        if (code.Length > codeCapacity.ByteLength)
            throw new InvalidDataException("SHA-256 code and constants exceed the supplied capacity.");
        return new(code, new(codeCapacity.Space, codeCapacity.Offset, code.Length), checked((int)codeCapacity.ByteLength), codeAddress,
            inputAddress, maximumInputBytes, digestAddress, workspaceAddress);

        void Return(uint result) { e.Constant(v0, result); e.R(8, ra, z, z); e.Word(0); }
        void Rotate(int input, int output, int scratch, int count)
        { e.R(2, z, input, output, count); e.R(0, z, input, scratch, 32 - count); e.R(0x25, output, scratch, output); }
        void SmallSigma(int input, int output, int scratch, int r1, int r2, int shift)
        {
            Rotate(input, output, scratch, r1); Rotate(input, t7, scratch, r2);
            e.R(0x26, output, t7, output); e.R(2, z, input, scratch, shift); e.R(0x26, output, scratch, output);
        }
        void BigSigma(int input, int output, int scratch, int r1, int r2, int r3)
        {
            Rotate(input, output, scratch, r1); Rotate(input, t7, scratch, r2); e.R(0x26, output, t7, output);
            Rotate(input, t7, scratch, r3); e.R(0x26, output, t7, output);
        }
    }

    // Restricted local encoder. Loads and transfers always retire through an
    // explicit NOP; no hidden at register, HI/LO mutation, call or pseudoinstruction.
    private sealed class Emitter(uint baseAddress)
    {
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = [];
        private readonly List<(int Index, string Label)> branches = [], addresses = [];
        internal void Word(uint word) => words.Add(word);
        internal void R(int fn, int rs, int rt, int rd, int shift = 0) => Word((uint)(rs << 21 | rt << 16 | rd << 11 | shift << 6 | fn));
        internal void I(int op, int rs, int rt, int value) => Word((uint)(op << 26 | rs << 21 | rt << 16) | (ushort)value);
        internal void Move(int destination, int source) => R(0x21, source, 0, destination);
        internal void Constant(int register, uint value)
        {
            if (value <= ushort.MaxValue) I(0x0D, 0, register, (int)value);
            else { I(0x0F, 0, register, (int)(value >> 16)); if ((value & 65535) != 0) I(0x0D, register, register, (int)(value & 65535)); }
        }
        internal void Address(int register, string label)
        { addresses.Add((words.Count, label)); I(0x0F, 0, register, 0); I(0x0D, register, register, 0); }
        internal void Load(int op, int register, int pointer, int offset) { I(op, pointer, register, offset); Word(0); }
        internal void Label(string label) => labels.Add(label, words.Count);
        internal void Branch(int op, int rs, int rt, string label)
        { branches.Add((words.Count, label)); I(op, rs, rt, 0); Word(0); }
        internal byte[] Finish()
        {
            foreach (var branch in branches)
            {
                int delta = labels[branch.Label] - branch.Index - 1;
                if (delta < short.MinValue || delta > short.MaxValue) throw new InvalidDataException("SHA-256 branch outside MIPS-I range.");
                words[branch.Index] |= (ushort)(short)delta;
            }
            foreach (var address in addresses)
            {
                uint value = checked(baseAddress + (uint)labels[address.Label] * 4);
                words[address.Index] |= value >> 16; words[address.Index + 1] |= value & 65535;
            }
            byte[] result = new byte[checked(words.Count * 4)];
            for (int i = 0; i < words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i * 4), words[i]);
            return result;
        }
    }
}
