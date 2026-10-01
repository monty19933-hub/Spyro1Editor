namespace Spyro.Editor.Core.Universal;

public sealed record UniversalPlayerDamageWrite(uint StorePc, uint LoadPc, uint DamageMask);
public sealed record UniversalPlayerDamageWriteAnalysis(uint DamageMask, IReadOnlyList<UniversalPlayerDamageWrite> Writes)
{
    public IReadOnlyList<int> AnimationIds(byte[] originalExecutable) =>
        UniversalPlayerDamageDependencies.MapDamageMask(DamageMask, originalExecutable);
}

/// <summary>Supplemental source-code dependencies: an owned same-basic-block read/OR/write
/// of the native player collision accumulator. Mutable values, unknown incoming edges,
/// unsupported transformations and unowned instructions never become invented masks.</summary>
public static class UniversalPlayerDamageWrites
{
    public const uint PlayerDamageFlags = 0x80078A84;
    public static UniversalPlayerDamageWriteAnalysis AnalyzeOverlay(UniversalOverlay source, SortedSet<uint> instructions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(instructions);
        if (instructions.Count == 0) return new(0, []);
        SortedSet<uint> boundaries = [];
        uint previous = 0;
        foreach (uint pc in instructions)
        {
            if (!source.Contains(pc) || pc % 4 != 0)
                throw new InvalidDataException("Player damage instruction leaves its original overlay owner.");
            if (previous + 4 != pc) boundaries.Add(pc);
            previous = pc;
            uint word = source.Word(pc);
            if (!UniversalMips.IsBranch(word)) continue;
            if (UniversalMips.IsPcRelative(word) || UniversalMips.Op(word) == 2)
            {
                uint target = UniversalMips.BranchTarget(word, pc);
                if (instructions.Contains(target)) boundaries.Add(target);
            }
            if (instructions.Contains(pc + 8)) boundaries.Add(pc + 8);
            if (UniversalMips.IsJr(word) && UniversalMips.Rs(word) != 31)
            {
                var table = UniversalDispatchAnalysis.ResolveJumpTable(source, pc);
                if (table is null) return new(0, []);
                foreach (uint target in table.Targets)
                    if (instructions.Contains(target)) boundaries.Add(target);
            }
        }
        List<UniversalPlayerDamageWrite> writes = [];
        uint combined = 0;
        foreach (uint pc in instructions)
        {
            uint store = source.Word(pc);
            if (UniversalMips.Op(store) != 43) continue;
            uint floor = boundaries.GetViewBetween(instructions.Min, pc).Max;
            int budget = 512;
            Dictionary<(int, uint, int), uint?> cache = [];
            uint? address = Constant(UniversalMips.Rs(store), pc, 32);
            if (address is not uint b || unchecked(b + (uint)UniversalMips.SImm(store)) != PlayerDamageFlags) continue;
            var flags = Flags(UniversalMips.Rt(store), pc, 32);
            if (flags is not { } proof || proof.LoadPc + 8 > pc || proof.Mask == 0) continue;
            writes.Add(new(pc, proof.LoadPc, proof.Mask)); combined |= proof.Mask;

            (uint Pc, uint Word)? Definition(int register, uint before)
            {
                if (register == 0 || before <= floor) return null;
                for (uint at = before - 4; at >= floor && before - at <= 512; at -= 4)
                {
                    if (!instructions.Contains(at)) return null;
                    uint w = source.Word(at);
                    if (!UniversalMips.IsBranch(w) && !UniversalMips.IsStore(w) &&
                        (UniversalMips.Op(w) == 0 ? UniversalMips.Rd(w) : UniversalMips.Rt(w)) == register)
                        return (at, w);
                    if (at == floor) break;
                }
                return null;
            }
            uint? Constant(int register, uint before, int depth)
            {
                if (register == 0) return 0;
                var key = (register, before, depth);
                if (cache.TryGetValue(key, out var old)) return old;
                if (--budget < 0 || depth <= 0 || Definition(register, before) is not { } d) return null;
                int op = UniversalMips.Op(d.Word), rs = UniversalMips.Rs(d.Word), rt = UniversalMips.Rt(d.Word);
                uint? value;
                if (op == 15) value = (uint)UniversalMips.Imm(d.Word) << 16;
                else
                {
                    bool alu = op == 0 && UniversalMips.Fn(d.Word) is 0 or 2 or 3 or 4 or 6 or 7 or
                        0x20 or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x2A or 0x2B;
                    if (!(op is >= 8 and <= 14) && !alu) return null;
                    var state = new UniversalMipsState();
                    state[rs] = Constant(rs, d.Pc, depth - 1);
                    if (alu) state[rt] = Constant(rt, d.Pc, depth - 1);
                    state.Apply(d.Word); value = state[register];
                }
                cache[key] = value; return value;
            }
            (uint LoadPc, uint Mask)? Flags(int register, uint before, int depth)
            {
                if (--budget < 0 || depth <= 0 || Definition(register, before) is not { } d) return null;
                int op = UniversalMips.Op(d.Word), rs = UniversalMips.Rs(d.Word), rt = UniversalMips.Rt(d.Word);
                if (op == 35 && Constant(rs, d.Pc, depth - 1) is uint addr &&
                    unchecked(addr + (uint)UniversalMips.SImm(d.Word)) == PlayerDamageFlags)
                    return (d.Pc, 0);
                if (op == 13 && Flags(rs, d.Pc, depth - 1) is { } f && f.LoadPc + 8 <= d.Pc)
                    return (f.LoadPc, f.Mask | (uint)UniversalMips.Imm(d.Word));
                if (op == 0 && UniversalMips.Fn(d.Word) is 0x21 or 0x25 && (rs == 0 || rt == 0))
                {
                    var alias = Flags(rs == 0 ? rt : rs, d.Pc, depth - 1);
                    return alias is { } a && a.LoadPc + 8 <= d.Pc ? a : null;
                }
                if (op == 0 && UniversalMips.Fn(d.Word) == 0x25)
                {
                    foreach (var pair in new[] { (rs, rt), (rt, rs) })
                        if (Flags(pair.Item1, d.Pc, depth - 1) is { } f2 && f2.LoadPc + 8 <= d.Pc &&
                            Constant(pair.Item2, d.Pc, depth - 1) is uint mask)
                            return (f2.LoadPc, f2.Mask | mask);
                }
                return null;
            }
        }
        return new(combined, writes.AsReadOnly());
    }
}
