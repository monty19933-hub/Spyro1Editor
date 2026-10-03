namespace Spyro.Editor.Core.Universal;
/// <summary>Only an unchanged original native Alpine world can retain original
/// physical target48. Separate from foreign translated-world installation.</summary>
internal sealed class UniversalGreenDruidNativeReferences
{
 private readonly UniversalRowTemplate _original;
 private UniversalGreenDruidNativeReferences(UniversalRowTemplate original)=>_original=original with{Row=original.Row.ToArray(),Properties=original.Properties.ToArray(),InternalPointers=original.InternalPointers.ToArray(),ExternalRowPointers=original.ExternalRowPointers.ToArray()};
 internal static UniversalRowTemplate Capture(UniversalLevelData current,UniversalLevelData source,UniversalOverlay overlay,UniversalRowTemplate template,IReadOnlySet<int> removed)
 {
  if(!UniversalGreenDruidWorldRows.Applies(template))throw new InvalidDataException("Unenrolled retained Green Druid owner.");UniversalGreenDruidWorldRows.RequireSource(source,overlay);UniversalGreenDruidWorldOwnership.Require("alpineridge","alpineridge",0x10E,current,source,removed);var original=source.CaptureRow("alpineridge",template.SourceRowIndex);var certificate=new UniversalGreenDruidNativeReferences(original);var result=template with{SourceRowIndices=[],SourcePrerequisiteRows=[],ImportedRowIndices=[],GreenDruidNativeReferences=certificate};certificate.Require(result);return result;
 }
 internal void Require(UniversalRowTemplate template)
 {
  if(template.SourceLevelKey!=_original.SourceLevelKey||template.SourceRowIndex!=_original.SourceRowIndex||!template.Row.SequenceEqual(_original.Row)||!template.Properties.SequenceEqual(_original.Properties)||!template.InternalPointers.SequenceEqual(_original.InternalPointers)||!template.ExternalRowPointers.SequenceEqual(_original.ExternalRowPointers)||template.DroppedExternalPointers!=_original.DroppedExternalPointers||template.SourceRowIndices.Count!=0||template.SourcePrerequisiteRows.Count!=0||template.ImportedRowIndices.Count!=0||!ReferenceEquals(template.GreenDruidNativeReferences,this))throw new InvalidDataException("Retained native Green Druid row/world owner changed.");
 }
}
