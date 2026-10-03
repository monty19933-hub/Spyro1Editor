using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;
/// <summary>Detached exact six-sector group1 data binding. No renderer/actor admission.</summary>
internal sealed class UniversalGreenDruidGroup1WorldRebinding
{
 internal sealed record RecordBinding(int SourceGroup,int DestinationGroup,int Role,int SourceRecordIndex,int DestinationRecordIndex);
 private readonly UniversalGreenDruidGroup1WorldPrerequisites.Translation _resources;
 private readonly int[] _sectors,_physical;
 private readonly RecordBinding[] _records;
 private readonly byte[] _assignments,_flags,_visibility;
 internal UniversalGreenDruidGroup1WorldPrerequisites.Translation Resources=>Copy(_resources);
 internal int DestinationGroup{get;}
 internal int[] DestinationSectors=>_sectors.ToArray();
 internal int[] PhysicalRows=>_physical.ToArray();
 internal int FirstMovingTriangle{get;}
 internal int FirstFixedTriangle{get;}
 internal IReadOnlyDictionary<int,int> SourceToDestinationContexts=>new Dictionary<int,int>{{16,1},{20,2}};
 internal IReadOnlyList<RecordBinding> RecordBindings=>_records.ToArray();
 internal byte[] AssignmentBytes=>_assignments.ToArray();
 internal byte[] SurfaceFlags=>_flags.ToArray();
 internal byte[] VisibilityComponent=>_visibility.ToArray();
 private static int I(ReadOnlySpan<byte>b,int n)=>BinaryPrimitives.ReadInt32LittleEndian(b[n..]);
 private static uint U(ReadOnlySpan<byte>b,int n)=>BinaryPrimitives.ReadUInt32LittleEndian(b[n..]);
 private static void W(Span<byte>b,int n,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[n..],v);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid group1 binding: "+why);}
 internal static byte[][] Banks(UniversalLevelData host)
 {
  var sf=host.Subfiles()[3];int at=sf.Offset+0x88;List<byte[]> banks=[];
  for(int role=0;role<7;role++){int size=I(host.Bytes,at),count=I(host.Bytes,at+4);Require(size>=8&&size%4==0&&count>=0&&8L+4L*count<=size&&at+(long)size<=sf.Offset+sf.Length,"complete seven native banks");byte[] b=host.Bytes.AsSpan(at,size).ToArray();for(int n=0;n<count;n++){int p=4+I(b,8+4*n);Require(p>=8+4*count&&p<size&&p%4==0,"retained record pointer");}banks.Add(b);at+=size;}Require(at==sf.Offset+host.Scene().MobyComponent,"all bank bytes own scene prefix");return banks.ToArray();
 }
 private static byte[] Record(byte[] bank,int index)
 {int count=I(bank,4),start=4+I(bank,8+4*index),end=Enumerable.Range(0,count).Select(n=>4+I(bank,8+4*n)).Where(p=>p>start).DefaultIfEmpty(bank.Length).Min();return bank.AsSpan(start,end-start).ToArray();}
 private static UniversalGreenDruidGroup1WorldPrerequisites.Translation Copy(UniversalGreenDruidGroup1WorldPrerequisites.Translation t)=>new(t.DeltaRaw.ToArray(),t.Actors.Select(a=>new UniversalGreenDruidGroup1WorldPrerequisites.TranslatedActor(a.SourceRow,a.Row.ToArray(),a.Template with{Row=a.Template.Row.ToArray(),Properties=a.Template.Properties.ToArray(),InternalPointers=a.Template.InternalPointers.ToArray(),ExternalRowPointers=a.Template.ExternalRowPointers.ToArray()})).ToArray(),t.Sectors.Select(b=>b.ToArray()).ToArray(),t.AnimationRecords.Select(b=>b.ToArray()).ToArray(),t.Roles.ToArray());
 internal static byte[] PackVisibility(IReadOnlyList<int[]> terrain,IReadOnlyList<int[]> sky)
 {
  Require(terrain.Count==sky.Count&&terrain.Count<=255,"parallel contexts");static byte[] List(int[] ids){if(ids.Any(i=>i<0||i>=255)||ids.Distinct().Count()!=ids.Length)throw new InvalidDataException("Invalid context sector/sky owners");byte[] b=new byte[UniversalLevelData.Align(ids.Length+1,4)];b.AsSpan().Fill(255);for(int n=0;n<ids.Length;n++)b[n]=(byte)ids[n];return b;}
  var a=terrain.Select(List).ToArray();var b=sky.Select(List).ToArray();int at=12+4*a.Length,skyAt=at+a.Sum(x=>x.Length);byte[] output=new byte[skyAt+4+4*b.Length+b.Sum(x=>x.Length)];W(output,0,output.Length);W(output,4,skyAt-4);W(output,8,a.Length);for(int n=0;n<a.Length;n++){W(output,12+4*n,at-4);a[n].CopyTo(output,at);at+=a[n].Length;}W(output,skyAt,b.Length);at=skyAt+4+4*b.Length;for(int n=0;n<b.Length;n++){W(output,skyAt+4+4*n,at-skyAt);b[n].CopyTo(output,at);at+=b[n].Length;}return output;
 }
 internal static UniversalGreenDruidGroup1WorldRebinding Bind(UniversalGreenDruidGroup1WorldPrerequisites proof,UniversalGreenDruidGroup1WorldPrerequisites.Translation translated,UniversalLevelData host,int destinationGroup,IReadOnlyList<int> destinationSectors,int firstMoving,int firstFixed,int firstMaterial,IReadOnlyList<int> physicalRows,IReadOnlyDictionary<int,int> contextMap)
 {proof.RequireTranslation(translated);return new(proof,translated,new(host.Bytes.ToArray()),destinationGroup,destinationSectors,firstMoving,firstFixed,firstMaterial,physicalRows,contextMap);}
 private UniversalGreenDruidGroup1WorldRebinding(UniversalGreenDruidGroup1WorldPrerequisites proof,UniversalGreenDruidGroup1WorldPrerequisites.Translation translated,UniversalLevelData host,int group,IReadOnlyList<int> sectors,int moving,int fixedFirst,int material,IReadOnlyList<int> physical,IReadOnlyDictionary<int,int> contexts)
 {
  var world=GreenDruidCompleteWorldStorage.World(host);var retained=GreenDruidCompleteWorldStorage.Sectors(world[1]);var banks=Banks(host);var collision=GreenDruidAnimatedCollisionStorage.Read(world[4]);
  Require(group is >=0 and <255&&!banks.Any(b=>Enumerable.Range(0,I(b,4)).Any(n=>Record(b,n)[0]==group)),"actually free group byte;FF reserved");Require(sectors.Count==6&&sectors.SequenceEqual(Enumerable.Range(retained.Length,6))&&retained.Length+6<255,"six complete appended byte-visible sectors");
  Require(moving==collision.Count&&fixedFirst==moving+50&&fixedFirst+377<=32768,"actual complete contiguous15-bit appended collision intervals");Require(material==I(world[0],4)&&material>=0&&material+16<=128,"sixteen appended7-bit material owners");Require(physical.Count==2&&physical.Distinct().Count()==2&&physical.Order().SequenceEqual(Enumerable.Range(host.Scene().RowCount,2))&&physical.All(p=>p is >=0 and <=255),"both actual append allocation rows; no dummy or reused owner");
  Require(contexts.Count==2&&contexts.TryGetValue(16,out int c16)&&c16==1&&contexts.TryGetValue(20,out int c20)&&c20==2,"two distinct actually appended context owners16→1/20→2");int[] prefix=Enumerable.Range(0,retained.Length).ToArray();Require(world[3].SequenceEqual(new byte[]{8,0,0,0,0,0,0,0})&&world[2].SequenceEqual(PackVisibility(new[]{prefix},new[]{Array.Empty<int>()})),"exact ordered one-context authored host and empty native sky");
  DestinationGroup=group;_sectors=sectors.ToArray();_physical=physical.ToArray();FirstMovingTriangle=moving;FirstFixedTriangle=fixedFirst;var moved=Copy(translated);var records=moved.AnimationRecords;var sectorData=moved.Sectors;List<RecordBinding> bindings=[];int[] added=new int[7];
  for(int n=0;n<proof.Tracks.Count;n++){var t=proof.Tracks[n];int index=I(banks[t.Role],4)+added[t.Role]++;Require(t.Role==4||index<255,"LP/HP recordFF remains reserved");records[n][0]=(byte)group;if(t.Role==4)W(records[n],4,(moving<<16)|50);else{int slot=proof.RequiredSourceSectors.ToList().IndexOf(t.SourceTarget&65535);Require(slot>=0,"original complete sector target");W(records[n],4,(t.SourceTarget&unchecked((int)0xffff0000))|_sectors[slot]);sectorData[slot][t.Role==3?24:26]=(byte)index;}bindings.Add(new(1,group,t.Role,t.SourceRecordIndex,index));}
  int cache=1;foreach(var s in retained){Require((U(s,12)&65535)==cache,"exact retained subdivision prefix");cache+=s[22];}foreach(var s in sectorData){W(s,12,(int)((U(s,12)&0xffff0000)|(uint)cache));cache+=s[22];int f=28+4*s[23]+4*s[20]+8*s[21];for(int n=0;n<s[22];n++){int at=f+16*n+8,slot=proof.RequiredSourceMaterials.ToList().IndexOf((int)(U(s,at)&127));Require(slot>=0,"every complete source material owner");W(s,at,(int)((U(s,at)&~127u)|(uint)(material+slot)));}}
  // Source body/class/state stays exact; only issued native group and physical target integer change.
  var actors=moved.Actors.Select(a=>{byte[] p=a.Template.Properties.ToArray();if(a.SourceRow==6){W(p,4,group);W(p,8,_physical[1]);}return a with{Template=a.Template with{Properties=p}};}).ToArray();_resources=moved with{Actors=actors};_records=bindings.ToArray();_assignments=proof.CopySourceAssignments();for(int n=0;n<_assignments.Length;n++)if(_assignments[n]!=255){Require(contexts.TryGetValue(_assignments[n],out int mapped),"every original camera context has an issued owner");_assignments[n]=(byte)mapped;}_flags=proof.CopySourceSurfaceFlags();
  var sourceContexts=proof.CopySourceContexts();var terrain=new List<int[]>{prefix.Concat(_sectors).ToArray()};foreach(var context in sourceContexts){Require(contexts[context.ContextId]==terrain.Count,"ordered dual source contexts");terrain.Add(prefix.Concat(context.OwnedTerrain.Select(id=>_sectors[proof.RequiredSourceSectors.ToList().IndexOf(id)])).ToArray());}_visibility=PackVisibility(terrain,new[]{Array.Empty<int>(),Array.Empty<int>(),Array.Empty<int>()});
  // Existing native subdivision cache limit remains a staging guard. Queue admission is a separate certificate.
  Require(cache<=0x1c00,"complete subdivision cache must stay before unchanged native scratch boundary");
 }
}
