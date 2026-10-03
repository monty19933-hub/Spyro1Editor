using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original89 scans native active actor pointers and compares each physical class
/// against source literal193 or its source property4C. The class is comparison-only;
/// position, target damage and native model accesses keep destination actor identities.
/// This proves namespace handling, not world-path translation or gameplay acceptance.</summary>
internal static class UniversalBeastMistyBoarClassOwnership
{
 private sealed record Profile(string SourceKey,string OverlaySha,uint Read,IReadOnlyList<UniversalExeBinding> Bindings);
 private static readonly Profile[] Profiles=[
  new("beastmakers","6bed996a20df44803ec4eefe434294082c3f5814ce56080e0594bab95eeb287f",0x80081D84,[
   new(0x800785D8,0x8005AFB8,0x8005AFBC,0x8008AB70),
   new(0x800758CC,0x8005AFC8,0x8005AFCC,0x80087EF0),
   new(0x80075734,0x8005AFD8,0x8005AFDC,0x8007E18C),
   new(0x800758E4,0x8005AFE8,0x8005AFEC,0x80089AB8),
   new(0x800756BC,0x8005AFF8,0x8005AFFC,0x800888F8),
   new(0x800757A0,0x8005B008,0x8005B00C,0x8007B664),
   new(0x800758A8,0x8005B018,0x8005B01C,0x8007B7A0),
   new(0x8007574C,0x8005B028,0x8005B02C,0x8007B7E4),
   new(0x800758D8,0x8005B038,0x8005B03C,0x8007D784)]),
  new("mistybog","3b37eb8b4b0f5fb74e7d263670705e20c807f2d8f1bbf03f34a461eb1c3e2ff6",0x8007F41C,[
   new(0x800785D8,0x8005B0A0,0x8005B0A4,0x80087130),
   new(0x800758CC,0x8005B0B0,0x8005B0B4,0x80084718),
   new(0x80075734,0x8005B0C0,0x8005B0C4,0x8007AFBC),
   new(0x800758E4,0x8005B0D0,0x8005B0D4,0x800861CC),
   new(0x800756BC,0x8005B0E0,0x8005B0E4,0x80085084)])];
 internal static IReadOnlyList<uint> Translate(UniversalAsmPart part,string sourceKey,UniversalOverlay source,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[] exe)
 {
  var owner=Profiles.SingleOrDefault(p=>p.SourceKey==sourceKey);
  if(owner==null)return [];
  if(!classes.Contains(0x89))
  {if(part.Items.Any(i=>(i.SourcePc??i.Orig)==owner.Read))throw new InvalidDataException("Boar class scan has no source89 handler owner.");return [];}
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!=owner.OverlaySha || source.Bindings.Count!=owner.Bindings.Count || owner.Bindings.Any(b=>!source.Bindings.TryGetValue(b.Global,out var actual)||actual!=b))
   throw new InvalidDataException("Boar class scan lost its original overlay or callback identities.");
  const uint nativeStart=0x80051FEC;const int nativeLength=724;
  if(exe.Length<(int)(nativeStart-UniversalExeBindings.ExeBase)+nativeLength || Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan((int)(nativeStart-UniversalExeBindings.ExeBase),nativeLength)))!="70dc8c577dc54c205c8891cae9375af93a242f95ef37ab77bf0318a01e319d96")
   throw new InvalidDataException("Boar class scan lost its native active actor list producer.");
  string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Boar handler has no emitted entry label.");
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,classes,tag,map).Part;
  // The complete source-owned emitted update binds the native list setup, live
  // root actor/props, scan skip gate, both comparisons, loop and every successor.
  // Call only immediately after extraction, before other certified adapters.
  if(!part.Items.SequenceEqual(expected.Items) || part.Tables.Count!=expected.Tables.Count || expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var a)||!a.SequenceEqual(t.Value)))
   throw new InvalidDataException("Boar class scan gained modified operands, delay slots, flow, entries or duplicate code.");
  int at=part.Items.FindIndex(i=>(i.SourcePc??i.Orig)==owner.Read);
  if(at<0 || part.Items[at].Word!=0x86030036 || source.Word(owner.Read+4)!=0x24020193 || source.Word(owner.Read+0x10)!=0x8E22004C)
   throw new InvalidDataException("Boar scan class/constant/property operands changed.");
  // LH v1,[s0+36]; literal193 setup retires its load, BEQ tests v1.
  // Mismatch loads source props4C into v0, then BNE tests v1. On a hit,
  // LW v1,[s0+C] kills the class. On mismatch it reaches the native list
  // step and either exits (no v1 consumer) or the next LH overwrites v1.
  // Thus inverse mapping may use only v1; no target pointer/model index/store
  // receives a translated class. No constant-spawn or object-row admission.
  UniversalClassRemapping.TranslateOwnedClassification(part,at,map,"boar_scan_class");
  return [owner.Read];
 }
}
