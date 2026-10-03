using Spyro.Editor.Core.Universal;using Spyro.Editor.Core.Exporting;using System.Text.Json;using System.Buffers.Binary;
internal static class RescueAudit
{
 internal static int Run(UniversalSourceLibrary lib,UniversalComposeResult compose,string output)
 {
var original=lib.Get("artisans").Data;var good=new UniversalLevelData(compose.LevelData);var bad=FormerOrder(good,compose.Classes.Single(c=>c.ClassId==0xfd).DestinationClassId,compose.Classes.Single(c=>c.ClassId==0x10f).DestinationClassId);int checks=0;void Check(bool x,string why){checks++;if(!x)throw new Exception(why);}void Refuse(Action f,string why){try{f();}catch(InvalidDataException){checks++;return;}throw new Exception("Unrefused:"+why);}
int need=UniversalAlpineArtisansRescueModels.RequiredPrefix(original,lib.Disc.Exe);Check(need==243712,"actual largest five native dragon header/sound prefixes");Refuse(()=>UniversalAlpineArtisansRescueModels.RequirePrefix(bad,original,lib.Disc.Exe),"failed former capacity layout");UniversalAlpineArtisansRescueModels.RequirePrefix(good,original,lib.Disc.Exe);checks++;
Check(new[]{(0x14b70,0x15370),(0x133e0,0x13658),(0x16958,0x169ac)}.All(span=>lib.Disc.Exe.AsSpan(span.Item1-0xf800,span.Item2-span.Item1).SequenceEqual(compose.Executable.AsSpan(span.Item1-0xf800,span.Item2-span.Item1))),"entire native rescue arithmetic/copy/model reload code unchanged");
var wad=lib.Disc.RebuildWad(new Dictionary<int,byte[]>{{9,compose.Overlay},{10,compose.LevelData}});for(int n=0;n<256;n++)if(n is not(9 or 10)){int offset=BitConverter.ToInt32(wad,n*8),length=BitConverter.ToInt32(wad,n*8+4);Check(lib.Disc.EntryBytes(n).AsSpan().SequenceEqual(wad.AsSpan(offset,length)),"unchanged current composed WAD entry"+n);}
Check(bad.Subfiles().SequenceEqual(good.Subfiles())&&bad.Bytes.Length==good.Bytes.Length,"no allocation growth or loss");var regs=bad.Registrations();foreach(var r in regs){var nr=good.Registrations().Single(x=>x.ClassId==r.ClassId);Check(bad.Package(r.ClassId).SequenceEqual(good.Package(r.ClassId)),"every complete registered native/imported model"+r.ClassId);Check((r.Root&7)==(nr.Root&7),"native root alignment"+r.ClassId);}Check(good.Registrations().Select(x=>x.Root).SequenceEqual(good.Registrations().Select(x=>x.Root).Order()),"registration roots remain ascending");
var sf=good.Subfiles()[2];var delta=Enumerable.Range(0,good.Bytes.Length).Where(i=>bad.Bytes[i]!=good.Bytes[i]).ToArray();Check(delta.All(i=>i>=sf.Offset&&i<sf.Offset+sf.Length||i>=0x50&&i<0x150||i>=0x150&&i<0x1d0),"only full model ordering and registration tables differ");
foreach(int n in Enumerable.Range(4,5)){var s=original.Subfiles()[n];var t=good.Subfiles()[n];Check(original.Bytes.AsSpan(s.Offset,s.Length).SequenceEqual(good.Bytes.AsSpan(t.Offset,t.Length)),"every byte of retained rescue stream"+n);foreach(int o in new[]{4,8,12}){var b=new UniversalLevelData(original.Bytes.ToArray());b.Bytes[s.Offset+o]^=1;Refuse(()=>UniversalAlpineArtisansRescueModels.RequiredPrefix(b,lib.Disc.Exe),"rescue stream/header authentication"+n+"/"+o);}}
var dr=good.Registrations().Single(r=>r.ClassId==250);int avail=(dr.Root-sf.Offset)/2048*2048;Check(avail==258048&&avail-need==14336,"real worst stream reservation margin");foreach(int prefix in new[]{need-2048,need-4}){var b=new UniversalLevelData(good.Bytes.ToArray());b.Put32(0x50+4*dr.Slot,sf.Offset+prefix);Refuse(()=>UniversalAlpineArtisansRescueModels.RequirePrefix(b,original,lib.Disc.Exe),"undersized actual rescue prefix"+prefix);}var exact=new UniversalLevelData(good.Bytes.ToArray());exact.Put32(0x50+4*dr.Slot,sf.Offset+need);UniversalAlpineArtisansRescueModels.RequirePrefix(exact,original,lib.Disc.Exe);checks++;
var exe=lib.Disc.Exe.ToArray();exe[0x14df8-0xf800]^=1;Refuse(()=>UniversalAlpineArtisansRescueModels.RequirePrefix(good,original,exe),"native memcpy owner code mutation");var missing=new UniversalLevelData(good.Bytes.ToArray());BinaryPrimitives.WriteUInt16LittleEndian(missing.Bytes.AsSpan(0x150+2*dr.Slot),249);Refuse(()=>UniversalAlpineArtisansRescueModels.RequirePrefix(missing,original,lib.Disc.Exe),"missing native statue owner");
Directory.CreateDirectory(output);
var overlay=new UniversalOverlay(compose.Overlay,UniversalExeBindings.Read(compose.Executable,10));var sound=new NativeLevelSoundLayout(good.Bytes);uint model=overlay.CopyBuffer+(uint)(sound.SoundEnd-sound.DataOffset);
Check(model+(uint)(dr.Root-sf.Offset)==0x8015a2e8,"actual corrected model250 native root from current compose output");
var receipt=new{Status="DRAGON_RESCUE_CAPACITY_SOURCE_MODEL_MUTATIONS_PASS_NATIVE_RUNTIME_PENDING",Checks=checks,RequiredPrefix=need,AvailablePrefix=avail,PrefixMargin=avail-need,ChangedBytes=delta.Length,FullModels=regs.Count};
File.WriteAllText(Path.Combine(output,"rescue-mutations.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllBytes(Path.Combine(output,"rescue-original.exe"),lib.Disc.Exe);File.WriteAllBytes(Path.Combine(output,"rescue-original.data"),original.Bytes);File.WriteAllBytes(Path.Combine(output,"rescue-former-order.data"),bad.Bytes);
File.WriteAllText(Path.Combine(output,"rescue-replay-input.json"),JsonSerializer.Serialize(new{ModelStart=model,WadLba=lib.Disc.WadLba,LevelEntryOffset=BitConverter.ToInt32(wad,80),CorrectedDataSHA256=UniversalAlpineArtisansHost.Hash(good.Bytes),OriginalDataSHA256=UniversalAlpineArtisansHost.Hash(original.Bytes),NativeExeSHA256=UniversalAlpineArtisansHost.Hash(lib.Disc.Exe),Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("ALPINE_ARTISANS_RESCUE_MUTATIONS_OK checks="+checks);return checks;

 }
 private static UniversalLevelData FormerOrder(UniversalLevelData actual,int fdClass,int druidClass)
 {
  // Independently reconstruct the previously unsafe append order from CURRENT
  // complete packages. No private candidate, RAM or expected-output fixture.
  var next=new UniversalLevelData(actual.Bytes.ToArray());var regs=actual.Registrations();var fd=regs.Single(r=>r.ClassId==fdClass);int bytes=actual.Package(fdClass).Length;int beforeDruid=regs.Single(r=>r.ClassId==druidClass).Root;int target=beforeDruid-bytes;
  actual.Bytes.AsSpan(fd.Root+bytes,target-fd.Root).CopyTo(next.Bytes.AsSpan(fd.Root));actual.Bytes.AsSpan(fd.Root,bytes).CopyTo(next.Bytes.AsSpan(target));
  var roots=regs.Select(r=>(Class:r.ClassId,Root:r.ClassId==fdClass?target:r.Root>=fd.Root+bytes&&r.Root<beforeDruid?r.Root-bytes:r.Root)).OrderBy(r=>r.Root).ToArray();
  for(int n=0;n<roots.Length;n++){next.Put32(0x50+4*(n+1),roots[n].Root);BinaryPrimitives.WriteUInt16LittleEndian(next.Bytes.AsSpan(0x150+2*(n+1)),(ushort)roots[n].Class);}return next;
 }
}
