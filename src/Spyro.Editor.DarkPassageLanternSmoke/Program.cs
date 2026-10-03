using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var s=lib.Get("darkpassage");var dh=lib.Get("darkhollow");int checks=0;List<object> cases=[];
void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
void Refuse(Action a,string name){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted "+name);}
int I(byte[] b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(o));
UniversalMobyPlacement P(int row,int delta=20000,string? group=null){var r=s.Data.Row(row);return new("darkpassage",row,I(r,12)+delta,I(r,16)-10000,I(r,20)+2000,EditorId:Guid.NewGuid().ToString("N")){LinkGroupId=group};}
UniversalComposeResult Compose(string dst,IReadOnlyList<UniversalMobyPlacement> ps,IReadOnlyList<int>? removed=null){var d=lib.Get(dst);return UniversalMobyComposer.Compose(d.Level,d.Data.Bytes.ToArray(),d.Overlay.Bytes.ToArray(),lib.Disc.Exe.ToArray(),lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],d.Level),ps,removedRows:removed,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);}
UniversalDarkPassageLanternGroups.ProveSource(s.Data,s.Overlay);
foreach(int root in new[]{19,20,21,22,23,24,25,37,68,26,27,29,30,38,66,67})
{
 var group=UniversalPlacementGroups.Expand(lib.Get,P(root));Check(group.Count== (root is 68 or 66 or 67?3:root is 21 or 22 or 24?1:2),"complete group root="+root+" count="+group.Count);
 foreach(string dst in new[]{"darkhollow","darkpassage"}){
  var result=Compose(dst,group);var data=new UniversalLevelData(result.LevelData);Check(result.UnresolvedObjectLinks==0,"no cleared links");
  for(int n=0;n<group.Count;n++){
   var original=s.Data.CaptureRow("darkpassage",group[n].SourceRowIndex);var row=data.Row(result.NewRowIndices[n]);int po=I(row,0),so=data.Subfiles()[3].Offset;
   Check(I(row,12)==group[n].X&&I(row,16)==group[n].Y&&I(row,20)==group[n].Z,"editable translated group");Check(row[0x4A]==255,"native culling sentinel");
   if(original.ClassId==0x7A)Check(I(data.Bytes,so+po+24)==(dst=="darkpassage"?I(original.Properties,24):-1),"owned context ordinal");
   else {int target=I(original.Properties,12),position=group.ToList().FindIndex(p=>p.SourceRowIndex==target);Check(I(data.Bytes,so+po+12)==result.NewRowIndices[position],"physical lantern index");Check(!data.Scene().Fixups.Contains(po+12),"integer not pointer/save fixup");}
  }
  cases.Add(new{root,dst,rows=group.Select(p=>p.SourceRowIndex),physical=result.NewRowIndices,classes=result.Classes.Select(c=>new{c.ClassId,c.DestinationClassId,c.BehaviorImported}),result.Warnings,result.ParticleDependencies});
 }
}
var full=UniversalPlacementGroups.Expand(lib.Get,P(68)).ToArray();
Refuse(()=>Compose("darkhollow",full.Take(2).ToArray()),"missing shared dog");
Refuse(()=>Compose("darkhollow",[full[0],full[1],full[1],full[2]]),"duplicate dog");
Refuse(()=>Compose("darkhollow",full.Select((p,n)=>n==1?p with{X=p.X+1}:p).ToArray()),"different translation");
Refuse(()=>Compose("darkhollow",full.Select((p,n)=>n==1?p with{LinkGroupId="other"}:p).ToArray()),"wrong group");
Refuse(()=>Compose("darkhollow",full.Select((p,n)=>n==0?p with{RowByteOverrides=new Dictionary<int,byte>{{0x49,1}}}:p).ToArray()),"changed initial lighting");
Refuse(()=>Compose("darkhollow",full.Select((p,n)=>n==0?p with{RowByteOverrides=new Dictionary<int,byte>{{0x3E,99}}}:p).ToArray()),"changed animation frame");
var copies=UniversalPlacementGroups.Expand(lib.Get,P(68,20000,"one")).Concat(UniversalPlacementGroups.Expand(lib.Get,P(68,30000,"two"))).ToArray();var two=Compose("darkhollow",copies);var td=new UniversalLevelData(two.LevelData);
foreach(int n in new[]{1,2,4,5})Check(I(td.Bytes,td.Subfiles()[3].Offset+I(td.Row(two.NewRowIndices[n]),0)+12)==two.NewRowIndices[n<3?0:3],"independent copy index");
Refuse(()=>UniversalIndexedRows.RequireNativeRemoval("darkpassage",s.Data,s.Data,s.Overlay,new HashSet<int>{68}),"surviving native dog");
UniversalIndexedRows.RequireNativeRemoval("darkpassage",s.Data,s.Data,s.Overlay,new HashSet<int>{66,67,68});Check(true,"whole native group removal");
var rawClasses=(IReadOnlyList<int>)new[]{0x7A,0x7C};var map=new Dictionary<int,int>{{0x7A,509},{0x7C,508}};var maps=new Dictionary<string,IReadOnlyDictionary<int,int>>{{"darkpassage",map}};
Refuse(()=>UniversalBehaviorBundleBuilder.Build(dh.Overlay,[("darkpassage",s.Overlay,rawClasses)],0x80090000,classMaps:maps,nativeExecutable:lib.Disc.Exe),"raw Bundle missing context");
var context=UniversalDarkPassageLanternGroups.Issue("darkhollow",dh.Data,dh.Overlay,s.Data,s.Overlay,full,lib.Disc.Exe)!;var contexts=new Dictionary<string,UniversalDarkPassageLanternContext>{{"darkpassage",context}};
UniversalBehaviorBundleBuilder.Build(dh.Overlay,[("darkpassage",s.Overlay,rawClasses)],0x80090000,classMaps:maps,nativeExecutable:lib.Disc.Exe,darkLanternContexts:contexts);Check(true,"raw issued alias bundle");
var part=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay,rawClasses,"proof",map).Part;context.RequireCopied(part,s.Overlay,rawClasses,map);Check(true,"exact packet");
foreach(uint pc in new uint[]{0x800808EC,0x80080984,0x80080AB8,0x80080BAC,0x80080910,0x8008098C,0x80080930,0x800809A8,0x80080AC0,0x80080BB4}){int at=part.Items.FindIndex(i=>i.Orig==pc);var old=part.Items[at];part.Items[at]=old with{Word=old.Word^4};Refuse(()=>context.RequireCopied(part,s.Overlay,rawClasses,map),"packet mutation");part.Items[at]=old;}
var guardIndex=part.Items.FindIndex(i=>i.Orig==0x800808EC);var branch=part.Items[guardIndex];part.Items[guardIndex]=branch with{Fix=branch.Fix with{Orig=branch.Fix.Orig+4}};Refuse(()=>context.RequireCopied(part,s.Overlay,rawClasses,map),"wrong relocated target");part.Items[guardIndex]=branch;
part.Items.Insert(guardIndex,part.Items[guardIndex]);Refuse(()=>context.RequireCopied(part,s.Overlay,rawClasses,map),"duplicate incoming guard");part.Items.RemoveAt(guardIndex);
Refuse(()=>UniversalDarkPassageLanternGroups.Capture(s.Data,s.Overlay,s.Data.CaptureRow("shadow",19)),"mislabeled row source");

// Exact native helper, source, constructor and emitted-owner mutation guards.
foreach(uint pc in new uint[]{0x8002B390,0x8002B440,0x80058AE8,0x80058B60,0x8003A720,0x8003A798,0x800526A8,0x800529C8,0x80017718,0x80017788,0x800176EC}){var exe=lib.Disc.Exe.ToArray();exe[checked((int)(pc-UniversalExeBindings.ExeBase))]^=1;Refuse(()=>UniversalDarkPassageLanternGroups.Issue("darkhollow",dh.Data,dh.Overlay,s.Data,s.Overlay,full,exe),"native owner mutation");}
var sourceData=new UniversalLevelData(s.Data.Bytes.ToArray());sourceData.Bytes[^1]^=1;Refuse(()=>UniversalDarkPassageLanternGroups.ProveSource(sourceData,s.Overlay),"source data mutation");
var template=UniversalIndexedRows.Capture(s.Data,s.Overlay,s.Data.CaptureRow("darkpassage",68));var properties=template.Properties.ToArray();BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(24),-1);Refuse(()=>context.Prepare(template with{Properties=properties},template.Row.ToArray()),"manufactured negative original ordinal");
var targetTemplate=UniversalIndexedRows.Capture(s.Data,s.Overlay,s.Data.CaptureRow("darkpassage",66));var fake=template.Row.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(fake.AsSpan(0x36),0x20);Refuse(()=>UniversalIndexedRows.Resolve(targetTemplate,[template with{Row=fake},targetTemplate]),"wrong companion class");
foreach(int c in rawClasses){var init=UniversalBehaviorBundleBuilder.BuildSpecializedInit(s.Overlay,[c],"ctor").Part;context.RequireCopiedInit(init,s.Overlay,[c]);Check(true,"exact constructor");int at=init.Items.FindIndex(i=>i.Orig==0x80084BC8);var old=init.Items[at];init.Items[at]=old with{Word=old.Word^4};Refuse(()=>context.RequireCopiedInit(init,s.Overlay,[c]),"constructor mutation");}
Refuse(()=>UniversalBehaviorBundleBuilder.Build(dh.Overlay,[("shadow",s.Overlay,rawClasses)],0x80090000,nativeExecutable:lib.Disc.Exe),"raw mislabeled original source");
Refuse(()=>UniversalBehaviorBundleBuilder.Build(dh.Overlay,[("darkpassage",s.Overlay,rawClasses)],0x80090000,nativeExecutable:lib.Disc.Exe),"raw identity map missing context");
int inserted=part.Items.FindIndex(i=>i.Orig==0x80080910);part.Items.Insert(inserted,new(0xAE840018,default,null,null));Refuse(()=>context.RequireCopied(part,s.Overlay,rawClasses,map),"inserted ordinal writer");part.Items.RemoveAt(inserted);
var before=part.Items[inserted-1];part.Items[inserted-1]=part.Items[inserted+1];part.Items[inserted+1]=before;Refuse(()=>context.RequireCopied(part,s.Overlay,rawClasses,map),"reordered incoming packet");
var editedNative=new UniversalLevelData(s.Data.Bytes.ToArray());var layout=editedNative.Scene();int nativeProps=I(editedNative.Row(68),0);editedNative.Put32(editedNative.Subfiles()[3].Offset+nativeProps+24,-1);Refuse(()=>UniversalIndexedRows.RequireNativeRemoval("darkpassage",editedNative,s.Data,s.Overlay,new HashSet<int>{45}),"edited native environment owner");
var terrainMutant=new UniversalLevelData(s.Data.Bytes.ToArray());terrainMutant.Bytes[terrainMutant.Subfiles()[1].Offset+16]^=1;Check(!UniversalDarkPassageLanternGroups.HasSourceTerrain(terrainMutant,s.Data),"changed native terrain refuses original lighting context");
var frame=full.Select((p,n)=>n==0?p with{RowByteOverrides=new Dictionary<int,byte>{{0x48,2}}}:p).ToArray();Refuse(()=>Compose("darkhollow",frame),"authored initial state");
Refuse(()=>UniversalBehaviorBundleBuilder.Build(lib.Get("peacekeepers").Overlay,[("darkpassage",s.Overlay,rawClasses)],0x80090000,classMaps:maps,nativeExecutable:lib.Disc.Exe,darkLanternContexts:contexts),"context wrong destination");
foreach(int row in new[]{26,27,29,30,38,66,67}){var native=Compose("darkpassage",[P(row)]);var data=new UniversalLevelData(native.LevelData);Check(I(data.Bytes,data.Subfiles()[3].Offset+I(data.Row(native.NewRowIndices[0]),0)+12)==I(s.Data.CaptureRow("darkpassage",row).Properties,12),"retained original native lantern index");}
var reopened=JsonSerializer.Deserialize<UniversalMobyPlacement[]>(JsonSerializer.Serialize(full))!;Check(reopened.Select(p=>p.LinkGroupId).SequenceEqual(full.Select(p=>p.LinkGroupId)),"saved group identity");var reopenedResult=Compose("darkhollow",reopened);Check(reopenedResult.NewRowIndices.SequenceEqual(new[]{128,129,130}),"reopened group physical readback");
Refuse(()=>Compose("darkpassage",[P(26)],new[]{19,26}),"retained native target removed while copied dog survives");
var upper=UniversalPlacementGroups.Expand(lib.Get,P(68) with{SourceLevelKey="DarkPassage"});Check(Compose("darkhollow",upper).NewRowIndices.Count==3,"case-insensitive original source");
Refuse(()=>UniversalBehaviorBundleBuilder.Build(lib.Get("peacekeepers").Overlay,[("DarkPassage",s.Overlay,rawClasses)],0x80090000,classMaps:new Dictionary<string,IReadOnlyDictionary<int,int>>{{"DarkPassage",map}},nativeExecutable:lib.Disc.Exe,darkLanternContexts:new Dictionary<string,UniversalDarkPassageLanternContext>(StringComparer.OrdinalIgnoreCase){{"darkpassage",context}}),"case source cannot bypass destination certificate");
File.WriteAllText(args[2],JsonSerializer.Serialize(new{checks,cases,Marker="DARK_PASSAGE_LANTERN_ACTOR_CONTEXT_OK",Runtime="pending",Excluded="Imported terrain recoloring, runtime gameplay and unproved collection particles"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"DARK_PASSAGE_LANTERN_ACTOR_CONTEXT_OK checks={checks}");
