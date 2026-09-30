namespace Spyro.Editor.Core.Universal;

/// <summary>Minimal R3000 decode/encode helpers used by the universal behavior relinker.</summary>
public static class UniversalMips
{
    public const uint Nop = 0;
    public const uint JrRa = 0x03E00008;

    public static int Op(uint w) => (int)(w >> 26);
    public static int Rs(uint w) => (int)((w >> 21) & 31);
    public static int Rt(uint w) => (int)((w >> 16) & 31);
    public static int Rd(uint w) => (int)((w >> 11) & 31);
    public static int Sh(uint w) => (int)((w >> 6) & 31);
    public static int Fn(uint w) => (int)(w & 63);
    public static int Imm(uint w) => (int)(w & 0xFFFF);
    public static int SImm(uint w) => (short)(w & 0xFFFF);

    public static bool IsBranch(uint w)
    {
        int op = Op(w);
        if (op is 2 or 3 or 4 or 5 or 6 or 7) return true;
        if (op == 1 && Rt(w) is 0 or 1 or 16 or 17) return true;
        return op == 0 && Fn(w) is 8 or 9;
    }

    public static bool IsJal(uint w) => Op(w) == 3;
    public static bool IsJalr(uint w) => Op(w) == 0 && Fn(w) == 9;
    public static bool IsJr(uint w) => Op(w) == 0 && Fn(w) == 8;
    public static bool IsPcRelative(uint w) => Op(w) is 4 or 5 or 6 or 7 || (Op(w) == 1 && Rt(w) is 0 or 1 or 16 or 17);
    public static bool IsUnconditionalBranch(uint w) => Op(w) == 2 || (Op(w) == 4 && Rs(w) == 0 && Rt(w) == 0);
    public static bool IsLoad(uint w) => Op(w) is 32 or 33 or 34 or 35 or 36 or 37 or 38;
    public static bool IsStore(uint w) => Op(w) is 40 or 41 or 42 or 43 or 46;

    public static uint BranchTarget(uint w, uint pc)
    {
        int op = Op(w);
        if (op is 2 or 3) return ((pc + 4) & 0xF0000000u) | ((w & 0x3FFFFFF) << 2);
        return (uint)(pc + 4 + (SImm(w) << 2));
    }

    public static uint EncodeI(int op, int rs, int rt, int imm) =>
        ((uint)op << 26) | ((uint)rs << 21) | ((uint)rt << 16) | ((uint)imm & 0xFFFF);
    public static uint EncodeJ(int op, uint target) => ((uint)op << 26) | ((target >> 2) & 0x3FFFFFF);
    public static uint WithImm(uint w, int imm) => (w & 0xFFFF0000u) | ((uint)imm & 0xFFFF);
    public static uint WithJumpTarget(uint w, uint target) => (w & 0xFC000000u) | ((target >> 2) & 0x3FFFFFF);
    public static uint WithBranchTarget(uint w, uint pc, uint target)
    {
        long delta = ((long)target - (pc + 4)) >> 2;
        if (delta < short.MinValue || delta > short.MaxValue)
            throw new InvalidDataException($"Relocated branch at 0x{pc:X8} cannot reach 0x{target:X8}.");
        return WithImm(w, (int)delta);
    }
    public static int Hi(uint address) => (int)(((address + 0x8000) >> 16) & 0xFFFF);
    public static int Lo(uint address) => (int)(address & 0xFFFF);
    public static int Sgn(uint v) => unchecked((int)v);
}

/// <summary>Concrete-value evaluator for compiled class switches; unknown values stay null.</summary>
public sealed class UniversalMipsState
{
    private readonly uint?[] _r = new uint?[32];
    public UniversalMipsState(IReadOnlyDictionary<int, uint>? seeds = null)
    {
        if (seeds != null) foreach (var (k, v) in seeds) _r[k] = v;
        _r[0] = 0;
    }
    public uint? this[int r] { get => r == 0 ? 0 : _r[r]; set { if (r != 0) _r[r] = value; } }
    public Dictionary<int, uint> Snapshot()
    {
        Dictionary<int, uint> d = [];
        for (int i = 0; i < 32; i++) if (this[i] is uint v) d[i] = v;
        return d;
    }

    /// <summary>Applies a non-branch instruction. Unknown effects clear the written register.</summary>
    public void Apply(uint w)
    {
        if (w == 0) return;
        if (!TryAlu(w)) Kill(w);
    }

    private bool TryAlu(uint w)
    {
        int op = UniversalMips.Op(w), rs = UniversalMips.Rs(w), rt = UniversalMips.Rt(w), rd = UniversalMips.Rd(w);
        int sh = UniversalMips.Sh(w), fn = UniversalMips.Fn(w), imm = UniversalMips.Imm(w), simm = UniversalMips.SImm(w);
        uint? a = this[rs], b = this[rt];
        if (UniversalMips.IsStore(w)) return true;
        if (UniversalMips.IsLoad(w)) { this[rt] = null; return true; }
        switch (op)
        {
            case 8: case 9: this[rt] = a is uint x ? (uint)(x + simm) : null; return true;
            case 10: this[rt] = a is uint x2 ? (UniversalMips.Sgn(x2) < simm ? 1u : 0u) : null; return true;
            case 11: this[rt] = a is uint x3 ? (x3 < (uint)simm ? 1u : 0u) : null; return true;
            case 12: this[rt] = a is uint x4 ? x4 & (uint)imm : null; return true;
            case 13: this[rt] = a is uint x5 ? x5 | (uint)imm : null; return true;
            case 14: this[rt] = a is uint x6 ? x6 ^ (uint)imm : null; return true;
            case 15: this[rt] = (uint)imm << 16; return true;
            case 0:
                switch (fn)
                {
                    case 0x00: this[rd] = b is uint s0 ? s0 << sh : null; return true;
                    case 0x02: this[rd] = b is uint s2 ? s2 >> sh : null; return true;
                    case 0x03: this[rd] = b is uint s3 ? (uint)(UniversalMips.Sgn(s3) >> sh) : null; return true;
                    case 0x04: this[rd] = a is uint v4 && b is uint w4 ? w4 << (int)(v4 & 31) : null; return true;
                    case 0x06: this[rd] = a is uint v6 && b is uint w6 ? w6 >> (int)(v6 & 31) : null; return true;
                    case 0x07: this[rd] = a is uint v7 && b is uint w7 ? (uint)(UniversalMips.Sgn(w7) >> (int)(v7 & 31)) : null; return true;
                    case 0x20: case 0x21: this[rd] = a is uint p && b is uint q ? p + q : null; return true;
                    case 0x22: case 0x23: this[rd] = a is uint p2 && b is uint q2 ? p2 - q2 : null; return true;
                    case 0x24: this[rd] = a is uint p3 && b is uint q3 ? p3 & q3 : null; return true;
                    case 0x25: this[rd] = a is uint p4 && b is uint q4 ? p4 | q4 : null; return true;
                    case 0x26: this[rd] = a is uint p5 && b is uint q5 ? p5 ^ q5 : null; return true;
                    case 0x27: this[rd] = a is uint p6 && b is uint q6 ? ~(p6 | q6) : null; return true;
                    case 0x2A: this[rd] = a is uint p7 && b is uint q7 ? (UniversalMips.Sgn(p7) < UniversalMips.Sgn(q7) ? 1u : 0u) : null; return true;
                    case 0x2B: this[rd] = a is uint p8 && b is uint q8 ? (p8 < q8 ? 1u : 0u) : null; return true;
                }
                return false;
        }
        return false;
    }

    private void Kill(uint w)
    {
        int op = UniversalMips.Op(w);
        if (op == 0)
        {
            int fn = UniversalMips.Fn(w);
            if (fn is 0x18 or 0x19 or 0x1A or 0x1B or 0x11 or 0x13 or 0x0C or 0x0D) return;
            this[UniversalMips.Rd(w)] = null;
            return;
        }
        if (UniversalMips.IsStore(w)) return;
        if (op is 0x10 or 0x12) { if (UniversalMips.Rs(w) is 0 or 2) this[UniversalMips.Rt(w)] = null; return; }
        this[UniversalMips.Rt(w)] = null;
    }

    public bool? Condition(uint w)
    {
        int op = UniversalMips.Op(w);
        uint? a = this[UniversalMips.Rs(w)], b = this[UniversalMips.Rt(w)];
        switch (op)
        {
            case 2: return true;
            case 4: return a is uint x && b is uint y ? x == y : null;
            case 5: return a is uint x1 && b is uint y1 ? x1 != y1 : null;
            case 6: return a is uint x2 ? UniversalMips.Sgn(x2) <= 0 : null;
            case 7: return a is uint x3 ? UniversalMips.Sgn(x3) > 0 : null;
            case 1:
                int rt = UniversalMips.Rt(w);
                if (rt == 0) return a is uint x4 ? UniversalMips.Sgn(x4) < 0 : null;
                if (rt == 1) return a is uint x5 ? UniversalMips.Sgn(x5) >= 0 : null;
                return null;
        }
        return null;
    }
}
