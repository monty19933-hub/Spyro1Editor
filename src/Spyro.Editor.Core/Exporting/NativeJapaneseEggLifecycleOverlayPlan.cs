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
        NativeJapaneseEggPickupPredicateSource predicateSource, bool source)
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
        int[] sizes = Measure(binding).Bytes.Select(b => b.Length).Concat(new[] { 0x3C, 0x1C }).ToArray();
        NativeOwnedOverlayAppendLayout? layout = null;
        NativeJapaneseEggOverlayProjection? projection = null;
        bool settled = false;
        for (int pass = 0; pass < 8; pass++)
        {
            layout = NativeOwnedOverlayAppendLayout.Create("egg-lifecycle-successor:" + authored.PlanSha256 + ":" + manifest.Sha256 + ":" + ordinarySlot,
                beforeOverlay, OverlayBase, 0x10000, Names.Select((id, i) => new NativeOwnedOverlayBlockSize(id, sizes[i])), "alignment");
            projection = NativeJapaneseEggOverlayProjection.Create(authored, layout);
            binding = NativeJapaneseEggCollectibleBinding.CreateProjected(authored, manifest, identities, projection);
            int[] next = Measure(binding).Bytes.Select(b => b.Length).Concat(new[] { 0x3C, 0x1C }).ToArray();
            if (next.SequenceEqual(sizes)) { settled = true; break; }
            sizes = next;
        }
        if (!settled || layout is null || projection is null)
            throw new InvalidDataException("Final lifecycle code sizing/projection did not converge within its bounded layout budget.");
        Layout = layout; Projection = projection; Binding = binding;
        NativeByteExtent Owner(int i) => layout.Blocks.Single(b => b.Id == Names[i]).Owner;
        uint Pc(int i) => checked(OverlayBase + (uint)Owner(i).Offset);
        var kernels = Compile(binding, Owner, Pc);
        var bodies = kernels.Bytes;
        if (bodies.Where((b, i) => b.Length != sizes[i]).Any())
            throw new InvalidDataException("Final lifecycle emitter length changed after exact-owner allocation.");
        Predicate = kernels.Predicate; Collector = kernels.Collector; Query = kernels.Query;
        Attempt = kernels.Attempt; Lifecycle = kernels.Lifecycle;
        PreBridgePc = Pc(5); PostBridgePc = Pc(6);
        List<NativeOwnedOverlayBlock> blocks = bodies.Select((b, i) => new NativeOwnedOverlayBlock(Names[i], Owner(i), b)).ToList();
        // Both bridges execute inside the existing full-register adapter. They
        // allocate no frame and preserve s3. Available enters visual work, as
        // does UnboundRow: the predecessor also admits canonical F5 and its
        // unrelated visual behavior must remain unchanged. Suppression and
        // bound-row refusals restore the original dispatcher frame.
        blocks.Add(new(Names[5], Owner(5), Words(0x02602021, 0x3C05801F, 0x34A5F600,
            Call(Lifecycle.BeforeVisualEntryPc), 0, 0x3408000A, 0x10480006, 0, 0x34080006, 0x10480003, 0,
            Jump(Restore), 0, Jump(Visual), 0)));
        blocks.Add(new(Names[6], Owner(6), Words(0x02602021, 0x3C05801F, 0x34A5F600,
            Call(Lifecycle.AfterVisualEntryPc), 0, Jump(Restore), 0)));
        var entries = new List<NativeOwnedOverlayEntry>();
        foreach (int i in Enumerable.Range(0, Names.Length))
            entries.Add(new(Names[i], Names[i], new(layout.Domain, Owner(i).Offset + (i == 0 ? 0x100 : 0), 4)));
        entries.Add(new("after-visual", Names[4], new(layout.Domain, Lifecycle.AfterVisualEntryPc - OverlayBase, 4)));
        // All linker destinations must be owned entries, including both
        // lifecycle entries and the predicate's wrapper rather than raw helper.
        overlay = layout.Materialize(blocks, entries, entries.Select(e => e.Owner));
        Put(overlay, checked((int)(PreEdge - OverlayBase)), Jump(PreBridgePc));
        Put(overlay, checked((int)(PostEdge - OverlayBase)), Jump(PostBridgePc));
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
    internal bool SourceCertified { get; }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal NativeJapaneseEggOverlayProjection Projection { get; }
    internal NativeJapaneseEggCollectibleBinding Binding { get; }
    internal NativeJapaneseEggPickupPredicateKernel Predicate { get; }
    internal NativeCollectibleCollectionKernel Collector { get; }
    internal NativeCollectibleStateQueryKernel Query { get; }
    internal NativeJapaneseEggCollectionAttempt Attempt { get; }
    internal NativeJapaneseEggLifecycleCallables Lifecycle { get; }
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
