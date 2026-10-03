using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;
/// <summary>Exact Alpine18 sector138 collision provenance, after full USA packet authentication.
/// The native raised visual sides10/11 have no corresponding raised collision.
/// Retains the complete original18 moving and13 fixed triangles without inventing sides.</summary>
internal static class GreenDruidRampCollisionOwnership
{
 private static uint U(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("Green Druid original ramp collision: "+why);}
 private static int S9(uint v){int n=(int)(v&511);return n>=256?n-512:n;}
 internal static void Require(byte[]sector,byte[]hpRecord,byte[]collisionRecord,byte[]component,int[]fixedIds)
 {
  int[] issued=[4020,4021,4022,4023,4032,4033,4034,4039,4040,4063,4064,4070,4077];
  Require(fixedIds.SequenceEqual(issued)&&sector[20]==27&&sector[16]==22&&sector[22]==17&&sector[18]==15&&hpRecord.Length==268&&collisionRecord.Length==572,"exact issued complete topology");
  int vertex=28+sector[23]*4,face=vertex+sector[20]*4+sector[21]*8,tri=4+(int)U(component,20);int[] origin=[(int)(U(sector,8)>>16),(int)(U(sector,8)&65535),(int)(U(sector,12)>>16)];
  int[] Point(uint w)=>[origin[0]+(int)(w>>21),origin[1]+(int)((w>>10)&2047),origin[2]+(int)(w&1023)];
  int[][] BasePoints()=>Enumerable.Range(0,27).Select(n=>Point(U(sector,vertex+n*4))).ToArray();
  int[][] FramePoints(int frame)=>Enumerable.Range(0,27).Select(n=>Point(U(n<16?hpRecord:sector,n<16?140+frame*64+n*4:vertex+n*4))).ToArray();
  int[] FaceIds(int f)=>sector.AsSpan(face+f*16,4).ToArray().Select(v=>(int)v).ToArray();
  bool Matches(int[][]t,int[]ids,int[][]points)=>t.All(p=>ids.Any(i=>p.SequenceEqual(points[i])));
  int[][] Moving(int frame,int n){uint[]w=Enumerable.Range(0,3).Select(a=>U(collisionRecord,140+frame*216+n*12+a*4)).ToArray();int[]a=w.Select(v=>(int)(v&16383)).ToArray();return[a,w.Select((v,i)=>a[i]+S9(v>>14)).ToArray(),w.Select((v,i)=>a[i]+S9(v>>23)).ToArray()];}
  int[][] Fixed(int n)=>GreenDruidAnimatedCollisionStorage.Points(component.AsSpan(tri+n*12,12).ToArray(),0);
  int[][] fixedByFace=[[4070,4077],[4063,4064],[4020,4021],[4022,4023],[4032,4040],[4039],[4033,4034]];
  for(int f=10;f<17;f++)
  {
   var ids=FaceIds(f);Require(ids.All(i=>i<27),"all native fixed/base face vertices bounded");Require(f<12?ids.Any(i=>i<16):ids.All(i=>i>=16),"authentic animated visual sides and five wholly fixed faces");
   var found=issued.Where(n=>Matches(Fixed(n),ids,BasePoints())).Order().ToArray();Require(found.SequenceEqual(fixedByFace[f-10].Order()),"every exact original fixed/base triangle and repeated-vertex face");Require(ids.Distinct().Count()==(f==15?3:4),"authentic triangle/quad grammar");
  }
  for(int geometryFrame=0;geometryFrame<2;geometryFrame++)
  {
   var points=FramePoints(geometryFrame);int collisionFrame=1-geometryFrame;HashSet<int> all=[];
   for(int f=0;f<10;f++)
   {
    var ids=FaceIds(f);Require(ids.All(i=>i<16),"complete animated-prefix face owner");var found=Enumerable.Range(0,18).Where(n=>Matches(Moving(collisionFrame,n),ids,points)).ToArray();Require(found.Length==(f<2?1:2),"every native animated triangle under opposite source frame order");all.UnionWith(found);
   }
   Require(all.SetEquals(Enumerable.Range(0,18)),"all18 source animated triangles accounted for");
   if(geometryFrame==1)for(int f=10;f<12;f++)Require(!Enumerable.Range(0,18).Any(n=>Matches(Moving(collisionFrame,n),FaceIds(f),points))&&!issued.Any(n=>Matches(Fixed(n),FaceIds(f),points)),"source raised visual side asymmetry retained; no substitute collider");
  }
 }
}
