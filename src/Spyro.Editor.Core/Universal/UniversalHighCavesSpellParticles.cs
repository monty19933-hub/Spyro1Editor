using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Count1/type5 spell puff, with native equivalent handlers or a scoped original copy.
/// This grants no particle1/12 authority and no gameplay acceptance.</summary>
public sealed class UniversalHighCavesSpellParticleCertificate
{
 internal UniversalHighCavesSpellParticleCertificate(string key,UniversalOverlay source,UniversalOverlay destination,UniversalCollectionStarCertificate continuation)
 {DestinationLevelKey=key;Source=new(source.Bytes.ToArray(),source.Bindings.ToDictionary());Destination=new(destination.Bytes.ToArray(),destination.Bindings.ToDictionary());NativeContinuation=continuation;CopiesEffect=UniversalHighCavesSpellParticles.RequiresCopy(destination);}
 public string SourceLevelKey=>"highcaves";
 public string DestinationLevelKey{get;}
 public int TypeId=>5;
 public bool CopiesEffect{get;}
 internal UniversalOverlay Source{get;}
 internal UniversalOverlay Destination{get;}
 internal UniversalCollectionStarCertificate NativeContinuation{get;}
 internal UniversalOwnedParticleCertificate? OwnedDispatcher=>CopiesEffect?new(new("highcaves",Source,UniversalHighCavesSpellParticles.Spawn5,UniversalHighCavesSpellParticles.Update5),DestinationLevelKey,Destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,UniversalHighCavesSpellParticles.Patch,
  d=>UniversalHighCavesSpellParticles.RequireDestination(DestinationLevelKey,d,null)):null;
}

public static class UniversalHighCavesSpellParticles
{
 internal static readonly UniversalOwnedParticleFunction Spawn5=new(0x8008B0B0,5772,"0a63c7f07cd608d221aad8716353cf6044cef39e172b107ee4887ad6c842da42",0x8007B4A4,0x8008C6E4,[5]);
 internal static readonly UniversalOwnedParticleFunction Update5=new(0x80089454,7260,"5acbe0b27fbf6788274973e5a0b9982dc25d26bc500dd9e44068bdff71b455db",0x8007B360,0x8008B060,[5]);
 public static UniversalHighCavesSpellParticleCertificate Capture(string sourceKey,string destinationKey,int destinationLevelId,UniversalLevelData sourceData,UniversalOverlay source,UniversalLevelData destinationData,UniversalOverlay destination,byte[] exe)
 {
  if(sourceKey!="highcaves")throw new InvalidDataException("High Caves spell particle source was mislabeled.");UniversalHighCavesTornadoPlacement.RequireSource(sourceData,source);RequireSource(source);RequireDestination(destinationKey,destination,exe);
  // The existing issued continuation validator binds the complete initializer,
  // generated dispatcher bytes and native manager exactly once. It does not add
  // arbitrary type12 callers or alter the destination's collection effect.
  var continuation=UniversalCollectionStarParticles.Capture(sourceKey,sourceData,source,destinationData,destination,exe,destinationLevelId);
  return new(destinationKey,source,destination,continuation);
 }
 internal static bool RequiresCopy(UniversalOverlay d)=>UniversalDarkPassageParticleHosts.Resolve(d).Key is not("highcaves" or "alpineridge" or "wizardpeak");
 internal static void RequireDestination(string key,UniversalOverlay d,byte[]?exe)
 {
  var profile=UniversalDarkPassageParticleHosts.Require(key,d,exe);if(exe!=null)UniversalDarkPassageProjectileParticles.RequireNative(exe);
  // Complete function/table owners from the existing35 issuer authenticate these
  // leaves. Source execution separately proves equivalent5 in the three native
  // hosts and exact no-op5 in every other original dispatcher, including Town.
  if(RequiresCopy(d)){
   var tails=NoopTails[profile.Key];
   if(d.Word(profile.SpawnTable+20)!=tails.Spawn || d.Word(profile.UpdateTable+20)!=tails.Update)throw new InvalidDataException("The High Caves spell host no longer has its native no-op type5 tails.");
  }
 }
 private static readonly IReadOnlyDictionary<string,(uint Spawn,uint Update)> NoopTails=new Dictionary<string,(uint,uint)>
 {
  ["artisans"]=(0x800885C8,0x80087390),
  ["stonehill"]=(0x8008A360,0x80089274),
  ["darkhollow"]=(0x8008553C,0x80084450),
  ["townsquare"]=(0x80089E74,0x80088F20),
  ["toasty"]=(0x800849B8,0x800838CC),
  ["sunnyflight"]=(0x80084E78,0x800840D8),
  ["peacekeepers"]=(0x8008CF4C,0x8008BFA4),
  ["drycanyon"]=(0x8008D5A8,0x8008C4F0),
  ["clifftown"]=(0x8008BAA0,0x8008A958),
  ["icecavern"]=(0x80086208,0x80085138),
  ["doctorshemp"]=(0x800871B8,0x800860CC),
  ["nightflight"]=(0x80083B98,0x80082F10),
  ["magiccrafters"]=(0x8008E5B0,0x8008D284),
  ["blowhard"]=(0x80087444,0x80085EF0),
  ["crystalflight"]=(0x80084338,0x800836B0),
  ["beastmakers"]=(0x8008AB18,0x80089A68),
  ["terracevillage"]=(0x800878EC,0x80086688),
  ["mistybog"]=(0x800870D8,0x80086184),
  ["treetops"]=(0x800897F0,0x8008864C),
  ["metalhead"]=(0x8008A644,0x80089460),
  ["wildflight"]=(0x800847EC,0x80083B64),
  ["dreamweavers"]=(0x8008BAE0,0x8008A9D4),
  ["darkpassage"]=(0x8008807C,0x80086CE8),
  ["loftycastle"]=(0x800876C4,0x800863E8),
  ["hauntedtowers"]=(0x800897C8,0x80088188),
  ["jacques"]=(0x800862F0,0x80085204),
  ["icyflight"]=(0x800848DC,0x80083B3C),
  ["gnastysworld"]=(0x80085C88,0x80084E58),
  ["gnorccove"]=(0x80088610,0x8008742C),
  ["twilightharbor"]=(0x80085FAC,0x80084E5C),
  ["gnastygnorc"]=(0x8008545C,0x800845E4),
  ["gnastysloot"]=(0x8008620C,0x800851E0),
 };
 private static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 internal static void RequireSource(UniversalOverlay source)
 {
  if(Hash(source.Bytes)!=UniversalHighCavesTornadoPlacement.OverlaySha||source.Update!=0x8007B64C||source.Init!=0x8008883C||source.CopyBuffer!=0x8008C73C)throw new InvalidDataException("High Caves spell particle source owner changed.");
  byte[] bb=new byte[source.Bindings.Count*16];int at=0;foreach(var b in source.Bindings.OrderBy(b=>b.Key))foreach(uint n in new[]{b.Value.Global,b.Value.LuiAddress,b.Value.AddiuAddress,b.Value.Value}){System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bb.AsSpan(at),n);at+=4;}
  if(source.Bindings.Any(b=>b.Key!=b.Value.Global)||Hash(bb)!="0719106e3771779b366e785551f692c4e998e1aae61ec853aaca13ab3a7908eb"||source.Word(Spawn5.Table+20)!=0x8008B340||source.Word(Update5.Table+20)!=0x80089AF4||source.Word(Spawn5.Table+52)!=Spawn5.Default||source.Word(Update5.Table+52)!=Update5.Default)throw new InvalidDataException("High Caves particle5 callbacks, source leaves or no-op tail changed.");
 }
 internal static void RequireBundle(string key,UniversalOverlay source,IReadOnlyList<int>classes,UniversalOverlay destination,byte[]exe,IReadOnlyDictionary<string,UniversalHighCavesSpellParticleCertificate>?proofs,IReadOnlyList<UniversalOwnedParticleCertificate>?owned)
 {
  if(!classes.Contains(0x127))return;
  bool sourceOwner=Hash(source.Bytes)==UniversalHighCavesTornadoPlacement.OverlaySha||source.Bindings.TryGetValue(UniversalExeBindings.ParticleSpawnGlobal,out var spawn)&&spawn.Value==Spawn5.Entry;
  if(key!="highcaves"){if(sourceOwner)throw new InvalidDataException("High Caves spell particle source was mislabeled.");return;}
  RequireSource(source);if(proofs==null||!proofs.TryGetValue(key,out var proof))throw new InvalidDataException("This High Caves creature needs its original spell effect before it can be placed here.");
  RequireDestination(proof.DestinationLevelKey,destination,exe);
  if(!source.Bytes.SequenceEqual(proof.Source.Bytes)||source.Bindings.Count!=proof.Source.Bindings.Count||source.Bindings.Any(b=>!proof.Source.Bindings.TryGetValue(b.Key,out var v)||v!=b.Value)||RequiresCopy(destination)!=proof.CopiesEffect)throw new InvalidDataException("High Caves spell particle certificate belongs to another source or host.");
  var copies=owned?.Where(p=>p.TypeIds.Contains(5)).ToArray()??[];
  if(proof.CopiesEffect?copies.Length!=1||copies[0].SourceLevelKey!="highcaves"||!copies[0].TypeIds.SequenceEqual(new[]{5}):copies.Length!=0)throw new InvalidDataException("High Caves particle5 needs one owned updater, never a duplicate native or copied lifetime pass.");
 }
 internal static UniversalParticleDependency Inspect(UniversalHighCavesSpellParticleCertificate p)=>new("highcaves",5,p.CopiesEffect?"source-certified-copied":"source-certified-native-equivalent","Original count1/type5/position/NULL call; pooled FT4 quad, RNG drift and48-tick/rendered lifetime, sampled texture0 and full palette; native manager runs once. Native gameplay/visual acceptance remains separate.");
 internal static void RequireTextures(UniversalHighCavesSpellParticleCertificate proof,UniversalLevelData source,UniversalLevelData destination)
 {
  UniversalHighCavesTornadoPlacement.RequireSource(source,proof.Source);var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);
  if(!a.TryGetValue(5,out var x)||x.Length!=8||!b.TryGetValue(5,out var y)||y.Length!=8||!UniversalParticleDependencies.EquivalentTexture(x,source.Vram(),y,destination.Vram()))throw new InvalidDataException("High Caves particle5 lost its first texture tile, sampled pixels or complete palette.");
 }
 internal static void RequireCopiedCalls(UniversalAsmPart part,UniversalOverlay source)
 {
  RequireSource(source);var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x127],"spellproof").Part;const uint first=0x800845A0,last=0x800845C0;
  var wanted=expected.Items.Where(i=>i.Orig>=first&&i.Orig<=last).ToArray();var starts=part.Items.Select((i,n)=>(i,n)).Where(x=>x.i.Orig==first).Select(x=>x.n).ToArray();
  if(starts.Length==0)throw new InvalidDataException("Copied High Caves spell lost its count/type/position/NULL source packet.");
  foreach(int start in starts)if(!part.Items.Skip(start).Where(i=>i.Word!=null).Take(wanted.Length).SequenceEqual(wanted))throw new InvalidDataException("Copied High Caves spell changed arguments, ordering, delay slot or flow.");
  foreach(var w in wanted)if(part.Items.Count(i=>i.Orig==w.Orig)!=starts.Length)throw new InvalidDataException("Copied High Caves spell has a partial duplicated packet.");
  foreach(var i in part.Items.Where(i=>(i.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump)&&i.Fix.Orig>=first&&i.Fix.Orig<=last))if(!expected.Items.Any(e=>e.Orig==i.Orig&&e.Word==i.Word&&e.Fix==i.Fix))throw new InvalidDataException("Copied High Caves spell gained an incoming interior entry.");
  foreach(var t in part.Tables.Values)foreach(uint target in t.Where(t=>t>=first&&t<=last))if(!expected.Tables.Values.Any(t=>t.Contains(target)))throw new InvalidDataException("Copied High Caves spell gained an interior table entry.");
  HashSet<string> labels=[];uint? next=null;for(int n=part.Items.Count-1;n>=0;n--){var i=part.Items[n];if(i.Word!=null)next=i.Orig;if(i.Label!=null&&next>=first&&next<=last)labels.Add(i.Label);}
  if(part.Items.Any(i=>(i.Fix.Kind is UniversalFixKind.BranchLabel or UniversalFixKind.JumpLabel or UniversalFixKind.JalLabel)&&i.Fix.Label!=null&&labels.Contains(i.Fix.Label)))throw new InvalidDataException("Copied High Caves spell gained an incoming interior label.");
 }
 internal static void Patch(UniversalAsmPart part,UniversalOverlay source,string label)
 {RequireCopiedCalls(part,source);foreach(int at in part.Items.Select((i,n)=>(i,n)).Where(x=>x.i.Orig==0x800845B8).Select(x=>x.n).ToArray())part.Items[at]=part.Items[at] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};}
 internal static void RequireFinal(UniversalHighCavesSpellParticleCertificate proof,UniversalLevelData source,UniversalLevelData final,UniversalOverlay finalOverlay,byte[]exe,UniversalCollectionStarContinuation?continuation)
 {
  RequireTextures(proof,source,final);RequireDestination(proof.DestinationLevelKey,new(finalOverlay.Bytes.ToArray(),proof.Destination.Bindings),null);UniversalDarkPassageProjectileParticles.RequireNative(exe);
  if(proof.CopiesEffect&&continuation==null)throw new InvalidDataException("High Caves spell lost its issued copied-particle continuation.");
  UniversalCollectionStarParticles.RequireFinalDestination(proof.NativeContinuation,final,finalOverlay,exe,continuation);
 }
}
