using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalGnorcCurrentModelReadSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>C9's current-actor class selects the destination model's animation11.
/// Keep that physical class unchanged; this is a model lookup, not a source-class comparison.</summary>
internal static class UniversalGnorcCurrentModelRead
{
    internal const uint Read = 0x80080E34, Start = 0x80080E24;
    internal static readonly uint[] Packet = [
        0x3C098007, 0x25296378, 0xAC620010, 0xAE220000, 0x86620036,
        0x2403000B, 0xA2630048, 0xA2600040, 0x00021080, 0x00491021,
        0x8C420000, 0];

    internal static IReadOnlyList<uint> MarkOwned(UniversalAsmPart part, UniversalOverlay source)
    {
        var copies = part.Items.Select((item, index) => (Item: item, Index: index))
            .Where(c => (c.Item.SourcePc ?? c.Item.Orig) == Read).ToArray();
        if (copies.Length == 0) return [];
        if (source.Update != 0x8007B528 || source.Init != 0x80085664 || source.CopyBuffer != 0x80088668 ||
            Convert.ToHexStringLower(SHA256.HashData(source.Bytes)) != UniversalIndexedRows.GnorcOverlaySha256)
            throw new InvalidDataException("Gnorc current model lookup needs its complete authenticated source overlay and callbacks.");
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(source);
        uint dispatch = source.Word(sw.TreeStart - 4);
        if (UniversalMips.Op(dispatch) is not (33 or 37) || UniversalMips.Rs(dispatch) != 19 || UniversalMips.SImm(dispatch) != 0x36)
            throw new InvalidDataException("Gnorc current model lookup lost its source dispatch actor.");
        var (code, tables) = UniversalDispatchAnalysis.Closure(source, sw.Function, [sw.Function], new HashSet<uint>());
        var predecessors = UniversalDestinationGemCollectionAdapter.Predecessors(source, code, tables);
        for (int n = 0; n < Packet.Length; n++)
        {
            uint pc = Start + (uint)n * 4;
            if (source.Word(pc) != Packet[n] || n > 0 &&
                (!predecessors.TryGetValue(pc, out var incoming) || !incoming.SetEquals([pc - 4])))
                throw new InvalidDataException($"Gnorc current model lookup packet has changed source bytes or incoming edges at 0x{pc:X8}.");
        }
        foreach (var copy in copies)
        {
            int first = copy.Index - checked((int)((Read - Start) / 4));
            if (first < 0 || first + Packet.Length > part.Items.Count)
                throw new InvalidDataException("Gnorc current model lookup lacks its complete copied packet.");
            for (int n = 0; n < Packet.Length; n++)
            {
                uint pc = Start + (uint)n * 4; var item = part.Items[first + n];
                var fix = UniversalMips.Op(Packet[n]) == 2 ? new UniversalFix(UniversalFixKind.Jump, UniversalMips.BranchTarget(Packet[n], pc)) : default;
                if ((item.SourcePc ?? item.Orig) != pc || item.Word != Packet[n] || item.Fix != fix)
                    throw new InvalidDataException("Gnorc current model lookup copied packet or relocation changed.");
            }
            UniversalNativePlayerDamageCalls.RequireCopiedEntries(part, Start, Start + (uint)(Packet.Length - 1) * 4, first, first + Packet.Length - 1);
        }
        // The class lives only in v0: shift -> indexed address -> model root load at E4C
        // kills the class. The original later join at E58 is outside that value lifetime.
        // Authenticate every copy before marking any. This emits no code and leaves LH's
        // value in the destination namespace for the g_Models[physicalClass] selection.
        foreach (var copy in copies) part.Items[copy.Index] = copy.Item with { ClassReadOwned = true };
        return [Read];
    }
}
