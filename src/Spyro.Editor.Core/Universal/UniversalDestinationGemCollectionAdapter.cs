using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalDestinationGemCall(uint CallPc, uint DelayWord, string ActorRole, uint Adapter);

/// <summary>Destination companion behavior can collect an imported, source-scoped gem through
/// its actor link. Redirect the certified native calls too; fading particles must receive the
/// canonical native color, while model and persistence identities remain source scoped.</summary>
internal static class UniversalDestinationGemCollectionAdapter
{
    internal sealed record OwnedCall(uint Pc, uint Delay, string Role);
    private static readonly Dictionary<string, IReadOnlyList<OwnedCall>> Cache = [];

    internal static IReadOnlyList<OwnedCall> Analyze(UniversalOverlay owner)
    {
        string key = Convert.ToHexStringLower(SHA256.HashData(owner.Bytes)) + owner.Update.ToString("X8");
        lock (Cache) if (Cache.TryGetValue(key, out var hit)) return hit;
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(owner);
        uint load = owner.Word(sw.TreeStart - 4);
        if (UniversalMips.Op(load) is not (33 or 37) || UniversalMips.Rt(load) != sw.ClassRegister ||
            UniversalMips.SImm(load) != 0x36)
            throw new InvalidDataException("Destination collection lacks a native current-actor class-load owner.");
        int actor = UniversalMips.Rs(load);
        var (code, tables) = UniversalDispatchAnalysis.Closure(owner, sw.Function, [sw.Function], new HashSet<uint>());
        var calls = ProveCalls(owner, code, tables, actor);
        // Cache only immutable-byte identities and proof records, never level/overlay objects.
        lock (Cache) { if (Cache.Count >= 64) Cache.Remove(Cache.Keys.First()); Cache[key] = calls; }
        return calls;
    }

    internal static IReadOnlyList<OwnedCall> ProveCalls(UniversalOverlay owner, IReadOnlySet<uint> code,
        IReadOnlyList<UniversalJumpTable> tables, int actor)
    {
        if (actor is < 16 or > 23)
            throw new InvalidDataException("Destination collection needs a native callee-saved current-actor register.");
        Dictionary<uint, HashSet<uint>> predecessors = [];
        void Edge(uint from, uint to)
        {
            if (!code.Contains(to)) return;
            if (!predecessors.TryGetValue(to, out var set)) predecessors[to] = set = [];
            set.Add(from);
        }
        foreach (uint pc in code)
        {
            uint word = owner.Word(pc);
            if (pc >= 4 && code.Contains(pc - 4) && UniversalMips.IsBranch(owner.Word(pc - 4))) continue;
            if (!UniversalMips.IsBranch(word)) { Edge(pc, pc + 4); continue; }
            if (!code.Contains(pc + 4) || UniversalMips.IsBranch(owner.Word(pc + 4)))
                throw new InvalidDataException("Destination collection has an ambiguous branch delay owner.");
            Edge(pc, pc + 4);
            if (UniversalMips.IsJal(word) || UniversalMips.IsJalr(word)) Edge(pc + 4, pc + 8);
            else if (UniversalMips.IsJr(word))
            {
                if (UniversalMips.Rs(word) == 31) continue;
                var table = tables.SingleOrDefault(t => t.JumpRegisterPc == pc) ??
                    throw new InvalidDataException("Destination collection has an unresolved indirect branch owner.");
                foreach (uint target in table.Targets) Edge(pc + 4, target);
            }
            else
            {
                Edge(pc + 4, UniversalMips.BranchTarget(word, pc));
                if (!UniversalMips.IsUnconditionalBranch(word)) Edge(pc + 4, pc + 8);
            }
        }
        bool PropertyRegister(uint before, int register)
        {
            // Calls preserve native s-registers. Volatile input/loaded-pointer registers do not
            // establish a linked-actor owner across an intervening call.
            if (register is < 16 or > 23) return false;
            HashSet<uint> seen = [];
            Stack<uint> pending = new(); pending.Push(before);
            bool defined = false;
            while (pending.Count != 0)
            {
                uint pc = pending.Pop();
                if (!seen.Add(pc)) continue;
                uint word = owner.Word(pc);
                if (Writes(word, register))
                {
                    if (pc == before || UniversalMips.Op(word) != 35 || UniversalMips.Rs(word) != actor || UniversalMips.SImm(word) != 0)
                        return false;
                    defined = true; continue;
                }
                if (!predecessors.TryGetValue(pc, out var incoming) || incoming.Count == 0) return false;
                foreach (uint p in incoming) pending.Push(p);
            }
            return defined;
        }
        List<OwnedCall> calls = [];
        foreach (uint pc in code.Order())
        {
            if (owner.Word(pc) != UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem)) continue;
            uint delay = owner.Word(pc + 4);
            string? role = null;
            bool MoveActor(uint w) => UniversalMips.Op(w) == 0 && UniversalMips.Fn(w) is 0x21 or 0x25 &&
                UniversalMips.Rd(w) == 4 && ((UniversalMips.Rs(w) == actor && UniversalMips.Rt(w) == 0) ||
                (UniversalMips.Rt(w) == actor && UniversalMips.Rs(w) == 0));
            if (MoveActor(delay)) role = "current-native-actor";
            else if (!Writes(delay, 4) && code.Contains(pc - 4) && predecessors.TryGetValue(pc, out var incoming) &&
                incoming.SetEquals([pc - 4]))
            {
                uint argument = owner.Word(pc - 4);
                if (MoveActor(argument)) role = "current-native-actor";
                else if (UniversalMips.Op(argument) == 35 && UniversalMips.Rt(argument) == 4 &&
                    UniversalMips.SImm(argument) == 0 && PropertyRegister(pc - 8, UniversalMips.Rs(argument)))
                    role = "native-actor-property-linked-actor";
            }
            if (role == null || (code.Contains(pc - 4) && UniversalMips.IsBranch(owner.Word(pc - 4))))
                throw new InvalidDataException($"Destination gem collection call at 0x{pc:X8} lacks a proved native actor argument.");
            calls.Add(new(pc, delay, role));
        }
        return calls;
    }

    internal static void RequireCurrentOwner(UniversalOverlay current, UniversalOverlay source, IEnumerable<OwnedCall> calls)
    {
        // Authenticate the full native function, not merely its JAL word. Appended wrappers may
        // change the current update binding; their retained native target remains independently owned.
        uint from = source.Update, end = source.FunctionEnd(from);
        if (!current.Contains(from) || !current.Contains(end - 4) ||
            !current.Bytes.Slice((int)(from - UniversalOverlay.Base), (int)(end - from))
                .SequenceEqual(source.Bytes.Slice((int)(from - UniversalOverlay.Base), (int)(end - from))))
            throw new InvalidDataException("Destination gem collection native update differs from its original source owner.");
        foreach (var call in calls)
            if (current.Word(call.Pc) != UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem) ||
                current.Word(call.Pc + 4) != call.Delay)
                throw new InvalidDataException("Destination gem collection call/delay differs from its certified source owner.");
        var (_, tables) = UniversalDispatchAnalysis.Closure(source, source.Update, [source.Update], new HashSet<uint>());
        foreach (var table in tables)
            for (int i = 0; i < table.Targets.Count; i++)
            {
                uint pc = table.TableAddress + (uint)i * 4;
                if (!current.Contains(pc) || current.Word(pc) != source.Word(pc))
                    throw new InvalidDataException("Destination gem collection dispatch table differs from its original source owner.");
            }
    }

    internal static void Apply(byte[] overlay, IReadOnlyList<UniversalDestinationGemCall> calls)
    {
        foreach (var call in calls)
        {
            int offset = checked((int)(call.CallPc - UniversalOverlay.Base));
            if (BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset)) != UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem) ||
                BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset + 4)) != call.DelayWord)
                throw new InvalidDataException("Destination gem collection changed before final overlay publication.");
        }
        foreach (var call in calls)
            BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan((int)(call.CallPc - UniversalOverlay.Base)), UniversalMips.EncodeJ(3, call.Adapter));
    }

    private static bool Writes(uint w, int register)
    {
        int op = UniversalMips.Op(w);
        if (op == 0) return UniversalMips.Fn(w) is not (8 or 0x18 or 0x19 or 0x1A or 0x1B) && UniversalMips.Rd(w) == register;
        return op == 3 ? register == 31 : (op is >= 8 and <= 15 or >= 32 and <= 38) && UniversalMips.Rt(w) == register;
    }
}
