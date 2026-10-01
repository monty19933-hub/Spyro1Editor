using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalJumpModelOwnershipSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>The Gnorc Cove 09A animation reset passes the actual class through a direct
/// jump to its model lookup. Retain that index only while both copied packets are intact.</summary>
internal static class UniversalNativeJumpModelOwnership
{
    internal const uint ClassRead = 0x8007EE0C;
    internal const uint ModelTail = 0x800843FC;
    private static readonly uint[] Producer = [0x86620036, 0x3C098007, 0x25296378,
        0x080210FF, 0xA2600049];
    private static readonly uint[] Consumer = [0xA2630048, 0xA2600040, 0x00021080,
        0x00491021, 0x8C420000, 0];

    internal static bool MarkOwned(UniversalAsmPart part, UniversalOverlay owner)
    {
        if (!owner.Contains(ClassRead - 4) || !owner.Contains(ModelTail + 20) ||
            UniversalMips.IsBranch(owner.Word(ClassRead - 4))) return false;
        bool Source(uint pc, uint[] words) => words.Select((word, n) =>
            owner.Contains(pc + (uint)n * 4) && owner.Word(pc + (uint)n * 4) == word).All(v => v);
        if (!Source(ClassRead, Producer) || !Source(ModelTail, Consumer)) return false;
        // lh has two independent literal-base instructions before its first use. The
        // direct jump executes its unchanged store delay slot; the tail only shifts
        // v0 once, indexes the exact model table, then replaces v0 with its model root.
        // No class-derived value reaches a call, another register, or a store.
        var emitted = part.Items.Where(i => i.Word != null).ToArray();
        bool Packet(uint start, uint[] words)
        {
            for (int n = 0; n < words.Length; n++)
            {
                uint pc = start + (uint)n * 4;
                var copies = Enumerable.Range(0, emitted.Length).Where(at =>
                    (emitted[at].Orig ?? emitted[at].SourcePc) == pc).ToArray();
                if (copies.Length == 0) return false;
                foreach (int at in copies)
                {
                    var item = emitted[at];
                    bool jump = pc == ClassRead + 12;
                    if (pc == ClassRead && at > 0 && UniversalMips.IsBranch(emitted[at - 1].Word!.Value)) return false;
                    if (item.Word != words[n] || (jump
                        ? item.Fix.Kind != UniversalFixKind.Jump || item.Fix.Orig != ModelTail
                        : item.Fix.Kind != UniversalFixKind.None)) return false;
                    // An intact witness elsewhere cannot authenticate a reordered,
                    // truncated or modified additional copy of this same source packet.
                    if (at < n || at - n + words.Length > emitted.Length) return false;
                    for (int k = 0; k < words.Length; k++)
                        if ((emitted[at - n + k].Orig ?? emitted[at - n + k].SourcePc) != start + (uint)k * 4)
                            return false;
                }
            }
            return true;
        }
        if (!Packet(ClassRead, Producer) || !Packet(ModelTail, Consumer) ||
            !part.Items.Any(i => i.Orig == ModelTail)) return false;
        for (int n = 0; n < part.Items.Count; n++)
            if ((part.Items[n].Orig ?? part.Items[n].SourcePc) == ClassRead)
                part.Items[n] = part.Items[n] with { ClassReadOwned = true };
        return true;
    }
}
