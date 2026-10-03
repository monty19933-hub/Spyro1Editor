using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;
internal sealed partial class UniversalGreenDruidGroup8Actors
{
 // Generic texture/sound import remains the producer. This verifies the issued
 // complete package after those two exact transformations, before registration.
 internal void RequireImportedPackages(IReadOnlyList<(int ClassId,byte[] Package)>pending,UniversalLevelData destination)
 {
  Require(_map!=null,"issued namespace precedes model import");foreach(int c in Modeled){var found=pending.Where(p=>p.ClassId==_map![c]).ToArray();Require(found.Length==1,"one complete imported model"+c.ToString("X3"));RequirePackage(c,found[0].Package,destination,false);}
  Require(!pending.Any(p=>p.ClassId==_map![0x148]),"native target148 must not acquire a substitute model");RequirePoof(destination);
 }
 // Preserve generic AppendModels for all other inputs. The only extra prefixes
 // are issued four-zero bytes for original146/127 rootsmod8=4; registration roots
 // point past the prefix, not at a fake model header. Existing owners do not move.
 internal IReadOnlyList<int> AppendModels(UniversalLevelData destination,IReadOnlyList<(int ClassId,byte[] Package)> pending)
 {
  RequireImportedPackages(pending,destination);var before=new UniversalLevelData(destination.Bytes.ToArray());var original=before.Registrations();
  var mods=Modeled.ToDictionary(c=>_map![c],c=>_source.Registrations().Single(r=>r.ClassId==c).Root&7);
  var padded=pending.Select(p=>mods.TryGetValue(p.ClassId,out int pad)&&pad==4?(p.ClassId,Package:new byte[4].Concat(p.Package).ToArray()):p).ToArray();var slots=destination.AppendModels(padded);
  for(int n=0;n<pending.Count;n++)if(mods.GetValueOrDefault(pending[n].ClassId)==4){int field=0x50+slots[n]*4;destination.Put32(field,destination.U32(field)+4);}
  var old=before.Subfiles()[2];Require(destination.Bytes.AsSpan(old.Offset,old.Length).SequenceEqual(before.Bytes.AsSpan(old.Offset,old.Length))&&original.All(r=>destination.Registrations().Any(x=>x==r)),"existing complete model data/registration owner");
  foreach(int c in Modeled){var r=destination.Registrations().Single(r=>r.ClassId==_map![c]);Require((r.Root&7)==(_source.Registrations().Single(r=>r.ClassId==c).Root&7),"native source root alignment");}
  RequireFinalResources(destination);return slots;
 }
 internal void RequireFinalResources(UniversalLevelData destination)
 {
  Require(_map!=null,"issued namespace precedes resource readback");var regs=destination.Registrations();foreach(int c in Modeled){var r=regs.Where(r=>r.ClassId==_map![c]).ToArray();Require(r.Length==1&&(r[0].Root&7)==(_source.Registrations().Single(r=>r.ClassId==c).Root&7),"complete physical model registration/alignment");RequirePackage(c,destination.Package(_map![c]),destination,true);}
  Require(!regs.Any(r=>r.ClassId==_map![0x148]),"native cache-only target owns no model");RequirePoof(destination);RequireParticles(destination);
 }
 private void RequirePoof(UniversalLevelData destination)
 {
  var a=new NativeLevelSoundLayout(_source.Bytes).Sound;var b=new NativeLevelSoundLayout(destination.Bytes).Sound;int id=b.Map[38];Require(id!=255&&id<b.Descriptors.Count,"retained global poof sound selector38");RequireSound(a.Descriptors[a.Map[38]],b.Descriptors[id]);
 }
 private static void RequireSound(Spyro.Editor.Core.Analysis.NativeSoundDescriptor a,Spyro.Editor.Core.Analysis.NativeSoundDescriptor b)
 {Require(a.Sample.ByteLength==b.Sample.ByteLength&&a.Sample.CopyBytes().SequenceEqual(b.Sample.CopyBytes())&&a.CopyDescriptor().AsSpan(4).SequenceEqual(b.CopyDescriptor().AsSpan(4)),"complete ADPCM sample and native descriptor controls");}
 private void RequirePackage(int sourceClass,byte[]actual,UniversalLevelData destination,bool allocationTail)
 {
  byte[] original=_packages[sourceClass];Require(actual.Length>=original.Length&&(allocationTail?actual.Length-original.Length<2048:actual.Length==original.Length)&&actual.AsSpan(original.Length).IndexOfAnyExcept((byte)0)<0,"complete source model payload and issued zero alignment suffix");
  var a=original.ToArray();var b=actual.AsSpan(0,original.Length).ToArray();var asnd=new NativeLevelSoundLayout(_source.Bytes).Sound;var bsnd=new NativeLevelSoundLayout(destination.Bytes).Sound;
  for(int n=4;n<20;n++){if(a[n]==255)Require(b[n]==255,"unused native local sound slot changed");else{Require(b[n]!=255&&b[n]<bsnd.Descriptors.Count,"required local sound cannot be silenced");RequireSound(asnd.Descriptors[a[n]],bsnd.Descriptors[b[n]]);}a[n]=b[n];}
  var offsets=UniversalAssetImport.TextureDescriptorOffsets(original);Require(offsets.SequenceEqual(UniversalAssetImport.TextureDescriptorOffsets(b)),"complete renderer material topology");
  foreach(int at in offsets){RequireTexture12(original.AsSpan(at,12),_source.Vram(),b.AsSpan(at,12),destination.Vram());b.AsSpan(at,12).CopyTo(a.AsSpan(at));}
  Require(a.SequenceEqual(b),"model geometry, animations, tables, material flags or full payload was pruned/changed");
 }
 private static void RequireTexture12(ReadOnlySpan<byte>a,ReadOnlySpan<byte> av,ReadOnlySpan<byte>b,ReadOnlySpan<byte>bv)
 {
  int du=b[0]-a[0],dv=b[1]-a[1];foreach(int off in new[]{0,4,8,10})Require(b[off]-a[off]==du&&b[off+1]-a[off+1]==dv,"all four native UV corners retain common translation");
  // The complete four-corner footprint can exceed either diagonal. Validate
  // its bounding rectangle and full palette, retaining depth/material bits.
  byte[] x=a[..8].ToArray(),y=b[..8].ToArray();x[0]=new[]{a[0],a[4],a[8],a[10]}.Min();x[4]=new[]{a[0],a[4],a[8],a[10]}.Max();x[1]=new[]{a[1],a[5],a[9],a[11]}.Min();x[5]=new[]{a[1],a[5],a[9],a[11]}.Max();y[0]=checked((byte)(x[0]+du));y[4]=checked((byte)(x[4]+du));y[1]=checked((byte)(x[1]+dv));y[5]=checked((byte)(x[5]+dv));
  Require(UniversalParticleDependencies.EquivalentTexture(x,av,y,bv),"every sampled model texel/full palette/native material bit");
 }
}
