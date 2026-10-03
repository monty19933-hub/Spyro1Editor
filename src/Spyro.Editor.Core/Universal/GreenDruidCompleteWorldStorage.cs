using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;
namespace Spyro.Editor.Core.Universal;
/// <summary>Combined real world storage. This remains inert until actor, copied
/// callbacks and native queue/cache lifecycle are certified together.</summary>
internal sealed class GreenDruidCompleteWorldStorage
{
 private readonly byte[] _before,_after;
 private readonly int _materialCount;
 private readonly byte[][] _beforeWorld,_afterWorld;
 internal GreenDruidSceneBankStorage Banks{get;}
 internal GreenDruidAnimatedCollisionStorage Collision{get;}
 internal GreenDruidWorldMaterialStorage Materials{get;}
 internal UniversalGreenDruidWorldPrerequisites.Translation Actors{get;}
 internal int FirstSector{get;}
 internal int FirstMaterial{get;}
 internal int TotalHighFaces{get;}
 internal int HighQueueContentWords{get;}
 internal int SectorQueueContentWords{get;}
 internal int AddedWorldBytes{get;}
 internal string BeforeHash=>H(_before);
 internal bool ForeignPlacementAdmitted=>false;
 private static int I(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadInt32LittleEndian(b[o..]);
 private static uint U(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
 private static void W(Span<byte>b,int o,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[o..],v);
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static void Require(bool v,string why){if(!v)throw new InvalidDataException("Green Druid complete world storage: "+why);}
 internal static byte[][] World(UniversalLevelData d)
 {
  var sf=d.Subfiles()[1];int at=sf.Offset;List<byte[]> result=[];
  for(int n=0;n<6;n++){int size=I(d.Bytes,at);Require(size>=4&&size%4==0&&at+(long)size<=sf.Offset+sf.Length,"complete six-component extent");result.Add(d.Bytes.AsSpan(at,size).ToArray());at+=size;}return result.ToArray();
 }
 internal static byte[][] Sectors(byte[] component)
 {
  int count=I(component,4);Require(count is >=0 and <255&&I(component,0)==component.Length&&8+count*4<=component.Length,"native byte-visible sector array");List<byte[]> result=[];
  for(int n=0;n<count;n++){int at=4+I(component,8+n*4),end=n+1<count?4+I(component,8+(n+1)*4):component.Length;Require(at>=8+count*4&&end>=at+28&&end<=component.Length&&at%4==0&&end%4==0,"ordered complete sector pointer owner");byte[] s=component.AsSpan(at,end-at).ToArray();_=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(s,s.Length,false,"combined world sector");Require(s[20]<=170&&s[16]<=170,"native projected vertex scratch capacity");int face=28+4*s[23]+4*s[20]+8*s[21];Require(face+16*s[22]<=s.Length,"complete high face array");result.Add(s);}return result.ToArray();
 }
 private static byte[] PackSectors(byte[][] sectors)
 {int at=8+sectors.Length*4;byte[] b=new byte[at+sectors.Sum(s=>s.Length)];W(b,0,b.Length);W(b,4,sectors.Length);for(int n=0;n<sectors.Length;n++){W(b,8+n*4,at-4);sectors[n].CopyTo(b,at);at+=sectors[n].Length;}return b;}
 private static byte[] AddVisibility(byte[] b,int first,int added)
 {
  int count=I(b,8),sky=4+I(b,4);Require(count is >=0 and <=255&&12+4*count<=sky&&sky<=b.Length,"visibility table and retained sky owner");List<byte[]> lists=[];
  for(int n=0;n<count;n++){int at=4+I(b,12+n*4),end=n+1<count?4+I(b,12+(n+1)*4):sky;Require(at>=12+4*count&&end>at&&end<=sky,"native visibility list owner");var list=b.AsSpan(at,end-at);int terminal=list.IndexOf((byte)255);Require(terminal>=0&&list[terminal..].IndexOfAnyExcept((byte)255)<0&&list[..terminal].ToArray().All(id=>id<first),"exact host ordinal list andFF suffix");int size=UniversalLevelData.Align(terminal+added+1,4);byte[] moved=new byte[size];moved.AsSpan().Fill(255);list[..terminal].CopyTo(moved);for(int k=0;k<added;k++)moved[terminal+k]=(byte)(first+k);lists.Add(moved);}
  int cursor=12+4*count,newSky=cursor+lists.Sum(l=>l.Length);byte[] result=new byte[newSky+b.Length-sky];b.AsSpan(0,12).CopyTo(result);W(result,0,result.Length);W(result,4,newSky-4);for(int n=0;n<count;n++){W(result,12+n*4,cursor-4);lists[n].CopyTo(result,cursor);cursor+=lists[n].Length;}b.AsSpan(sky).CopyTo(result.AsSpan(newSky));return result;
 }
 internal static GreenDruidCompleteWorldStorage Capture(string key,UniversalLevelData source,UniversalOverlay overlay,byte[]originalExe,UniversalLevelData host,IReadOnlyList<int>delta,UniversalVramAllocator allocator,IReadOnlyList<int>physicalRows,int sourceDruidRow=5)
 {return new(key,source,overlay,originalExe,host,delta,allocator,physicalRows,sourceDruidRow);}
 private GreenDruidCompleteWorldStorage(string key,UniversalLevelData source,UniversalOverlay overlay,byte[]exe,UniversalLevelData host,IReadOnlyList<int>delta,UniversalVramAllocator allocator,IReadOnlyList<int>physicalRows,int sourceDruidRow)
 {
  var proof=UniversalGreenDruidWorldPrerequisites.CaptureSource(key,source,overlay,exe,sourceDruidRow);_before=host.Bytes.ToArray();_beforeWorld=World(host);var sectors=Sectors(_beforeWorld[1]);FirstSector=sectors.Length;Require(FirstSector+proof.RequiredSourceSectors.Count<=255,"the appended sector IDs must excludeFF");
  _materialCount=proof.RequiredSourceMaterials.Count;FirstMaterial=I(_beforeWorld[0],4);Require(FirstMaterial>=0&&FirstMaterial+_materialCount<=128&&_beforeWorld[0].Length==8+184*FirstMaterial,"complete native material table and7-bit new IDs");
  // Conservative all-sector context includes every high face and sector header.
  // No culling assumptions grant queue space, including imported FF triangles.
  var moved=proof.CopyTranslated(delta);var all=sectors.Concat(moved.Sectors).ToArray();TotalHighFaces=all.Sum(s=>(int)s[22]);SectorQueueContentWords=all.Length;HighQueueContentWords=all.Sum(s=>s[22]==0?0:1+s[22]);Require(SectorQueueContentWords+1<=128&&HighQueueContentWords+1<=128,"combined all-visible native queue partitions need their zero terminators");Require(TotalHighFaces+1<=0x1c00,"native subdivision cache must end before HP queue800718F4");
  int cache=1;foreach(var s in sectors){Require((U(s,12)&65535)==cache,"host native cache prefix exactly owns prior high faces");cache+=s[22];}
  foreach(var s in moved.Sectors){Require(s[20]<=170&&s[16]<=170,"new native projection scratch bounds");W(s,12,(int)((U(s,12)&0xffff0000)|(uint)cache));cache+=s[22];int face=28+4*s[23]+4*s[20]+8*s[21];for(int f=0;f<s[22];f++){int at=face+f*16+8;int old=(int)(U(s,at)&127),slot=proof.RequiredSourceMaterials.ToList().IndexOf(old);Require(slot>=0,"each new sampled material ID issued");W(s,at,(int)((U(s,at)&~127u)|(uint)(FirstMaterial+slot)));}}
  Collision=GreenDruidAnimatedCollisionStorage.Capture(key,source,overlay,exe,_beforeWorld[4],delta,sourceDruidRow);Banks=GreenDruidSceneBankStorage.Capture(key,source,overlay,exe,host,delta,FirstSector,Collision.FirstTriangle,sourceDruidRow);
  Require(physicalRows.Count==proof.RequiredSourceRows.Count&&physicalRows.Distinct().Count()==proof.RequiredSourceRows.Count&&physicalRows.All(r=>r is >=0 and <=255) ,"exact issued native physical owners");Actors=proof.Rebind(proof.CopyTranslated(delta),Banks.GroupId,new[]{FirstSector},Collision.FirstTriangle,physicalRows);
  var stagedAllocator=allocator.Clone();Materials=GreenDruidWorldMaterialStorage.Capture(key,source,overlay,exe,host.Vram().ToArray(),stagedAllocator,sourceDruidRow);  _afterWorld=_beforeWorld.Select(b=>b.ToArray()).ToArray();_afterWorld[0]=Materials.AppendTable(_beforeWorld[0]);_afterWorld[1]=PackSectors(sectors.Concat(moved.Sectors).ToArray());_afterWorld[2]=AddVisibility(_beforeWorld[2],FirstSector,moved.Sectors.Length);_afterWorld[4]=Collision.CopyComponent();AddedWorldBytes=_afterWorld.Sum(b=>b.Length)-_beforeWorld.Sum(b=>b.Length);Require(AddedWorldBytes>=0,"append cannot discard host world owner");
  var outData=new UniversalLevelData(_before.ToArray());var sf=outData.Subfiles()[1];int beforeEnd=_beforeWorld.Sum(b=>b.Length),afterEnd=_afterWorld.Sum(b=>b.Length),tailSize=sf.Length-beforeEnd;byte[] tail=outData.Bytes.AsSpan(sf.Offset+beforeEnd,tailSize).ToArray();if(AddedWorldBytes>0)outData.GrowSubfile(1,UniversalLevelData.Align(AddedWorldBytes,0x800));sf=outData.Subfiles()[1];outData.Bytes.AsSpan(sf.Offset,sf.Length).Clear();int cursor=sf.Offset;foreach(var b in _afterWorld){b.CopyTo(outData.Bytes,cursor);cursor+=b.Length;}tail.CopyTo(outData.Bytes,cursor);Materials.CopyGpu().CopyTo(outData.Vram());outData.ReplaceScene(Banks.CopyScene());_after=outData.Bytes.ToArray();RequireFinal(_after);
  // Only commit allocator reservations after every combined storage/readback check.
  foreach(var region in Materials.Regions)for(int row=0;row<region.RowCount;row++)allocator.Reserve(region.TargetOffset+row*1024,region.RowByteLength);
 }
 internal byte[] CopyLevelData()=>_after.ToArray();
 internal void RequireFinal(byte[] data)
 {
  Require(data.AsSpan().SequenceEqual(_after),"complete issued final body changed");var current=new UniversalLevelData(data);var parts=World(current);for(int n=0;n<6;n++)Require(parts[n].AsSpan().SequenceEqual(_afterWorld[n]),"full world component readback");Collision.ValidateReadback(parts[4]);Materials.RequireFinal(current.Vram().ToArray(),Enumerable.Range(0,_materialCount).Select(n=>{int id=FirstMaterial+n,count=I(parts[0],4);return new byte[]{ }.Concat(parts[0].AsSpan(8+id*16,16).ToArray()).Concat(parts[0].AsSpan(8+count*16+id*168,168).ToArray()).ToArray();}).ToArray());
  var old=new UniversalLevelData(_before);var beforeSf=old.Subfiles()[1];var afterSf=current.Subfiles()[1];int beforeEnd=_beforeWorld.Sum(b=>b.Length),afterEnd=parts.Sum(b=>b.Length);Require(_before.AsSpan(beforeSf.Offset+beforeEnd,beforeSf.Length-beforeEnd).SequenceEqual(data.AsSpan(afterSf.Offset+afterEnd,beforeSf.Length-beforeEnd)),"every original portal/particle/sound/padding byte retained");var beforeSound=new NativeLevelSoundLayout(_before);var afterSound=new NativeLevelSoundLayout(data);Require(afterSound.SoundOffset-afterSf.Offset==beforeSound.SoundOffset-beforeSf.Offset+AddedWorldBytes&&afterSound.Sound.CopyComponent().AsSpan().SequenceEqual(beforeSound.Sound.CopyComponent()),"complete retained native sound owner boundary");
 }
}
