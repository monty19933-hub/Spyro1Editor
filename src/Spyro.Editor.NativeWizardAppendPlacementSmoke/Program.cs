using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

int checks = 0, refusals = 0;
void Check(bool value, string reason)
{
    if (!value) throw new InvalidOperationException(reason);
    checks++;
}
void Reject(Action action, string reason)
{
    try { action(); }
    catch (InvalidDataException) { checks++; refusals++; return; }
    throw new InvalidOperationException("Expected placement refusal: " + reason);
}
bool Separate(int ax,int ay,int bx,int by,int cx,int cy,int dx,int dy,int radius=3072) =>
    NativeWizardPlacementPairGeometry.IsStrictlySeparated(ax,ay,bx,by,cx,cy,dx,dy,radius);

// The two8192-long segments cross, yet each endpoint is4096 from the other
// segment. A four-endpoint-only implementation would incorrectly accept.
Check(!Separate(-4096,0,4096,0,0,-4096,0,4096), "interior proper crossing");
Check(!Separate(0,0,8192,0,4096,0,12288,0), "collinear overlap");
Check(!Separate(0,0,8192,0,8192,0,8192,8192), "endpoint touch");
Check(!Separate(0,0,8192,0,4096,0,4096,0), "degenerate point inside segment");
Check(!Separate(0,0,8192,0,0,3072,8192,3072), "parallel exact threshold");
Check(Separate(0,0,8192,0,0,3073,8192,3073), "parallel just beyond threshold");
Check(!Separate(0,0,8192,0,11264,0,14000,0), "collinear cap equality");
Check(Separate(0,0,8192,0,11265,0,14000,0), "collinear separated beyond cap");
Check(!Separate(0,0,8192,0,4096,3000,6000,8000), "endpoint-to-interior near miss");
Check(Separate(0,0,8192,0,4096,3073,6000,8000), "endpoint-to-interior clear");
Check(!Separate(int.MinValue,int.MinValue,int.MaxValue,int.MaxValue,
    int.MinValue,int.MaxValue,int.MaxValue,int.MinValue), "signed-int crossing overflow guard");
Check(Separate(int.MinValue,int.MinValue,int.MaxValue,int.MinValue,
    int.MinValue,int.MaxValue,int.MaxValue,int.MaxValue), "signed-int parallel overflow guard");
Reject(() => Separate(0,0,1,1,4,4,8,8,-1), "negative distance");
foreach (var fixture in new[]
{
    (0,0,8192,0,0,3073,8192,3073), (-4096,0,4096,0,0,-4096,0,4096),
    (0,0,8192,0,4096,3000,6000,8000), (1,2,1,2,10000,2,10000,2)
})
{
    var (ax,ay,bx,by,cx,cy,dx,dy) = fixture;
    bool value = Separate(ax,ay,bx,by,cx,cy,dx,dy);
    Check(value == Separate(bx,by,ax,ay,cx,cy,dx,dy) &&
          value == Separate(ax,ay,bx,by,dx,dy,cx,cy) &&
          value == Separate(cx,cy,dx,dy,ax,ay,bx,by), "closed-segment ordering symmetry");
}
foreach (var type in new[] { typeof(NativeDarkHollowWizardAppendPlacementSource), typeof(NativeDarkHollowWizardAppendPlacementPlan) })
    Check(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0, "source-issued constructor boundary");
Console.WriteLine($"NATIVE_WIZARD_APPEND_PLACEMENT_FREE_OK checks={checks} refusals={refusals}");
if (args.Length == 0) return;
if (args.Length != 3 || args[0] != "--source") throw new ArgumentException("Usage: --source original.bin workspace");

string image = args[1];
var catalog = LevelCatalog.Load(args[2]);
var issuer = NativeDarkHollowWizardAppendSource.Capture(image,catalog);
var first = issuer.CompileActive(issuer.EmptyOwners(), [new("z")]);
var z = new NativeWizardPlacementIntent(92832,25248,10445,91808,25248);
var a = new NativeWizardPlacementIntent(86688,25248,10445,87712,25248);
var oneSource = NativeDarkHollowWizardAppendPlacementSource.Capture(image,first);
var one = oneSource.Compile([new("z",z)]);
var fixedScene = NativeDarkHollowWizardScenePlan.Capture(image);
var fixedPlacement = NativeDarkHollowWizardPlacementSource.Capture(image,fixedScene).Compile(z);
Check(one.CopyScene().SequenceEqual(fixedPlacement.CopyScene()) &&
    one.FinalSceneSha256 == "6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f",
    "old one-Wizard fixed bytes stay exact");

byte[] acceptedOwners = first.ProposedOwners.Serialize();
var reopenedIssuer = NativeDarkHollowWizardAppendSource.Capture(image,catalog);
var accepted = reopenedIssuer.RestoreOwners(acceptedOwners);
var appended = reopenedIssuer.CompileActive(accepted,[new("a"),new("z")]);
byte[] baseBytes = appended.CopyScene(), ownersBefore = appended.ProposedOwners.Serialize();
var source = NativeDarkHollowWizardAppendPlacementSource.Capture(image,appended);
var requests = new[] { new NativeWizardFamilyPlacementIntent("a",a), new("z",z) };
var plan = source.Compile(requests);
Check(plan.FinalSceneSha256 == "9dbdf8839f19cf31becda27b52b5a854f6badb8b59ad1db110c921055e5263dc",
    "independent full raw-sector two-family coordinate golden");
Check(plan.Families.Select(f => (f.FamilyId,f.NativeIndex)).SequenceEqual(new[] { ("z",128),("a",129) }),
    "ledger ordinal ordering must survive input order");
Check(plan.Mutations.Count == 18 && plan.Mutations.Select(m => m.SceneOffset).Distinct().Count() == 18,
    "actual eighteen distinct allocated coordinate fields");
Check(plan.ProposedOwners.Serialize().SequenceEqual(ownersBefore), "placement never allocates or accepts persistent owners");
Check(plan.BaseSceneSha256 == appended.FinalSceneSha256 && plan.BaseGraphFingerprint == appended.GraphFingerprint,
    "exact append afterimage and source schema binding");
var reversed = source.Compile([new("z",z),new("a",a)]);
Check(reversed.CopyScene().SequenceEqual(plan.CopyScene()) && reversed.PlanSha256 == plan.PlanSha256,
    "placement request order cannot change native mapping, bytes or fingerprint");
var thirdIssuer = NativeDarkHollowWizardAppendSource.Capture(image,catalog);
var thirdAppend = thirdIssuer.CompileActive(thirdIssuer.RestoreOwners(ownersBefore),[new("z"),new("a")]);
var repeated = NativeDarkHollowWizardAppendPlacementSource.Capture(image,thirdAppend).Compile([new("z",z),new("a",a)]);
Check(repeated.CopyScene().SequenceEqual(plan.CopyScene()) && repeated.PlanSha256 == plan.PlanSha256,
    "reopened source/address objects retain deterministic scene and intent mapping");

byte[] final = plan.CopyScene(), inverse = final.ToArray();
foreach (var family in appended.Families)
{
    int row=family.RowOffset, props=family.PropertiesOffset;
    var fields=plan.Mutations.Where(m => m.FamilyId==family.FamilyId).ToArray();
    Check(fields.Select(m=>m.SceneOffset).SequenceEqual(new[] {row+12,row+16,row+20,
        props+0x30,props+0x34,props+0x38,props+0x40,props+0x44,props+0x48}),
        "field addresses derive from each source-issued row/private owner");
    Check(I32(final,row)==props && I32(final,props)==props+0x28 &&
        I32(final,props+0x28)==2 && I32(final,props+0x2C)==-65536 &&
        I32(final,props+0x3C)==0 && I32(final,props+0x4C)==0,
        "per-family pointers and complete route header/trailing words preserved");
}
foreach (var field in plan.Mutations)
{
    Check(I32(baseBytes,field.SceneOffset)==field.Before && I32(final,field.SceneOffset)==field.After,
        "declared exact coordinate before/after values");
    BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(field.SceneOffset),field.Before);
}
Check(inverse.SequenceEqual(baseBytes), "entire unowned byte complement including all128 residents/suffix/pool/fixups");
Check(plan.RevertToAppendedScene(final).SequenceEqual(baseBytes), "inverse accepts exact final scene only");
plan.RequireOriginalAndFinal(baseBytes,final);
foreach (int offset in plan.Mutations.Select(m=>m.SceneOffset).Concat(new[] {
    0,0x8C34,0x8C38,0xB838,0xB890,0xE038,0xEED4,0xEF24,0xEF74,0x10F90,0x111E0,0x117FF }))
{
    byte[] corrupt=final.ToArray();corrupt[offset]^=1;
    Reject(()=>plan.RequireOriginalAndFinal(baseBytes,corrupt),"coordinate or unowned complement changed");
}
Reject(()=>plan.RequireOriginalAndFinal(first.CopyScene(),final),"wrong count/source-issued append afterimage");

Reject(()=>source.Compile([new("z",z)]),"omitted active family");
Reject(()=>source.Compile([new("a",a)]),"omitted prior z128");
Reject(()=>source.Compile([]),"empty placement set");
Reject(()=>source.Compile([new("z",z),new("unknown",a)]),"unknown family");
Reject(()=>source.Compile([new("z",z),new("Z",a)]),"ordinal-case identity is not an alias");
Reject(()=>source.Compile([new("z",z),new("z",a)]),"duplicate family");
Reject(()=>source.Compile([new("z",z),null!]),"late null request");
Reject(()=>source.Compile([new("z",z),new("a",null!)]),"late null placement");
Reject(()=>source.Compile([new("z",z),new("a",z)]),"individually valid routes coincide");
Reject(()=>source.Compile([new("z",z),new("a",z with{OriginX=z.EndX,EndX=z.OriginX})]),"same closed route reversed");
Reject(()=>source.Compile([new("z",z),new("a",new(z.OriginX,z.OriginY,z.OriginZ,z.OriginX,z.OriginY-1024))]),
    "individually surveyed dry corridors share endpoint");
foreach (var invalid in new[] {
    a with{OriginX=0,OriginY=0,EndX=1024,EndY=0},a with{OriginX=a.OriginX+1},
    a with{OriginZ=10753},a with{OriginZ=10239},a with{EndX=a.OriginX},a with{EndX=200000} })
    Reject(()=>source.Compile([new("z",z),new("a",invalid)]),"second-family terrain/grid/height/length failure stays atomic");
int advances=0;
IEnumerable<NativeWizardFamilyPlacementIntent> Finite()
{
    foreach (var r in new[] {new NativeWizardFamilyPlacementIntent("z",z),new("a",a),new("extra",a)})
    { advances++;yield return r; }
    throw new InvalidOperationException("Materializer read the fourth entry after max+1.");
}
Reject(()=>source.Compile(Finite()),"bounded request materialization");
Check(advances<=3,"source-issued two-family bound before expensive geometry");
Reject(()=>source.Compile(new LyingPlacements([new("z",z),new("a",a)],1)),"declared collection count disagrees with complete enumerated set");
Reject(()=>source.Compile(new LyingPlacements([new("z",z)],2)),"enumerator ends before declared count");
Reject(()=>source.Compile(new LyingPlacements([new("z",z),new("a",a)],-1)),"negative declared collection count");
string tiny=Path.Combine(Path.GetTempPath(),"spyro-count-placement-"+Guid.NewGuid().ToString("N")+".bin");
try
{
    File.WriteAllBytes(tiny,[0]);
    Reject(()=>NativeDarkHollowWizardAppendPlacementSource.Capture(tiny,appended),
        "genuine appended plan cannot lend source authority to a stale physical input");
}
finally { if(File.Exists(tiny)) File.Delete(tiny); }
requests[0]=new("caller-replaced-array",z);
byte[] copied=plan.CopyScene();copied[0]^=1;
Check(plan.CopyScene().SequenceEqual(final) && appended.CopyScene().SequenceEqual(baseBytes) &&
    appended.ProposedOwners.Serialize().SequenceEqual(ownersBefore) && accepted.Serialize().SequenceEqual(acceptedOwners),
    "successful and refused placement calls cannot mutate caller/accepted owners");
Check(source.Compile([new("z",z),new("a",a)]).PlanSha256==plan.PlanSha256,"retry remains exact after refusals");
Check(plan.AuthoredDryCorridorAccepted && plan.OriginalCenterSeparationAccepted &&
    plan.AuthoredPairwiseCenterSeparationAccepted && plan.OriginalResidentCount==128 &&
    !plan.InitialHeightProven && !plan.NativeBodyAndTrajectoryProven && !plan.PlacementApproved &&
    !plan.RuntimeFitProven && !plan.RuntimeVerified && !plan.PublicationApproved &&
    !plan.ProjectTransactionIntegrated && !plan.AcceptedExportHistoryProven,
    "authored static policy must not become native physics, history, App or candidate approval");
Console.WriteLine($"NATIVE_WIZARD_APPEND_PLACEMENT_SOURCE_OK checks={checks} refusals={refusals} placement=false runtime=false publication=false");
static int I32(byte[] b,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(offset));

sealed class LyingPlacements(NativeWizardFamilyPlacementIntent[] entries,int declaredCount)
    : ICollection<NativeWizardFamilyPlacementIntent>
{
    public int Count=>declaredCount;
    public bool IsReadOnly=>true;
    public IEnumerator<NativeWizardFamilyPlacementIntent> GetEnumerator()=>((IEnumerable<NativeWizardFamilyPlacementIntent>)entries).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    public void CopyTo(NativeWizardFamilyPlacementIntent[] array,int index)=>throw new InvalidOperationException("Use bounded enumeration, not trusted caller count/copy.");
    public void Add(NativeWizardFamilyPlacementIntent item)=>throw new NotSupportedException();
    public void Clear()=>throw new NotSupportedException();
    public bool Contains(NativeWizardFamilyPlacementIntent item)=>throw new NotSupportedException();
    public bool Remove(NativeWizardFamilyPlacementIntent item)=>throw new NotSupportedException();
}
