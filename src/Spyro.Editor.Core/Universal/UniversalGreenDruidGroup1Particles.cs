namespace Spyro.Editor.Core.Universal;
internal sealed partial class UniversalGreenDruidGroup1Actors
{
 internal UniversalOwnedParticleCertificate Dispatcher(UniversalOverlay destination)
 {
  RequireDestination(destination);var profile=UniversalGreenDruidWorldSpellParticles.CaptureSource(_source,_overlay,_exe);
  return new(profile,"peacekeepers",destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,(part,owner,label)=>PatchActor(part,owner,label),RequireDestination);
 }
 private void PatchActor(UniversalAsmPart part,UniversalOverlay source,string label)
 {
  Require(_map!=null&&_groups!=null,"issued world group and namespace precede emission");Require(source.Bytes.SequenceEqual(_overlay.Bytes)&&source.Bindings.SequenceEqual(_overlay.Bindings),"complete original overlay owner");
  UniversalGreenDruidWorldSpellParticles.PatchCalls(part,source,label,Classes,_map!);
 }
 internal void RequireParticles(UniversalLevelData final)
 {
  UniversalGreenDruidWorldSpellParticles.RequireTexture(_source,final);var a=UniversalParticleDependencies.Records(_source);var b=UniversalParticleDependencies.Records(final);Require(b.TryGetValue(11,out var poof)&&UniversalParticleDependencies.EquivalentTexture(a[11],_source.Vram(),poof,final.Vram()),"unchanged native Town poof11 needs exact source sampled texels/palette");
 }
 internal void RequireOwnedParticles(IReadOnlyList<UniversalOwnedParticleCertificate> owned)
 {Require(owned.Count==1&&owned[0].SourceLevelKey=="alpineridge"&&owned[0].DestinationLevelKey=="peacekeepers"&&owned[0].TypeIds.SequenceEqual(new[]{8})&&owned[0].Profile.Spawn==UniversalGreenDruidWorldSpellParticles.Spawn8&&owned[0].Profile.Update==UniversalGreenDruidWorldSpellParticles.Update8,"one complete source8 manager and exact native continuation");}
}
