using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Atomic staging of the six complete sectors, both groups, all183
/// collision owners, original materials and parallel terrain/sky contexts.</summary>
internal sealed class GreenDruidLinkedWorldStorage
{
 private readonly byte[] _before,_after;
 private readonly byte[][] _beforeWorld,_afterWorld;
 internal UniversalGreenDruidLinkedWorldPrerequisites Proof{get;}
 internal UniversalGreenDruidLinkedWorldPrerequisites.Rebinding Binding{get;}
 internal GreenDruidLinkedCollisionStorage Collision{get;}
 internal GreenDruidLinkedSceneBankStorage Banks{get;}
 internal GreenDruidWorldMaterialStorage Materials{get;}
 internal int FirstSector{get;}
 internal int FirstMaterial{get;}
 internal int AddedWorldBytes{get;}
 internal int HighQueueContentWords{get;}
 internal IReadOnlyList<int> DeltaRaw=>Binding.Resources.DeltaRaw.ToArray();
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid linked world storage: "+why);}
 internal GreenDruidLinkedWorldStorage(UniversalGreenDruidLinkedWorldPrerequisites proof,UniversalLevelData host,IReadOnlyList<int> delta,UniversalVramAllocator allocator,IReadOnlyList<int> physicalRows)
 {
  Proof=proof;_before=host.Bytes.ToArray();_beforeWorld=GreenDruidCompleteWorldStorage.World(host);var sectors=GreenDruidCompleteWorldStorage.Sectors(_beforeWorld[1]);FirstSector=sectors.Length;FirstMaterial=I(_beforeWorld[0],4);
  var occupied=new HashSet<int>();var sceneSf=host.Subfiles()[3];int bankAt=sceneSf.Offset+0x88;
  for(int role=0;role<7;role++){int size=I(host.Bytes,bankAt),count=I(host.Bytes,bankAt+4);Require(size>=8&&count>=0&&8+count*4<=size&&bankAt+size<=sceneSf.Offset+sceneSf.Length,"complete host scene bank");for(int n=0;n<count;n++)occupied.Add(host.Bytes[bankAt+4+I(host.Bytes,bankAt+8+n*4)]);bankAt+=size;}
  int[] groups=Enumerable.Range(0,256).Where(g=>!occupied.Contains(g)).Take(2).ToArray();Require(groups.Length==2,"two free native groups");var collision=GreenDruidAnimatedCollisionStorage.Read(_beforeWorld[4]);
  Binding=proof.Rebind(proof.CopyTranslated(delta),host,groups,Enumerable.Range(FirstSector,6).ToArray(),[collision.Count,collision.Count+10],collision.Count+20,FirstMaterial,physicalRows);
  Collision=new(proof,_beforeWorld[4],delta,Binding);Banks=new(host,Binding);
  HighQueueContentWords=sectors.Sum(s=>s[22]==0?0:1+s[22])+proof.HighQueueContentWords;Require(HighQueueContentWords+1<=128,"unchanged complete all-visible queue capacity");
  var staged=allocator.Clone();Materials=GreenDruidWorldMaterialStorage.CaptureLinked(proof,host.Vram().ToArray(),staged);
  _afterWorld=_beforeWorld.Select(p=>p.ToArray()).ToArray();_afterWorld[0]=Materials.AppendTable(_beforeWorld[0]);_afterWorld[1]=PackSectors(sectors.Concat(Binding.Resources.Sectors).ToArray());_afterWorld[2]=Binding.VisibilityComponent.ToArray();_afterWorld[4]=Collision.CopyComponent();AddedWorldBytes=_afterWorld.Sum(p=>p.Length)-_beforeWorld.Sum(p=>p.Length);Require(AddedWorldBytes>=0,"host resource append cannot discard owners");
  var output=new UniversalLevelData(_before.ToArray());var sf=output.Subfiles()[1];int oldEnd=_beforeWorld.Sum(p=>p.Length);byte[] tail=output.Bytes.AsSpan(sf.Offset+oldEnd,sf.Length-oldEnd).ToArray();if(AddedWorldBytes>0)output.GrowSubfile(1,UniversalLevelData.Align(AddedWorldBytes,2048));sf=output.Subfiles()[1];output.Bytes.AsSpan(sf.Offset,sf.Length).Clear();int cursor=sf.Offset;foreach(var p in _afterWorld){p.CopyTo(output.Bytes,cursor);cursor+=p.Length;}tail.CopyTo(output.Bytes,cursor);Materials.CopyGpu().CopyTo(output.Vram());output.ReplaceScene(Banks.CopyScene());_after=output.Bytes.ToArray();RequireFinal(_after);
  foreach(var region in Materials.Regions)for(int y=0;y<region.RowCount;y++)allocator.Reserve(region.TargetOffset+y*1024,region.RowByteLength);
 }
 private static byte[] PackSectors(byte[][] sectors)
 {int at=8+sectors.Length*4;byte[] b=new byte[at+sectors.Sum(s=>s.Length)];W(b,0,b.Length);W(b,4,sectors.Length);for(int n=0;n<sectors.Length;n++){W(b,8+n*4,at-4);sectors[n].CopyTo(b,at);at+=sectors[n].Length;}return b;}
 internal byte[] CopyLevelData()=>_after.ToArray();
 internal void RequireFinal(byte[] actual)
 {
  Require(actual.AsSpan().SequenceEqual(_after),"complete issued storage body");var final=new UniversalLevelData(actual);var world=GreenDruidCompleteWorldStorage.World(final);for(int n=0;n<6;n++)Require(world[n].SequenceEqual(_afterWorld[n]),"each complete world component");Collision.ValidateReadback(world[4]);var sf=final.Subfiles()[3];Banks.ValidateFinal(final.Bytes.AsSpan(sf.Offset,final.Scene().UsedEnd).ToArray());
  var old=new UniversalLevelData(_before);var a=old.Subfiles()[1];var b=final.Subfiles()[1];int oldEnd=_beforeWorld.Sum(p=>p.Length),newEnd=world.Sum(p=>p.Length);Require(_before.AsSpan(a.Offset+oldEnd,a.Length-oldEnd).SequenceEqual(actual.AsSpan(b.Offset+newEnd,a.Length-oldEnd)),"all original trailing portal/particle/sound/padding bytes");
  var oldSound=new NativeLevelSoundLayout(_before);var newSound=new NativeLevelSoundLayout(actual);Require(newSound.SoundOffset-b.Offset==oldSound.SoundOffset-a.Offset+AddedWorldBytes&&oldSound.Sound.CopyComponent().SequenceEqual(newSound.Sound.CopyComponent()),"whole retained native sound component and pointer boundary");
  int count=I(world[0],4);var records=Enumerable.Range(FirstMaterial,9).Select(id=>world[0].AsSpan(8+id*16,16).ToArray().Concat(world[0].AsSpan(8+count*16+id*168,168).ToArray()).ToArray()).ToArray();Materials.RequireFinal(final.Vram().ToArray(),records);
 }
}
