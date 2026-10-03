using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original Haunted effects into intact original Dark Hollow.
/// Native1/70 retain their equivalent host implementation;7/29/65 retain source code.
/// This is a bounded native ownership contract, not gameplay acceptance.</summary>
internal static class UniversalHauntedWizardDarkHollowEffects
{
 private static readonly IReadOnlyList<int> Copied=Array.AsReadOnly(new[]{7,29,65});
 private static readonly UniversalOwnedParticleFunction Spawn=new(0x800881D8,5704,"50b00ff3a9a569328d9192195bc323f515da5c1f9e0e7fb84e1ed08dc70b3a4a",0x8007B388,0x800897C8,Copied);
 private static readonly UniversalOwnedParticleFunction Update=new(0x80086754,6788,"e5af2729cd0e3666ee092c2993b007d3430e44699a67f97258a762e5da0a99a8",0x8007B244,0x80088188,Copied);
 internal static UniversalOwnedParticleCertificate Capture(UniversalLevelData source,UniversalOverlay overlay,UniversalOverlay destination,byte[] exe)
 {
  UniversalHauntedWizardPlacement.RequireSource(source,overlay);UniversalHauntedWizardParticles.RequireNative(exe);UniversalHauntedWizardDarkHollowParticles.Require(destination,exe);
  RequireDispatch(overlay,destination);
  return new(new("hauntedtowers",new UniversalOverlay(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary()),Spawn,Update),"darkhollow",UniversalHauntedWizardDarkHollowParticles.Update,Patch,d=>{UniversalHauntedWizardDarkHollowParticles.Require(d);RequireDispatch(overlay,d);});
 }
 internal static bool Owns(UniversalOwnedParticleCertificate c)=>c.SourceLevelKey=="hauntedtowers"&&c.DestinationLevelKey=="darkhollow"&&c.NativeUpdate==UniversalHauntedWizardDarkHollowParticles.Update&&c.TypeIds.SequenceEqual(Copied)&&c.Profile.Update.Types.SequenceEqual(Copied)&&c.Profile.Spawn==Spawn&&c.Profile.Update==Update;
 private static void RequireDispatch(UniversalOverlay source,UniversalOverlay destination)
 {
  // Complete original managers are owned independently, including native1/70.
  // Type1 differs only in register/scheduling;70 in register/SP scratch layout.
  // Whole-function native replay covers allocator, vector, RNG and expiry helpers.
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc")throw new InvalidDataException("Haunted effects need their complete original source manager.");
  foreach(var (type,spawn,update)in new[]{(1,0x800882B0u,0x80086874u),(7,0x80088538u,0x80086A38u),(29,0x80088B90u,0x8008714Cu),(65,0x80088EB4u,0x8008767Cu),(70,0x800890D8u,0x80087844u)})
   if(source.Word(Spawn.Table+(uint)type*4)!=spawn||source.Word(Update.Table+(uint)type*4)!=update)throw new InvalidDataException("Haunted effect dispatch changed.");
  foreach(var (type,spawn,update)in new[]{(1,0x8008458Cu,0x80083380u),(7,0x8008553Cu,0x80084450u),(29,0x8008553Cu,0x80084450u),(65,0x8008553Cu,0x80084450u),(70,0x80084E54u,0x80083B0Cu)})
   if(destination.Word(0x8007ACB8+(uint)type*4)!=spawn||destination.Word(0x8007AB74+(uint)type*4)!=update)throw new InvalidDataException("Dark Hollow effect dispatch changed.");
 }
 internal static void RequireTextures(UniversalLevelData source,UniversalLevelData destination)
 {
  var src=UniversalParticleDependencies.Records(source);var dst=UniversalParticleDependencies.Records(destination);
  foreach(int type in new[]{1,7,29})if(!src.TryGetValue(type,out var a)||!dst.TryGetValue(type,out var b)||a[4]-a[0]!=b[4]-b[0]||a[5]-a[1]!=b[5]-b[1]||!UniversalParticleDependencies.EquivalentTexture(a,source.Vram(),b,destination.Vram()))throw new InvalidDataException("Haunted effects need their original texture pixels and palettes after placement.");
  //65/70 allocate the native untextured kind1 and write their own RGB endpoints;
  // the complete native573C8 renderer owner is authenticated by RequireNative.
  foreach(int type in new[]{65,70})if(src.ContainsKey(type)||dst.ContainsKey(type))throw new InvalidDataException("Haunted line effects changed their native untextured resource contract.");
 }
 internal static UniversalParticleDependency Inspect(int type)=>type is 1 or 70?new("hauntedtowers",type,"source-certified-native-equivalent","Uses the authenticated equivalent native effect with its complete lifetime and resources; native gameplay remains pending."):throw new InvalidDataException("No native Haunted effect equivalence was issued for this type.");
 private static void Patch(UniversalAsmPart part,UniversalOverlay source,string label)
 {
  UniversalHauntedWizardParticles.PatchConstructor(part,source,label);
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x26,0xCB,0xCC,0xD7],"effects_proof").Part;
  foreach(uint site in new[]{0x8007CA4Cu,0x80081AE8u})
  {
   if(!part.Items.Any(i=>i.Orig==site))continue;
   for(uint pc=site-24;pc<=site+4;pc+=4){var actual=part.Items.Where(i=>i.Orig==pc).ToArray();var wanted=expected.Items.Single(i=>i.Orig==pc);if(actual.Length!=1||actual[0].Word!=wanted.Word||actual[0].Fix!=wanted.Fix)throw new InvalidDataException("Haunted effect constructor arguments or live actor changed.");}
   int at=part.Items.FindIndex(i=>i.Orig==site);part.Items[at]=part.Items[at]with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
  }
 }
}
