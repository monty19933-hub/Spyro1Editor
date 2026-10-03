using Spyro.Editor.Core.Analysis;
namespace Spyro.Editor.Core.Universal;

/// <summary>A loaded, zero-filled scene-tail allocation, outside every component
/// and the original Moby arena. The certificate cannot accept caller queue sizes.</summary>
internal sealed class GreenDruidGroup1TerrainQueueSceneStorage
{
 private readonly byte[] _before,_after;
 private readonly UniversalGreenDruidGroup1TerrainQueues.BoundWorld _world;
 internal int SceneTailOffset{get;}
 internal int ReservationBytes=>_world.ReservationBytes;
 internal int CloseByteLength=>4*_world.CloseWords;
 internal int FinalByteLength=>4*_world.FinalWords;
 internal int CloseWords=>_world.CloseWords;
 internal int FinalWords=>_world.FinalWords;
 internal UniversalGreenDruidGroup1TerrainQueues.BoundWorld World=>_world;
 private GreenDruidGroup1TerrainQueueSceneStorage(UniversalGreenDruidGroup1TerrainQueues.BoundWorld world,UniversalLevelData data)
 {
  world.RequireFinal(data);_world=world;_before=data.Bytes.ToArray();SceneTailOffset=data.Subfiles()[3].Length;var copy=new UniversalLevelData(_before.ToArray());copy.GrowSubfile(3,ReservationBytes);_after=copy.Bytes.ToArray();RequireFinal(copy);
 }
 internal static GreenDruidGroup1TerrainQueueSceneStorage Reserve(UniversalGreenDruidGroup1TerrainQueues.BoundWorld world,UniversalLevelData completeFinalData)=>new(world,completeFinalData);
 internal byte[] CopyLevelData()=>_after.ToArray();
 internal void RequireFinal(UniversalLevelData actual)
 {
  UniversalGreenDruidGroup1TerrainQueues.Require(actual.Bytes.AsSpan().SequenceEqual(_after),"exact full typed scene-tail body");_world.RequireFinal(actual);
  var before=new UniversalLevelData(_before);var a=before.Subfiles();var b=actual.Subfiles();UniversalGreenDruidGroup1TerrainQueues.Require(a.Count==b.Count&&b[3].Length==a[3].Length+ReservationBytes&&b[3].Offset==a[3].Offset,"actual scene CD read descriptor growth");
  UniversalGreenDruidGroup1TerrainQueues.Require(_before.AsSpan(a[3].Offset,a[3].Length).SequenceEqual(actual.Bytes.AsSpan(b[3].Offset,a[3].Length))&&actual.Bytes.AsSpan(b[3].Offset+SceneTailOffset,ReservationBytes).IndexOfAnyExcept((byte)0)<0,"retained entire scene allocation plus zero-owned suffix");
  for(int n=0;n<a.Count;n++)if(n!=3)UniversalGreenDruidGroup1TerrainQueues.Require(a[n].Length==b[n].Length&&_before.AsSpan(a[n].Offset,a[n].Length).SequenceEqual(actual.Bytes.AsSpan(b[n].Offset,b[n].Length)),"all other complete subfile bytes and read lengths");
  var x=before.Scene();var y=actual.Scene();UniversalGreenDruidGroup1TerrainQueues.Require(x.MobyComponent==y.MobyComponent&&x.MobyComponentSize==y.MobyComponentSize&&x.RowCount==y.RowCount&&x.RowsOffset==y.RowsOffset&&x.PropertiesComponent==y.PropertiesComponent&&x.PropertiesSize==y.PropertiesSize&&x.PodComponent==y.PodComponent&&x.CollisionComponent==y.CollisionComponent&&x.FixupsOffset==y.FixupsOffset&&x.Fixups.SequenceEqual(y.Fixups)&&x.UsedEnd==y.UsedEnd&&UniversalMobyComposer.ArenaSlots(before)==UniversalMobyComposer.ArenaSlots(actual),"all native scene offsets, fixups, rows and dynamic capacity unchanged");
 }
 internal Loaded BindLoaded(byte[] finalExe,byte[] finalOverlay,NativeLevelRuntimeMemoryReport nativeDestinationOwner)
 {
  var data=new UniversalLevelData(CopyLevelData());RequireFinal(data);_world.Source.RequireNativeOwners(finalExe,true);
  UniversalGreenDruidGroup1TerrainQueues.Require(nativeDestinationOwner.NativeSourceCertified&&nativeDestinationOwner.LevelId==20&&nativeDestinationOwner.NormalReservedBufferStart==UniversalMobyComposer.NormalBufferFloor,"source-authenticated original normal destination buffer owner");
  var bindings=UniversalExeBindings.Read(finalExe,20);UniversalGreenDruidGroup1TerrainQueues.Require(bindings.TryGetValue(UniversalExeBindings.CopyBufferGlobal,out var b)&&b.Value==UniversalOverlay.Base+finalOverlay.Length,"actual final initializer copy-buffer anchor follows complete emitted overlay allocation");
  uint copy=b!.Value,end=UniversalMobyComposer.RuntimeEnd(data,copy),scene=checked(end-(uint)data.Subfiles()[3].Length);
  UniversalGreenDruidGroup1TerrainQueues.Require(end+(ulong)UniversalMobyComposer.RamSafetyMargin<=nativeDestinationOwner.NormalReservedBufferStart,"unchanged native normal buffer and composer safety margin");
  uint close=checked(scene+(uint)SceneTailOffset),normal=checked(close+(uint)CloseByteLength);UniversalGreenDruidGroup1TerrainQueues.Require(normal+(ulong)FinalByteLength<=end&&close%4==0&&normal%4==0,"disjoint complete close/final owners with final prefetch");
  return new(this,copy,scene,close,normal,end);
 }
 internal sealed class Loaded
 {
  internal GreenDruidGroup1TerrainQueueSceneStorage Storage{get;}
  internal uint CopyBuffer{get;}
  internal uint Scene{get;}
  internal uint Close{get;}
  internal uint Final{get;}
  internal uint End{get;}
  internal Loaded(GreenDruidGroup1TerrainQueueSceneStorage storage,uint copy,uint scene,uint close,uint final,uint end){Storage=storage;CopyBuffer=copy;Scene=scene;Close=close;Final=final;End=end;}
 }
}
