using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Complete exact11/44/53 installed capability. The initial destination
/// is an empty authored Peace Keepers host with authenticated Town continuations.</summary>
public sealed class UniversalGreenDruidLinkedWorldInstallation
{
 private readonly UniversalLevelData _source;
 private readonly UniversalOverlay _sourceOverlay,_destination;
 private readonly UniversalMobyPlacement[] _placements;
 private readonly int[] _physicalRows;
 internal UniversalGreenDruidGroup8Actors Actors{get;}
 internal GreenDruidLinkedWorldStorage Storage{get;}
 internal string BehaviorNote=>"This linked group includes both characters, their moving terrain, and the surfaces needed by their paths. Move or remove the group together.";
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Linked Druid world: "+why);}
 internal static bool IsRequested(IReadOnlyList<UniversalMobyPlacement> placements)=>placements.Any(p=>p.SourceLevelKey=="alpineridge"&&UniversalGreenDruidLinkedRows.Members.Contains(p.SourceRowIndex));
 internal static void RequireRequest(string destination,UniversalLevelData host,UniversalOverlay overlay,byte[]exe,byte[]?originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlySet<int> removed,int?prefix)
 {
  Require(destination=="peacekeepers"&&host.Scene().RowCount==0&&removed.Count==0&&prefix==135,"requires an empty authored Peace Keepers level with its original save prefix");
  Require(placements.Count==3&&placements.All(p=>p.SourceLevelKey=="alpineridge"&&p.SpecialId==null&&!p.Retired)&&placements.Select(p=>p.SourceRowIndex).Order().SequenceEqual(UniversalGreenDruidLinkedRows.Members)&&placements.Select(p=>p.LinkGroupId).Distinct().Count()==1&&!string.IsNullOrWhiteSpace(placements[0].LinkGroupId),"all three original companions once in one linked group");
  Require(originalExe!=null,"complete original executable owner required");var proof=UniversalGreenDruidLinkedWorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!);var actors=UniversalGreenDruidGroup8Actors.CaptureSource(source,sourceOverlay,originalExe!);actors.RequireNative(exe);actors.RequireDestination(overlay);
  var scene=host.Scene();int capacity=(scene.ArenaEnd-scene.RowsOffset-placements.Count*UniversalLevelData.RowStride)/0x70;
  Require(capacity>=actors.OriginalDynamicCapacity&&capacity+placements.Count<=256,"requires91 native dynamic slots after placement for complete projectile and fragment gameplay");
  _=proof.BuildZeroSkyVisibility(host,Enumerable.Range(GreenDruidCompleteWorldStorage.Sectors(GreenDruidCompleteWorldStorage.World(host)[1]).Length,6).ToArray());
  int[][] deltas=placements.Select(p=>{var row=source.Row(p.SourceRowIndex);return new[]{p.X-I(row,12),p.Y-I(row,16),p.Z-I(row,20)};}).ToArray();Require(deltas.All(d=>d.SequenceEqual(deltas[0]))&&deltas[0].All(d=>d%16==0),"move the entire original group by one common whole-unit translation");_=proof.CopyTranslated(deltas[0]);
 }
 internal static UniversalGreenDruidLinkedWorldInstallation Capture(string destination,UniversalLevelData host,UniversalOverlay overlay,byte[]exe,byte[]?originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,UniversalVramAllocator allocator,IReadOnlySet<int> removed,int?prefix)
 {
  RequireRequest(destination,host,overlay,exe,originalExe,source,sourceOverlay,placements,removed,prefix);var proof=UniversalGreenDruidLinkedWorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!);var actors=UniversalGreenDruidGroup8Actors.CaptureSource(source,sourceOverlay,originalExe!);
  var allocation=UniversalRowRemoval.Allocate(host,removed,3);int[] physical=proof.RequiredSourceRows.Select(r=>allocation.NewRows[placements.ToList().FindIndex(p=>p.SourceRowIndex==r)]).ToArray();var p=placements[0];var row=source.Row(p.SourceRowIndex);int[] delta=[p.X-I(row,12),p.Y-I(row,16),p.Z-I(row,20)];var storage=new GreenDruidLinkedWorldStorage(proof,host,delta,allocator,physical);
  actors.BindGroups(new Dictionary<int,int>{{8,storage.Binding.DestinationGroups[0]},{9,storage.Binding.DestinationGroups[1]}});return new(source,sourceOverlay,overlay,placements,allocation.NewRows,actors,storage);
 }
 private UniversalGreenDruidLinkedWorldInstallation(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalOverlay destination,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlyList<int> physical,UniversalGreenDruidGroup8Actors actors,GreenDruidLinkedWorldStorage storage)
 {_source=new(source.Bytes.ToArray());_sourceOverlay=new(sourceOverlay.Bytes.ToArray(),sourceOverlay.Bindings.ToDictionary());_destination=new(destination.Bytes.ToArray(),destination.Bindings.ToDictionary());_placements=placements.ToArray();_physicalRows=physical.ToArray();Actors=actors;Storage=storage;}
 internal void BindNamespace(IReadOnlyDictionary<int,int> map,byte[]exe)=>Actors.BindBundle(_sourceOverlay,_destination,UniversalGreenDruidGroup8Actors.Classes,map,exe);
 internal UniversalRowTemplate Prepare(UniversalRowTemplate t,ReadOnlySpan<byte> row)
 {
  Require(UniversalGreenDruidLinkedRows.Applies(t),"exact enrolled source member");var original=Actors.CaptureRow(t.SourceRowIndex,t.LinkGroupId!);Require(t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.ClassId==original.ClassId&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.Count==0&&t.DroppedExternalPointers==0&&t.SourceRowIndices.SequenceEqual(original.SourceRowIndices)&&t.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows),"source template state/path/links changed");
  int index=Array.FindIndex(_placements,p=>p.SourceRowIndex==t.SourceRowIndex);var p=_placements[index];Require(row.Length==88&&I(row,12)==p.X&&I(row,16)==p.Y&&I(row,20)==p.Z,"issued common translated actor position");var moved=Storage.Binding.Resources.Actors.Single(a=>a.SourceRow==t.SourceRowIndex);return t with{Properties=moved.Template.Properties.ToArray()};
 }
 internal void RequireAllocation(IReadOnlyList<int> actual)=>Require(actual.SequenceEqual(_physicalRows),"actual physical allocation changed");
 internal UniversalOwnedParticleCertificate Dispatcher()=>Actors.Dispatcher(_destination);
 internal void RequireBundle(UniversalOverlay source,UniversalOverlay destination,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[]exe,IReadOnlyList<UniversalOwnedParticleCertificate>?owned)
 {Require(destination.Bytes.SequenceEqual(_destination.Bytes)&&destination.Bindings.SequenceEqual(_destination.Bindings),"unchanged initial destination callbacks");Actors.BindBundle(source,destination,classes,map,exe);Actors.RequireOwnedParticles(owned??[]);}
 internal void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int>classes)=>Actors.RequireInitializer(part,source,classes);
 private byte[]? _code;private uint _base;private IReadOnlyDictionary<string,uint>? _labels;
 internal void RequireCompiled(byte[] code,uint address,IReadOnlyList<UniversalAsmPart>parts,IReadOnlyDictionary<string,uint>labels)
 {
  Require(address>=_destination.CopyBuffer&&address%8==0,"original native callback extent");Actors.RequireCompiled(code,address,parts,labels,_destination);var replay=UniversalBehaviorBundleBuilder.Assemble(parts,address);Require(code.SequenceEqual(replay.Code)&&labels.Count==replay.Labels.Count&&labels.All(p=>replay.Labels.TryGetValue(p.Key,out var v)&&v==p.Value),"complete emitted bytes and relocations");
  if(_code!=null)Require(_base==address&&_code.SequenceEqual(code)&&_labels!.Count==labels.Count&&_labels.All(p=>labels.TryGetValue(p.Key,out var v)&&v==p.Value),"compiled capability cannot be rebound");_code=code.ToArray();_base=address;_labels=labels.ToDictionary();
 }
 internal void RequireFinal(UniversalLevelData final,UniversalOverlay overlay,byte[]exe,IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)>rows,IReadOnlyList<int> physical)
 {
  RequireAllocation(physical);Actors.RequireNative(exe,true);Actors.RequireFinalRows(final,rows,Storage.DeltaRaw,physical);Actors.RequireFinalResources(final);
  int capacity=UniversalMobyComposer.ArenaSlots(final);Require(capacity>=Actors.OriginalDynamicCapacity&&capacity+final.Scene().RowCount<=256,"complete native dynamic reservation retained in final export");
  var before=new UniversalLevelData(Storage.CopyLevelData());var expected=GreenDruidCompleteWorldStorage.World(before);var actual=GreenDruidCompleteWorldStorage.World(final);for(int n=0;n<6;n++)Require(actual[n].SequenceEqual(expected[n]),"whole final world component owner");var a=before.Subfiles()[3];var b=final.Subfiles()[3];Require(before.Bytes.AsSpan(a.Offset,before.Scene().MobyComponent).SequenceEqual(final.Bytes.AsSpan(b.Offset,final.Scene().MobyComponent)),"whole final seven-bank prefix");
  var materials=Storage.Materials.CopyRecords();var gpu=Storage.Materials.CopyGpu();int count=I(actual[0],4);for(int n=0;n<materials.Length;n++){int id=Storage.FirstMaterial+n;byte[] record=[..actual[0].AsSpan(8+id*16,16),..actual[0].AsSpan(8+count*16+id*168,168)];Require(record.SequenceEqual(materials[n]),"whole issued material descriptor");foreach(var f in GreenDruidWorldMaterialStorage.Describe(record))Require(GreenDruidWorldMaterialStorage.CopyRectangle(final.Vram(),f.Pixel,f.Width,f.Side).SequenceEqual(GreenDruidWorldMaterialStorage.CopyRectangle(gpu,f.Pixel,f.Width,f.Side))&&GreenDruidWorldMaterialStorage.CopyRectangle(final.Vram(),f.Palette,f.PaletteWidth,f.PaletteRows).SequenceEqual(GreenDruidWorldMaterialStorage.CopyRectangle(gpu,f.Palette,f.PaletteWidth,f.PaletteRows)),"complete material samples and fade palettes");}
  Require(_code!=null&&_labels!=null,"final native callback emission required");uint copy=(_base+(uint)_code!.Length+15)&~15u;byte[] raw=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),2048)];_destination.Bytes.Slice(0,checked((int)(_destination.CopyBuffer-UniversalOverlay.Base))).CopyTo(raw);_code.CopyTo(raw,checked((int)(_base-UniversalOverlay.Base)));var saved=_placements.Select((p,i)=>checked(135+(p.PersistentSlot??i))).ToArray();var mapped=NativeMobySaveMap.Append(_destination,raw,_labels!["wrap_upd"],copy,saved);
  var bindings=_destination.Bindings.ToDictionary();foreach(var pair in new[]{(UniversalExeBindings.UpdateGlobal,mapped.UpdatePointer),(UniversalExeBindings.InitGlobal,_labels["wrap_init"]),(UniversalExeBindings.CopyBufferGlobal,mapped.CopyBuffer),(UniversalExeBindings.ParticleUpdateGlobal,_labels["wrap_owned_particles"])})bindings[pair.Item1]=bindings[pair.Item1] with{Value=pair.Item2};Require(overlay.Bytes.SequenceEqual(mapped.Overlay)&&overlay.Bindings.Count==bindings.Count&&bindings.All(p=>overlay.Bindings.TryGetValue(p.Key,out var v)&&v==p.Value),"final complete native callbacks, compiled code and save-map continuation");
 }
}
