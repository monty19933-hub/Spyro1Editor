/// <summary>A tiny R3000 subset (the instructions the 8 MB check and its hook sites use), with branch and load delay
/// slots, over word memory that either mirrors the first 2 MB at 0x80200000.. (a PlayStation) or holds separate
/// words there (DuckStation with 8 MB). A test interpreter, not an emulator.</summary>
internal sealed class CheckCpu(bool mirror)
{
    private readonly Dictionary<uint, uint> _memory = [];
    internal uint[] Regs { get; } = new uint[32];
    internal List<uint> Writes { get; } = [];
    private uint Physical(uint address)
    {
        if (address % 4 != 0 || address < 0x80000000 || address >= 0x80800000) throw new InvalidOperationException($"Address 0x{address:X8} is outside main RAM.");
        return mirror ? 0x80000000 | (address & 0x1FFFFF) : address;
    }
    internal uint Get(uint address) => _memory.TryGetValue(Physical(address), out uint word) ? word : 0;
    internal void Put(uint address, uint word) => _memory[Physical(address)] = word;
    internal void Put(uint address, IEnumerable<uint> words) { foreach (uint word in words) { Put(address, word); address += 4; } }

    /// <summary>Runs from <paramref name="pc"/> until it reaches <paramref name="stop"/> (at most 200 instructions).</summary>
    internal int Run(uint pc, uint stop)
    {
        uint? jump = null; (int Reg, uint Value)? load = null; int steps = 0;
        while (pc != stop || jump != null)
        {
            if (++steps > 200) throw new InvalidOperationException("The check did not return.");
            uint w = Get(pc), next = pc + 4;
            int op = (int)(w >> 26), rs = (int)(w >> 21) & 31, rt = (int)(w >> 16) & 31, rd = (int)(w >> 11) & 31;
            short imm = (short)w; uint a = Regs[rs], b = Regs[rt];
            var pendingLoad = load; load = null; var pendingJump = jump; jump = null;
            (int Reg, uint Value)? write = null;
            switch (op)
            {
                case 0:
                    switch (w & 63)
                    {
                        case 0: write = (rd, b << (int)((w >> 6) & 31)); break;   // sll (nop)
                        case 8: jump = a; break;                                    // jr
                        case 0x21: write = (rd, a + b); break;                      // addu
                        default: throw new InvalidOperationException($"Unexpected instruction {w:X8} at {pc:X8}.");
                    }
                    break;
                case 3: Regs[31] = pc + 8; jump = (pc & 0xF0000000) | ((w & 0x3FFFFFF) << 2); break;   // jal
                case 4: if (a == b) jump = (uint)(next + imm * 4); break;                             // beq
                case 9: write = (rt, (uint)(a + imm)); break;                                          // addiu
                case 15: write = (rt, (uint)imm << 16); break;                                         // lui
                case 35: load = (rt, Get((uint)(a + imm))); break;                                     // lw
                case 43: Put((uint)(a + imm), b); Writes.Add((uint)(a + imm)); break;                 // sw
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
