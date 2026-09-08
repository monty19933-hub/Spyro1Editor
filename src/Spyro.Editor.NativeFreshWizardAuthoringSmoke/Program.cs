using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

if (args.Length != 0 && (args.Length != 3 || args[0] != "--source"))
    throw new ArgumentException("Usage: NativeFreshWizardAuthoringSmoke [--source original.bin original.cue]");
int checks = 0;
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    await Refuse<OperationCanceledException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(null!, null!, null!, cancelled.Token));
    await Refuse<OperationCanceledException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(null!, null!, null!, null!, null!, cancelled.Token));
}
await Refuse<ArgumentNullException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync("must-not-open", null!, null!));
await Refuse<ArgumentNullException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open", null!, null!, null!, null!));
Console.WriteLine($"FRESH_WIZARD_AUTHORING_FREE_OK checks={checks}");
if (args.Length == 0) return;
string bin = Path.GetFullPath(args[1]), cue = Path.GetFullPath(args[2]);
string originalHash = FileHash(bin), cueHash = FileHash(cue);
Check(originalHash == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37", "Complete original identity.");
try
{
    var a = Floor(1024);
    var b = Floor(2048);
    var ca = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, a);
    var cb = await NativeId65FreshDocumentCompilation.CaptureAsync(bin, b);
    var family = await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
    var oldResource = await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(bin, ca, family);
    var newResource = await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin, cb, family);
    newResource.RequireAncestry(cb,family);
    RefuseNow<InvalidDataException>(() => newResource.RequireAncestry(ca,family));
    Check(newResource.BindingProfileId!=oldResource.BindingProfileId,"Actual named producer profiles remain distinct.");
    var authoredA=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin,ca,family);
    Check(authoredA.CopyDataComponents().SequenceEqual(oldResource.CopyDataComponents()) && authoredA.PlanSha256!=oldResource.PlanSha256,
        "Equal authored A bytes do not alias the fixed research capability domain.");
    Check(newResource.CopyUploadPayload().SequenceEqual(oldResource.CopyUploadPayload()) &&
          newResource.CopyModelPayload().SequenceEqual(oldResource.CopyModelPayload()), "Floor authorship changes neither immutable model nor upload/audio allocation.");
    byte[] oldData = oldResource.CopyDataComponents(), newData = newResource.CopyDataComponents();
    Check(oldData.Length == 0x8FC && newData.Length == oldData.Length &&
          newData.AsSpan(0, 8).SequenceEqual(oldData.AsSpan(0, 8)) &&
          newData.AsSpan(0x14C).SequenceEqual(oldData.AsSpan(0x14C)) &&
          !newData.AsSpan(8, 0x144).SequenceEqual(oldData.AsSpan(8, 0x144)), "Only the actual canonical surface owner changes, with fixed native copy geometry.");
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchResourcePlan.CaptureAsync("must-not-open", cb, family));
    var route = NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111", 9984, 9984, 8704, 9472, 9984);
    var document = NativeFreshLevelDocumentIntent.Create(b, [route]);
    var scene = await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin, document, cb, family, newResource);
    Check(scene.Placement == new NativeWizardPlacementIntent(9984, 9984, 8704, 9472, 9984), "Native row and route retain exact authored coordinates.");
    Check(scene.CopyCanonicalDocument().SequenceEqual(cb.CopyCanonicalDocument()), "Native scene retains actual wider floor intent.");
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAsync("must-not-open", cb, family, newResource, scene.Placement));
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open", document, ca, family, newResource));
    var equalBaseDocument = NativeFreshLevelDocumentIntent.Create(Floor(2048), [route]);
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open", equalBaseDocument, cb, family, newResource));
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open", NativeFreshLevelDocumentIntent.Create(b, []), cb, family, newResource));
    var twice=NativeFreshLevelDocumentIntent.Create(b,[route,NativeFreshWizardRouteIntent.Create("22222222222222222222222222222222",9984,9472,8704,9472,9472)]);
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open",twice,cb,family,newResource));
    var elevated=NativeFreshLevelDocumentIntent.Create(b,[NativeFreshWizardRouteIntent.Create(route.EditorId,9984,9984,9217,9472,9984)]);
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync("must-not-open",elevated,cb,family,newResource));
    var clearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin,document,scene);
    clearance.RequireInitialSeparation(document,scene);
    Check(clearance.Finding.Status==NativeInitialSpawnClearanceStatus.InitialSeparation &&
          clearance.Finding.Centre==new NativeSpawnClearancePoint(9828,9974,8944) &&
          clearance.Finding.DistanceSquared==1855604 && clearance.Finding.CombinedRadiusSquared==571536,
          "Actual source-owned initial sphere assessment agrees with independent wider reference.");
    var equalDocument=NativeFreshLevelDocumentIntent.Create(b,[route]);
    await Refuse<InvalidDataException>(() => NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("must-not-open",equalDocument,scene));
    RefuseNow<InvalidDataException>(()=>clearance.RequireInitialSeparation(equalDocument,scene));
    var fallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin, newResource);
    var host = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin, scene, fallback);
    var body = await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(bin, host);
    var exe = await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(bin, body);
    var disc = await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(bin, exe);
    Check(body.ModelRuntimeAddress == 0x8008C334 && body.SceneRuntimeAddress == 0x800A5334 &&
          body.KnownSceneEndAddress == 0x800A8334, "Authored surface does not silently relocate live resources.");
    Check(disc.CopyBody().AsSpan(0xE6000, 0x8FC).SequenceEqual(newData) &&
          disc.CopyBody().AsSpan(0x100000, 0x3000).SequenceEqual(scene.CopyScene()), "Final detached disc binds actual authored data and scene.");
    Check(Hash(disc.CopyExecutable()) == "a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc" &&
          Hash(disc.CopyOverlay()) == "ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e", "Wider floor/route changes no native enemy code or global behavior.");
    await Refuse<InvalidDataException>(() => NativeId65FreshWizardResearchCandidatePlan.CaptureAsync("must-not-open", "must-not-open", disc));
    Check(Hash(disc.CopyBody())=="503916389b85b655f5e1db5728e01fc2ea95daa7d3b635a3df7f5f4cc6452d29" &&
          Hash(newData)=="b650a22bd06fe8d983a3422321981682e45c35c477aed176a88331034492983b" &&
          Hash(scene.CopyScene())=="08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343",
          "Full native outputs match the independently pinned-P1-derived surface/route reference, not a self-generated expectation.");
    RefuseNow<InvalidDataException>(()=>NativeId65FreshWizardBodyLayout.ValidateStructure(body.CopyLevelData()));
    var damage=newData.ToArray();damage[0x1FF]^=1;
    RefuseNow<InvalidDataException>(()=>NativeFreshWizardAuthoringShape.RequireSurfaceComplement(damage,0x8FC,
        "bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4"));
    var renderer=newData.ToArray();renderer[8+12+136]^=1;
    RefuseNow<InvalidDataException>(()=>NativeFreshWizardAuthoringShape.RequireSurfaceComplement(renderer,0x8FC,
        "bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4"));
    var variable=NativeId65FreshDocumentIntent.Create(4096,4096,8704,1024,1024,4608,4608,64,new(80,128,112),new(128,96,64),19);
    var otherLayout=await NativeId65FreshDocumentCompilation.CaptureAsync(bin,variable);
    Check(otherLayout.FreshResourceAncestor.CopyDataComponents().Length!=0x8C8,"A genuine valid fresh compilation has a different copy-layout size.");
    await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync("must-not-open",otherLayout,family));
    Check(!disc.RuntimeVerified && !disc.PublicationApproved && !disc.NormalCreateBinEnabled, "Detached composition is not runtime/publication authority.");
    Console.WriteLine($"FRESH_WIZARD_AUTHORING_SOURCE_OK checks={checks} body={Hash(disc.CopyBody())} scene={Hash(scene.CopyScene())} data={Hash(newData)} disc={disc.PlanSha256} outputWritten=false");
}
finally
{
    Check(FileHash(bin) == originalHash, "Original image preserved through final source checks.");
    Check(FileHash(cue) == cueHash, "Original CUE preserved.");
}
Console.WriteLine($"FRESH_WIZARD_AUTHORING_FINAL_OK checks={checks} originalAndCueUnchanged=true outputWritten=false");

NativeId65FreshDocumentIntent Floor(int width) => NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, width, width,
    8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void RefuseNow<T>(Action action) where T:Exception
{
    try{action();}catch(T){checks++;return;}throw new Exception("Expected "+typeof(T).Name);
}
async Task Refuse<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new Exception("Expected " + typeof(T).Name);
}
string FileHash(string path) { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
