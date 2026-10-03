using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
int checks=0;
void Check(bool ok,string what) { checks++;if(!ok)throw new Exception(what); }
void Refuse(Action action,string what) { checks++;try{action();}catch(InvalidDataException){return;}throw new Exception("Accepted: "+what); }
if(args.Length!=3)throw new ArgumentException("Original USA BIN, catalog root, NEW report path required.");
var report=Path.GetFullPath(args[2]);var directory=report+".artifacts";
if(File.Exists(report)||Directory.Exists(directory))throw new InvalidDataException("Preserve existing proof output.");Directory.CreateDirectory(directory);
var library=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));
var exe=library.Disc.Exe.ToArray();var sourceOwner=library.Get("magiccrafters");
var sourceData=sourceOwner.Data.Bytes.ToArray();var overlayBytes=sourceOwner.Overlay.Bytes.ToArray();
var source=new UniversalOverlay(overlayBytes,UniversalExeBindings.Read(exe,30));var data=new UniversalLevelData(sourceData);
UniversalMagicChainPathFollowing.RequireSource(data,source,exe);Check(true,"native whole source owner");
foreach(uint pc in new uint[]{0x80039E94,0x80039F9C,0x8003A068,0x8003A15C})
{
 var bad=exe.ToArray();bad[pc-UniversalExeBindings.ExeBase]^=1;
 Refuse(()=>UniversalMagicChainPathFollowing.RequireSource(data,source,bad),"native path producer mutation");
}
for(uint pc=0x800875BC;pc<=0x80087618;pc+=4)
{
 var bad=overlayBytes.ToArray();bad[pc-UniversalOverlay.Base]^=1;
 Refuse(()=>UniversalMagicChainPathFollowing.RequireSource(data,new(bad,source.Bindings),exe),"source argument/control/flag/selector mutation");
}
var original=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[271],"magic_chain_update").Part;
for(uint pc=0x800875BC;pc<=0x80087618;pc+=4)
{
 var bad=new UniversalAsmPart();bad.Items.AddRange(original.Items);foreach(var t in original.Tables)bad.Tables[t.Key]=t.Value;
 int at=bad.Items.FindIndex(i=>i.Orig==pc);Check(at>=0,"source packet retained");bad.Items[at]=bad.Items[at] with{Word=bad.Items[at].Word^1};
 Refuse(()=>UniversalMagicChainPathFollowing.BindSource(bad,source),"copied source argument/control/flag/selector mutation");
}
UniversalMagicChainPathFollowing.BindSource(original,source);
// These objects remain research-only: this unfiltered behavior still owns its13B
// arms. It proves the new path prerequisite; it is never a Compose pruning issuer.
var boundary=UniversalMagicPairMovementBoundary.Build();var path=UniversalMagicChainPathFollowing.Build();
foreach(uint baseAddress in new uint[]{0x80090000,0x800A1400})
{
 var (code,labels)=UniversalBehaviorBundleBuilder.Assemble([original,boundary,path],baseAddress);
 UniversalMagicChainPathFollowing.RequireWritten(code,labels,baseAddress);Check(true,"independent full native path relocation/readback");
 uint first=labels[UniversalMagicChainPathFollowing.WrapperLabel],last=labels[UniversalMagicChainPathFollowing.ShadowLabel]+1024;
 for(uint pc=first;pc<last;pc+=4)
 {
  var bad=code.ToArray();bad[pc-baseAddress]^=1;
  Refuse(()=>UniversalMagicChainPathFollowing.RequireWritten(bad,labels,baseAddress),"copied wrapper/native/path/shadow mutation");
 }
 foreach(var name in labels.Keys.Where(n=>n.StartsWith("magic_chain_path_")||n==UniversalMagicChainPathFollowing.EntryLabel||n==UniversalMagicChainPathFollowing.ShadowLabel))
 {
  var badLabels=labels.ToDictionary();badLabels[name]+=4;
  Refuse(()=>UniversalMagicChainPathFollowing.RequireWritten(code,badLabels,baseAddress),"copied label namespace mutation");
 }
 foreach(uint destination in new uint[]{first+4,labels[UniversalMagicChainPathFollowing.EntryLabel]+4,labels[UniversalMagicChainPathFollowing.ShadowLabel]})
 {
  var bad=code.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad,UniversalMips.EncodeJ(2,destination));
  Refuse(()=>UniversalMagicChainPathFollowing.RequireWritten(bad,labels,baseAddress),"outside jump into argument/native/data interior");
  bad=code.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad,destination);
  Refuse(()=>UniversalMagicChainPathFollowing.RequireWritten(bad,labels,baseAddress),"outside raw/table address into native/data interior");
  bad=code.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad,UniversalMips.EncodeI(15,0,8,UniversalMips.Hi(destination)));
  BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(4),UniversalMips.EncodeI(9,8,8,UniversalMips.Lo(destination)));
  Refuse(()=>UniversalMagicChainPathFollowing.RequireWritten(bad,labels,baseAddress),"outside materialized address into native/data interior");
 }
 var proof=new {Scope="Private path/collision prerequisite only; no selector pruning or ordinary Compose admission",Base=baseAddress,CodeHex=Convert.ToHexString(code),Labels=labels,NativeEntry=UniversalMagicChainPathFollowing.Entry,SourceCall=UniversalMagicChainPathFollowing.SourceCall};
 File.WriteAllText(Path.Combine(directory,$"path-emission-{baseAddress:X8}.json"),JsonSerializer.Serialize(proof,new JsonSerializerOptions{WriteIndented=true})+"\n");
}

if(args.Length>=3)
{
 var host=library.Get("darkhollow");
 foreach(var order in new[]{new[]{2,3,4,5},new[]{5,4,3,2},new[]{3,5,2,4,4,2,5,3}})
 {
  var tuples=new List<(byte[] Row,UniversalRowTemplate Template)>();var rawTemplates=new List<UniversalRowTemplate>();
  for(int i=0;i<order.Length;i++)
  {
   int sourceRow=order[i];var raw=data.CaptureRow("magiccrafters",sourceRow);var template=UniversalMagicDruidChainResources.CaptureSource(data,source,raw);
   Check(template.SourcePrerequisiteRows.Count==3,"whole exact source pod prerequisite enrollment");
   int occurrence=order.Take(i).Count(row=>row==sourceRow);string group="group"+occurrence;
   var placement=new UniversalMobyPlacement("magiccrafters",sourceRow,BitConverter.ToInt32(raw.Row,12)-50000+occurrence*5000,BitConverter.ToInt32(raw.Row,16)-30000,BitConverter.ToInt32(raw.Row,20)-10000,EditorId:Guid.NewGuid().ToString(),PersistentSlot:i){LinkGroupId=group};
   template=UniversalMagicDruidChainResources.Rebase(data,source,raw,placement);
   byte[] row=template.Row.ToArray();BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(12),placement.X);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(16),placement.Y);BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(20),placement.Z);row[67]=255;row[74]=255;
   rawTemplates.Add(template);tuples.Add((row,template));
  }
  for(int i=0;i<tuples.Count;i++)tuples[i]=(tuples[i].Row,UniversalIndexedRows.Resolve(tuples[i].Template,rawTemplates));
  var destination=new UniversalLevelData(host.Data.Bytes.ToArray());var (_,_,physical)=destination.AppendRows(tuples);
  UniversalMagicDruidChainResources.AppendOwnedPods(destination,tuples,physical);
  var reopened=new UniversalLevelData(destination.Bytes.ToArray());var sc=reopened.Scene();int so=reopened.Subfiles()[3].Offset;
  var flags=new List<int>();
  foreach(var group in rawTemplates.Select(t=>t.LinkGroupId).Distinct())
  {
   int leader=rawTemplates.FindIndex(t=>t.SourceRowIndex==2&&t.LinkGroupId==group);int flag=BitConverter.ToInt32(reopened.Row(physical[leader]))+76;flags.Add(flag);
   var members=Enumerable.Range(2,4).Select(row=>physical[rawTemplates.FindIndex(t=>t.SourceRowIndex==row&&t.LinkGroupId==group)]).ToArray();
   int pod=reopened.Row(members[0])[67],offset=BitConverter.ToInt32(reopened.Bytes,so+sc.PodComponent+8+4*pod);
   for(int i=0;i<4;i++)Check(BitConverter.ToUInt16(reopened.Bytes,so+sc.PodComponent+offset+2*i)==(members[i]|(i==3?0x8000:0)),"exact group-scoped physical member/high terminal bit");
   foreach(int row in members)
   {
    int properties=BitConverter.ToInt32(reopened.Row(row));Check(BitConverter.ToInt32(reopened.Bytes,so+properties+60)==flag&&sc.Fixups.Contains(properties+60),"one source-owned mutable flag per complete pod/group");
   }
  }
  Check(flags.Distinct().Count()==flags.Count,"duplicate source imports do not share mutable flags across editor groups");
  for(int i=0;i<tuples.Count;i++)
  {
   int properties=BitConverter.ToInt32(reopened.Row(physical[i]));var t=tuples[i].Template;int sourceRow=t.SourceRowIndex;
   int expected=sourceRow==5?-1:physical[rawTemplates.FindIndex(target=>target.SourceRowIndex==sourceRow+1&&target.LinkGroupId==t.LinkGroupId)];
   Check(BitConverter.ToInt32(reopened.Bytes,so+properties+36)==expected,"integer source identity resolves after final physical order/group allocation");
   Check(BitConverter.ToInt32(reopened.Bytes,so+properties+40)==-1,"unproved second negative value retained exactly");
   Check(BitConverter.ToInt32(reopened.Bytes,so+properties)==properties+80&&sc.Fixups.Contains(properties),"individual two-node path remains private/owned/fixed up");
   for(int at=4;at<60;at+=4)if(at!=36)Check(BitConverter.ToInt32(reopened.Bytes,so+properties+at)==BitConverter.ToInt32(t.Properties,at),"rebased home/source property values preserved");
   Check(reopened.Bytes.AsSpan(so+properties+80,40).SequenceEqual(t.Properties.AsSpan(80,40)),"native complete rebased path header/nodes preserved after reopen");
  }
  File.WriteAllBytes(Path.Combine(directory,$"chain-resources-{order.Length}-{order[0]}.data"),reopened.Bytes);
  File.WriteAllText(Path.Combine(directory,$"chain-resources-{order.Length}-{order[0]}.json"),JsonSerializer.Serialize(new{Physical=physical,SourceRows=order,Groups=rawTemplates.Select(t=>t.LinkGroupId),SceneOffset=so,sc.RowsOffset,sc.PodComponent,sc.PropertiesComponent,sc.Fixups,Scope="Serialized resource ownership only; no Compose/source-state issuer"},new JsonSerializerOptions{WriteIndented=true})+"\n");
 }

 var rootPlacement=new UniversalMobyPlacement("magiccrafters",2,18000,69000,17740,Yaw:data.Row(2)[70],EditorId:"chain-root",PersistentSlot:0);
 var authoredGroup=UniversalPlacementGroups.Expand(library.Get,rootPlacement);
 Check(authoredGroup.Count==4&&authoredGroup.Select(p=>p.SourceRowIndex).Order().SequenceEqual(new[]{2,3,4,5}),"ordinary Expand either root follows authenticated whole pod prerequisites");
 var aggregate=UniversalMagicDruidChainCertificate.Capture("darkhollow",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,source,authoredGroup,null);
 var composed=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,library.Get,library.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),authoredGroup,sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
 Check(composed.Behavior.Single().PrivateMagicDruidChain!=null,"ordinary Compose auto-captures structural chain certificate");
 var codeOwner=composed.Behavior.Single().PrivateMagicDruidChain!;
 aggregate.RequireCompiled(composed.Overlay.AsSpan((int)(codeOwner.Base-UniversalOverlay.Base),codeOwner.Bytes).ToArray(),codeOwner.Base);
 aggregate.RequireFinalRows(new(composed.LevelData),composed.NewRowIndices);aggregate.RequireFinalOverlay(composed.Overlay);aggregate.RequireFinalExecutable(composed.Executable);checks+=4;

 // Structural code guards bind every instruction/data word without a fixed final
 // image hash, and final row/resource/host inputs derive independently.
 var candidateCode=composed.Overlay.AsSpan((int)(codeOwner.Base-UniversalOverlay.Base),codeOwner.Bytes).ToArray();
 for(int at=0;at<candidateCode.Length;at+=4)
 {var bad=candidateCode.ToArray();bad[at]^=1;Refuse(()=>aggregate.RequireCompiled(bad,codeOwner.Base),"every copied source/replay/init/native/argument/shadow/table word");}

 var structuralParts=aggregate.ExpectedParts();var (structuralCode,structuralLabels)=UniversalBehaviorBundleBuilder.Assemble(structuralParts,codeOwner.Base);Check(structuralCode.SequenceEqual(candidateCode),"independently derived complete source/native structural packet matches ordinary emission");
 for(int i=0;i<structuralParts.Count;i++)
 {
  var ownerParts=aggregate.ExpectedParts();int index=ownerParts[i].Items.FindIndex(item=>item.Word!=null);ownerParts[i].Items[index]=ownerParts[i].Items[index] with{SourcePc=0x80000000};Refuse(()=>aggregate.RequireCompiled(candidateCode,codeOwner.Base,ownerParts),"copied source/constructor/wrapper/native producer schema");
  ownerParts=aggregate.ExpectedParts();index=ownerParts[i].Items.FindIndex(item=>item.Label!=null);if(index>=0){ownerParts[i].Items[index]=ownerParts[i].Items[index] with{Label="unowned_incoming"};Refuse(()=>aggregate.RequireCompiled(candidateCode,codeOwner.Base,ownerParts),"copied label/entry ownership namespace");}
 }
 File.WriteAllText(Path.Combine(directory,"ordinary-chain-emission.json"),JsonSerializer.Serialize(new{Scope="Actual ordinary Compose emitted native path/cache prerequisite; movement remains independently tested",Base=codeOwner.Base,CodeHex=Convert.ToHexString(candidateCode),Labels=structuralLabels},new JsonSerializerOptions{WriteIndented=true})+"\n");
 foreach(var active in new[]{authoredGroup.Take(3).ToArray(),authoredGroup.Concat(authoredGroup.Take(1)).ToArray(),authoredGroup.Select((p,i)=>i==0?p with{Yaw=0}:p).ToArray(),authoredGroup.Select((p,i)=>i==0?p with{X=p.X+1}:p).ToArray(),authoredGroup.Select((p,i)=>i==0?p with{LinkGroupId="changed"}:p).ToArray(),authoredGroup.Select((p,i)=>i==0?p with{RowByteOverrides=new Dictionary<int,byte>{{72,6}}}:p).ToArray()})
  Refuse(()=>UniversalMagicDruidChainCertificate.Capture("darkhollow",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,source,active,null),"partial/duplicated/yaw/relative-pose/group/state override");
 foreach(var binding in source.Bindings)
 {var bad=source.Bindings.ToDictionary();bad[binding.Key]=binding.Value with{Value=binding.Value.Value+4};Refuse(()=>UniversalMagicDruidChainCertificate.Capture("darkhollow",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,new(source.Bytes.ToArray(),bad),authoredGroup,null),"every source callback/copy/particle owner");}
 Refuse(()=>UniversalMagicDruidChainCertificate.Capture("darkhollow",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,source,authoredGroup,[7]),"unproved native deletion/reuse");
 Refuse(()=>UniversalMagicDruidChainCertificate.Capture("artisans",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,source,authoredGroup,null),"unproved chain host");
 foreach(var kind in new[]{"hostData","hostOverlay","sourceData","sourceOverlay","exe"})
 {
  var hd=host.Data.Bytes.ToArray();var ho=host.Overlay.Bytes.ToArray();var sd=sourceData.ToArray();var so=overlayBytes.ToArray();var ex=exe.ToArray();var bad=kind=="hostData"?hd:kind=="hostOverlay"?ho:kind=="sourceData"?sd:kind=="sourceOverlay"?so:ex;bad[bad.Length/2]^=1;
  Refuse(()=>UniversalMagicDruidChainCertificate.Capture("darkhollow",hd,ho,ex,new(sd),new(so,source.Bindings),authoredGroup,null),"complete host/source/native incoming owner mutation");
 }
 var finalData=new UniversalLevelData(composed.LevelData);var finalScene=finalData.Scene();int sceneOffset=finalData.Subfiles()[3].Offset;
 foreach(int physical in composed.NewRowIndices)
 {
  for(int at=0;at<88;at++){var bad=composed.LevelData.ToArray();bad[sceneOffset+finalScene.RowsOffset+physical*88+at]^=1;Refuse(()=>aggregate.RequireFinalRows(new(bad),composed.NewRowIndices),"all final row/pose/class/state/pod/lifetime/visibility bytes");}
  int properties=BitConverter.ToInt32(finalData.Row(physical));for(int at=0;at<120;at++){var bad=composed.LevelData.ToArray();bad[sceneOffset+properties+at]^=1;Refuse(()=>aggregate.RequireFinalRows(new(bad),composed.NewRowIndices),"all final source/indices/shared flag/path/coordinate bytes");}
 }
 for(int at=0;at<composed.LevelData.Length;at+=8192){var bad=composed.LevelData.ToArray();bad[at]^=1;Refuse(()=>aggregate.RequireFinalRows(new(bad),composed.NewRowIndices),"final immutable assets/header/native terrain/source resource owner");}
 foreach(int at in new[]{0,128,8192,composed.Overlay.Length-1}){var bad=composed.Overlay.ToArray();bad[at]^=1;Refuse(()=>aggregate.RequireFinalOverlay(bad),"full final overlay native/incoming/callback/cache/table/schema owner");}
 foreach(int at in new[]{0,4096,exe.Length-1}){var bad=composed.Executable.ToArray();bad[at]^=1;Refuse(()=>aggregate.RequireFinalExecutable(bad),"full native executable/loader/engine/incoming/callback owner");}
 var parameterized=new List<object>();
 foreach(int rootRow in new[]{2,3,4,5})
 foreach(var pose in new[]{(18000,69000,17740),(22000,70000,17740)})
 {
  var sourceRoot=data.Row(rootRow);var baseRoot=data.Row(2);var root=new UniversalMobyPlacement("magiccrafters",rootRow,pose.Item1+BitConverter.ToInt32(sourceRoot,12)-BitConverter.ToInt32(baseRoot,12),pose.Item2+BitConverter.ToInt32(sourceRoot,16)-BitConverter.ToInt32(baseRoot,16),pose.Item3,Yaw:sourceRoot[70],EditorId:Guid.NewGuid().ToString(),PersistentSlot:0);
  var group=UniversalPlacementGroups.Expand(library.Get,root);
  if(rootRow==5)group=Enumerable.Range(0,4).Select(i=>root with{EditorId="retired"+i,Retired=true,PersistentSlot=i}).Concat(group.Select(p=>p with{PersistentSlot=p.PersistentSlot+4})).ToArray();
  var receipt=UniversalMagicDruidChainCertificate.Capture("darkhollow",host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,data,source,group,null);
  var result=UniversalMobyComposer.Compose(host.Level,host.Data.Bytes,host.Overlay.Bytes.ToArray(),exe,library.Get,library.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],host.Level),group,sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations,magicPairBundlePaddingBytes:rootRow==4?256:0);
  var owner=result.Behavior.Single().PrivateMagicDruidChain!;receipt.RequireCompiled(result.Overlay.AsSpan((int)(owner.Base-UniversalOverlay.Base),owner.Bytes).ToArray(),owner.Base);receipt.RequireFinalRows(new(result.LevelData),result.NewRowIndices.Where(row=>row>=0).ToArray());receipt.RequireFinalOverlay(result.Overlay);receipt.RequireFinalExecutable(result.Executable);checks+=4;
  Check(result.NewRowIndices.Where(row=>row>=0).SequenceEqual(new[]{128,129,130,131})&&result.PlacementSaveRows.Where(row=>row>=0).SequenceEqual(rootRow==5?new[]{132,133,134,135}:new[]{128,129,130,131}),"ordinary either-root source order, independent bundle allocation, retired lifetime identities");
  parameterized.Add(new{RootRow=rootRow,Pose=pose,CodeBase=owner.Base,result.NewRowIndices,result.PlacementSaveRows});
 }
 File.WriteAllText(Path.Combine(directory,"ordinary-parameterized-tests.json"),JsonSerializer.Serialize(new{Checks=checks,Cases=parameterized,Scope="Original unchanged Dark Hollow; four source1→0 chain members, common pose/native yaw/lifetime identity, original13B/mixed/edited-host refusals remain"},new JsonSerializerOptions{WriteIndented=true})+"\n");
 File.WriteAllBytes(Path.Combine(directory,"ordinary-chain.data"),composed.LevelData);File.WriteAllBytes(Path.Combine(directory,"ordinary-chain.overlay"),composed.Overlay);File.WriteAllBytes(Path.Combine(directory,"ordinary-chain.exe"),composed.Executable);
 File.WriteAllText(Path.Combine(directory,"ordinary-chain.json"),JsonSerializer.Serialize(new{Scope="PRIVATE normal Compose/readback only; source issuer/resources integration review and native runtime pending",Placements=authoredGroup,composed.NewRowIndices,Behavior=composed.Behavior},new JsonSerializerOptions{WriteIndented=true})+"\n");
 var valid=Enumerable.Range(2,4).Select(row=>UniversalMagicDruidChainResources.CaptureSource(data,source,data.CaptureRow("magiccrafters",row)) with{LinkGroupId="native-chain"}).ToArray();
 foreach(var malformed in new[]{valid.Take(3).ToArray(),valid.Concat(valid.Take(1)).ToArray(),valid.Select((t,i)=>i==1?t with{LinkGroupId="other"}:t).ToArray(),valid.Select((t,i)=>i==1?t with{Row=t.Row.Select((b,n)=>n==54?(byte)1:b).ToArray()}:t).ToArray()})
  Refuse(()=>UniversalMagicDruidChainResources.RequireGroup(valid[0],malformed),"missing/duplicated/other-group/changed source chain member");
 for(int row=2;row<=5;row++)
 {
  var raw=data.CaptureRow("magiccrafters",row);
  foreach(int at in Enumerable.Range(0,raw.Properties.Length))
  {var bad=raw.Properties.ToArray();bad[at]^=1;Refuse(()=>UniversalMagicDruidChainResources.CaptureSource(data,source,raw with{Properties=bad}),"every original chain property/path/flag byte");}
 }
}
File.WriteAllText(report,JsonSerializer.Serialize(new{Status="MAGIC_CHAIN_SOURCE_COPY_COMPOSE_READBACK",Checks=checks,NativeEvidence=false})+"\n");
Console.WriteLine($"MAGIC_CHAIN_SOURCE_OWNERSHIP_OK checks={checks}");
