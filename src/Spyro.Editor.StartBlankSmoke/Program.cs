using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Scene;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
void Refuses(Action action,string name){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted: "+name);}
string root=Directory.CreateTempSubdirectory("spyro-start-blank-").FullName;
var catalog=new LevelCatalog([new LevelDefinition{Key="artisans",LevelId=10,SourceWadEntry=10,DisplayName="Artisans",SourceRecordCount=195}]);
var target=new SceneTargetBinding("artisans",10,10,9,UniversalSourceLibrary.OriginalUsaSha256,195);
var priorId=Guid.NewGuid().ToString("N");
var ledger=new UniversalPlacementLedger(1,[new(priorId,0)]);
var doc=SceneDocument.New("My Level") with{TargetBinding=target,Ledger=ledger};
Check(doc.ResolveTarget(catalog) is {LevelId:10,SourceWadEntry:10,SourceRecordCount:0,DisplayName:"My Level"},"same native slot and zero physical source actors");
Refuses(()=> (target with{SourceSha256="other"}).Resolve(catalog),"source revision");
Refuses(()=> (target with{BodyWadEntry=12,OverlayWadEntry=11}).Resolve(catalog),"catalog mismatch");
Refuses(()=> (target with{LevelId=15}).Validate(),"flight slot");
Refuses(()=> SceneDocumentStore.PathForTarget(root,"../artisans"),"path escape");
var empty=doc with{Surfaces=[]};empty.Validate();var projection=empty.ProjectGeometry();
Check(projection.Polygons.Count==0&&!projection.Bounds.IsEmpty&&float.IsFinite(projection.MinZ),"empty editor projection remains finite and does not invent floor");
string path=SceneDocumentStore.PathForTarget(root,"artisans");await empty.SaveAsync(path);
Check(SceneDocumentStore.ReadForTarget(root,catalog.Levels[0],catalog)!.Surfaces.Count==0,"zero-face save and reopen");
Check(SceneDocumentStore.LoadOverrides(root,catalog).Single().TargetBinding==target,"active override discovery");
await SceneDocument.New().SaveAsync(SceneDocumentStore.PathForTarget(root,"authored65"));
Check(SceneDocumentStore.ReadAll(root,catalog).Count==2,"legacy65 and replacement coexist");
File.WriteAllText(Path.Combine(root,"artisans-native-edits.json"),"{\"edits\":[],\"universalPlacementLedger\":"+JsonSerializer.Serialize(ledger)+"}");
File.WriteAllText(Path.Combine(root,"artisans-terrain-edits.json"),"paint and sculpt draft");
File.WriteAllText(Path.Combine(root,"toasty-native-edits.json"),"unrelated");
string art=Path.Combine(root,"private-painted.png");byte[] image=[1,2,3,4,5];File.WriteAllBytes(art,image);
File.WriteAllText(CustomTerrainTextureStore.ManifestPath(root,"artisans"),JsonSerializer.Serialize(new{textures=new[]{new{sourceImagePath=art}}}));
var before=File.ReadAllBytes(path);var blank=doc.StartBlank();
Check(blank.Objects.Count==0&&blank.Surfaces.Count==2&&blank.Ledger==ledger&&blank.Id!=doc.Id,"neutral new scene retains retired ledger and target");
try{await StartBlankRecoveryStore.ClearAsync(root,"artisans",blank,snapshotFaultForTesting:p=>File.WriteAllText(Path.Combine(p,"before.scene.json"),"corrupt"));throw new Exception("fault accepted");}
catch(InvalidDataException){checks++;}
Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(before)&&StartBlankRecoveryStore.Read(root,"artisans")==null,"snapshot fault leaves original scene/pointer unchanged");
try{await StartBlankRecoveryStore.ClearAsync(root,"artisans",blank,snapshotFaultForTesting:_=>File.WriteAllText(Path.Combine(root,"artisans-new-edit.json"),"external"));throw new Exception("new owner accepted");}catch(InvalidDataException){checks++;}
Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(before),"new sidecar during snapshot prevents clear publication");File.Delete(Path.Combine(root,"artisans-new-edit.json"));
try{await StartBlankRecoveryStore.ClearAsync(root,"artisans",blank,snapshotFaultForTesting:_=>File.WriteAllBytes(art,[9]));throw new Exception("changed art accepted");}catch(InvalidDataException){checks++;}
Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(before),"changed associated paint prevents clear publication");File.WriteAllBytes(art,image);
var record=await StartBlankRecoveryStore.ClearAsync(root,"artisans",blank);StartBlankRecoveryStore.RequireSnapshot(root,record);
Check(record.Assets!.Count==1&&File.ReadAllBytes(Path.Combine(root,"level-recovery","artisans",record.SnapshotId,record.Assets[0].SnapshotName)).AsSpan().SequenceEqual(image),"only referenced terrain artwork is byte-verified in recovery");
Check(SceneDocument.Read(path).Id==blank.Id&&record.Active,"verified clear switches scene");
Check(File.ReadAllText(Path.Combine(root,"artisans-terrain-edits.json"))=="paint and sculpt draft"&&File.ReadAllText(Path.Combine(root,"toasty-native-edits.json"))=="unrelated","clear keeps original and unrelated sidecar bytes");
var newId=Guid.NewGuid().ToString("N");var blankLedger=new UniversalPlacementLedger(1,[new(priorId,0),new(newId,1)]);
await (blank with{Ledger=blankLedger}).SaveAsync(path);
File.WriteAllText(Path.Combine(root,"artisans-terrain-edits.json"),"external change");
try{await StartBlankRecoveryStore.RestoreAsync(root,"artisans");throw new Exception("changed original accepted");}catch(InvalidDataException){checks++;}
Check(SceneDocument.Read(path).Id==blank.Id&&StartBlankRecoveryStore.Read(root,"artisans")!.Active,"failed restore retains current scene/recovery");
File.WriteAllText(Path.Combine(root,"artisans-terrain-edits.json"),"paint and sculpt draft");
try{await StartBlankRecoveryStore.RestoreAsync(root,"artisans",()=>File.WriteAllBytes(art,[8]));throw new Exception("restore art race accepted");}catch(InvalidDataException){checks++;}
Check(SceneDocument.Read(path).Id==blank.Id,"restore publication rechecks original artwork after awaiting backup");File.WriteAllBytes(art,image);
UniversalNativeLayoutStore.Bind(root,"artisans",195);
await StartBlankRecoveryStore.RestoreAsync(root,"artisans");
Check(SceneDocument.Read(path).Id==empty.Id&&SceneDocument.Read(path).Surfaces.Count==0,"restore previous authored geometry including empty scene");
Check(SceneDocument.Read(path).Ledger.Entries.Count==2&&SceneDocument.Read(path).Ledger.Entries[1].Id==newId,"restore keeps blank-era collectible reservation");
var future=new Moby{Index=0,TrueIndex=198,IsAdded=true,CrossLevelRequiredExporterFeature=UniversalMobyExport.RequiredExporterFeature,UniversalPlacementId=Guid.NewGuid().ToString("N")};
var afterLedger=UniversalPlacementLedger.Reconcile(Path.Combine(root,"artisans-native-edits.json"),[future]);
Check(afterLedger.Entries.Count==3&&afterLedger.Entries[2].Id==future.UniversalPlacementId,"restored retail new object cannot reuse blank-era slot");
await MobyEditStore.SaveAsync(Path.Combine(root,"artisans-native-edits.json"),[future],"Artisans");
using(var saved=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"artisans-native-edits.json")) ))
    Check(UniversalPlacementLedger.Read(saved.RootElement).Entries.Count==3,"normal MobyEditStore save includes blank-era retired slots");
Refuses(()=>StartBlankRecoveryStore.ReconcileLifetime(root,"artisans",new(1,[new(Guid.NewGuid().ToString("N"),0)]),195),"divergent slot ownership");
Check(!StartBlankRecoveryStore.Read(root,"artisans")!.Active,"durable restore state");
File.Delete(path);var originalBlank=SceneDocument.New("Artisans") with{TargetBinding=target,Ledger=afterLedger};
await StartBlankRecoveryStore.ClearAsync(root,"artisans",originalBlank);
await StartBlankRecoveryStore.RestoreAsync(root,"artisans");
Check(!File.Exists(path)&&File.Exists(Path.Combine(root,"artisans-native-edits.json")),"original retail restored by deactivating only override");
Check(Directory.EnumerateFiles(Path.Combine(root,"level-recovery"),"after-*.scene.json",SearchOption.AllDirectories).Any(),"recovery preserves post-clear work");
Check(!Directory.EnumerateFiles(root,"*.bin",SearchOption.AllDirectories).Any(),"no native image or source opened");
Console.WriteLine($"START_BLANK_SOURCE_FREE_OK checks={checks} fixtures={root}");
