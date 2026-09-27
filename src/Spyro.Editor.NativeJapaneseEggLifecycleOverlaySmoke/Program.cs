using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = AttemptFixtures;

int checks = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
void Refuse(Action action)
{ try { action(); } catch (InvalidDataException) { checks++; return; } throw new Exception("Expected refusal"); }
uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
const uint OverlayBase = 0x8007AA38, PreEdge = 0x80088C3C, PostEdge = 0x80088AE4;
uint Jump(uint pc) => 0x08000000u | ((pc >> 2) & 0x03FFFFFF);
var manifest = F.Manifest(257);
NativeJapaneseEggCollectibleIdentity[] mappings = [new("egg-a", F.Id(257)), new("egg-b", F.Id(256))];

void Verify(NativeJapaneseEggAuthoredClassesPlan authored, bool source,
    Func<DetachedCollectibleManifest, int, NativeJapaneseEggCollectibleIdentity[], NativeJapaneseEggLifecycleOverlayPlan> build,
    Action<NativeJapaneseEggLifecycleOverlayPlan>? startupChecks = null)
{
    byte[] predecessorOverlay = authored.CopyOverlay(), predecessorExe = authored.CopyExecutable(), predecessorData = authored.CopyLevelData();
    byte[] predecessorScene = authored.CopyScene(), manifestBytes = manifest.SerializeCanonical();
    var plan = build(manifest, 1, mappings);
    byte[] overlay = plan.CopyOverlay(), exe = plan.CopyExecutable(), data = plan.CopyLevelData();
    Check(plan.SourceCertified == source && plan.Projection.SourceCertified == source && plan.Binding.SourceCertified == source && plan.Predicate.SourceBound == source &&
        plan.Attempt.SourceBound == source && plan.Lifecycle.SourceBound == source, "Fixture and source ancestry remain distinct throughout the callable chain.");
    Check(!plan.PublicationApproved && !plan.NativeResidenceIssued && !plan.DispatchActivated && plan.DispatchInstalledInDetachedOverlay,
        "Detached overlay installation grants no native residence, dispatch activation, or publication authority.");
    Check(predecessorOverlay.Length == 0xE800 && overlay.Length > 0xE800 && overlay.Length % 0x800 == 0,
        "Successor appends beyond the complete sector-aligned E800 predecessor.");
    Check(plan.Layout.RetainedByteLength == predecessorOverlay.Length && plan.Layout.CopyRetainedPrefix().SequenceEqual(predecessorOverlay) &&
        plan.Layout.FinalByteLength == overlay.Length, "Retained allocation includes the complete original code and zero padding.");
    bool EdgeByte(int i) => i >= PreEdge - OverlayBase && i < PreEdge - OverlayBase + 8 ||
        i >= PostEdge - OverlayBase && i < PostEdge - OverlayBase + 8;
    Check(Enumerable.Range(0, predecessorOverlay.Length).Where(i => !EdgeByte(i)).All(i => overlay[i] == predecessorOverlay[i]),
        "Every predecessor-overlay byte outside the two complete J/NOP edges is preserved.");
    Check(Word(overlay, (int)(PreEdge - OverlayBase)) == Jump(plan.PreBridgePc) && Word(overlay, (int)(PreEdge - OverlayBase) + 4) == 0 &&
        Word(overlay, (int)(PostEdge - OverlayBase)) == Jump(plan.PostBridgePc) && Word(overlay, (int)(PostEdge - OverlayBase) + 4) == 0,
        "Only the exact owned previsual and postvisual edges enter the newly owned bridges.");
    long cursor = predecessorOverlay.Length;
    Check(plan.Layout.Blocks.Select(b => b.Id).Distinct().Count() == plan.Layout.Blocks.Count, "Every append owner has a unique name.");
    foreach (var block in plan.Layout.Blocks.OrderBy(b => b.Owner.Offset))
    {
        Check(block.Owner.Offset == cursor && block.Owner.ByteLength > 0 && block.Owner.Offset % 4 == 0 &&
            ReferenceEquals(block.Owner.Space, plan.Layout.Domain), "Append coverage is contiguous, aligned, and in its final domain.");
        if (block.IsPadding) Check(overlay.AsSpan((int)block.Owner.Offset, (int)block.Owner.ByteLength).ToArray().All(b => b == 0), "Only explicit padding is zero filled.");
        cursor = block.Owner.EndExclusive;
    }
    Check(cursor == overlay.Length, "Named append ownership covers the complete padded suffix.");
    void CodeOwner(uint pc, NativeByteExtent owner, byte[] bytes, string role)
    {
        Check(pc == OverlayBase + owner.Offset && ReferenceEquals(owner.Space, plan.Layout.Domain) && owner.Offset >= 0xE800 &&
            owner.ByteLength == bytes.Length && overlay.AsSpan((int)owner.Offset, bytes.Length).SequenceEqual(bytes), role + " uses final owned overlay bytes and PC.");
        Check(plan.Layout.Blocks.Count(b => !b.IsPadding && b.Owner.Offset == owner.Offset && b.Owner.ByteLength >= owner.ByteLength) == 1,
            role + " occupies one named callable block.");
    }
    CodeOwner(plan.Predicate.RuntimeBase, plan.Predicate.Owner, plan.Predicate.CopyBytes(), "predicate");
    CodeOwner(plan.Collector.EntryPc, plan.Collector.Owner, plan.Collector.CopyBytes(), "collector");
    CodeOwner(plan.Query.EntryPc, plan.Query.Owner, plan.Query.CopyBytes(), "query");
    CodeOwner(plan.Attempt.EntryPc, plan.Attempt.Owner, plan.Attempt.CopyBytes(), "attempt");
    CodeOwner(plan.Lifecycle.BeforeVisualEntryPc, plan.Lifecycle.Owner, plan.Lifecycle.CopyBytes(), "lifecycle");
    foreach (uint bridge in new[] { plan.PreBridgePc, plan.PostBridgePc })
        Check(plan.Layout.Blocks.Count(b => !b.IsPadding && OverlayBase + b.Owner.Offset == bridge) == 1, "Each bridge starts its uniquely owned append block.");
    Check(plan.Attempt.PredicateEntryPc == plan.Predicate.EntryPc && plan.Attempt.CollectionEntryPc == plan.Collector.EntryPc &&
        ReferenceEquals(plan.Attempt.Binding, plan.Binding) && ReferenceEquals(plan.Lifecycle.Attempt, plan.Attempt), "Callable dependencies use this final compilation.");

    uint delta = checked((uint)(overlay.Length - predecessorOverlay.Length));
    Check(plan.Projection.CopyAnchor == plan.Layout.CopyAnchor && plan.Layout.CopyAnchor == OverlayBase + overlay.Length &&
        plan.Projection.Delta == delta && plan.Projection.CopyAnchor == authored.Integration.FinalCopyBufferAddress + delta,
        "Final copy anchor and projection delta derive from the complete grown overlay.");
    Check(plan.Projection.ModelAddress == authored.Projection.ProjectedModelRamStart + delta &&
        plan.Projection.SceneAddress == authored.Projection.ProjectedSceneRamStart + delta &&
        plan.Projection.SceneEnd == authored.Projection.ProjectedSceneReadEnd + delta &&
        plan.Projection.NormalGap == authored.Projection.ProjectedNormalGap - delta,
        "Models, scene, scene end and remaining normal gap track the same exact overlay growth.");
    Check(data.SequenceEqual(predecessorData) && data.AsSpan((int)Word(data, 0x18), predecessorScene.Length).SequenceEqual(predecessorScene),
        "Complete disk level data, source-relative model pointers, scene pointers, rows and private data remain identical.");
    Check(exe.Length == predecessorExe.Length && Enumerable.Range(0, exe.Length).Where(i => i < 0x4AF18 || i >= 0x4AF20).All(i => exe[i] == predecessorExe[i]),
        "Executable complement is unchanged outside the owned eight-byte LUI/ADDIU pair.");
    uint anchor = plan.Projection.CopyAnchor;
    Check(Word(exe, 0x4AF18) == (0x3C020000u | ((anchor + 0x8000u) >> 16)) && Word(exe, 0x4AF1C) == (0x24420000u | (anchor & 0xFFFF)),
        "Executable anchor uses the signed-low LUI/ADDIU encoding.");
    Check(unchecked((Word(exe, 0x4AF18) << 16) + (uint)(int)(short)Word(exe, 0x4AF1C)) == anchor, "Independent instruction decode recovers the exact final anchor.");
    Check(plan.Binding.ModelAddress == plan.Projection.ModelAddress && plan.Binding.SceneAddress == plan.Projection.SceneAddress,
        "Final collectible binding uses the grown-overlay projection.");
    foreach (var row in plan.Binding.Rows)
    {
        var old = authored.Bindings.Single(b => b.RowId == row.RowId);
        var model = plan.Projection.Models.Single(b => b.RowId == row.RowId);
        Check(row.RowAddress == old.ProjectedRowAddress + delta && row.PrivateAddress == old.ProjectedPrivateAddress + delta &&
            model.RowAddress == row.RowAddress && model.PrivateAddress == row.PrivateAddress && model.RootAddress == old.ProjectedRoot + delta &&
            model.AuthoredClass == old.AuthoredDestinationClass && row.BitIndex == manifest.GetEntry(row.InstanceId).BitIndex,
            "Each runtime root, row, private block, class and full-width identity has its final projected owner.");
        Check(row.RowOwner.Offset == Word(data, 0x18) + old.RowSceneOffset && Word(data, (int)row.RowOwner.Offset) + plan.Projection.SceneAddress == row.PrivateAddress,
            "Disk extents stay put while source-relative private pointers resolve at the new scene base.");
        foreach (var field in model.LoaderFields)
        {
            var oldField = old.LoaderFields.Single(f => f.PackageFieldOffset == field.PackageFieldOffset);
            Check(field.SourceWord == oldField.SourceWord && field.RuntimeTargetAddress == oldField.RuntimeTargetAddress + delta &&
                Word(data, old.PackageDataOffset + field.PackageFieldOffset) == field.SourceWord,
                "Loader projections move their targets without pre-rebasing a disk-form package.");
        }
    }
    Check(plan.Lifecycle.DynamicBase == plan.Binding.Rows.Max(r => r.RowAddress) + 0x58,
        "Lifecycle dynamic boundary is rebuilt from final projected rows.");
    Check(plan.RevertOverlayExact(overlay).SequenceEqual(predecessorOverlay) && plan.RevertExecutableExact(exe).SequenceEqual(predecessorExe),
        "Exact inverses recover complete authored predecessors, including earlier modifications.");
    foreach (int at in new[] { 0, (int)(PreEdge - OverlayBase), (int)(PostEdge - OverlayBase), 0xE7FF, 0xE800, overlay.Length - 1 }.Distinct())
    { byte[] altered = overlay.ToArray(); altered[at] ^= 1; Refuse(() => plan.RevertOverlayExact(altered)); }
    foreach (int at in new[] { 0, 0x4AF18, 0x4AF1F, exe.Length - 1 })
    { byte[] altered = exe.ToArray(); altered[at] ^= 1; Refuse(() => plan.RevertExecutableExact(altered)); }
    Refuse(() => plan.RevertOverlayExact(overlay[..^1])); Refuse(() => plan.RevertExecutableExact(exe[..^1]));
    foreach (var (copy, again) in new (byte[], Func<byte[]>)[] {
        (plan.CopyOverlay(), plan.CopyOverlay), (plan.CopyExecutable(), plan.CopyExecutable), (plan.CopyLevelData(), plan.CopyLevelData),
        (plan.Layout.CopyRetainedPrefix(), plan.Layout.CopyRetainedPrefix), (plan.Predicate.CopyBytes(), plan.Predicate.CopyBytes),
        (plan.Collector.CopyBytes(), plan.Collector.CopyBytes), (plan.Query.CopyBytes(), plan.Query.CopyBytes),
        (plan.Attempt.CopyBytes(), plan.Attempt.CopyBytes), (plan.Lifecycle.CopyBytes(), plan.Lifecycle.CopyBytes) })
    { byte original = copy[0]; copy[0] ^= 1; Check(again()[0] == original, "Returned bytes cannot mutate retained plan state."); }
    var rebuilt = build(manifest, 1, mappings.Reverse().ToArray());
    Check(rebuilt.CopyOverlay().SequenceEqual(overlay) && rebuilt.CopyExecutable().SequenceEqual(exe) && rebuilt.Projection.PlanSha256 == plan.Projection.PlanSha256,
        "Mapping enumeration order does not change persistent identities or deterministic final bytes.");
    var nextManifest = manifest.Advance([F.Id(258)], []);
    plan.Projection.RequireCurrent(authored); plan.Binding.RequireCurrent(authored, manifest);
    Refuse(() => plan.Binding.RequireCurrent(authored, nextManifest));
    var differentManifest = build(nextManifest, 1, mappings);
    var differentSlot = build(manifest, 2, mappings);
    Check(differentManifest.Binding.ManifestSha256 == nextManifest.Sha256 && differentManifest.Collector.ManifestSha256 == nextManifest.Sha256 &&
        differentManifest.Query.ManifestSha256 == nextManifest.Sha256 && !differentManifest.CopyOverlay().SequenceEqual(overlay), "A successor manifest receives its own freshly compiled callable ancestry.");
    Check(differentSlot.Collector.OrdinarySlot == 2 && differentSlot.Query.OrdinarySlot == 2 && differentSlot.Attempt.OrdinarySlot == 2 &&
        !differentSlot.CopyOverlay().SequenceEqual(overlay), "A different ordinary slot is freshly compiled across every ledger consumer.");
    Refuse(() => build(manifest, -1, mappings)); Refuse(() => build(manifest, DetachedCollectibleSave.OrdinarySlotCount, mappings));
    Refuse(() => build(manifest, 1, mappings[..1]));
    Refuse(() => build(manifest, 1, [mappings[0], new("egg-b", mappings[0].InstanceId)]));
    Refuse(() => build(manifest.Advance([], [F.Id(257)]), 1, mappings));
    OverlayCpuProbe.Verify(plan, authored, manifest, Check);
    OverlayCpuProbe.VerifyLedgerAncestry(plan, differentManifest, differentSlot, authored, manifest, Check);
    startupChecks?.Invoke(plan);
    Check(authored.CopyOverlay().SequenceEqual(predecessorOverlay) && authored.CopyExecutable().SequenceEqual(predecessorExe) &&
        authored.CopyLevelData().SequenceEqual(predecessorData) && manifest.SerializeCanonical().SequenceEqual(manifestBytes), "No operation mutates authored ancestors or the supplied manifest.");
    Console.WriteLine($"LIFECYCLE_OVERLAY source={source} bytes={overlay.Length:X} delta={delta:X} anchor={anchor:X8} scene={plan.Projection.SceneAddress:X8} checks={checks}");
}

var fixture = F.Authored();
Verify(fixture, false, (m, s, ids) => NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, m, s, ids, F.Predicate()));
var fixturePlan = NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, manifest, 1, mappings, F.Predicate());
var movedFixture = F.Authored([new("egg-a",72804,47104,6656,32,1), new("egg-b",72704,49152,6656,96,1)]);
Refuse(() => fixturePlan.Projection.RequireCurrent(movedFixture));
Refuse(() => fixturePlan.Binding.RequireCurrent(movedFixture, manifest));
Refuse(() => NativeJapaneseEggCollectibleBinding.CreateProjected(movedFixture, manifest, mappings, fixturePlan.Projection));
var movedPlan = NativeJapaneseEggLifecycleOverlayPlan.Fixture(movedFixture, manifest, 1, mappings, F.Predicate());
Check(movedPlan.Projection.PlanSha256 != fixturePlan.Projection.PlanSha256 && movedPlan.CopyLevelData().SequenceEqual(movedFixture.CopyLevelData()),
    "Moved authored data requires a fresh exact projection and retains the new disk-form ancestor.");
StartupCompositionChecks.SourceFree(Check);
var maximumManifest = F.Manifest(1408);
var maximum = NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, maximumManifest, 0,
    [new("egg-a", F.Id(1408)), new("egg-b", F.Id(1407))], F.Predicate());
Check(maximum.CopyOverlay().Length <= 0x10000 && maximum.Binding.Rows.All(r => r.BitIndex >= 1406),
    "Maximum admitted lifetime ledger retains high IDs inside the bounded successor allocation.");
Refuse(() => NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, F.Manifest(1409), 0,
    [new("egg-a", F.Id(1409)), new("egg-b", F.Id(1408))], F.Predicate()));
byte[] wrongPrefix = fixture.CopyOverlay(); wrongPrefix[^1] ^= 1;
var wrongLayout = NativeOwnedOverlayAppendLayout.Create("changed-prefix", wrongPrefix, OverlayBase, 0x10000, [new("body",4)], "padding");
Refuse(() => NativeJapaneseEggOverlayProjection.Create(fixture, wrongLayout));
var wrongBase = NativeOwnedOverlayAppendLayout.Create("changed-base", fixture.CopyOverlay(), OverlayBase+4, 0x10000, [new("body",4)], "padding");
Refuse(() => NativeJapaneseEggOverlayProjection.Create(fixture, wrongBase));
Console.WriteLine($"JP_EGG_LIFECYCLE_OVERLAY_SOURCE_FREE_OK checks={checks} detached=true nativeResidence=false dispatchActivated=false publication=false");
if (args.Length == 0) return;
if (args.Length != 3 || args[0] != "--source") throw new ArgumentException("--source JP USA");
using var jp = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.JapanScps10083);
using var usa = NativeRegionalSourceSession.Open(args[2], NativeDiscRevision.UsaScus94228);
try
{
    var authored = F.Authored(jp, usa);
    Refuse(() => NativeJapaneseEggLifecycleOverlayPlan.Capture(jp, usa, fixture, manifest, 1, mappings));
    Refuse(() => NativeJapaneseEggLifecycleOverlayPlan.Fixture(authored, manifest, 1, mappings, F.Predicate()));
    Refuse(() => NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture, manifest, 1, mappings, NativeJapaneseEggPickupPredicateSource.Capture(jp, usa)));
    Verify(authored, true, (m, s, ids) => NativeJapaneseEggLifecycleOverlayPlan.Capture(jp, usa, authored, m, s, ids),
        plan => StartupCompositionChecks.Verify(usa, authored, plan, manifest, Check));
    Console.WriteLine($"JP_EGG_LIFECYCLE_OVERLAY_SOURCE_OK checks={checks} detached=true nativeResidence=false dispatchActivated=false publication=false");
}
finally { try { jp.VerifyUnchanged(); } finally { usa.VerifyUnchanged(); } }
