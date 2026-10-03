using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Bounded original Haunted resurrection29 into issued native no-op29 destination managers.
/// Source placement inputs and seven armor links must also be authenticated. Static proof is not runtime acceptance.</summary>
public static class UniversalHauntedWizardParticles
{
 private const uint Spawn=0x800881D8,Update=0x80086754;
 private static readonly UniversalOwnedParticleFunction SpawnOwner=new(Spawn,5704,"50b00ff3a9a569328d9192195bc323f515da5c1f9e0e7fb84e1ed08dc70b3a4a",0x8007B388,0x800897C8,[29]);
 private static readonly UniversalOwnedParticleFunction UpdateOwner=new(Update,6788,"e5af2729cd0e3666ee092c2993b007d3430e44699a67f97258a762e5da0a99a8",0x8007B244,0x80088188,[29]);
 public static UniversalOwnedParticleCertificate Capture(UniversalLevelData source,UniversalOverlay overlay,UniversalOverlay destination,byte[] exe)=>Capture(source,overlay,destination,exe,"peacekeepers");
 public static UniversalOwnedParticleCertificate Capture(UniversalLevelData source,UniversalOverlay overlay,UniversalOverlay destination,byte[] exe,string destinationLevelKey)
 {
  UniversalHauntedWizardPlacement.RequireSource(source,overlay);RequireNative(exe);
  if(overlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=Spawn || overlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=Update ||
   overlay.Word(SpawnOwner.Table+29*4)!=0x80088B90 || overlay.Word(UpdateOwner.Table+29*4)!=0x8008714C)
   throw new InvalidDataException("Haunted resurrection particle callbacks/table29 changed.");
  uint nativeUpdate=RequireDestination(destination,destinationLevelKey);
  if(destinationLevelKey=="darkhollow")UniversalHauntedWizardDarkHollowParticles.Require(destination,exe);
  return new(new("hauntedtowers",new UniversalOverlay(overlay.Bytes.ToArray(),overlay.Bindings.ToDictionary(p=>p.Key,p=>p.Value)),SpawnOwner,UpdateOwner),destinationLevelKey,nativeUpdate,PatchConstructor,d=>{if(RequireDestination(d,destinationLevelKey)!=nativeUpdate)throw new InvalidDataException("Particle destination manager changed after capture.");});
 }
 internal static bool HasNativeOwner(UniversalOverlay overlay)=>
  Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))=="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc" &&
  overlay.Update==0x8007B510 && overlay.Init==0x8008590C && overlay.CopyBuffer==0x80089820 &&
  overlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==Spawn && overlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==Update;
 internal static uint RequireDestination(UniversalOverlay destination,string destinationLevelKey="peacekeepers")
 {
  if(destinationLevelKey=="darkhollow"){UniversalHauntedWizardDarkHollowParticles.Require(destination);return UniversalHauntedWizardDarkHollowParticles.Update;}
  if(destinationLevelKey!="peacekeepers")throw new InvalidDataException("This wizard needs an issued native resurrection effect owner for this level.");
  // Start Blank's separately authenticated Town engine retains the complete
  // native table and update callback. Type29 is the exact same no-op loop tail.
  if(destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==0x80087E20 &&
   Convert.ToHexStringLower(SHA256.HashData(destination.Bytes))=="9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" &&
   destination.Word(0x8007AC80+29*4)==0x80088F20 && destination.Word(0x80088F20)==0x26100020)
   return 0x80087E20;
  RequirePeaceDestination(destination);return 0x8008AE28;
 }
 internal static void RequirePeaceDestination(UniversalOverlay destination)
 {
  // The full original manager, table and loop tail are owned. Prepared/cleared
  // hosts may retain that native callback, never substitute an unknown manager.
  const uint pc=0x8008AE28;
  if(destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=pc || !destination.Contains(pc) ||
   Convert.ToHexStringLower(SHA256.HashData(destination.Bytes.Slice((int)(pc-UniversalOverlay.Base),4552)))!="4222aa7ae72bafa344b89844ba31b817b9e1917ed1636d957ef17fd76b015b9b" ||
   destination.Word(0x8007B5CC+29*4)!=0x8008BFA4 || destination.Word(0x8008BFA4)!=0x26100020)
   throw new InvalidDataException("Resurrection29 needs the original Peace particle manager with its native no-op29 tail.");
 }
 internal static void RequireNative(byte[] exe)
 {
  UniversalHauntedWizardPlacement.RequireNative(exe);
  foreach(var owner in Native)
   if(owner.Pc<UniversalExeBindings.ExeBase || (ulong)owner.Pc-UniversalExeBindings.ExeBase+(uint)owner.Length>(uint)exe.Length || Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan((int)(owner.Pc-UniversalExeBindings.ExeBase),owner.Length)))!=owner.Sha)
    throw new InvalidDataException("Haunted resurrection lost a native allocator, vector, RNG, renderer or texture-loader owner.");
 }
 private static readonly (uint Pc,int Length,string Sha)[] Native=[
  (0x800169AC,5288,"cd7816e798e34cc9b4a015bca681d845f526823c50ca29e2db94cf8b1ced0426"),
  (0x80053570,308,"29bfbf3ce7c1b33a88e8e75a7b23d6c47ff63fa8b70fa405dc04c456c2365d69"),
  (0x800573C8,6160,"87c64709fc57f9f1153f925a758d65464f12fee3a777288af1b06bbe41042d7a"),
  (0x8006272C,48,"8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
  (0x800131BC,48,"26d3b05d50cd1f302f5a3690c1d1e38719e35c8a46e04b4f1327e78953f077b0"),
  (0x8006CBF8,512,"877872b39150488ce0baad5491d04ec492f0a7d4ba45248244c46a0ea548a82b"),
];
 internal static void RequireDestinationTexture(UniversalLevelData source,UniversalLevelData destination)
 {
  var src=UniversalParticleDependencies.Records(source);var dst=UniversalParticleDependencies.Records(destination);
  if(!src.TryGetValue(29,out var a)||a.Length!=8||!dst.TryGetValue(29,out var b)||b.Length!=8||!UniversalParticleDependencies.EquivalentTexture(a,source.Vram(),b,destination.Vram()))
   throw new InvalidDataException("Resurrection29 needs its original first texture tile, pixels and palette after relocation.");
 }
 internal static void RequirePhysicalWizardRow(int physicalRow)
 {
  if(physicalRow<0 || physicalRow>255)throw new InvalidDataException("This wizard needs a placed actor slot below 256 for its native resurrection particles.");
 }
 internal static void PatchConstructor(UniversalAsmPart copiedWizard,UniversalOverlay source,string label)
 {
  const uint site=0x80081FFC;var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0xCC],"proof").Part;
  UniversalHauntedWizardPlacement.RequireCopiedPlacementInputs(copiedWizard,source,[0xCC]);
  // Bind the complete model-origin, constanttype29, callback and live wizard a3 packet.
  for(uint pc=0x80081FDC;pc<=0x80082000;pc+=4){var es=expected.Items.Single(x=>x.Orig==pc);var actual=copiedWizard.Items.Where(x=>x.Orig==pc).ToArray();if(actual.Length!=1 || actual[0].Word!=es.Word || actual[0].Fix!=es.Fix)throw new InvalidDataException("Wizard resurrection constructor arguments/flow changed.");}
  int at=copiedWizard.Items.FindIndex(x=>x.Orig==site);
  copiedWizard.Items[at]=copiedWizard.Items[at] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
 }
}
