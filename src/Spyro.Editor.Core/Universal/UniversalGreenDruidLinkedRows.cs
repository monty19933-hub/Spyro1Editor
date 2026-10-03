using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original11/44/53 source identities for the ordinary linked-object
/// chooser. Enrollment never grants foreign world or behavior ownership.</summary>
internal static class UniversalGreenDruidLinkedRows
{
 internal static readonly int[] Members=[11,44,53];
 private static readonly int[] Classes=[0x10E,0x146,0x148];
 internal static bool Applies(UniversalRowTemplate t)=>t.SourceLevelKey=="alpineridge"&&Members.Contains(t.SourceRowIndex);
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("Linked Druid group: "+why);}
 private static UniversalSourceRowIndex[] Links(int row)=>row switch{11=>[new(8,53,0x148),new(12,44,0x146)],44=>[new(8,11,0x10E)],53=>[],_=>throw new InvalidDataException("Unissued linked Druid row")};
 private static UniversalSourcePrerequisite[] Prerequisites(int row)=>Members.Where(r=>r!=row).Select(r=>new UniversalSourcePrerequisite(r,Classes[Array.IndexOf(Members,r)])).ToArray();
 internal static UniversalRowTemplate Capture(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t)
 {
  if(!Applies(t))return t;UniversalGreenDruidWorldRows.RequireSource(data,overlay);var original=data.CaptureRow("alpineridge",t.SourceRowIndex);
  Require(t.ClassId==Classes[Array.IndexOf(Members,t.SourceRowIndex)]&&t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.Count==0&&t.DroppedExternalPointers==0,"complete original source row/path/link bytes");
  foreach(var link in Links(t.SourceRowIndex))Require(BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(link.Offset))==link.SourceRow,"original reciprocal integer target");
  return t with{SourceRowIndices=Links(t.SourceRowIndex),SourcePrerequisiteRows=Prerequisites(t.SourceRowIndex)};
 }
 internal static void RequirePrerequisites(UniversalRowTemplate t,IReadOnlyList<UniversalRowTemplate> all)
 {
  Require(Applies(t)&&t.SourceRowIndices.SequenceEqual(Links(t.SourceRowIndex))&&t.SourcePrerequisiteRows.SequenceEqual(Prerequisites(t.SourceRowIndex)),"complete issued source links and companions");
  foreach(int row in Members){var found=all.Where(a=>Applies(a)&&a.SourceRowIndex==row&&(a.LinkGroupId??"")==(t.LinkGroupId??"")).ToArray();Require(found.Length==1&&found[0].ClassId==Classes[Array.IndexOf(Members,row)],"all three original members once in the same linked group");}
 }
 internal static void RequireNativeRemoval(string key,UniversalLevelData current,UniversalLevelData original,UniversalOverlay overlay,IReadOnlySet<int> removed)
 {
  if(key!="alpineridge"||removed.Count==0)return;UniversalGreenDruidWorldRows.RequireSource(original,overlay);if(Members.Any(removed.Contains))Require(Members.All(removed.Contains),"remove the complete linked Druid/wizard/terrain-target group together");
  foreach(int row in Members.Where(r=>!removed.Contains(r))){var a=original.CaptureRow(key,row);var b=current.CaptureRow(key,row);Require(a.ClassId==b.ClassId&&a.Properties.Length==b.Properties.Length,"surviving source identity");foreach(var link in Links(row))Require(a.Properties.AsSpan(link.Offset,4).SequenceEqual(b.Properties.AsSpan(link.Offset,4)),"surviving reciprocal links cannot be retargeted before slot reuse");}
 }
}
