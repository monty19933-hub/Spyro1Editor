using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Original source ordinals only; does not grant world import.</summary>
internal static class UniversalGreenDruidWorldRows
{
 internal static readonly int[] Members=[5,48];internal static readonly int[] Classes=[0x10E,0x148];
 internal static bool OwnsCopiedActor(string key,UniversalOverlay overlay,IReadOnlyList<int> classes)
 {
  if(!classes.Contains(0x10E))return false;
  return key.Equals("alpineridge",StringComparison.OrdinalIgnoreCase)||Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))==UniversalGreenDruidWorldPrerequisites.OverlaySha||overlay.Update==0x8007BB00&&overlay.Init==0x8008A36C||overlay.Bytes.Length>=0x80083C60-UniversalOverlay.Base&&overlay.Word(0x80083A98)==0x0060F809&&overlay.Word(0x80083C0C)==0x2402001F;
 }
 internal static bool Applies(UniversalRowTemplate t)=>t.SourceLevelKey=="alpineridge"&&Members.Contains(t.SourceRowIndex);
 internal static void RequireSource(UniversalLevelData data,UniversalOverlay overlay)
 {
  Require(Convert.ToHexStringLower(SHA256.HashData(data.Bytes))==UniversalGreenDruidWorldPrerequisites.DataSha&&Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes))==UniversalGreenDruidWorldPrerequisites.OverlaySha,"complete original source owners");
  byte[] bindings=new byte[overlay.Bindings.Count*16];int cursor=0;foreach(var b in overlay.Bindings.OrderBy(b=>b.Key))foreach(uint v in new[]{b.Value.Global,b.Value.LuiAddress,b.Value.AddiuAddress,b.Value.Value}){BinaryPrimitives.WriteUInt32LittleEndian(bindings.AsSpan(cursor),v);cursor+=4;}Require(overlay.Bindings.All(b=>b.Key==b.Value.Global)&&Convert.ToHexStringLower(SHA256.HashData(bindings))=="461f8f271b5653a1f4268d41cd3aadba45d27dc24cf06013965da839c2212850","all original callback addresses and load sites");
 }
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("Green Druid linked terrain: "+why);}
 internal static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;RequireSource(data,overlay);var original=data.CaptureRow(t.SourceLevelKey,t.SourceRowIndex);Require(t.ClassId==Classes[Array.IndexOf(Members,t.SourceRowIndex)]&&t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&t.DroppedExternalPointers==0,"original source state/path/input row");
  return t with{SourceRowIndices=t.SourceRowIndex==5?[new(8,48,0x148)]:[],SourcePrerequisiteRows=Members.Where(r=>r!=t.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,Classes[Array.IndexOf(Members,r)])).ToArray()};
 }
 internal static void RequirePrerequisites(UniversalRowTemplate t,IReadOnlyList<UniversalRowTemplate> rows)
 {
  if(t.GreenDruidNativeReferences!=null){t.GreenDruidNativeReferences.Require(t);return;}
  Require(Applies(t),"source enrollment");Require(t.SourceRowIndices.SequenceEqual(t.SourceRowIndex==5?new UniversalSourceRowIndex[]{new(8,48,0x148)}:[]),"original target enrollment");Require(t.SourcePrerequisiteRows.SequenceEqual(Members.Where(r=>r!=t.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,Classes[Array.IndexOf(Members,r)]))),"both prerequisite owners");foreach(int row in Members){var found=rows.Where(x=>Applies(x)&&x.SourceRowIndex==row&&(x.LinkGroupId??"")==(t.LinkGroupId??"")).ToArray();Require(found.Length==1&&found[0].ClassId==Classes[Array.IndexOf(Members,row)],"unique same-instance original group");}
 }
 internal static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(key!="alpineridge"||removed.Count==0)return;RequireSource(original,overlay);if(Members.Any(removed.Contains))Require(Members.All(removed.Contains),"remove the complete actor/terrain-target pair together");foreach(int row in Members.Where(r=>!removed.Contains(r))){var before=original.CaptureRow(key,row);var now=current.CaptureRow(key,row);Require(now.ClassId==before.ClassId&&now.Properties.Length==before.Properties.Length&&(row!=5||now.Properties.AsSpan(4,12).SequenceEqual(before.Properties.AsSpan(4,12))),"remaining actor cannot hide/retarget its group or target before slot reuse");}
 }
}
