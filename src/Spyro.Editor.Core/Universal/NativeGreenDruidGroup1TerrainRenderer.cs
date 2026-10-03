using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Complete exact native terrain clone. Its single entry gate requires
/// destination index6 AND actual copy/scene anchors; every other load uses the
/// original renderer. Queue storage grants no GPU frame-capacity admission.</summary>
internal sealed class NativeGreenDruidGroup1TerrainRenderer
{
 private const int GateBytes=128;
 private readonly byte[] _exe,_overlay;
 private readonly Dictionary<uint,uint> _pcMap;
 internal GreenDruidGroup1TerrainQueueSceneStorage.Loaded Queues{get;}
 internal uint CloneAddress{get;}
 internal uint GateAddress{get;}
 internal int ReboundControlCount{get;}
 internal bool GpuCapacityCertified=>false;
 internal bool ForeignPlacementAdmitted=>false;
 internal IReadOnlyDictionary<uint,uint> SourceToFinalPc=>new Dictionary<uint,uint>(_pcMap);
 internal byte[] CopyExecutable()=>_exe.ToArray();
 internal byte[] CopyOverlay()=>_overlay.ToArray();
 internal byte[] CopyLevelData()=>Queues.Storage.CopyLevelData();
 /// <summary>Only an issued, exact final renderer may restore its two owned hook
 /// words for existing original-native guards. This is a detached validation view.</summary>
 internal byte[] CopyOriginalRendererValidationView(byte[] actualExe,byte[] actualOverlay,byte[] actualData)
 {RequireFinal(actualExe,actualOverlay,actualData);byte[] view=actualExe.ToArray(),original=Queues.Storage.World.Source.CopyExecutable();int at=(int)(UniversalGreenDruidGroup1TerrainQueues.RendererStart-UniversalExeBindings.ExeBase);original.AsSpan(at,8).CopyTo(view.AsSpan(at,8));return view;}
 private static uint Word(byte[] b,uint pc)=>UniversalExeBindings.Word(b,pc);
 private static void Put(byte[] b,int at,uint w)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),w);
 private static IEnumerable<uint> Lit(int r,uint v)=>new[]{0x3C000000u|((uint)r<<16)|(v>>16),0x34000000u|((uint)r<<21)|((uint)r<<16)|(v&65535)};
 private static uint Jump(uint pc,uint target){UniversalGreenDruidGroup1TerrainQueues.Require(((pc+4)^target)>>28==0,"native J region");return 0x08000000u|((target>>2)&0x3FFFFFF);}
 internal static NativeGreenDruidGroup1TerrainRenderer Install(GreenDruidGroup1TerrainQueueSceneStorage scene,byte[] currentExe,byte[] completeOverlay,NativeLevelRuntimeMemoryReport originalDestinationOwner)=>new(scene,currentExe,completeOverlay,originalDestinationOwner);
 private NativeGreenDruidGroup1TerrainRenderer(GreenDruidGroup1TerrainQueueSceneStorage scene,byte[] currentExe,byte[] overlay,NativeLevelRuntimeMemoryReport owner)
 {
  _owner=owner;var source=scene.World.Source;source.RequireNativeOwners(currentExe);scene.RequireFinal(new(scene.CopyLevelData()));
  var bindings=UniversalExeBindings.Read(currentExe,20);UniversalGreenDruidGroup1TerrainQueues.Require(bindings.TryGetValue(UniversalExeBindings.CopyBufferGlobal,out var binding)&&binding.Value<=UniversalOverlay.Base+(uint)overlay.Length&&binding.Value>=UniversalOverlay.Base,"owned current initializer copy anchor");
  // The generic allocation maximum remains unchanged. Retain the ENTIRE
  // previous overlay allocation, including its existing final sector padding.
  const int cloneBytes=19984;var layout=NativeOwnedOverlayAppendLayout.Create("exact-green-group1-terrain-renderer",overlay,UniversalOverlay.Base,checked(overlay.Length+0x20000),new[]{new NativeOwnedOverlayBlockSize("terrain-clone",cloneBytes),new NativeOwnedOverlayBlockSize("terrain-entry-gate",GateBytes)},"terrain-padding");
  CloneAddress=UniversalOverlay.Base+(uint)overlay.Length;GateAddress=CloneAddress+cloneBytes;
  _exe=currentExe.ToArray();UniversalExeBindings.Patch(_exe,binding!,layout.CopyAnchor);
  _overlay=new byte[layout.FinalByteLength];overlay.CopyTo(_overlay,0);
  // The loaded certificate derives addresses from real final EXE descriptors
  // and CD read lengths, not from a caller's address/count guess.
  Queues=scene.BindLoaded(_exe,_overlay,owner);
  var built=Clone(source.CopyExecutable(),CloneAddress,Queues.Close,Queues.Final);_pcMap=built.Map;ReboundControlCount=built.Controls;
  UniversalGreenDruidGroup1TerrainQueues.Require(built.Bytes.Length==cloneBytes,"exact complete clone allocation");built.Bytes.CopyTo(_overlay,overlay.Length);
  byte[] gate=Gate(GateAddress,CloneAddress,Queues.CopyBuffer,Queues.Scene);gate.CopyTo(_overlay,overlay.Length+cloneBytes);
  Put(_exe,(int)(UniversalGreenDruidGroup1TerrainQueues.RendererStart-UniversalExeBindings.ExeBase),Jump(UniversalGreenDruidGroup1TerrainQueues.RendererStart,GateAddress));Put(_exe,(int)(UniversalGreenDruidGroup1TerrainQueues.RendererStart-UniversalExeBindings.ExeBase)+4,0);
  RequireFinal(_exe,_overlay,scene.CopyLevelData());
 }
 private static (byte[] Bytes,Dictionary<uint,uint> Map,int Controls) Clone(byte[] original,uint @base,uint close,uint normal)
 {
  uint start=UniversalGreenDruidGroup1TerrainQueues.RendererStart,end=UniversalGreenDruidGroup1TerrainQueues.RendererEnd;
  var forms=new Dictionary<uint,(uint End,(int R,uint V)[] Pairs)>{
   [0x800259B4]=(0x800259C4,new[]{(2,0x8006FCF4u),(15,0x800718F4u),(16,normal)}),
   [0x800261E4]=(0x800261F0,new[]{(31,normal)}),
   [0x80026794]=(0x800267A8,new[]{(1,0x8006FCF4u),(31,0x800718F4u),(29,close),(28,normal)}),
   [0x80027690]=(0x8002769C,new[]{(31,normal)}),
   [0x8002857C]=(0x80028590,new[]{(31,close),(1,normal)}),
   [0x800294F4]=(0x80029500,new[]{(31,close)}),
   [0x8002A0A0]=(0x8002A0B0,new[]{(31,normal)})};
  var map=new Dictionary<uint,uint>();var deleted=new HashSet<uint>();List<(uint Final,uint Word,uint? Source)> pieces=[];uint cursor=@base;
  for(uint pc=start;pc<end;){map.Add(pc,cursor);if(forms.TryGetValue(pc,out var form)){var words=form.Pairs.SelectMany(p=>Lit(p.R,p.V)).ToList();if(pc==0x8002857C)words.Add(0xADE103FC);if(pc==0x8002A0A0)words.Insert(0,Word(original,pc));foreach(uint w in words){pieces.Add((cursor,w,null));cursor+=4;}for(uint p=pc+4;p<form.End;p+=4)deleted.Add(p);pc=form.End;}else{pieces.Add((cursor,Word(original,pc),pc));pc+=4;cursor+=4;}}map.Add(end,cursor);
  byte[] result=new byte[cursor-@base];int controls=0;
  foreach(var item in pieces){uint w=item.Word;if(item.Source is uint pc){int op=(int)(w>>26);if(op is 2 or 3){uint target=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);UniversalGreenDruidGroup1TerrainQueues.Require(map.TryGetValue(target,out uint translated)&&!deleted.Contains(target),"all native J/JAL internal ownership; no target in replaced packet interior");w=(w&0xFC000000)|((translated>>2)&0x3FFFFFF);controls++;}else if(op is 1 or 4 or 5 or 6 or 7){uint target=unchecked(pc+4+(uint)(4*(short)w));UniversalGreenDruidGroup1TerrainQueues.Require(map.TryGetValue(target,out uint translated)&&!deleted.Contains(target),"all native branch ownership");long displacement=((long)translated-item.Final-4)/4;UniversalGreenDruidGroup1TerrainQueues.Require(displacement>=short.MinValue&&displacement<=short.MaxValue,"native signed branch reach");w=(w&0xFFFF0000)|(ushort)(short)displacement;controls++;}else if(op==0&&(w&63) is 8 or 9)UniversalGreenDruidGroup1TerrainQueues.Require(pc==0x8002A6F4&&w==0x03E00008,"only original restored indirect return");}Put(result,(int)(item.Final-@base),w);}
  UniversalGreenDruidGroup1TerrainQueues.Require(controls==452,"complete exact control relocation census");return(result,map,controls);
 }
 private static byte[] Gate(uint @base,uint clone,uint copy,uint scene)
 {
  List<uint> w=[];List<int> fails=[];
  // Only AT/v0 are touched. Every load is given its real delay slot. The
  // original prologue overwrites AT and v0 before either is used as input.
  foreach(var check in new[]{(Global:0x80075964u,Value:6u),(Global:0x800785D8u,Value:copy),(Global:0x800785E4u,Value:scene)}){
   uint hi=(check.Global+0x8000)>>16;w.Add(0x3C010000|hi);w.Add(0x8C220000|(check.Global&65535));w.Add(0);w.AddRange(Lit(1,check.Value));fails.Add(w.Count);w.Add(0x14410000);w.Add(0);
  }
  w.Add(Jump(@base+(uint)w.Count*4,clone));w.Add(0);int defaultIndex=w.Count;
  w.Add(0x3C018007);w.Add(0x24217DD8);w.Add(Jump(@base+(uint)w.Count*4,UniversalGreenDruidGroup1TerrainQueues.RendererStart+8));w.Add(0);
  foreach(int index in fails)w[index]|=(ushort)(defaultIndex-index-1);
  UniversalGreenDruidGroup1TerrainQueues.Require(w.Count*4<=GateBytes,"exact entry-gate allocation");byte[] bytes=new byte[GateBytes];for(int n=0;n<w.Count;n++)Put(bytes,4*n,w[n]);return bytes;
 }
 internal void RequireFinal(byte[] exe,byte[] overlay,byte[] data)
 {
  UniversalGreenDruidGroup1TerrainQueues.Require(exe.AsSpan().SequenceEqual(_exe)&&overlay.AsSpan().SequenceEqual(_overlay),"all issued final EXE/overlay bytes");Queues.Storage.RequireFinal(new(data));Queues.Storage.World.Source.RequireNativeOwners(exe,true);
  var current=Queues.Storage.BindLoaded(exe,overlay,_owner!);UniversalGreenDruidGroup1TerrainQueues.Require(current.CopyBuffer==Queues.CopyBuffer&&current.Scene==Queues.Scene&&current.Close==Queues.Close&&current.Final==Queues.Final&&current.End==Queues.End,"authenticated loaded owner anchors unchanged");
 }
 private readonly NativeLevelRuntimeMemoryReport? _owner;
}
