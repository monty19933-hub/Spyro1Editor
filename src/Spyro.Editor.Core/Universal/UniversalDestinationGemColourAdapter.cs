using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalDestinationGemColourLoad(uint PatchPc, uint OriginalWord, uint DelayWord, uint AddWord, uint LoadWord, uint LoadPc,
    int ClassRegister, string ActorRole, uint Adapter);

/// <summary>Native linked pickup behavior emits a sparkle before collection, independently of
/// CollectItem. Its class-indexed native color load needs the same source-scoped gem semantics.</summary>
internal static class UniversalDestinationGemColourAdapter
{
    internal sealed record OwnedLoad(uint Pc, uint PatchWord, uint Delay, uint LoadPc, uint AddWord, uint LoadWord, int ClassRegister, string Role);

    internal static IReadOnlyList<OwnedLoad> Analyze(UniversalOverlay owner)
    {
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(owner);
        int actor = UniversalMips.Rs(owner.Word(sw.TreeStart - 4));
        var (code, tables) = UniversalDispatchAnalysis.Closure(owner, sw.Function, [sw.Function], new HashSet<uint>());
        return ProveLoads(owner, code, tables, actor);
    }

    internal static IReadOnlyList<OwnedLoad> ProveLoads(UniversalOverlay owner, IReadOnlySet<uint> code,
        IReadOnlyList<UniversalJumpTable> tables, int actor)
    {
        var predecessors = UniversalDestinationGemCollectionAdapter.Predecessors(owner, code, tables);
        bool Linear(uint pc, uint previous) => predecessors.TryGetValue(pc, out var incoming) && incoming.SetEquals([previous]);
        bool ClassOwner(uint before, int register, out string role)
        {
            HashSet<uint> seen = []; Stack<uint> pending = new(); pending.Push(before); role = "";
            bool defined = false;
            while (pending.Count != 0)
            {
                uint pc = pending.Pop(); if (!seen.Add(pc)) continue;
                uint word = owner.Word(pc);
                if (UniversalDestinationGemCollectionAdapter.Writes(word, register))
                {
                    if (pc == before || pc + 4 == before || UniversalMips.Op(word) is not (33 or 37) || UniversalMips.SImm(word) != 0x36)
                        return false;
                    int pointer = UniversalMips.Rs(word);
                    if (pointer == actor) { defined = true; role = "current-native-actor-class"; continue; }
                    if (!code.Contains(pc - 8) || !Linear(pc, pc - 4) || !Linear(pc - 4, pc - 8) ||
                        UniversalMips.IsBranch(owner.Word(pc - 4)) || UniversalDestinationGemCollectionAdapter.Writes(owner.Word(pc - 4), pointer)) return false;
                    uint link = owner.Word(pc - 8);
                    if (UniversalMips.Op(link) != 35 || UniversalMips.Rt(link) != pointer || UniversalMips.SImm(link) is not (0 or 0x10) ||
                        !UniversalDestinationGemCollectionAdapter.PropertyRegister(owner, predecessors, pc - 12, UniversalMips.Rs(link), actor)) return false;
                    defined = true; role = "native-actor-property-linked-class"; continue;
                }
                if (UniversalMips.IsJal(word) || UniversalMips.IsJalr(word))
                    if (register is < 16 or > 23) return false;
                if (!predecessors.TryGetValue(pc, out var incoming) || incoming.Count == 0) return false;
                foreach (uint p in incoming) pending.Push(p);
            }
            return defined;
        }
        List<OwnedLoad> result = [];
        foreach (uint pc in code.Order())
        {
            uint word = owner.Word(pc);
            if (UniversalMips.Op(word) != 35 || UniversalMips.Imm(word) != 0xE330) continue;
            // The certified native color load is fed by class*4 plus LUI8007. Reject other
            // consumers instead of assuming an arbitrary offset/data pointer is a gem color.
            int address = UniversalMips.Rs(word);
            if (!code.Contains(pc - 12) || (code.Contains(pc - 16) && UniversalMips.IsBranch(owner.Word(pc - 16))) || !Linear(pc, pc - 4) || !Linear(pc - 4, pc - 8) || !Linear(pc - 8, pc - 12)) throw Unproved(pc);
            uint add = owner.Word(pc - 4), upper = owner.Word(pc - 8), shift = owner.Word(pc - 12);
            int index = UniversalMips.Rd(shift), cls = UniversalMips.Rt(shift);
            if (UniversalMips.Rt(word) != 7 || address != 1 || UniversalMips.Op(upper) != 15 || UniversalMips.Rt(upper) != address || UniversalMips.Imm(upper) != 0x8007 ||
                UniversalMips.Op(add) != 0 || UniversalMips.Fn(add) != 0x21 || UniversalMips.Rd(add) != address ||
                !((UniversalMips.Rs(add) == address && UniversalMips.Rt(add) == index) || (UniversalMips.Rt(add) == address && UniversalMips.Rs(add) == index)) ||
                UniversalMips.Op(shift) != 0 || UniversalMips.Fn(shift) != 0 || UniversalMips.Rs(shift) != 0 || ((shift >> 6) & 31) != 2 ||
                index == cls || index is 0 or 1 or 7 or 8 or 29 or 31 || cls is 0 or 1 or 7 or 8 or 29 or 31 || !ClassOwner(pc - 12, cls, out string role)) throw Unproved(pc);
            // The trampoline commits A3 before rejoining; native LW leaves its result
            // pending for this one instruction. Certify the exact independent continuation.
            if (!code.Contains(pc + 4) || owner.Word(pc + 4) != UniversalMips.EncodeI(15, 0, 2, 0x8007)) throw Unproved(pc);
            // Replace SLL with a non-linking jump. Its original LUI delay is independent of
            // the shifted index; retain its exact source word and every entry edge.
            result.Add(new(pc - 12, shift, upper, pc, add, word, cls, role));
        }
        return result;
    }
    private static InvalidDataException Unproved(uint pc) => new($"Destination native gem color consumer at 0x{pc:X8} lacks a proved source class/address/delay owner.");

    internal static UniversalAsmPart Build(IReadOnlyList<KeyValuePair<int, int>> gems, OwnedLoad site, string label)
    {
        _ = UniversalGemCollectionAdapter.BuildMappings(gems, "validation");
        UniversalAsmPart part = new();
        void I(int op, int rs, int rt, int imm) => part.Emit(UniversalMips.EncodeI(op, rs, rt, imm));
        // The patched native J preserves RA. The retained LUI delay sets AT before this
        // trampoline replays SLL/ADDU, leaving their original register effects intact.
        part.Label(label); I(9, 29, 29, -32); I(43, 29, 8, 16);
        part.Emit(site.PatchWord); part.Emit(site.AddWord);
        for (int i = 0; i < gems.Count; i++)
        {
            I(9, 0, 8, gems[i].Value);
            part.Emit(UniversalMips.EncodeI(5, site.ClassRegister, 8, 0), new(UniversalFixKind.BranchLabel, Label: label + "_n" + i)); part.Emit(0);
            I(15, 0, 7, 0x8007); I(35, 7, 7, unchecked((short)(0xE330 + gems[i].Key * 4)));
            part.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: label + "_done")); part.Emit(0);
            part.Label(label + "_n" + i);
        }
        part.Emit(site.LoadWord); part.Emit(0); // Commit the original unmatched load.
        part.Label(label + "_done"); I(35, 29, 8, 16); I(9, 29, 29, 32);
        part.Emit(UniversalMips.EncodeJ(2, site.LoadPc + 4)); part.Emit(0);
        return part;
    }

    internal static void Apply(byte[] overlay, IReadOnlyList<UniversalDestinationGemColourLoad> loads)
    {
        foreach (var site in loads)
        {
            int offset = (int)(site.PatchPc - UniversalOverlay.Base);
            if (BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset)) != site.OriginalWord || BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset + 4)) != site.DelayWord ||
                BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset + 8)) != site.AddWord ||
                BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset + 12)) != site.LoadWord ||
                BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(offset + 16)) != UniversalMips.EncodeI(15, 0, 2, 0x8007))
                throw new InvalidDataException("Destination native gem color load changed before final publication.");
        }
        foreach (var site in loads) BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan((int)(site.PatchPc - UniversalOverlay.Base)), UniversalMips.EncodeJ(2, site.Adapter));
    }
}
