using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A small MIPS assembler for code placed into executable caves: named blocks of code or data, labels shared
/// across blocks, branch/jump/address fix-ups, and a first-fit placement of the blocks into a list of caves.</summary>
internal sealed class NativeCaveAssembler
{
    internal static readonly IReadOnlyDictionary<string, int> Reg = new[]
    {
        "zero", "at", "v0", "v1", "a0", "a1", "a2", "a3", "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7",
        "s0", "s1", "s2", "s3", "s4", "s5", "s6", "s7", "t8", "t9", "k0", "k1", "gp", "sp", "fp", "ra",
    }.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i);

    internal enum Fix { None, Branch, Jump, Hi, Lo }
    internal sealed record Item(uint Word, Fix Fix = Fix.None, string? Label = null, uint Absolute = 0, int Offset = 0);

    internal sealed class Block
    {
        public Block(string name, bool data) { Name = name; IsData = data; }
        public string Name { get; }
        public bool IsData { get; }
        internal List<Item> Items { get; } = [];
        internal Dictionary<string, int> Labels { get; } = [];
        public uint Address { get; internal set; }
        public int Length => Items.Count * 4;
        private static int R(string name) => Reg[name];

        public Block Label(string name) { Labels.Add(name, Items.Count); return this; }
        public Block Word(uint w) { Items.Add(new(w)); return this; }
        public void SetWord(int index, uint w) => Items[index] = new(w);
        public Block Bytes(ReadOnlySpan<byte> bytes)
        {
            int n = (bytes.Length + 3) / 4;
            byte[] padded = new byte[n * 4]; bytes.CopyTo(padded);
            for (int i = 0; i < n; i++) Items.Add(new(BinaryPrimitives.ReadUInt32LittleEndian(padded.AsSpan(4 * i))));
            return this;
        }
        private Block I(int op, string rs, string rt, int imm) => Word(((uint)op << 26) | ((uint)R(rs) << 21) | ((uint)R(rt) << 16) | ((uint)imm & 0xFFFF));
        private Block Rr(string rs, string rt, string rd, int sh, int fn) => Word(((uint)R(rs) << 21) | ((uint)R(rt) << 16) | ((uint)R(rd) << 11) | ((uint)sh << 6) | (uint)fn);
        public Block Lb(string rt, int off, string rs) => I(0x20, rs, rt, off);
        public Block Lh(string rt, int off, string rs) => I(0x21, rs, rt, off);
        public Block Lw(string rt, int off, string rs) => I(0x23, rs, rt, off);
        public Block Lbu(string rt, int off, string rs) => I(0x24, rs, rt, off);
        public Block Lhu(string rt, int off, string rs) => I(0x25, rs, rt, off);
        public Block Sb(string rt, int off, string rs) => I(0x28, rs, rt, off);
        public Block Sh(string rt, int off, string rs) => I(0x29, rs, rt, off);
        public Block Sw(string rt, int off, string rs) => I(0x2B, rs, rt, off);
        public Block Addiu(string rt, string rs, int imm) => I(0x09, rs, rt, imm);
        public Block Sltiu(string rt, string rs, int imm) => I(0x0B, rs, rt, imm);
        public Block Ori(string rt, string rs, int imm) => I(0x0D, rs, rt, imm);
        public Block Lui(string rt, int imm) => I(0x0F, "zero", rt, imm);
        public Block Addu(string rd, string rs, string rt) => Rr(rs, rt, rd, 0, 0x21);
        public Block Subu(string rd, string rs, string rt) => Rr(rs, rt, rd, 0, 0x23);
        public Block Sll(string rd, string rt, int sh) => Rr("zero", rt, rd, sh, 0x00);
        public Block Sra(string rd, string rt, int sh) => Rr("zero", rt, rd, sh, 0x03);
        public Block Mult(string rs, string rt) => Rr(rs, rt, "zero", 0, 0x18);
        public Block Mfhi(string rd) => Rr("zero", "zero", rd, 0, 0x10);
        public Block Jr(string rs) => Rr(rs, "zero", "zero", 0, 0x08);
        public Block Move(string rd, string rs) => Addu(rd, rs, "zero");
        public Block Nop() => Word(0);
        public Block Li(string rt, int value) => value is >= short.MinValue and <= short.MaxValue
            ? Addiu(rt, "zero", value) : value is >= 0 and <= 0xFFFF ? Ori(rt, "zero", value) : throw new ArgumentOutOfRangeException(nameof(value));
        private Block Branch(int op, string rs, string rt, string label) { Items.Add(new(((uint)op << 26) | ((uint)R(rs) << 21) | ((uint)R(rt) << 16), Fix.Branch, label)); return this; }
        public Block Beq(string rs, string rt, string label) => Branch(4, rs, rt, label);
        public Block Bne(string rs, string rt, string label) => Branch(5, rs, rt, label);
        public Block Beqz(string rs, string label) => Branch(4, rs, "zero", label);
        public Block Bnez(string rs, string label) => Branch(5, rs, "zero", label);
        public Block B(string label) => Branch(4, "zero", "zero", label);
        public Block J(string label) { Items.Add(new(2u << 26, Fix.Jump, label)); return this; }
        public Block J(uint target) { Items.Add(new(2u << 26, Fix.Jump, null, target)); return this; }
        public Block Jal(string label) { Items.Add(new(3u << 26, Fix.Jump, label)); return this; }
        /// <summary>lui rt, %hi(target); the matching %lo goes into the next load/store/addiu (<see cref="LoLoad"/>).</summary>
        public Block LuiHi(string rt, string label, int offset = 0) { Items.Add(new(0x0Fu << 26 | ((uint)R(rt) << 16), Fix.Hi, label, 0, offset)); return this; }
        public Block LuiHi(string rt, uint address) { Items.Add(new(0x0Fu << 26 | ((uint)R(rt) << 16), Fix.Hi, null, address)); return this; }
        /// <summary>An I-type instruction whose immediate is %lo(label + offset).</summary>
        public Block LoLoad(int op, string rt, string rs, string label, int offset = 0) { Items.Add(new(((uint)op << 26) | ((uint)R(rs) << 21) | ((uint)R(rt) << 16), Fix.Lo, label, 0, offset)); return this; }
        public Block LoLoad(int op, string rt, string rs, uint address) { Items.Add(new(((uint)op << 26) | ((uint)R(rs) << 21) | ((uint)R(rt) << 16), Fix.Lo, null, address)); return this; }
        /// <summary>rt = label + offset (lui + addiu).</summary>
        public Block La(string rt, string label, int offset = 0) => LuiHi(rt, label, offset).LoLoad(0x09, rt, rt, label, offset);
        public Block La(string rt, uint address) => LuiHi(rt, address).LoLoad(0x09, rt, rt, address);

        internal uint[] Encode(IReadOnlyDictionary<string, uint> labels)
        {
            var words = new uint[Items.Count];
            for (int k = 0; k < Items.Count; k++)
            {
                var it = Items[k]; uint pc = Address + 4 * (uint)k;
                uint target = it.Label == null ? it.Absolute : labels.TryGetValue(it.Label, out var a) ? a : throw new InvalidDataException($"Unknown label {it.Label}.");
                target = unchecked(target + (uint)it.Offset);
                words[k] = it.Fix switch
                {
                    Fix.None => it.Word,
                    Fix.Branch => it.Word | (uint)(ushort)checked((short)(((long)target - (pc + 4)) >> 2)),
                    Fix.Jump => (pc & 0xF0000000) == (target & 0xF0000000) ? it.Word | ((target >> 2) & 0x03FFFFFF) : throw new InvalidDataException("Jump out of segment."),
                    Fix.Hi => it.Word | (uint)UniversalMipsHi(target),
                    Fix.Lo => it.Word | (target & 0xFFFF),
                    _ => throw new InvalidOperationException(),
                };
                if (it.Fix == Fix.Branch && (((long)target - (pc + 4)) & 3) != 0) throw new InvalidDataException("Misaligned branch target.");
            }
            return words;
        }
        private static int UniversalMipsHi(uint address) => (int)(((address + 0x8000) >> 16) & 0xFFFF);
    }

    public List<Block> Blocks { get; } = [];
    public Block Code(string name) { var b = new Block(name, false); Blocks.Add(b); return b; }
    public Block Data(string name) { var b = new Block(name, true); Blocks.Add(b); return b; }

    /// <summary>Places every block into the caves (largest block first, first cave with room; a block never spans two
    /// caves) and returns the label table.</summary>
    public IReadOnlyDictionary<string, uint> Place(IReadOnlyList<NativeExecutableCaves.Cave> caves)
    {
        var free = caves.Select(c => (c.Start, c.End)).ToList();
        foreach (var b in Blocks.OrderByDescending(b => b.Length).ThenBy(b => b.Name, StringComparer.Ordinal))
        {
            int i = free.FindIndex(f => f.End - f.Start >= b.Length);
            if (i < 0) throw new InvalidDataException($"The level window's {b.Name} ({b.Length} bytes) does not fit its executable caves " +
                $"(blocks {string.Join(", ", Blocks.Select(x => $"{x.Name} {x.Length}"))}; room {string.Join(", ", free.Select(f => f.End - f.Start))}).");
            b.Address = free[i].Start;
            free[i] = (free[i].Start + (uint)b.Length, free[i].End);
        }
        Dictionary<string, uint> labels = [];
        foreach (var b in Blocks)
        {
            labels.Add(b.Name, b.Address);
            foreach (var (name, index) in b.Labels) labels.Add(name, b.Address + 4 * (uint)index);
        }
        return labels;
    }
}
