using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Closed extension of Dark Hollow's Haunted wizard certificate. Kiss31
/// needs its source manager; native66 and powered flame1/79 retain the host manager.</summary>
internal static class UniversalHauntedSuperflameEffects
{
 private static readonly IReadOnlyList<int> Copied=Array.AsReadOnly(new[]{7,29,31,65});
 private static readonly UniversalOwnedParticleFunction Spawn=new(0x800881D8,5704,"50b00ff3a9a569328d9192195bc323f515da5c1f9e0e7fb84e1ed08dc70b3a4a",0x8007B388,0x800897C8,Copied);
 private static readonly UniversalOwnedParticleFunction Update=new(0x80086754,6788,"e5af2729cd0e3666ee092c2993b007d3430e44699a67f97258a762e5da0a99a8",0x8007B244,0x80088188,Copied);
 internal static UniversalOwnedParticleCertificate Capture(UniversalLevelData source,UniversalOverlay overlay,UniversalOverlay destination,byte[] exe)
 {
  UniversalHauntedSuperflameFairy.Capture(source,overlay,source.CaptureRow("hauntedtowers",82));UniversalHauntedSuperflameFairy.RequireNative(exe);UniversalHauntedWizardDarkHollowEffects.Capture(source,overlay,destination,exe);RequireDispatch(overlay,destination);
  return new(new("hauntedtowers",new UniversalOverlay(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary()),Spawn,Update),"darkhollow",UniversalHauntedWizardDarkHollowParticles.Update,Patch,d=>{UniversalHauntedWizardDarkHollowParticles.Require(d);RequireDispatch(overlay,d);});
 }
 internal static bool Owns(UniversalOwnedParticleCertificate c)=>c.SourceLevelKey=="hauntedtowers"&&c.DestinationLevelKey=="darkhollow"&&c.NativeUpdate==UniversalHauntedWizardDarkHollowParticles.Update&&c.TypeIds.SequenceEqual(Copied)&&c.Profile.Spawn==Spawn&&c.Profile.Update==Update;
 private static void RequireDispatch(UniversalOverlay source,UniversalOverlay host)
 {
  foreach(var(t,ss,su,ds,du)in new[]{(31,0x80088D00u,0x800875A0u,0x8008553Cu,0x80084450u),(66,0x80088F78u,0x80087778u,0x80084CF4u,0x80083A40u),(1,0x800882B0u,0x80086874u,0x8008458Cu,0x80083380u),(79,0x80089538u,0x80087F74u,0x800852ACu,0x8008423Cu)})
   if(source.Word(Spawn.Table+(uint)t*4)!=ss||source.Word(Update.Table+(uint)t*4)!=su||host.Word(0x8007ACB8+(uint)t*4)!=ds||host.Word(0x8007AB74+(uint)t*4)!=du)throw new InvalidDataException("Superflame kiss, sparkle or native powered flame dispatch changed.");
 }
 internal static void RequireTextures(UniversalLevelData source,UniversalLevelData destination)
 {
  UniversalHauntedWizardDarkHollowEffects.RequireTextures(source,destination);var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);
  foreach(int t in new[]{31})if(!a.TryGetValue(t,out var sa)||!b.TryGetValue(t,out var db)||sa.Length!=8||db.Length!=8||!UniversalParticleDependencies.EquivalentTexture(sa,source.Vram(),db,destination.Vram()))throw new InvalidDataException("Superflame needs its complete original kiss texture pixels and palette.");
  UniversalHauntedSuperflameScene.RequireEquivalent(source,destination);
  if(a.ContainsKey(66)||b.ContainsKey(66))throw new InvalidDataException("Fairy sparkle66 needs its original native untextured line resource.");
 }
 internal static void RequireBundle(string source,IReadOnlyList<int> classes,UniversalOverlay destination,byte[] exe,IReadOnlyList<UniversalOwnedParticleCertificate>? certs)
 {
  if(source!="hauntedtowers"||!classes.Contains(0x8E)||UniversalHauntedWizardParticles.HasNativeOwner(destination))return;
  if(!classes.Contains(0xCC)||!classes.Contains(0xCB)||certs?.Count(c=>Owns(c))!=1)throw new InvalidDataException("This fairy needs its issued complete original Dark Hollow power and effects owner.");RequireNativeOwner(destination,exe);
 }
 private static void RequireNativeOwner(UniversalOverlay host,byte[] exe){UniversalHauntedWizardDarkHollowParticles.Require(host,exe);UniversalHauntedSuperflameFairy.RequireNative(exe);}
 private static void Patch(UniversalAsmPart part,UniversalOverlay source,string label)
 {
  UniversalHauntedSuperflameFairy.RequireCopied(part,source,[0x8E]);UniversalHauntedWizardDarkHollowEffects.Patch(part,source,label);
  const uint site=0x80080510;var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x8E],"fairy_emit").Part;
  for(uint pc=site-32;pc<=site+4;pc+=4){var es=expected.Items.Single(x=>x.Orig==pc);var actual=part.Items.Where(x=>x.Orig==pc).ToArray();if(actual.Length!=1||actual[0].Word!=es.Word||actual[0].Fix!=es.Fix)throw new InvalidDataException("Original fairy kiss emission arguments changed.");}
  int at=part.Items.FindIndex(i=>i.Orig==site);part.Items[at]=part.Items[at]with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
 }
}
