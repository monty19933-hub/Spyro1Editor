using System.Buffers.Binary;
using Spyro.Editor.Core.Scene;
namespace Spyro.Editor.Core.Universal;

/// <summary>Complete two-track collision append issued by the original linked-world
/// certificate. Every endpoint contributes to the spatial index; native queries
/// retain their original triangle geometry and ordering.</summary>
internal sealed class GreenDruidLinkedCollisionStorage
{
 private readonly byte[] _before,_after;
 internal int FirstTriangle {get;}
 internal int FirstFixedTriangle=>FirstTriangle+20;
 internal int AddedReferences {get;}
 internal int AddedCells {get;}
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid linked collision: "+why);}
 internal GreenDruidLinkedCollisionStorage(UniversalGreenDruidLinkedWorldPrerequisites proof,byte[] before,IReadOnlyList<int> delta,UniversalGreenDruidLinkedWorldPrerequisites.Rebinding binding)
 {
  _before=before.ToArray();var l=GreenDruidAnimatedCollisionStorage.Read(_before);FirstTriangle=l.Count;
  Require(binding.FirstTriangles.SequenceEqual(new[]{l.Count,l.Count+10})&&binding.FirstFixedTriangle==l.Count+20&&l.Count+183<=32768,"actual contiguous complete appended intervals");
  Require(binding.AssignmentBytes.Length==183&&binding.AssignmentBytes.All(b=>b is 1 or 255)&&binding.SurfaceFlags.Length==183,"issued context and surface arrays");
  var groups=proof.CopyCanonicalCollisionFrames(delta);var fixedFrame=proof.CopyFixedCollision(delta);
  Require(groups.Count==2&&groups.Select(g=>g.SourceGroup).SequenceEqual(new[]{8,9})&&groups.All(g=>g.Frames.All(f=>f.Length==120))&&fixedFrame.Length==163*12,"complete two canonical tracks and fixed closure");
  var initial=groups[0].Frames[0].Concat(groups[1].Frames[0]).Concat(fixedFrame).ToArray();
  var oldIndex=NativeCollisionIndexCodec.Decode(_before.AsSpan(l.Tree,l.Blocks-l.Tree),_before.AsSpan(l.Blocks,l.Triangles-l.Blocks),l.UsedBlocks,l.Count,false);
  var cells=oldIndex.Cells.ToDictionary(p=>p.Key,p=>p.Value.OrderedTriangleIndexes.ToList());int references=0;
  for(int triangle=0;triangle<183;triangle++)
  {
   var frames=triangle<10?groups[0].Frames:triangle<20?groups[1].Frames:new[]{fixedFrame};int local=triangle<10?triangle:triangle<20?triangle-10:triangle-20;
   int[] min=[int.MaxValue,int.MaxValue,int.MaxValue],max=[int.MinValue,int.MinValue,int.MinValue];
   foreach(var frame in frames)foreach(var point in GreenDruidAnimatedCollisionStorage.Points(frame,local))for(int axis=0;axis<3;axis++){Require(point[axis] is >=0 and <16384,"whole native endpoint bounds");min[axis]=Math.Min(min[axis],point[axis]);max[axis]=Math.Max(max[axis],point[axis]);}
   // The original integer interpolation remains in its endpoint hull. A one-unit
   // index margin includes packed-boundary rounding without changing collision.
   for(int axis=0;axis<3;axis++){min[axis]=Math.Max(0,min[axis]-1);max[axis]=Math.Min(16383,max[axis]+1);}
   for(int z=min[2]>>8;z<=max[2]>>8;z++)for(int y=min[1]>>8;y<=max[1]>>8;y++)for(int x=min[0]>>8;x<=max[0]>>8;x++)
   {var key=new NativeCollisionCell(x,y,z);if(!cells.TryGetValue(key,out var rows)){rows=[];cells.Add(key,rows);}rows.Add(l.Count+triangle);references++;}
  }
  var packed=NativeCollisionIndexCodec.Create(cells.Select(p=>new KeyValuePair<NativeCollisionCell,IReadOnlyList<int>>(p.Key,p.Value.Where(t=>t>=l.Count).OrderDescending().Concat(p.Value.Where(t=>t<l.Count)).ToArray())),l.Count+183,false);
  var tree=packed.CopyTree();var blocks=packed.CopyBlocks();int count=l.Count+183,ta=32+tree.Length+blocks.Length,aa=ta+count*12,fa=aa+UniversalLevelData.Align(count,4);
  byte[] output=new byte[fa+UniversalLevelData.Align(count,4)];W(output,0,output.Length);W(output,4,count);W(output,8,count);W(output,12,28);W(output,16,28+tree.Length);W(output,20,ta-4);W(output,24,aa-4);W(output,28,fa-4);
  tree.CopyTo(output,32);blocks.CopyTo(output,32+tree.Length);_before.AsSpan(l.Triangles,l.Count*12).CopyTo(output.AsSpan(ta));initial.CopyTo(output,ta+l.Count*12);
  _before.AsSpan(l.Assignment,l.Count).CopyTo(output.AsSpan(aa));binding.AssignmentBytes.CopyTo(output,aa+l.Count);output.AsSpan(fa,count).Fill(255);_before.AsSpan(l.Flags,l.FlagCount).CopyTo(output.AsSpan(fa));binding.SurfaceFlags.CopyTo(output,fa+l.Count);
  _after=output;AddedReferences=references;AddedCells=cells.Count-oldIndex.Cells.Count;ValidateReadback(output);
 }
 internal byte[] CopyComponent()=>_after.ToArray();
 internal void ValidateReadback(byte[] actual)
 {
  Require(actual.AsSpan().SequenceEqual(_after),"issued complete collision bytes");var old=GreenDruidAnimatedCollisionStorage.Read(_before);var final=GreenDruidAnimatedCollisionStorage.Read(actual);
  Require(final.Count==old.Count+183&&final.FlagCount==final.Count,"complete count and flags");Require(_before.AsSpan(old.Triangles,old.Count*12).SequenceEqual(actual.AsSpan(final.Triangles,old.Count*12))&&_before.AsSpan(old.Assignment,old.Count).SequenceEqual(actual.AsSpan(final.Assignment,old.Count))&&_before.AsSpan(old.Flags,old.FlagCount).SequenceEqual(actual.AsSpan(final.Flags,old.FlagCount)),"every retained triangle/assignment/flag");
  var a=NativeCollisionIndexCodec.Decode(_before.AsSpan(old.Tree,old.Blocks-old.Tree),_before.AsSpan(old.Blocks,old.Triangles-old.Blocks),old.UsedBlocks,old.Count,false);var b=NativeCollisionIndexCodec.Decode(actual.AsSpan(final.Tree,final.Blocks-final.Tree),actual.AsSpan(final.Blocks,final.Triangles-final.Blocks),final.UsedBlocks,final.Count,false);
  Require(a.Cells.All(p=>b.Cells.TryGetValue(p.Key,out var c)&&c.OrderedTriangleIndexes.Where(t=>t<old.Count).SequenceEqual(p.Value.OrderedTriangleIndexes)),"retained ordered collision cell membership");
 }
}
