using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Immutable original-USA consumed CRT prefix and complete overlay
/// literal/direct-reference domain. This alone does not issue computed-reference,
/// ready/bootstrap or unload lifetime proof. Cold boot retains original disk CRT.</summary>
internal sealed class NativeGreenDruidGroup1ResidentEntrySource
{
 private readonly byte[] _exe;
 private NativeGreenDruidGroup1ResidentEntrySource(byte[] exe){_exe=exe.ToArray();}
 internal byte[] CopyOriginalExecutable()=>_exe.ToArray();
 private static string Hash(byte[]b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 internal static NativeGreenDruidGroup1ResidentEntrySource Capture(UniversalSourceLibrary original)
 {
  UniversalSourceLibrary.RequireOriginal(original.ImagePath);byte[]exe=original.Disc.Exe;
  UniversalGreenDruidGroup1TerrainQueues.Require(Hash(exe)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","complete original CRT/native helpers");
  const uint first=0x8005B8E0,end=0x8005B960;
  void Census(byte[]b,uint origin,bool header){for(int n=0;n+4<=b.Length;n+=4){uint w=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(n));uint pc=origin+(uint)n;if(w>=first&&w<end)UniversalGreenDruidGroup1TerrainQueues.Require(header&&n==0x10&&w==first,"CRT literal ownership");if((w>>26)is 2 or 3){uint t=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);UniversalGreenDruidGroup1TerrainQueues.Require(t<first||t>=end,"no original code calls retired CRT prefix");}}}
  Census(exe,UniversalExeBindings.ExeBase,true);
  {byte[]b=original.Disc.EntryBytes(9);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55","original artisans full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(11);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="876c0145649bb5b26921858d4d677468463974901dcc416dcba5ccea25caa069","original stonehill full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(13);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32","original darkhollow full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(15);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5","original townsquare full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(17);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="3b412fceb81e9ab60eef189fc3292387efae0886f4ed0ada0e169f9a190eb049","original toasty full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(19);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="42b4e8a637ff91f87b884a9beea3266299d01ae36d36b437436d247f78812f59","original sunnyflight full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(21);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc","original peacekeepers full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(23);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="8aa4036028de3ea9cdc6ee93b4d96cb7dc2b9fa5f818b6e748c0c2bda946cc0f","original drycanyon full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(25);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="25f0fc287cc8b6b5b70d960630025e009e0e425f7af723dadf008605af5b0286","original clifftown full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(27);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="a50a830556b071b7b7afd6f30232b7e4d8be2ab3329dff24a4332e19ed5ff5a6","original icecavern full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(29);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="73c7fd7ba2083507edbeca38393c825072ec2bd92e0bbbe9c3df5f5010aa8de9","original doctorshemp full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(31);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="3891e7af5f83825dbebc80952e173fbbea8125653d4b00fc7b1980a1128b873a","original nightflight full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(33);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d","original magiccrafters full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(35);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="f7c9a174f64959fb53b4058f9786df223a9c142b2289563ca31edc6a24c6d383","original alpineridge full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(37);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="6c153366d9b3dd6d25a7aa8e8d1ea6708bdf97b04ee35912b11df0f8b9913140","original highcaves full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(39);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1","original wizardpeak full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(41);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef","original blowhard full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(43);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="e8d91a7c5f9f5d06b95ac01ade6fa4423485fd3d5dd75dd0dc15cc8319d46a59","original crystalflight full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(45);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="6bed996a20df44803ec4eefe434294082c3f5814ce56080e0594bab95eeb287f","original beastmakers full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(47);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="285fc63da6b536af2b41c19e41d1db03a980007226dc55e5d930a1998f39c50e","original terracevillage full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(49);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="3b37eb8b4b0f5fb74e7d263670705e20c807f2d8f1bbf03f34a461eb1c3e2ff6","original mistybog full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(51);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="f7363f0f2a03e6ddcc906fd68c914905c3704e49e058220941ac9a0ece488405","original treetops full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(53);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="97a960c4c4e9f6933d3dc4ab19a31987a105f33dfdfd98433021977e7778543f","original metalhead full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(55);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="5069a570d9cce769fed272d8c8fe24a56ade3317370fe5c688ebb43126570e97","original wildflight full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(57);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="dcbe76c16c51393e784037af420c7fb7c00cecd526ed4d890ae5f1b257cdb352","original dreamweavers full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(59);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="a8b99270c37a37363824cf1fd2081c092968abef62df9e74d70ae0017b984727","original darkpassage full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(61);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="e0f1f9e14726e4ca9d41e9e8bca112290263f00ce85413a7e389ab59c9e358c3","original loftycastle full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(63);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="e714a2c4aff4a7e8d4980fa091540f4fdcd320b4fe4c532fa8c6c3d36fd7fbfc","original hauntedtowers full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(65);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="e09f6cb0adeb6275b04549507ed5d8424c1f6045ecfde0f50424f6ba58713f49","original jacques full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(67);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="67de47f547e65d4345401dedbb988b38c0de61648c903ab98d1a64f64fce9f0e","original icyflight full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(69);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="16b6a88de15db049ee69c53fea4fbd21a3725fce4effa97cd9851467a2ea9d64","original gnastysworld full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(71);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="1d6f62cc80f515c73efc50b9c8435a36cb51b5e2bcea8823005ce05ed856fad9","original gnorccove full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(73);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="55d0845c750c0fcbd13d69633d1481d288bb3b02ce33bf60e3b17866111739dd","original twilightharbor full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(75);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="9b8b9677df191c917ed45896192db86c82cdcad2853ba1cd7dfd6feca3be43a4","original gnastygnorc full overlay");Census(b,UniversalOverlay.Base,false);}
  {byte[]b=original.Disc.EntryBytes(77);UniversalGreenDruidGroup1TerrainQueues.Require(Hash(b)=="2117b3b14116b98035d9b003df3f299ef127ee34558f700af2be9fcb7e165a6e","original gnastysloot full overlay");Census(b,UniversalOverlay.Base,false);}
  return new(exe);
 }
 internal void RequireExecutable(byte[]actual)
 {
  const uint first=0x8005B8E0,end=0x8005B988;int n=(int)(first-UniversalExeBindings.ExeBase);
  UniversalGreenDruidGroup1TerrainQueues.Require(actual.AsSpan(n,(int)(end-first)).SequenceEqual(_exe.AsSpan(n,(int)(end-first)))&&actual.AsSpan(0,0x800).SequenceEqual(_exe.AsSpan(0,0x800)),"on-disc complete CRT/PS-X entry/active continuation intact");
  foreach(var p in new (uint A,int N)[]{(A:0x8001ED5Cu,N:0x3FC),(0x800626E8u,12)}){int at=(int)(p.A-UniversalExeBindings.ExeBase);UniversalGreenDruidGroup1TerrainQueues.Require(actual.AsSpan(at,p.N).SequenceEqual(_exe.AsSpan(at,p.N)),"complete original frame/FlushCache owner retained");}
 }
}
