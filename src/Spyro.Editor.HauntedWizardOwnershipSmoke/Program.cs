using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length!=3)throw new Exception("USA BIN, catalog root, NEW report path");
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var source=lib.Get("hauntedtowers");int checks=0;
void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;}
void Refuse(Action action,string text){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("admitted "+text);}
var rows=Enumerable.Range(0,source.Data.Scene().RowCount).Select(i=>source.Data.CaptureRow("hauntedtowers",i)).Where(t=>t.ClassId is 0xCB or 0xCC).ToArray();Check(rows.Length==31,"31 variants");
var peace=lib.Get("peacekeepers");
var town=lib.Get("townsquare");var townParticle=UniversalHauntedWizardParticles.Capture(source.Data,source.Overlay,town.Overlay,lib.Disc.Exe);Check(townParticle.NativeUpdate==0x80087E20,"neutral cleared Town manager owner");
var particle=UniversalHauntedWizardParticles.Capture(source.Data,source.Overlay,peace.Overlay,lib.Disc.Exe);var pp=UniversalOwnedParticleDispatch.Build(particle.Profile,"haunted29");
Check(pp.Spawn.Items.Count(i=>i.Word!=null)*4==484 && pp.Update.Items.Count(i=>i.Word!=null)*4==1248,"exact issued callback footprints");
foreach(int physical in new[]{0,255}){UniversalHauntedWizardParticles.RequirePhysicalWizardRow(physical);checks++;}
foreach(int physical in new[]{-1,256})Refuse(()=>UniversalHauntedWizardParticles.RequirePhysicalWizardRow(physical),"physical row byte");
foreach(uint pc in new[]{0x800169ACu,0x80053570u,0x800573C8u,0x8006272Cu,0x800131BCu,0x8006CBF8u}){var exe=lib.Disc.Exe.ToArray();exe[pc-UniversalExeBindings.ExeBase]^=1;Refuse(()=>UniversalHauntedWizardParticles.RequireNative(exe),"native particle callee");}
foreach(uint pc in new[]{0x800881D8u,0x80086754u,0x8007B388u+29*4,0x8007B244u+29*4}){var b=source.Overlay.Bytes.ToArray();b[pc-UniversalOverlay.Base]^=1;Refuse(()=>UniversalHauntedWizardParticles.Capture(source.Data,new UniversalOverlay(b,source.Overlay.Bindings),peace.Overlay,lib.Disc.Exe),"source particle callback/table");}
foreach(var pair in new[]{(Part:pp.Spawn,Owner:particle.Profile.Spawn,Tag:"haunted29_spawn"),(Part:pp.Update,Owner:particle.Profile.Update,Tag:"haunted29_update")}){
 var items=pair.Part.Items.ToArray();int a=Array.FindIndex(items,x=>x.Word!=null);
 foreach(string mut in new[]{"word","reorder","insert","duplicate","table"}){
  pair.Part.Items.Clear();pair.Part.Items.AddRange(items);var tables=pair.Part.Tables.ToDictionary(x=>x.Key,x=>x.Value);
  if(mut=="word")pair.Part.Items[a]=pair.Part.Items[a] with{Word=pair.Part.Items[a].Word^1};
  if(mut=="reorder")(pair.Part.Items[a],pair.Part.Items[a+1])=(pair.Part.Items[a+1],pair.Part.Items[a]);
  if(mut=="insert")pair.Part.Items.Insert(a+1,new(0,default,null,null));
  if(mut=="duplicate")pair.Part.Items.Add(items[a]);
  if(mut=="table"){string k=pair.Part.Tables.Keys.First();var v=pair.Part.Tables[k].ToArray();v[29]+=4;pair.Part.Tables[k]=v;}
  Refuse(()=>UniversalOwnedParticleDispatch.RequireExact(pair.Part,particle.Profile.Source,pair.Owner,pair.Tag),"emitted particle "+mut);
  pair.Part.Tables.Clear();foreach(var t in tables)pair.Part.Tables.Add(t.Key,t.Value);
 }
 pair.Part.Items.Clear();pair.Part.Items.AddRange(items);
 Check(pair.Part.Tables.Values.First().Where((v,t)=>t!=29).All(v=>v==pair.Owner.Default),"unowned particle entries original no-op");
}
var chain=UniversalOwnedParticleDispatch.ChainUpdates([pp.UpdateLabel],particle.NativeUpdate,"particle_chain");
var assembled=UniversalBehaviorBundleBuilder.Assemble([pp.Spawn,pp.Update,chain],0x80090000);
var cw=chain.Items.Where(x=>x.Word!=null).Select(x=>x.Word!.Value).ToArray();Check(cw.Count(w=>UniversalMips.Op(w)==2)==1 && cw.Last(w=>UniversalMips.Op(w)==2)==UniversalMips.EncodeJ(2,particle.NativeUpdate),"one native update tail");
Check(cw[1]==UniversalMips.EncodeI(43,29,31,0x18)&&cw[2]==UniversalMips.EncodeI(43,29,4,0x14)&&cw[^5]==UniversalMips.EncodeI(35,29,31,0x18)&&cw[^4]==UniversalMips.EncodeI(35,29,4,0x14),"wrapper preserves return and native delta");
var constructor=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,[0xCC],"wizard").Part;var originalConstructor=constructor.Items.ToArray();int ctor=constructor.Items.FindIndex(x=>x.Orig==0x80081FDC);
foreach(string mut in new[]{"reorder","insert","target","entry","delay","duplicate"}){
 constructor.Items.Clear();constructor.Items.AddRange(originalConstructor);
 if(mut=="reorder")(constructor.Items[ctor+1],constructor.Items[ctor+2])=(constructor.Items[ctor+2],constructor.Items[ctor+1]);
 if(mut=="insert")constructor.Items.Insert(ctor+1,new(0,default,null,null));
 if(mut=="target")constructor.Items[ctor+1]=constructor.Items[ctor+1] with{Fix=new(UniversalFixKind.Jump,0x80081FFC)};
 if(mut=="entry")constructor.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,0x80081FFC),null,null));
 if(mut=="delay"){int at=constructor.Items.FindIndex(x=>x.Orig==0x80082000);constructor.Items[at]=constructor.Items[at] with{Word=0};}
 if(mut=="duplicate")constructor.Items.Add(originalConstructor[ctor+1]);
 Refuse(()=>UniversalHauntedWizardParticles.PatchConstructor(constructor,source.Overlay,pp.SpawnLabel),"constructor "+mut);
}
constructor.Items.Clear();constructor.Items.AddRange(originalConstructor);UniversalHauntedWizardParticles.PatchConstructor(constructor,source.Overlay,pp.SpawnLabel);Check(constructor.Items.Single(x=>x.Orig==0x80081FFC).Fix.Label==pp.SpawnLabel,"owned constructor route");Check(constructor.Items.Single(x=>x.Orig==0x80082000).Word==source.Overlay.Word(0x80082000),"live physical wizard a3 delay preserved");
Check(UniversalHauntedWizardParticles.HasNativeOwner(source.Overlay)&&!UniversalHauntedWizardParticles.HasNativeOwner(town.Overlay),"same-level native resurrection owner");
var nativeControl=UniversalBehaviorBundleBuilder.Build(source.Overlay,[("hauntedtowers",source.Overlay,(IReadOnlyList<int>)new[]{0xCC})],0x80090000,nativeExecutable:lib.Disc.Exe);Check(nativeControl.OwnedParticleUpdateWrapper==null,"native same-level manager retained");
Refuse(()=>UniversalBehaviorBundleBuilder.Build(town.Overlay,[("hauntedtowers",source.Overlay,(IReadOnlyList<int>)new[]{0xCC})],0x80090000,nativeExecutable:lib.Disc.Exe),"raw copied wizard missing particle certificate");
Refuse(()=>UniversalHauntedWizardParticles.RequireDestinationTexture(source.Data,peace.Data),"unimported particle29 tile");
foreach(uint pc in new[]{0x80037F90u,0x80038C4Cu,0x80039E94u}){var exe=lib.Disc.Exe.ToArray();exe[pc-UniversalExeBindings.ExeBase]^=1;Refuse(()=>UniversalHauntedWizardPlacement.RequireNative(exe),"native placement callee");}
foreach(uint pc in new[]{0x80087E20u,0x8007AC80u+29*4,0x80088F20u}){var b=town.Overlay.Bytes.ToArray();b[pc-UniversalOverlay.Base]^=1;Refuse(()=>UniversalHauntedWizardParticles.Capture(source.Data,source.Overlay,new UniversalOverlay(b,town.Overlay.Bindings),lib.Disc.Exe),"cleared manager mutation");}
var map=new Dictionary<string,IReadOnlyDictionary<int,int>>{{"hauntedtowers",new Dictionary<int,int>{{0xCB,0x1FC},{0xCC,0x1FD},{0xD7,0x1FA},{0x26,0x1FB}}}};
var mappedControl=UniversalBehaviorBundleBuilder.Build(town.Overlay,[("hauntedtowers",source.Overlay,(IReadOnlyList<int>)new[]{0xCB,0xCC,0xD7,0x26})],0x80090000,classMaps:map,nativeExecutable:lib.Disc.Exe,ownedParticles:[townParticle]);Check(mappedControl.OwnedParticleUpdateWrapper!=null,"mapped CB/CC owned particle dispatcher");
foreach(uint start in new[]{0x8008590Cu,0x80085C88u,0x80085CFCu}){
 var ctorPart=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source.Overlay,[0x26],"projectile").Part;var items=ctorPart.Items.ToArray();int first=ctorPart.Items.FindIndex(i=>i.Orig==start);
 foreach(string mut in new[]{"word","reorder","insert","entry","duplicate"}){
  ctorPart.Items.Clear();ctorPart.Items.AddRange(items);
  if(mut=="word")ctorPart.Items[first]=ctorPart.Items[first] with{Word=ctorPart.Items[first].Word^1};
  if(mut=="reorder")(ctorPart.Items[first+1],ctorPart.Items[first+2])=(ctorPart.Items[first+2],ctorPart.Items[first+1]);
  if(mut=="insert")ctorPart.Items.Insert(first+1,new(0,default,null,null));
  if(mut=="entry")ctorPart.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,start+4),null,null));
  if(mut=="duplicate")ctorPart.Items.Add(items[first+1]);
  Refuse(()=>UniversalHauntedWizardClassOwnership.PatchProjectileParent(ctorPart,source.Overlay,map["hauntedtowers"],0x26),"projectile parent "+mut);
 }
}
foreach(uint pc in new[]{0x80081354u,0x800813A4u,0x80081414u,0x800843ACu,0x800817C4u,0x80083D8Cu}){
 var armor=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,[0xCB],"armor_models").Part;int at=armor.Items.FindIndex(i=>i.Orig==pc);armor.Items[at]=armor.Items[at] with{Word=armor.Items[at].Word^1};
 Refuse(()=>UniversalHauntedWizardClassOwnership.MarkArmorDebrisModelRead(armor,source.Overlay,[0xCB]),"armor model owner "+pc);
}
List<object> reports=[];
foreach(var raw in rows)
{
 var t=UniversalHauntedWizardPlacement.Capture(source.Data,source.Overlay,raw);
 var expected=t.ClassId==0xCC?Enumerable.Range(0,7).Select(i=>0x20+i*4):new[]{0x18};
 Check(t.SourceRowIndices.Count==expected.Count(i=>BitConverter.ToInt32(t.Properties,i)>=0),"all actual signed links");
 foreach(var delta in new[]{new[]{0,0,0},new[]{1700,-1900,320},new[]{-900,1500,-64}})
 {
  var row=t.Row.ToArray();for(int a=0;a<3;a++)BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12+a*4),checked(BitConverter.ToInt32(row,12+a*4)+delta[a]));row[0x43]=row[0x4A]=255;
  UniversalHauntedWizardPlacement.RequireInitialRow(t,row);checks++;
  var translated=UniversalHauntedWizardPlacement.Rebase(source.Data,source.Overlay,t,row);
  var vectors=t.InternalPointers.SelectMany(p=>Enumerable.Range(0,t.Properties[p.Target]).Select(n=>p.Target+8+n*16)).ToList();
  if(t.ClassId==0xCC)vectors.AddRange([0x40,0x58,0x70,0x88,0xA0,0xB8,0xD0,0x110,0x134,0x14C]);
  var coordinateBytes=new HashSet<int>();foreach(int v in vectors)for(int a=0;a<3;a++){int at=v+4*a;Check(BitConverter.ToInt32(translated.Properties,at)==checked(BitConverter.ToInt32(t.Properties,at)+delta[a]),"XYZ ownership");for(int b=0;b<4;b++)coordinateBytes.Add(at+b);}
  for(int i=0;i<t.Properties.Length;i++)if(!coordinateBytes.Contains(i))Check(translated.Properties[i]==t.Properties[i],"noncoordinate byte preserved");
 }
 foreach(int at in new[]{0x36,0x3D,0x3F,0x48,0x49,0x4B,0x4F}){var r=t.Row.ToArray();r[0x43]=r[0x4A]=255;r[at]^=1;Refuse(()=>UniversalHauntedWizardPlacement.RequireInitialRow(t,r),"row input "+at);}
 var mutation=t with{Properties=t.Properties.ToArray()};mutation.Properties[0]^=1;Refuse(()=>UniversalHauntedWizardPlacement.Capture(source.Data,source.Overlay,mutation),"property owner");
 var overlay=new UniversalOverlay(source.Overlay.Bytes.ToArray(),source.Overlay.Bindings.ToDictionary(b=>b.Key,b=>b.Key==UniversalExeBindings.UpdateGlobal?b.Value with{Value=b.Value.Value+4}:b.Value));Refuse(()=>UniversalHauntedWizardPlacement.Capture(source.Data,overlay,t),"callback identity");
 if(t.ClassId==0xCC){var root=new UniversalMobyPlacement("hauntedtowers",t.SourceRowIndex,100000,100000,14000);var expanded=UniversalPlacementGroups.Expand(lib.Get,root);Check(expanded.Count==t.SourceRowIndices.Count+1,"group closure");var templates=expanded.Select(p=>UniversalIndexedRows.Capture(source.Data,source.Overlay,source.Data.CaptureRow(p.SourceLevelKey,p.SourceRowIndex)) with{LinkGroupId=p.LinkGroupId}).ToArray();var resolved=UniversalIndexedRows.Resolve(templates[0],templates);Check(resolved.ImportedRowIndices.Count==t.SourceRowIndices.Count,"exact resolved targets");Refuse(()=>UniversalIndexedRows.Resolve(templates[0],templates.Take(1).ToArray()),"missing armor");var wrong=templates.ToArray();wrong[1]=wrong[1] with{Row=wrong[1].Row.ToArray()};wrong[1].Row[0x36]^=1;Refuse(()=>UniversalIndexedRows.Resolve(templates[0],wrong),"wrong class");Refuse(()=>UniversalHauntedWizardPlacement.RequireNativeRemoval("hauntedtowers",source.Data,source.Data,source.Overlay,new HashSet<int>{t.SourceRowIndices[0].SourceRow}),"surviving wizard removed armor");}
 reports.Add(new{t.SourceRowIndex,t.ClassId,Links=t.SourceRowIndices,PathNodes=t.InternalPointers.Select(p=>t.Properties[p.Target]).ToArray()});
}
foreach(int c in new[]{0xCB,0xCC}){
 var part=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,[c],"haunted").Part;UniversalHauntedWizardPlacement.RequireCopiedPlacementInputs(part,source.Overlay,[c]);checks++;
 int start=part.Items.FindIndex(x=>x.Orig==(c==0xCB?0x80081428u:0x80081D94u));var keep=part.Items.ToArray();
 foreach(string mut in new[]{"word","target","reorder","insert","entry","duplicate"}){
  part.Items.Clear();part.Items.AddRange(keep);
  if(mut=="word")part.Items[start+1]=part.Items[start+1] with{Word=part.Items[start+1].Word^1};
  if(mut=="target"){int j=part.Items.FindIndex(start,x=>x.Fix.Kind==UniversalFixKind.Branch);part.Items[j]=part.Items[j] with{Fix=part.Items[j].Fix with{Orig=part.Items[j].Fix.Orig+4}};}
  if(mut=="reorder")(part.Items[start+1],part.Items[start+2])=(part.Items[start+2],part.Items[start+1]);
  if(mut=="insert")part.Items.Insert(start+1,new(0,default,null,null));
  if(mut=="entry")part.Items.Add(new(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.Jump,keep[start+1].Orig!.Value),null,null));
  if(mut=="duplicate")part.Items.Add(keep[start+1]);
  Refuse(()=>UniversalHauntedWizardPlacement.RequireCopiedPlacementInputs(part,source.Overlay,[c]),"emitted "+mut);
 }
}
foreach(int owner in new[]{21,22,24,28,29,52,32}){
 var original=UniversalHauntedWizardPlacement.Capture(source.Data,source.Overlay,source.Data.CaptureRow("hauntedtowers",owner));int victim=original.SourceRowIndices.First().SourceRow;
 UniversalHauntedWizardPlacement.RequireNativeRemoval("hauntedtowers",source.Data,source.Data,source.Overlay,new HashSet<int>{owner,victim});checks++;
 foreach(string mut in new[]{"class","retarget"}){
  var edited=new UniversalLevelData(source.Data.Bytes.ToArray());int sf=edited.Subfiles()[3].Offset,ro=sf+edited.Scene().RowsOffset+owner*UniversalLevelData.RowStride;
  if(mut=="class")edited.Bytes[ro+0x36]^=1;else{int prop=sf+BitConverter.ToInt32(edited.Row(owner),0);BinaryPrimitives.WriteInt32LittleEndian(edited.Bytes.AsSpan(prop+original.SourceRowIndices[0].Offset),-1);}
  Refuse(()=>UniversalHauntedWizardPlacement.RequireNativeRemoval("hauntedtowers",edited,source.Data,source.Overlay,new HashSet<int>{victim}),"edited native owner "+mut);
 }
}
File.WriteAllText(args[2],JsonSerializer.Serialize(new{Scope="Haunted source placement/index/path inputs only; particle29 source/emitted guards; integration and runtime pending",Checks=checks,Variants=reports,CoreSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)))},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"HAUNTED_PLACEMENT_OWNERSHIP_GUARDS_OK checks={checks} variants={rows.Length}");
