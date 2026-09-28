using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
void Refuse(Action action,string why)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or IOException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Check(typeof(NativeJapaneseEggAdmittedLifecycleResearchPlan).IsSealed &&
    typeof(NativeJapaneseEggAdmittedLifecycleResearchPlan).GetConstructors().Length == 0,"Closed plan constructor.");
Check(typeof(NativeJapaneseEggAdmittedLifecycleResearchPlan).GetMethod("CaptureAsync")!.GetParameters().Select(p => p.Name)
    .SequenceEqual(new[] { "japaneseImagePath","originalImagePath","originalCuePath","token" }),"Only exact regional sources, no bytes/manifest/slot input.");
Check(NativeJapaneseEggAdmittedLifecycleResearchPlan.Profile == "native-artisans-two-japanese-eggs-admitted-lifecycle-research-v1",
    "Fixed independent research profile.");
Check(!NativeJapaneseEggAdmittedLifecycleResearchPlan.OutputNames.Intersect(NativeJapaneseEggAuthoredResearchCandidatePlan.OutputNames
    .Concat(NativeCollectibleCardAdmissionResearchPlan.OutputNames).Concat(NativeCollectibleCardLoadResearchPlan.OutputNames)).Any(),
    "All previous artifact names protected.");
Check(typeof(NativeWizardResearchPublicationBinding).GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
    .Where(m => m.Name.StartsWith("From",StringComparison.Ordinal)).All(m => m.GetParameters().Length == 1 &&
        m.GetParameters()[0].ParameterType.IsSealed && !typeof(Delegate).IsAssignableFrom(m.GetParameters()[0].ParameterType)),"No raw physical publication binding.");
Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPlan.CaptureAsync("","missing","missing").GetAwaiter().GetResult(),"empty donor before I/O");
Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(null!,"unused").GetAwaiter().GetResult(),"null publisher");
Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.VerifyAsync(null!,"unused").GetAwaiter().GetResult(),"null verifier");
Refuse(() => NativeWizardResearchPublicationBinding.FromAdmittedJapaneseEgg(null!),"null typed binding");
Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchRenderBinding.Capture(null!,null!),"null current render binding");
Refuse(() => NativePackedResearchImageOwner.CaptureAdmittedEgg("unused",null!,null!),"null physical source owner");
Refuse(() => NativePackedResearchEntryComposer.ComposeAdmittedArtisans(new byte[0x800],0x1000,null!,null!,(_,_) => throw new Exception("Unexpected read")),
    "null entry source owner");
using (var cancel = new CancellationTokenSource())
{
    cancel.Cancel();
    try { await NativeJapaneseEggAdmittedLifecycleResearchPlan.CaptureAsync("missing","missing","missing",cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
}
if (args.Length == 0)
{
    Console.WriteLine($"JP_EGG_ADMITTED_RESEARCH_SOURCE_FREE_OK checks={checks} sourceReads=0 writes=0"); return;
}
if (args.Length is not (4 or 5) || args[0] is not ("--source" or "--describe" or "--publication-faults" or "--verify" or "--publish") ||
    (args[0] is "--source" or "--describe") != (args.Length == 4))
    throw new ArgumentException("Usage: no args | --source|--describe JP.bin USA.bin USA.cue | --publication-faults|--verify|--publish JP.bin USA.bin USA.cue directory. Publication/faults require explicit NEW directories.");
var plan = await NativeJapaneseEggAdmittedLifecycleResearchPlan.CaptureAsync(args[1],args[2],args[3]);
var p = plan.Composition; var render = plan.FinalRenderBinding; var binding = NativeWizardResearchPublicationBinding.FromAdmittedJapaneseEgg(plan);
Check(binding.JapaneseSourceImagePath == plan.JapaneseSourceImagePath && binding.SourceImagePath == plan.SourceImagePath &&
    binding.SourceCuePath == plan.SourceCuePath && binding.PlanSha256 == plan.PlanSha256,"All exact input source identities retained.");
Check(binding.OutputNames.SequenceEqual(NativeJapaneseEggAdmittedLifecycleResearchPlan.OutputNames) &&
    binding.StagePrefix == ".native-japanese-eggs-admitted-lifecycle-stage-" &&
    binding.LeaseName == ".native-japanese-eggs-admitted-lifecycle.writer.lock","Dedicated output/lease/staging names.");
foreach (string sourceOwner in new[] { plan.SourceImagePath,plan.SourceCuePath,Path.GetDirectoryName(plan.JapaneseSourceImagePath)!,
    Path.Combine(Path.GetDirectoryName(plan.JapaneseSourceImagePath)!,".",Path.GetFileName(plan.JapaneseSourceImagePath)) })
    Refuse(() => NativeJapaneseEggAdmittedLifecycleResearchPublisher.VerifyAsync(plan,sourceOwner).GetAwaiter().GetResult(),
        "Actual shared resolver refuses USA/CUE/JP parent and alias overlap");
Check(plan.ResearchOnly && plan.ResearchDiscoveryPublicationEnabled && plan.GuardedReadyAdmissionEmitted && plan.GuardedCollectionEmitted &&
    !plan.RuntimeVerified && !plan.ReadyStateIssued && !plan.NativeInstallationApproved && !plan.LifetimeProven &&
    !plan.InterruptSafetyProven && !plan.WholeEngineFitProven && !plan.CardWrites && !plan.PersistentSaveChanges && !plan.NormalCreateBinApproved,
    "Emitted collection/admission is not runtime, residence, IRQ, save or normal-export approval.");
Refuse(plan.RequireNativeInstallation,"product gate stays closed"); render.RequireCurrent(p); checks++;
var sameBytesNewOwner = NativeJapaneseEggAdmittedLifecyclePlan.Compose(p.Admission,p.Authored,p.Lifecycle);
Refuse(() => render.RequireCurrent(sameBytesNewOwner),"final-render owner cannot be reissued from identical metadata");
Refuse(() => NativePackedResearchImageOwner.CaptureAdmittedEgg("unused",sameBytesNewOwner,render),"cross-owner render evidence refused before I/O");
byte[] executable = plan.CopyExecutable(), original = p.CopyOriginalExecutable(), card = p.CopyCardOverlay(), artisans = p.CopyArtisansOverlay(), data = p.CopyLevelData();
Check(plan.RevertExecutableExact(executable).SequenceEqual(original),"Complete merged EXE inverse.");
Check(p.RevertCardOverlayExact(card).SequenceEqual(p.Admission.CopyOriginalOverlay()) &&
    p.RevertArtisansOverlayExact(artisans).SequenceEqual(p.Authored.Integration.CopyOriginalOverlay()) &&
    p.RevertLevelDataExact(data).SequenceEqual(p.Authored.Integration.CopyOriginalLevelData()),"All three complete original WAD inverses.");
Check(data.SequenceEqual(p.Authored.CopyLevelData()),"Final guarded lifecycle does not rewrite original-relative authored data.");
foreach (int at in new[] { 0,0x10,0x2F00,0x3030,0x1EAFC,0x23E24,0x4AF18,0x4AF20,0x65DA4,0x65E40,0x65FFF })
{ byte[] bad = executable.ToArray(); bad[at] ^= 1; Refuse(() => plan.RevertExecutableExact(bad),"EXE exact owner/complement mutation"); }
foreach (var pair in new[] { (card,(Action<byte[]>)(b => p.RevertCardOverlayExact(b))),
    (artisans,(Action<byte[]>)(b => p.RevertArtisansOverlayExact(b))),(data,(Action<byte[]>)(b => p.RevertLevelDataExact(b))) })
    foreach (int at in new[] { 0,pair.Item1.Length/2,pair.Item1.Length-1 })
    { byte[] bad = pair.Item1.ToArray(); bad[at] ^= 1; Refuse(() => pair.Item2(bad),"Three-source inverse owner/complement mutation"); }
Check(p.EntryOwners.Select(e => e.Index).SequenceEqual(new[] {2,9,10}),"Exactly three typed entry owners.");
int growth = checked(p.EntryOwners.Sum(e => e.FinalLength-e.OriginalLength));
Check(growth == plan.GrowthBytes && plan.RelocatedExecutableLba == 53875+growth/0x800 &&
    plan.FinalExecutableEndLba == plan.RelocatedExecutableLba+executable.Length/0x800,"Relocation derived from actual verified owner deltas.");
Check(artisans.Length == p.Lifecycle.Layout.FinalByteLength && artisans.Length <= 0x10000 &&
    p.Lifecycle.Layout.CopyAnchor == 0x8007AA38+artisans.Length,"Final guarded overlay capacity charged before projection.");
foreach (var row in render.Rows)
{
    var current = p.Lifecycle.Binding.Rows.Single(r => r.RowId == row.RowId);
    var model = p.Lifecycle.Projection.Models.Single(m => m.RowId == row.RowId);
    var old = render.SourceEvidence.Rows.Single(r => r.Id == row.RowId);
    Check(row.RowAddress == current.RowAddress && row.PrivateAddress == current.PrivateAddress && row.ModelRoot == model.RootAddress &&
        row.RowAddress != old.RowAddress && row.ModelRoot != old.ModelRoot,"Current final addresses, not ancestor render addresses.");
    Check(row.NativeRenderMode == 1 && row.AuthoredClass == current.AuthoredClass && row.LoaderFields.SequenceEqual(model.LoaderFields),
        "Current final class/mode/complete model loader projection.");
}
byte[] wad = plan.CopyWad(); int oldCursor = 0x800, newCursor = 0x800;
foreach (var entry in plan.Entries)
{
    Check(entry.OriginalOffset == oldCursor && entry.FinalOffset == newCursor,"Complete packed directory coverage.");
    Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8)) == entry.FinalOffset &&
        BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8+4)) == entry.FinalLength &&
        Hash(wad.AsSpan(entry.FinalOffset,entry.FinalLength)) == entry.FinalSha256,"Actual packed owner directory and complete bytes.");
    if (entry.Index is not (2 or 9 or 10)) Check(entry.OriginalLength == entry.FinalLength && entry.OriginalSha256 == entry.FinalSha256,
        "Every unowned WAD payload remains exact.");
    oldCursor += entry.OriginalLength; newCursor += entry.FinalLength;
}
Check(oldCursor == 0x6927000 && newCursor == wad.Length && newCursor-oldCursor == growth,"Complete original/final WAD length.");
plan.RequireStageReversion(); await plan.RequireFreshAsync(); checks++;
using var doc = JsonDocument.Parse(plan.BuildDescription()); var d = doc.RootElement;
Check(d.GetProperty("ExecutableSha256").GetString() == Hash(executable) && d.GetProperty("LevelDataSha256").GetString() == Hash(data),"Description exact final whole owners.");
byte[] reconstructedExe = original.ToArray();
void ApplySpans(JsonElement spans,byte[] before,byte[] after)
{
    int end = 0;
    foreach (var span in spans.EnumerateArray())
    {
        int at = span.GetProperty("offset").GetInt32(), length = span.GetProperty("byteLength").GetInt32();
        byte[] bytes = Convert.FromBase64String(span.GetProperty("bytesBase64").GetString()!);
        Check(at >= end && length == bytes.Length && at+length <= before.Length &&
            Hash(before.AsSpan(at,length)) == span.GetProperty("originalSha256").GetString() && Hash(bytes) == span.GetProperty("sha256").GetString(),
            "Bounded exact disjoint patch before/after owner.");
        bytes.CopyTo(after,at); end = at+length;
    }
}
ApplySpans(d.GetProperty("executablePatchSpans"),original,reconstructedExe);
Check(reconstructedExe.SequenceEqual(executable),"Description reconstructs entire EXE and complement.");
foreach (var spec in new[] { ("card",card,p.Admission.CopyOriginalOverlay()),("artisans",artisans,p.Authored.Integration.CopyOriginalOverlay()) })
{
    var desc = d.GetProperty(spec.Item1); byte[] rebuilt = new byte[spec.Item2.Length]; spec.Item3.CopyTo(rebuilt,0);
    ApplySpans(desc.GetProperty("patchSpans"),spec.Item3,rebuilt); int cursor = spec.Item3.Length;
    foreach (var block in desc.GetProperty("blocks").EnumerateArray())
    {
        int offset = block.GetProperty("offset").GetInt32(), length = block.GetProperty("byteLength").GetInt32();
        Check(offset == cursor && length > 0 && offset+length <= rebuilt.Length,"Complete append block coverage.");
        if (block.GetProperty("bytesBase64").ValueKind == JsonValueKind.String)
        { var bytes = Convert.FromBase64String(block.GetProperty("bytesBase64").GetString()!); Check(bytes.Length == length,"Whole bounded block bytes."); bytes.CopyTo(rebuilt,offset); }
        else Check(block.GetProperty("zeroInitialized").GetBoolean(),"Non-code owner is zero initialized.");
        Check(Hash(rebuilt.AsSpan(offset,length)) == block.GetProperty("sha256").GetString(),"Whole block hash."); cursor += length;
    }
    Check(cursor == rebuilt.Length && rebuilt.SequenceEqual(spec.Item2),"Complete original-plus-owned-block overlay reconstruction.");
}
var guard = d.GetProperty("guard");
Check(Convert.FromBase64String(guard.GetProperty("bytesBase64").GetString()!).SequenceEqual(p.Lifecycle.LiveGuard!.CopyBytes()),"Receipt contains exact bounded guard code.");
Check(guard.GetProperty("expectedGlobals").GetArrayLength() == p.Lifecycle.LiveGuard.ExpectedSceneGlobals.Count &&
    d.GetProperty("projectedRows").GetArrayLength() == 2 && d.GetProperty("projectedModels").GetArrayLength() == 2,"Observer receives actual final globals and projections.");
if (args[0] == "--describe") { Console.WriteLine(Encoding.UTF8.GetString(plan.BuildDescription())); return; }
var expected = PhysicalOracle.Check(plan,Check);
using var receipt = JsonDocument.Parse(plan.BuildReceipt(expected));
Check(receipt.RootElement.GetProperty("GuardedCollectionEmitted").GetBoolean() && !receipt.RootElement.GetProperty("RuntimeVerified").GetBoolean() &&
    !receipt.RootElement.GetProperty("CardWrites").GetBoolean() && receipt.RootElement.GetProperty("guard").GetProperty("expectedGlobals").GetArrayLength() > 0,
    "Receipt preserves emitted-vs-verified boundary and guard evidence.");
if (args[0] == "--publication-faults") await PublicationFaults.Run(plan,args[4],expected.Hash);
if (args[0] == "--publish") Console.WriteLine("JP_EGG_ADMITTED_RESEARCH_PUBLICATION " + JsonSerializer.Serialize(
    await NativeJapaneseEggAdmittedLifecycleResearchPublisher.PublishAsync(plan,args[4])));
if (args[0] == "--verify") Console.WriteLine("JP_EGG_ADMITTED_RESEARCH_VERIFIED " + JsonSerializer.Serialize(
    await NativeJapaneseEggAdmittedLifecycleResearchPublisher.VerifyAsync(plan,args[4])));
await plan.RequireFreshAsync(); checks++;
Console.WriteLine($"JP_EGG_ADMITTED_RESEARCH_SOURCE_OK checks={checks} exe={plan.ExecutableSha256} card={plan.CardOverlaySha256} artisans={plan.ArtisansOverlaySha256} data={plan.LevelDataSha256} wad={plan.WadSha256} root={plan.RootSha256} growth={growth:X} exeLba={plan.RelocatedExecutableLba} endLba={plan.FinalExecutableEndLba} expectedImage={expected.Hash} changed={expected.Changed} publishedImages={(args[0]=="--publish"?1:0)} runtime=false");
