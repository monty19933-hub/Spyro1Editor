using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original Haunted temporary fairy82 into intact Dark Hollow, alongside
/// the authenticated wizard/armor group. Other fairy variants retain separate owners.</summary>
internal static class UniversalHauntedSuperflameFairy
{
 internal static bool Applies(UniversalRowTemplate t)=>string.Equals(t.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&t.ClassId==0x8E&&t.SourceRowIndex==82;
 internal static bool Requested(IReadOnlyList<UniversalMobyPlacement> ps)=>ps.Any(p=>!p.Retired&&string.Equals(p.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&p.SourceRowIndex==82);
 internal static void RequirePlacements(string destination,IReadOnlyList<UniversalMobyPlacement> ps,Func<string,(Spyro.Editor.Core.Levels.LevelDefinition Level,UniversalLevelData Data,UniversalOverlay Overlay)> source)
 {
  var fairies=ps.Where(p=>!p.Retired&&string.Equals(p.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&source(p.SourceLevelKey).Data.CaptureRow(p.SourceLevelKey,p.SourceRowIndex).ClassId is 0x8E or 0xF1).ToArray();
  if(string.Equals(destination,"hauntedtowers",StringComparison.OrdinalIgnoreCase)||fairies.Length==0)return;
  if(fairies.Any(p=>p.SourceRowIndex!=82))throw new InvalidDataException("This original fairy variant still needs its own native path and power owner for another level. The complete temporary fairy82 group is available in original Dark Hollow.");
  if(!string.Equals(destination,"darkhollow",StringComparison.OrdinalIgnoreCase)||ps.Count(p=>!p.Retired&&string.Equals(p.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&p.SourceRowIndex==82)!=1||
   !ps.Any(p=>!p.Retired&&string.Equals(p.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&p.SourceRowIndex==21)||!ps.Any(p=>!p.Retired&&string.Equals(p.SourceLevelKey,"hauntedtowers",StringComparison.OrdinalIgnoreCase)&&p.SourceRowIndex==1))
   throw new InvalidDataException("This temporary Superflame fairy needs the complete Haunted wizard and armor group in original Dark Hollow.");
 }
 internal static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;Require(data,overlay,t);return t;
 }
 private static void Require(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  UniversalHauntedWizardPlacement.RequireSource(data,overlay);var original=data.CaptureRow("hauntedtowers",82);
  if(!t.Row.SequenceEqual(original.Row)||!t.Properties.SequenceEqual(original.Properties)||!t.InternalPointers.SequenceEqual(original.InternalPointers)||!t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)||t.DroppedExternalPointers!=0||t.ClassId!=0x8E||t.Properties.Length!=132||t.InternalPointers.Count!=1||t.InternalPointers[0]!=(0x28,0x3C)||t.ExternalRowPointers.Count!=0||I(t.Properties,0x20)!=0||I(t.Properties,0x30)!=0||I(t.Properties,0x34)!=-1||I(t.Properties,0x38)!=-1||t.Properties[0x3C]!=4)
   throw new InvalidDataException("This fairy needs its complete original temporary power, path and unlinked actor settings.");
 }
 internal static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t,byte[] row)
 {
  Require(data,overlay,t);if(row.Length!=88)throw new InvalidDataException("Fairy row is truncated.");
  for(int i=4;i<row.Length;i++)if(i is not(>=12 and <24)&&i is not(0x43 or 0x46 or 0x4A)&&row[i]!=t.Row[i])throw new InvalidDataException("This fairy needs its original state, animation, material, collision and duration settings.");
  if(row[0x43]!=255||row[0x4A]!=255)throw new InvalidDataException("The imported fairy needs unrestricted native visibility and activation.");
  var properties=t.Properties.ToArray();for(int axis=0;axis<3;axis++){int delta=checked(I(row,12+4*axis)-I(t.Row,12+4*axis));foreach(int vector in new[]{0,0x44,0x54,0x64,0x74})BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(vector+4*axis),checked(I(properties,vector+4*axis)+delta));}
  return t with{Properties=properties};
 }
 internal static void RequireCopied(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes)
 {
  if(!classes.Contains(0x8E))return;
  if(Convert.ToHexStringLower(SHA256.HashData(source.Bytes))!="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc")throw new InvalidDataException("Fairy copied state owner changed.");
  var owned=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x8E],part.Items.First(i=>i.Label!=null).Label!).Part;
  foreach(var table in owned.Tables)if(!part.Tables.TryGetValue(table.Key,out var actualTable)||!actualTable.SequenceEqual(table.Value))throw new InvalidDataException("Fairy native state table owner changed.");
  var expected=owned.Items.Where(i=>i.Orig>=0x8007FF28&&i.Orig<=0x800809D8).ToArray();var actual=part.Items.Where(i=>i.Orig>=0x8007FF28&&i.Orig<=0x800809D8).ToArray();
  if(expected.Length==0||expected.Length!=actual.Length||!expected.SequenceEqual(actual))throw new InvalidDataException("Fairy native power, camera, path, kiss or lifetime body changed.");
 }
 internal static void RequireNative(byte[] exe)
 {
  UniversalHauntedWizardParticles.RequireNative(exe);UniversalHauntedSuperflameScene.RequireNative(exe);
  if(Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(0x5D3F8,640)))!="795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55")throw new InvalidDataException("Fairy kiss needs the complete original sine/cosine lookup including its native128-byte tail.");
  // Complete native Spyro update owner, not a shortened power selector: duration,
  // collisions, powered flame producers and renderer callers remain native.
  if(exe.Length!=0x66000||Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(0x3939C,0x259C)))!=NativePlayerSha)throw new InvalidDataException("Superflame needs the complete original native Spyro power, combat and render caller owner.");
 }
 internal const string NativePlayerSha="db5df854d246bcf37e06bbb39e31fd7446b48e6a1b739ec263cae9b58e2cf7d2";
 private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
}
