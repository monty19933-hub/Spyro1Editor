using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalNativePlayerDamageCallsSmoke")]

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalNativePlayerDamageCall(uint CallPc, uint ArgumentPc, uint DamageMask, uint NativeStorePc);
public sealed record UniversalNativePlayerDamageCallAnalysis(uint DamageMask, IReadOnlyList<UniversalNativePlayerDamageCall> Calls)
{
    public IReadOnlyList<int> AnimationIds(byte[] executable) => UniversalPlayerDamageDependencies.MapDamageMask(DamageMask, executable);
}

/// <summary>Authenticated original USA native sphere damage helper calls. The helper
/// ORs a2 into Spyro's damage accumulator after its distance checks. Inspect the actual
/// copied packets, not just original source PCs, before requesting reaction animations.</summary>
internal static partial class UniversalNativePlayerDamageCalls
{
    internal const uint Helper = 0x8004E2E8, NativeStore = 0x8004E3B4;
    internal const string HelperSha256 = "616f1e7f8fed79452a3ec0793db251866c9127bf2e76f66dd634ed02f89163f3";

    internal static UniversalNativePlayerDamageCallAnalysis AnalyzeGnorc(UniversalOverlay source, UniversalAsmPart part, byte[]? executable) =>
        AnalyzeSource("gnorccove", source, part, executable);

    internal static UniversalNativePlayerDamageCallAnalysis AnalyzeSource(string sourceKey, UniversalOverlay source, UniversalAsmPart part, byte[]? executable)
    {
        CertifiedSources.TryGetValue(sourceKey, out var owner);
        var calls = part.Items.Select((item, index) => (Item: item, Index: index)).Where(c =>
            c.Item.Word is uint w && UniversalMips.IsJal(w) && UniversalMips.BranchTarget(w, UniversalOverlay.Base) == Helper ||
            owner != null && owner.Sites.Any(site => site.Call == (c.Item.SourcePc ?? c.Item.Orig))).ToArray();
        if (calls.Length == 0) return new(0, []);
        if (owner == null || Convert.ToHexStringLower(SHA256.HashData(source.Bytes)) != owner.Sha256 ||
            source.Update != owner.Update || source.Init != owner.Init || source.CopyBuffer != owner.Copy)
            throw new InvalidDataException("Native player damage requires its complete authenticated source overlay and callbacks.");
        if (executable == null || executable.Length != 0x66000 ||
            Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(Helper - UniversalExeBindings.ExeBase), 0xE0))) != HelperSha256)
            throw new InvalidDataException("Native player sphere damage helper differs from its complete certified USA owner.");

        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(source);
        var (code, tables) = UniversalDispatchAnalysis.Closure(source, sw.Function, [sw.Function], new HashSet<uint>());
        var predecessors = UniversalDestinationGemCollectionAdapter.Predecessors(source, code, tables);
        List<UniversalNativePlayerDamageCall> proofs = [];
        foreach (var occurrence in calls)
        {
            var item = occurrence.Item;
            uint pc = item.SourcePc ?? item.Orig ?? throw new InvalidDataException("Native player damage call has no source owner.");
            var site = owner.Sites.SingleOrDefault(site => site.Call == pc) ??
                throw new InvalidDataException($"Native player damage call {pc:X8} has no certified source argument packet.");
            uint start = site.Start, arg = site.Argument, mask = site.Mask;
            var argument = new UniversalMipsState();
            for (int n = 0; n < site.Words.Count; n++)
            {
                uint at = start + (uint)n * 4, word = source.Word(at);
                if (word != site.Words[n] || UniversalMips.IsBranch(word) && at != pc)
                    throw new InvalidDataException("Native damage source argument packet differs from its certified constant producer.");
                if (at != pc) argument.Apply(word);
            }
            if (argument[6] != mask)
                throw new InvalidDataException("Native damage argument packet does not produce the certified a2 mask.");
            for (uint at = start; at <= pc + 4; at += 4)
            {
                if (at > start && (!predecessors.TryGetValue(at, out var incoming) || !incoming.SetEquals([at - 4])))
                    throw new InvalidDataException("Native player damage argument packet has an unowned incoming edge.");
                var copies = part.Items.Where(i => (i.SourcePc ?? i.Orig) == at).ToArray();
                if (copies.Length == 0 || copies.Any(i => i.Word != source.Word(at) || i.Fix.Kind != UniversalFixKind.None))
                    throw new InvalidDataException("Copied native player damage argument packet differs from its source owner.");
            }
            // Prove the actual copied occurrence has the whole packet adjacent, including
            // the delay slot. A surviving source packet elsewhere cannot bless a partial copy.
            int index = occurrence.Index, wordCount = checked((int)((pc - start) / 4));
            int first = index - wordCount;
            if (first < 0 || index + 1 >= part.Items.Count)
                throw new InvalidDataException("Copied native player damage call lacks its complete argument packet.");
            for (int n = 0; n <= wordCount + 1; n++)
            {
                var copy = part.Items[first + n]; uint at = start + (uint)n * 4;
                if ((copy.SourcePc ?? copy.Orig) != at || copy.Word != source.Word(at) || copy.Fix.Kind != UniversalFixKind.None)
                    throw new InvalidDataException("Copied native player damage packet is not one intact linear occurrence.");
            }
            RequireCopiedEntries(part, start, pc + 4, first, index + 1);
            proofs.Add(new(pc, arg, mask, NativeStore));
        }
        var unique = proofs.Distinct().OrderBy(p => p.CallPc).ToArray();
        return new(unique.Aggregate(0u, (mask, p) => mask | p.DamageMask), unique);
    }
    internal static void RequireCopiedEntries(UniversalAsmPart part, uint start, uint end, int first, int last)
    {
        var labels = new Dictionary<string, int>();
        for (int n = 0; n < part.Items.Count; n++)
            if (part.Items[n].Label is string label)
            {
                int next = n + 1;
                while (next < part.Items.Count && part.Items[next].Word == null) next++;
                if (!labels.TryAdd(label, next)) throw new InvalidDataException("Copied native damage packet has duplicate labels.");
            }
        // Source CFG ownership alone cannot authenticate newly emitted incoming edges.
        foreach (var item in part.Items)
        {
            uint? target = item.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump ? item.Fix.Orig : null;
            if (target == null && item.Fix.Kind == UniversalFixKind.None && item.Word is uint word &&
                (UniversalMips.IsPcRelative(word) || UniversalMips.Op(word) == 2))
                target = UniversalMips.BranchTarget(word, item.SourcePc ?? item.Orig ?? UniversalOverlay.Base);
            if (target > start && target <= end)
                throw new InvalidDataException("Copied native damage packet has an incoming branch or jump.");
            if (item.Fix.Label is string to && labels.TryGetValue(to, out int index) && index > first && index <= last)
                throw new InvalidDataException("Copied native damage packet has an incoming label reference.");
        }
        if (part.Tables.Values.SelectMany(t => t).Any(target => target > start && target <= end))
            throw new InvalidDataException("Copied native damage packet has an incoming jump table reference.");
        if (first > 0 && part.Items[first - 1].Word is uint prior && UniversalMips.IsBranch(prior))
            throw new InvalidDataException("Copied native damage argument begins in an unowned delay slot.");
    }

}
