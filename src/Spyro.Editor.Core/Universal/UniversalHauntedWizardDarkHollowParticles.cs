using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original Dark Hollow's complete particle constructor/update and native
/// table29 loop tails. This is a resurrection29 owner, not a projectile proof.</summary>
internal static class UniversalHauntedWizardDarkHollowParticles
{
 internal const uint Spawn=0x800844A0,Update=0x80083274;
 internal static void Require(UniversalOverlay destination,byte[]? exe=null)
 {
  if(Hash(destination.Bytes)!="9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32"||destination.Bindings.Count!=Bindings.Length||
   Bindings.Any(b=>!destination.Bindings.TryGetValue(b.Global,out var actual)||actual!=b))
   throw new InvalidDataException("This wizard needs Dark Hollow's original particle manager and callback bindings.");
  Owner(Spawn,4340,"3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908");
  Owner(Update,4652,"42568934fe492f56cb58c8d22e803ccfb605b994789d917c86ae8a245b074af8");
  Owner(0x8007ACB8,324,"3d7229fa46f8f21d4d3dd07f28fb18cbfa0872c445aa24ae571eb1975243c4c6");
  Owner(0x8007AB74,324,"138aafd4977dd3327e9283992d2082469c5d064b08b373848f2101d53cce3997");
  if(destination.Word(0x8007ACB8+29*4)!=0x8008553C||destination.Word(0x8007AB74+29*4)!=0x80084450||
   destination.Word(0x8008553C)!=0x8FA80088||destination.Word(0x80084450)!=0x26310020)
   throw new InvalidDataException("Dark Hollow's native resurrection29 entries must remain their original no-op loop tails.");
  if(exe!=null){var bindings=UniversalExeBindings.Read(exe,12);if(Bindings.Any(b=>!bindings.TryGetValue(b.Global,out var actual)||actual!=b)||exe.Length!=0x66000||Hash(exe.AsSpan(checked((int)(0x8005A848-UniversalExeBindings.ExeBase)),80))!="bb0bcedcf3f8862724eada056a33adb9b2aeed82756bd99bedcce30f62ae7e53")throw new InvalidDataException("This wizard needs Dark Hollow's original particle callback initializer.");}
  void Owner(uint pc,int length,string sha){if(!destination.Contains(pc)||(ulong)pc+(uint)length>destination.End||Hash(destination.Bytes.Slice(checked((int)(pc-UniversalOverlay.Base)),length))!=sha)throw new InvalidDataException("Dark Hollow's resurrection29 function or dispatch table owner changed.");}
 }
 private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static readonly UniversalExeBinding[] Bindings=[
  new(0x800785D8,0x8005A848,0x8005A84C,0x80085594),
  new(0x800758CC,0x8005A858,0x8005A85C,0x80082960),
  new(0x80075734,0x8005A868,0x8005A86C,0x8007AE40),
  new(0x800758E4,0x8005A878,0x8005A87C,Spawn),
  new(0x800756BC,0x8005A888,0x8005A88C,Update)];
}
