namespace Spyro.Editor.Core.Universal;
internal sealed partial class UniversalGreenDruidGroup8Actors
{
 private static readonly UniversalOwnedParticleFunction Spawn58=UniversalGreenDruidWorldSpellParticles.Spawn8 with{Types=new[]{5,8}};
 private static readonly UniversalOwnedParticleFunction Update58=UniversalGreenDruidWorldSpellParticles.Update8 with{Types=new[]{5,8}};
 internal UniversalOwnedParticleCertificate Dispatcher(UniversalOverlay destination)
 {
  RequireDestination(destination);var profile=new UniversalOwnedParticleProfile("alpineridge",new(_overlay.Bytes.ToArray(),_overlay.Bindings.ToDictionary()),Spawn58,Update58);
  Require(_overlay.Word(Spawn58.Table+20)==0x8008CC68&&_overlay.Word(Update58.Table+20)==0x8008B5D8,"exact Alpine type5 source dispatcher");
  return new(profile,"peacekeepers",destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,(part,owner,label)=>PatchActor(part,owner,label),RequireDestination);
 }
 private void PatchActor(UniversalAsmPart part,UniversalOverlay source,string particleSpawnLabel)
 {
  Require(_map!=null&&_groups!=null,"issued world groups and class namespace precede copied actor emission");Require(source.Bytes.SequenceEqual(_overlay.Bytes)&&source.Bindings.SequenceEqual(_overlay.Bindings),"exact copied source callback owner");
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(_overlay,Classes,part.Items.First().Label!,_map).Part;RequireSamePart(part,expected,"complete copied update flow/labels/operands/tables/entry and delay slots");
  MarkModelReads(part);
  // Each complete original indirect packet is source-addressed, not found by a
  // generic numeric scan. Preserve target args, real player path and delay slots.
  PatchPacket(0x80083A70,12,0x80083A98);
  PatchPacket(0x80086BCC,8,0x80086BE4);
  // The original mode3 handoff uses counted-path byte7 then group9. Actor
  // state reset10 is independent. Rebase only its authenticated group argument.
  for(uint pc=0x80084198;pc<=0x800841D4;pc+=4)RequireItem(pc);
  int at=part.Items.FindIndex(i=>i.Orig==0x800841B0);Require(part.Items[at].Word==0x24040009,"source group9 setter argument");part.Items[at]=part.Items[at] with{Word=UniversalMips.EncodeI(9,0,4,_groups![9])};
  void RequireItem(uint pc)
  {
   var n=part.Items.Select((i,n)=>(i,n)).Where(x=>x.i.Orig==pc).ToArray();Require(n.Length==1&&n[0].i.Word==_overlay.Word(pc),"source packet input changed at"+pc.ToString("X8"));
   if(UniversalMips.IsPcRelative(_overlay.Word(pc))||UniversalMips.Op(_overlay.Word(pc))==2)Require(n[0].i.Fix.Orig==UniversalMips.BranchTarget(_overlay.Word(pc),pc),"source packet edge changed");
  }
  void PatchPacket(uint first,int count,uint call)
  {
   for(int n=0;n<count;n++){uint pc=first+(uint)n*4;RequireItem(pc);if(n>0)Require(part.Items.FindIndex(i=>i.Orig==pc)==part.Items.FindIndex(i=>i.Orig==pc-4)+1,"source particle packet adjacency/entry");}
   int index=part.Items.FindIndex(i=>i.Orig==call);Require(UniversalMips.IsJalr(part.Items[index].Word!.Value),"authentic indirect particle factory call");part.Items[index]=part.Items[index] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:particleSpawnLabel)};
  }
 }
 private void MarkModelReads(UniversalAsmPart part)
 {
  // Physical class remains the index into g_Models. All three roots are then
  // replaced in v0 by an animation header; no residual-address ABI assumption.
  foreach(var (pc,header) in new (uint,int)[]{(0x80087AC8,0x50),(0x80087EF0,0x54),(0x80087F60,0x3C)})
  {
   int at=part.Items.FindIndex(i=>i.Orig==pc);Require(at>=0&&part.Items.Count(i=>i.Orig==pc)==1,"three complete physical animation model reads");
   uint end=pc==0x80087AC8?0x80087AF4:pc==0x80087EF0?0x80087F1C:0x80087F8C;
   for(uint q=pc;q<=end;q+=4){int n=at+(int)((q-pc)/4);Require(n<part.Items.Count&&part.Items[n].Orig==q&&part.Items[n].Word==_overlay.Word(q)&&part.Items[n].Fix.Kind==UniversalFixKind.None,"complete linear animation/header packet changed");}
   Require(_overlay.Word(end-8)==UniversalMips.EncodeI(35,2,2,header)&&_overlay.Word(end)==UniversalMips.EncodeI(36,2,pc==0x80087AC8?4:3,12),"animation root read and scalar kill owner");part.Items[at]=part.Items[at] with{ClassReadOwned=true};
  }
 }
 internal void RequireParticles(UniversalLevelData final)
 {
  var source=UniversalParticleDependencies.Records(_source);var actual=UniversalParticleDependencies.Records(final);
  foreach(int type in new[]{5,8,11})Require(actual.TryGetValue(type,out var b)&&UniversalParticleDependencies.EquivalentTexture(source[type],_source.Vram(),b,final.Vram()),"complete particle"+type+" pixels/palette owner");
  Require(!source.ContainsKey(72),"native line72 is untextured");
 }
 internal void RequireOwnedParticles(IReadOnlyList<UniversalOwnedParticleCertificate> owned)
 {Require(owned.Count==1&&owned[0].SourceLevelKey=="alpineridge"&&owned[0].DestinationLevelKey=="peacekeepers"&&owned[0].TypeIds.SequenceEqual(new[]{5,8})&&owned[0].Profile.Spawn==Spawn58&&owned[0].Profile.Update==Update58,"one merged source5+8 manager; retained native11/72 runs exactly once");}
}
