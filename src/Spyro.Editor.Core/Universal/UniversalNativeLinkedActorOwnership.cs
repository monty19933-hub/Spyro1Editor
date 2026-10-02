using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalLinkedActorOwnershipSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>Gnorc Cove's linked-actor comparisons have a different pointer owner from
/// the current actor. Admit only the original native producer/consumer and actual copied
/// constructors. Placed integer links are relocated separately by UniversalIndexedRows.</summary>
internal static class UniversalNativeLinkedActorOwnership
{
    internal const uint ParentRead = 0x800814E0, IndexedRead = 0x80081764, SharedParentRead = 0x800821B8, CfParentRead = 0x80081E14;
    internal static readonly uint[] SharedParentConsumer = [0x8EC40004, 0, 0x10800C47, 0x2402009A,
        0x84830036, 0, 0x14620C43, 0x24090002, 0x90820048, 0, 0x14490C3F, 0x24020001, 0x080214B4, 0xA0820049];
    internal static readonly uint[] ParentConsumer = [0x8EC40004, 0, 0x1080000A, 0x2402009A,
        0x84830036, 0, 0x14620006, 0x24090002, 0x90820048, 0, 0x14490002, 0x24020001, 0xA0820049];
    internal static readonly uint[] ParentProducer = [0x8E700000, 0x0C00E9C8, 0x02602021,
        0x2672000C, 0x02402021, 0x0C005DC0, 0x26A5000C, 0x02602021,
        0x2402FFF4, 0x2411FFFF, 0xAE02000C, 0x240201CD, 0xAE150004, 0xA6000002, 0xAE110010];
    internal static readonly uint[] IndexConsumer = [0x8EC30010, 0, 0x0460000C, 0x00031040,
        0x00431021, 0x00021080, 0x00431023, 0x3C038007, 0x8C635828, 0x000210C0,
        0x00431021, 0x84430036, 0x240200C9, 0x10620091, 0];
    internal static readonly uint[] IndexProducer = [0x8E700000, 0x0C00E9C8, 0x02602021,
        0x2672000C, 0x02402021, 0x0C005DC0, 0x26A5000C, 0x02602021,
        0x2402FFF4, 0x2411FFFF, 0xAE02000C, 0x240201CD, 0xAE150004,
        0xA6000002, 0xA6000014, 0xAE110010];

    internal static IReadOnlyList<uint> Patch(UniversalAsmPart update, UniversalOverlay source,
        IReadOnlyDictionary<int, int> map, IReadOnlyDictionary<int, UniversalAsmPart> constructors)
    {
        if (!map.Any(m => m.Key != m.Value) ||
            !constructors.Keys.Any(c => c is 0xCA or 0xCF) &&
            !update.Items.Any(i => (i.Orig ?? i.SourcePc) is ParentRead or IndexedRead or SharedParentRead or CfParentRead)) return [];
        if (source.Init != 0x80085664 || source.Update != 0x8007B528 || source.CopyBuffer != 0x80088668 ||
            Convert.ToHexStringLower(SHA256.HashData(source.Bytes)) != UniversalIndexedRows.GnorcOverlaySha256) return [];
        List<uint> sites = [];
        foreach (var spec in new[] {
            (Class: 0xCA, Read: ParentRead, Properties: 0x80081004u, Producer: 0x80085B84u, Words: ParentProducer, Consumer: 0x800814D0u, Uses: ParentConsumer),
            (Class: 0xCF, Read: IndexedRead, Properties: 0x800816E8u, Producer: 0x80085C04u, Words: IndexProducer, Consumer: 0x80081738u, Uses: IndexConsumer),
            (Class: 0xCF, Read: CfParentRead, Properties: 0x800816E8u, Producer: 0x80085C04u, Words: IndexProducer, Consumer: 0x80081E04u, Uses: ParentConsumer) })
        {
            if (!constructors.ContainsKey(spec.Class) && !update.Items.Any(i => (i.Orig ?? i.SourcePc) == spec.Read)) continue;
            if (!constructors.TryGetValue(spec.Class, out var init))
                throw new InvalidDataException($"Linked class 0x{spec.Class:X3} has no copied native constructor owner.");
            var roles = UniversalClassRemapping.ProveInitializerRoles(init, source);
            if (roles.ActorRegister != 19 || roles.ParentRegister != 21 ||
                init.Items.Any(i => (i.Orig ?? i.SourcePc) is uint pc && pc > roles.ParentSavePc && pc < spec.Producer &&
                    UniversalClassRemapping.Writes(source.Word(pc), 21)))
                throw new InvalidDataException("Linked actor constructor lost its native parent input.");
            RequirePacket(init, source, spec.Producer, spec.Words);
            RequirePacket(update, source, spec.Properties, [0x8E760000]);
            RequirePacket(update, source, spec.Consumer, spec.Uses);
            if (spec.Read == CfParentRead)
            {
                foreach (int first in Enumerable.Range(0, update.Items.Count).Where(n =>
                             (update.Items[n].SourcePc ?? update.Items[n].Orig) == spec.Consumer))
                {
                    for (int n = 0; n < spec.Uses.Length; n++)
                        if (first + n >= update.Items.Count ||
                            (update.Items[first + n].SourcePc ?? update.Items[first + n].Orig) != spec.Consumer + (uint)n * 4)
                            throw new InvalidDataException("CF parent comparison has a partial or noncontiguous copied packet.");
                    UniversalNativePlayerDamageCalls.RequireCopiedEntries(update, spec.Consumer,
                        spec.Consumer + (uint)(spec.Uses.Length - 1) * 4, first, first + spec.Uses.Length - 1);
                }
            }
            var instructions = update.Items.Where(i => i.Orig != null || i.SourcePc != null)
                .Select(i => (i.Orig ?? i.SourcePc)!.Value).ToHashSet();
            if (!UniversalClassRemapping.ComparisonOnly(source, instructions, spec.Read + 4, 3,
                    map.Keys.Append(0x9A).Append(0xC9).Append(0).Distinct().ToArray()))
                throw new InvalidDataException("Linked actor class escapes its authenticated comparison.");
            sites.Add(spec.Read);
        }
        // CA and CF share this second parent notification on a later state path. Both
        // constructors above store the authenticated parent in properties+4, and neither
        // handler overwrites that property. Its direct jump retains the native store slot.
        RequirePacket(update, source, 0x800821A8, SharedParentConsumer);
        var sharedInstructions = update.Items.Where(i => i.Orig != null || i.SourcePc != null)
            .Select(i => (i.Orig ?? i.SourcePc)!.Value).ToHashSet();
        if (!UniversalClassRemapping.ComparisonOnly(source, sharedInstructions, SharedParentRead + 4, 3,
                map.Keys.Append(0x9A).Append(0).Distinct().ToArray()))
            throw new InvalidDataException("Shared linked actor class escapes its authenticated comparison.");
        sites.Add(SharedParentRead);
        // Validate all owners before changing anything; an intact copy cannot authenticate
        // a changed duplicate. Descending insertion keeps original item indices stable.
        foreach (int index in Enumerable.Range(0, update.Items.Count).Where(n =>
                     sites.Contains(update.Items[n].Orig ?? update.Items[n].SourcePc ?? 0)).OrderDescending())
            UniversalClassRemapping.TranslateOwnedClassification(update, index, map, "linked_class");
        return sites;
    }

    internal static void RequirePacket(UniversalAsmPart part, UniversalOverlay source, uint start, uint[] words)
    {
        var emitted = part.Items.Where(i => i.Word != null).ToArray();
        for (int n = 0; n < words.Length; n++)
        {
            uint pc = start + (uint)n * 4;
            if (!source.Contains(pc) || source.Word(pc) != words[n]) throw Failure(pc);
            var copies = Enumerable.Range(0, emitted.Length).Where(i => (emitted[i].Orig ?? emitted[i].SourcePc) == pc).ToArray();
            if (copies.Length == 0) throw Failure(pc);
            foreach (int at in copies)
            {
                var item = emitted[at];
                var expected = UniversalMips.IsPcRelative(words[n])
                    ? new UniversalFix(UniversalFixKind.Branch, UniversalMips.BranchTarget(words[n], pc))
                    : UniversalMips.Op(words[n]) == 2 ? new UniversalFix(UniversalFixKind.Jump, UniversalMips.BranchTarget(words[n], pc)) : default;
                if (item.Word != words[n] || item.Fix != expected || at < n || at - n + words.Length > emitted.Length)
                    throw Failure(pc);
                if (n == 0 && at > 0 && UniversalMips.IsBranch(emitted[at - 1].Word!.Value)) throw Failure(pc);
                for (int k = 0; k < words.Length; k++)
                    if ((emitted[at - n + k].Orig ?? emitted[at - n + k].SourcePc) != start + (uint)k * 4)
                        throw Failure(pc);
            }
        }
        // New relocated entries inside the packet cannot skip the pointer/index producer.
        foreach (var item in emitted.Where(i => i.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump))
            if (item.Fix.Orig > start && item.Fix.Orig < start + words.Length * 4 &&
                ((item.Orig ?? item.SourcePc) is not uint pc || pc < start || pc >= start + words.Length * 4))
                throw Failure(item.Fix.Orig);
        if (part.Tables.Values.SelectMany(t => t).Any(pc => pc > start && pc < start + words.Length * 4))
            throw Failure(start);
        var interiorLabels = part.Items.Where(i => i.Label != null &&
            (i.Orig ?? i.SourcePc) > start && (i.Orig ?? i.SourcePc) < start + words.Length * 4)
            .Select(i => i.Label).ToHashSet();
        if (part.Items.Any(i => i.Fix.Label != null && interiorLabels.Contains(i.Fix.Label)))
            throw Failure(start);
    }

    private static InvalidDataException Failure(uint pc) =>
        new($"Linked actor ownership needs an intact copied native witness at 0x{pc:X8}.");
}
