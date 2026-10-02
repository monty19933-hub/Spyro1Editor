using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Exporting;

if (args.Length == 0) { UnitChecks(); return; }
if (args.Length != 3) throw new ArgumentException("<original USA BIN> <catalog workspace> <new report JSON>");
if (File.Exists(args[2])) throw new IOException("Preserve existing receipts; choose a new report path.");
SourceChecks(args);

static void UnitChecks()
{
int checks=0;void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
byte[] Fixture(int padding,int ownedZeros,bool dirtyPadding){byte[] b=new byte[0x2000];void Put(int o,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o),v);Put(0,0x800);Put(4,0x800);Put(8,0x1000);Put(12,0x800);Put(16,0x1800);Put(20,0x800);Put(0x54,0x1800);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x152),1);Array.Fill(b,(byte)0x5A,0x800,0x800);Array.Fill(b,(byte)0xA5,0x1000,0x800-padding);Array.Fill(b,(byte)0xC7,0x1800,0x800);Array.Clear(b,0x1800-padding-ownedZeros,ownedZeros);if(dirtyPadding)b[0x17FF]=0x19;return b;}
foreach(int padding in new[]{0,4,8,12,16,20,24})foreach(int zeros in new[]{0,4,8,12,28})foreach(bool dirty in new[]{false,true}){
 if(padding==0&&dirty)continue;int used=0x800-padding;
 foreach(int offset in new[]{0,used/2,used}){
  var before=Fixture(padding,zeros,dirty);var data=new UniversalLevelData(before.ToArray());byte[] insert=Enumerable.Range(1,12).Select(i=>(byte)i).ToArray();data.InsertWithinSubfile(1,offset,insert,used);int growth=padding>=12&&!dirty?0:0x800;
  Check(data.Bytes.Length==before.Length+growth,"only certified padding consumed");Check(data.Subfiles()[1].Length==0x800+growth,"data subfile length");Check(data.Subfiles()[2].Offset==0x1800+growth,"following subfile offset");Check(data.Registrations().Single().Root==0x1800+growth,"model root relocation");
  Check(data.Bytes.AsSpan(0x1000,offset).SequenceEqual(before.AsSpan(0x1000,offset)),"prefix intact");Check(data.Bytes.AsSpan(0x1000+offset,12).SequenceEqual(insert),"insert exact");Check(data.Bytes.AsSpan(0x1000+offset+12,used-offset).SequenceEqual(before.AsSpan(0x1000+offset,used-offset)),"all owned bytes intact including zero tail");
  Check(data.Bytes.AsSpan(0x800,0x800).SequenceEqual(before.AsSpan(0x800,0x800)),"preceding subfile intact");Check(data.Bytes.AsSpan(0x1800+growth,0x800).SequenceEqual(before.AsSpan(0x1800,0x800)),"following subfile intact");if(dirty)Check(data.Bytes[0x17FF+12]==0x19,"unknown nonzero padding retained");
 }
}
foreach(var pair in new[]{(used:-1,offset:0),(used:2049,offset:0),(used:2040,offset:-1),(used:2040,offset:2041)}){
 var original=Fixture(8,8,false);var data=new UniversalLevelData(original.ToArray());bool refused=false;try{data.InsertWithinSubfile(1,pair.offset,new byte[12],pair.used);}catch(ArgumentOutOfRangeException){refused=true;}Check(refused,"invalid owner refused");Check(data.Bytes.SequenceEqual(original),"invalid owner no mutation");
}
var twoBefore=Fixture(20,8,false);var two=new UniversalLevelData(twoBefore.ToArray());two.InsertWithinSubfile(1,80,new byte[12],0x800-20);Check(two.Bytes.Length==twoBefore.Length,"first insertion uses 20 real padding bytes");two.InsertWithinSubfile(1,92,new byte[12],0x800-8);Check(two.Bytes.Length==twoBefore.Length+0x800,"second insertion cannot consume owned zeros");Check(two.Bytes.AsSpan(0x1000+104,0x800-20-80).SequenceEqual(twoBefore.AsSpan(0x1000+80,0x800-20-80)),"two inserts preserve all original owned bytes");
Console.WriteLine($"PARTICLE_INSERTION_UNIT_OK checks={checks}");
}

static void SourceChecks(string[] args)
{
int checks=0;
void Check(bool yes,string label){checks++;if(!yes)throw new Exception(label);}
UniversalSourceLibrary.RequireOriginal(args[0]);
var cat=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],cat);var receipts=new List<object>();
foreach(var level in cat.Levels.Where(l=>l.SourceWadEntry>0)){
 var original=lib.Get(level.Key).Data;var data=new UniversalLevelData(original.Bytes.ToArray());var before=new NativeLevelSoundLayout(data.Bytes);var sounds=data.Bytes.AsSpan(before.SoundOffset,before.Sound.ComponentByteLength).ToArray();
 var oldSf=data.Subfiles().ToArray();var oldRegs=data.Registrations().ToArray();var records=UniversalParticleDependencies.Records(data);var tile=records.Values.FirstOrDefault(x=>x.Length==8)??new byte[8];var pkg=UniversalParticleDependencies.TexturePackage(tile);int initialLength=data.Bytes.Length;
 for(int i=1;i<=2;i++){
  UniversalParticleDependencies.AppendTexture(data,65534-i,pkg);var after=new NativeLevelSoundLayout(data.Bytes);Check(data.Bytes.AsSpan(after.SoundOffset,after.Sound.ComponentByteLength).SequenceEqual(sounds),level.Key+" sound intact "+i);
  Check(after.SoundEnd<=after.DataOffset+after.DataLength,level.Key+" sound bounded "+i);Check(after.SoundOffset==before.SoundOffset+12*i,level.Key+" sound shifted "+i);
  var now=UniversalParticleDependencies.Records(data);Check(now.Count==records.Count+i,level.Key+" particle count "+i);foreach(var (type,bytes) in records)Check(now[type].AsSpan().SequenceEqual(bytes),level.Key+" particle intact "+type);Check(now[65534-i].AsSpan().SequenceEqual(tile),level.Key+" particle inserted "+i);
  var currentSf=data.Subfiles();int growth=data.Bytes.Length-initialLength;Check(growth%2048==0,level.Key+" growth alignment");Check(currentSf[1].Length==oldSf[1].Length+growth,level.Key+" data length");
  foreach(int sf in Enumerable.Range(0,oldSf.Length).Where(x=>x!=1)){Check(currentSf[sf].Offset==oldSf[sf].Offset+(sf>1?growth:0),level.Key+" subfile offset "+sf);Check(data.Bytes.AsSpan(currentSf[sf].Offset,currentSf[sf].Length).SequenceEqual(original.Bytes.AsSpan(oldSf[sf].Offset,oldSf[sf].Length)),level.Key+" subfile intact "+sf);}
  foreach(var reg in oldRegs){var newer=data.Registrations().Single(r=>r.ClassId==reg.ClassId);Check(newer.Root==reg.Root+growth&&newer.Slot==reg.Slot,level.Key+" model root "+reg.ClassId);}
  if(level.Key=="peacekeepers")Check(growth==(i==1?0:2048),"Peace Keepers only real padding consumed "+i);
 }
 receipts.Add(new{level=level.Key,originalLength=initialLength,finalLength=data.Bytes.Length,soundSha256=Convert.ToHexStringLower(SHA256.HashData(sounds)),soundBefore=before.SoundOffset,soundAfter=new NativeLevelSoundLayout(data.Bytes).SoundOffset});
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);File.WriteAllText(args[2],JsonSerializer.Serialize(new{checks,levels=receipts},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PARTICLE_SOUND_BOUNDARY_OK checks={checks} levels={receipts.Count}");
}
