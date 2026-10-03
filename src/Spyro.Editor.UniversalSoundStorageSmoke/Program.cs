using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
int checks=0;var results=new List<object>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
void Refuse(Action action,string text){try{action();throw new Exception("Expected refusal "+text);}catch(InvalidDataException e){Check(e.Message.Contains(text),"Refusal "+text);}}
var catalog=LevelCatalog.Load(args[1]);var library=new UniversalSourceLibrary(args[0],catalog);UniversalSourceLibrary.RequireOriginal(args[0]);
UniversalSoundBankStorage.RequireExecutable(library.Disc.Exe);
var changedExe=library.Disc.Exe.ToArray();changedExe[0x4CC04]^=1;Refuse(()=>UniversalSoundBankStorage.RequireExecutable(changedExe),"certified native owner");
foreach(var level in catalog.Levels){
 var source=library.Get(level.Key).Data;var copy=new UniversalLevelData(source.Bytes.ToArray());var before=new NativeLevelSoundLayout(copy.Bytes).Sound;
 int used=UniversalSoundBankStorage.Compact(copy,new(copy.Bytes));var after=new NativeLevelSoundLayout(copy.Bytes).Sound;
 Check(before.Map.SequenceEqual(after.Map),level.Key+" all256 globals");Check(before.Descriptors.Count==after.Descriptors.Count,level.Key+" all descriptor indices");
 foreach(var desc in before.Descriptors){var next=after.Descriptors[desc.Index];Check(next.CopyDescriptor().AsSpan(4).SequenceEqual(desc.CopyDescriptor().AsSpan(4)),"controls");Check(next.Sample.CopyBytes().AsSpan().SequenceEqual(desc.Sample.CopyBytes()),"entire ADPCM");}
 Check(after.Descriptors.Max(x=>x.Sample.BankOffset+x.Sample.ByteLength)==used,"packed used extent");
 results.Add(new{level.Key,UsedBytes=used,BeforeBankBytes=before.BankByteLength,AfterBankBytes=after.BankByteLength});
}
var dh=library.Get("darkhollow");var sourceLayout=new NativeLevelSoundLayout(dh.Data.Bytes);var original=sourceLayout.Sound;
// Unknown padding and absolute loop owners must fail before any destination byte changes.
var badGap=new UniversalLevelData(dh.Data.Bytes.ToArray());var starts=original.Descriptors.Select(x=>x.Sample).DistinctBy(x=>x.BankOffset).OrderBy(x=>x.BankOffset).ToArray();int gap=starts[0].BankOffset+starts[0].ByteLength;badGap.Bytes[sourceLayout.BankOffset+gap+2]=99;var witness=badGap.Bytes.ToArray();Refuse(()=>UniversalSoundBankStorage.Compact(badGap,new(badGap.Bytes)),"unmodeled native padding");Check(badGap.Bytes.AsSpan().SequenceEqual(witness),"unknown gap refusal immutable");
var badLoop=new UniversalLevelData(dh.Data.Bytes.ToArray());BinaryPrimitives.WriteUInt32LittleEndian(badLoop.Bytes.AsSpan(sourceLayout.SoundOffset+0x10C),0x1800);witness=badLoop.Bytes.ToArray();Refuse(()=>UniversalSoundBankStorage.Compact(badLoop,new(badLoop.Bytes)),"external native loop-address");Check(badLoop.Bytes.AsSpan().SequenceEqual(witness),"unknown loop refusal immutable");
var badInternal=new UniversalLevelData(dh.Data.Bytes.ToArray());var first=starts[0];badInternal.Bytes[sourceLayout.BankOffset+first.BankOffset+first.ByteLength-15]=3;witness=badInternal.Bytes.ToArray();Refuse(()=>UniversalSoundBankStorage.Compact(badInternal,new(badInternal.Bytes)),"internal ADPCM loop-start");Check(badInternal.Bytes.AsSpan().SequenceEqual(witness),"missing internal loop refusal immutable");
// Storage allocation can own native rounded DMA padding, never an extra usable sample.
var maximum=new UniversalLevelData(dh.Data.Bytes.ToArray());maximum.GrowSubfile(0,0x7F000-original.BankByteLength);var ml=new NativeLevelSoundLayout(maximum.Bytes);Check(ml.Sound.BankByteLength==0x7EFF0,"decode only usable SPU bytes");maximum.Bytes[ml.BankOffset+0x7EFF0]=1;Refuse(()=>{_=new NativeLevelSoundLayout(maximum.Bytes);},"zero DMA padding frame");maximum.Bytes[ml.BankOffset+0x7EFF0]=0;BinaryPrimitives.WriteInt32LittleEndian(maximum.Bytes.AsSpan(ml.SoundOffset+0x108),0x7EFF0);Refuse(()=>{_=new NativeLevelSoundLayout(maximum.Bytes);},"bounded aligned SPU-bank-relative");
foreach(var (key,row) in new[]{("magiccrafters",7),("alpineridge",5),("blowhard",11),("gnastygnorc",0)}) {
 var src=library.Get(key);var r=src.Data.Row(row);var group=UniversalPlacementGroups.Expand(library.Get,new UniversalMobyPlacement(key,row,BitConverter.ToInt32(r,12),BitConverter.ToInt32(r,16),BitConverter.ToInt32(r,20)));
 try{
 var result=UniversalMobyComposer.Compose(dh.Level,dh.Data.Bytes,dh.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],dh.Level).ToArray(),group,sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
 Check(key is "magiccrafters" or "alpineridge","only druid fits retained bank");var final=new UniversalLevelData(result.LevelData);var sound=new NativeLevelSoundLayout(final.Bytes).Sound;
 Check(sound.Map.SequenceEqual(original.Map),key+" all256 destination globals");Check(sound.Descriptors.Count==original.Descriptors.Count+10,"all80 original indices plus10");
 foreach(var desc in original.Descriptors){var next=sound.Descriptors[desc.Index];Check(next.CopyDescriptor().AsSpan(4).SequenceEqual(desc.CopyDescriptor().AsSpan(4)),"composed retained controls");Check(next.Sample.CopyBytes().AsSpan().SequenceEqual(desc.Sample.CopyBytes()),"composed retained ADPCM");}
 foreach(var item in result.Classes.Where(x=>x.ModelImported)){var donor=library.Get(item.SourceLevelKey);var donorPkg=donor.Data.Package(item.ClassId);var imported=final.Package(item.DestinationClassId);var ds=new NativeLevelSoundLayout(donor.Data.Bytes).Sound;for(int slot=0;slot<16;slot++){byte index=donorPkg[4+slot];if(index==255){Check(imported[4+slot]==255,"empty model slot");continue;}var wanted=ds.Descriptors[index];var got=sound.Descriptors[imported[4+slot]];Check(got.CopyDescriptor().AsSpan(4).SequenceEqual(wanted.CopyDescriptor().AsSpan(4)),"imported slot controls");Check(got.Sample.CopyBytes().AsSpan().SequenceEqual(wanted.Sample.CopyBytes()),"imported slot entire ADPCM");}}
 Check(result.Classes.Sum(x=>x.Sounds?.BankBytesReclaimed??0)==1168,"reported exact reclaimed bank bytes");
 Check(final.Subfiles()[0].Length-0x80000==0x7F000,"full rounded native DMA source owned");Check(sound.Descriptors.Max(x=>x.Sample.BankOffset+x.Sample.ByteLength)==519840,"exact full druid used bytes");
 results.Add(new{key,Status="composed-static-only",DescriptorCount=sound.Descriptors.Count,UsedSampleBytes=519840,StorageBytes=0x7F000,UsableBytes=0x7EFF0,result.Ram,result.Classes});
 }catch(InvalidDataException e){Check(key is "blowhard" or "gnastygnorc","only expected remaining capacity refusals: "+key+" "+e.Message);Check(e.Message.Contains("sound bank cannot fit"),"capacity refusal preserved");results.Add(new{key,Status="capacity-refused",e.Message});}
}
UniversalSourceLibrary.RequireOriginal(args[0]);File.WriteAllText(args[2],JsonSerializer.Serialize(new{Scope="Source/compiled composition only; no disc mutation, runtime or audio listening proof",Checks=checks,Results=results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("UNIVERSAL_SOUND_STORAGE_CHECKS_OK checks="+checks);
