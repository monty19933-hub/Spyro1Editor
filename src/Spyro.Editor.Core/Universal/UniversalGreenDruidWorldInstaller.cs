using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;
/// <summary>Private capability for exactly one issued complete original
/// Alpine group in an empty bounded authored Peace Keepers host. Original actor
/// visibility is retained; fixed collision uses unrestricted destination occlusion.</summary>
public sealed class UniversalGreenDruidWorldInstallation
{
 private readonly UniversalLevelData _source;
 private readonly UniversalOverlay _sourceOverlay,_destination;
 private readonly byte[] _originalExe;
 private readonly UniversalMobyPlacement[] _placements;
 private readonly int[] _physicalRows;
 private readonly GreenDruidWorldSourceProfile _profile;
 internal GreenDruidCompleteWorldStorage Storage{get;}
 internal string DestinationKey=>"peacekeepers";
 internal string BehaviorNote=>"Moving terrain uses destination visibility; source camera occlusion is unrestricted. Move the druid and its terrain target together.";
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException(UniversalGreenDruidWorldOwnership.Refusal+": "+why);}
 internal static UniversalGreenDruidWorldRequest? RequireRequest(string destinationKey,UniversalLevelData host,UniversalOverlay destination,byte[]exe,byte[]? originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlySet<int> removed,int? persistentPrefix)
 {
  if(!placements.Any(p=>p.SourceLevelKey=="alpineridge"&&UniversalGreenDruidWorldRows.EnrolledMembers.Contains(p.SourceRowIndex))||destinationKey=="alpineridge")return null;
  Require(originalExe!=null,"original native executable required");Require(destinationKey=="peacekeepers"&&host.Scene().RowCount==0&&removed.Count==0&&persistentPrefix==135,"issued slice requires an empty authored Peace Keepers host with unchanged save prefix");Require(placements.Count==2&&placements.All(p=>p.SourceLevelKey=="alpineridge"&&p.SpecialId==null&&!p.Retired)&&placements.Select(p=>p.SourceRowIndex).Order().SequenceEqual(GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).Members.Order())&&placements.Select(p=>p.LinkGroupId??"").Distinct().Count()==1,"both original linked members once in one group");_=UniversalGreenDruidWorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!,GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).DruidRow);RequireNative(exe,originalExe!);RequireDestination(destination,exe);
  return new(destinationKey,host.Bytes.ToArray(),source.Bytes.ToArray());
 }
 internal static UniversalGreenDruidWorldInstallation? Capture(string destinationKey,UniversalLevelData host,UniversalOverlay destination,byte[]exe,byte[]? originalExe,UniversalLevelData source,UniversalOverlay sourceOverlay,IReadOnlyList<UniversalMobyPlacement> placements,UniversalVramAllocator allocator,IReadOnlySet<int> removed,int? persistentPrefix)
 {
  if(RequireRequest(destinationKey,host,destination,exe,originalExe,source,sourceOverlay,placements,removed,persistentPrefix)==null)return null;
  Require(originalExe!=null,"the original native executable is required");Require(persistentPrefix==135,"the unchanged Peace Keepers saved-row prefix is required");Require(destinationKey=="peacekeepers"&&host.Scene().RowCount==0&&removed.Count==0,"this moving-terrain group needs an empty authored Peace Keepers host");Require(placements.Count==2&&placements.All(p=>p.SourceLevelKey=="alpineridge"&&p.SpecialId==null&&!p.Retired)&&placements.Select(p=>p.SourceRowIndex).Order().SequenceEqual(GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).Members.Order()),"the complete two original linked objects must be placed once");Require(placements.Select(p=>p.LinkGroupId??"").Distinct().Count()==1,"all two objects belong to one linked group");RequireNative(exe,originalExe!);_=UniversalGreenDruidWorldPrerequisites.CaptureSource("alpineridge",source,sourceOverlay,originalExe!,GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).DruidRow);RequireDestination(destination,exe);
  var sf=host.Subfiles()[3];int at=sf.Offset+0x88;for(int role=0;role<7;role++){Require(host.U32(at+4)==0,"issued slice retains an authored host with no existing animation groups");at+=host.U32(at);}
  int[][] deltas=placements.Select(p=>{var t=source.CaptureRow("alpineridge",p.SourceRowIndex);return new[]{p.X-I(t.Row,12),p.Y-I(t.Row,16),p.Z-I(t.Row,20)};}).ToArray();Require(deltas.All(d=>d.SequenceEqual(deltas[0])),"move the linked group together, preserving the native paths and terrain");
  var allocation=UniversalRowRemoval.Allocate(host,removed,placements.Count);int[] physical=GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).Members.Select(r=>allocation.NewRows[placements.ToList().FindIndex(p=>p.SourceRowIndex==r)]).ToArray();Require(physical.All(p=>p is >=0 and <=255),"both physical rows fit the particle target byte");
  var storage=GreenDruidCompleteWorldStorage.Capture("alpineridge",source,sourceOverlay,originalExe!,host,deltas[0],allocator,physical,GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex).DruidRow);return new(source,sourceOverlay,destination,originalExe!,placements,allocation.NewRows,storage);
 }
 private UniversalGreenDruidWorldInstallation(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalOverlay destination,byte[]originalExe,IReadOnlyList<UniversalMobyPlacement>placements,IReadOnlyList<int>physical,GreenDruidCompleteWorldStorage storage)
 {_profile=GreenDruidWorldSourceProfile.ForRow(placements[0].SourceRowIndex);_source=new(source.Bytes.ToArray());_sourceOverlay=new(sourceOverlay.Bytes.ToArray(),sourceOverlay.Bindings.ToDictionary());_destination=new(destination.Bytes.ToArray(),destination.Bindings.ToDictionary());_originalExe=originalExe.ToArray();_placements=placements.ToArray();_physicalRows=physical.ToArray();Storage=storage;}
 internal static void RequireNative(byte[]current,byte[]original,bool allowSaveMap=false)
 {
  _=UniversalGreenDruidWorldPrerequisites.ExeSha;Require(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(original))==UniversalGreenDruidWorldPrerequisites.ExeSha,"complete original native executable owner");Require(current.Length==original.Length,"complete current native executable extent");UniversalSoundBankStorage.RequireExecutable(current);var renderer=NativeDragonStatueColors.OriginalRendererView(current);
  // The final generic composer installs the complete authenticated saved-row
  // mapping image. Validate every emitted window before restoring its exact
  // original bytes for the native world-consumer fence; partial hooks refuse.
  var windows=NativeMobySaveMap.Windows();bool changed=windows.Any(w=>!renderer.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length).SequenceEqual(original.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length)));
  if(changed){Require(allowSaveMap,"saved-row mapping is not an initial source owner");Require(windows.All(w=>renderer.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length).SequenceEqual(w.Code)),"complete canonical saved-row mapping required");renderer=renderer.ToArray();foreach(var w in windows)original.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length).CopyTo(renderer.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length));}
  foreach(var (first,end) in new (uint,uint)[]{(0x80016AB4,0x80017D8C),(0x800530C0,0x8005366C),(0x80055A78,0x80057000),(0x8006CBF8,0x8006CC38),(0x80012CF0,0x80013234),(0x80013C64,0x80013EF0),(0x80016930,0x80016958),(0x800176C8,0x80017D8C),(0x800258F0,0x8002BA74),(0x80037DEC,0x80037EA0),(0x80037F90,0x8003A7A0),(0x8004AE38,0x8004E29C),(0x800524C4,0x80052F10),(0x8006272C,0x80062790)}){int at=(int)(first-UniversalExeBindings.ExeBase),len=(int)(end-first);Require(renderer.Length==original.Length&&renderer.AsSpan(at,len).SequenceEqual(original.AsSpan(at,len)),"native world loader/queue/cache/collision/vector/model/RNG consumer owner changed at0x"+first.ToString("X8")+" firstdelta0x"+(first+(uint)Enumerable.Range(0,len).FirstOrDefault(n=>renderer[at+n]!=original[at+n])).ToString("X8"));}
 }
 internal static void RequireDestination(UniversalOverlay destination,byte[]?exe)
 {var p=UniversalDarkPassageParticleHosts.Require("peacekeepers",destination,exe);Require(p.Key=="townsquare"&&destination.Word(p.SpawnTable+32)==0x80089E74&&destination.Word(p.UpdateTable+32)==0x80088F20,"the first host must own native no-op type8 managers");}
 internal UniversalRowTemplate Prepare(UniversalRowTemplate t,ReadOnlySpan<byte>row)
 {
  Require(UniversalGreenDruidWorldRows.Applies(t),"unenrolled source row");var original=UniversalGreenDruidWorldRows.Capture(_source,_sourceOverlay,_source.CaptureRow("alpineridge",t.SourceRowIndex));Require(t.ClassId==original.ClassId&&t.ExternalRowPointers.Count==0&&t.DroppedExternalPointers==0&&t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.SourceRowIndices.SequenceEqual(original.SourceRowIndices)&&t.SourcePrerequisiteRows.SequenceEqual(original.SourcePrerequisiteRows),"original enrollment and mutable inputs changed");int index=Array.FindIndex(_placements,p=>p.SourceRowIndex==t.SourceRowIndex);Require(index>=0,"placement member owner");var p=_placements[index];Require(row.Length==88&&I(row,12)==p.X&&I(row,16)==p.Y&&I(row,20)==p.Z,"issued moved position");for(int b=4;b<88;b++)if(b is not(>=12 and <24)&&b is not(0x43 or 0x4A))Require(row[b]==t.Row[b],"original state, animation, orientation and collision inputs required");Require(row[0x43]==255&&row[0x4A]==255,"native unrestricted actor activation/visibility");var moved=Storage.Actors.Actors.Single(a=>a.SourceRow==t.SourceRowIndex);return t with{Properties=moved.Template.Properties.ToArray()};
 }
 internal void RequireAllocation(IReadOnlyList<int> actual)=>Require(actual.SequenceEqual(_physicalRows),"physical row allocation changed after world ownership");
 internal UniversalOwnedParticleCertificate Dispatcher()
 {
  var profile=UniversalGreenDruidWorldSpellParticles.CaptureSource(_source,_sourceOverlay,_originalExe);return new(profile,DestinationKey,_destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,(part,owner,label)=>PatchActor(part,owner,label),d=>RequireDestination(d,null));
 }
 private IReadOnlyList<int>? _classes;private IReadOnlyDictionary<int,int>? _map;
 internal void RequireBundle(UniversalOverlay source,UniversalOverlay destination,IReadOnlyList<int>classes,IReadOnlyDictionary<int,int>map,byte[]exe,IReadOnlyList<UniversalOwnedParticleCertificate>?owned)
 {
  Require(source.Bytes.SequenceEqual(_sourceOverlay.Bytes)&&source.Bindings.SequenceEqual(_sourceOverlay.Bindings),"exact copied source callback owner");RequireNative(exe,_originalExe);RequireDestination(destination,exe);Require(destination.Bytes.SequenceEqual(_destination.Bytes)&&destination.Bindings.SequenceEqual(_destination.Bindings),"exact initial destination callback owner");Require(classes.SequenceEqual(new[]{0x10E,0x148})&&map.Count==classes.Count&&map.Keys.Order().SequenceEqual(classes)&&map.Values.Distinct().Count()==classes.Count&&map.Values.All(v=>v is >=0 and <512),"the complete exact world/root/child namespace");var dispatch=owned?.Where(d=>d.SourceLevelKey=="alpineridge").ToArray()??[];Require(dispatch.Length==1&&dispatch[0].TypeIds.SequenceEqual(new[]{8}),"one copied8 manager, never repeated lifetime passes");if(_classes!=null)Require(_classes.SequenceEqual(classes)&&_map!.Count==map.Count&&_map.All(p=>map.TryGetValue(p.Key,out var v)&&v==p.Value),"issued bundle class allocation cannot be rebound");_classes=classes.ToArray();_map=map.ToDictionary();
 }
 private void PatchActor(UniversalAsmPart part,UniversalOverlay owner,string label)
 {
  Require(_classes!=null&&_map!=null,"world bundle owner must precede copied actor patch");var original=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(owner,_classes!,part.Items.First().Label!,_map!).Part;
  Require(part.Items.SequenceEqual(original.Items)&&part.Tables.Count==original.Tables.Count&&original.Tables.All(p=>part.Tables.TryGetValue(p.Key,out var rows)&&rows.SequenceEqual(p.Value)),"complete emitted actor flow, operands, targets, entries and delays");
  UniversalGreenDruidWorldSpellParticles.PatchCalls(part,owner,label,_classes!,_map!);

 }
 internal void RequireInitializer(UniversalAsmPart part,UniversalOverlay source,IReadOnlyList<int>classes)
 {
  Require(_classes!=null&&source.Bytes.SequenceEqual(_sourceOverlay.Bytes)&&source.Bindings.SequenceEqual(_sourceOverlay.Bindings)&&classes.All(c=>_classes.Contains(c)),"issued source constructor owner");string tag=part.Items.FirstOrDefault()?.Label??throw new InvalidDataException("Missing Green Druid world initializer.");var expected=UniversalBehaviorBundleBuilder.BuildSpecializedInit(source,classes,tag).Part;
  Require(part.Items.SequenceEqual(expected.Items)&&part.LenientTables==expected.LenientTables&&part.Tables.Count==expected.Tables.Count&&expected.Tables.All(t=>part.Tables.TryGetValue(t.Key,out var v)&&v.SequenceEqual(t.Value)),"complete emitted world constructor/cache/path state owner changed");
 }
 private byte[]? _code;private IReadOnlyDictionary<string,uint>? _labels;private uint _base;private IReadOnlyDictionary<uint,uint>? _actorOrigins;
 internal void RequireCompiled(byte[] code,uint address,IReadOnlyList<UniversalAsmPart>parts,IReadOnlyDictionary<string,uint>labels)
 {
  Require(_classes!=null&&parts.Count>0&&address>=_destination.CopyBuffer&&address%8==0,"issued bundle phase and retained native callback bounds");var replay=UniversalBehaviorBundleBuilder.Assemble(parts,address);Require(code.SequenceEqual(replay.Code)&&labels.Count==replay.Labels.Count&&labels.All(p=>replay.Labels.TryGetValue(p.Key,out var v)&&v==p.Value),"final compiled relocation bytes/labels changed");
  if(_code!=null)Require(_base==address&&_code.SequenceEqual(code)&&_labels!.Count==labels.Count&&_labels.All(p=>labels.TryGetValue(p.Key,out var v)&&v==p.Value),"compiled world capability cannot be rebound");_base=address;_code=code.ToArray();_labels=labels.ToDictionary();var origins=new Dictionary<uint,uint>();uint pc=address;foreach(var p in parts){bool actor=p.Items.FirstOrDefault()?.Label=="u_alpineridge";foreach(var i in p.Items){if(i.Word==null)continue;if(actor&&i.Orig is uint orig){Require(origins.TryAdd(orig,pc),"duplicate emitted actor origin");}pc+=4;}}_actorOrigins=origins;
 }
 internal IReadOnlyDictionary<uint,uint> CopyActorOrigins()=>_actorOrigins?.ToDictionary()??throw new InvalidDataException("Missing compiled actor owner");
 internal (byte[]Code,IReadOnlyDictionary<string,uint>Labels,uint Base) CopyReplay()=> (_code?.ToArray()??throw new InvalidDataException("Missing compiled world owner"),_labels!.ToDictionary(),_base);
 private void RequireCompiledOverlay(UniversalOverlay final)
 {
  Require(_code!=null&&_labels!=null,"final world needs its actual compiled bundle");uint copy=(_base+(uint)_code!.Length+15)&~15u;byte[] raw=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),2048)];_destination.Bytes.Slice(0,checked((int)(_destination.CopyBuffer-UniversalOverlay.Base))).CopyTo(raw);_code.CopyTo(raw,checked((int)(_base-UniversalOverlay.Base)));var saveRows=_placements.Select((p,i)=>checked(135+(p.PersistentSlot??i))).ToArray();var emitted=NativeMobySaveMap.Append(_destination,raw,_labels!["wrap_upd"],copy,saveRows);
  var expectedBindings=_destination.Bindings.ToDictionary();foreach(var pair in new[]{(UniversalExeBindings.UpdateGlobal,emitted.UpdatePointer),(UniversalExeBindings.InitGlobal,_labels["wrap_init"]),(UniversalExeBindings.CopyBufferGlobal,emitted.CopyBuffer),(UniversalExeBindings.ParticleUpdateGlobal,_labels["wrap_owned_particles"])})expectedBindings[pair.Item1]=expectedBindings[pair.Item1] with{Value=pair.Item2};
  Require(final.Bytes.SequenceEqual(emitted.Overlay)&&final.Bindings.Count==expectedBindings.Count&&expectedBindings.All(p=>final.Bindings.TryGetValue(p.Key,out var b)&&b==p.Value),"final original callbacks, compiled world/particles and saved-row continuation changed");
 }
 internal void RequireFinal(UniversalLevelData final,UniversalOverlay overlay,byte[]exe,IReadOnlyList<(byte[]Row,UniversalRowTemplate Template)>rows,IReadOnlyList<int>physical)
 {
  RequireNative(exe,_originalExe,true);RequireCompiledOverlay(overlay);RequireAllocation(physical);var before=new UniversalLevelData(Storage.CopyLevelData());var expected=GreenDruidCompleteWorldStorage.World(before);var actual=GreenDruidCompleteWorldStorage.World(final);for(int n=0;n<6;n++)Require(expected[n].SequenceEqual(actual[n]),"complete final world resource owner changed");var sf=before.Subfiles()[3];var outSf=final.Subfiles()[3];int prefix=before.Scene().MobyComponent;Require(before.Bytes.AsSpan(sf.Offset,prefix).SequenceEqual(final.Bytes.AsSpan(outSf.Offset,prefix)),"final seven banks/header/fixup owner changed");int count=I(actual[0],4);var records=Storage.Materials.CopyRecords();var issuedGpu=Storage.Materials.CopyGpu();for(int n=0;n<records.Length;n++){int id=Storage.FirstMaterial+n;byte[] record=[..actual[0].AsSpan(8+id*16,16),..actual[0].AsSpan(8+count*16+id*168,168)];Require(record.SequenceEqual(records[n]),"final sampled material descriptor changed");foreach(var d in GreenDruidWorldMaterialStorage.Describe(record))Require(GreenDruidWorldMaterialStorage.CopyRectangle(final.Vram(),d.Pixel,d.Width,d.Side).SequenceEqual(GreenDruidWorldMaterialStorage.CopyRectangle(issuedGpu,d.Pixel,d.Width,d.Side))&&GreenDruidWorldMaterialStorage.CopyRectangle(final.Vram(),d.Palette,d.PaletteWidth,d.PaletteRows).SequenceEqual(GreenDruidWorldMaterialStorage.CopyRectangle(issuedGpu,d.Palette,d.PaletteWidth,d.PaletteRows)),"final sampled moving material pixels/palette changed");}
  UniversalGreenDruidWorldSpellParticles.RequireTexture(_source,final);foreach(var (row,t) in rows){Require(UniversalGreenDruidWorldRows.Applies(t),"complete group-only first slice");int n=Array.FindIndex(_placements,p=>p.SourceRowIndex==t.SourceRowIndex);var found=final.CaptureRow("final",physical[n]);var properties=t.Properties.ToArray();foreach(var link in t.ImportedRowIndices)BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(link.Offset),physical[link.PlacementIndex]);var observed=found.Properties.ToArray();foreach(var p in t.InternalPointers)properties.AsSpan(p.Offset,4).Clear();foreach(var p in found.InternalPointers)observed.AsSpan(p.Offset,4).Clear();Require(row.AsSpan(4).SequenceEqual(found.Row.AsSpan(4))&&properties.SequenceEqual(observed)&&t.InternalPointers.SequenceEqual(found.InternalPointers)&&found.ExternalRowPointers.Count==0,"final exact row/path/group/physical target/cache inputs");if(t.SourceRowIndex==_profile.DruidRow)Require(I(found.Properties,4)==_profile.Group&&I(found.Properties,8)==physical[Array.FindIndex(_placements,p=>p.SourceRowIndex==_profile.TargetRow)],"original group byte and actual target physical ordinal");}
 }
}

/// <summary>Pending resource validation only; no Bundle/export permission. The
/// complete installed capability must follow before copied code is emitted.</summary>
internal sealed class UniversalGreenDruidWorldRequest
{
 private readonly string _destination;private readonly byte[] _host,_source;
 internal UniversalGreenDruidWorldRequest(string destination,byte[]host,byte[]source){_destination=destination;_host=host;_source=source;}
 internal void Require(string destination,string sourceKey,int actor,UniversalLevelData host,UniversalLevelData source)
 {if(destination!=_destination||sourceKey!="alpineridge"||actor!=0x10E||!host.Bytes.SequenceEqual(_host)||!source.Bytes.SequenceEqual(_source))throw new InvalidDataException("Pending Green Druid owner changed before complete world installation.");}
}
