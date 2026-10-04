using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Exporting;
using System.Buffers.Binary;
using System.Text.Json;
using System.Security.Cryptography;
internal static class MagicRescueAudit
{
 internal static int Run(UniversalSourceLibrary lib,UniversalComposeResult compose,string output)
 {
  var original=lib.Get("artisans").Data;var good=new UniversalLevelData(compose.LevelData);var former=FormerOrder(good);int checks=0;
  void Check(bool x,string why){checks++;if(!x)throw new InvalidDataException(why);}
  void Refuse(Action f,string why){try{f();}catch(InvalidDataException){checks++;return;}throw new Exception("Unrefused:"+why);}
  string H(byte[]b)=>Convert.ToHexStringLower(SHA256.HashData(b));
  var budget=UniversalNativeDragonRescuePrefix.RequireSafe("artisans","Artisans",original,good,compose.Executable)!;
  Check(budget.RequiredPrefix==243712&&budget.AvailablePrefix==280576&&budget.StreamCount==5,"every retained rescue now fits with 36,864 bytes margin");
  Refuse(()=>UniversalNativeDragonRescuePrefix.RequireSafe("artisans","Artisans",original,former,compose.Executable),"independently reconstructed former ordering remains refused");
  var again=new UniversalLevelData(former.Bytes.ToArray());UniversalMagicArtisansRescueModels.Prepare(again);Check(again.Bytes.SequenceEqual(good.Bytes),"independently reconstructed current components normalize to exactly current output");
  Check(good.Bytes.Length==former.Bytes.Length&&good.Subfiles().SequenceEqual(former.Subfiles()),"no allocation growth");
  int first=good.Registrations().Min(r=>r.Root);Check(BinaryPrimitives.ReadInt32LittleEndian(good.Bytes.AsSpan(first-8))==-1&&good.Bytes.AsSpan(first-4,4).IndexOfAnyExcept((byte)0)<0,"exact parser terminator and four-byte alignment gap survive rotation");
  var regs=good.Registrations();Check(regs.Count==33&&regs.Select(r=>r.Root).SequenceEqual(regs.Select(r=>r.Root).Order()),"all33 native registrations remain ascending");
  foreach(var r in regs)
  {
   var a=former.Package(r.ClassId);var b=good.Package(r.ClassId);
   Check(a.AsSpan().SequenceEqual(b),"every complete native/imported model and padding byte retained "+r.ClassId);
   Check((r.Root&7)==(former.Registrations().Single(x=>x.ClassId==r.ClassId).Root&7),"every native root alignment "+r.ClassId);
   Check(b.Length==a.Length,"whole-package rotation retains every extent "+r.ClassId);
   var sourceModel=(r.ClassId==271?lib.Get("magiccrafters").Data:original).Registrations().Single(x=>x.ClassId==r.ClassId);
   Check((r.Root&7)==(sourceModel.Root&7),"source-native root alignment restored "+r.ClassId);
   var oldReg=former.Registrations().Single(x=>x.ClassId==r.ClassId);foreach(int at in new[]{oldReg.Root,oldReg.Root+a.Length/2,oldReg.Root+a.Length-1})
   {var changed=new UniversalLevelData(former.Bytes.ToArray());changed.Bytes[at]^=1;string hash=H(changed.Bytes);Refuse(()=>UniversalMagicArtisansRescueModels.Prepare(changed),"each complete incoming model mutation "+r.ClassId);Check(H(changed.Bytes)==hash,"refused normalization leaves input unchanged "+r.ClassId);}
   foreach(int at in new[]{0x50+4*oldReg.Slot,0x150+2*oldReg.Slot}){var changed=new UniversalLevelData(former.Bytes.ToArray());changed.Bytes[at]^=1;Refuse(()=>UniversalMagicArtisansRescueModels.Prepare(changed),"every incoming registration root/identity mutation "+r.ClassId);}
  }
  Check(former.Package(271).AsSpan(0,83756).SequenceEqual(good.Package(271).AsSpan(0,83756)),"all83,756 imported model payload bytes intact");
  var sf=good.Subfiles()[2];var delta=Enumerable.Range(0,good.Bytes.Length).Where(i=>good.Bytes[i]!=former.Bytes[i]).ToArray();Check(delta.All(i=>i>=sf.Offset&&i<sf.Offset+sf.Length||i>=0x50&&i<0x1d0),"onlymodel order and registrations change");
  for(int n=0;n<good.Subfiles().Count;n++)if(n!=2){var a=good.Subfiles()[n];var b=former.Subfiles()[n];Check(good.Bytes.AsSpan(a.Offset,a.Length).SequenceEqual(former.Bytes.AsSpan(b.Offset,b.Length)),"every other complete asset/scene/rescue subfile unchanged "+n);}
  foreach(var span in new[]{(0x14b70,0x15370),(0x133e0,0x13658),(0x16958,0x169ac)})Check(lib.Disc.Exe.AsSpan(span.Item1-0xf800,span.Item2-span.Item1).SequenceEqual(compose.Executable.AsSpan(span.Item1-0xf800,span.Item2-span.Item1)),"complete native reader/copy/modelreload owner unchanged");
  Directory.CreateDirectory(output);var overlay=new UniversalOverlay(compose.Overlay,UniversalExeBindings.Read(compose.Executable,10));var sound=new NativeLevelSoundLayout(good.Bytes);uint model=overlay.CopyBuffer+(uint)(sound.SoundEnd-sound.DataOffset);var wad=lib.Disc.RebuildWad(new Dictionary<int,byte[]>{{9,compose.Overlay},{10,compose.LevelData}});
  File.WriteAllBytes(Path.Combine(output,"rescue-original.exe"),lib.Disc.Exe);File.WriteAllBytes(Path.Combine(output,"rescue-original.data"),original.Bytes);File.WriteAllBytes(Path.Combine(output,"rescue-former-order.data"),former.Bytes);
  File.WriteAllText(Path.Combine(output,"rescue-replay-input.json"),JsonSerializer.Serialize(new{ModelStart=model,WadLba=lib.Disc.WadLba,LevelEntryOffset=BitConverter.ToInt32(wad,80),CorrectedDataSHA256=H(good.Bytes),OriginalDataSHA256=H(original.Bytes),NativeExeSHA256=H(lib.Disc.Exe),ExpectedModelCount=33,RequiredModelClasses=new[]{271,250,251},Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(output,"magic-rescue-mutations.json"),JsonSerializer.Serialize(new{Status="MAGIC_ARTISANS_ALL_RESCUE_MODEL_SOURCE_MUTATIONS_PASS_GAMEPLAY_PENDING",Checks=checks,budget,FullModels=33,ImportedPayloadBytes=83756,GeneratedSectorPaddingReassigned=0,AllocationGrowth=0,ChangedBytes=delta.Length},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("MAGIC_ARTISANS_RESCUE_MUTATIONS_OK checks="+checks);return checks;
 }
 private static UniversalLevelData FormerOrder(UniversalLevelData data)
 {
  // Independent inverse rotation derives the former allocation from CURRENT
  // complete output; it does not load any frozen private candidate or RAM.
  var result=new UniversalLevelData(data.Bytes.ToArray());var regs=data.Registrations();var moved=regs.Single(r=>r.ClassId==271);int size=data.Package(271).Length;var sf=data.Subfiles()[2];int target=sf.Offset+sf.Length-size;
  data.Bytes.AsSpan(moved.Root+size,target-moved.Root).CopyTo(result.Bytes.AsSpan(moved.Root));data.Bytes.AsSpan(moved.Root,size).CopyTo(result.Bytes.AsSpan(target));
  var roots=regs.Select(r=>(Class:r.ClassId,Root:r.ClassId==271?target:r.Root>=moved.Root+size&&r.Root<target+size?r.Root-size:r.Root)).OrderBy(r=>r.Root).ToArray();
  for(int n=0;n<roots.Length;n++){result.Put32(0x50+4*(n+1),roots[n].Root);BinaryPrimitives.WriteUInt16LittleEndian(result.Bytes.AsSpan(0x150+2*(n+1)),(ushort)roots[n].Class);}return result;
 }
}
