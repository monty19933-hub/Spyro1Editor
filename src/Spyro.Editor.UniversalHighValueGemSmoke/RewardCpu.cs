/// <summary>A small R3000 test interpreter for the teal and rainbow reward routines: the integer instructions they use,
/// with branch delay slots and load delays, over the first 2 MB of RAM. Calls into the game's own routines stop at stubs
/// that record their arguments and return. A test interpreter, not an emulator.</summary>
internal sealed class RewardCpu
{
    private readonly byte[] _ram = new byte[0x200000];
    internal uint[] Regs { get; } = new uint[32];
    internal Dictionary<uint, Action<RewardCpu>> Stubs { get; } = [];
    internal List<(uint At, uint A0, uint A1, uint A2, uint A3)> Calls { get; } = [];
    internal const uint Return = 0x8001FFF0;
    private int Offset(uint address, int size)
    {
        if (address < 0x80000000 || address + (uint)size > 0x80200000 || address % (uint)size != 0)
            throw new InvalidOperationException($"Address 0x{address:X8} is outside RAM or unaligned.");
        return (int)(address - 0x80000000);
    }
    internal byte U8(uint a) => _ram[Offset(a, 1)];
    internal ushort U16(uint a) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(_ram.AsSpan(Offset(a, 2)));
    internal uint U32(uint a) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(_ram.AsSpan(Offset(a, 4)));
    internal void W8(uint a, int v) => _ram[Offset(a, 1)] = (byte)v;
    internal void W16(uint a, int v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(_ram.AsSpan(Offset(a, 2)), (ushort)v);
    internal void W32(uint a, uint v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(_ram.AsSpan(Offset(a, 4)), v);
    internal void Load(uint a, byte[] code) => code.CopyTo(_ram, Offset(a, 4));

    /// <summary>Calls <paramref name="entry"/> with ra = <see cref="Return"/> and runs until it returns.</summary>
    internal int Call(uint entry, int limit = 200000)
    {
        Regs[31] = Return; uint pc = entry; uint? jump = null; (int Reg, uint Value)? load = null; int steps = 0;
        while (pc != Return || jump != null)
        {
            if (++steps > limit) throw new InvalidOperationException("The routine did not return.");
            if (jump == null && Stubs.TryGetValue(pc, out var stub))
            {
                if (load is { } l0 && l0.Reg != 0) { Regs[l0.Reg] = l0.Value; load = null; }
                Calls.Add((pc, Regs[4], Regs[5], Regs[6], Regs[7])); stub(this); pc = Regs[31]; continue;
            }
            uint w = U32(pc), next = pc + 4;
            int op = (int)(w >> 26), rs = (int)(w >> 21) & 31, rt = (int)(w >> 16) & 31, rd = (int)(w >> 11) & 31, sh = (int)(w >> 6) & 31;
            short imm = (short)w; uint a = Regs[rs], b = Regs[rt];
            var pendingLoad = load; load = null; var pendingJump = jump; jump = null;
            (int Reg, uint Value)? write = null;
            uint Target() => (uint)(next + imm * 4);
            switch (op)
            {
                case 0:
                    switch (w & 63)
                    {
                        case 0: write = (rd, b << sh); break;
                        case 2: write = (rd, b >> sh); break;
                        case 3: write = (rd, (uint)((int)b >> sh)); break;
                        case 4: write = (rd, b << (int)(a & 31)); break;
                        case 6: write = (rd, b >> (int)(a & 31)); break;
                        case 8: jump = a; break;
                        case 9: write = (rd, pc + 8); jump = a; break;
                        case 0x21: write = (rd, a + b); break;
                        case 0x23: write = (rd, a - b); break;
                        case 0x24: write = (rd, a & b); break;
                        case 0x25: write = (rd, a | b); break;
                        case 0x2A: write = (rd, (int)a < (int)b ? 1u : 0); break;
                        case 0x2B: write = (rd, a < b ? 1u : 0); break;
                        default: throw new InvalidOperationException($"Unexpected instruction {w:X8} at {pc:X8}.");
                    }
                    break;
                case 1: if (rt == 0 ? (int)a < 0 : rt == 1 ? (int)a >= 0 : throw new InvalidOperationException($"Unexpected branch {w:X8}")) jump = Target(); break;
                case 2: jump = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2); break;
                case 3: write = (31, pc + 8); jump = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2); break;
                case 4: if (a == b) jump = Target(); break;
                case 5: if (a != b) jump = Target(); break;
                case 6: if ((int)a <= 0) jump = Target(); break;
                case 7: if ((int)a > 0) jump = Target(); break;
                case 9: write = (rt, (uint)(a + imm)); break;
                case 10: write = (rt, (int)a < imm ? 1u : 0); break;
                case 11: write = (rt, a < (uint)(int)imm ? 1u : 0); break;
                case 12: write = (rt, a & (ushort)imm); break;
                case 13: write = (rt, a | (ushort)imm); break;
                case 15: write = (rt, (uint)(ushort)imm << 16); break;
                case 32: load = (rt, (uint)(sbyte)U8((uint)(a + imm))); break;
                case 33: load = (rt, (uint)(short)U16((uint)(a + imm))); break;
                case 35: load = (rt, U32((uint)(a + imm))); break;
                case 36: load = (rt, U8((uint)(a + imm))); break;
                case 37: load = (rt, U16((uint)(a + imm))); break;
                case 40: W8((uint)(a + imm), (int)b); break;
                case 41: W16((uint)(a + imm), (int)b); break;
                case 43: W32((uint)(a + imm), b); break;
                default: throw new InvalidOperationException($"Unexpected instruction {w:X8} at {pc:X8}.");
            }
            if (pendingLoad is { } l && l.Reg != 0 && write?.Reg != l.Reg) Regs[l.Reg] = l.Value;
            if (write is { } x && x.Reg != 0) Regs[x.Reg] = x.Value;
            pc = pendingJump ?? next;
        }
        if (load is { } last && last.Reg != 0) Regs[last.Reg] = last.Value;
        return steps;
    }
}
