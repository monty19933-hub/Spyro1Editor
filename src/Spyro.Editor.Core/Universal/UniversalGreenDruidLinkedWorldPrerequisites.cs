using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact inert Alpine11/44/53 source-resource certificate. Six entire sectors,
/// two complete groups, both counted paths and original collision owners. This grants
/// no actor/particle/model admission or original-world guard exception.</summary>
public sealed class UniversalGreenDruidLinkedWorldPrerequisites
{
 private readonly UniversalLevelData _source;
 private readonly byte[][] _world,_banks,_sectors,_records;
 private readonly UniversalRowTemplate[] _actors;
 private readonly int[] _context20;
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static uint U(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid linked world prerequisite: "+why);}
 private static int S9(uint v){int n=(int)(v&511);return n>=256?n-512:n;}
 public bool ForeignPlacementAdmitted=>false;
 public IReadOnlyList<int> RequiredSourceRows=>GreenDruidLinkedWorldSourceProfile.Rows.ToArray();
 public IReadOnlyList<int> RequiredSourceSectors=>GreenDruidLinkedWorldSourceProfile.Sectors.ToArray();
 public IReadOnlyList<int> RequiredSourceMaterials=>GreenDruidLinkedWorldSourceProfile.Materials.ToArray();
 public IReadOnlyList<int> RequiredFixedSourceTriangles=>GreenDruidLinkedWorldSourceProfile.FixedTriangles.ToArray();
 public IReadOnlyList<int> SourceGroups=>new[]{8,9};
 public int FixedCollisionTriangleCount=>163;
 public int CollisionTriangleCount=>20;
 public int HighQueueContentWords=>100;
 public IReadOnlyList<int> ProjectedSourceContextOrder=>_context20.Where(RequiredSourceSectors.Contains).ToArray();
 public sealed record TrackDescriptor(int Group,int Role,int SourceRecordIndex,int SourceTarget,int Commands,int Frames,int Stride,int FrameOffset);
 public IReadOnlyList<TrackDescriptor> Tracks=>GreenDruidLinkedWorldSourceProfile.Tracks.Select(t=>new TrackDescriptor(t.Group,t.Role,t.Index,t.Target,t.Commands,t.Frames,t.Stride,t.FrameOffset)).ToArray();
 public sealed record TranslatedActor(int SourceRow,byte[] Row,UniversalRowTemplate Template);
 public sealed record Translation(int[] DeltaRaw,IReadOnlyList<TranslatedActor> Actors,byte[][] Sectors,byte[][] AnimationRecords,int[] Roles);
 public sealed record CanonicalFrames(int SourceGroup,int SourceFirstTriangle,byte[][] Frames);
 public sealed record RecordBinding(int SourceGroup,int DestinationGroup,int Role,int SourceRecordIndex,int DestinationRecordIndex);
 public sealed record Rebinding(Translation Resources,int[] DestinationGroups,int[] DestinationSectors,int[] FirstTriangles,int FirstFixedTriangle,IReadOnlyList<RecordBinding> Records,byte[] AssignmentBytes,byte[] SurfaceFlags,byte[] VisibilityComponent);

 private static byte[][] World(UniversalLevelData data)
 {
  var sf=data.Subfiles()[1];int at=sf.Offset;List<byte[]> result=[];
  for(int n=0;n<6;n++){int size=I(data.Bytes,at);Require(size>=4&&size%4==0&&at+(long)size<=sf.Offset+sf.Length,"six complete world components");result.Add(data.Bytes.AsSpan(at,size).ToArray());at+=size;}return result.ToArray();
 }
 private static byte[][] Banks(UniversalLevelData data)
 {
  var sf=data.Subfiles()[3];int at=sf.Offset+0x88;List<byte[]> result=[];
  for(int n=0;n<7;n++){int size=I(data.Bytes,at),count=I(data.Bytes,at+4);Require(size>=8&&size%4==0&&count>=0&&8L+4L*count<=size&&at+(long)size<=sf.Offset+sf.Length,"seven complete scene banks");byte[] b=data.Bytes.AsSpan(at,size).ToArray();for(int r=0;r<count;r++){int p=4+I(b,8+r*4);Require(p>=8+count*4&&p%4==0&&p<b.Length,"bank record pointer");}result.Add(b);at+=size;}Require(at==sf.Offset+data.Scene().MobyComponent,"complete scene prefix");return result.ToArray();
 }
 private static byte[] Record(byte[] bank,int index)
 {int count=I(bank,4);Require(index>=0&&index<count,"source record ordinal");int start=4+I(bank,8+index*4),end=Enumerable.Range(0,count).Select(n=>4+I(bank,8+n*4)).Where(p=>p>start).DefaultIfEmpty(bank.Length).Min();Require(end>start,"whole source record");return bank.AsSpan(start,end-start).ToArray();}
 private static byte[][] Sectors(byte[] env)
 {
  int count=I(env,4);Require(I(env,0)==env.Length&&count is >=0 and <255&&8+4*count<=env.Length,"complete byte-visible sector table");List<byte[]> result=[];
  for(int n=0;n<count;n++){int start=4+I(env,8+n*4),end=n+1<count?4+I(env,12+n*4):env.Length;Require(start>=8+4*count&&end>=start+28&&end<=env.Length&&start%4==0&&end%4==0,"whole sector pointer span");byte[] s=env.AsSpan(start,end-start).ToArray();Require(s[16]<=170&&s[20]<=170&&28+4*s[23]+4*s[20]+8*s[21]+16*s[22]<=s.Length,"complete projected vertex/color/face owners");result.Add(s);}return result.ToArray();
 }
 private static int[][] Lists(byte[] vis,bool sky)
 {
  Require(I(vis,0)==vis.Length,"complete dual visibility component");int boundary=4+I(vis,4);Require(boundary>=12&&boundary<=vis.Length-4,"parallel sky list owner");int count=I(vis,sky?boundary:8),table=sky?boundary+4:12,offset=sky?boundary:4,endOwner=sky?vis.Length:boundary;Require(count is >=0 and <=255&&table+4*count<=endOwner,"visibility count/table");List<int[]> lists=[];
  for(int n=0;n<count;n++){int start=offset+I(vis,table+n*4),end=n+1<count?offset+I(vis,table+(n+1)*4):endOwner;Require(start>=table+count*4&&start<end&&end<=endOwner,"terminated visibility span");var raw=vis.AsSpan(start,end-start);int stop=raw.IndexOf((byte)255);Require(stop>=0&&raw[stop..].IndexOfAnyExcept((byte)255)<0,"exact FF terminator and suffix");int[] ids=raw[..stop].ToArray().Select(b=>(int)b).ToArray();Require(ids.Distinct().Count()==ids.Length,"unique visibility owners");lists.Add(ids);}return lists.ToArray();
 }
 private static byte[] Visibility(int[][] terrain,int[][] sky)
 {
  Require(terrain.Length==sky.Length,"two parallel context counts");static byte[] Payload(int[] ids){byte[] b=new byte[UniversalLevelData.Align(ids.Length+1,4)];b.AsSpan().Fill(255);for(int n=0;n<ids.Length;n++)b[n]=checked((byte)ids[n]);return b;}
  var tb=terrain.Select(Payload).ToArray();var sb=sky.Select(Payload).ToArray();int at=12+terrain.Length*4,skyAt=at+tb.Sum(b=>b.Length);byte[] output=new byte[skyAt+4+sky.Length*4+sb.Sum(b=>b.Length)];W(output,0,output.Length);W(output,4,skyAt-4);W(output,8,terrain.Length);
  for(int n=0;n<tb.Length;n++){W(output,12+n*4,at-4);tb[n].CopyTo(output,at);at+=tb[n].Length;}W(output,skyAt,sky.Length);at=skyAt+4+sky.Length*4;
  for(int n=0;n<sb.Length;n++){W(output,skyAt+4+n*4,at-skyAt);sb[n].CopyTo(output,at);at+=sb[n].Length;}return output;
 }
 private UniversalGreenDruidLinkedWorldPrerequisites(UniversalLevelData source)
 {
  _source=new(source.Bytes.ToArray());_world=World(_source);_banks=Banks(_source);var all=Sectors(_world[1]);_sectors=RequiredSourceSectors.Select(id=>all[id]).ToArray();_records=GreenDruidLinkedWorldSourceProfile.Tracks.Select(t=>Record(_banks[t.Role],t.Index)).ToArray();_actors=RequiredSourceRows.Select(row=>_source.CaptureRow("alpineridge",row)).ToArray();_context20=Lists(_world[2],false)[20];
 }
 public static UniversalGreenDruidLinkedWorldPrerequisites CaptureSource(string sourceKey,UniversalLevelData source,UniversalOverlay overlay,byte[] exe)
 {
  Require(sourceKey=="alpineridge"&&H(source.Bytes)==UniversalGreenDruidWorldPrerequisites.DataSha&&H(overlay.Bytes)==UniversalGreenDruidWorldPrerequisites.OverlaySha&&H(exe)==UniversalGreenDruidWorldPrerequisites.ExeSha,"complete immutable original source/native bytes");
  byte[] bindings=new byte[overlay.Bindings.Count*16];int at=0;foreach(var b in overlay.Bindings.OrderBy(b=>b.Key))foreach(uint v in new[]{b.Value.Global,b.Value.LuiAddress,b.Value.AddiuAddress,b.Value.Value}){W(bindings,at,(int)v);at+=4;}Require(overlay.Bindings.All(b=>b.Key==b.Value.Global)&&H(bindings)=="461f8f271b5653a1f4268d41cd3aadba45d27dc24cf06013965da839c2212850","complete callback bindings");
  var proof=new UniversalGreenDruidLinkedWorldPrerequisites(source);proof.RequireSource();return proof;
 }
 private void RequireSource()
 {
  for(int n=0;n<_sectors.Length;n++){var s=_sectors[n];var p=GreenDruidLinkedWorldSourceProfile.SectorOwners[n];Require(s.Length==p.Bytes&&H(s)==p.Sha&&s[16]==p.LPVertices&&s[17]==p.LPColors&&s[18]==p.LPFaces&&s[20]==p.HPVertices&&s[21]==p.HPColors&&s[22]==p.HPFaces&&s.AsSpan(24,4).IndexOfAnyExcept((byte)255)<0,"six exact whole source sectors and untouched ownership caches");}
  for(int n=0;n<_records.Length;n++){var r=_records[n];var t=GreenDruidLinkedWorldSourceProfile.Tracks[n];Require(r.Length==t.Bytes&&H(r)==t.Sha&&r[0]==t.Group&&U(r,4)==t.Target&&I(r,8)==t.FrameOffset&&r.Length==t.FrameOffset+t.Stride*t.Frames&&r[1]==2&&r[2]==0&&r[3]==1,"eight complete original command/keyframe records");for(int cmd=0;cmd<t.Commands;cmd++){int p=12+8*cmd;Require(r[p+2]<t.Commands&&r[p+3]<t.Commands&&r[p+5]<t.Frames&&r[p+6]<t.Frames,"complete counted branches and frames");}}
  List<(int Role,int Index)> incoming=[];
  for(int role=0;role<7;role++)for(int n=0;n<I(_banks[role],4);n++){var r=Record(_banks[role],n);uint target=U(r,4);if(role!=4&&RequiredSourceSectors.Contains((int)(target&65535)))incoming.Add((role,n));if(r[0] is 8 or 9)Require(GreenDruidLinkedWorldSourceProfile.Tracks.Any(t=>t.Role==role&&t.Index==n),"no unowned source group consumer");if(role==4){int first=(int)(target>>16),count=(int)(target&65535);if(first<290&&first+count>270)Require(GreenDruidLinkedWorldSourceProfile.Tracks.Any(t=>t.Role==4&&t.Index==n),"every overlapping collision track");Require(RequiredFixedSourceTriangles.All(id=>id<first||id>=first+count),"every fixed collider outside every moving source interval");}}
  Require(incoming.Count==6&&incoming.All(x=>GreenDruidLinkedWorldSourceProfile.Tracks.Any(t=>t.Role==x.Role&&t.Index==x.Index)),"all six incoming LP/HP and absent static-sector consumers");
  int[] classes=[0x10E,0x146,0x148],lengths=[164,204,4];for(int n=0;n<3;n++)Require(_actors[n].ClassId==classes[n]&&_actors[n].Properties.Length==lengths[n]&&_actors[n].DroppedExternalPointers==0&&_actors[n].ExternalRowPointers.Count==0,"complete three source actor/property owners");
  Require(_actors[0].InternalPointers.SequenceEqual(new[]{(0,92)})&&_actors[1].InternalPointers.SequenceEqual(new[]{(12,52)})&&_actors[2].InternalPointers.Count==0&&_actors[0].Properties[92]==4&&_actors[1].Properties[52]==9,"both full counted source paths");Require(I(_actors[0].Properties,4)==8&&I(_actors[0].Properties,8)==53&&I(_actors[0].Properties,12)==44&&I(_actors[0].Properties,16)==3&&I(_actors[1].Properties,8)==11&&I(_actors[2].Properties,0)==-1,"exact native reciprocal links/mode3/cache-1");
  var terrain=Lists(_world[2],false);var sky=Lists(_world[2],true);Require(terrain.Length==21&&sky.Length==21&&terrain[20].Length==133&&sky[20].Length==51&&ProjectedSourceContextOrder.Count==6&&ProjectedSourceContextOrder.Distinct().Count()==6,"complete original dual context20 ownership and six-sector projection");
  int[] used=_sectors.SelectMany(s=>Enumerable.Range(0,s[22]).Select(f=>(int)(U(s,28+4*s[23]+4*s[20]+8*s[21]+16*f+8)&127))).Distinct().Order().ToArray();Require(used.SequenceEqual(RequiredSourceMaterials),"all nine original HP materials");foreach(var m in CopyMaterials())_=GreenDruidWorldMaterialStorage.Describe(m.Record);
  byte[] assignments=SourceAssignments(),flags=SourceFlags();Require(assignments.All(a=>a is 20 or 255)&&flags.Take(20).All(f=>f==63)&&flags.Skip(20).All(f=>f==255),"authentic assignments separate from explicit/implicit surface flags");Require(RequiredFixedSourceTriangles.Count==163&&RequiredFixedSourceTriangles.Distinct().Count()==163&&_sectors.Sum(s=>s[22])==94&&_sectors.Sum(s=>s[18])==48,"complete source closure dimensions");
 }
 public IReadOnlyList<UniversalGreenDruidWorldPrerequisites.SourceMaterial> CopyMaterials()
 {var table=_world[0];int count=I(table,4);return RequiredSourceMaterials.Select(id=>new UniversalGreenDruidWorldPrerequisites.SourceMaterial(id,[..table.AsSpan(8+id*16,16),..table.AsSpan(8+count*16+id*168,168)])).ToArray();}
 public byte[] CopySourceVram()=>_source.Vram().ToArray();
 private int[] CollisionIds()=>Enumerable.Range(270,20).Concat(RequiredFixedSourceTriangles).ToArray();
 private byte[] SourceAssignments(){var c=_world[4];int at=4+I(c,24);return CollisionIds().Select(id=>c[at+id]).ToArray();}
 private byte[] SourceFlags(){var c=_world[4];int at=4+I(c,28),count=I(c,8);return CollisionIds().Select(id=>id<count?c[at+id]:(byte)255).ToArray();}
 private static int[][] PackedPoints(byte[] raw,bool animated)
 {uint[] words=Enumerable.Range(0,3).Select(a=>U(raw,a*4)).ToArray();int[] a=words.Select(w=>(int)(w&16383)).ToArray(),b=words.Select((w,k)=>a[k]+(k==2&&!animated?(int)((w>>16)&255):S9(w>>14))).ToArray(),c=words.Select((w,k)=>a[k]+(k==2&&!animated?(int)(w>>24):S9(w>>23))).ToArray();return [a,b,c];}
 private static void PointsInWorld(byte[] raw,bool animated){foreach(var p in PackedPoints(raw,animated))Require(p.All(v=>v is >=0 and <16384),"every full decoded collision vertex in world bounds");}
 public byte[] CopyFixedCollision(IReadOnlyList<int> deltaRaw)
 {
  Delta(deltaRaw);var c=_world[4];int at=4+I(c,20);byte[] output=new byte[163*12];for(int n=0;n<163;n++){c.AsSpan(at+RequiredFixedSourceTriangles[n]*12,12).CopyTo(output.AsSpan(n*12));MoveCollision(output,n*12,deltaRaw);PointsInWorld(output.AsSpan(n*12,12).ToArray(),false);}return output;
 }
 private static void Delta(IReadOnlyList<int>d){Require(d.Count==3&&d.All(v=>v%16==0),"common exact sixteen-raw-unit translation");}
 private static void MoveCollision(byte[] b,int at,IReadOnlyList<int>d){for(int axis=0;axis<3;axis++){uint old=U(b,at+axis*4);int value=checked((int)(old&16383)+d[axis]/16);Require(value is >=0 and <16384,"collision base bounds");W(b,at+axis*4,(int)((old&~16383u)|(uint)value));}}
 public Translation CopyTranslated(IReadOnlyList<int> deltaRaw)
 {
  Delta(deltaRaw);var sectors=_sectors.Select(s=>s.ToArray()).ToArray();foreach(var s in sectors){foreach(int at in new[]{0,8}){uint old=U(s,at);int x=checked((int)(old>>16)+deltaRaw[0]/16),y=checked((int)(old&65535)+deltaRaw[1]/16);Require(x is >=0 and <16384&&y is >=0 and <16384,"whole source sector center/origin XY");W(s,at,(x<<16)|y);}foreach(int at in new[]{4,12}){uint old=U(s,at);int z=checked((int)(old>>16)+deltaRaw[2]/16);Require(z is >=0 and <16384,"whole source sector center/origin Z");W(s,at,(z<<16)|(int)(old&65535));}foreach(int vertex in Enumerable.Range(0,s[16]).Select(n=>28+n*4).Concat(Enumerable.Range(0,s[20]).Select(n=>28+4*s[23]+n*4)))GeometryPoint(s,U(s,vertex));}
  var records=_records.Select(r=>r.ToArray()).ToArray();for(int n=0;n<records.Length;n++){var t=GreenDruidLinkedWorldSourceProfile.Tracks[n];var r=records[n];if(t.Role==4){for(int at=t.FrameOffset;at<r.Length;at+=12){MoveCollision(r,at,deltaRaw);PointsInWorld(r.AsSpan(at,12).ToArray(),true);}}else{int slot=Array.IndexOf(GreenDruidLinkedWorldSourceProfile.Sectors,t.Target&65535);for(int at=t.FrameOffset;at<r.Length;at+=4)GeometryPoint(sectors[slot],U(r,at));}}
  List<TranslatedActor> actors=[];for(int n=0;n<3;n++){var t=_actors[n];byte[] row=t.Row.ToArray(),props=t.Properties.ToArray();TranslateXYZ(row,12,deltaRaw);if(n<2){int start=n==0?100:60,count=n==0?4:9;for(int node=0;node<count;node++)TranslateXYZ(props,start+node*16,deltaRaw);}actors.Add(new(RequiredSourceRows[n],row,t with{Row=t.Row.ToArray(),Properties=props,InternalPointers=t.InternalPointers.ToArray(),ExternalRowPointers=t.ExternalRowPointers.ToArray()}));}return new(deltaRaw.ToArray(),actors,sectors,records,Tracks.Select(t=>t.Role).ToArray());
 }
 private static void TranslateXYZ(byte[] b,int at,IReadOnlyList<int>d){for(int a=0;a<3;a++){int value=checked(I(b,at+a*4)+d[a]);Require(value is >=0 and <262144,"every whole actor/path XYZ in native world");W(b,at+a*4,value);}}
 private static void GeometryPoint(byte[] s,uint p){int[] o=[(int)(U(s,8)>>16),(int)(U(s,8)&65535),(int)(U(s,12)>>16)],v=[(int)(p>>21),(int)((p>>10)&2047),(int)(p&1023)];Require(Enumerable.Range(0,3).All(a=>o[a]+v[a] is >=0 and <16384),"all static/keyframed LP/HP world vertices");}
 public IReadOnlyList<CanonicalFrames> CopyCanonicalCollisionFrames(IReadOnlyList<int> deltaRaw)
 {
  var moved=CopyTranslated(deltaRaw);List<CanonicalFrames> groups=[];for(int n=0;n<Tracks.Count;n++){var t=Tracks[n];if(t.Role!=4)continue;var r=moved.AnimationRecords[n];List<byte[]> frames=[];for(int f=0;f<t.Frames;f++){byte[] output=new byte[120];for(int tri=0;tri<10;tri++){var points=PackedPoints(r.AsSpan(t.FrameOffset+f*120+tri*12,12).ToArray(),true);var a=points[0];var b=points[1];var c=points[2];if(b[2]<a[2]&&b[2]<=c[2])(a,b,c)=(b,c,a);else if(c[2]<a[2]&&c[2]<b[2])(a,b,c)=(c,a,b);for(int axis=0;axis<3;axis++){int db=b[axis]-a[axis],dc=c[axis]-a[axis];Require(axis==2?db is >=0 and <256&&dc is >=0 and <256:db is >=-256 and <256&&dc is >=-256 and <256,"exact native canonical delta grammar");uint word=(uint)a[axis]|(axis==2?(uint)(db<<16)|(uint)(dc<<24):((uint)db&511)<<14|((uint)dc&511)<<23);W(output,tri*12+axis*4,(int)word);}PointsInWorld(output.AsSpan(tri*12,12).ToArray(),false);}frames.Add(output);}groups.Add(new(t.Group,t.SourceTarget>>16,frames.ToArray()));}return groups.OrderBy(g=>g.SourceGroup).ToArray();
 }
 private void RequireTranslation(Translation actual)
 {
  var expected=CopyTranslated(actual.DeltaRaw);Require(actual.Actors.Count==3&&actual.Sectors.Length==6&&actual.AnimationRecords.Length==8&&actual.Roles.SequenceEqual(expected.Roles),"complete issued translated resource topology");for(int n=0;n<6;n++)Require(actual.Sectors[n].AsSpan().SequenceEqual(expected.Sectors[n]),"whole issued translated LP/HP sector");for(int n=0;n<8;n++)Require(actual.AnimationRecords[n].AsSpan().SequenceEqual(expected.AnimationRecords[n]),"whole issued translated command/frame record");for(int n=0;n<3;n++){var a=actual.Actors[n];var b=expected.Actors[n];Require(a.SourceRow==b.SourceRow&&a.Row.AsSpan().SequenceEqual(b.Row)&&a.Template.SourceLevelKey==b.Template.SourceLevelKey&&a.Template.SourceRowIndex==b.Template.SourceRowIndex&&a.Template.Row.AsSpan().SequenceEqual(b.Template.Row)&&a.Template.Properties.AsSpan().SequenceEqual(b.Template.Properties)&&a.Template.InternalPointers.SequenceEqual(b.Template.InternalPointers)&&a.Template.ExternalRowPointers.SequenceEqual(b.Template.ExternalRowPointers)&&a.Template.DroppedExternalPointers==b.Template.DroppedExternalPointers&&a.Template.SourceRowIndices.Count==0&&a.Template.SourcePrerequisiteRows.Count==0&&a.Template.ImportedRowIndices.Count==0,"three complete source actor/link/path identities");}
 }
 /// <summary>Only exact one-context all-visible zero-sky authored host grammar.
 /// Both original host sky context owners are checked; source sky is not imported.</summary>
 public byte[] BuildZeroSkyVisibility(UniversalLevelData host,IReadOnlyList<int> destinationSectors)
 {
  var w=World(host);var sectors=Sectors(w[1]);Require(w[3].AsSpan().SequenceEqual(new byte[]{8,0,0,0,0,0,0,0}),"authenticated entirely empty native sky geometry");var terrain=Lists(w[2],false);var sky=Lists(w[2],true);int[] prefix=Enumerable.Range(0,sectors.Length).ToArray();Require(terrain.Length==1&&sky.Length==1&&terrain[0].SequenceEqual(prefix)&&sky[0].Length==0&&w[2].AsSpan().SequenceEqual(Visibility([prefix],[[]])),"exact ordered host terrain and empty parallel sky context0");Require(destinationSectors.Count==6&&destinationSectors.SequenceEqual(Enumerable.Range(sectors.Length,6))&&sectors.Length+6<255,"six actually appended byte-visible sector owners");int high=sectors.Sum(s=>s[22]==0?0:1+s[22])+HighQueueContentWords;Require(high+1<=128&&sectors.Length+6+1<=128,"unchanged127-word host+complete-world queue partitions");Require(sectors.Sum(s=>(int)s[22])+94+1<=0x1c00,"complete subdivision cache before HP queue");int[] projected=ProjectedSourceContextOrder.Select(si=>destinationSectors[Array.IndexOf(GreenDruidLinkedWorldSourceProfile.Sectors,si)]).ToArray();return Visibility([prefix.Concat(destinationSectors).ToArray(),prefix.Concat(projected).ToArray()],[[],[]]);
 }
 /// <summary>Inert resource binding. Generic indexed-row resolver retains ownership
 /// of physical actor links; this maps only the Druid group field. The separately
 /// issued actor dispatcher must authenticate its hardcoded group9 setter rewrite.</summary>
 public Rebinding Rebind(Translation translated,UniversalLevelData host,IReadOnlyList<int> destinationGroups,IReadOnlyList<int> destinationSectors,IReadOnlyList<int> firstTriangles,int firstFixedTriangle,int firstMaterial,IReadOnlyList<int> physicalRows)
 {
  RequireTranslation(translated);var banks=Banks(host);Require(destinationGroups.Count==2&&destinationGroups.Distinct().Count()==2&&destinationGroups.All(g=>g is >=0 and <=255),"two distinct allocated native group bytes");var occupied=banks.SelectMany(b=>Enumerable.Range(0,I(b,4)).Select(n=>(int)Record(b,n)[0])).ToHashSet();Require(destinationGroups.All(g=>!occupied.Contains(g)),"both actual group IDs free in complete retained host banks");Require(firstTriangles.Count==2&&firstTriangles.All(t=>t>=0&&t+10<=32768)&&firstFixedTriangle>=0&&firstFixedTriangle+163<=32768,"complete native15-bit collision intervals");var ids=Enumerable.Range(firstTriangles[0],10).Concat(Enumerable.Range(firstTriangles[1],10)).Concat(Enumerable.Range(firstFixedTriangle,163)).ToArray();Require(ids.Distinct().Count()==183,"all three destination collision intervals disjoint");Require(physicalRows.Count==3&&physicalRows.Distinct().Count()==3&&physicalRows.All(r=>r is >=0 and <=255),"all three complete physical actor owners including particle target byte");var world=World(host);Require(firstMaterial==I(world[0],4)&&firstMaterial>=0&&firstMaterial+9<=128,"nine appended native7-bit material slots");var visibility=BuildZeroSkyVisibility(host,destinationSectors);var records=translated.AnimationRecords.Select(r=>r.ToArray()).ToArray();var sectors=translated.Sectors.Select(s=>s.ToArray()).ToArray();List<RecordBinding> bindings=[];int[] added=new int[7];
  for(int n=0;n<8;n++){var t=Tracks[n];int group=destinationGroups[t.Group==8?0:1],index=I(banks[t.Role],4)+added[t.Role]++;Require(t.Role==4||index<255,"LP/HP recordFF remains reserved");records[n][0]=(byte)group;if(t.Role==4)W(records[n],4,(firstTriangles[t.Group==8?0:1]<<16)|10);else{int slot=Array.IndexOf(GreenDruidLinkedWorldSourceProfile.Sectors,t.SourceTarget&65535);W(records[n],4,(t.SourceTarget&unchecked((int)0xffff0000))|destinationSectors[slot]);sectors[slot][t.Role==3?24:26]=(byte)index;}bindings.Add(new(t.Group,group,t.Role,t.SourceRecordIndex,index));}
  int cache=1;foreach(var s in Sectors(world[1])){Require((U(s,12)&65535)==cache,"complete retained host subdivision cache prefix");cache+=s[22];}foreach(var s in sectors){W(s,12,(int)((U(s,12)&0xffff0000)|(uint)cache));cache+=s[22];int face=28+4*s[23]+4*s[20]+8*s[21];for(int f=0;f<s[22];f++){int at=face+16*f+8,id=(int)(U(s,at)&127),slot=Array.IndexOf(GreenDruidLinkedWorldSourceProfile.Materials,id);Require(slot>=0,"every complete source material owner");W(s,at,(int)((U(s,at)&~127u)|(uint)(firstMaterial+slot)));}}
  var actors=translated.Actors.Select(a=>{byte[] p=a.Template.Properties.ToArray();if(a.SourceRow==11)W(p,4,destinationGroups[0]);return a with{Row=a.Row.ToArray(),Template=a.Template with{Properties=p}};}).ToArray();byte[] assignments=SourceAssignments();for(int n=0;n<assignments.Length;n++)if(assignments[n]==20)assignments[n]=1;return new(new(translated.DeltaRaw.ToArray(),actors,sectors,records,translated.Roles.ToArray()),destinationGroups.ToArray(),destinationSectors.ToArray(),firstTriangles.ToArray(),firstFixedTriangle,bindings,assignments,SourceFlags(),visibility);
 }
}
