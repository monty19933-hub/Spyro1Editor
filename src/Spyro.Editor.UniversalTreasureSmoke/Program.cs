using System.Buffers.Binary;
using System.Text.Json;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

int checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new InvalidOperationException(label);}
void Refuse(Action action,string label){try{action();throw new InvalidOperationException(label+" accepted");}catch(InvalidDataException){checks++;}}
byte[] Row(int cls,int kind,int value,int selector){byte[] r=new byte[0x58];BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x36),(ushort)cls);r[0x50]=(byte)kind;r[0x4F]=(byte)value;r[0x53]=(byte)selector;return r;}
foreach(var gem in GemValue.Known)
    for(int selector=0;selector<256;selector++)
    {
        Check(UniversalMobyComposer.TreasureValue(Row(gem.IdByte,0x18,gem.ValueByte,selector))==gem.Value,"visible gem wins over unrelated selector");
        Check(UniversalMobyComposer.TreasureValue(Row(gem.IdByte,0x20,gem.ValueByte,selector))==0,"authored non-gem kind cannot fall through to selector");
    }
foreach(int container in new[]{0xD,0xC3,0x149,0x186,0x11B})
    foreach(var gem in GemValue.Known)
        Check(UniversalMobyComposer.TreasureValue(Row(container,0x10,0,gem.IdByte))==gem.Value,"known native single reward selector");
foreach(int unknown in new[]{0,0x22,0x7F,0xFF})
    Check(UniversalMobyComposer.TreasureValue(Row(0xD,0x10,0,unknown))==0,"unknown/compound/random reward has no invented static total");
Check(UniversalMobyComposer.TreasureValue(Row(0x153,0x18,1,0xFF))==0,"class low byte is not native gem identity");
Check(UniversalMobyComposer.TreasureValue(Row(0x55,0x18,2,0x55))==0,"mismatched visible encoding cannot fall through");
Refuse(()=>UniversalMobyComposer.TreasureValue(new byte[12]),"partial row");
if(args.Length==0){Console.WriteLine("UNIVERSAL_TREASURE_SOURCE_FREE_OK checks="+checks);return;}
if(args.Length is not (3 or 5))throw new ArgumentException("Required: original-USA catalog-workspace artifacts [immutable-saved-project original-Japan]");
Directory.CreateDirectory(args[2]);var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var originalExe=lib.Disc.Exe;
int Offset(int id)=>(int)(UniversalMobyComposer.TreasureTableAddress-UniversalExeBindings.ExeBase)+((id/10-1)*6+id%10)*2;
int Total(byte[] exe,int id)=>BinaryPrimitives.ReadInt16LittleEndian(exe.AsSpan(Offset(id)));
var bounds=originalExe.ToArray();BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(Offset(10)),32766);
UniversalTreasureAccounting.ApplyDelta(bounds,10,1);Check(Total(bounds,10)==32767,"signed maximum accepted");
Refuse(()=>UniversalTreasureAccounting.ApplyDelta(bounds,10,1),"32768 refused");
BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(Offset(10)),0);Refuse(()=>UniversalTreasureAccounting.ApplyDelta(bounds,10,-1),"negative target refused");
byte[] bad=originalExe.ToArray();bad[UniversalTreasureAccounting.ConsumerOffset]^=1;Refuse(()=>UniversalTreasureAccounting.ApplyDelta(bad,10,1),"modified native consumer refused");
int[] donorRows=[59,56,64,107,40];var jacques=lib.Get("jacques");
var placements=donorRows.Select((r,i)=>new UniversalMobyPlacement("jacques",r,8800+i*140,9000,8704,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:i)).ToArray();
Check(donorRows.Select(r=>UniversalMobyComposer.TreasureValue(jacques.Data.Row(r))).SequenceEqual(new[]{1,2,5,10,25}),"five pristine Jacques source values sum43");
var fresh=await NativeFreshMainCompilation.CaptureAsync(args[0],SceneDocument.New(),Path.Combine(args[2],"fresh-zero.bin"));
Check(Total(fresh.CopyExecutable(),65)==0,"source-issued fresh target alreadyzero");
NativeFreshTreasureTarget.RequireZeroBaseline(originalExe,fresh.CopyExecutable());
bad=fresh.CopyExecutable();BinaryPrimitives.WriteInt16LittleEndian(bad.AsSpan(Offset(65)),5);
Refuse(()=>NativeFreshTreasureTarget.RequireZeroBaseline(originalExe,bad),"staged nonzero conflict refused rather than reset");
var composed=fresh.Compose(lib,placements);Check(composed.TreasureDelta==43 && Total(composed.Executable,65)==43,"remapped fresh five-gem target43");
var rows=new UniversalLevelData(composed.LevelData);
Check(composed.Classes.Any(c=>c.ClassId is >=0x53 and <=0x57 && c.DestinationClassId!=c.ClassId),"actual remapped native gem fixture");
Check(composed.NewRowIndices.Sum(r=>UniversalMobyComposer.TreasureValue(rows.Row(r)))!=43,"post-remap counting is a failing negative control");
var retired=fresh.Compose(lib,placements.Select((p,i)=>p with{Retired=i==2}).ToArray());
Check(Total(retired.Executable,65)==38 && retired.NewRowIndices[2]==-1,"retired blue gem omitted from target and physical actors");
var overridden=fresh.Compose(lib,placements.Select((p,i)=>i==2?p with{RowByteOverrides=new Dictionary<int,byte>{{0x50,0x20},{0x53,0x55}}}:p).ToArray());
Check(Total(overridden.Executable,65)==38,"authored kind override short-circuits coincidental reward selector");
string freshOutput=Path.Combine(args[2],"fresh-five.bin");await fresh.ExportAsync(lib,placements,null,freshOutput,args[2],catalog);
Check(Total(UniversalDiscImage.Read(freshOutput).Exe,65)==43,"normal fresh written target43");
string emptyOutput=Path.Combine(args[2],"fresh-empty.bin");await fresh.ExportAsync(lib,[],null,emptyOutput,args[2],catalog);
Check(Total(UniversalDiscImage.Read(emptyOutput).Exe,65)==0,"empty normal fresh written target0");
var art=lib.Get("artisans");var gone=Enumerable.Range(0,art.Data.Scene().RowCount).ToArray();int removed=gone.Sum(r=>UniversalMobyComposer.TreasureValue(art.Data.Row(r)));
UniversalComposeResult Retail(byte[] exe,bool applied,IReadOnlyCollection<int>? removedFixture=null)=>UniversalMobyComposer.Compose(art.Level,art.Data.Bytes,art.Overlay.Bytes.ToArray(),exe,lib.Get,lib.SharedClasses,
    UniversalMobyExport.DefaultProtectedVram(args[0],art.Level),placements,removedRows:removedFixture??gone,protectedVramAfterDrop:d=>UniversalMobyExport.DefaultProtectedVram(args[0],art.Level,d),
    sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations,removedTreasureAlreadyApplied:applied);
var retail=Retail(originalExe,false);Check(Total(retail.Executable,10)==Total(originalExe,10)-removed+43 && retail.TreasureDelta==43-removed,"direct retail known removals and imports counted once");
var nonRewards=Enumerable.Range(0,art.Data.Scene().RowCount).Where(r=>UniversalMobyComposer.TreasureValue(art.Data.Row(r))==0).ToArray();
var fiveWithNativeTreasure=Retail(originalExe,false,nonRewards);
Check(fiveWithNativeTreasure.TreasureDelta==43 && Total(fiveWithNativeTreasure.Executable,10)==143,"retail native treasure100 plus remapped five gems43");
byte[] staged=originalExe.ToArray();UniversalTreasureAccounting.ApplyDelta(staged,10,-removed);
var priorApplied=Retail(staged,true);Check(Total(priorApplied.Executable,10)==Total(retail.Executable,10) && priorApplied.TreasureDelta==43,"ordinary pre-applied removal contract does not subtract twice");
Check(priorApplied.LevelData.AsSpan().SequenceEqual(retail.LevelData) && priorApplied.Overlay.AsSpan().SequenceEqual(retail.Overlay),"treasure contract changes no native rows/models/save/overlay ownership");
var specialExe=priorApplied.Executable.ToArray();UniversalSpecialBehaviors.PatchExeCave(specialExe,originalExe,true,[(0,174,0)]);
UniversalTreasureAccounting.RequireConsumer(specialExe);UniversalTreasureAccounting.ApplyDelta(specialExe,10,1);
Check(Total(specialExe,10)==Total(priorApplied.Executable,10)+1,"existing egg/sign cave preserves certified target consumer");
// A local shadow of an authentic shared gem is a bounded logic fixture, not a
// claim about a retail level. Drop its final instance before any row-class rewrite.
var local=new UniversalLevelData(fresh.CopyLevelData());local.AppendModels([(0x55,lib.SharedModelPackage(0x55))]);
var template=jacques.Data.CaptureRow("jacques",64);local.AppendRows([(template.Row,template)]);
var shared=lib.SharedClasses.Where(c=>c!=0x55).ToHashSet();var localExe=fresh.CopyExecutable();BinaryPrimitives.WriteInt16LittleEndian(localExe.AsSpan(Offset(65)),5);
var overlay=new UniversalOverlay(fresh.CopyOverlay(),UniversalExeBindings.Read(localExe,65));
var dropped=UniversalMobyComposer.DroppableClasses(local,overlay,localExe,shared,[0],new HashSet<int>());
Check(dropped.Classes.Contains(0x55),"final local gem instance is classified for model/inert rewrite");
var deleted=UniversalMobyComposer.Compose(fresh.Target,local.Bytes,overlay.Bytes.ToArray(),localExe,
    k=>k==fresh.Target.Key?(fresh.Target,new UniversalLevelData(local.Bytes),overlay):lib.Get(k),shared,
    UniversalMobyExport.DefaultProtectedVram(Path.Combine(args[2],"fresh-zero.bin"),fresh.Target),[],removedRows:[0],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
Check(deleted.DroppedModelClasses.Contains(0x55) && deleted.TreasureDelta==-5 && Total(deleted.Executable,65)==0,"removed source value captured before final gem becomes inert");
if(args.Length==5)
{
    string project=args[3],edits=Path.Combine(project,"artisans-native-edits.json");
    string Hash(string p)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)));
    var paths=new[]{edits,Path.Combine(project,"artisans-terrain-edits.json"),Path.Combine(project,"artisans-native-terrain-texture-relocations.json")};
    var hashes=paths.ToDictionary(p=>p,Hash);var savedPlacements=UniversalPlacementReader.Read(edits);var savedRemoved=UniversalPlacementReader.ReadRemovedNativeRows(edits);
    Check(savedPlacements.Count(p=>!p.Retired)==8 && savedRemoved.Count==5,"actual saved8imports/5removals fixture");
    var sourcePlan=MobySourcePatchExporter.BuildPlan(args[0],Path.ChangeExtension(args[0],".cue"),"","",art.Level,edits);
    byte[] sourceStaged=originalExe.ToArray();
    foreach(var p in sourcePlan.Patches.Where(p=>p.Kind=="level-treasure-total"))
    {
        byte[] before=Convert.FromHexString(p.BeforeHexPreview.Replace(" ","")),after=Convert.FromHexString(p.AfterHexPreview.Replace(" ",""));
        Check(before.AsSpan().SequenceEqual(sourceStaged.AsSpan(Offset(10),2)),"actual ordinary counter patch preimage");after.CopyTo(sourceStaged,Offset(10));
    }
    int savedValue=savedRemoved.Sum(r=>UniversalMobyComposer.TreasureValue(art.Data.Row(r)));
    Check(Total(sourceStaged,10)==Total(originalExe,10)-savedValue,"ordinary saved removal target proof");
    var special=UniversalMobyExport.SpecialProvider(new UniversalJapaneseSource(args[4]));
    var saved=NativeTerrainTextureRelocationEditStore.Load(project,"artisans");
    var terrain=TerrainPatchExporter.BuildPlan(args[0],Path.ChangeExtension(args[0],".cue"),"","",art.Level,"",Path.Combine(project,"_local/terrain/artisans-runtime-terrain-source-search-native.json"),paths[1]);
    terrain=TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(terrain,art.Level,saved);
    var request=new NativeTerrainTexturePrivateRecordBatchRequest(args[0],Path.ChangeExtension(args[0],".cue"),Path.Combine(args[2],"unused-private-stage"),"",art.Level,saved,NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(terrain))
        {AdditionalProtectedRanges=UniversalTerrainReservations.Capture(lib,art.Level,savedPlacements,special,savedRemoved)};
    var host=UniversalCompositionHost.FromPrivateTerrainPlan(args[0],art.Level,NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(request));
    var probe=host.DryRun(lib,savedPlacements,special,savedRemoved);if(probe.Result==null)throw new InvalidDataException(probe.Error);
    var prepared=host.DestinationData;
    IEnumerable<(long Offset,int Length)> Protected(IReadOnlySet<int> dropped)=>NativeTexturePageOwnershipScanner.ScanExcludingResidentClasses(args[0],art.Level,dropped,prepared.Bytes).ProtectedRanges.Select(r=>(r.Offset,r.Length));
    var stagedResult=UniversalMobyComposer.Compose(art.Level,prepared.Bytes,host.DestinationOverlay.Bytes.ToArray(),sourceStaged,lib.Get,lib.SharedClasses,
        Protected(new HashSet<int>()),savedPlacements,special,savedRemoved,protectedVramAfterDrop:Protected,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations,removedTreasureAlreadyApplied:true);
    Check(Total(stagedResult.Executable,10)==Total(probe.Result.Executable,10),"actual ordinary-stage saved-project no double subtraction");
    Check(stagedResult.LevelData.AsSpan().SequenceEqual(probe.Result.LevelData) && stagedResult.Overlay.AsSpan().SequenceEqual(probe.Result.Overlay),"saved private68/eightpaint/8actors/save-map exact across counter paths");
    Check(Convert.ToHexStringLower(SHA256.HashData(probe.Result.LevelData))=="6316ef27f65500ea398f11bb595c3609b3b58f8937fdc6ceb3d3fd4d1cc87807","previous packaged8actor Artisans body bytepreserved");
    Check(hashes.All(p=>Hash(p.Key)==p.Value),"immutable saved project inputs unchanged");
}
UniversalSourceLibrary.RequireOriginal(args[0]);
File.WriteAllText(Path.Combine(args[2],"result.json"),JsonSerializer.Serialize(new{checks,Scope="known initial source-semantic treasure and source-bound disc readback",Fresh43=Total(composed.Executable,65),retailFive143=Total(fiveWithNativeTreasure.Executable,10),retailBefore=Total(originalExe,10),removedKnown=removed,retailAfter=Total(retail.Executable,10),Retired38=Total(retired.Executable,65),UnknownCompoundRewards="not assigned invented totals",RuntimeClaim=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("UNIVERSAL_TREASURE_SOURCE_OK checks="+checks);
