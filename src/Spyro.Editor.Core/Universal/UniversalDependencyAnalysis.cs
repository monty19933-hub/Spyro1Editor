namespace Spyro.Editor.Core.Universal;

public sealed record UniversalDependencyCall(uint CallPc, uint Global, uint GlobalLoadPc, uint? Argument0, uint? Argument1);

/// <summary>Supplemental constant recovery within one source-owned basic block. It does not read
/// mutable memory, cross control-flow entries, or guess arguments returned by other functions.</summary>
public static class UniversalDependencyAnalysis
{
    private const uint MaximumLookbackBytes = 128 * 4;
    private const int MaximumExpressionDepth = 32;

    public static IReadOnlyList<UniversalDependencyCall> Calls(UniversalOverlay source, SortedSet<uint> instructions)
    {
        if (instructions.Count == 0) return [];
        SortedSet<uint> boundaries = [];
        uint previous = 0;
        foreach (uint pc in instructions)
        {
            if (previous + 4 != pc) boundaries.Add(pc);
            previous = pc;
            uint word = source.Word(pc);
            if (!UniversalMips.IsBranch(word)) continue;
            if (UniversalMips.IsPcRelative(word) || UniversalMips.Op(word) == 2)
            {
                uint target = UniversalMips.BranchTarget(word, pc);
                if (instructions.Contains(target)) boundaries.Add(target);
            }
            // A call's continuation has caller-clobbered registers. A branch's continuation
            // can be reached independently of its lexical predecessor and delay slot.
            if (instructions.Contains(pc + 8)) boundaries.Add(pc + 8);
            if (UniversalMips.IsJr(word) && UniversalMips.Rs(word) != 31)
            {
                var table = UniversalDispatchAnalysis.ResolveJumpTable(source, pc);
                if (table is null) return []; // no sound claim about unknown incoming edges
                foreach (uint target in table.Targets)
                    if (instructions.Contains(target)) boundaries.Add(target);
            }
        }

        List<UniversalDependencyCall> calls = [];
        foreach (uint pc in instructions)
        {
            uint word = source.Word(pc);
            if (!UniversalMips.IsJalr(word) || !instructions.Contains(pc + 4) ||
                UniversalMips.IsBranch(source.Word(pc + 4))) continue;
            uint floor = boundaries.GetViewBetween(instructions.Min, pc).Max;
            Dictionary<(int Register, uint Before, int Depth), uint?> constants = [];
            int expressionBudget = 512;
            var global = Global(UniversalMips.Rs(word), pc, MaximumExpressionDepth);
            if (global is not { } owner || owner.LoadPc + 8 > pc ||
                owner.Address is not (UniversalExeBindings.InitGlobal or UniversalExeBindings.ParticleSpawnGlobal)) continue;
            // The call target is read before the delay slot; arguments are read by the callee
            // after that slot. A delay-slot rewrite of the target therefore does not change it.
            calls.Add(new(pc, owner.Address, owner.LoadPc,
                Constant(4, pc + 8, MaximumExpressionDepth), Constant(5, pc + 8, MaximumExpressionDepth)));

            (uint Pc, uint Word)? Definition(int register, uint before)
            {
                if (register == 0 || before <= floor) return null;
                for (uint at = before - 4; at >= floor && before - at <= MaximumLookbackBytes; at -= 4)
                {
                    if (!instructions.Contains(at)) return null;
                    uint candidate = source.Word(at);
                    if (!UniversalMips.IsBranch(candidate) && !UniversalMips.IsStore(candidate) &&
                        (UniversalMips.Op(candidate) == 0 ? UniversalMips.Rd(candidate) : UniversalMips.Rt(candidate)) == register)
                        return (at, candidate);
                    if (at == floor) break;
                }
                return null;
            }

            uint? Constant(int register, uint before, int depth)
            {
                if (register == 0) return 0;
                var key = (register, before, depth);
                if (constants.TryGetValue(key, out var cached)) return cached;
                if (--expressionBudget < 0) return null;
                uint? value = RecoverConstant(register, before, depth);
                constants[key] = value;
                return value;
            }

            uint? RecoverConstant(int register, uint before, int depth)
            {
                if (depth <= 0 || Definition(register, before) is not { } definition) return null;
                uint candidate = definition.Word;
                int op = UniversalMips.Op(candidate), rs = UniversalMips.Rs(candidate), rt = UniversalMips.Rt(candidate);
                if (op == 15) return (uint)UniversalMips.Imm(candidate) << 16;
                bool immediate = op is >= 8 and <= 14;
                bool alu = op == 0 && UniversalMips.Fn(candidate) is 0 or 2 or 3 or 4 or 6 or 7 or
                    0x20 or 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x2A or 0x2B;
                if (!immediate && !alu) return null;
                var state = new UniversalMipsState();
                state[rs] = Constant(rs, definition.Pc, depth - 1);
                if (alu) state[rt] = Constant(rt, definition.Pc, depth - 1);
                state.Apply(candidate);
                return state[register];
            }

            (uint Address, uint LoadPc)? Global(int register, uint before, int depth)
            {
                if (depth <= 0 || Definition(register, before) is not { } definition) return null;
                uint candidate = definition.Word;
                int op = UniversalMips.Op(candidate), rs = UniversalMips.Rs(candidate), rt = UniversalMips.Rt(candidate);
                if (op == 35 && Constant(rs, definition.Pc, depth - 1) is uint address)
                    return (unchecked(address + (uint)UniversalMips.SImm(candidate)), definition.Pc);
                if (op == 0 && UniversalMips.Fn(candidate) is 0x21 or 0x25 && (rs == 0 || rt == 0))
                {
                    var alias = Global(rs == 0 ? rt : rs, definition.Pc, depth - 1);
                    // Do not reinterpret a consumer in a load delay slot as a copy of the new value.
                    return alias is { } a && a.LoadPc + 8 <= definition.Pc ? a : null;
                }
                return null;
            }
        }
        return calls;
    }
}
