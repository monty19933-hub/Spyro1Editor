using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;
/// <summary>Source ordinals only. Group metadata grants no world import permission.</summary>
internal static class UniversalHighCavesWorldRows
{
 internal static readonly int[] Members=[5,6,15,20];
 internal static readonly int[] Classes=[0x12D,0x12F,0x12E,0x148];
 internal static bool Applies(UniversalRowTemplate t)=>t.SourceLevelKey=="highcaves"&&Members.Contains(t.SourceRowIndex);
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("High Caves moving terrain group: "+why);}
 internal static UniversalRowTemplate Capture(UniversalLevelData source,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;UniversalHighCavesTornadoPlacement.RequireSource(source,overlay);var b=source.CaptureRow("highcaves",t.SourceRowIndex);int slot=Array.IndexOf(Members,t.SourceRowIndex);
  Require(t.ClassId==Classes[slot]&&t.Row.SequenceEqual(b.Row)&&t.Properties.SequenceEqual(b.Properties)&&t.InternalPointers.SequenceEqual(b.InternalPointers)&&t.ExternalRowPointers.SequenceEqual(b.ExternalRowPointers)&&t.DroppedExternalPointers==0,"exact initial original source row/path/cache/target");
  UniversalSourceRowIndex[] links=t.SourceRowIndex switch{5=>[new(0,15,0x12E)],6=>[new(16,5,0x12D)],15=>[new(12,20,0x148)],20=>[],_=>throw new InvalidDataException()};
  return t with{SourceRowIndices=links,ImportedRowIndices=[],SourcePrerequisiteRows=Members.Where(r=>r!=t.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,Classes[Array.IndexOf(Members,r)])).ToArray()};
 }
 internal static void RequirePrerequisites(UniversalRowTemplate t,IReadOnlyList<UniversalRowTemplate> rows)
 {
  if(!Applies(t))return;int slot=Array.IndexOf(Members,t.SourceRowIndex);Require(t.ClassId==Classes[slot],"original class owner");UniversalSourceRowIndex[] expected=t.SourceRowIndex switch{5=>[new(0,15,0x12E)],6=>[new(16,5,0x12D)],15=>[new(12,20,0x148)],_=>[]};Require(t.SourceRowIndices.SequenceEqual(expected),"source target enrollment");Require(t.SourcePrerequisiteRows.SequenceEqual(Members.Where(r=>r!=t.SourceRowIndex).Select(r=>new UniversalSourcePrerequisite(r,Classes[Array.IndexOf(Members,r)]))),"all four prerequisite owners");foreach(int r in Members){var peers=rows.Where(x=>Applies(x)&&x.SourceRowIndex==r&&(x.LinkGroupId??"")==(t.LinkGroupId??"")).ToArray();Require(peers.Length==1&&peers[0].ClassId==Classes[Array.IndexOf(Members,r)],"unique four-row linked group");}
 }
 internal static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(key!="highcaves"||removed.Count==0)return;UniversalHighCavesTornadoPlacement.RequireSource(original,overlay);if(Members.Any(removed.Contains))Require(Members.All(removed.Contains),"remove the entire moving terrain actor group together");foreach(int r in Members.Where(r=>!removed.Contains(r))){var b=original.CaptureRow(key,r);var now=current.CaptureRow(key,r);int offset=r switch{5=>0,6=>16,15=>12,_=>-1};Require(now.ClassId==b.ClassId&&now.Properties.Length==b.Properties.Length&&(offset<0||BinaryPrimitives.ReadInt32LittleEndian(now.Properties.AsSpan(offset))==BinaryPrimitives.ReadInt32LittleEndian(b.Properties.AsSpan(offset))),"surviving native world actor changed or hid its target");}
 }
}
