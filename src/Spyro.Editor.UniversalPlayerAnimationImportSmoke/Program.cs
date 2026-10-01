using System.Buffers.Binary;
using System.Text.Json;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

if (args.Length != 3) throw new ArgumentException("Required: unchanged-USA-BIN catalog-workspace artifacts-directory");
UniversalSourceLibrary.RequireOriginal(args[0]);
var catalog = LevelCatalog.Load(args[1]);
if (catalog.Levels.Count < 35) throw new InvalidDataException("Complete original retail catalog required.");
Directory.CreateDirectory(args[2]);
var library = new UniversalSourceLibrary(args[0], catalog);
var animations = library.PlayerAnimations();
int checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException(label); }
var art = library.Get("artisans");
var alpine = library.Get("alpineridge");
var magic = library.Get("magiccrafters");
var stone = library.Get("stonehill");
var originalDonorHashes=new[]{art,alpine,magic,stone}.ToDictionary(x=>x.Level.Key,x=>Convert.ToHexStringLower(SHA256.HashData(x.Data.Bytes)));
byte[] originalExecutable=library.Disc.Exe.ToArray();
int Class(UniversalLevelData data, int row) => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(row).AsSpan(0x36));
var alpineRows = Enumerable.Range(0, alpine.Data.Scene().RowCount).Where(r => Class(alpine.Data,r)==0x10F).ToArray();
Check(alpineRows.Contains(1) && alpineRows.Any(r=>r!=1), "original Alpine enemy fixture available");
int alpineRow = alpineRows.First(r=>r!=1);
var placement = new UniversalMobyPlacement("alpineridge",alpineRow,85000,47000,10000);
var removed = Enumerable.Range(0, art.Data.Scene().RowCount).ToArray();
var protectedVram = UniversalMobyExport.DefaultProtectedVram(args[0],art.Level).ToArray();
UniversalComposeResult Compose(IReadOnlyList<UniversalMobyPlacement> rows, bool provider=true, byte[]? data=null) =>
    UniversalMobyComposer.Compose(art.Level,data??art.Data.Bytes,art.Overlay.Bytes.ToArray(),library.Disc.Exe,
        library.Get,library.SharedClasses,protectedVram,rows,removedRows:removed,
        protectedVramAfterDrop: dropped=>UniversalMobyExport.DefaultProtectedVram(args[0],art.Level,dropped),
        sharedModelPackage:library.SharedModelPackage,playerAnimationSource:provider?library.PlayerAnimations:null);
var result = Compose([placement]);
var report = result.PlayerAnimationImports.Single(r=>r.SourceLevelKey=="alpineridge");
Check(report.RequestedAnimationIds.SequenceEqual(new[]{13,15,23}), "exact collider-owned required slots");
Check(report.AddedAnimationIds.SequenceEqual(new[]{23}) && report.ReusedSharedAnimationIds.SequenceEqual(new[]{13,15}), "only local 23 added; shared 13/15 reused");
var donor23 = animations.Levels.Single(l=>l.LevelId==31).Stream.Records.Single(r=>r.AnimationId==23);
Check(report.InsertedBytes==donor23.ByteLength && report.ModelGrowthBytes==UniversalLevelData.Align(donor23.ByteLength,0x800), "actual record length and rounded allocation reported");
byte[] geometry=library.Disc.EntryBytes(8).AsSpan(0x800+0xAA8,NativePlayerAnimationSource.SharedPlayerPackageByteLength-0xAA8).ToArray();
NativePlayerAnimationStream Stream(byte[] body) {
    int start=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0x50)),end=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0x54));
    return NativePlayerAnimationStreamCodec.Decode(body.AsSpan(start,end-start),geometry);
}
void ExistingRecords(UniversalLevelData before, UniversalComposeResult after) {
    var original=Stream(before.Bytes);var final=Stream(after.LevelData);
    foreach(var record in original.Records) Check(final.Records.Single(r=>r.AnimationId==record.AnimationId).CopyRecord().AsSpan().SequenceEqual(record.CopyRecord()),$"existing player record {record.AnimationId} preserved");
    Check(final.Records.Select(r=>r.AnimationId).Distinct().Count()==final.Records.Count,"no duplicate player animation IDs");
}
ExistingRecords(art.Data,result);
Check(Stream(result.LevelData).Records.Single(r=>r.AnimationId==23).CopyRecord().AsSpan().SequenceEqual(donor23.CopyRecord()),"inserted 23 is exact original donor record");
var finalData=new UniversalLevelData(result.LevelData);
int originalRoot=art.Data.U32(0x50), finalRoot=finalData.U32(0x50);
Check(art.Data.Bytes.AsSpan(art.Data.Subfiles()[2].Offset,originalRoot-art.Data.Subfiles()[2].Offset).SequenceEqual(finalData.Bytes.AsSpan(finalData.Subfiles()[2].Offset,finalRoot-finalData.Subfiles()[2].Offset)),"complete native environment before player stream byte preserved");
int enemyModelBytes=result.Classes.Where(c=>c.ModelImported).Sum(c=>UniversalLevelData.Align(c.ModelBytes,8));
Check(result.Ram.ModelGrowth==UniversalLevelData.Align(enemyModelBytes,0x800)+report.ModelGrowthBytes,"RAM model growth includes separately charged player animation allocation");
var finalOverlay=new UniversalOverlay(result.Overlay,UniversalExeBindings.Read(result.Executable,10));
Check(result.Ram.FinalEnd==UniversalMobyComposer.RuntimeEnd(finalData,finalOverlay.CopyBuffer),"RAM report equals actual emitted body and EXE binding");
Check((long)result.Ram.FinalEnd-result.Ram.OriginalEnd==(long)result.Ram.OverlayGrowth+result.Ram.ModelGrowth+result.Ram.SceneGrowth+result.Ram.DataGrowth,"complete growth accounting reconciles");
Check(result.Ram.FinalHeadroom>=UniversalMobyComposer.RamSafetyMargin,"actual final RAM margin admitted");
Check(result.NewRowIndices.Count==1 && Class(finalData,result.NewRowIndices[0])==result.Classes.Single(c=>c.SourceLevelKey=="alpineridge"&&c.ClassId==0x10F).DestinationClassId,"placed row uses emitted source class mapping");
Check(result.PlacementSaveRows[0]==art.Data.Scene().RowCount,"physical reuse does not recycle native save identity");
var twice=Compose([placement,placement with{X=86000}]);
Check(twice.PlayerAnimationImports.Count==1 && twice.PlayerAnimationImports.Sum(r=>r.InsertedBytes)==report.InsertedBytes,"repeat donor deduplicates animation import");
Check(Stream(twice.LevelData).Records.Count(r=>r.AnimationId==23)==1,"repeat donor emits a single 23 record");
ExistingRecords(art.Data,twice);

// Retain an authentic native actor/model while importing, rather than only proving
// preservation in the intentionally stripped RAM fixture.
var keepClass=Class(art.Data,0);
var retainRemoved=removed.Where(row=>Class(art.Data,row)!=keepClass).ToArray();
var retainedBaseline=UniversalMobyComposer.Compose(art.Level,art.Data.Bytes,art.Overlay.Bytes.ToArray(),library.Disc.Exe,
    library.Get,library.SharedClasses,protectedVram,[],removedRows:retainRemoved,
    protectedVramAfterDrop:dropped=>UniversalMobyExport.DefaultProtectedVram(args[0],art.Level,dropped),
    sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
var retainedBaselineData=new UniversalLevelData(retainedBaseline.LevelData);
var retained=UniversalMobyComposer.Compose(art.Level,art.Data.Bytes,art.Overlay.Bytes.ToArray(),library.Disc.Exe,
    library.Get,library.SharedClasses,protectedVram,[placement],removedRows:retainRemoved,
    protectedVramAfterDrop:dropped=>UniversalMobyExport.DefaultProtectedVram(args[0],art.Level,dropped),
    sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
var retainedData=new UniversalLevelData(retained.LevelData);
foreach(var registration in art.Data.Registrations().Where(r=>!retained.DroppedModelClasses.Contains(r.ClassId)))
    {
    var originalPackage=art.Data.Package(registration.ClassId);var finalPackage=retainedData.Package(registration.ClassId);
    Check(finalPackage.Length>=originalPackage.Length && finalPackage.AsSpan(0,originalPackage.Length).SequenceEqual(originalPackage),$"retained native model {registration.ClassId:X3} original extent byte preserved");
    Check(finalPackage.AsSpan(originalPackage.Length).IndexOfAnyExcept((byte)0)<0,$"retained model {registration.ClassId:X3} extra alignment contains only zero padding");
}
foreach(var row in removed.Except(retainRemoved)) {
    // Native property blocks move when unrelated actors are permanently removed.
    // The row payload and its packed property content must remain unchanged.
    Check(retainedData.Row(row).AsSpan(4).SequenceEqual(art.Data.Row(row).AsSpan(4)), $"retained native scene row {row} payload byte preserved");
    Check(retainedData.Row(row).AsSpan().SequenceEqual(retainedBaselineData.Row(row)), $"retained native row {row} exact prepared scene bytes preserved");
    Check(retainedData.CaptureRow("artisans",row).Properties.AsSpan().SequenceEqual(retainedBaselineData.CaptureRow("artisans",row).Properties),$"retained row {row} prepared property content preserved");
}

var same=UniversalMobyCatalogBuilder.DryRun(library,alpine.Level,[placement]);
Check(same.Error==null && same.Result!=null,"same-source actual DryRun succeeds");
Check(same.Result!.PlayerAnimationImports.Count==0,"same-source requires no player import");
Check(same.Result.Ram.ModelGrowth==0,"same-source placement has no model/animation growth");
ExistingRecords(alpine.Data,same.Result);
var sameData=new UniversalLevelData(same.Result.LevelData);
Check(sameData.Bytes.AsSpan(sameData.Subfiles()[2].Offset,sameData.Subfiles()[2].Length).SequenceEqual(alpine.Data.Bytes.AsSpan(alpine.Data.Subfiles()[2].Offset,alpine.Data.Subfiles()[2].Length)),"same-source complete model subfile unchanged");
for(int row=0;row<alpine.Data.Scene().RowCount;row++) Check(sameData.Row(row).AsSpan().SequenceEqual(alpine.Data.Row(row)),"same-source native scene row preserved");

int secondRow=Enumerable.Range(0,stone.Data.Scene().RowCount).First(row=>Class(stone.Data,row)==0x071);
Check(animations.Levels.Single(l=>l.LevelId==11).Stream.Records.Single(r=>r.AnimationId==23).CopyRecord().AsSpan().SequenceEqual(donor23.CopyRecord()),"two actual enemy donors own equivalent animation 23");
var mixed=Compose([placement,new("stonehill",secondRow,87000,47000,10000)]);
Check(mixed.PlayerAnimationImports.Count==2,"each foreign donor dependency closure reported");
Check(mixed.PlayerAnimationImports.Sum(r=>r.AddedAnimationIds.Count(id=>id==23))==1 && mixed.PlayerAnimationImports.Sum(r=>r.ReusedLocalAnimationIds.Count(id=>id==23))==1,"multi-enemy donors deduplicate equivalent 23");
Check(Stream(mixed.LevelData).Records.Count(r=>r.AnimationId==23)==1,"multi-donor final stream contains one 23");

var unresolved=UniversalMobyCatalogBuilder.DryRun(library,art.Level,[new("magiccrafters",1,87000,47000,10000)],removedRows:removed);
Check(unresolved.Result==null && unresolved.Error?.Contains("Class 0x13B",StringComparison.Ordinal)==true,"unresolved Magic child remains refused instead of borrowing an animation closure");
void Refuses(Action action,string text,string label) {
    try { action();throw new InvalidOperationException(label+" unexpectedly succeeded"); }
    catch(InvalidDataException e) { Check(e.Message.Contains(text,StringComparison.OrdinalIgnoreCase),label+": "+e.Message); }
}
Refuses(()=>Compose([placement],provider:false),"additional Spyro animations","missing original-source provider refuses");
byte[] unchanged=art.Data.Bytes.ToArray();
// Preserve the final authentic model class so its deliberately oversized zero
// allocation remains resident after deletion; the renderer bytes stay unchanged.
var inflated=new UniversalLevelData(unchanged.ToArray());inflated.GrowSubfile(2,0x40000);
int tailClass=art.Data.Registrations().MaxBy(r=>r.Root)!.ClassId;
var budgetRemoved=removed.Where(row=>Class(art.Data,row)!=tailClass).ToArray();
UniversalRamBudget? refusalRam=null;
try {
    UniversalMobyComposer.Compose(art.Level,inflated.Bytes,art.Overlay.Bytes.ToArray(),library.Disc.Exe,
        library.Get,library.SharedClasses,protectedVram,[placement],removedRows:budgetRemoved,
        protectedVramAfterDrop:dropped=>UniversalMobyExport.DefaultProtectedVram(args[0],art.Level,dropped),
        sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
    throw new InvalidOperationException("insufficient RAM unexpectedly admitted");
}
catch(UniversalBudgetException e) {
    refusalRam=e.Budget;
    Check(e.Budget.FinalEnd>UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin,"insufficient actual RAM refuses after merge");
    Check(e.Budget.ModelGrowth>=report.ModelGrowthBytes,"refused RAM includes animation allocation");
}
Check(art.Data.Bytes.AsSpan().SequenceEqual(unchanged),"failed compositions never mutate original library body");
var resolvedRow1=UniversalMobyCatalogBuilder.DryRun(library,art.Level,[placement with{SourceRowIndex=1}],removedRows:removed);
Check(resolvedRow1.Result!=null && resolvedRow1.Error==null,"resolved Alpine row1 actual composition admitted");
Check(resolvedRow1.Result!.PlayerAnimationImports.Single(r=>r.SourceLevelKey=="alpineridge").AddedAnimationIds.SequenceEqual(new[]{23}),"resolved row1 imports the required local 23");
Check(Stream(resolvedRow1.Result.LevelData).Records.Single(r=>r.AnimationId==23).CopyRecord().AsSpan().SequenceEqual(donor23.CopyRecord()),"resolved row1 23 bytes exact source donor");

// Exercise the exact provider wired by the authored main-editor compiler, with
// real source-issued neutral ID65 resources. No UI, package, or emulator launch.
UniversalComposeResult? freshReceipt=null;
string baseline=Path.Combine(args[2],"fresh-bootstrap.bin");
try {
    var fresh=await NativeFreshMainCompilation.CaptureAsync(args[0],Spyro.Editor.Core.Editing.SceneDocument.New(),baseline);
    Check(new UniversalLevelData(fresh.CopyLevelData()).Scene().RowCount==0,"fresh source-issued host contains no donor actors");
    var freshResult=fresh.Compose(library,[placement]);freshReceipt=freshResult;
    Check(freshResult.PlayerAnimationImports.Any(r=>r.AddedAnimationIds.Contains(23)),"fresh compiler actually connects authenticated animation provider");
    Check(Stream(freshResult.LevelData).Records.Single(r=>r.AnimationId==23).CopyRecord().AsSpan().SequenceEqual(donor23.CopyRecord()),"fresh emitted 23 exact original donor");
    ExistingRecords(new UniversalLevelData(fresh.CopyLevelData()),freshResult);
    Check(freshResult.Ram.FinalEnd==UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(freshResult.LevelData),new UniversalOverlay(freshResult.Overlay,UniversalExeBindings.Read(freshResult.Executable,65)).CopyBuffer),"fresh RAM is actual final body cost");
} finally { if(File.Exists(baseline))File.Delete(baseline); }

foreach(var original in originalDonorHashes) Check(Convert.ToHexStringLower(SHA256.HashData(library.Get(original.Key).Data.Bytes))==original.Value,$"cached original {original.Key} body remains unchanged");
Check(library.Disc.Exe.AsSpan().SequenceEqual(originalExecutable),"cached original executable remains unchanged");
UniversalSourceLibrary.RequireOriginal(args[0]);
Check(true,"original disc hash authenticated after all fixtures");
File.WriteAllText(Path.Combine(args[2],"result.json"),JsonSerializer.Serialize(new{Scope="actual-source-static-compose",checks,alpineRow,secondEnemy=new{Source="stonehill",Row=secondRow,Class=0x071},retainedNativeClass=keepClass,SourceSha256=UniversalSourceLibrary.OriginalUsaSha256,result.Ram,result.PlayerAnimationImports,Mixed=mixed.PlayerAnimationImports,SameSource=same.Result.Ram,RefusedRam=refusalRam,Fresh=new{freshReceipt!.Ram,freshReceipt.PlayerAnimationImports},RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"UNIVERSAL_PLAYER_ANIMATION_IMPORT_CHECKS_OK checks={checks}");
