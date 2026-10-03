namespace Spyro.Editor.Core.Universal;

/// <summary>One replayed instruction of a class-switch path. Either an original word or a
/// materialized constant load from the source overlay's static data (<see cref="ConstantRegister"/>).</summary>
public readonly record struct UniversalReplayed(uint Word, int ConstantRegister = -1, uint ConstantValue = 0, uint? SourcePc = null)
{
    public bool IsConstant => ConstantRegister >= 0;
}

public sealed record UniversalTrace(uint Stop, IReadOnlyList<UniversalReplayed> Sequence);

public sealed record UniversalJumpTable(uint JumpRegisterPc, uint LuiPc, uint LoadPc, uint TableAddress, IReadOnlyList<uint> Targets);

/// <summary>A class switch inside an overlay function: where the class is loaded and how it is compared.</summary>
public sealed record UniversalClassSwitch(uint Function, uint TreeStart, int ClassRegister,
    IReadOnlyDictionary<int, uint> Seeds, uint DefaultStop, int DistinctStops);

/// <summary>
/// Static analysis of the per-level Moby update/init functions. The update function is a single
/// loop over the active list with an inlined class switch; each class's behavior is the control-flow
/// closure reachable from its switch leaf.
/// </summary>
public static class UniversalDispatchAnalysis
{
    public const int SentinelClass = 0x7FFF;

    public static UniversalTrace Trace(UniversalOverlay o, uint start, int cls, IReadOnlyDictionary<int, uint> seeds,
        int classRegister, int maxSequence = 48)
    {
        UniversalMipsState s = new(seeds);
        s[classRegister] = (uint)cls;
        uint pc = start;
        List<UniversalReplayed> seq = [];
        HashSet<uint> visited = [];
        while (true)
        {
            if (!visited.Add(pc) || seq.Count > maxSequence || !o.Contains(pc)) return new(pc, seq);
            uint w = o.Word(pc);
            if (UniversalMips.IsBranch(w))
            {
                if (UniversalMips.IsJr(w) && UniversalMips.Rs(w) != 31 && s[UniversalMips.Rs(w)] is uint jt && o.Contains(jt))
                {
                    uint ds = o.Word(pc + 4);
                    if (UniversalMips.IsBranch(ds)) return new(pc, seq);
                    s.Apply(ds); seq.Add(new(ds, SourcePc: pc + 4)); pc = jt; continue;
                }
                if (UniversalMips.IsJal(w) || UniversalMips.IsJr(w) || UniversalMips.IsJalr(w)) return new(pc, seq);
                bool? c = s.Condition(w);
                if (c == null) return new(pc, seq);
                uint delay = o.Word(pc + 4);
                if (UniversalMips.IsBranch(delay)) return new(pc, seq);
                s.Apply(delay); seq.Add(new(delay, SourcePc: pc + 4));
                pc = c.Value ? UniversalMips.BranchTarget(w, pc) : pc + 8;
                continue;
            }
            if (UniversalMips.Op(w) == 35 && s[UniversalMips.Rs(w)] is uint b)
            {
                uint ad = (uint)(b + UniversalMips.SImm(w));
                if (o.Contains(ad) && ad < o.CodeStart)
                {
                    uint v = o.Word(ad); int rt = UniversalMips.Rt(w);
                    s[rt] = v; seq.Add(new(w, rt, v, pc)); pc += 4; continue;
                }
            }
            s.Apply(w); seq.Add(new(w, SourcePc: pc)); pc += 4;
        }
    }

    public static IReadOnlyDictionary<int, uint> SeedLinear(UniversalOverlay o, uint f, uint upto)
    {
        UniversalMipsState s = new();
        uint a = f;
        while (a < upto)
        {
            uint w = o.Word(a);
            if (UniversalMips.IsBranch(w))
            {
                if (UniversalMips.IsJal(w) || UniversalMips.IsJalr(w))
                    foreach (int r in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 24, 25, 31 }) s[r] = null;
                s.Apply(o.Word(a + 4)); a += 8; continue;
            }
            s.Apply(w); a += 4;
        }
        return s.Snapshot();
    }

    /// <summary>Locates the main class switch of the update function: the class load (LH rX,0x36(moby))
    /// whose traced leaves are most diverse.</summary>
    private static readonly Dictionary<string, UniversalClassSwitch> SwitchCache = [];

    public static UniversalClassSwitch FindUpdateSwitch(UniversalOverlay o)
    {
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(o.Bytes)) + o.Update.ToString("X8");
        lock (SwitchCache) if (SwitchCache.TryGetValue(key, out var hit)) return hit;
        var found = FindUpdateSwitchUncached(o);
        lock (SwitchCache) SwitchCache[key] = found;
        return found;
    }

    private static UniversalClassSwitch FindUpdateSwitchUncached(UniversalOverlay o)
    {
        uint f = o.Update, e = o.FunctionEnd(f);
        UniversalClassSwitch? best = null;
        for (uint a = f; a < e; a += 4)
        {
            uint w = o.Word(a);
            if (UniversalMips.Op(w) is not (0x21 or 0x25) || UniversalMips.Imm(w) != 0x36) continue;
            int creg = UniversalMips.Rt(w);
            uint ts = a + 4;
            var seeds = new Dictionary<int, uint>(SeedLinear(o, f, ts));
            seeds.Remove(creg);
            HashSet<uint> stops = [];
            for (int c = 0; c < 0x200; c++) stops.Add(Trace(o, ts, c, seeds, creg).Stop);
            if (best == null || stops.Count > best.DistinctStops)
                best = new(f, ts, creg, seeds, Trace(o, ts, SentinelClass, seeds, creg).Stop, stops.Count);
        }
        return best ?? throw new InvalidDataException("No class switch was found in the level update function.");
    }

    /// <summary>True when the class takes a behavior path distinct from the switch default: a different stop,
    /// or the same stop reached after different side effects (short inlined cases).</summary>
    public static bool Handles(UniversalOverlay o, UniversalClassSwitch sw, int cls)
    {
        var t = Trace(o, sw.TreeStart, cls, sw.Seeds, sw.ClassRegister);
        if (t.Stop != sw.DefaultStop) return true;
        var d = Trace(o, sw.TreeStart, SentinelClass, sw.Seeds, sw.ClassRegister);
        return !Effects(t.Sequence).SequenceEqual(Effects(d.Sequence));
    }

    private static IEnumerable<uint> Effects(IReadOnlyList<UniversalReplayed> seq) =>
        seq.Where(r => !r.IsConstant && !IsTreeScratch(r.Word)).Select(r => r.Word);

    private static bool IsTreeScratch(uint w)
    {
        int op = UniversalMips.Op(w);
        int dst = op == 0 ? UniversalMips.Rd(w) : UniversalMips.Rt(w);
        bool alu = op is >= 8 and <= 15 || (op == 0 && UniversalMips.Fn(w) is >= 0x20 and <= 0x2B);
        return w == 0 || (alu && dst is 1 or 2 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 24 or 25);
    }

    public static UniversalJumpTable? ResolveJumpTable(UniversalOverlay o, uint jr)
    {
        uint? lw = null, lui = null; int? bound = null;
        for (uint b = jr - 4; b + 40 > jr && b >= UniversalOverlay.Base; b -= 4)
        {
            uint w = o.Word(b);
            if (UniversalMips.Op(w) == 35 && lw == null) lw = b;
            if (UniversalMips.Op(w) == 15 && UniversalMips.Rt(w) == 1 && lw != null && lui == null) lui = b;
            if (UniversalMips.Op(w) == 11 && bound == null) bound = UniversalMips.Imm(w);
        }
        if (lw == null || lui == null || bound is null or <= 0 or > 1024) return null;
        uint table = (uint)((UniversalMips.Imm(o.Word(lui.Value)) << 16) + UniversalMips.SImm(o.Word(lw.Value)));
        if (!o.Contains(table)) return null;
        List<uint> targets = [];
        for (int i = 0; i < bound; i++) targets.Add(o.Word(table + (uint)(4 * i)));
        return new(jr, lui.Value, lw.Value, table, targets);
    }

    /// <summary>Control-flow closure inside function <paramref name="f"/>; <paramref name="stops"/> are not entered.</summary>
    public static (SortedSet<uint> Instructions, List<UniversalJumpTable> Tables) Closure(UniversalOverlay o, uint f,
        IEnumerable<uint> entries, ISet<uint> stops, UniversalDryCanyonBirdCertificate? birdBranches = null, UniversalMagicPairCertificate? magicPair = null)
    {
        birdBranches?.RequireOwner(o); magicPair?.RequireOwner(o);
        uint fe = o.FunctionEnd(f);
        SortedSet<uint> seen = [];
        Dictionary<uint, UniversalJumpTable> tables = [];
        Stack<uint> work = new(entries);
        while (work.Count > 0)
        {
            uint pc = work.Pop();
            while (true)
            {
                if (seen.Contains(pc) || stops.Contains(pc)) break;
                if (pc < f || pc >= fe) throw new InvalidDataException($"Behavior closure left its function at 0x{pc:X8}.");
                seen.Add(pc);
                uint w = o.Word(pc);
                if (UniversalMips.IsBranch(w))
                {
                    seen.Add(pc + 4);
                    if (UniversalMips.IsJal(w) || UniversalMips.IsJalr(w)) { pc += 8; continue; }
                    if (UniversalMips.IsJr(w))
                    {
                        if (UniversalMips.Rs(w) == 31) break;
                        var t = ResolveJumpTable(o, pc) ?? throw new InvalidDataException($"Unresolved indirect jump at 0x{pc:X8}.");
                        if(magicPair!=null)t=magicPair.Filter(o,t);
                        tables[pc] = t;
                        foreach (uint x in t.Targets) work.Push(x);
                        break;
                    }
                    uint target = UniversalMips.BranchTarget(w, pc);
                    if (birdBranches?.TryDecision(pc, out uint ownedTarget) == true) { pc = ownedTarget; continue; }
                    if (UniversalMips.IsUnconditionalBranch(w)) { pc = target; continue; }
                    work.Push(target); pc += 8; continue;
                }
                pc += 4;
            }
        }
        return (seen, tables.Values.ToList());
    }

    /// <summary>Returns LUI-based absolute references that resolve into the overlay (pointers needing relocation).</summary>
    public static List<(uint Lui, uint Use, uint Target)> OverlayReferences(UniversalOverlay o, SortedSet<uint> insns)
    {
        List<(uint, uint, uint)> refs = [];
        foreach (uint a in insns)
        {
            uint w = o.Word(a);
            if (UniversalMips.Op(w) != 15) continue;
            int r = UniversalMips.Rt(w); uint hi = (uint)UniversalMips.Imm(w) << 16;
            for (uint b = a + 4; b < a + 4 * 24; b += 4)
            {
                if (!insns.Contains(b)) break;
                uint w2 = o.Word(b);
                int op2 = UniversalMips.Op(w2), rs2 = UniversalMips.Rs(w2), rt2 = UniversalMips.Rt(w2);
                if ((op2 is 9 || UniversalMips.IsLoad(w2) || UniversalMips.IsStore(w2)) && rs2 == r)
                {
                    uint t = (uint)(hi + UniversalMips.SImm(w2));
                    if (o.Contains(t)) refs.Add((a, b, t));
                    if ((op2 == 9 || UniversalMips.IsLoad(w2)) && rt2 == r) break;
                    continue;
                }
                if (op2 == 13 && rs2 == r) { uint t = hi | (uint)UniversalMips.Imm(w2); if (o.Contains(t) && t < o.CodeStart) refs.Add((a, b, t)); break; }
                bool writes = (op2 is 9 or 15 or 13 or 12 or 10 or 11 || UniversalMips.IsLoad(w2)) ? rt2 == r
                    : op2 == 0 && UniversalMips.Rd(w2) == r && !UniversalMips.IsJr(w2) && !UniversalMips.IsJalr(w2);
                if (writes) break;
            }
        }
        return refs;
    }

    /// <summary>Child classes and particle types referenced by constant arguments before calls through
    /// the level spawn (0x800758CC) and particle (0x800758E4) globals.</summary>
    public static (SortedSet<int> SpawnedClasses, SortedSet<int> ParticleTypes) Dependencies(UniversalOverlay o, SortedSet<uint> insns)
    {
        SortedSet<int> spawns = [], particles = [];
        foreach (uint a in insns)
        {
            uint w = o.Word(a);
            if (!UniversalMips.IsJalr(w)) continue;
            int rs = UniversalMips.Rs(w); uint? global = null;
            for (uint b = a - 4; b + 24 > a && insns.Contains(b); b -= 4)
            {
                uint w2 = o.Word(b);
                if (UniversalMips.Op(w2) == 35 && UniversalMips.Rt(w2) == rs)
                {
                    for (uint c = b - 4; c + 24 > b && insns.Contains(c); c -= 4)
                    {
                        uint w3 = o.Word(c);
                        if (UniversalMips.Op(w3) == 15 && UniversalMips.Rt(w3) == UniversalMips.Rs(w2))
                        { global = (uint)((UniversalMips.Imm(w3) << 16) + UniversalMips.SImm(w2)); break; }
                    }
                    break;
                }
            }
            int? a0 = ConstantBefore(o, insns, a + 8, 4), a1 = ConstantBefore(o, insns, a + 8, 5);
            if (global == UniversalExeBindings.InitGlobal && a0 is int k && k is > 0 and < 0x200) spawns.Add(k);
            if (global == UniversalExeBindings.ParticleSpawnGlobal && a1 is int p && p is >= 0 and < 0x51) particles.Add(p);
        }
        foreach (var call in UniversalDependencyAnalysis.Calls(o, insns))
        {
            if (call.Global == UniversalExeBindings.InitGlobal && call.Argument0 is > 0 and < 0x200)
                spawns.Add((int)call.Argument0.Value);
            if (call.Global == UniversalExeBindings.ParticleSpawnGlobal && call.Argument1 is < 0x51)
                particles.Add((int)call.Argument1.Value);
        }
        return (spawns, particles);
    }

    private static int? ConstantBefore(UniversalOverlay o, SortedSet<uint> insns, uint at, int reg)
    {
        uint b = at;
        for (int i = 0; i < 12; i++)
        {
            b -= 4;
            if (!insns.Contains(b)) continue;
            uint w = o.Word(b); int op = UniversalMips.Op(w);
            if ((op is 9 or 13) && UniversalMips.Rt(w) == reg && UniversalMips.Rs(w) == 0) return op == 13 ? UniversalMips.Imm(w) : UniversalMips.SImm(w);
            bool writes = (op is 9 or 13 or 15 or 10 or 11 or 12 || UniversalMips.IsLoad(w)) ? UniversalMips.Rt(w) == reg
                : op == 0 && UniversalMips.Rd(w) == reg;
            if (writes) return null;
        }
        return null;
    }
}

/// <summary>Path-sensitive closure of a function for a fixed set of values of one argument register:
/// branches decided by the known value only keep their reachable side.</summary>
public static class UniversalSpecializer
{
    public sealed record Result(SortedSet<uint> Instructions, Dictionary<uint, (bool Taken, bool NotTaken)> Branches, List<UniversalJumpTable> Tables);

    private static readonly int[] CallerSaved = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 24, 25, 31];

    public static Result Specialize(UniversalOverlay o, uint f, int argRegister, IEnumerable<int> values)
    {
        uint fe = o.FunctionEnd(f);
        SortedSet<uint> seen = [];
        Dictionary<uint, (bool, bool)> branches = [];
        Dictionary<uint, UniversalJumpTable> tables = [];
        foreach (int value in values)
        {
            Dictionary<uint, Dictionary<int, uint>> stateAt = [];
            Stack<(uint Pc, Dictionary<int, uint> State)> work = new();
            work.Push((f, new Dictionary<int, uint> { [argRegister] = (uint)value, [0] = 0 }));
            int budget = 200000;
            while (work.Count > 0 && budget-- > 0)
            {
                var (start, st0) = work.Pop();
                if (stateAt.TryGetValue(start, out var prior))
                {
                    var joined = prior.Where(kv => st0.TryGetValue(kv.Key, out uint v) && v == kv.Value).ToDictionary(kv => kv.Key, kv => kv.Value);
                    if (joined.Count == prior.Count) continue; // no new information
                    stateAt[start] = joined; st0 = joined;
                }
                else stateAt[start] = st0;
                var s = new UniversalMipsState(st0);
                uint pc = start;
                while (true)
                {
                    if (pc < f || pc >= fe) throw new InvalidDataException($"Specialized closure left its function at 0x{pc:X8}.");
                    seen.Add(pc);
                    uint w = o.Word(pc);
                    if (!UniversalMips.IsBranch(w)) { s.Apply(w); pc += 4; continue; }
                    seen.Add(pc + 4);
                    uint ds = o.Word(pc + 4);
                    if (UniversalMips.IsJal(w) || UniversalMips.IsJalr(w))
                    {
                        s.Apply(ds); foreach (int r in CallerSaved) s[r] = null; pc += 8;
                        if (stateAt.ContainsKey(pc)) { work.Push((pc, s.Snapshot())); break; }
                        continue;
                    }
                    if (UniversalMips.IsJr(w))
                    {
                        if (UniversalMips.Rs(w) == 31) break;
                        var t = UniversalDispatchAnalysis.ResolveJumpTable(o, pc) ?? throw new InvalidDataException($"Unresolved indirect jump at 0x{pc:X8}.");
                        tables[pc] = t;
                        uint? idx = s[UniversalMips.Rs(w)];
                        s.Apply(ds);
                        IEnumerable<uint> targets = idx is uint known && o.Contains(known) ? [known] : t.Targets;
                        foreach (uint x in targets) work.Push((x, s.Snapshot()));
                        break;
                    }
                    bool? c = s.Condition(w);
                    // Evaluate the condition before the delay slot executes (R3000 semantics).
                    s.Apply(ds);
                    uint target = UniversalMips.BranchTarget(w, pc);
                    branches.TryGetValue(pc, out var seenOutcome);
                    if (UniversalMips.IsUnconditionalBranch(w) || c == true)
                    {
                        branches[pc] = (true, seenOutcome.Item2);
                        work.Push((target, s.Snapshot())); break;
                    }
                    if (c == false) { branches[pc] = (seenOutcome.Item1, true); pc += 8; if (stateAt.ContainsKey(pc)) { work.Push((pc, s.Snapshot())); break; } continue; }
                    branches[pc] = (true, true);
                    work.Push((target, s.Snapshot()));
                    pc += 8;
                    if (stateAt.ContainsKey(pc)) { work.Push((pc, s.Snapshot())); break; }
                }
            }
            if (budget <= 0) throw new InvalidDataException("Specialized closure exceeded its analysis budget.");
        }
        return new(seen, branches, tables.Values.ToList());
    }
}
