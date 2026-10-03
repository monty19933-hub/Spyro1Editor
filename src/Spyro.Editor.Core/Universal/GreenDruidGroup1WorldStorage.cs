using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;
/// <summary>Immutable complete group1 world draft. Never grants actor/renderer admission.
/// A separately issued terrain queue capability must bind this exact draft before installation.</summary>
internal sealed class GreenDruidGroup1WorldStorage
{
 private readonly byte[] _before,_after;
 private readonly byte[][] _beforeWorld,_afterWorld;
 internal UniversalGreenDruidGroup1WorldPrerequisites Proof{get;}
 internal UniversalGreenDruidGroup1WorldRebinding Binding{get;}
 internal GreenDruidGroup1CollisionStorage Collision{get;}
 internal GreenDruidGroup1SceneBankStorage Banks{get;}
 internal GreenDruidGroup1MaterialStorage Materials{get;}
 internal bool ForeignPlacementAdmitted=>false;
 internal int FirstSector{get;}
 internal int FirstMaterial{get;}
 internal int AddedWorldBytes{get;}
 internal int ConservativeHighQueueContentWords{get;}
 internal IReadOnlyList<int> DeltaRaw=>Binding.Resources.DeltaRaw.ToArray();
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid group1 world draft: "+why);}
 internal GreenDruidGroup1WorldStorage(UniversalGreenDruidGroup1WorldPrerequisites proof,UniversalLevelData host,IReadOnlyList<int> delta,UniversalVramAllocator allocator,IReadOnlyList<int> physicalRows)
 {
  Proof=proof;_before=host.Bytes.ToArray();_beforeWorld=GreenDruidCompleteWorldStorage.World(host);var sectors=GreenDruidCompleteWorldStorage.Sectors(_beforeWorld[1]);FirstSector=sectors.Length;FirstMaterial=I(_beforeWorld[0],4);
  var banks=UniversalGreenDruidGroup1WorldRebinding.Banks(host);var occupied=banks.SelectMany(b=>Enumerable.Range(0,I(b,4)).Select(n=>(int)b[4+I(b,8+n*4)])).ToHashSet();int group=!occupied.Contains(1)?1:Enumerable.Range(0,255).FirstOrDefault(g=>!occupied.Contains(g),-1);Require(group>=0,"actual free native group byte");var collision=GreenDruidAnimatedCollisionStorage.Read(_beforeWorld[4]);
  Binding=UniversalGreenDruidGroup1WorldRebinding.Bind(proof,proof.CopyTranslated(delta),host,group,Enumerable.Range(FirstSector,6).ToArray(),collision.Count,collision.Count+50,FirstMaterial,physicalRows,new Dictionary<int,int>{{16,1},{20,2}});
  Collision=new(proof,_beforeWorld[4],delta,Binding);Banks=new(host,Binding);
  ConservativeHighQueueContentWords=sectors.Sum(s=>s[22]==0?0:1+s[22])+proof.HighQueueContentWords;
  // This is an inert draft: conservative demand exceeds original queues. No resource count grants admission.
  // Queue worker must bind the complete draft and issue its typed renderer reservation before root exports it.
  Materials=GreenDruidGroup1MaterialStorage.Capture(proof,host.Vram().ToArray(),allocator.Clone());
  _afterWorld=_beforeWorld.Select(p=>p.ToArray()).ToArray();_afterWorld[0]=Materials.AppendTable(_beforeWorld[0]);_afterWorld[1]=PackSectors(sectors.Concat(Binding.Resources.Sectors).ToArray());_afterWorld[2]=Binding.VisibilityComponent;_afterWorld[4]=Collision.CopyComponent();AddedWorldBytes=_afterWorld.Sum(p=>p.Length)-_beforeWorld.Sum(p=>p.Length);Require(AddedWorldBytes>=0,"complete host resource append cannot discard owners");
  var output=new UniversalLevelData(_before.ToArray());var sf=output.Subfiles()[1];int oldEnd=_beforeWorld.Sum(p=>p.Length);byte[] tail=output.Bytes.AsSpan(sf.Offset+oldEnd,sf.Length-oldEnd).ToArray();if(AddedWorldBytes>0)output.GrowSubfile(1,UniversalLevelData.Align(AddedWorldBytes,2048));sf=output.Subfiles()[1];output.Bytes.AsSpan(sf.Offset,sf.Length).Clear();int cursor=sf.Offset;foreach(var p in _afterWorld){p.CopyTo(output.Bytes,cursor);cursor+=p.Length;}tail.CopyTo(output.Bytes,cursor);Materials.CopyGpu().CopyTo(output.Vram());output.ReplaceScene(Banks.CopyScene());_after=output.Bytes.ToArray();RequireDraftOwnership();
  // Caller VRAM allocator is deliberately untouched by a draft. Root commits exact reservations only after budget issuance.
 }
 private static byte[] PackSectors(byte[][] sectors)
 {int at=8+sectors.Length*4;byte[] b=new byte[at+sectors.Sum(s=>s.Length)];W(b,0,b.Length);W(b,4,sectors.Length);for(int n=0;n<sectors.Length;n++){W(b,8+n*4,at-4);sectors[n].CopyTo(b,at);at+=sectors[n].Length;}return b;}
 internal byte[] CopyDraftLevelData()=>_after.ToArray();
 internal void RequireDraftOwnership()=>RequireDraftOwnership(_after);
 internal void RequireDraftOwnership(byte[] actual)
 {
  Require(actual.AsSpan().SequenceEqual(_after),"complete immutable draft body");var final=new UniversalLevelData(actual);var world=GreenDruidCompleteWorldStorage.World(final);for(int n=0;n<6;n++)Require(world[n].SequenceEqual(_afterWorld[n]),"each complete world component");Collision.ValidateReadback(world[4]);var sf=final.Subfiles()[3];Banks.ValidateFinal(final.Bytes.AsSpan(sf.Offset,final.Scene().UsedEnd).ToArray());
  var old=new UniversalLevelData(_before);var a=old.Subfiles()[1];var b=final.Subfiles()[1];int oldEnd=_beforeWorld.Sum(p=>p.Length),newEnd=world.Sum(p=>p.Length);Require(_before.AsSpan(a.Offset+oldEnd,a.Length-oldEnd).SequenceEqual(actual.AsSpan(b.Offset+newEnd,a.Length-oldEnd)),"all original trailing portal/particle/sound/padding bytes");
  var oldSound=new NativeLevelSoundLayout(_before);var newSound=new NativeLevelSoundLayout(actual);Require(newSound.SoundOffset-b.Offset==oldSound.SoundOffset-a.Offset+AddedWorldBytes&&oldSound.Sound.CopyComponent().SequenceEqual(newSound.Sound.CopyComponent()),"whole retained native sound component and boundary");
  int count=I(world[0],4);var records=Enumerable.Range(FirstMaterial,16).Select(id=>world[0].AsSpan(8+id*16,16).ToArray().Concat(world[0].AsSpan(8+count*16+id*168,168).ToArray()).ToArray()).ToArray();Materials.RequireFinal(final.Vram().ToArray(),records);
 }
}
