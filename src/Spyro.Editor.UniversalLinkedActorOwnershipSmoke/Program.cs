using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks=0;
void Check(bool b,string message){if(!b)throw new InvalidOperationException(message);checks++;}
void Refuse(Action a,string message){try{a();}catch(InvalidDataException){checks++;return;}throw new InvalidOperationException(message);}
if(args.Length!=2)throw new ArgumentException("<original USA.bin> <workspace>");
UniversalSourceLibrary.RequireOriginal(args[0]);
var library=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));
var source=library.Get("gnorccove");var overlay=source.Overlay;
var map=new Dictionary<int,int>{{0x9A,0x1FD},{0xCA,0x1FB},{0xCF,0x1FA},{0xC9,0x1F9}};
UniversalAsmPart Update()=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(overlay,[0x9A,0xCA,0xCF],"u_test",map).Part;
Dictionary<int,UniversalAsmPart> Constructors()=>new[]{0xCA,0xCF}.ToDictionary(c=>c,c=>UniversalBehaviorBundleBuilder.BuildSpecializedInit(overlay,[c],$"i_{c:X3}").Part);
var update=Update();var init=Constructors();
var sites=UniversalNativeLinkedActorOwnership.Patch(update,overlay,map,init);
Check(sites.SequenceEqual(new[]{0x800814E0u,0x80081764u,0x80081E14u,0x800821B8u}),"all native linked comparisons owned");
Check(update.Items.Where(i=>sites.Contains(i.Orig??i.SourcePc??0)).All(i=>i.ClassReadOwned),"all comparison copies marked");
foreach(var spec in new[]{(Start:0x800814D0u,Words:UniversalNativeLinkedActorOwnership.ParentConsumer,Init:false,Class:0xCA),
    (Start:0x80081738u,Words:UniversalNativeLinkedActorOwnership.IndexConsumer,Init:false,Class:0xCF),
    (Start:0x80081E04u,Words:UniversalNativeLinkedActorOwnership.ParentConsumer,Init:false,Class:0xCF),
    (Start:0x80085B84u,Words:UniversalNativeLinkedActorOwnership.ParentProducer,Init:true,Class:0xCA),
    (Start:0x80085C04u,Words:UniversalNativeLinkedActorOwnership.IndexProducer,Init:true,Class:0xCF),
    (Start:0x800821A8u,Words:UniversalNativeLinkedActorOwnership.SharedParentConsumer,Init:false,Class:0xCA)})
{
    for(int n=0;n<spec.Words.Length;n++){
        uint pc=spec.Start+(uint)n*4;var u=Update();var ci=Constructors();var part=spec.Init?ci[spec.Class]:u;
        int at=part.Items.FindIndex(i=>(i.Orig??i.SourcePc)==pc);Check(at>=0,"native witness retained");
        part.Items[at]=part.Items[at] with{Word=part.Items[at].Word^1};
        Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,ci),$"mutated copy {pc:X8}");
        u=Update();ci=Constructors();part=spec.Init?ci[spec.Class]:u;
        part.Items.RemoveAt(part.Items.FindIndex(i=>(i.Orig??i.SourcePc)==pc));
        Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,ci),$"missing copy {pc:X8}");
    }
}
foreach (uint start in new[]{0x800814D0u,0x80081738u,0x80081E04u,0x800821A8u}) {
 var u=Update();u.Emit(0x10000000,new(UniversalFixKind.Branch,start+4));
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"new branch cannot bypass linked producer");
 u=Update();u.Tables.Add("unowned_entry",new[]{start+4});
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"new table cannot bypass linked producer");
 u=Update();int at=u.Items.FindIndex(i=>(i.Orig??i.SourcePc)==start+4);
 u.Items[at]=u.Items[at] with{Label="unowned_label"};u.Emit(0,new(UniversalFixKind.JumpLabel,Label:"unowned_label"));
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"new label cannot bypass linked producer");
}
for(uint target=0x80081E08;target<=0x80081E34;target+=4){
 var u=Update();u.Emit(UniversalMips.EncodeJ(2,target));
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"raw CF parent incoming jump refuses");
 foreach(var kind in new[]{UniversalFixKind.Branch,UniversalFixKind.Jump}){
 u=Update();u.Emit(UniversalMips.EncodeJ(2,target),new(kind,target));
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"CF parent new incoming edge refuses");}
 u=Update();u.Tables["bad_cf_parent"]=[target];
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"CF parent interior jump table refuses");
 u=Update();int at=u.Items.FindIndex(i=>(i.SourcePc??i.Orig)==target);u.Items.Insert(at,new(null,default,null,"bad_cf_parent_label"));u.Emit(0,new(UniversalFixKind.JumpLabel,Label:"bad_cf_parent_label"));
 Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(u,overlay,map,Constructors()),"CF parent actual interior label reference refuses");
}
for(uint pc=0x80081E04;pc<=0x80081E34;pc+=4){
 var modified=overlay.Bytes.ToArray();modified[(int)(pc-UniversalOverlay.Base)]^=1;
 Check(UniversalNativeLinkedActorOwnership.Patch(Update(),new(modified,overlay.Bindings),map,Constructors()).Count==0,"Changed CF source consumer cannot enroll");
}
foreach(uint global in new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal}) {
 var bindings=overlay.Bindings.ToDictionary();bindings[global]=bindings[global] with{Value=bindings[global].Value+4};
 Check(UniversalNativeLinkedActorOwnership.Patch(Update(),new(overlay.Bytes.ToArray(),bindings),map,Constructors()).Count==0,"changed callback cannot enroll");
}
var missing=Constructors();missing.Remove(0xCA);
Refuse(()=>UniversalNativeLinkedActorOwnership.Patch(Update(),overlay,map,missing),"missing actual constructor");
var altered=overlay.Bytes.ToArray();altered[100]^=1;
Check(UniversalNativeLinkedActorOwnership.Patch(Update(),new(altered,overlay.Bindings),map,Constructors()).Count==0,"full source hash mutation cannot enroll");
foreach(int c in new[]{0xCA,0xCF}){
 var ci=Constructors();UniversalClassRemapping.PatchInitClassStore(ci[c],overlay,c,map[c]);
 Check(UniversalNativeLinkedActorOwnership.Patch(Update(),overlay,map,ci).Count==4,"actual mapped constructor retains producer proof");
}
// Execute the assembled inverse-class conversion independently with R3000 load/branch
// delays. Every native class value must map back, with all other registers unchanged.
for(uint nativeClass=0;nativeClass<512;nativeClass++) {
 var part=new UniversalAsmPart();part.Label("scalar");part.Emit(0x84830036,orig:0x800814E0);
 UniversalClassRemapping.TranslateOwnedClassification(part,1,map,"scalar");part.Label("done");
 var(code,labels)=UniversalBehaviorBundleBuilder.Assemble([part],0x80150000);
 uint[] r=Enumerable.Range(0,32).Select(n=>0xAC000000u+(uint)n).ToArray();r[0]=0;r[4]=0x80123400;var before=r.ToArray();
 uint pc=0x80150000;uint? pendingLoad=null,pendingJump=null;int budget=256;
 while(pc!=labels["done"]&&budget-->0){
  uint w=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan((int)(pc-0x80150000)));int op=(int)(w>>26),rs=(int)(w>>21)&31,rt=(int)(w>>16)&31;
  uint value=0;bool write=false,load=false;uint? jump=null;
  switch(op){
   case 0:Check(w==0,"only scalar NOP");break;
   case 33:Check(r[rs]+(uint)(short)w==0x80123436,"owned parent class address");value=nativeClass;load=true;break;
   case 9:value=unchecked(r[rs]+(uint)(short)w);write=true;break;
   case 5:if(r[rs]!=r[rt])jump=unchecked(pc+4+(uint)((short)w*4));break;
   case 2:jump=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);break;
   default:throw new InvalidOperationException("Unexpected assembled conversion instruction");
  }
  if(pendingLoad is uint prior)r[3]=prior;pendingLoad=null;
  if(load)pendingLoad=value;else if(write&&rt!=0)r[rt]=value;
  uint next=pendingJump??pc+4;pendingJump=jump;pc=next;
 }
 Check(budget>0,"bounded conversion completion");
 uint expected=map.Any(m=>m.Value==nativeClass)?(uint)map.Single(m=>m.Value==nativeClass).Key:nativeClass;
 Check(r[3]==expected,"inverse namespace for every class");
 Check(Enumerable.Range(0,32).Where(n=>n!=3).All(n=>r[n]==before[n]),"all other live registers preserved");
}
// Compose two identical indexed pairs as independent groups through the ordinary Core path.
var destination=library.Get("darkhollow");var nativeRows=destination.Data.Scene().RowCount;
var firstGroup=UniversalPlacementGroups.Expand(library.Get,new("gnorccove",34,16000,61000,17766,77,EditorId:"cf-group-a",PersistentSlot:0));
var secondGroup=UniversalPlacementGroups.Expand(library.Get,new("gnorccove",34,17000,61000,17766,77,EditorId:"cf-group-b",PersistentSlot:2));
var placements=firstGroup.Concat(secondGroup).ToArray();
Check(placements.Length==4&&placements.Select(p=>p.LinkGroupId).Distinct().Count()==2,"duplicate indexed group identity distinct");
var composed=UniversalMobyComposer.Compose(destination.Level,destination.Data.Bytes,destination.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,
    UniversalMobyExport.DefaultProtectedVram(args[0],destination.Level),placements,sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
var readback=new UniversalLevelData(composed.LevelData);
Check(composed.RestoredIndexedObjectLinks==4&&readback.Scene().RowCount==nativeRows+4,"duplicate CF/C9 groups composed with four indexed links");
for(int group=0;group<2;group++){
 int cf=composed.NewRowIndices[group*2],c9=composed.NewRowIndices[group*2+1];
 var cfProps=readback.CaptureRow("written",cf).Properties;var c9Props=readback.CaptureRow("written",c9).Properties;
 Check(BinaryPrimitives.ReadInt32LittleEndian(cfProps.AsSpan(0x10))==c9,"CF exact physical target from same link group");
 Check(BinaryPrimitives.ReadInt32LittleEndian(c9Props)==cf,"C9 exact reciprocal target from same link group");
}
Check(composed.Behavior.Single(b=>b.SourceLevelKey=="gnorccove").LinkedClassReadSites.Contains(0x80081E14),"ordinary compose includes CF parent ownership");
Check(composed.PlayerAnimationImports.SelectMany(p=>p.RequestedAnimationIds).Contains(29),"ordinary duplicate group compose carries native helper reaction29");
Console.WriteLine($"DUPLICATE_CF_GROUP_COMPOSE_OK rows={string.Join(',',composed.NewRowIndices)} links={composed.RestoredIndexedObjectLinks}");
UniversalSourceLibrary.RequireOriginal(args[0]);
Console.WriteLine($"UNIVERSAL_LINKED_ACTOR_SOURCE_OK checks={checks}");
