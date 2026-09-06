using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeParticleBehaviorCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] before = Words(0x92420001, 0xA7A40070, 0x08000000, 0);
        NativeParticleBehaviorOperandPatch[] patches =
        [new(0x80088370, 0x92420001, 0x92220001, NativeParticleBehaviorOperandKind.ParticleBase),
         new(0x80088374, 0xA7A40070, 0xA7A40030, NativeParticleBehaviorOperandKind.StackHalfword)];
        byte[] after = NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, patches);
        check(after.AsSpan().SequenceEqual(Words(0x92220001, 0xA7A40030, 0x08000000, 0)), "typed base and scratch edits preserve instruction complement");
        check(NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, patches.Reverse().ToArray()).AsSpan().SequenceEqual(after), "operand edit order is not authority");
        after[0] ^= 1;
        check(before[0] == 1 && NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, patches)[0] == 1, "source and emitted instruction bytes detached");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[0], patches[0]]), "duplicate instruction ownership");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[0] with { BeforeWord = 0x92420002 }]), "stale instruction preimage");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[0] with { AfterWord = 0x92230001 }]), "destination register cannot change");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[1] with { AfterWord = 0xA7A40032 }]), "scratch mapping cannot choose unrelated cell");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[0] with { SourcePc = 0x80088371 }]), "partial instruction field");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, [patches[0] with { SourcePc = 0x80088380 }]), "unowned instruction field");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(Words(0x12400001), 0x80088370, [new(0x80088370, 0x12400001, 0x12200001, NativeParticleBehaviorOperandKind.ParticleBase)]), "branch-register rewrite is not particle memory semantics");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(Words(0xAFA40070), 0x80088370, [new(0x80088370, 0xAFA40070, 0xAFA40030, NativeParticleBehaviorOperandKind.StackHalfword)]), "word scratch access not certified halfword grammar");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before, 0x80088370, new BoundedAdversary(patches[0])), "finite adversarial operand enumeration stops at50");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(before[..^1], 0x80088370, []), "partial source instruction owner");
        reject(() => NativeParticleBehaviorOperandRewriter.Apply(new byte[0x384], 0x80088370, []), "source byte budget applies before copying");
        check(typeof(NativeDarkHollowWizardParticleBehaviorSource).GetConstructors().Length == 0, "public source metadata cannot issue native behavior authority");
    }

    internal static void RunSource(string path, Action<bool, string> check, Action<Action, string> reject)
    {
        var source = NativeDarkHollowWizardParticleBehaviorSource.Capture(path);
        NativeParticleLifetimeCases.RunSource(source, check, reject);
        check(source.Package.Fragments.Count == 4 && source.Package.InstructionCount == 224 && source.Package.EntryPcs.Count == 5, "four complete native particle owners and five entry points");
        check(source.OperandPatches.Count == 49 && source.OperandPatches.Count(p => p.Kind == NativeParticleBehaviorOperandKind.StackHalfword) == 12, "exact37 base and12 scratch fields");
        check(source.Hooks.Count == 4 && source.Continuations.Count == 6, "only four absent dispatch hooks and six continuation sites");
        check(!source.CompleteNativeClosureProven && !source.BehaviorInstalled && !source.PublicationApproved, "detached effect code not runtime or installed ownership");
        var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "particle-final", NativeAddressRevision.Final, 0xD000);
        var arena = new NativeByteExtent(domain, 0xB000, 0x1000);
        uint runtime = 0x80085A38;
        uint[] targets = [0x8008442C, 0x80084448, 0x80084450, 0x8008553C];
        var bridges = targets.Select((pc, i) => new NativeParticleBehaviorBridge(pc, new(domain, 0xBF00 + i * 8, 8))).ToArray();
        var linked = source.LinkInto(arena, runtime, bridges);
        check(linked.CopyBytes().Length == 0x380 && linked.UnmaterializedExternalTargets.Count == 4, "link declares four actual bridge owners still to install");
        check(source.LinkInto(arena, runtime, bridges.Reverse().ToArray()).CopyBytes().AsSpan().SequenceEqual(linked.CopyBytes()), "bridge request ordering is irrelevant");
        reject(() => source.LinkInto(arena, runtime + 4, bridges), "runtime must bind the original overlay base and final offset");
        var sceneDomain = new NativeAddressSpace(NativeAddressKind.Scene, "particle-final", NativeAddressRevision.Final, 0xD000);
        reject(() => source.LinkInto(new(sceneDomain, 0xB000, 0x1000), runtime,
            targets.Select((pc, i) => new NativeParticleBehaviorBridge(pc, new(sceneDomain, 0xBF00 + i * 8, 8))).ToArray()), "scene storage cannot become native overlay code by label");
        foreach (var b in bridges)
        {
            byte[] bytes = source.CopyBridgeBytes(b.DestinationPc);
            check(bytes.AsSpan().SequenceEqual(Words(0x08000000 | ((b.DestinationPc >> 2) & 0x3FFFFFF), 0)), "bridge targets native continuation without clobbering carried registers");
        }
        reject(() => source.LinkInto(arena, runtime, bridges[..3]), "missing native continuation refused");
        reject(() => source.LinkInto(arena, runtime, [bridges[0], bridges[0], bridges[2], bridges[3]]), "duplicate native continuation refused");
        reject(() => source.CopyBridgeBytes(0x80084CE4), "wrong live-register sprite tail refused");
        reject(() => source.CopyBridgeBytes(0x80084410), "duplicate tick increment tail refused");
        var bad = bridges.ToArray(); bad[0] = bad[0] with { Owner = new(domain, 0xB000, 8) };
        reject(() => source.LinkInto(arena, runtime, bad), "bridge reservation cannot alias actual generated code");
        bad = bridges.ToArray(); bad[0] = bad[0] with { Owner = bridges[1].Owner };
        reject(() => source.LinkInto(arena, runtime, bad), "different bridge code cannot overlap");
        var detached = new NativeAddressSpace(NativeAddressKind.LevelData, "particle-final", NativeAddressRevision.Final, 0xD000);
        bad = bridges.ToArray(); bad[0] = bad[0] with { Owner = new(detached, 0xBF00, 8) };
        reject(() => source.LinkInto(arena, runtime, bad), "same label does not bind final canonical identity");
        foreach (var fragment in source.Package.Fragments)
        {
            byte[] original = source.ReadSourceExtent(fragment.Source.Read.Extent);
            byte[] expected = NativeParticleBehaviorOperandRewriter.Apply(original, fragment.Source.SourcePc,
                source.OperandPatches.Where(p => p.SourcePc >= fragment.Source.SourcePc && p.SourcePc < fragment.Source.SourcePc + fragment.ByteLength).ToArray());
            check(expected.AsSpan().SequenceEqual(fragment.CopyBytes()), "complete source owner differs only at exact operand ledger");
            byte[] bytes = linked.CopyBytes();
            for (int offset = 0; offset < fragment.ByteLength; offset += 4)
            {
                uint pc = fragment.Source.SourcePc + (uint)offset;
                uint finalPc = linked.SourceToFinalPc[pc]; int finalOffset = checked((int)(finalPc - runtime));
                var transfer = source.Package.Transfers.SingleOrDefault(t => t.SourcePc == pc);
                uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(finalOffset));
                uint originalWord = BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset));
                if (transfer == null) check(actual == originalWord, "every unowned instruction operand and delay word remains exact");
                else
                {
                    uint target;
                    if (linked.SourceToFinalPc.TryGetValue(transfer.TargetPc!.Value, out uint mapped)) target = mapped;
                    else if (transfer.Kind == NativeMipsBehaviorTransferKind.Call26) target = transfer.TargetPc.Value;
                    else
                    {
                        var claim = source.Continuations.Single(c => c.SourcePc == pc);
                        var bridge = bridges.Single(b => b.DestinationPc == claim.DestinationPc);
                        target = checked(runtime + (uint)(bridge.Owner.Offset - arena.Offset));
                    }
                    uint decoded = transfer.Kind == NativeMipsBehaviorTransferKind.Branch16 ? unchecked(finalPc + 4 + (uint)((short)actual * 4)) :
                        ((finalPc + 4) & 0xF0000000) | ((actual & 0x3FFFFFF) << 2);
                    check(decoded == target && (actual & (transfer.Kind == NativeMipsBehaviorTransferKind.Branch16 ? 0xFFFF0000u : 0xFC000000u)) ==
                        (originalWord & (transfer.Kind == NativeMipsBehaviorTransferKind.Branch16 ? 0xFFFF0000u : 0xFC000000u)), "independent branch/jump decoding preserves source opcode and exact owned target");
                }
            }
        }
        check(source.Package.StackAccesses.All(s => s.ByteWidth == 2 && s.Offset is 0x30 or 0x32 or 0x34), "adapted stack metadata names actual halfword owner");
        foreach (var hook in source.Hooks)
            check(BinaryPrimitives.ReadUInt32LittleEndian(source.ReadSourceExtent(hook.Read.Extent)) == hook.BeforeWord &&
                linked.SourceToFinalPc.ContainsKey(hook.SourceEntryPc), "every issued hook binds actual default native word and one emitted entry");
        byte[] dark = source.ReadSourceExtent(new(source.Hooks[0].Read.Extent.Space, 0, 0xB000));
        source.RequireOriginalDark(dark); dark[^1] ^= 1;
        reject(() => source.RequireOriginalDark(dark), "complete original overlay complement required, not just hook words");
    }
    private static byte[] Words(params uint[] words)
    { byte[] b = new byte[words.Length * 4]; for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i * 4), words[i]); return b; }
    private sealed class BoundedAdversary(NativeParticleBehaviorOperandPatch value) : IReadOnlyList<NativeParticleBehaviorOperandPatch>
    {
        public int Count => 49;
        public NativeParticleBehaviorOperandPatch this[int i] => value;
        public IEnumerator<NativeParticleBehaviorOperandPatch> GetEnumerator()
        { for (int i = 0; i < 50; i++) yield return value; throw new InvalidOperationException("enumerated beyond maximum+1"); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
