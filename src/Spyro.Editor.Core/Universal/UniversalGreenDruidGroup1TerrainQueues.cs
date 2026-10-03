using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact Alpine6/52 terrain-worklist ownership. This proves queue storage,
/// not GPU packet capacity or general renderer/gameplay admission.</summary>
internal sealed class UniversalGreenDruidGroup1TerrainQueues
{
 internal const uint RendererStart=0x800258F0,RendererEnd=0x8002A6FC;
 internal const uint TableStart=0x8006CF48,TableEnd=0x8006D618;
 private readonly byte[] _exe;
 private UniversalGreenDruidGroup1TerrainQueues(byte[] exe){_exe=exe.ToArray();}
 internal byte[] CopyExecutable()=>_exe.ToArray();
 internal static int I(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b[o..]);
 internal static uint U(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
 internal static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 internal static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException("Exact Group1 terrain queues: "+reason);}
 internal static UniversalGreenDruidGroup1TerrainQueues CaptureSource(string key,UniversalLevelData data,byte[] exe)
 {
  Require(key=="alpineridge"&&Hash(data.Bytes)=="507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf"&&Hash(exe)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","complete immutable original USA data and EXE");
  // Authenticate the exact recursive subdivision grammar used by the finite bound.
  uint[] tri=[0x00103000,0x10304000,0x10204000,0x30405000,0x00201001,0x20504001,0x50003001];
  uint[] quad=[0x00103000,0x10304000,0x10204000,0x20405000,0x30406000,0x40607000,0x40507000,0x50708000,0x00201001,0x20805001,0x80607001,0x60003001];
  Require(tri.Select((v,n)=>U(exe,(int)(0x8006D5C8-UniversalExeBindings.ExeBase)+4*n)==v).All(v=>v)&&quad.Select((v,n)=>U(exe,(int)(0x8006D5E4-UniversalExeBindings.ExeBase)+4*n)==v).All(v=>v),"native four/eight-child recursive tables and span-exempt seams");
  return new(exe);
 }
 internal void RequireNativeOwners(byte[] exe,bool entryPatched=false)
 {
  int first=(int)(RendererStart-UniversalExeBindings.ExeBase)+(entryPatched?8:0),end=(int)(RendererEnd-UniversalExeBindings.ExeBase);
  Require(exe.Length==_exe.Length&&exe.AsSpan(first,end-first).SequenceEqual(_exe.AsSpan(first,end-first)),"complete original renderer instructions, GTE operations, control transfers and delay slots");
  int t=(int)(TableStart-UniversalExeBindings.ExeBase);Require(exe.AsSpan(t,(int)(TableEnd-TableStart)).SequenceEqual(_exe.AsSpan(t,(int)(TableEnd-TableStart))),"all original renderer subdivision/interpolation tables");
  foreach(var range in new[]{(Start:0x80037E2Cu,End:0x80037E5Cu),(Start:0x8002B9CCu,End:0x8002BA50u)}){int at=(int)(range.Start-UniversalExeBindings.ExeBase),size=(int)(range.End-range.Start);Require(exe.AsSpan(at,size).SequenceEqual(_exe.AsSpan(at,size)),"original camera-count normalization and renderer admission consumers");}
  int loader=(int)(0x80013B00-UniversalExeBindings.ExeBase);Require(exe.AsSpan(loader,0x500).SequenceEqual(_exe.AsSpan(loader,0x500)),"original model/scene loader and arena initialization consumers");
 }
 internal BoundWorld Bind(GreenDruidGroup1WorldStorage storage)=>new(this,storage);
 internal sealed record ContextBound(int Context,int Sectors,int Triangles,int Quads,int CloseWords,int FinalWords);
 internal sealed class BoundWorld
 {
  private readonly GreenDruidGroup1WorldStorage _storage;
  private readonly byte[][] _world,_banks;
  private readonly ContextBound[] _bounds;
  internal UniversalGreenDruidGroup1TerrainQueues Source{get;}
  internal int CloseWords=>_bounds.Max(b=>b.CloseWords);
  internal int FinalWords=>_bounds.Max(b=>b.FinalWords);
  internal int RequiredBytes=>checked(4*(CloseWords+FinalWords));
  internal int ReservationBytes=>UniversalLevelData.Align(RequiredBytes,2048);
  internal IReadOnlyList<ContextBound> Contexts=>_bounds.ToArray();
  internal bool GpuCapacityCertified=>false;
  internal bool ForeignPlacementAdmitted=>false;
  internal BoundWorld(UniversalGreenDruidGroup1TerrainQueues source,GreenDruidGroup1WorldStorage storage)
  {
   Source=source;_storage=storage;storage.RequireDraftOwnership();Require(Hash(storage.Proof.CopySourceLevelData())=="507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf","independently issued complete source resource proof");
   var data=new UniversalLevelData(storage.CopyDraftLevelData());_world=GreenDruidCompleteWorldStorage.World(data);_banks=UniversalGreenDruidGroup1WorldRebinding.Banks(data);
   var sectors=GreenDruidCompleteWorldStorage.Sectors(_world[1]);var imported=storage.Binding.DestinationSectors;
   Require(imported.Length==6&&imported.Distinct().Count()==6&&sectors.Length==imported.Length+storage.FirstSector,"six whole source sectors plus complete host");
   int hostFaces=sectors.Take(storage.FirstSector).Sum(s=>(int)s[22]);Require(hostFaces<=25&&storage.FirstSector<=25,"bounded whole authored host (no face thinning)");
   int sourceQ=0,sourceT=0;foreach(int id in imported){var counts=Shape(sectors[id]);sourceQ+=counts.Q;sourceT+=counts.T;}Require(sourceQ==198&&sourceT==23,"all221 exact authentic face kinds");
   int vis=4+I(_world[2],4),count=I(_world[2],8);Require(count==3&&I(_world[2],vis)==3,"exact complete parallel source/host contexts");List<ContextBound> bounds=[];
   bounds.Add(Bound(-1,Enumerable.Range(0,sectors.Length).ToArray(),sectors));
   for(int context=0;context<count;context++){int start=4+I(_world[2],12+4*context),end=context+1<count?4+I(_world[2],16+4*context):vis;var bytes=_world[2].AsSpan(start,end-start);int stop=bytes.IndexOf((byte)255);Require(stop>=0&&bytes[stop..].IndexOfAnyExcept((byte)255)<0,"authentic FF visibility terminator and suffix");int[] ids=bytes[..stop].ToArray().Select(x=>(int)x).ToArray();Require(ids.Distinct().Count()==ids.Length&&ids.All(id=>id<sectors.Length),"complete unique sector context");bounds.Add(Bound(context,ids,sectors));}
   _bounds=bounds.ToArray();Require(CloseWords<=4*128&&FinalWords>CloseWords&&ReservationBytes<=0xA0000,"separate finite close/final worklist owners; no original queue bypass");RequireFinal(data);
  }
  private static (int T,int Q) Shape(byte[] s)
  {
   int at=28+4*s[23]+4*s[20]+8*s[21],t=0,q=0;Require(at+16*s[22]<=s.Length,"complete high face words");for(int n=0;n<s[22];n++){uint w=U(s,at+16*n);if((w&255)==((w>>8)&255))t++;else q++;}return(t,q);
  }
  private static ContextBound Bound(int context,int[] ids,byte[][] sectors)
  {
   int t=0,q=0,headers=0;foreach(int id in ids){var s=sectors[id];var shape=Shape(s);t+=shape.T;q+=shape.Q;if(s[22]!=0)headers++;Require(s[16]<=170&&s[20]<=170,"unchanged native vertex-cache capacity");}
   // RTPS SXY is clamped to [-1024,1023]. Every affine endpoint/midpoint
   // interpolation halves coordinate spans (ceil). With original child tests
   // |dy|>=512 or |dx|>=1024, no level3 child can be queued. A triangle
   // contributes <=1+4+16=21 and a quad <=1+8+32=41 queued nodes.
   // Original close table has16 roots per source face. Normal faces already
   // in the final queue contribute <=1 each, thus the close bound dominates.
   // Final consumer prefetches one word even when RA==SP; that word is owned.
   return new(context,ids.Length,t,q,checked(headers+t+q+1),checked(16*(21*t+41*q)+1));
  }
  internal void RequireStorage(GreenDruidGroup1WorldStorage storage){Require(ReferenceEquals(storage,_storage),"issued storage identity");storage.RequireDraftOwnership();}
  internal void RequireFinal(UniversalLevelData data)
  {
   var world=GreenDruidCompleteWorldStorage.World(data);Require(world.Length==_world.Length&&world.Select((b,n)=>b.SequenceEqual(_world[n])).All(v=>v),"all complete world components, materials, visibility, fixed/moving collision and source geometry");var banks=UniversalGreenDruidGroup1WorldRebinding.Banks(data);Require(banks.Select((b,n)=>b.SequenceEqual(_banks[n])).All(v=>v),"all seven complete native command/frame banks");
  }
 }
}
