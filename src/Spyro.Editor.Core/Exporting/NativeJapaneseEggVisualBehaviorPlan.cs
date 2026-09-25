using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggVisualBehaviorSmoke")]

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeJapaneseEggBehaviorEvidence(string Id, string Region, int Offset, int ByteLength, string Sha256);
public sealed record NativeJapaneseEggVisualCodeOwner(string Id, int OverlayOffset, uint RuntimePc, int ByteLength, string Sha256);
public sealed record NativeJapaneseEggVisualCall(string Id, uint SourceCallPc, uint JapaneseTargetPc, uint UsaTargetPc);

/// <summary>
/// Exact-source detached visual initializer/update, with no dispatch activation.
/// Excludes Japanese device gating, level11 height override, pickup/effects and
/// persistence. Model and code ownership do not prove native placement or RAM fit.
/// </summary>
public sealed class NativeJapaneseEggVisualBehaviorPlan
{
    internal const uint JapaneseOverlayBase = 0x80084A28, UsaOverlayBase = 0x8007AA38;
    internal const uint JapaneseEntry = 0x8008DE1C, UsaContinuation = 0x80085780;
    private readonly byte[] _overlay, _originalOverlay, _usaExe;
    private readonly Dictionary<NativeAddressSpace, byte[]> _sources = new(ReferenceEqualityComparer.Instance);
    private readonly NativeJapaneseEggModelAssetPlan _asset;
    internal static readonly (uint Pc, int Length, string Hash)[] FragmentSpecs =
    [
        (0x8008DE1C, 0x10, "489e0bafad1196885277e64e10780f34ae57b59867bedd92dbfc38a54ac999ca"),
        (0x8008DEC4, 0x0C, "ce7bbcdaa907a872683388d05de3d066a5cde841954832595d6c748a584a6bfb"),
        (0x8008DEE4, 0x80, "1ce7bed9b67aa42553c3c31a78282fd6431da05cda08a7622fe189cdbe06894b")
    ];
    private NativeJapaneseEggVisualBehaviorPlan(NativeJapaneseEggModelAssetPlan asset,
        NativeJapaneseEggModelAssemblyPlan model, byte[] jpOverlay, byte[] usaOverlay, byte[] jpExe, byte[] usaExe, bool certified)
    {
        if (asset.SourceCertified != certified || model.SourceCertified != certified)
            throw new InvalidDataException("Visual behavior cannot relabel another source/fixture ancestry.");
        _asset = asset; ModelAssembly = model; SourceCertified = certified;
        _originalOverlay = usaOverlay.ToArray(); _usaExe = usaExe.ToArray();
        var jd = Domain("JP", "overlay", jpOverlay, NativeAddressKind.LevelData);
        var ud = Domain("USA", "overlay", usaOverlay, NativeAddressKind.LevelData);
        var je = Domain("JP", "exe", jpExe, NativeAddressKind.ExecutableFile);
        var ue = Domain("USA", "exe", usaExe, NativeAddressKind.ExecutableFile);
        List<NativeJapaneseEggBehaviorEvidence> evidence = [];
        List<NativeMobyDependencyRead> reads = [];
        NativeMobyDependencyRead Read(string id, string region, NativeAddressSpace d, int offset, int length, string hash)
        {
            if (Hash(_sources[d].AsSpan(offset, length)) != hash) throw new InvalidDataException("Visual native evidence changed: " + id);
            evidence.Add(new(id, region, offset, length, hash));
            var r = new NativeMobyDependencyRead(id, new(d, offset, length), hash); reads.Add(r); return r;
        }
        var fragments = FragmentSpecs.Select((s, i) => new NativeMipsBehaviorFragmentSource("visual-" + i, s.Pc,
            Read("jp-fragment-" + i, "JP", jd, (int)(s.Pc - JapaneseOverlayBase), s.Length, s.Hash))).ToArray();
        Package = NativeMipsBehaviorFragmentCodec.Capture(fragments, [JapaneseEntry], [0x8008DE2C, 0x8008DED0, 0x8008DF64], ReadSource);
        Calls = Array.AsReadOnly(new NativeJapaneseEggVisualCall[]
        {
            new("sine", 0x8008DEF0, 0x8001623C, 0x80016C58),
            new("random", 0x8008DF2C, 0x800675A8, 0x8006272C),
            new("random", 0x8008DF3C, 0x800675A8, 0x8006272C),
            new("turn", 0x8008DF5C, 0x8003C4B4, 0x80038EE0)
        });
        if (Package.InstructionCount != 39 || Package.Transfers.Count != 9 || Package.StackAccesses.Count != 1 ||
            Package.StackAccesses[0] != new NativeMipsBehaviorStackAccess(0x8008DF54, 0x10, 4, false) ||
            !Package.Transfers.Where(t => t.Kind == NativeMipsBehaviorTransferKind.Call26)
                .All(t => Calls.Any(c => c.SourceCallPc == t.SourcePc && c.JapaneseTargetPc == t.TargetPc)))
            throw new InvalidDataException("Visual fragment edges or direct outgoing stack ABI changed.");
        if (certified)
        {
            RequireHash(jpOverlay, "bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2");
            RequireHash(usaOverlay, "7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55");
            RequireHash(jpExe, "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4");
            RequireHash(usaExe, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
            foreach (var s in new (string Id, NativeAddressSpace Domain, int Offset, int Length, string Hash)[]
            {
                ("jp-full-f5", jd, 0x93F4, 0x2CC, "9ce284e21b76b118f3ed22c328e52ca77026ea1c8ecea400d5b0766ca15692da"),
                ("jp-dispatch", jd, 0x34D8, 0x68, "2b43676644dbe1242980cbfd349b519924ab424b754b67c727609876c0cd3aa5"),
                ("jp-f5-slot", jd, 0x3F4, 4, "1a996ebeaa767031fab8e40d62fda82930005b6facf2afef1aff23f4125b3ebd"),
                ("usa-update-dispatch", ud, 0x3064, 0x268, "dc92ae336bbacdaf983be2d418820d894c74e8165f0a93a7858e7dd9afaff5a7"),
                ("usa-update-prologue", ud, 0x3000, 0x64, "7d531ec1116f4d20f5dfd2e769f5da3cd9facf7c1288c4a61729e9e0acf1745e"),
                ("usa-continuation", ud, 0xAD48, 0x4C, "6184b1194497b8c179effe34b9de247ad493df9698c991c4758f05b48138e169"),
                ("usa-dynamic-dispatch", ud, 0xAE1C, 0x134, "054ccd57ef717942731f0558b57cb70ffa07e04f7ea855c1f8cb83c4f8b98adf"),
                ("usa-dynamic-default", ud, 0xB69C, 0x34, "d0fdb4f986a005106f54b18de70c41f481b7c538916d99ea0f1da766f56f92ed"),
                ("jp-sine", je, 0x6A3C, 0x58, "89e1b79f47b38fb3233f5256a40ec0805dedfb4dbcb9058783f5303025924a47"),
                ("usa-sine", ue, 0x7458, 0x58, "a06a8bd2545b48c22353bfbe1ed13f86536e47d9a775dbb4f11b2a2f95a540f2"),
                ("jp-random", je, 0x57DA8, 0x30, "636e2e9d37ffad425186742355b4ff94b08ff3082645004dd99aad89dcc5369b"),
                ("usa-random", ue, 0x52F2C, 0x30, "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
                ("jp-turn", je, 0x2CCB4, 0xE8, "b0b07bf667a66addb83e532814569057b7cff006c006296c9a8586958e439ee9"),
                ("usa-turn", ue, 0x296E0, 0xE8, "d58397320748b47f903d597b0b66e3b56397cad4530443b65c82123e739d63b4"),
                ("jp-sine-table", je, 0x65DAC, 0x202, "8c9989b4ded9012234f3b6a3d7d86c70b6633147ca3c41422e75c2e2d4af7803"),
                ("usa-sine-table", ue, 0x5D3F8, 0x202, "8c9989b4ded9012234f3b6a3d7d86c70b6633147ca3c41422e75c2e2d4af7803"),
                ("jp-angle-delta", je, 0x76EC, 0x88, "cde33c4167af361eeecef5ce94f067205cf7baa5f6aa53db728e4836edb26057"),
                ("usa-angle-delta", ue, 0x8108, 0x88, "cde33c4167af361eeecef5ce94f067205cf7baa5f6aa53db728e4836edb26057"),
                ("jp-angle-step", je, 0x77D4, 0x48, "719a346d07b7b2e900b2e1a4adc264d599d7e42fcf2eaa371787e078e732399e"),
                ("usa-angle-step", ue, 0x81F0, 0x48, "719a346d07b7b2e900b2e1a4adc264d599d7e42fcf2eaa371787e078e732399e")
            }) Read(s.Id, s.Id.StartsWith("jp-") ? "JP" : "USA", s.Domain, s.Offset, s.Length, s.Hash);
            Differences(0x6A3C, 0x7458, 0x58, [8]);
            Differences(0x57DA8, 0x52F2C, 0x30, [4, 8, 0x1C, 0x20]);
            Differences(0x2CCB4, 0x296E0, 0xE8, [0x34, 0x40, 0x44, 0x58, 0x90, 0x9C, 0xB4]);
        }
        Evidence = evidence.AsReadOnly();
        int mainLength = Package.Fragments.Sum(f => f.ByteLength + (f.HasFallthrough ? 8 : 0));
        var sizing = NativeMipsOwnedFrameAdapter.Create(JapaneseEntry, UsaContinuation);
        Layout = NativeOwnedOverlayAppendLayout.Create("detached-jp-f5-visual:" + Hash(usaOverlay), usaOverlay,
            UsaOverlayBase, usaOverlay.Length + NativeOwnedOverlayMaterializer.MaximumAppendByteLength,
            [new("visual-main", mainLength), new("enter-initializer", 8), new("enter-state", 8),
                new("usa-sine", 8), new("usa-random", 8), new("usa-turn", 8),
                new("restore-frame", sizing.CopyExit().Length), new("owned-entry", sizing.CopyEntry().Length)], "alignment");
        var blocks = Layout.Blocks.Where(b => !b.IsPadding).ToDictionary(b => b.Id, b => b.Owner);
        uint Pc(NativeByteExtent e) => checked(UsaOverlayBase + (uint)e.Offset);
        NativeByteExtent Entry(string id) => new(Layout.Domain, blocks[id].Offset, 4);
        uint Mapped(uint pc)
        {
            int offset = 0;
            foreach (var f in Package.Fragments)
            {
                if (pc >= f.Source.SourcePc && pc < f.Source.SourcePc + f.ByteLength)
                    return Pc(blocks["visual-main"]) + (uint)offset + pc - f.Source.SourcePc;
                offset += f.ByteLength + (f.HasFallthrough ? 8 : 0);
            }
            throw new InvalidDataException("Unowned visual entry.");
        }
        List<NativeMipsBehaviorExternalBinding> bindings = [];
        foreach (var t in Package.Transfers)
        {
            if (t.TargetPc is uint target && Package.OwnsPc(target)) continue;
            string id = t.SourcePc switch
            {
                0x8008DE2C => "enter-initializer", 0x8008DED0 => "enter-state", 0x8008DF64 => "restore-frame",
                _ => "usa-" + Calls.Single(c => c.SourceCallPc == t.SourcePc && c.JapaneseTargetPc == t.TargetPc).Id
            };
            var entry = Entry(id); bindings.Add(new(t.SourcePc, t.TargetPc, Pc(entry), entry));
        }
        Bindings = bindings.AsReadOnly();
        Linked = Package.Link(new(Layout.Domain, blocks["visual-main"].Offset, Layout.Arena.EndExclusive - blocks["visual-main"].Offset),
            Pc(blocks["visual-main"]), Bindings);
        var adapter = NativeMipsOwnedFrameAdapter.Create(Mapped(JapaneseEntry), UsaContinuation);
        var payloads = new List<NativeOwnedOverlayBlock>
        {
            new("visual-main", blocks["visual-main"], Linked.CopyBytes()),
            new("enter-initializer", blocks["enter-initializer"], Jump(Mapped(0x8008DEC4))),
            new("enter-state", blocks["enter-state"], Jump(Mapped(0x8008DEE4))),
            new("restore-frame", blocks["restore-frame"], adapter.CopyExit()),
            new("owned-entry", blocks["owned-entry"], adapter.CopyEntry())
        };
        // New Final bridge instructions carry the privately pinned USA targets.
        // The general codec still refuses rebinding an Original JP address to
        // a different Original routine. No such guard was relaxed or bypassed.
        foreach (var call in Calls.DistinctBy(c => c.Id)) payloads.Add(new("usa-" + call.Id, blocks["usa-" + call.Id], Jump(call.UsaTargetPc)));
        List<NativeOwnedOverlayEntry> entries = blocks.Keys.Where(id => id != "visual-main").Select(id => new NativeOwnedOverlayEntry(id, id, Entry(id))).ToList();
        foreach (uint pc in new uint[] { JapaneseEntry, 0x8008DEC4, 0x8008DEE4, 0x8008DEEC })
            entries.Add(new("source-entry-" + pc.ToString("X"), "visual-main", new(Layout.Domain, Mapped(pc) - UsaOverlayBase, 4)));
        _overlay = Layout.Materialize(payloads, entries, Linked.UnmaterializedExternalTargets);
        EntryPc = Pc(Entry("owned-entry"));
        CodeOwners = Array.AsReadOnly(Layout.Blocks.Select(b => new NativeJapaneseEggVisualCodeOwner(b.Id, (int)b.Owner.Offset,
            Pc(b.Owner), (int)b.Owner.ByteLength, Hash(_overlay.AsSpan((int)b.Owner.Offset, (int)b.Owner.ByteLength)))).ToArray());
        var prefix = new NativeMobyDependencyRead("original-usa-overlay", new(ud, 0, usaOverlay.Length), Hash(usaOverlay));
        var nodes = asset.PackageNodes(new(model.FinalDomain, model.PackageOffset, model.PackageByteLength)).ToList();
        nodes.Add(new("f5.visual.regional-evidence", NativeMobyDependencyKind.NativeRoutine, NativeMobyDependencyResolution.Aggregate,
            NativeMobyDependencySharing.Immutable, Hash(System.Text.Encoding.UTF8.GetBytes(string.Join('|', reads.Select(r => r.ExpectedSha256)))), [], reads));
        nodes.Add(new("f5.visual.retained-overlay", NativeMobyDependencyKind.RuntimeBuffer, NativeMobyDependencyResolution.DestinationResident,
            NativeMobyDependencySharing.Immutable, prefix.ExpectedSha256, [], [prefix],
            Preserved: [new(prefix.Id, 0, new(Layout.Domain, 0, usaOverlay.Length))]));
        nodes.Add(new("f5.visual.detached-code", NativeMobyDependencyKind.Behavior, NativeMobyDependencyResolution.ImportFromSource,
            NativeMobyDependencySharing.Immutable, Hash(_overlay.AsSpan(usaOverlay.Length)),
            [NativeJapaneseEggModelAssetPlan.PackageNodeId, "f5.visual.regional-evidence", "f5.visual.retained-overlay"],
            fragments.Select(f => f.Read).ToArray(), _ => new(_overlay.AsSpan(usaOverlay.Length), [new("unactivated-code", Layout.Arena)], [])));
        Graph = NativeMobyDependencyClosureCompiler.Compile(nodes, ["f5.visual.detached-code"], ReadSource);
        Graph.RequirePreservedFinalBytes(Layout.Domain, _overlay, ReadSource);
        if (!_overlay.AsSpan(0, usaOverlay.Length).SequenceEqual(usaOverlay) ||
            !Graph.CopyPayload("f5.visual.detached-code").AsSpan().SequenceEqual(_overlay.AsSpan(usaOverlay.Length)) ||
            !Graph.CopyPayload(NativeJapaneseEggModelAssetPlan.PackageNodeId).SequenceEqual(model.CopyModelPackage()))
            throw new InvalidDataException("Detached visual graph changed an original hook/complement or model owner.");
        OverlaySha256 = Hash(_overlay);
        void Differences(int j, int u, int n, int[] expected)
        {
            var actual = Enumerable.Range(0, n / 4).Select(i => i * 4).Where(i => Word(jpExe, j + i) != Word(usaExe, u + i));
            if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Regional visual helper differs beyond the pinned native bindings.");
        }
    }
    public bool SourceCertified { get; }
    public NativeJapaneseEggModelAssemblyPlan ModelAssembly { get; }
    public IReadOnlyList<NativeJapaneseEggBehaviorEvidence> Evidence { get; }
    public IReadOnlyList<NativeJapaneseEggVisualCall> Calls { get; }
    public IReadOnlyList<NativeJapaneseEggVisualCodeOwner> CodeOwners { get; }
    public uint EntryPc { get; }
    public string OverlaySha256 { get; }
    public string DependencyGraphSha256 => Graph.Fingerprint;
    public bool DetachedCodeOwnersComplete => true;
    public bool DispatchActivated => false;
    public bool RuntimeMemoryFitProven => false;
    public bool NativeImportApproved => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<string> UnresolvedObligations { get; } = Array.AsReadOnly(new[]
    {
        "Reserve actual combined overlay/model/scene/copy-buffer RAM and 0x2B0 adapter plus 0x28 helper stack, before activation.",
        "Own the USA compare-chain hook and exact continuation; this detached overlay leaves every original dispatch byte unchanged.",
        "Admit a native row and exactly 12 private bytes with zero phase/target/state; no dynamic-spawn constructor is imported.",
        "Bind the native render/animation/draw-list and USA appearance profile/normal owners; model geometry alone does not prove visible color.",
        "Bind independently owned per-instance pickup/retirement, inventory, 100-percent requirement and persistent save extension.",
        "Prove candidate readback, cold boot, two same-level colors, LAB65, pickup, reentry/death and save/load behavior."
    });
    public byte[] CopyDetachedOverlay() => _overlay.ToArray();
    public byte[] CopyOriginalOverlay() => _originalOverlay.ToArray();
    public byte[] RevertOverlayExact(ReadOnlySpan<byte> afterimage)
    { if (!afterimage.SequenceEqual(_overlay)) throw new InvalidDataException("Visual inverse needs the complete exact detached afterimage."); return _originalOverlay.ToArray(); }
    internal NativeMipsBehaviorFragmentPackage Package { get; }
    internal NativeMipsBehaviorLinkedCode Linked { get; }
    internal IReadOnlyList<NativeMipsBehaviorExternalBinding> Bindings { get; }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal NativeMobyDependencyClosurePlan Graph { get; }
    internal byte[] CopyUsaExecutable() => _usaExe.ToArray();
    private NativeAddressSpace Domain(string region, string id, byte[] bytes, NativeAddressKind kind)
    {
        var d = new NativeAddressSpace(kind, (SourceCertified ? "source:" : "fixture:") + region + ":" + id + ":" + Hash(bytes),
            NativeAddressRevision.Original, Math.Max(1, bytes.Length)); _sources.Add(d, bytes.ToArray()); return d;
    }
    private byte[] ReadSource(NativeByteExtent e) => _sources.TryGetValue(e.Space, out var b)
        ? b.AsSpan((int)e.Offset, (int)e.ByteLength).ToArray() : _asset.ReadSource(e);
    public static NativeJapaneseEggVisualBehaviorPlan Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa,
        NativeJapaneseEggModelAssetPlan asset)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa); ArgumentNullException.ThrowIfNull(asset);
        if (japan.Revision != NativeDiscRevision.JapanScps10083 || usa.Revision != NativeDiscRevision.UsaScus94228 || !asset.SourceCertified ||
            asset.JapaneseSourceImageSha256 != japan.SourceImageSha256 || asset.UsaSourceImageSha256 != usa.SourceImageSha256)
            throw new InvalidDataException("Visual binding requires the authenticated JP model issuer and exact separate regional sources.");
        var model = asset.AssembleOriginalUsaResource(usa, NativeJapaneseEggUsaResource.Artisans);
        var result = new NativeJapaneseEggVisualBehaviorPlan(asset, model, japan.ReadWadEntry(22), usa.ReadWadEntry(9),
            japan.ReadExecutableRange(0, japan.ExecutableByteLength), usa.ReadExecutableRange(0, usa.ExecutableByteLength), true);
        japan.VerifyUnchanged(); usa.VerifyUnchanged(); return result;
    }
    internal static NativeJapaneseEggVisualBehaviorPlan Fixture(NativeJapaneseEggModelAssetPlan asset, byte[] currentModel,
        byte[] jpOverlay, byte[] usaOverlay, byte[] usaExe) => new(asset,
            NativeJapaneseEggModelAssemblyPlan.Compile(asset, currentModel, -1, false), jpOverlay, usaOverlay, [], usaExe, false);
    private static byte[] Jump(uint pc)
    { byte[] b = new byte[8]; BinaryPrimitives.WriteUInt32LittleEndian(b, 0x08000000 | ((pc >> 2) & 0x3FFFFFF)); return b; }
    internal static uint Word(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
    internal static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void RequireHash(byte[] b, string hash)
    { if (Hash(b) != hash) throw new InvalidDataException("Complete visual source owner identity changed."); }
}
