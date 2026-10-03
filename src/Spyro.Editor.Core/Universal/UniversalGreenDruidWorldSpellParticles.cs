using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Alpine original live model-vertex spell8, complete constructor/update
/// and physical class148 target. This does not own a terrain bank on its own.</summary>
internal static class UniversalGreenDruidWorldSpellParticles
{
 internal static readonly UniversalOwnedParticleFunction Spawn8=new(0x8008C9D8,5352,"43bd6a02a7ec0cd376bff48bdccac1787efdcec26974cf723dd7e269bede1bbb",0x8007B998,0x8008DE68,[8]);
 internal static readonly UniversalOwnedParticleFunction Update8=new(0x8008AF54,6788,"4e56bf96819f2f5aa5aae7338ba8487250c1edbc7d48c1d5858ea7c8a7f0a639",0x8007B854,0x8008C988,[8]);
 internal static UniversalOwnedParticleProfile CaptureSource(UniversalLevelData data,UniversalOverlay source,byte[]exe)
 {
  UniversalGreenDruidWorldRows.RequireSource(data,source);if(Convert.ToHexStringLower(SHA256.HashData(exe))!=UniversalGreenDruidWorldPrerequisites.ExeSha||source.Word(Spawn8.Table+32)!=0x8008CD2C||source.Word(Update8.Table+32)!=0x8008B694)throw new InvalidDataException("Green Druid spell lost its full native/source dispatch owner.");return new("alpineridge",new(source.Bytes.ToArray(),source.Bindings.ToDictionary()),Spawn8,Update8);
 }
 internal static void PatchCalls(UniversalAsmPart part,UniversalOverlay source,string label,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map)
 {
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!=UniversalGreenDruidWorldPrerequisites.OverlaySha||!classes.SequenceEqual(new[]{0x10E,0x148})||map.Count!=2||!classes.All(map.ContainsKey))throw new InvalidDataException("Green Druid copied actor namespace owner changed.");
  var original=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing Green Druid entry."),map).Part;
  if(!part.Items.SequenceEqual(original.Items)||part.Tables.Count!=original.Tables.Count||original.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var rows)||!rows.SequenceEqual(t.Value)))throw new InvalidDataException("Green Druid complete copied actor flow/targets/entries/operands changed.");
  uint[] packet=[0x8E020008,0x3C038007,0x8C6358E4,0x00023840,0x00E23821,0x00073880,0x00E23823,0x3C028007,0x8C425828,0x000738C0,0x0060F809,0x00473821];
  for(int n=0;n<packet.Length;n++){uint pc=0x80083A70+(uint)n*4;if(source.Word(pc)!=packet[n])throw new InvalidDataException("Green Druid spell native physical-target packet changed.");int at=part.Items.FindIndex(i=>i.Orig==pc);if(at<0||part.Items.Count(i=>i.Orig==pc)!=1||part.Items[at].Word!=packet[n])throw new InvalidDataException("Green Druid spell emitted target packet changed.");if(n>0&&at!=part.Items.FindIndex(i=>i.Orig==pc-4)+1)throw new InvalidDataException("Green Druid spell target packet gained an inserted/reordered consumer.");}
  int call=part.Items.FindIndex(i=>i.Orig==0x80083A98);part.Items[call]=part.Items[call] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
 }
 internal static void RequireTexture(UniversalLevelData source,UniversalLevelData destination)
 {
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!=UniversalGreenDruidWorldPrerequisites.DataSha)throw new InvalidDataException("Green Druid original sampled source owner changed.");
  var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);if(!a.TryGetValue(8,out var x)||!b.TryGetValue(8,out var y)||!UniversalParticleDependencies.EquivalentTexture(x,source.Vram(),y,destination.Vram()))throw new InvalidDataException("Green Druid spell needs its exact sampled source texture and palette.");
 }
}
