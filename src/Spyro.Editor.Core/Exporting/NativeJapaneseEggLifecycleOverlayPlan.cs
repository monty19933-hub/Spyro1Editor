using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Successor to the exact authored visual overlay, retaining its full E800
/// allocation and appending explicit code owners. The two adapter-owned edges
/// are linked in the detached afterimage; no disc, live dispatch, Ready state,
/// card transaction or publication capability is issued by this plan.
/// </summary>
internal sealed class NativeJapaneseEggLifecycleOverlayPlan
{
    private const uint OverlayBase = 0x8007AA38, Ledger = 0x801FF600;
    private const uint PreEdge = 0x80088C3C, PostEdge = 0x80088AE4, Visual = 0x80088A38, Restore = 0x80088B14;
    private const int CopyField = NativeJapaneseEggArtisansIntegrationPlan.CopyBufferPatchOffset;
    private readonly byte[] beforeOverlay, beforeExecutable, overlay, executable, data;
    private readonly record struct Kernels(NativeJapaneseEggPickupPredicateKernel Predicate, NativeCollectibleCollectionKernel Collector,
        NativeCollectibleStateQueryKernel Query, NativeJapaneseEggCollectionAttempt Attempt, NativeJapaneseEggLifecycleCallables Lifecycle)
    {
        internal byte[][] Bytes => [Predicate.CopyBytes(), Collector.CopyBytes(), Query.CopyBytes(), Attempt.CopyBytes(), Lifecycle.CopyBytes()];
    }
    private static readonly string[] Names = ["pickup-predicate", "collect-state", "query-state", "pickup-attempt", "egg-lifecycle", "pre-visual-bridge", "post-visual-bridge"];

    private NativeJapaneseEggLifecycleOverlayPlan(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, int ordinarySlot, IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings,
        NativeJapaneseEggPickupPredicateSource predicateSource, bool source, bool guarded = false)
    {
        ArgumentNullException.ThrowIfNull(authored); ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(mappings); ArgumentNullException.ThrowIfNull(predicateSource);
        if (authored.SourceCertified != source) throw new InvalidDataException("Lifecycle overlay cannot change source ancestry.");
        SourceCertified = source; beforeOverlay = authored.CopyOverlay(); beforeExecutable = authored.CopyExecutable(); data = authored.CopyLevelData();
        var identities = mappings.Take(authored.Bindings.Count + 1).ToArray();
        RequireWords(beforeOverlay, PreEdge, Jump(Visual), 0);
        RequireWords(beforeOverlay, PostEdge, Jump(Restore), 0);
        if (beforeOverlay.Length != 0xE800 || authored.OverlayLayout.RuntimeBase != OverlayBase ||
            authored.OverlayLayout.CopyAnchor != OverlayBase + beforeOverlay.Length ||
            Word(beforeExecutable, CopyField + 8) != 0x3C018008 || Word(beforeExecutable, CopyField + 12) != 0xAC2285D8 ||
            DecodeAnchor(beforeExecutable) != authored.OverlayLayout.CopyAnchor)
            throw new InvalidDataException("Lifecycle overlay requires the unchanged exact visual allocation and copy initializer.");

        // Use the real emitters to measure, never a second instruction-count
        // formula. Probe owners are detached and never emitted into the final
        // output. Re-measure after projection, because literal address encoding
        // may change length. A bounded fixed point prevents accidental cycles.
        var binding = NativeJapaneseEggCollectibleBinding.Create(authored, manifest, identities);
        var names=guarded?Names.Concat(new[]{"live-consumer-guard"}).ToArray():Names;
        int[] Sizes(NativeJapaneseEggCollectibleBinding b)=>Measure(b).Bytes.Select(x=>x.Length).Concat(
            guarded?new[]{0x3C,0x1C,NativeJapaneseEggLiveGuardKernel.CapacityBytes}:new[]{0x3C,0x1C}).ToArray();
        int[] sizes = Sizes(binding);
        NativeOwnedOverlayAppendLayout? layout = null;
        NativeJapaneseEggOverlayProjection? projection = null;
        bool settled = false;
        for (int pass = 0; pass < 8; pass++)
        {
            layout = NativeOwnedOverlayAppendLayout.Create("egg-lifecycle-successor:" + authored.PlanSha256 + ":" + manifest.Sha256 + ":" + ordinarySlot,
                beforeOverlay, OverlayBase, 0x10000, names.Select((id, i) => new NativeOwnedOverlayBlockSize(id, sizes[i])), "alignment");
            projection = NativeJapaneseEggOverlayProjection.Create(authored, layout);
            binding = NativeJapaneseEggCollectibleBinding.CreateProjected(authored, manifest, identities, projection);
            int[] next = Sizes(binding);
            if (next.SequenceEqual(sizes)) { settled = true; break; }
            sizes = next;
        }
        if (!settled || layout is null || projection is null)
            throw new InvalidDataException("Final lifecycle code sizing/projection did not converge within its bounded layout budget.");
        Layout = layout; Projection = projection; Binding = binding;
        NativeByteExtent Owner(int i) => layout.Blocks.Single(b => b.Id == names[i]).Owner;
        uint Pc(int i) => checked(OverlayBase + (uint)Owner(i).Offset);
        var kernels = Compile(binding, Owner, Pc);
        var bodies = kernels.Bytes;
        if (bodies.Where((b, i) => b.Length != sizes[i]).Any())
            throw new InvalidDataException("Final lifecycle emitter length changed after exact-owner allocation.");
        Predicate = kernels.Predicate; Collector = kernels.Collector; Query = kernels.Query;
        Attempt = kernels.Attempt; Lifecycle = kernels.Lifecycle;
        PreBridgePc = Pc(5); PostBridgePc = Pc(6);
        if(guarded)
            LiveGuard=NativeJapaneseEggLiveGuardKernel.Compile(authored,binding,projection,Lifecycle,Owner(7),Pc(7),PreBridgePc,PostBridgePc);
        overlay = Materialize(layout, kernels, LiveGuard, PreBridgePc, PostBridgePc);
        executable = beforeExecutable.ToArray();
        short low = unchecked((short)projection.CopyAnchor);
        uint high = checked((uint)(((long)projection.CopyAnchor - low) >> 16));
        Put(executable, CopyField, 0x3C020000 | high); Put(executable, CopyField + 4, 0x24420000u | (ushort)low);
        if (DecodeAnchor(executable) != projection.CopyAnchor ||
            !executable.AsSpan(0, CopyField).SequenceEqual(beforeExecutable.AsSpan(0, CopyField)) ||
            !executable.AsSpan(CopyField + 8).SequenceEqual(beforeExecutable.AsSpan(CopyField + 8)))
            throw new InvalidDataException("Successor executable changed outside its exact signed-low copy-anchor pair.");
        for (int i = 0; i < beforeOverlay.Length; i++)
            if ((i < PreEdge - OverlayBase || i >= PreEdge - OverlayBase + 8) &&
                (i < PostEdge - OverlayBase || i >= PostEdge - OverlayBase + 8) && overlay[i] != beforeOverlay[i])
                throw new InvalidDataException("Lifecycle linker changed another retained visual overlay owner.");
        FinalOverlaySha256 = Hash(overlay); FinalExecutableSha256 = Hash(executable);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "authored-egg-lifecycle-successor-overlay-v1", SourceCertified, authored = authored.PlanSha256,
            projection = projection.PlanSha256, manifest = manifest.Sha256, ordinarySlot,
            identities = binding.Rows.Select(r => new { r.RowId, r.InstanceId, r.BitIndex }), FinalOverlaySha256, FinalExecutableSha256
        }));
        _ = RevertOverlayExact(overlay); _ = RevertExecutableExact(executable);

        Kernels Measure(NativeJapaneseEggCollectibleBinding b)
        {
            var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "lifecycle-size-probe", NativeAddressRevision.Final, 0x10000);
            uint[] pcs = [0x800F0000, 0x80100000, 0x80110000, 0x800E0000, 0x800D0000];
            return Compile(b, i => new(domain, i * 0x2000, i == 0 ? 0x154 : 0x2000), i => pcs[i]);
        }
        Kernels Compile(NativeJapaneseEggCollectibleBinding b, Func<int, NativeByteExtent> owner, Func<int, uint> pc)
        {
            var predicate = predicateSource.Compile(owner(0), pc(0));
            var collector = NativeCollectibleCollectionKernel.Compile(manifest, ordinarySlot, owner(1), pc(1), Ledger);
            var query = NativeCollectibleStateQueryKernel.Compile(manifest, ordinarySlot, owner(2), pc(2), Ledger);
            var attempt = NativeJapaneseEggCollectionAttempt.Compile(b, authored, manifest, predicate, collector, owner(3), pc(3));
            var lifecycle = source
                ? NativeJapaneseEggLifecycleCallables.Compile(authored, manifest, attempt, predicate, collector, query, owner(4), pc(4))
                : NativeJapaneseEggLifecycleCallables.CompileFixture(authored, manifest, attempt, predicate, collector, query, owner(4), pc(4));
            return new(predicate, collector, query, attempt, lifecycle);
        }
    }

    // Closed recompile of an existing guarded allocation, not a new layout or
    // caller-supplied payload route. Immediate inverse is the admitted parent's
    // lifecycle overlay/EXE, not the earlier E800 authored predecessor. Startup
    // and publication composers intentionally do not accept this successor.
    private NativeJapaneseEggLifecycleOverlayPlan(NativeCollectibleSaveWorkspacePlan workspace)
    {
        Projection = NativeJapaneseEggOverlayProjection.CreateForWorkspace(workspace);
        var parent = workspace.Ancestry.Lifecycle;
        var authored = workspace.Ancestry.Authored;
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        if (parent.LiveGuard is not { SourceBound: true } || parent.Collector.OrdinarySlot != 0 ||
            parent.Collector.ManifestSha256 != manifest.Sha256 || parent.Collector.CallerLedgerAddress != Ledger)
            throw new InvalidDataException("Workspace lifecycle requires the unchanged source-bound guarded fixed-slot parent.");
        SourceCertified = true; Layout = parent.Layout;
        beforeOverlay = parent.CopyOverlay(); beforeExecutable = parent.CopyExecutable(); data = workspace.CopyLevelData();
        Binding = NativeJapaneseEggCollectibleBinding.CreateProjected(authored, manifest,
            parent.Binding.Rows.Select(r => new NativeJapaneseEggCollectibleIdentity(r.RowId, r.InstanceId)), Projection);
        Predicate = parent.Predicate; Collector = parent.Collector; Query = parent.Query;
        Attempt = NativeJapaneseEggCollectionAttempt.Compile(Binding, authored, manifest, Predicate, Collector,
            parent.Attempt.Owner, parent.Attempt.EntryPc);
        Lifecycle = NativeJapaneseEggLifecycleCallables.Compile(authored, manifest, Attempt, Predicate, Collector, Query,
            parent.Lifecycle.Owner, parent.Lifecycle.BeforeVisualEntryPc);
        PreBridgePc = parent.PreBridgePc; PostBridgePc = parent.PostBridgePc;
        var guardOwner = Layout.Blocks.Single(b => b.Id == "live-consumer-guard").Owner;
        LiveGuard = NativeJapaneseEggLiveGuardKernel.Compile(authored, Binding, Projection, Lifecycle,
            guardOwner, parent.LiveGuard.BeforeEntryPc, PreBridgePc, PostBridgePc);
        if (Attempt.CopyBytes().Length != parent.Attempt.CopyBytes().Length ||
            Lifecycle.CopyBytes().Length != parent.Lifecycle.CopyBytes().Length ||
            Lifecycle.AfterVisualEntryPc != parent.Lifecycle.AfterVisualEntryPc ||
            LiveGuard.CodeByteLength != parent.LiveGuard.CodeByteLength ||
            LiveGuard.BeforeEntryPc != parent.LiveGuard.BeforeEntryPc || LiveGuard.AfterEntryPc != parent.LiveGuard.AfterEntryPc ||
            LiveGuard.ValidatePc != parent.LiveGuard.ValidatePc)
            throw new InvalidDataException("Workspace recompilation cannot resize or move any existing owned callable entry.");
        overlay = Materialize(Layout, new(Predicate, Collector, Query, Attempt, Lifecycle), LiveGuard, PreBridgePc, PostBridgePc);
        var changed = new[] { Attempt.Owner, Lifecycle.Owner, guardOwner };
        if (overlay.Length != beforeOverlay.Length || DecodeAnchor(beforeExecutable) != Projection.CopyAnchor)
            throw new InvalidDataException("Workspace recompilation cannot change the overlay or executable copy-anchor allocation.");
        for (int i = 0; i < overlay.Length; i++)
            if (!changed.Any(o => i >= o.Offset && i < o.EndExclusive) && overlay[i] != beforeOverlay[i])
                throw new InvalidDataException("Workspace recompilation changed another complete overlay owner.");
        executable = beforeExecutable.ToArray();
        FinalOverlaySha256 = Hash(overlay); FinalExecutableSha256 = Hash(executable);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "authored-egg-controller-workspace-lifecycle-v1", SourceCertified,
            workspace = workspace.PlanSha256, parent = parent.PlanSha256, projection = Projection.PlanSha256,
            Projection.FinalDataSha256, FinalOverlaySha256, FinalExecutableSha256
        }));
        RequireWorkspaceCurrent(workspace);
        _ = RevertOverlayExact(overlay); _ = RevertExecutableExact(executable);
    }

    // Shared owner/linker path. No route hand-patches individual literal
    // addresses: the existing typed emitters regenerate each complete body.
    private static byte[] Materialize(NativeOwnedOverlayAppendLayout layout, Kernels kernels,
        NativeJapaneseEggLiveGuardKernel? guard, uint preBridgePc, uint postBridgePc)
    {
        NativeByteExtent Owner(string id) => layout.Blocks.Single(b => b.Id == id).Owner;
        var bodies = kernels.Bytes;
        List<NativeOwnedOverlayBlock> blocks = bodies.Select((b, i) => new NativeOwnedOverlayBlock(Names[i], Owner(Names[i]), b)).ToList();
        // No bridge frame; preserve canonical UnboundRow visual behavior.
        blocks.Add(new(Names[5], Owner(Names[5]), Words(0x02602021, 0x3C05801F, 0x34A5F600,
            Call(kernels.Lifecycle.BeforeVisualEntryPc), 0, 0x3408000A, 0x10480006, 0, 0x34080006, 0x10480003, 0,
            Jump(Restore), 0, Jump(Visual), 0)));
        blocks.Add(new(Names[6], Owner(Names[6]), Words(0x02602021, 0x3C05801F, 0x34A5F600,
            Call(kernels.Lifecycle.AfterVisualEntryPc), 0, Jump(Restore), 0)));
        if (guard is not null) blocks.Add(new("live-consumer-guard", Owner("live-consumer-guard"), guard.CopyBytes()));
        var entries = Names.Select((id, i) => new NativeOwnedOverlayEntry(id, id,
            new(layout.Domain, Owner(id).Offset + (i == 0 ? 0x100 : 0), 4))).ToList();
        entries.Add(new("after-visual", Names[4], new(layout.Domain, kernels.Lifecycle.AfterVisualEntryPc - OverlayBase, 4)));
        if (guard is not null)
        {
            entries.Add(new("guard-before", "live-consumer-guard", new(layout.Domain, guard.BeforeEntryPc - OverlayBase, 4)));
            entries.Add(new("guard-after", "live-consumer-guard", new(layout.Domain, guard.AfterEntryPc - OverlayBase, 4)));
        }
        byte[] result = layout.Materialize(blocks, entries, entries.Select(e => e.Owner));
        Put(result, checked((int)(PreEdge - OverlayBase)), Jump(guard?.BeforeEntryPc ?? preBridgePc));
        Put(result, checked((int)(PostEdge - OverlayBase)), Jump(guard?.AfterEntryPc ?? postBridgePc));
        return result;
    }
    internal bool SourceCertified { get; }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal NativeJapaneseEggOverlayProjection Projection { get; }
    internal NativeJapaneseEggCollectibleBinding Binding { get; }
    internal NativeJapaneseEggPickupPredicateKernel Predicate { get; }
    internal NativeCollectibleCollectionKernel Collector { get; }
    internal NativeCollectibleStateQueryKernel Query { get; }
    internal NativeJapaneseEggCollectionAttempt Attempt { get; }
    internal NativeJapaneseEggLifecycleCallables Lifecycle { get; }
    internal NativeJapaneseEggLiveGuardKernel? LiveGuard { get; }
    internal uint PreBridgePc { get; }
    internal uint PostBridgePc { get; }
    internal string FinalOverlaySha256 { get; }
    internal string FinalExecutableSha256 { get; }
    internal string PlanSha256 { get; }
    internal bool DispatchInstalledInDetachedOverlay => true;
    internal bool DispatchActivated => false;
    internal bool NativeResidenceIssued => false;
    internal bool PersistentSaveChanges => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyOverlay() => overlay.ToArray();
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyLevelData() => data.ToArray();
    internal byte[] RevertOverlayExact(ReadOnlySpan<byte> after)
    { if (!after.SequenceEqual(overlay)) throw new InvalidDataException("Lifecycle inverse requires the complete exact successor overlay."); return beforeOverlay.ToArray(); }
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> after)
    { if (!after.SequenceEqual(executable)) throw new InvalidDataException("Lifecycle inverse requires the complete exact successor executable."); return beforeExecutable.ToArray(); }
    internal static NativeJapaneseEggLifecycleOverlayPlan CreateForWorkspace(NativeCollectibleSaveWorkspacePlan workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return new(workspace);
    }
    internal void RequireWorkspaceCurrent(NativeCollectibleSaveWorkspacePlan workspace)
    {
        Projection.RequireWorkspaceCurrent(workspace);
        if (!workspace.Ancestry.Lifecycle.CopyOverlay().AsSpan().SequenceEqual(beforeOverlay) ||
            !workspace.Ancestry.Lifecycle.CopyExecutable().AsSpan().SequenceEqual(beforeExecutable) ||
            !workspace.CopyLevelData().AsSpan().SequenceEqual(data))
            throw new InvalidDataException("Lifecycle recompilation requires its exact controller workspace and complete immediate parent.");
    }
    internal static NativeJapaneseEggLifecycleOverlayPlan Fixture(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, int ordinarySlot, IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings,
        NativeJapaneseEggPickupPredicateSource predicateSource) => new(authored, manifest, ordinarySlot, mappings, predicateSource, false);
    internal static NativeJapaneseEggLifecycleOverlayPlan Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa,
        NativeJapaneseEggAuthoredClassesPlan authored, DetachedCollectibleManifest manifest, int ordinarySlot,
        IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa);
        try
        {
            // Validate the unchanged source-issued predecessor with its existing
            // strict report; do not relax its fixed E800/anchor assumptions.
            _ = NativeJapaneseEggAuthoredMemoryReport.Capture(usa, authored);
            return new(authored, manifest, ordinarySlot, mappings, NativeJapaneseEggPickupPredicateSource.Capture(japan, usa), true);
        }
        finally { try { japan.VerifyUnchanged(); } finally { usa.VerifyUnchanged(); } }
    }
    internal static NativeJapaneseEggLifecycleOverlayPlan FixtureGuarded(NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest,int ordinarySlot,IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings,
        NativeJapaneseEggPickupPredicateSource predicateSource)=>new(authored,manifest,ordinarySlot,mappings,predicateSource,false,true);
    internal static NativeJapaneseEggLifecycleOverlayPlan CaptureGuarded(NativeRegionalSourceSession japan,NativeRegionalSourceSession usa,
        NativeJapaneseEggAuthoredClassesPlan authored,DetachedCollectibleManifest manifest,int ordinarySlot,
        IEnumerable<NativeJapaneseEggCollectibleIdentity> mappings)
    {
        try
        {
            _=NativeJapaneseEggAuthoredMemoryReport.Capture(usa,authored);
            return new(authored,manifest,ordinarySlot,mappings,NativeJapaneseEggPickupPredicateSource.Capture(japan,usa),true,true);
        }
        finally{try{japan.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}
    }
    private static uint DecodeAnchor(byte[] exe)
    {
        if ((Word(exe, CopyField) & 0xFFFF0000) != 0x3C020000 || (Word(exe, CopyField + 4) & 0xFFFF0000) != 0x24420000)
            throw new InvalidDataException("Copy-anchor opcode/register schema changed.");
        return unchecked((Word(exe, CopyField) << 16) + (uint)(int)(short)Word(exe, CopyField + 4));
    }
    private static void RequireWords(byte[] bytes, uint pc, uint first, uint second)
    {
        int at = checked((int)(pc - OverlayBase));
        if (at < 0 || at > bytes.Length - 8 || Word(bytes, at) != first || Word(bytes, at + 4) != second)
            throw new InvalidDataException("Exact authored adapter edge changed before lifecycle linking.");
    }
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static void Put(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
    private static uint Jump(uint pc) => 0x08000000 | ((pc >> 2) & 0x3FFFFFF);
    private static uint Call(uint pc) => 0x0C000000 | ((pc >> 2) & 0x3FFFFFF);
    private static byte[] Words(params uint[] words)
    { byte[] result = new byte[words.Length * 4]; for (int i = 0; i < words.Length; i++) Put(result, i * 4, words[i]); return result; }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
