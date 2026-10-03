using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Inert source-issued particle8/caller prerequisite. No Composer hook,
/// world exception, destination row admission, or gameplay acceptance.</summary>
internal static class UniversalHighCavesWorldSpellParticles
{
 internal static readonly UniversalOwnedParticleFunction Spawn8=new(0x8008B0B0,5772,"0a63c7f07cd608d221aad8716353cf6044cef39e172b107ee4887ad6c842da42",0x8007B4A4,0x8008C6E4,[8]);
 internal static readonly UniversalOwnedParticleFunction Update8=new(0x80089454,7260,"5acbe0b27fbf6788274973e5a0b9982dc25d26bc500dd9e44068bdff71b455db",0x8007B360,0x8008B060,[8]);
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static void Require(bool b,string why){if(!b)throw new InvalidDataException("High Caves moving-world spell prerequisite: "+why);}
 internal static UniversalOwnedParticleProfile CaptureSource(string key,UniversalLevelData data,UniversalOverlay source,byte[]exe)
 {
  Require(key=="highcaves","source mislabeled");UniversalHighCavesTornadoPlacement.RequireSource(data,source);UniversalHighCavesSpellParticles.RequireSource(source);
  Require(H(exe)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","complete native model-loader/vertex/RNG/vector/pool/renderer owner");
  Require(source.Word(Spawn8.Table+32)==0x8008B404&&source.Word(Update8.Table+32)==0x80089B9C,"type8 source dispatch");
  return new("highcaves",new(source.Bytes.ToArray(),source.Bindings.ToDictionary()),Spawn8,Update8);
 }
 internal static UniversalOwnedParticleCertificate CaptureDarkHollow(string key,UniversalLevelData data,UniversalOverlay source,UniversalOverlay destination,byte[]exe)
 {
  var profile=CaptureSource(key,data,source,exe);RequireDarkHollow(destination);
  return new(profile,"darkhollow",0x80083274,(part,owner,label)=>throw new InvalidDataException("Full High Caves world placement/caller ownership must precede dispatch installation."),RequireDarkHollow);
 }
 private static void RequireDarkHollow(UniversalOverlay destination)
 {
  var profile=UniversalDarkPassageParticleHosts.Require("darkhollow",destination,null);
  Require(destination.Word(profile.SpawnTable+32)==0x8008553C&&destination.Word(profile.UpdateTable+32)==0x80084450,"original type8 no-op manager");
 }
 internal static void RequireTexture(UniversalLevelData source,UniversalLevelData destination)
 {
  Require(H(source.Bytes)==UniversalHighCavesTornadoPlacement.DataSha,"complete original source texture owner");var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);
  Require(a.TryGetValue(8,out var x)&&b.TryGetValue(8,out var y)&&UniversalParticleDependencies.EquivalentTexture(x,source.Vram(),y,destination.Vram()),"complete sampled texture0/palette owner");
 }
 // Particle payload accepts0, but source-positive controller indices do not:
 //the preserved state0/state4 BEQZ treats physical0 as inactive.
 internal static void RequireControllerTargetRow(int physical)=>Require(physical is >0 and <=255,"source-positive controller target must preserve its zero-inactive branches");
 internal static void RequireTargetRow(int physical)=>Require(physical is >=0 and <=255,"native physical target byte capacity");
 /// <summary>Validates the entire emitted source extraction before owning the current
 /// allocated class read and replacing four exact original ABI calls. Remains inert.</summary>
 internal static IReadOnlyList<uint> MarkAndPatchActorPackets(UniversalAsmPart part,string key,UniversalLevelData data,UniversalOverlay source,IReadOnlyList<int>classes,IReadOnlyDictionary<int,int>map,byte[]exe,string label)
 {
  _=CaptureSource(key,data,source,exe);Require(classes.Contains(0x12E)&&map.TryGetValue(0x12E,out var dest)&&dest is >0 and <512,"actual controller class map");
  string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing controller entry");var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
  Require(part.Items.SequenceEqual(expected.Items)&&part.Tables.Count==expected.Tables.Count&&expected.Tables.All(t=>part.Tables.TryGetValue(t.Key,out var b)&&b.SequenceEqual(t.Value)),"complete copied flow/operands/delays/targets/tables");
  //85304..14 establishes the current allocated class and gModels, then8757C
  //writes state1 and consumes its animation1 framecount before killing V0.
  Require(source.Word(0x80085304)==0x86620036&&source.Word(0x80085308)==0x3C0A8007&&source.Word(0x8008530C)==0x254A6378&&source.Word(0x80085310)==0x08021D5F&&source.Word(0x80085314)==0xAE600018&&source.Word(0x80087584)==0x00021080&&source.Word(0x80087588)==0x004A1021&&source.Word(0x8008758C)==0x8C420000&&source.Word(0x80087594)==0x8C42003C&&source.Word(0x8008759C)==0x9042000C,"native current-model animation1 consumer");
  int read=part.Items.FindIndex(i=>i.Orig==0x80085304);Require(read>=0&&part.Items.Count(i=>i.Orig==0x80085304)==1,"one exact current-class read");
  uint[]calls=[0x8008503C,0x80085168,0x80085210,0x800852D8];foreach(uint pc in calls)Require(part.Items.Count(i=>i.Orig==pc)==1&&source.Word(pc)==0x0060F809,"all four native count1/type8/liveactor/physicaltarget calls");
  part.Items[read]=part.Items[read] with{ClassReadOwned=true};foreach(uint pc in calls){int at=part.Items.FindIndex(i=>i.Orig==pc);part.Items[at]=part.Items[at] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};}
  return calls;
 }
}
