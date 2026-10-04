using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact source6/52 preparation and complete emitted-stage verification.
/// Inert: no actor enrollment, export admission, or GPU capacity certificate.
/// An independent source-camera GPU owner is required before any product admission.</summary>
public sealed class UniversalGreenDruidGroup1WorldInstallation
{
 private readonly UniversalLevelData _source;
 private readonly UniversalOverlay _sourceOverlay,_destination;
 private readonly UniversalMobyPlacement[] _placements;
 private readonly int[] _physicalRows;
 private readonly NativeFreshRewardFallbackResources _fallbacks;
 private readonly byte[] _hostGpu;private readonly int[] _hostProtected;
 private Dictionary<int,int>? _map;
 private byte[]? _code; private uint _base; private Dictionary<string,uint>? _labels;
 internal UniversalGreenDruidGroup1Actors Actors{get;}
 internal GreenDruidGroup1WorldStorage Storage{get;}
 internal UniversalGreenDruidGroup1TerrainQueues.BoundWorld TerrainQueues{get;}
 public bool ForeignPlacementAdmitted=>false;
 public bool GpuCapacityCertified=>false;
 internal static readonly int[] Members=[6,52];
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Inert Group1 installation: "+why);}
 internal static bool IsRequested(IReadOnlyList<UniversalMobyPlacement> placements)=>placements.Any(p=>p.SourceLevelKey=="alpineridge"&&Members.Contains(p.SourceRowIndex));
 internal static void RequireRequest(string destination,UniversalLevelData host,UniversalOverlay overlay,byte[] exe,byte[]? originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlySet<int> removed,int? prefix,NativeFreshRewardFallbackResources fallbacks)
 {
  Require(destination=="peacekeepers"&&host.Scene().RowCount==0&&removed.Count==0&&prefix==135,"empty authored Peace Keepers host, no removals, original save prefix135");
  Require(placements.Count==2&&placements.All(p=>p.SourceLevelKey=="alpineridge"&&p.SpecialId==null&&!p.Retired)&&placements.Select(p=>p.SourceRowIndex).Order().SequenceEqual(Members)&&placements.Select(p=>p.LinkGroupId).Distinct().Count()==1&&!string.IsNullOrWhiteSpace(placements[0].LinkGroupId),"both exact source members once in one linked group");
  Require(placements.Select((p,n)=>p.PersistentSlot??n).Distinct().Count()==2&&placements.Select((p,n)=>p.PersistentSlot??n).All(s=>s>=0&&135+s<256),"two distinct native save identities");
  Require(originalExe!=null,"complete original executable owner");_=UniversalGreenDruidGroup1WorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!);var actors=UniversalGreenDruidGroup1Actors.CaptureSource(source,sourceOverlay,originalExe!);actors.RequireNative(exe);actors.RequireDestination(overlay);fallbacks.RequireInstalled(host.Bytes);
  var scene=host.Scene();int capacity=(scene.ArenaEnd-scene.RowsOffset-2*UniversalLevelData.RowStride)/0x70;Require(capacity>=actors.OriginalDynamicCapacity&&capacity+2<=256,"complete original91 native dynamic slots after both placements");
  int[][] deltas=placements.Select(p=>{var row=source.Row(p.SourceRowIndex);return new[]{p.X-I(row,12),p.Y-I(row,16),p.Z-I(row,20)};}).ToArray();Require(deltas.All(d=>d.SequenceEqual(deltas[0]))&&deltas[0].All(d=>d%16==0),"one common whole-unit source translation");
  _=UniversalGreenDruidGroup1WorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!).CopyTranslated(deltas[0]);
 }
 internal static UniversalGreenDruidGroup1WorldInstallation Capture(string destination,UniversalLevelData host,UniversalOverlay overlay,byte[] exe,byte[]? originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,UniversalVramAllocator allocator,IReadOnlySet<int> removed,int? prefix,NativeFreshRewardFallbackResources fallbacks)
 {
  RequireRequest(destination,host,overlay,exe,originalExe,source,sourceOverlay,placements,removed,prefix,fallbacks);var proof=UniversalGreenDruidGroup1WorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!);var actors=UniversalGreenDruidGroup1Actors.CaptureSource(source,sourceOverlay,originalExe!);
  var allocation=UniversalRowRemoval.Allocate(host,removed,2);int[] physical=Members.Select(r=>allocation.NewRows[placements.ToList().FindIndex(p=>p.SourceRowIndex==r)]).ToArray();var p=placements[0];var row=source.Row(p.SourceRowIndex);int[] delta=[p.X-I(row,12),p.Y-I(row,16),p.Z-I(row,20)];var storage=new GreenDruidGroup1WorldStorage(proof,host,delta,allocator,physical);actors.BindGroups(new Dictionary<int,int>{{1,storage.Binding.DestinationGroup}});
  var queues=UniversalGreenDruidGroup1TerrainQueues.CaptureSource("alpineridge",source,originalExe!).Bind(storage);return new(source,sourceOverlay,overlay,placements,allocation.NewRows,actors,storage,queues,fallbacks,host.Vram().ToArray(),allocator);
 }
 private UniversalGreenDruidGroup1WorldInstallation(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalOverlay destination,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlyList<int> physical,UniversalGreenDruidGroup1Actors actors,GreenDruidGroup1WorldStorage storage,UniversalGreenDruidGroup1TerrainQueues.BoundWorld queues,NativeFreshRewardFallbackResources fallbacks,byte[] hostGpu,UniversalVramAllocator allocator)
 {_source=new(source.Bytes.ToArray());_sourceOverlay=new(sourceOverlay.Bytes.ToArray(),sourceOverlay.Bindings.ToDictionary());_destination=new(destination.Bytes.ToArray(),destination.Bindings.ToDictionary());_placements=placements.ToArray();_physicalRows=physical.ToArray();Actors=actors;Storage=storage;TerrainQueues=queues;_fallbacks=fallbacks;_hostGpu=hostGpu.ToArray();_hostProtected=Enumerable.Range(0,hostGpu.Length).Where(n=>!allocator.IsFree(n,1)).ToArray();}
 internal UniversalRowTemplate CaptureRow(int row)=>Actors.CaptureRow(row,_placements.Single(p=>p.SourceRowIndex==row).LinkGroupId!);
 internal void BindNamespace(IReadOnlyDictionary<int,int> map,byte[] exe)
 {Actors.BindBundle(_sourceOverlay,_destination,UniversalGreenDruidGroup1Actors.Classes,map,exe);_map=map.ToDictionary();}
 internal UniversalRowTemplate Prepare(UniversalRowTemplate t,ReadOnlySpan<byte> row)
 {
  Require(Members.Contains(t.SourceRowIndex),"exact source member");var original=CaptureRow(t.SourceRowIndex);Require(t.SourceLevelKey==original.SourceLevelKey&&t.LinkGroupId==original.LinkGroupId&&t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.ClassId==original.ClassId&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.Count==0&&t.DroppedExternalPointers==0&&t.SourceRowIndices.SequenceEqual(original.SourceRowIndices)&&t.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows)&&t.ImportedRowIndices.Count==0&&t.ImportedRowPointers.Count==0&&t.BoarPlacementCertificate==null&&t.DarkLanternContext==null&&t.GreenDruidNativeReferences==null,"unchanged source enrollment/state/path/indices");
  Require(_map!=null,"issued namespace before row preparation");var p=_placements.Single(p=>p.SourceRowIndex==t.SourceRowIndex);byte[] expected=t.Row.ToArray();for(int a=0;a<3;a++)BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12+a*4),a==0?p.X:a==1?p.Y:p.Z);BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(54),(ushort)_map![t.ClassId]);expected[67]=expected[74]=255;Require(row.SequenceEqual(expected),"exact authored pose/mapped class/unrestricted activation; original state/yaw/reward unchanged");
  var moved=Storage.Binding.Resources.Actors.Single(a=>a.SourceRow==t.SourceRowIndex);return t with{Properties=moved.Template.Properties.ToArray()};
 }
 internal IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> ResolveRows(IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> prepared)
 {
  Require(prepared.Count==2&&prepared.Select(r=>r.Template.SourceRowIndex).SequenceEqual(_placements.Select(p=>p.SourceRowIndex)),"exact issued placement order before target fixup");var templates=prepared.Select(r=>r.Template).ToArray();foreach(var t in templates){Actors.RequirePrerequisites(t,templates);Require(t.ImportedRowIndices.Count==0&&t.ImportedRowPointers.Count==0,"unresolved issued metadata only");}
  var result=prepared.Select(r=>(Row:r.Row.ToArray(),Template:r.Template with{ImportedRowIndices=r.Template.SourceRowIndices.Select(l=>new UniversalImportedRowIndex(l.Offset,Array.FindIndex(templates,t=>t.SourceRowIndex==l.SourceRow),l.SourceRow,l.SourceClass)).ToArray()})).ToArray();Actors.RequireTranslatedRows(result,Storage.DeltaRaw,_physicalRows);return result;
 }
 // Source metadata remains owned by this immutable certificate. Generic
 // source enrollment intentionally still refuses6/52. A detached transport view
 // contains already-bound integer links, so the unchanged ordinary writer owns
 // only scene rows/properties/internal pointer relocation here; no native bytes
 // or source prerequisite are dropped from the issued capability.
 internal (byte[] LevelData,int PropertiesBytes,int SceneGrowth,IReadOnlyList<int> NewRowIndices) AppendPreparedRows(UniversalLevelData before,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows)
 {
  Require(before.Scene().RowCount==0,"empty issued host before both exact rows");TerrainQueues.RequireFinal(before);_fallbacks.RequireInstalled(before.Bytes);Actors.RequireTranslatedRows(rows,Storage.DeltaRaw,_physicalRows);
  var copy=new UniversalLevelData(before.Bytes.ToArray());var transport=rows.Select(r=>(r.Row.ToArray(),r.Template with{SourceRowIndices=[],SourcePrerequisiteRows=[],ImportedRowIndices=[]})).ToArray();var written=copy.AppendRows(transport);RequireAllocation(written.NewRowIndices);Actors.RequireFinalRows(copy,rows,Storage.DeltaRaw,written.NewRowIndices);Require(UniversalMobyComposer.ArenaSlots(copy)>=Actors.OriginalDynamicCapacity,"retained complete native arena");return(copy.Bytes.ToArray(),written.PropertiesBytes,written.SceneGrowth,written.NewRowIndices.ToArray());
 }
 internal void RequireAllocation(IReadOnlyList<int> actual)=>Require(actual.SequenceEqual(_physicalRows),"actual physical allocation changed");
 internal UniversalOwnedParticleCertificate Dispatcher()=>Actors.Dispatcher(_destination);
 internal void RequireBundle(UniversalOverlay source,UniversalOverlay destination,IReadOnlyList<int> classes,IReadOnlyDictionary<int,int> map,byte[] exe,IReadOnlyList<UniversalOwnedParticleCertificate>? owned)
 {Require(destination.Bytes.SequenceEqual(_destination.Bytes)&&destination.Bindings.SequenceEqual(_destination.Bindings),"unchanged initial destination callbacks");BindNamespace(map,exe);Actors.BindBundle(source,destination,classes,map,exe);Actors.RequireOwnedParticles(owned??[]);}
 internal void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int> classes)=>Actors.RequireInitializer(part,source,classes);
 // Exact ordinary builder parts, usable by offline preparation/replay without
 // weakening its current complete-world guard or enrolling this cohort.
 internal IReadOnlyList<UniversalAsmPart> ReplayParts()
 {
  Require(_map!=null,"issued namespace");bool remapped=_map!.Any(p=>p.Key!=p.Value);UniversalAsmPart w=new();w.Label("wrap_upd");w.Emit(UniversalMips.EncodeI(9,29,29,-0x18));w.Emit(UniversalMips.EncodeI(43,29,31,0x10));w.Emit(UniversalMips.EncodeJ(3,_destination.Update));w.Emit(0);w.Emit(UniversalMips.EncodeJ(3,0),new(UniversalFixKind.JalLabel,Label:"u_alpineridge"));w.Emit(0);w.Emit(UniversalMips.EncodeI(35,29,31,0x10));w.Emit(0);w.Emit(UniversalMips.JrRa);w.Emit(UniversalMips.EncodeI(9,29,29,0x18));w.Label("wrap_init");
  foreach(int c in UniversalGreenDruidGroup1Actors.Classes){w.Emit(UniversalMips.EncodeI(9,0,1,_map![c]));w.Emit(UniversalMips.EncodeI(4,4,1,0),new(UniversalFixKind.BranchLabel,Label:remapped?$"it_alpineridge_{c:X3}":"i_alpineridge"));w.Emit(0);}w.Emit(UniversalMips.EncodeJ(2,_destination.Init));w.Emit(0);
  if(remapped)foreach(int c in UniversalGreenDruidGroup1Actors.Classes){w.Label($"it_alpineridge_{c:X3}");w.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:$"i_alpineridge_{c:X3}"));w.Emit(UniversalMips.EncodeI(9,0,4,c));}
  var particle=UniversalOwnedParticleDispatch.Build(Dispatcher().Profile,"particle_alpineridge");var actor=Actors.ReplayActorParts(particle.SpawnLabel);return new[]{UniversalSpecialBehaviors.SignHookPreamble(false),w,particle.Spawn,particle.Update,UniversalOwnedParticleDispatch.ChainUpdates([particle.UpdateLabel],_destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,"wrap_owned_particles"),actor.Update}.Concat(actor.Constructors).ToArray();
 }
 internal void RequireCompiled(byte[] code,uint address,IReadOnlyList<UniversalAsmPart> parts,IReadOnlyDictionary<string,uint> labels)
 {
  var expected=ReplayParts();Require(parts.Count==expected.Count,"no extra callback/behavior/particle owner");for(int n=0;n<parts.Count;n++)Require(parts[n].Items.SequenceEqual(expected[n].Items)&&parts[n].LenientTables==expected[n].LenientTables&&parts[n].Tables.Count==expected[n].Tables.Count&&expected[n].Tables.All(p=>parts[n].Tables.TryGetValue(p.Key,out var found)&&p.Value.SequenceEqual(found)),"complete ordinary wrappers, actor, constructors, dispatch and all tables/delay slots");
  Actors.RequireCompiled(code,address,parts,labels,_destination);
  if(_code!=null)Require(_base==address&&_code.SequenceEqual(code)&&_labels!.Count==labels.Count&&_labels.All(p=>labels.TryGetValue(p.Key,out uint v)&&v==p.Value),"compiled stage cannot be rebound");_code=code.ToArray();_base=address;_labels=labels.ToDictionary();
 }
 internal UniversalOverlay CopyCompiledOverlay()
 {
  Require(_code!=null&&_labels!=null,"complete compiled callback emission");uint copy=(_base+(uint)_code!.Length+15)&~15u;byte[] raw=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),2048)];_destination.Bytes.Slice(0,checked((int)(_destination.CopyBuffer-UniversalOverlay.Base))).CopyTo(raw);_code.CopyTo(raw,checked((int)(_base-UniversalOverlay.Base)));var saved=_placements.Select((p,i)=>checked(135+(p.PersistentSlot??i))).ToArray();var mapped=NativeMobySaveMap.Append(_destination,raw,_labels!["wrap_upd"],copy,saved);var bindings=_destination.Bindings.ToDictionary();foreach(var pair in new[]{(UniversalExeBindings.UpdateGlobal,mapped.UpdatePointer),(UniversalExeBindings.InitGlobal,_labels["wrap_init"]),(UniversalExeBindings.CopyBufferGlobal,mapped.CopyBuffer),(UniversalExeBindings.ParticleUpdateGlobal,_labels["wrap_owned_particles"])})bindings[pair.Item1]=bindings[pair.Item1] with{Value=pair.Item2};return new(mapped.Overlay,bindings);
 }
 internal void RequirePreparedFinal(UniversalLevelData final,UniversalOverlay overlay,byte[] exe,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> physical)
 {
  RequireAllocation(physical);Actors.RequireNative(exe,true);Actors.RequireFinalRows(final,rows,Storage.DeltaRaw,physical);Actors.RequireFinalResources(final);_fallbacks.RequireInstalled(final.Bytes);TerrainQueues.RequireStorage(Storage);TerrainQueues.RequireFinal(final);
  int capacity=UniversalMobyComposer.ArenaSlots(final);Require(capacity>=Actors.OriginalDynamicCapacity&&capacity+final.Scene().RowCount<=256,"complete original dynamic arena retained");
  var world=GreenDruidCompleteWorldStorage.World(final);int count=I(world[0],4);var records=Enumerable.Range(Storage.FirstMaterial,16).Select(id=>world[0].AsSpan(8+id*16,16).ToArray().Concat(world[0].AsSpan(8+count*16+id*168,168).ToArray()).ToArray()).ToArray();RequireMaterials(final,records);
  var expected=CopyCompiledOverlay();Require(overlay.Bytes.SequenceEqual(expected.Bytes)&&overlay.Bindings.Count==expected.Bindings.Count&&expected.Bindings.All(p=>overlay.Bindings.TryGetValue(p.Key,out var v)&&v==p.Value),"whole compiled/save-map overlay and exact callbacks");var actual=UniversalExeBindings.Read(exe,20);Require(expected.Bindings.All(p=>actual.TryGetValue(p.Key,out var b)&&b==p.Value),"final emitted EXE callback descriptor ownership");
 }
 private void RequireMaterials(UniversalLevelData final,byte[][] records)
 {
  var expected=Storage.Materials.CopyRecords();Require(records.Length==expected.Length&&records.Select((r,n)=>r.SequenceEqual(expected[n])).All(v=>v),"all16 complete issued material descriptors");var gpu=Storage.Materials.CopyGpu();var actual=final.Vram();foreach(int n in _hostProtected)Require(actual[n]==_hostGpu[n],"complete protected host texture/palette byte");foreach(var record in records)foreach(var f in GreenDruidGroup1MaterialStorage.Describe(record))Require(GreenDruidGroup1MaterialStorage.CopyRectangle(actual,f.Pixel,f.Width,f.Side).SequenceEqual(GreenDruidGroup1MaterialStorage.CopyRectangle(gpu,f.Pixel,f.Width,f.Side))&&GreenDruidGroup1MaterialStorage.CopyRectangle(actual,f.Palette,f.PaletteWidth,f.PaletteRows).SequenceEqual(GreenDruidGroup1MaterialStorage.CopyRectangle(gpu,f.Palette,f.PaletteWidth,f.PaletteRows)),"every complete source material pixel and full distance/fade palette");
 }
 internal InertFinalStage PrepareInertRendererStage(UniversalLevelData final,UniversalOverlay overlay,byte[] exe,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> physical,NativeLevelRuntimeMemoryReport originalDestinationOwner)
 {RequirePreparedFinal(final,overlay,exe,rows,physical);return new(this,final,overlay,exe,rows,physical,originalDestinationOwner);}
 internal sealed class InertFinalStage
 {
  private readonly UniversalGreenDruidGroup1WorldInstallation _owner;
  private readonly (byte[] Row,UniversalRowTemplate Template)[] _rows;
  private readonly int[] _physical;
  internal NativeGreenDruidGroup1TerrainRenderer Renderer{get;}
  internal bool ForeignPlacementAdmitted=>false;
  internal bool GpuCapacityCertified=>false;
  internal InertFinalStage(UniversalGreenDruidGroup1WorldInstallation owner,UniversalLevelData data,UniversalOverlay overlay,byte[] exe,IReadOnlyList<(byte[] Row,UniversalRowTemplate Template)> rows,IReadOnlyList<int> physical,NativeLevelRuntimeMemoryReport originalDestinationOwner)
  {owner.RequirePreparedFinal(data,overlay,exe,rows,physical);_owner=owner;_physical=physical.ToArray();_rows=rows.Select(r=>(r.Row.ToArray(),r.Template with{Row=r.Template.Row.ToArray(),Properties=r.Template.Properties.ToArray(),InternalPointers=r.Template.InternalPointers.ToArray(),SourceRowIndices=r.Template.SourceRowIndices.ToArray(),SourcePrerequisiteRows=r.Template.SourcePrerequisiteRows.ToArray(),ImportedRowIndices=r.Template.ImportedRowIndices.ToArray()})).ToArray();var tail=GreenDruidGroup1TerrainQueueSceneStorage.Reserve(owner.TerrainQueues,data);Renderer=NativeGreenDruidGroup1TerrainRenderer.Install(tail,exe,overlay.Bytes.ToArray(),originalDestinationOwner);RequireInertFinal(Renderer.CopyLevelData(),Renderer.CopyOverlay(),Renderer.CopyExecutable());}
  internal void RequireInertFinal(byte[] data,byte[] overlay,byte[] exe)
  {Renderer.RequireFinal(exe,overlay,data);var view=Renderer.CopyOriginalRendererValidationView(exe,overlay,data);_owner.Actors.RequireNative(view,true);var final=new UniversalLevelData(data);_owner.RequireAllocation(_physical);_owner.Actors.RequireFinalRows(final,_rows,_owner.Storage.DeltaRaw,_physical);_owner.Actors.RequireFinalResources(final);_owner._fallbacks.RequireInstalled(data);_owner.TerrainQueues.RequireFinal(final);Require(!ForeignPlacementAdmitted&&!GpuCapacityCertified,"inert evidence only; independent real GPU certificate unavailable");}
 }
}
