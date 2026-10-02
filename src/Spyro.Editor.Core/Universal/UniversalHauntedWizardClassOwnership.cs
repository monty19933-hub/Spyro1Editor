using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Exact Haunted class26 constructor parent classification. Allocated model indexing retains destination IDs.</summary>
internal static class UniversalHauntedWizardClassOwnership
{
 internal static IReadOnlyList<uint> PatchProjectileParent(UniversalAsmPart part,UniversalOverlay source,IReadOnlyDictionary<int,int> map,int sourceClass)
 {
  if(sourceClass!=0x26 || !map.Any(m=>m.Key!=m.Value))return [];
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc" || source.Init!=0x8008590C || source.Update!=0x8007B510 || source.CopyBuffer!=0x80089820)
   throw new InvalidDataException("Haunted projectile parent lost its original initializer owner.");
  var roles=UniversalClassRemapping.ProveInitializerRoles(part,source);
  if(roles.ClassRegister!=16 || roles.ActorRegister!=19 || roles.ParentRegister!=21 || roles.ParentSavePc!=0x8008591C)
   throw new InvalidDataException("Haunted projectile class/allocated actor/parent roles changed.");
  const uint read=0x80085CA4;
  foreach(var (start,end) in new[]{(0x8008590Cu,0x80085938u),(0x80085C88u,0x80085CB8u),(0x80085CFCu,0x80085D0Cu)}){
   int at=part.Items.FindIndex(x=>x.Orig==start);if(at<0)throw new InvalidDataException("Haunted projectile parent packet is missing.");
   for(uint pc=start;pc<=end;pc+=4){int i=at+(int)((pc-start)/4);if(i>=part.Items.Count)throw new InvalidDataException("Haunted parent packet is truncated.");var x=part.Items[i];uint w=source.Word(pc);var kind=UniversalMips.IsPcRelative(w)?UniversalFixKind.Branch:UniversalFixKind.None;
    if(x.Orig!=pc||x.Word!=w||x.Fix.Kind!=kind||x.Fix.Orig!=(kind==UniversalFixKind.Branch?UniversalMips.BranchTarget(w,pc):0)||part.Items.Count(v=>v.Orig==pc)!=1)throw new InvalidDataException("Haunted parent packet changed, reordered or duplicated.");}
   foreach(var x in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && x.Fix.Orig>start&&x.Fix.Orig<=end))
    if(x.Orig is not uint pc||source.Word(pc)!=x.Word||UniversalMips.BranchTarget(source.Word(pc),pc)!=x.Fix.Orig)throw new InvalidDataException("Haunted parent packet gained an incoming entry.");
  }
  foreach(var x in part.Items.Where(x=>x.Word!=null && (x.Orig??x.SourcePc)>=0x80085920 && (x.Orig??x.SourcePc)<read))
   if(Writes(x.Word!.Value,16)||Writes(x.Word.Value,21))throw new InvalidDataException("Haunted projectile saved class/parent changed before classification.");
  // Exact source comparison at5CAC uses S0, which the specialized constructor
  // receives as original26; PatchInitClassStore changes only the allocated store.
  // Both successor paths overwrite V0 before any address/store/argument use.
  if(source.Word(0x80085CAC)!=0x14500013 || source.Word(0x80085CB8)!=0x86620036 || source.Word(0x80085D04)!=0x2C620006)
   throw new InvalidDataException("Haunted parent comparison-only successors changed.");
  int index=part.Items.FindIndex(x=>x.Orig==read);UniversalClassRemapping.TranslateOwnedClassification(part,index,map,"haunted_projectile_parent");return [read];
 }
 internal static void MarkArmorDebrisModelRead(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes)
 {
  if(!classes.Contains(0xCB))return;
  UniversalHauntedWizardPlacement.RequireCopiedPlacementInputs(part,source,[0xCB]);
  // Full authenticated armor body issues classD7 to source init, preserves a1
  // parent across the jalr, saves returned allocated Moby in S0 at81354, and
  // leaves S0 live through813A4. Class×4 then native gModels selects that
  // allocated actor's destination package; it is never classification/reversed.
  if(source.Word(0x80081334)!=0x240400D7 || source.Word(0x80081344)!=0x0040F809 || source.Word(0x80081348)!=0x02602821 || source.Word(0x80081354)!=0x00408021 || source.Word(0x800813A4)!=0x86020036 || source.Word(0x800813B4)!=0x00021080 || source.Word(0x800813B8)!=0x004B1021 || source.Word(0x800813BC)!=0x8C430000)
   throw new InvalidDataException("Haunted debris allocated-model lookup lost its constructor/model-table owner.");
  foreach(var (read,tail) in new[]{(0x80081414u,0x800843ACu),(0x800817C4u,0x80083D8Cu)}){
   Packet(part,source,read,read+16);Packet(part,source,tail,tail+20);
   if(source.Word(read)!=0x86620036 || source.Word(read+4)!=0x3C0B8007 || source.Word(read+8)!=0x256B6378 || UniversalMips.BranchTarget(source.Word(read+12),read+12)!=tail || source.Word(tail+8)!=0x00021080 || source.Word(tail+12)!=0x004B1021 || source.Word(tail+16)!=0x8C420000)
    throw new InvalidDataException("Haunted current actor model jump lost its exact table consumer.");
   int x=part.Items.FindIndex(i=>i.Orig==read);part.Items[x]=part.Items[x] with{ClassReadOwned=true};
  }
  int at=part.Items.FindIndex(x=>x.Orig==0x800813A4);if(at<0)throw new InvalidDataException("Missing Haunted debris model class read.");part.Items[at]=part.Items[at] with{ClassReadOwned=true};
 }
 private static void Packet(UniversalAsmPart part,UniversalOverlay source,uint start,uint end){
  int at=part.Items.FindIndex(x=>x.Orig==start);if(at<0)throw new InvalidDataException("Missing Haunted model packet.");
  for(uint pc=start;pc<=end;pc+=4){int i=at+(int)((pc-start)/4);if(i>=part.Items.Count)throw new InvalidDataException("Truncated Haunted model packet.");var x=part.Items[i];uint w=source.Word(pc);var k=UniversalMips.IsPcRelative(w)?UniversalFixKind.Branch:UniversalMips.Op(w)==2?UniversalFixKind.Jump:UniversalFixKind.None;
   if(x.Orig!=pc||x.Word!=w||x.Fix.Kind!=k||x.Fix.Orig!=(k==UniversalFixKind.None?0:UniversalMips.BranchTarget(w,pc))||part.Items.Count(v=>v.Orig==pc)!=1)throw new InvalidDataException("Haunted model packet changed, reordered or duplicated.");}
  foreach(var x in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && x.Fix.Orig>start&&x.Fix.Orig<=end))
   if(x.Orig is not uint pc||source.Word(pc)!=x.Word||UniversalMips.BranchTarget(source.Word(pc),pc)!=x.Fix.Orig)throw new InvalidDataException("Haunted model packet gained an incoming entry.");
 }
 private static bool Writes(uint w,int r){int op=UniversalMips.Op(w);if(op==0)return UniversalMips.Rd(w)==r && UniversalMips.Fn(w) is not (8 or 17 or 19 or 24 or 25 or 26 or 27);return op is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 40 or 41 or 42 or 43 or 46) && UniversalMips.Rt(w)==r;}
}
