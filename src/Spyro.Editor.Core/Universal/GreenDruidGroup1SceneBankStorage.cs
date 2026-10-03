using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;
namespace Spyro.Editor.Core.Universal;
/// <summary>Nine exact issued group1-world records in the native seven banks.
/// Retained row/property pointers and all existing bank payloads are preserved.</summary>
internal sealed class GreenDruidGroup1SceneBankStorage
{
 private readonly byte[] _before,_after;
 private readonly byte[][] _beforeBanks,_afterBanks;
 private readonly UniversalSceneLayout _beforeLayout;
 private readonly int[] _oldStarts,_newStarts,_newRecordCounts;
 internal int GroupId{get;}
 internal int AddedByteCount{get;}
 internal int OriginalPrefix{get;}
 internal int NewPrefix{get;}
 internal IReadOnlyList<int> OriginalBankCounts=>_beforeBanks.Select(b=>I(b,4)).ToArray();
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static void Require(bool b,string why){if(!b)throw new InvalidDataException("Green Druid detached scene bank storage: "+why);}
 private static byte[][] Banks(byte[]scene)
 {
  int at=0x88;List<byte[]> banks=[];
  for(int role=0;role<7;role++){int size=I(scene,at),count=I(scene,at+4);Require(size>=8&&size%4==0&&count>=0&&8+count*4<=size&&at+size<=scene.Length,"complete native bank bounds");for(int n=0;n<count;n++){int p=4+I(scene,at+8+n*4);Require(p>=8+count*4&&p<size&&p%4==0,"native record pointer owner");}banks.Add(scene.AsSpan(at,size).ToArray());at+=size;}
  return banks.ToArray();
 }
 internal GreenDruidGroup1SceneBankStorage(UniversalLevelData host,UniversalGreenDruidGroup1WorldRebinding binding)
 {
  var sf=host.Subfiles()[3];_before=host.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();_beforeLayout=host.Scene();_beforeBanks=Banks(_before);OriginalPrefix=_beforeLayout.MobyComponent;
  GroupId=binding.DestinationGroup;
  var rebound=binding.Resources;Require(rebound.AnimationRecords.Length==9&&rebound.Roles.Length==9,"all nine original records");
  _afterBanks=_beforeBanks.Select(b=>b.ToArray()).ToArray();_newRecordCounts=new int[7];
  foreach(int role in new[]{2,3,4})
  {
   var before=_beforeBanks[role];var added=rebound.AnimationRecords.Where((r,n)=>rebound.Roles[n]==role).ToArray();int count=I(before,4),growth=added.Length*4;
   // Sector-header FF means no animation; record index255 must stay reserved.
   Require(role==4||count+added.Length<=255,"byte animation indexFF is reserved for no animation");_newRecordCounts[role]=added.Length;
   byte[] b=new byte[before.Length+growth+added.Sum(r=>r.Length)];W(b,0,b.Length);W(b,4,count+added.Length);
   for(int n=0;n<count;n++)W(b,8+n*4,I(before,8+n*4)+growth);
   before.AsSpan(8+count*4).CopyTo(b.AsSpan(8+(count+added.Length)*4));int cursor=before.Length+growth;
   for(int n=0;n<added.Length;n++){W(b,8+(count+n)*4,cursor-4);added[n].CopyTo(b,cursor);cursor+=added[n].Length;}_afterBanks[role]=b;
  }
  _oldStarts=new int[7];_newStarts=new int[7];int old=0x88,@new=0x88;
  for(int n=0;n<7;n++){_oldStarts[n]=old;_newStarts[n]=@new;old+=_beforeBanks[n].Length;@new+=_afterBanks[n].Length;}Require(old==OriginalPrefix,"native scene prefix matches complete banks");NewPrefix=@new;AddedByteCount=NewPrefix-OriginalPrefix;
  using MemoryStream stream=new();stream.Write(_before.AsSpan(0,0x88));foreach(var b in _afterBanks)stream.Write(b);stream.Write(_before.AsSpan(OriginalPrefix,_beforeLayout.FixupsOffset-OriginalPrefix));byte[] body=stream.ToArray();
  foreach(int field in _beforeLayout.Fixups){int target=Map(field);W(body,target,Map(I(_before,field)));}
  using MemoryStream final=new();final.Write(body);byte[] word=new byte[4];W(word,0,_beforeLayout.Fixups.Count);final.Write(word);foreach(int field in _beforeLayout.Fixups){W(word,0,Map(field));final.Write(word);}_after=final.ToArray();ValidateFinal(_after);
 }
 private int Map(int at)
 {
  Require(at>=0&&at<_beforeLayout.UsedEnd,"retained scene-relative pointer bounds");if(at<0x88)return at;if(at>=OriginalPrefix)return checked(at+AddedByteCount);
  for(int n=0;n<7;n++)if(at<_oldStarts[n]+_beforeBanks[n].Length){int relative=at-_oldStarts[n],tableEnd=8+I(_beforeBanks[n],4)*4;return _newStarts[n]+relative+(relative>=tableEnd?_newRecordCounts[n]*4:0);}
  throw new InvalidDataException("Unmapped retained native scene address");
 }
 internal byte[] CopyScene()=>_after.ToArray();
 internal void ValidateFinal(byte[] scene)
 {
  Require(scene.AsSpan().SequenceEqual(_after),"issued full final scene changed");var layout=UniversalLevelData.ParseScene(scene);Require(layout.MobyComponent==NewPrefix&&layout.RowCount==_beforeLayout.RowCount&&layout.Fixups.Count==_beforeLayout.Fixups.Count,"retained native row/fixup identities");
  foreach(int f in _beforeLayout.Fixups)Require(I(scene,Map(f))==Map(I(_before,f))&&layout.Fixups.Contains(Map(f)),"every native retained pointer/field translated");
  var finalBanks=Banks(scene);for(int role=0;role<7;role++)
  {
   var old=_beforeBanks[role];var final=finalBanks[role];int count=I(old,4),growth=_newRecordCounts[role]*4;
   Require(final.AsSpan(8+(count+_newRecordCounts[role])*4,old.Length-8-count*4).SequenceEqual(old.AsSpan(8+count*4)),"every retained bank payload byte preserved");
   for(int n=0;n<count;n++)Require(I(final,8+n*4)==I(old,8+n*4)+growth,"every original relative record pointer retains its exact target");
  }
 }
}
