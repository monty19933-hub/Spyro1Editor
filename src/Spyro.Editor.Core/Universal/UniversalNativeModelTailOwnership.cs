using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalModelTailSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>Own the native second model lookup whose address survives in v1 until the
/// animation helper. Keep the destination class; authenticate the helper path that kills
/// the residual address before reading it. This proof adds no emitted instructions.</summary>
internal static class UniversalNativeModelTailOwnership
{
    internal const uint AnimationHelper = 0x80055A78;
    internal static void RequireHelper(byte[]? executable)
    {
        if (executable == null || executable.Length != 0x66000)
            throw new InvalidDataException("Native model lookup needs its complete USA executable owner.");
        foreach (var (address, length, hash) in new (uint, int, string)[]
        {
            (AnimationHelper, 0x94, "c29e385a72d3af75bc736f601d3fd9d9e10d44329638a3611ed31c9b2bc6f324"),
            (0x80056DC4, 8, "db11f83174925e06fb45c5f8b618fb1453ec73ddb8e12caffbb637febbfe8de4")
        })
            if (Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(address - UniversalExeBindings.ExeBase), length))) != hash)
                throw new InvalidDataException("Native model lookup animation helper differs from its certified source owner.");
        // a2=8 takes 55AC0 -> 55AFC, then calls 56DC4. Neither prefix reads v1;
        // 56DC8 replaces v1 with zero. No ABI-only caller-saved assumption is used.
    }

    internal static bool Prove(UniversalOverlay owner, uint pc, int actor,
        IReadOnlyDictionary<uint, HashSet<uint>> predecessors, ISet<uint>? witness = null)
    {
        if (actor is < 16 or > 23 || !owner.Contains(pc + 40)) return false;
        uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
        bool localBase = UniversalMips.Op(owner.Word(pc + 4)) == 15;
        int prefixLength = localBase ? 4 : 3;
        uint add = owner.Word(pc + (uint)(prefixLength + 1) * 4);
        int basis = UniversalMips.Rt(add);
        if (basis is not (6 or 9 or 10 or 11 or 12)) return false;
        uint[] prefix = localBase
            ? [I(33, actor, 3, 0x36), I(15, 0, basis, 0x8007), I(9, basis, basis, 0x6378), I(40, actor, 2, 0x52)]
            : [I(33, actor, 3, 0x36), I(9, 0, 2, 0xFF), I(40, actor, 2, 0x52)];
        bool delayedA2 = owner.Word(pc + (uint)(prefixLength + 3) * 4) == I(9, actor, 7, 0x54);
        uint[] expected = [.. prefix, 0x00031880, (uint)((3 << 21) | (basis << 16) | (3 << 11) | 0x21), I(35, 3, 2, 0),
            delayedA2 ? I(9, actor, 7, 0x54) : I(9, 0, 6, 8), I(36, 2, 4, 5), UniversalMips.EncodeJ(3, AnimationHelper),
            delayedA2 ? I(9, 0, 6, 8) : I(9, actor, 7, 0x54)];
        bool Linear(uint at, uint previous) => predecessors.TryGetValue(at, out var incoming) && incoming.SetEquals([previous]);
        for (int n = 0; n < expected.Length; n++)
        {
            if (owner.Word(pc + (uint)n * 4) != expected[n] || (n > 0 && !Linear(pc + (uint)n * 4, pc + (uint)(n - 1) * 4))) return false;
            witness?.Add(pc + (uint)n * 4);
        }
        if (owner.Contains(pc - 4) && UniversalMips.IsBranch(owner.Word(pc - 4))) return false;
        if (localBase)
        {
            if (!owner.Contains(pc - 4) || owner.Word(pc - 4) != I(9, 0, 2, 0xFF) || !Linear(pc, pc - 4)) return false;
            witness?.Add(pc - 4);
            return true;
        }

        // Prove every reaching definition of the base, including conditional predecessors.
        // The two-word literal definition must dominate its use; no call may carry it.
        HashSet<uint> seen = []; Stack<uint> pending = new();
        if (!predecessors.TryGetValue(pc, out var before) || before.Count == 0) return false;
        foreach (uint at in before) pending.Push(at);
        bool defined = false;
        while (pending.Count > 0)
        {
            uint at = pending.Pop();
            if (!seen.Add(at)) continue;
            witness?.Add(at);
            if (seen.Count > 128 || !owner.Contains(at)) return false;
            uint word = owner.Word(at);
            if (UniversalMips.IsJal(word) || UniversalMips.IsJalr(word)) return false;
            if (UniversalDestinationGemCollectionAdapter.Writes(word, basis))
            {
                if (word != I(9, basis, basis, 0x6378) || !owner.Contains(at - 8) ||
                    owner.Word(at - 4) != I(15, 0, basis, 0x8007) || !Linear(at, at - 4) ||
                    UniversalMips.IsBranch(owner.Word(at - 8))) return false;
                defined = true;
                witness?.Add(at - 4);
                continue;
            }
            if (!predecessors.TryGetValue(at, out var incoming) || incoming.Count == 0) return false;
            foreach (uint earlier in incoming) pending.Push(earlier);
        }
        return defined;
    }

    internal static IReadOnlyList<uint> MarkOwned(UniversalAsmPart part, UniversalOverlay owner, byte[]? executable)
    {
        var candidates = part.Items.Where(i => i.Word is uint w && UniversalMips.Op(w) == 33 &&
            UniversalMips.Rt(w) == 3 && UniversalMips.SImm(w) == 0x36 && !i.ClassReadOwned && (i.SourcePc ?? i.Orig) != null).ToArray();
        if (candidates.Length == 0) return [];
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(owner);
        uint dispatch = owner.Word(sw.TreeStart - 4);
        if (UniversalMips.Op(dispatch) is not (33 or 37) || UniversalMips.SImm(dispatch) != 0x36 || UniversalMips.Rt(dispatch) != sw.ClassRegister) return [];
        var (code, tables) = UniversalDispatchAnalysis.Closure(owner, sw.Function, [sw.Function], new HashSet<uint>());
        var predecessors = UniversalDestinationGemCollectionAdapter.Predecessors(owner, code, tables);
        List<uint> result = [];
        foreach (var candidate in candidates)
        {
            uint pc = (candidate.SourcePc ?? candidate.Orig)!.Value;
            HashSet<uint> witness = [];
            if (!Prove(owner, pc, UniversalMips.Rs(dispatch), predecessors, witness)) continue;
            // A proof of original bytes must not bless a later modified/pruned argument,
            // call or delay slot. Every native witness must survive in this exact part.
            bool intact = true;
            foreach (uint at in witness)
            {
                var matches = part.Items.Where(i => (i.SourcePc ?? i.Orig) == at).ToArray();
                // Class pruning can copy a straight-line prefix into a trace as well as
                // retaining its original body. Every occurrence must have the same owner.
                if (matches.Length == 0 || matches.Any(i => i.Word != owner.Word(at) ||
                    i.Fix.Kind is not (UniversalFixKind.None or UniversalFixKind.Branch or UniversalFixKind.Jump))) { intact = false; break; }
            }
            if (!intact) continue;
            RequireHelper(executable);
            for (int n = 0; n < part.Items.Count; n++)
                if ((part.Items[n].SourcePc ?? part.Items[n].Orig) == pc) part.Items[n] = part.Items[n] with { ClassReadOwned = true };
            result.Add(pc);
        }
        return result.Distinct().Order().ToArray();
    }
}
