using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Workspace;

// NEW_OUTPUT [ARTISANS_ORIGINAL_CACHE DAMAGED_PRIVATE_MANIFEST]
if(args.Length==0)args=[Path.Combine(Path.GetTempPath(),"spyro-added-moby-edits-"+Guid.NewGuid().ToString("N"))];
if(args.Length is not (1 or 3))throw new ArgumentException("NEW_OUTPUT [ARTISANS_ORIGINAL_CACHE DAMAGED_PRIVATE_MANIFEST]");
string output=Path.GetFullPath(args[0]);if(Directory.Exists(output))throw new IOException("Existing regression evidence preserved.");Directory.CreateDirectory(output);
int checks=0;void Check(bool b,string message){checks++;if(!b)throw new InvalidDataException(message);}
string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
string id50="11111111111111111111111111111111",id100="22222222222222222222222222222222";
List<Moby> Originals(){
 if(args.Length==3){var rows=MobyLoader.LoadCached(args[1]).ToList();MobyMetadataEnricher.Apply(new EditorWorkspace(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(args[1])))!),"artisans",rows);return rows;}
 return Enumerable.Range(0,174).Select(i=>new Moby{Index=i,TrueIndex=i,LegacyIndex=MobyLoader.GetLegacyAliasIndex(i),Position=new(i,2*i,3*i),OriginalPosition=new(i,2*i,3*i),Type=0,OriginalType=0,State=0,OriginalState=0,YawByte=0,OriginalYawByte=0,SourceByte36=30,OriginalSourceByte36=30,SourceByte37=1,OriginalSourceByte37=1,SourceByte4F=0,OriginalSourceByte4F=0,Flag4A=16,OriginalFlag4A=16,Flag4B=255,OriginalFlag4B=255,Label=i==159?"Ambient sound emitter":"Original "+i,OriginalLabel=i==159?"Ambient sound emitter":"Original "+i,Color=ColorRgba.FromRgb(30,40,50),OriginalColor=ColorRgba.FromRgb(30,40,50)}).ToList();
}
var baseline=Originals();Check(baseline.Count==174&&baseline.Select(m=>m.TrueIndex).Order().SequenceEqual(Enumerable.Range(0,174)),"complete original174retail rows");Check(baseline.Single(m=>m.TrueIndex==159).LegacyIndex==175,"real native/legacy alias collision159/175");
var originalSnapshots=baseline.ToDictionary(m=>m.TrueIndex,m=>JsonSerializer.Serialize(m));
void KeepOriginals(IList<Moby> rows,int? edited=null){foreach(var m in rows.Where(m=>!m.IsAdded)){if(m.TrueIndex==edited)continue;Check(JsonSerializer.Serialize(m)==originalSnapshots[m.TrueIndex],"complete original actor unchanged T"+m.TrueIndex);}Check(rows.Count(m=>!m.IsAdded)==174,"original actor count preserved");}
Moby Add(string id,int index,string identity)=>new(){Index=index,TrueIndex=index,LegacyIndex=MobyLoader.GetLegacyAliasIndex(index),Position=new(560,index,544),OriginalPosition=new(560,index,544),Type=0x18,OriginalType=0x18,State=0,OriginalState=0,YawByte=0,OriginalYawByte=0,SourceByte36=id==UniversalHighValueGems.TealId?0xCF:0xDA,OriginalSourceByte36=id==UniversalHighValueGems.TealId?0xCF:0xDA,SourceByte37=1,OriginalSourceByte37=1,SourceByte4F=0,OriginalSourceByte4F=0,Flag4A=0x40,OriginalFlag4A=0x40,Flag4B=255,OriginalFlag4B=255,Label=id,OriginalLabel=id,Color=ColorRgba.FromRgb(1,2,3),CrossLevelTemplateId=UniversalSpecialCatalog.TemplatePrefix+id,CrossLevelFamily="universal",CrossLevelSourceLevelKey="editor",CrossLevelSourceLevelName="USA gem variant",CrossLevelRequiredExporterFeature=UniversalMobyExport.RequiredExporterFeature,UniversalPlacementId=identity,IsAdded=true};
string saved=Path.Combine(output,"seed.json");await MobyEditStore.SaveAsync(saved,[Add(UniversalHighValueGems.TealId,174,id50),Add(UniversalHighValueGems.RainbowId,175,id100)],"Artisans");var seed=JsonNode.Parse(File.ReadAllText(saved))!.AsObject();
List<object> cases=[];
async Task Run(string name,Action<JsonObject> mutate,bool universal=true){
 var manifest=(JsonObject)seed.DeepClone();mutate(manifest);string path=Path.Combine(output,name+".json");File.WriteAllText(path,manifest.ToJsonString());var rows=Originals();var beforeLedger=UniversalPlacementLedger.Read(JsonDocument.Parse(manifest.ToJsonString()).RootElement);
 Check(MobyEditStore.Load(path,rows)==2,name+" bothadditions loaded");KeepOriginals(rows);Check(rows.Count==176&&rows.Select(m=>m.Index).Distinct().Count()==176&&rows.Select(m=>m.TrueIndex).Distinct().Count()==176,name+" unique editor indices");
 var identities=rows.Where(m=>m.IsAdded).Select(m=>m.UniversalPlacementId).Order().ToArray();Check(identities.SequenceEqual(new[]{id50,id100}),name+" placement identities preserved");
 if(universal){Check(rows.Single(m=>m.UniversalPlacementId==id50).Gem.Value==50&&rows.Single(m=>m.UniversalPlacementId==id100).Gem.Value==100,name+" actual treasure values");Check(rows.Where(m=>m.IsAdded).All(m=>m.Color==m.Gem.Color),name+" current highgem colors override stale saved palette");}
 var indexes=rows.Where(m=>m.IsAdded).ToDictionary(m=>m.UniversalPlacementId,m=>(m.Index,m.TrueIndex));
 for(int n=0;n<3;n++){Check(MobyEditStore.Load(path,rows)==2,name+" repeatload");KeepOriginals(rows);Check(rows.Count==176,name+" repeatload no duplicateactors");}
 await MobyEditStore.SaveAsync(path,rows,"Artisans");using(var doc=JsonDocument.Parse(File.ReadAllBytes(path))){var ledger=UniversalPlacementLedger.Read(doc.RootElement);Check(ledger.Entries.SequenceEqual(beforeLedger.Entries),name+" append-only ledger slots unchanged");}
 var reopened=Originals();Check(MobyEditStore.Load(path,reopened)==2,name+" saved/reopened");KeepOriginals(reopened);Check(reopened.Where(m=>m.IsAdded).All(m=>indexes[m.UniversalPlacementId]==(m.Index,m.TrueIndex)),name+" repaired indices remain stable");
 if(universal){var placements=UniversalPlacementReader.Read(path);Check(placements.Select(p=>p.PersistentSlot).SequenceEqual(new int?[]{0,1}),name+" native save slots0/1");Check(placements[0].SpecialId==UniversalHighValueGems.TealId&&(placements[1].SpecialId==UniversalHighValueGems.RainbowId||placements[1].Retired),name+" native source identities or permanent retirement");}
 cases.Add(new{Name=name,OriginalRows=174,AddedRows=2,StablePlacementIds=identities,RepairedIndices=indexes});
}
JsonObject Edit(JsonObject root,int n)=>(JsonObject)root["edits"]![n]!;
await Run("normal-high-gems",_=>{});
await Run("legacy175-aliases-original159",root=>Edit(root,1)["legacyIndex"]=175);
await Run("corrupt-original-true-and-index",root=>{Edit(root,1)["trueIndex"]=159;Edit(root,1)["index"]=159;Edit(root,1)["legacyIndex"]=175;Edit(root,1)["labelOriginal"]="Ambient sound emitter";});
await Run("index-collision-only",root=>Edit(root,1)["index"]=159);
await Run("true-index-collision-only",root=>Edit(root,1)["trueIndex"]=159);
await Run("missing-true-index",root=>{Edit(root,1)["trueIndex"]=null;Edit(root,1)["legacyIndex"]=175;});
await Run("duplicate-added-indices",root=>{Edit(root,1)["index"]=174;Edit(root,1)["trueIndex"]=174;});
await Run("editKind-add-without-added-flag",root=>Edit(root,1)["added"]=false);
await Run("retired-added-row",root=>Edit(root,1)["removed"]=true);
// Ordinary copied actors use the same safe addition path; this is not special-ID-only.
var ordinary=(JsonObject)seed.DeepClone();foreach(var node in ordinary["edits"]!.AsArray()){var e=node!.AsObject();e["crossLevelTemplate"]=null;e["universalPlacementId"]=null;}ordinary["universalPlacementLedger"]=null;string ordinaryPath=Path.Combine(output,"ordinary-native-clones.json");File.WriteAllText(ordinaryPath,ordinary.ToJsonString());var ordinaryRows=Originals();Check(MobyEditStore.Load(ordinaryPath,ordinaryRows)==2,"ordinaryclones safelyadded");KeepOriginals(ordinaryRows);await MobyEditStore.SaveAsync(ordinaryPath,ordinaryRows,"Artisans");for(int n=0;n<3;n++){Check(MobyEditStore.Load(ordinaryPath,ordinaryRows)==2&&ordinaryRows.Count==176,"ordinaryclonesrepeatload");KeepOriginals(ordinaryRows);}cases.Add(new{Name="ordinary-native-clones"});
// The legacy lookup remains necessary for original actor update manifests.
string update=Path.Combine(output,"original-legacy-update.json");File.WriteAllText(update,"{\"edits\":[{\"index\":175,\"edited\":{\"x\":10,\"y\":20,\"z\":30}}]}");var updated=Originals();Check(MobyEditStore.Load(update,updated)==1&&updated.Single(m=>m.TrueIndex==159).Position==new Vector3f(10,20,30),"originallegacyupdates still resolve159");KeepOriginals(updated,159);
// A deleted addition disappears on repeated load, but its collected-bit slot stays retired.
string retired=Path.Combine(output,"manifest-removes-added-object.json");var stale=Originals();MobyEditStore.Load(saved,stale);stale.Remove(stale.Single(m=>m.UniversalPlacementId==id100));File.Copy(saved,retired);await MobyEditStore.SaveAsync(retired,stale,"Artisans");MobyEditStore.Load(retired,stale);KeepOriginals(stale);Check(stale.Count==175&&UniversalPlacementReader.Read(retired)[1].Retired,"repeatload drops absentaddition butretains retiredledger slot");
// Parse failures must not remove existing additions or reset live edits.
string malformed=Path.Combine(output,"malformed.json");File.WriteAllText(malformed,"{");string beforeMalformed=JsonSerializer.Serialize(stale);bool parseRejected=false;try{MobyEditStore.Load(malformed,stale);}catch(JsonException){parseRejected=true;}Check(parseRejected&&JsonSerializer.Serialize(stale)==beforeMalformed,"malformedmanifest leaves currentscene intact");
if(args.Length==3){
 string corruptedHash=Hash(args[2]);string recovery=Path.Combine(output,"actual-private-damaged-manifest-recovered.json");File.Copy(args[2],recovery);using var original=JsonDocument.Parse(File.ReadAllBytes(args[2]));var previousLedger=UniversalPlacementLedger.Read(original.RootElement);var rows=Originals();Check(MobyEditStore.Load(recovery,rows)==2,"actualdamagedmanifest recovered2addedgems");KeepOriginals(rows);Check(rows.Count==176&&rows.Single(m=>m.TrueIndex==159).Label==baseline.Single(m=>m.TrueIndex==159).Label,"actualAmbientT159preserved");Check(rows.Where(m=>m.IsAdded).All(m=>m.IsHighValueGem&&m.Color==m.Gem.Color),"actualdamaged colors refreshed");await MobyEditStore.SaveAsync(recovery,rows,"Artisans");using var repaired=JsonDocument.Parse(File.ReadAllBytes(recovery));Check(UniversalPlacementLedger.Read(repaired.RootElement).Entries.SequenceEqual(previousLedger.Entries),"actualdamagedGUIDslots preserved");var reopen=Originals();MobyEditStore.Load(recovery,reopen);KeepOriginals(reopen);Check(Hash(args[2])==corruptedHash,"originalprivateevidence untouched");cases.Add(new{Name="actual-private-damaged-manifest",PreviousSha256=corruptedHash,Recovered= recovery,SaveRows=UniversalPlacementReader.Read(recovery).Select(p=>p.PersistentSlot).ToArray()});
}
var receipt=new{Marker="ADDED_MOBY_FULL_RETAIL_SAVE_REOPEN_REGRESSION_OK",Checks=checks,Cases=cases,OriginalRetailRows=174,SourceCache=args.Length==3?Path.GetFullPath(args[1]):null,SourceCacheSha256=args.Length==3?Hash(args[1]):null,CoreSha256=Hash(typeof(MobyEditStore).Assembly.Location),UserProjectWrites=0,NativeRuntimeTested=false};File.WriteAllText(Path.Combine(output,"receipt.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{receipt.Marker,checks,Cases=cases.Count}));
