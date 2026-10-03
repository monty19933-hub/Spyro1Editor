using System.Buffers.Binary;using System.Text.Json;using Spyro.Editor.Core.Universal;using Spyro.Editor.Core.Levels;
int checks=0;void Check(bool b,string s){if(!b)throw new Exception(s);checks++;}void Refuse(Action action,string s){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal "+s);}
UniversalSourceLibrary.RequireOriginal(args[0]);var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));foreach(var level in LevelCatalog.Load(args[1]).Levels){var nativeSource=lib.Get(level.Key);Check(UniversalTwilightTrooperOwnership.SourceFamily(nativeSource.Overlay)==(level.Key=="twilightharbor"),"source-family delegate cannot claim another original level by coincident callback values/PCs");}
var source=lib.Get("twilightharbor");var host=lib.Get("darkhollow");var particleCertificate=UniversalTwilightTrooperParticles.Capture(source.Data,source.Overlay,host.Overlay,lib.Disc.Exe,true);checks++;var sw=UniversalDispatchAnalysis.FindUpdateSwitch(source.Overlay);List<object> results=[];
foreach(int cls in new[]{0x93,0x94})
{
 var trace=UniversalDispatchAnalysis.Trace(source.Overlay,sw.TreeStart,cls,sw.Seeds,sw.ClassRegister);var closure=UniversalDispatchAnalysis.Closure(source.Overlay,sw.Function,[trace.Stop],new HashSet<uint>{sw.TreeStart});var deps=UniversalDispatchAnalysis.Dependencies(source.Overlay,closure.Instructions);
 Check(deps.SpawnedClasses.Contains(0x1FB),"real long-edge1FB dependency enrolled");
 var changed=source.Overlay.Bytes.ToArray();changed[(int)(0x8007F404-UniversalOverlay.Base)]^=1;Refuse(()=>UniversalTwilightTrooperOwnership.SpawnArguments(new UniversalOverlay(changed,source.Overlay.Bindings),closure.Instructions,0x8007FA40),"changed source edge producer");
 foreach(int row in Enumerable.Range(0,source.Data.Scene().RowCount).Where(r=>BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(r).AsSpan(0x36))==cls))
 {
  var placement=new UniversalMobyPlacement("twilightharbor",row,14500,63750,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:0);
  try
  {
   var composed=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),[placement],sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
   Check(composed.Classes.Any(c=>c.ClassId==0x1FB),"actual composition retains1FB child");
   var readback=new UniversalLevelData(composed.LevelData);var imported=readback.CaptureRow("readback",composed.NewRowIndices.Single());var original=source.Data.CaptureRow("twilightharbor",row);var expectedRow=original.Row.ToArray();
   for(int axis=0;axis<3;axis++)BinaryPrimitives.WriteInt32LittleEndian(expectedRow.AsSpan(12+axis*4),axis==0?placement.X:axis==1?placement.Y:placement.Z);
   var expected=UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,original,expectedRow);
   Check(imported.InternalPointers.SequenceEqual(expected.InternalPointers)&&imported.ExternalRowPointers.Count==0&&imported.DroppedExternalPointers==0,"independent final native fixups contain exact paths and no invented actor/index links");
   Check(imported.Properties.Length==expected.Properties.Length,"private property length readback");
   for(int at=0;at<expected.Properties.Length;at+=4)if(!expected.InternalPointers.Any(q=>q.Offset==at))Check(imported.Properties.AsSpan(at,4).SequenceEqual(expected.Properties.AsSpan(at,4)),"final path XYZ/activation rectangles/native latches/timings readback");
   Check(imported.Row[0x43]==255&&imported.Row[0x4A]==255,"native pod-independent activation and import visibility readback");
   Check(composed.ParticleDependencies.All(d=>d.Status.StartsWith("source-certified")),"every actual particle is certified including shared B0 explosion70");
   Check(cls!=0x94||composed.ParticleDependencies.Any(d=>d.TypeId==70),"actual shared callback effect70 remains enrolled");
   Check(cls!=0x94||composed.Behavior.Single(b=>b.SourceLevelKey=="twilightharbor").LinkedClassReadSites.Contains(0x8007F568),"linked class comparison owner reported");
   Check(composed.Classes.All(c=>readback.Registrations().Any(r=>r.ClassId==c.DestinationClassId)),"child models retained under actual destination classes");
   var artifact=Path.Combine(Path.GetDirectoryName(args[2])!,"composed",$"{cls:X3}-row{row}");Directory.CreateDirectory(artifact);File.WriteAllBytes(Path.Combine(artifact,"level.data"),composed.LevelData);File.WriteAllBytes(Path.Combine(artifact,"overlay.bin"),composed.Overlay);File.WriteAllBytes(Path.Combine(artifact,"staged.exe"),composed.Executable);
   results.Add(new{cls,row,status="composed-readback-verified",composed.Classes,composed.NewRowIndices,composed.Warnings,composed.ParticleDependencies,composed.Behavior});
  }catch(Exception e){throw new Exception($"Original{cls:X} row{row} must compose/readback",e);}
 }
}

Check(results.Count==14,"all14 genuine original actor variants completed");
var allClasses=new[]{0x93,0x94,0x1FB,0xB0,0x9C,0x1CA,0x1CB};var alias=allClasses.Select((c,i)=>(c,i)).ToDictionary(x=>x.c,x=>500-x.i);
UniversalAsmPart Part(IReadOnlyDictionary<int,int>? map=null)=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,allClasses,"twilight_test",map).Part;
var clean=Part(alias);UniversalTwilightTrooperOwnership.RequireCopiedInputs(clean,source.Overlay,allClasses,alias,lib.Disc.Exe);checks++;
for(int at=0;at<clean.Items.Count;at++)
{
 var bad=Part(alias);var item=bad.Items[at];if(item.Word is uint w){bad.Items[at]=item with{Word=w^1};Refuse(()=>UniversalTwilightTrooperOwnership.RequireCopiedInputs(bad,source.Overlay,allClasses,alias,lib.Disc.Exe),"every emitted input/producer/state/path/class word");}
}
foreach(uint target in new[]{0x8007F404u,0x8007FA38u,0x8007FA40u,0x8007F568u,0x8007FADC,0x8007EFF0})
{
 var bad=Part(alias);bad.Items.Insert(1,new(UniversalMips.EncodeJ(2,target),new(UniversalFixKind.Jump,target),0x80070000,null));Refuse(()=>UniversalTwilightTrooperOwnership.RequireCopiedInputs(bad,source.Overlay,allClasses,alias,lib.Disc.Exe),"new outside branch into producer/callback/linked-read/path interior");
 bad=Part(alias);bad.Tables["injected_table"]=[target];Refuse(()=>UniversalTwilightTrooperOwnership.RequireCopiedInputs(bad,source.Overlay,allClasses,alias,lib.Disc.Exe),"injected table entry into owned interior");
}
foreach(int cls in allClasses)
{
 var init=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source.Overlay,[cls],"init_test").Part;UniversalTwilightTrooperOwnership.RequireInitializer(init,source.Overlay,cls,lib.Disc.Exe);checks++;
 foreach(int at in Enumerable.Range(0,init.Items.Count).Where(i=>init.Items[i].Word!=null))
 {var bad=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source.Overlay,[cls],"init_test").Part;bad.Items[at]=bad.Items[at] with{Word=bad.Items[at].Word^1};Refuse(()=>UniversalTwilightTrooperOwnership.RequireInitializer(bad,source.Overlay,cls,lib.Disc.Exe),"every constructor/allocator/parent/tag/model word");}
}
foreach(var row in Enumerable.Range(0,source.Data.Scene().RowCount).Select(r=>source.Data.CaptureRow("twilightharbor",r)).Where(UniversalTwilightTrooperPlacement.Applies))
{
 var moved=row.Row.ToArray();int[] delta={7777,-4444,512};for(int axis=0;axis<3;axis++)BinaryPrimitives.WriteInt32LittleEndian(moved.AsSpan(12+axis*4),checked(BinaryPrimitives.ReadInt32LittleEndian(row.Row.AsSpan(12+axis*4))+delta[axis]));
 var rebased=UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,row,moved);Dictionary<int,int> coordinates=[];
 foreach(var path in row.InternalPointers)for(int node=0;node<row.Properties[path.Target];node++)for(int axis=0;axis<3;axis++)coordinates.Add(path.Target+8+16*node+4*axis,axis);
 foreach(int rect in row.ClassId==0x93?new[]{0x34,0x4C,0x64}:new[]{0x10,0x44,0x5C})for(int axis=0;axis<3;axis++)coordinates.Add(rect+4*axis,axis);
 for(int at=0;at<row.Properties.Length;at+=4)
 {Check(BinaryPrimitives.ReadInt32LittleEndian(rebased.Properties.AsSpan(at))==checked(BinaryPrimitives.ReadInt32LittleEndian(row.Properties.AsSpan(at))+(coordinates.ContainsKey(at)?delta[coordinates[at]]:0)),"world coordinates shift; timings/flags/latches/pointers/rect dimensions remain native");}
}
foreach(var sourceWord in new[]{0x8007F404u,0x8007FA18u,0x8007FA30u,0x8007FA40u,0x8007F568u,0x8007FADCu,0x80080620u,0x80080634u})
{var bad=source.Overlay.Bytes.ToArray();bad[(int)(sourceWord-UniversalOverlay.Base)]^=1;Refuse(()=>UniversalTwilightTrooperParticles.Capture(source.Data,new UniversalOverlay(bad,source.Overlay.Bindings),host.Overlay,lib.Disc.Exe,true),"source state/producer/constructor/effect source owner mutation");}
foreach(var binding in source.Overlay.Bindings)
{var bad=source.Overlay.Bindings.ToDictionary();bad[binding.Key]=binding.Value with{Value=binding.Value.Value+4};Refuse(()=>UniversalTwilightTrooperPlacement.RequireSource(source.Data,new UniversalOverlay(source.Overlay.Bytes.ToArray(),bad)),"source callback binding substitution");}
foreach(uint pc in new[]{0x8003A720u,0x800524C4u,0x800526A8u,0x80038C4Cu,0x80039E94u,0x80053570u,0x8006272Cu,0x800169ACu,0x800573C8u,0x8006CBF8u+636})
{var exe=lib.Disc.Exe.ToArray();exe[(int)(pc-UniversalExeBindings.ExeBase)]^=1;if(pc is 0x80053570 or 0x8006272C or 0x800169AC or 0x800573C8 ||pc==0x8006CBF8+636)Refuse(()=>UniversalTwilightTrooperParticles.Capture(source.Data,source.Overlay,host.Overlay,exe,true),"native particle owner mutation");if(pc is 0x8003A720 or 0x800524C4 or 0x800526A8 or 0x80038C4C or 0x80039E94 ||pc==0x8006CBF8+636)Refuse(()=>UniversalTwilightTrooperPlacement.RequireNative(exe),"native dynamic/default/path/rectangle owner mutation");}
foreach(uint pc in new[]{0x800844A0u,0x80083274u,0x8007ACB8u+13*4,0x8007AB74u+13*4})
{var bad=host.Overlay.Bytes.ToArray();bad[(int)(pc-UniversalOverlay.Base)]^=1;Refuse(()=>UniversalTwilightTrooperParticles.Capture(source.Data,source.Overlay,new UniversalOverlay(bad,host.Overlay.Bindings),lib.Disc.Exe,true),"native host particle body/table mutation");}
var particleParts=UniversalOwnedParticleDispatch.Build(particleCertificate.OwnedDispatcher!.Profile,"twilight13");
foreach(var pair in new[]{(particleParts.Spawn,UniversalTwilightTrooperParticles.Spawn13,particleParts.SpawnLabel),(particleParts.Update,UniversalTwilightTrooperParticles.Update13,particleParts.UpdateLabel)})
{
 for(int n=0;n<pair.Item1.Items.Count;n++)if(pair.Item1.Items[n].Word is uint word)
 {var bad=UniversalOwnedParticleDispatch.Build(particleCertificate.OwnedDispatcher.Profile,"twilight13");var part=pair.Item2==UniversalTwilightTrooperParticles.Spawn13?bad.Spawn:bad.Update;part.Items[n]=part.Items[n] with{Word=word^1};Refuse(()=>UniversalOwnedParticleDispatch.RequireExact(part,source.Overlay,pair.Item2,pair.Item3),"every copied effect word and table/ABI schema");}
 foreach(var key in pair.Item1.Tables.Keys){var bad=UniversalOwnedParticleDispatch.Build(particleCertificate.OwnedDispatcher.Profile,"twilight13");var part=pair.Item2==UniversalTwilightTrooperParticles.Spawn13?bad.Spawn:bad.Update;part.Tables[key]=[0x80080610];Refuse(()=>UniversalOwnedParticleDispatch.RequireExact(part,source.Overlay,pair.Item2,pair.Item3),"new copied particle table target");}
}
Refuse(()=>UniversalTwilightTrooperParticles.RequireBundle(source.Overlay,host.Overlay,allClasses,null,lib.Disc.Exe),"unissued impact13 refused even when child model exists");
foreach(uint pc in new[]{0x800805F8u,0x80080608u,0x80080610u,0x80080620u,0x80080624u})
{var bad=Part(alias);int at=bad.Items.FindIndex(i=>i.Orig==pc);bad.Items[at]=bad.Items[at] with{Word=bad.Items[at].Word^1};Refuse(()=>UniversalTwilightTrooperParticles.PatchImpact(bad,source.Overlay,"twilight13_spawn"),"copied impact vector/count/type/callback/live velocity mutation");}
foreach(uint target in new[]{0x80080608u,0x80080610u,0x80080620u})
{var bad=Part(alias);bad.Items.Insert(1,new(UniversalMips.EncodeJ(2,target),new(UniversalFixKind.Jump,target),0x80070000,null));Refuse(()=>UniversalTwilightTrooperOwnership.RequireCopiedInputs(bad,source.Overlay,allClasses,alias,lib.Disc.Exe),"incoming impact middle entry");}
var particleCode=UniversalBehaviorBundleBuilder.Assemble([particleParts.Spawn,particleParts.Update],0x800D0000);File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[2])!,"particle-emission.json"),JsonSerializer.Serialize(new{baseAddress=0x800D0000,code=Convert.ToHexString(particleCode.Code),labels=particleCode.Labels},new JsonSerializerOptions{WriteIndented=true}));
foreach(var row in Enumerable.Range(0,source.Data.Scene().RowCount).Select(r=>source.Data.CaptureRow("twilightharbor",r)).Where(UniversalTwilightTrooperPlacement.Applies))
{
 var moved=row.Row.ToArray();moved[0x43]=255;moved[0x4A]=255;
 var props=row.Properties.ToArray();props[8]^=1;Refuse(()=>UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,row with{Properties=props},moved),"changed source native selector/latch");
 Refuse(()=>UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,row with{SourceRowIndices=[new(0x38,1,0xB0)]},moved),"no source integer link can be guessed from dynamic B0 pointer");
 Refuse(()=>UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,row with{InternalPointers=[]},moved),"omitted owned PathData fixup");
 foreach(int field in new[]{0x36,0x42,0x47,0x48,0x49,0x50}){var bad=moved.ToArray();bad[field]^=1;Refuse(()=>UniversalTwilightTrooperPlacement.RequireInitialRow(row,bad),"changed authored initial actor/class/state/frame/collision input");}
}
foreach(string kind in new[]{"label","sourcepc","orig","lenient","duplicate"})
{var bad=Part(alias);int at=bad.Items.FindIndex(i=>i.Orig==0x8007FA40);var item=bad.Items[at];if(kind=="label")bad.Items.Insert(at,new(null,default,null,"new_inside_entry"));if(kind=="sourcepc")bad.Items[at]=item with{SourcePc=0x8007FA44};if(kind=="orig")bad.Items[at]=item with{Orig=0x8007FA44};if(kind=="lenient")bad.LenientTables=!bad.LenientTables;if(kind=="duplicate")bad.Items.Insert(at,item);Refuse(()=>UniversalTwilightTrooperOwnership.RequireCopiedInputs(bad,source.Overlay,allClasses,alias,lib.Disc.Exe),"copied incoming identity/label/table policy/duplicate schema");}
var duplicatePlacements=new[]{new UniversalMobyPlacement("twilightharbor",6,14500,63750,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:0),new UniversalMobyPlacement("twilightharbor",12,15000,63250,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:1),new UniversalMobyPlacement("twilightharbor",6,16000,62750,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:2),new UniversalMobyPlacement("twilightharbor",12,16500,62250,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:3)};
var duplicated=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),duplicatePlacements,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);
var duplicateData=new UniversalLevelData(duplicated.LevelData);var duplicateRows=duplicated.NewRowIndices.Select(duplicateData.Row).ToArray();Check(duplicateRows.Select(r=>BinaryPrimitives.ReadInt32LittleEndian(r)).Distinct().Count()==4,"two roots of each class retain separate mutable native state/child properties");Check(duplicated.ParticleDependencies.All(d=>d.Status.StartsWith("source-certified")),"combined duplicated93/94 closure has one issued13 updater");
for(int n=0;n<duplicateRows.Length;n++){var t=duplicateData.CaptureRow("result",duplicated.NewRowIndices[n]);Check(BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(12))==duplicatePlacements[n].X,"normal editor coordinates survive duplicate composed rows");if(duplicatePlacements[n].SourceRowIndex==12)Check(BinaryPrimitives.ReadUInt32LittleEndian(t.Properties.AsSpan(0x38))==0,"separate runtime B0 child pointers startempty and never bindsource/destination placed index");}
var duplicateDirectory=Path.Combine(Path.GetDirectoryName(args[2])!,"composed","duplicate-9394");Directory.CreateDirectory(duplicateDirectory);File.WriteAllBytes(Path.Combine(duplicateDirectory,"level.data"),duplicated.LevelData);File.WriteAllBytes(Path.Combine(duplicateDirectory,"overlay.bin"),duplicated.Overlay);File.WriteAllBytes(Path.Combine(duplicateDirectory,"staged.exe"),duplicated.Executable);
var foreign=new UniversalOwnedParticleCertificate(particleCertificate.OwnedDispatcher!.Profile with{SourceKey="foreign"},"darkhollow",0x80083274,(_,_,_)=>{},_=>{});
Refuse(()=>UniversalTwilightTrooperParticles.RequireBundle(source.Overlay,host.Overlay,allClasses,[particleCertificate.OwnedDispatcher,foreign],lib.Disc.Exe),"independent foreign13 updater is refused regardless of source ordering");
Refuse(()=>UniversalTwilightTrooperParticles.RequireBundle(source.Overlay,host.Overlay,allClasses,[foreign,particleCertificate.OwnedDispatcher],lib.Disc.Exe),"independent foreign13 reversed ordering is refused");
var dark=lib.Get("darkpassage");var darkParticleCertificate=UniversalDarkPassageProjectileParticles.Capture("darkpassage","darkhollow",dark.Data,dark.Overlay,host.Overlay,lib.Disc.Exe);
var actualOwners=new[]{particleCertificate.OwnedDispatcher!,darkParticleCertificate.OwnedDispatcher!};
foreach(var owners in new[]{actualOwners,actualOwners.Reverse().ToArray()})
{
 Refuse(()=>UniversalTwilightTrooperParticles.RequireBundle(source.Overlay,host.Overlay,allClasses,owners,lib.Disc.Exe),"two real issued source13 owners refused by Twilight in either order");
 Refuse(()=>UniversalDarkPassageProjectileParticles.RequireBundle("darkpassage",dark.Overlay,[0x9B],host.Overlay,lib.Disc.Exe,new Dictionary<string,UniversalDarkPassageProjectileParticleCertificate>{{"darkpassage",darkParticleCertificate}},owners),"two real issued source13 owners refused by Dark in either order");
}
var darkRootRow=Enumerable.Range(0,dark.Data.Scene().RowCount).First(r=>BinaryPrimitives.ReadUInt16LittleEndian(dark.Data.Row(r).AsSpan(0x36))==0x35);
var darkGroup=UniversalPlacementGroups.Expand(lib.Get,new UniversalMobyPlacement("darkpassage",darkRootRow,15000,63250,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:1));
var jointPlacements=new[]{new UniversalMobyPlacement("twilightharbor",12,14500,63750,17740,EditorId:Guid.NewGuid().ToString(),PersistentSlot:0)}.Concat(darkGroup).ToArray();
foreach(var placements in new[]{jointPlacements,jointPlacements.Reverse().ToArray()})
{
 try {UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),lib.Disc.Exe,lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),placements,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations);throw new Exception("Expected actual joint source updater refusal");}
 catch(InvalidDataException e){Check(e.Message.Contains("independently update the same particle13 lifetime"),"actual joint original Dark+Twilight composition reaches precise owner overlap refusal in both orders: "+e.Message);}
}
var sourceRoot=source.Data.CaptureRow("twilightharbor",6);Refuse(()=>UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,sourceRoot with{SourceLevelKey="foreign"},sourceRoot.Row),"known native source cannot lose its property owner by relabeling");var canonical=UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,sourceRoot,sourceRoot.Row);var capital=UniversalTwilightTrooperPlacement.Rebase(source.Data,source.Overlay,sourceRoot with{SourceLevelKey="TwilightHarbor"},sourceRoot.Row);Check(canonical.Properties.SequenceEqual(capital.Properties),"normal case-insensitive source spelling keeps the same property certificate");
var identityInit=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source.Overlay,allClasses,"identity_init").Part;foreach(int cls in allClasses){UniversalTwilightTrooperOwnership.RequireInitializer(identityInit,source.Overlay,cls,lib.Disc.Exe,allClasses);checks++;}
var native=Part();var mapped=Part(alias);UniversalTwilightTrooperOwnership.TranslateLinkedRead(mapped,source.Overlay,allClasses,alias);UniversalClassRemapping.PatchConstantSpawns(mapped,source.Overlay,alias);
object Emission(UniversalAsmPart p,uint address){var(code,labels)=UniversalBehaviorBundleBuilder.Assemble([p],address);uint cursor=address;var locations=new Dictionary<uint,uint>();foreach(var item in p.Items){if(item.Word==null)continue;if(item.Orig is uint orig)locations[orig]=cursor;cursor+=4;}return new{baseAddress=address,code=Convert.ToHexString(code),labels,locations};}
File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[2])!,"owned-emissions.json"),JsonSerializer.Serialize(new{native=Emission(native,0x80090000),mapped=Emission(mapped,0x800A0000),alias},new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Checks=checks,results,NativeRuntime=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"UNIVERSAL_TWILIGHT_TROOPER_OK checks={checks}");
