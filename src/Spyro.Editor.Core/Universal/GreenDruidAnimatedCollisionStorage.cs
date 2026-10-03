using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
namespace Spyro.Editor.Core.Universal;

/// <summary>Detached source-owned append of the complete counted animated triangle track.
/// Does not admit world import, change a host, or certify queue/renderer/runtime.</summary>
internal sealed class GreenDruidAnimatedCollisionStorage
{
 private readonly UniversalGreenDruidWorldPrerequisites _source;
 private readonly byte[] _before,_after;
 private readonly int[] _delta;
 internal int FirstTriangle {get;}
 internal int TriangleCount=>_source.CollisionTriangleCount+_source.FixedCollisionTriangleCount;
 internal int AddedCells{get;}
 internal int AddedReferences{get;}
 internal int UsedBlockBytes{get;}
 internal bool ForeignPlacementAdmitted=>false;
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static uint U(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static void Require(bool b,string why){if(!b)throw new InvalidDataException("Green Druid detached animated collision: "+why);}
 internal sealed record Layout(int Count,int FlagCount,int Tree,int Blocks,int Triangles,int Assignment,int Flags,int UsedBlocks);
 internal static Layout Read(byte[] component)
 {
  Require(component.Length>=36&&I(component,0)==component.Length&&component.Length%4==0,"complete component extent");
  int count=I(component,4),fc=I(component,8),tree=checked(4+I(component,12)),blocks=checked(4+I(component,16)),tri=checked(4+I(component,20)),assignment=checked(4+I(component,24)),flags=checked(4+I(component,28));
  Require(count is >=0 and <=32768&&fc>=0&&fc<=count&&tree==32&&blocks>=tree+4&&tri>=blocks+4&&assignment==tri+count*12&&flags>=assignment+count&&flags+fc<=component.Length,"native containing partitions");
  int used=tri-blocks;while(used>=4&&BinaryPrimitives.ReadUInt16LittleEndian(component.AsSpan(blocks+used-2))==0)used-=2;
  Require(used>=2,"native terminal collision group");
  _=NativeCollisionIndexCodec.Decode(component.AsSpan(tree,blocks-tree),component.AsSpan(blocks,tri-blocks),used,count,false);
  return new(count,fc,tree,blocks,tri,assignment,flags,used);
 }
 internal static byte[] Component(UniversalLevelData data)
 {
  var sf=data.Subfiles()[1];int at=sf.Offset;
  for(int n=0;n<4;n++){int size=I(data.Bytes,at);Require(size>=4&&size%4==0&&at+size<=sf.Offset+sf.Length,"world component owner");at+=size;}
  int end=I(data.Bytes,at);Require(end>=36&&at+end<=sf.Offset+sf.Length,"collision component owner");return data.Bytes.AsSpan(at,end).ToArray();
 }
 internal static byte[] Empty()
 {
  byte[] b=new byte[40];W(b,0,40);W(b,12,28);W(b,16,32);W(b,20,36);W(b,24,36);W(b,28,36);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(36),0x8000);return b;
 }
 private static int Signed9(uint x){int v=(int)(x&511);return v>=256?v-512:v;}
 internal static int[][] Points(byte[] frame,int triangle)
 {
  uint[] w=Enumerable.Range(0,3).Select(a=>U(frame,triangle*12+a*4)).ToArray();int[] a=w.Select(v=>(int)(v&16383)).ToArray();
  return [a,[a[0]+Signed9(w[0]>>14),a[1]+Signed9(w[1]>>14),a[2]+(int)((w[2]>>16)&255)],[a[0]+Signed9(w[0]>>23),a[1]+Signed9(w[1]>>23),a[2]+(int)(w[2]>>24)]];
 }
 private GreenDruidAnimatedCollisionStorage(UniversalGreenDruidWorldPrerequisites source,byte[] before,int[]delta)
 {
  _source=source;_before=before.ToArray();_delta=delta.ToArray();var l=Read(_before);Require(l.Count+TriangleCount<=32768,"15-bit native triangle IDs exhausted");FirstTriangle=l.Count;
  var index=NativeCollisionIndexCodec.Decode(_before.AsSpan(l.Tree,l.Blocks-l.Tree),_before.AsSpan(l.Blocks,l.Triangles-l.Blocks),l.UsedBlocks,l.Count,false);
  var cells=index.Cells.ToDictionary(p=>p.Key,p=>p.Value.OrderedTriangleIndexes.ToList());var frames=source.CopyCanonicalCollisionFrames(_delta);var fixedFrame=source.CopyFixedCollision(_delta);var allFrames=frames.Select(f=>f.Concat(fixedFrame).ToArray()).ToArray();
  Require(frames.Length==source.CollisionFrameCount&&frames.All(f=>f.Length==source.CollisionTriangleCount*12),"complete native canonical source frames");int refs=0;
  for(int t=0;t<TriangleCount;t++)
  {
   int[] min=[int.MaxValue,int.MaxValue,int.MaxValue],max=[int.MinValue,int.MinValue,int.MinValue];
   foreach(var f in allFrames)foreach(var p in Points(f,t))for(int a=0;a<3;a++){Require(p[a] is >=0 and <16384,"complete moving vertices in native bounds");min[a]=Math.Min(min[a],p[a]);max[a]=Math.Max(max[a],p[a]);}
   // Native GPF/GPL weighted interpolation remains within endpoint hull. One
   //whole-unit conservative index margin covers packed integer boundaries;
   //false positives still receive the unchanged native triangle query.
   for(int a=0;a<3;a++){min[a]=Math.Max(0,min[a]-1);max[a]=Math.Min(16383,max[a]+1);}
   for(int z=min[2]>>8;z<=max[2]>>8;z++)for(int y=min[1]>>8;y<=max[1]>>8;y++)for(int x=min[0]>>8;x<=max[0]>>8;x++)
   {var cell=new NativeCollisionCell(x,y,z);if(!cells.TryGetValue(cell,out var rows)){rows=[];cells.Add(cell,rows);}rows.Add(l.Count+t);refs++;}
  }
  var result=NativeCollisionIndexCodec.Create(cells.Select(p=>new KeyValuePair<NativeCollisionCell,IReadOnlyList<int>>(p.Key,p.Value.Where(i=>i>=l.Count).OrderDescending().Concat(p.Value.Where(i=>i<l.Count)).ToArray())),l.Count+TriangleCount,false);
  var tree=result.CopyTree();var blocks=result.CopyBlocks();UsedBlockBytes=result.UsedBlockBytes;AddedCells=cells.Count-index.Cells.Count;AddedReferences=refs;
  int count=l.Count+TriangleCount,ta=32+tree.Length+blocks.Length,aa=ta+count*12,fa=aa+UniversalLevelData.Align(count,4);byte[] b=new byte[fa+UniversalLevelData.Align(count,4)];
  W(b,0,b.Length);W(b,4,count);W(b,8,count);W(b,12,28);W(b,16,28+tree.Length);W(b,20,ta-4);W(b,24,aa-4);W(b,28,fa-4);
  tree.CopyTo(b,32);blocks.CopyTo(b,32+tree.Length);_before.AsSpan(l.Triangles,l.Count*12).CopyTo(b.AsSpan(ta));allFrames[0].CopyTo(b,ta+l.Count*12);
  _before.AsSpan(l.Assignment,l.Count).CopyTo(b.AsSpan(aa));b.AsSpan(aa+l.Count,TriangleCount).Fill(255);b.AsSpan(fa,count).Fill(255);_before.AsSpan(l.Flags,l.FlagCount).CopyTo(b.AsSpan(fa));b.AsSpan(fa+l.Count,source.CollisionTriangleCount).Fill(63);
  _after=b;ValidateReadback(b);
 }
 internal static GreenDruidAnimatedCollisionStorage Capture(string key,UniversalLevelData source,UniversalOverlay overlay,byte[]exe,byte[] before,IReadOnlyList<int>deltaRaw,int sourceDruidRow=5)
 {var issued=UniversalGreenDruidWorldPrerequisites.CaptureSource(key,source,overlay,exe,sourceDruidRow);return new(issued,before,deltaRaw.ToArray());}
 internal byte[] CopyComponent()=>_after.ToArray();
 internal byte[][] CopyFrames()=>_source.CopyCanonicalCollisionFrames(_delta);
 internal int[] CopyDelta()=>_delta.ToArray();
 internal string BeforeHash=>H(_before);
 internal void ValidateReadback(byte[] bytes)
 {
  Require(bytes.AsSpan().SequenceEqual(_after),"issued final bytes changed");var old=Read(_before);var final=Read(bytes);
  Require(final.Count==old.Count+TriangleCount&&final.FlagCount==final.Count,"native triangle and flag counts");
  Require(_before.AsSpan(old.Triangles,old.Count*12).SequenceEqual(bytes.AsSpan(final.Triangles,old.Count*12))&&_before.AsSpan(old.Assignment,old.Count).SequenceEqual(bytes.AsSpan(final.Assignment,old.Count))&&_before.AsSpan(old.Flags,old.FlagCount).SequenceEqual(bytes.AsSpan(final.Flags,old.FlagCount)),"retained triangles/assignments/flags");
  var original=NativeCollisionIndexCodec.Decode(_before.AsSpan(old.Tree,old.Blocks-old.Tree),_before.AsSpan(old.Blocks,old.Triangles-old.Blocks),old.UsedBlocks,old.Count,false);
  var output=NativeCollisionIndexCodec.Decode(bytes.AsSpan(final.Tree,final.Blocks-final.Tree),bytes.AsSpan(final.Blocks,final.Triangles-final.Blocks),final.UsedBlocks,final.Count,false);
  Require(original.Cells.All(p=>output.Cells.TryGetValue(p.Key,out var cell)&&cell.OrderedTriangleIndexes.Where(i=>i<old.Count).SequenceEqual(p.Value.OrderedTriangleIndexes)),"all native retained cell sequences preserved");
 }
}
