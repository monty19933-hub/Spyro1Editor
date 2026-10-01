using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalMobySmoke")]
[assembly: InternalsVisibleTo("Spyro.Editor.UniversalClassOwnershipSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>Instruction ownership checks for a source-scoped class namespace. Numeric immediates
/// are changed only at a proved constant spawn argument, never by a global search/replace.</summary>
public static class UniversalClassRemapping
{
    public sealed record SpawnArgument(uint CallPc, uint ConstantPc, int ClassId);

    public static IReadOnlyList<SpawnArgument> ConstantSpawns(UniversalOverlay overlay, IReadOnlySet<uint> instructions)
    {
        List<SpawnArgument> result = [];
        foreach (uint pc in instructions.Order())
        {
            uint word = overlay.Word(pc);
            if (!UniversalMips.IsJalr(word)) continue;
            int targetRegister = UniversalMips.Rs(word);
            uint? loadPc = null;
            for (uint q = pc - 4; q + 28 > pc && instructions.Contains(q); q -= 4)
            {
                uint w = overlay.Word(q);
                if (Writes(w, targetRegister)) { if (UniversalMips.Op(w) == 35) loadPc = q; break; }
                if (UniversalMips.IsBranch(w)) break;
            }
            if (loadPc is not uint lp) continue;
            uint load = overlay.Word(lp); int baseRegister = UniversalMips.Rs(load);
            uint? global = null;
            for (uint q = lp - 4; q + 28 > lp && instructions.Contains(q); q -= 4)
            {
                uint w = overlay.Word(q);
                if (Writes(w, baseRegister))
                {
                    if (UniversalMips.Op(w) == 15)
                        global = unchecked((uint)((UniversalMips.Imm(w) << 16) + UniversalMips.SImm(load)));
                    break;
                }
                if (UniversalMips.IsBranch(w)) break;
            }
            if (global != UniversalExeBindings.InitGlobal) continue;
            uint? constantPc = null;
            for (uint q = pc + 4; q + 52 > pc && instructions.Contains(q); q -= 4)
            {
                uint w = overlay.Word(q);
                if (Writes(w, 4))
                {
                    if (UniversalMips.Op(w) is 9 or 13 && UniversalMips.Rs(w) == 0) constantPc = q;
                    break;
                }
                if (q < pc && UniversalMips.IsBranch(w)) break;
            }
            if (constantPc is not uint cp)
                throw new InvalidDataException($"Source class remapping cannot prove dynamic child-spawn argument at 0x{pc:X8}.");
            int c = UniversalMips.Imm(overlay.Word(cp));
            if (c is <= 0 or >= 0x200) throw new InvalidDataException($"Invalid child class at 0x{cp:X8}.");
            result.Add(new(pc, cp, c));
        }
        return result;
    }

    internal static bool Writes(uint w, int register)
    {
        if (UniversalMips.IsStore(w) || UniversalMips.IsBranch(w)) return false;
        return UniversalMips.Op(w) == 0 ? UniversalMips.Rd(w) == register : UniversalMips.Rt(w) == register;
    }

    internal static void PatchConstantSpawns(UniversalAsmPart part, UniversalOverlay source, IReadOnlyDictionary<int, int> map)
    {
        if (!map.Any(k => k.Key != k.Value)) return;
        var insns = part.Items.Where(i => i.Orig != null || i.SourcePc != null).Select(i => (i.Orig ?? i.SourcePc)!.Value).ToHashSet();
        var owned = ConstantSpawns(source, insns);
        var reported = UniversalDispatchAnalysis.Dependencies(source, new SortedSet<uint>(insns)).SpawnedClasses;
        foreach (int dependency in reported.Where(c => map.TryGetValue(c, out int mapped) && mapped != c))
            if (!owned.Any(s => s.ClassId == dependency))
                throw new InvalidDataException($"Source class remapping has no proved instruction owner for child 0x{dependency:X3}.");
        foreach (var spawn in owned)
        {
            if (!map.TryGetValue(spawn.ClassId, out int mapped))
                throw new InvalidDataException($"Child class 0x{spawn.ClassId:X3} lacks a source-scoped mapping.");
            if (mapped == spawn.ClassId) continue;
            foreach (int index in Enumerable.Range(0, part.Items.Count).Where(i => (part.Items[i].Orig ?? part.Items[i].SourcePc) == spawn.ConstantPc))
            {
                var item = part.Items[index];
                part.Items[index] = item with { Word = UniversalMips.WithImm(item.Word!.Value, mapped) };
            }
        }
    }

    internal static void PatchInitClassStore(UniversalAsmPart part, UniversalOverlay source, int sourceClass, int destinationClass)
    {
        if (sourceClass == destinationClass) return;
        // Native constructors copy a0 to s0, call the engine allocator, copy v0 to s3,
        // then store s0 into the new Moby's class field. Require that exact ownership chain.
        uint f = source.Init;
        bool savedClass = false, allocatedMoby = false, allocatorCalled = false;
        foreach (var item in part.Items.Where(i => i.Orig >= f && i.Orig < f + 0x80))
        {
            uint w = source.Word(item.Orig!.Value);
            savedClass |= w == 0x00808021; // addu s0,a0,zero
            allocatorCalled |= w == UniversalMips.EncodeJ(3, 0x800524C4);
            allocatedMoby |= allocatorCalled && w == 0x00409821; // addu s3,v0,zero
        }
        if (!savedClass || !allocatedMoby || !allocatorCalled)
            throw new InvalidDataException("Source initializer has no proved class/allocated-Moby ownership chain.");
        var stores = part.Items.Where(i => i.Word is uint w && UniversalMips.Op(w) == 41 && UniversalMips.SImm(w) == 0x36).ToList();
        if (stores.Count != 1 || stores[0].Word != 0xA6700036)
            throw new InvalidDataException("Source initializer class stores differ from the proved native constructor contract.");
        if (part.Items.Any(i => i.Word is uint native && Reads(native, 25)))
            throw new InvalidDataException("Source initializer owns a live t9 input; no safe class-store scratch register was proved.");
        int store = part.Items.FindIndex(i => i.Orig == stores[0].Orig);
        int before = store;
        if (store > 0 && part.Items[store - 1].Word is uint previous && UniversalMips.IsBranch(previous)) before--;
        // t9 is caller-saved. The translation occupies the old instruction entry so any
        // incoming branch also performs it. Keep the native store in its original delay slot.
        var entry = part.Items[before];
        if (before != store && entry.Word is uint branch && (UniversalMips.Rs(branch) == 25 || UniversalMips.Rt(branch) == 25))
            throw new InvalidDataException("Native class-store delay branch owns t9; no safe scratch register was proved.");
        part.Items[before] = entry with { Orig = null };
        part.Items.Insert(before, new(UniversalMips.EncodeI(9, 0, 25, destinationClass), default, entry.Orig, null));
        store++;
        var it = part.Items[store];
        part.Items[store] = it with { Word = UniversalMips.EncodeI(41, 19, 25, 0x36) };
    }

    private static bool Reads(uint w, int register)
    {
        int op = UniversalMips.Op(w);
        if (w == 0 || op is 2 or 3 or 15) return false;
        if (op == 0) return UniversalMips.Rs(w) == register || UniversalMips.Rt(w) == register;
        return UniversalMips.Rs(w) == register || (UniversalMips.IsStore(w) || op is 4 or 5) && UniversalMips.Rt(w) == register;
    }

    /// <summary>A class field used as the native 512-entry model-table index must retain the
    /// destination ID. This exact dataflow ends by overwriting the loaded class register.</summary>
    public static bool FeedsModelTable(UniversalOverlay source, uint pc)
    {
        uint load = source.Word(pc); int r = UniversalMips.Rt(load);
        if (UniversalMips.Op(load) is not (33 or 37) || UniversalMips.SImm(load) != 0x36 || r == 0) return false;
        // A class read in a control-transfer delay slot has another possible successor;
        // the following straight-line lookup alone cannot prove its complete use.
        if (source.Contains(pc - 4) && UniversalMips.IsBranch(source.Word(pc - 4))) return false;
        // The R3000 class load is not visible to its immediately following instruction.
        if (!source.Contains(pc + 4) || Reads(source.Word(pc + 4), r)) return false;
        Dictionary<int, uint> bases = []; HashSet<int> indices = []; HashSet<int> addresses = [];
        bool shifted = false;
        for (uint q = pc + 4; q <= pc + 40 && source.Contains(q); q += 4)
        {
            uint w = source.Word(q); int op = UniversalMips.Op(w), rs = UniversalMips.Rs(w), rt = UniversalMips.Rt(w), rd = UniversalMips.Rd(w);
            if (UniversalMips.IsBranch(w)) return false;
            if (op == 0 && UniversalMips.Fn(w) == 0 && UniversalMips.Rt(w) == r && UniversalMips.Rd(w) == r && UniversalMips.Sh(w) == 2)
            { if (shifted) return false; indices.Add(r); shifted = true; continue; }
            if (!shifted && Reads(w, r)) return false;
            if (op == 15) { if (rt == r || rt == 0) return false; addresses.Remove(rt); indices.Remove(rt); bases[rt] = (uint)UniversalMips.Imm(w) << 16; continue; }
            // Some native overlays materialize the complete model-table address before
            // adding the shifted class, rather than putting its low half in the final lw.
            // Carry only a literal base: an indexed address or class alias cannot qualify.
            if (op == 9 && bases.TryGetValue(rs, out uint literal) && !addresses.Contains(rs) && !indices.Contains(rs))
            {
                if (rt == r || rt == 0) return false;
                bases[rt] = unchecked(literal + (uint)UniversalMips.SImm(w));
                addresses.Remove(rt); indices.Remove(rt); continue;
            }
            if (op == 0 && UniversalMips.Fn(w) == 33 &&
                (indices.Contains(rs) && !addresses.Contains(rs) && bases.ContainsKey(rt) && !indices.Contains(rt) && !addresses.Contains(rt) && (rd == r || rd == rt) ||
                 indices.Contains(rt) && !addresses.Contains(rt) && bases.ContainsKey(rs) && !indices.Contains(rs) && !addresses.Contains(rs) && (rd == r || rd == rs)))
            {
                uint basis = bases.TryGetValue(rs, out uint a) ? a : bases[rt]; bases[rd] = basis; addresses.Add(rd); continue;
            }
            if (op == 35 && addresses.Contains(rs) && bases.TryGetValue(rs, out uint baseAddress) && unchecked(baseAddress + (uint)UniversalMips.SImm(w)) == 0x80076378 && rt == r)
                return true;
            if (Reads(w, r) || Writes(w, r) || addresses.Any(a => Reads(w, a))) return false;
            int written = op == 0 ? rd : rt;
            if (Writes(w, written)) { bases.Remove(written); indices.Remove(written); addresses.Remove(written); }
        }
        return false;
    }

    private static Dictionary<int, uint> PrecedingConstants(UniversalOverlay source, HashSet<uint> instructions, uint start)
    {
        // A value defined immediately before the field read may remain live across it.
        // Stop at any alternative entry or control transfer; only literal assignments
        // on the unique fall-through path qualify, including a preceding delay slot.
        var targets = instructions.Where(pc => UniversalMips.IsBranch(source.Word(pc)) &&
            !UniversalMips.IsJr(source.Word(pc)) && !UniversalMips.IsJalr(source.Word(pc)))
            .Select(pc => UniversalMips.BranchTarget(source.Word(pc), pc)).ToHashSet();
        Dictionary<int, uint> result = new() { [0] = 0 }; HashSet<int> assigned = [];
        for (uint pc = start - 4; instructions.Contains(pc) && pc + 52 >= start; pc -= 4)
        {
            uint w = source.Word(pc);
            if (UniversalMips.IsBranch(w)) break;
            for (int r = 1; r < 32; r++)
                if (Writes(w, r) && assigned.Add(r))
                {
                    if (UniversalMips.Op(w) == 9 && UniversalMips.Rs(w) == 0) result[r] = unchecked((uint)UniversalMips.SImm(w));
                    if (UniversalMips.Op(w) == 13 && UniversalMips.Rs(w) == 0) result[r] = (uint)UniversalMips.Imm(w);
                    if (UniversalMips.Op(w) == 15) result[r] = (uint)UniversalMips.Imm(w) << 16;
                }
            if (targets.Contains(pc)) break;
        }
        return result;
    }

    private static readonly Dictionary<string, bool> ComparisonCache = [];
    internal static bool ComparisonOnly(UniversalOverlay source, HashSet<uint> instructions, uint start, int register, IReadOnlyList<int> classes)
    {
        // Coverage repeatedly asks the same source-owned proof for different destinations.
        // Content keys retain no overlays/level clones and invalidate on any source mutation.
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        hash.AppendData(source.Bytes);
        foreach (uint pc in instructions.Order()) hash.AppendData(BitConverter.GetBytes(pc));
        hash.AppendData(BitConverter.GetBytes(start)); hash.AppendData(BitConverter.GetBytes(register));
        foreach (int c in classes.Order()) hash.AppendData(BitConverter.GetBytes(c));
        string key = Convert.ToHexStringLower(hash.GetHashAndReset());
        lock (ComparisonCache) if (ComparisonCache.TryGetValue(key, out bool hit)) return hit;
        bool result = ComparisonOnlyCore(source, instructions, start, register, classes);
        lock (ComparisonCache)
        { if (ComparisonCache.Count >= 8192) ComparisonCache.Clear(); ComparisonCache[key] = result; }
        return result;
    }

    private static bool ComparisonOnlyCore(UniversalOverlay source, HashSet<uint> instructions, uint start, int register, IReadOnlyList<int> classes)
    {
        Stack<(uint Pc, uint Taint, Dictionary<int, uint> State)> work = new();
        foreach (int c in classes)
        { var seed = PrecedingConstants(source, instructions, start); seed[register] = (uint)c; work.Push((start, 1u << register, seed)); }
        HashSet<string> seen = []; int budget = 20000;
        bool Apply(uint w, ref uint taint)
        {
            int op = UniversalMips.Op(w), rs = UniversalMips.Rs(w), rt = UniversalMips.Rt(w), rd = UniversalMips.Rd(w);
            uint taintCopy = taint;
            bool A(int r) => (taintCopy & (1u << r)) != 0;
            bool read = Enumerable.Range(1, 31).Any(r => A(r) && Reads(w, r));
            int written = op == 0 ? rd : rt;
            if (UniversalMips.IsBranch(w)) return true;
            if (read)
            {
                if (op is 10 or 11 || op == 0 && UniversalMips.Fn(w) is 42 or 43)
                    taint &= ~(1u << written); // comparison result is a Boolean
                else if (op is 9 or 12 or 13 or 14 || op == 0 && UniversalMips.Fn(w) is 32 or 33 && (rs == 0 || rt == 0))
                    taint |= 1u << written;
                else return false; // no class value can reach a pointer, store, shift or engine argument
            }
            else if (Writes(w, written)) taint &= ~(1u << written);
            taint &= ~1u; return true;
        }
        while (work.Count > 0 && budget-- > 0)
        {
            var (pc, taint, initial) = work.Pop();
            var state = new UniversalMipsState(initial);
            while (taint != 0 && instructions.Contains(pc) && seen.Add($"{pc:X8}:{taint:X8}:" + string.Join(",", state.Snapshot().OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"))))
            {
                uint w = source.Word(pc);
                if (!UniversalMips.IsBranch(w)) { if (!Apply(w, ref taint)) return false; state.Apply(w); pc += 4; continue; }
                if (UniversalMips.Op(w) is 4 or 5)
                {
                    int rs = UniversalMips.Rs(w), rt = UniversalMips.Rt(w);
                    if ((taint & (1u << rs)) != 0 && (taint & (1u << rt)) == 0 && state[rt] == null ||
                        (taint & (1u << rt)) != 0 && (taint & (1u << rs)) == 0 && state[rs] == null) return false;
                }
                bool? condition = state.Condition(w);
                uint delay = source.Word(pc + 4);
                if (UniversalMips.IsJal(w) || UniversalMips.IsJalr(w))
                {
                    if (!Apply(delay, ref taint) || (taint & (0xF0u | (UniversalMips.IsJalr(w) ? 1u << UniversalMips.Rs(w) : 0))) != 0) return false;
                    state.Apply(delay);
                    foreach (int r in new[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,24,25,31 }) { taint &= ~(1u << r); state[r] = null; }
                    pc += 8; continue;
                }
                if (UniversalMips.IsJr(w)) { if (!Apply(delay, ref taint)) return false; break; }
                if (!Apply(delay, ref taint)) return false;
                state.Apply(delay);
                uint target = UniversalMips.BranchTarget(w, pc);
                if (condition != false) work.Push((target, taint, state.Snapshot()));
                if (UniversalMips.IsUnconditionalBranch(w) || condition == true) break;
                pc += 8;
            }
        }
        return budget > 0;
    }

    internal static void PatchOwnedClassReads(UniversalAsmPart part, UniversalOverlay source, IReadOnlyDictionary<int, int> map, IReadOnlyList<int> classes)
    {
        var remaps = map.Where(k => k.Key != k.Value).ToArray();
        if (remaps.Length == 0) return;
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(source);
        uint classRead = sw.TreeStart;
        while (classRead > sw.Function)
        {
            classRead -= 4; uint w = source.Word(classRead);
            if (UniversalMips.Op(w) is 33 or 37 && UniversalMips.SImm(w) == 0x36 && UniversalMips.Rt(w) == sw.ClassRegister) break;
        }
        int actorRegister = UniversalMips.Rs(source.Word(classRead));
        PatchClassificationReads(part, source, map, classes, actorRegister, sw.TreeStart, "current_class");
    }

    internal static IReadOnlyList<uint> PatchInitAllocatedClassReads(UniversalAsmPart part, UniversalOverlay source, IReadOnlyDictionary<int, int> map, int sourceClass)
    {
        if (!map.Any(k => k.Key != k.Value)) return [];
        var allocation = part.Items.FirstOrDefault(i => i.Orig >= source.Init && i.Orig < source.Init + 0x80 &&
            i.Word == UniversalMips.EncodeJ(3, 0x800524C4));
        var saved = part.Items.FirstOrDefault(i => i.Orig > allocation?.Orig && i.Orig < source.Init + 0x80 && i.Word == 0x00409821);
        if (saved?.Orig is not uint savedPc) return [];
        // s3 is the proved newly allocated Moby. Constructor setup may reread its
        // class for source-specific flags after the mapped class store was installed.
        return PatchClassificationReads(part, source, map, [sourceClass], 19, savedPc + 4, "allocated_class");
    }

    internal static IReadOnlyList<uint> PatchInitParentClassReads(UniversalAsmPart part, UniversalOverlay source, IReadOnlyDictionary<int, int> map)
    {
        if (!map.Any(k => k.Key != k.Value)) return [];
        // Native Moby initializer a1 is an optional existing Moby/row descriptor. The
        // allocated-Moby ownership chain is separately checked by PatchInitClassStore.
        // Admit only its direct, unique save in a callee-saved register before allocation.
        var saves = part.Items.Where(i => i.Orig >= source.Init && i.Orig < source.Init + 0x80 &&
            i.Word is uint w && UniversalMips.Op(w) == 0 && UniversalMips.Fn(w) is 32 or 33 &&
            (UniversalMips.Rs(w) == 5 && UniversalMips.Rt(w) == 0 || UniversalMips.Rs(w) == 0 && UniversalMips.Rt(w) == 5) &&
            UniversalMips.Rd(w) is >= 17 and <= 23 && UniversalMips.Rd(w) != 19).ToList();
        if (saves.Count != 1) return [];
        uint savedPc = saves[0].Orig!.Value;
        var allocation = part.Items.FirstOrDefault(i => i.Orig >= source.Init && i.Orig < source.Init + 0x80 &&
            i.Word == UniversalMips.EncodeJ(3, 0x800524C4));
        if (allocation?.Orig is not uint allocationPc || savedPc >= allocationPc) return [];
        int parentRegister = UniversalMips.Rd(saves[0].Word!.Value);
        // Only translated namespace classes need proof: unrelated destination IDs remain
        // unchanged by the inverse map. Pure comparisons preserve all live pointer registers.
        return PatchClassificationReads(part, source, map, map.Where(m => m.Key != m.Value).Select(m => m.Key).ToArray(),
            parentRegister, savedPc + 4, "parent_class");
    }

    private static IReadOnlyList<uint> PatchClassificationReads(UniversalAsmPart part, UniversalOverlay source,
        IReadOnlyDictionary<int, int> map, IReadOnlyList<int> classes, int actorRegister, uint fromPc, string role)
    {
        var remaps = map.Where(k => k.Key != k.Value).ToArray();
        var instructions = part.Items.Where(i => i.Orig != null || i.SourcePc != null).Select(i => (i.Orig ?? i.SourcePc)!.Value).ToHashSet();
        var candidates = part.Items.Select((item, i) => (Item: item, Index: i)).Where(x => x.Item.Word is uint w &&
            UniversalMips.Op(w) is 33 or 37 && UniversalMips.SImm(w) == 0x36 && (x.Item.Orig ?? x.Item.SourcePc) >= fromPc).ToList();
        uint lastRead = candidates.Select(c => c.Item.Orig ?? c.Item.SourcePc ?? 0).DefaultIfEmpty().Max();
        bool stableActor = !instructions.Any(pc => pc >= fromPc && pc <= lastRead && Writes(source.Word(pc), actorRegister));
        List<uint> sites = [];
        foreach (var candidate in candidates.OrderByDescending(c => c.Index))
        {
            var item = candidate.Item; uint pc = (item.Orig ?? item.SourcePc)!.Value, w = item.Word!.Value;
            if (FeedsModelTable(source, pc))
            { part.Items[candidate.Index] = item with { ClassReadOwned = true }; continue; }
            int target = UniversalMips.Rt(w);
            if (!stableActor || UniversalMips.Rs(w) != actorRegister || Reads(source.Word(pc + 4), target) ||
                UniversalMips.IsBranch(source.Word(pc - 4)) || !ComparisonOnly(source, instructions, pc + 4, target, classes)) continue;
            // Pure current-actor classification uses the source namespace. Translate in the
            // already overwritten destination register, preserving every other live register.
            UniversalAsmPart conversion = new(); conversion.Emit(UniversalMips.Nop);
            string tag = $"{part.Items.FirstOrDefault(i => i.Label != null)?.Label}_{role}_{candidate.Index}_{pc:X8}";
            for (int n = 0; n < remaps.Length; n++)
            {
                var m = remaps[n]; string next = $"{tag}_n{n}";
                conversion.Emit(UniversalMips.EncodeI(9, target, target, -m.Value));
                conversion.Emit(UniversalMips.EncodeI(5, target, 0, 0), new(UniversalFixKind.BranchLabel, Label: next)); conversion.Emit(0);
                conversion.Emit(UniversalMips.EncodeI(9, 0, target, m.Key));
                conversion.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: tag + "_done")); conversion.Emit(0);
                conversion.Label(next); conversion.Emit(UniversalMips.EncodeI(9, target, target, m.Value));
            }
            conversion.Label(tag + "_done");
            part.Items.InsertRange(candidate.Index + 1, conversion.Items);
            // The remaining access was proved/translated, so the final guard does not treat
            // it as an unowned source class read. Source metadata remains in the patch report.
            part.Items[candidate.Index] = item with { ClassReadOwned = true }; sites.Add(pc);
        }
        return sites;
    }

    internal static void RequireNoUnownedClassMemory(UniversalAsmPart part, bool init, UniversalOverlay? overlay = null)
    {
        var sw = !init && overlay != null ? UniversalDispatchAnalysis.FindUpdateSwitch(overlay) : null;
        foreach (var item in part.Items)
        {
            if (item.ClassReadOwned) continue;
            if (item.Word is not uint w || UniversalMips.SImm(w) != 0x36) continue;
            if (sw != null && (item.Orig ?? item.SourcePc) < sw.TreeStart && UniversalMips.Op(w) is 33 or 37 && UniversalMips.Rt(w) == sw.ClassRegister) continue;
            uint? sourcePc = item.Orig ?? item.SourcePc;
            if (!init && overlay != null && sourcePc is uint lookup && FeedsModelTable(overlay, lookup)) continue;
            if (UniversalMips.IsLoad(w) || UniversalMips.IsStore(w))
                if (!(init && UniversalMips.Op(w) == 41 && UniversalMips.Rs(w) == 19 && UniversalMips.Rt(w) is 16 or 25))
                    throw new InvalidDataException($"Source class remapping needs ownership for class-field access at 0x{(item.Orig ?? item.SourcePc):X8}.");
        }
    }
}
