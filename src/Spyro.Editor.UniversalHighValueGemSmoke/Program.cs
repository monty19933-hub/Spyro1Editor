using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Analysis;

int checks=0;
void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks++;}
bool Refuses(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
int[] classes=[UniversalHighValueGems.TealClass,UniversalHighValueGems.RainbowClass];
Check(UniversalHighValueGems.ValueForClass(classes[0])==50&&UniversalHighValueGems.ValueForClass(classes[1])==100,"two authored native awards");
for(int c=0;c<512;c++)if(!classes.Contains(c))Check(UniversalHighValueGems.ValueForClass(c)==0,"no retail reward selector changed");
Check(Refuses(()=>UniversalSpecialBehaviors.HighValueGemUpdate([0x57],"bad")),"retail native class refused");
Check(Refuses(()=>UniversalSpecialBehaviors.HighValueGemDraw([classes[0],classes[0]],"bad")),"duplicate class refused");
const uint basis=0x80090000;
var (update,ul)=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemUpdate(classes,"high_update")],basis);
var (draw,dl)=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemDraw(classes,"high_draw")],basis);
Check(update.Length>0&&draw.Length>0,"bounded native routines assembled");
Check(update.Length <= 792,"idle sparkle native code stays within its 192-byte resident growth allowance");
if(args.Length==2&&args[0]=="--emit")
{
 var remapped=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemUpdate(classes,"high_update",true)],basis).Code;
 var mark=Spyro.Editor.Core.Exporting.NativeMobySaveMap.Windows().Single(w=>w.Address==0x8003B854);
 File.WriteAllText(args[1],JsonSerializer.Serialize(new{Base=basis,Update=Convert.ToHexString(update),RemappedUpdate=Convert.ToHexString(remapped),Draw=Convert.ToHexString(draw),Classes=classes,PopupFrames=UniversalHighValueGems.PopupFrames,IdleSparkleFrames=UniversalHighValueGems.IdleSparkleFrames,IdleSparkleLimit=UniversalHighValueGems.IdleSparkleLimit,IdleSparkleRadius=UniversalHighValueGems.IdleSparkleRadius,RemappedMark=Convert.ToHexString(mark.Code)}));
 Console.WriteLine($"HIGH_VALUE_GEM_EMIT_OK checks={checks}");return;
}
if(args.Length==4&&args[0]=="--emit-particles")
{ParticleSourceCases.Run(args[1],args[2],args[3],Check);Console.WriteLine($"HIGH_VALUE_GEM_PARTICLE_SOURCE_OK checks={checks}");return;}
if(args.Length==0){Console.WriteLine($"HIGH_VALUE_GEM_FREE_OK checks={checks}");return;}
if(args.Length!=2)throw new ArgumentException("USA.bin catalog-workspace");
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var assets=UniversalHighValueGems.Assets(lib);
Check(ReferenceEquals(assets,UniversalHighValueGems.Assets(lib)),"certified asset cache reused");
Check(assets.Count==2&&assets.All(a=>a.SourceKey=="editor"),"USA assets need no Japanese disc");
Check(assets.All(a=>a.Behavior==UniversalSpecialBehavior.HighValueGem&&a.Package.Length==268),"separate native simple gem models with a closed fourteen-word RGB owner");
var original=lib.SharedModelPackage(0x57);
Check(Convert.ToHexStringLower(SHA256.HashData(original))==UniversalHighValueGems.PurplePackageSha256,"purple source model authenticated");
foreach(var asset in assets)
{
 Check(!asset.Package.SequenceEqual(original),"new native visible colour commands");
 Check(asset.RowTemplate[0x4F]==0&&asset.RowTemplate[0x50]==0x18&&asset.RowTemplate[0x53]==0xFF,"native loose gem kind and no retail reward selector");
 Check(BinaryPrimitives.ReadUInt16LittleEndian(asset.RowTemplate.AsSpan(0x36))==asset.ClassId,"separate runtime class");
 for(int p=0;p<12;p++)Check(asset.Package[p]==original[p],"vertex/RGB counts and pointer bytes preserved");
 Check(BinaryPrimitives.ReadInt32LittleEndian(asset.Package.AsSpan(12))==116,"face pointer follows closed RGB interval");
 for(int p=16;p<58;p++)Check(unchecked((sbyte)asset.Package[p])==2*unchecked((sbyte)original[p]),"every native signed vertex coordinate doubled exactly");
 for(int p=58;p<60;p++)Check(asset.Package[p]==original[p],"vertex alignment preserved");
 for(int p=260;p<268;p++)Check(asset.Package[p]==original[p+4],"face-end read-ahead preserved exactly after relocation");
 for(int face=0;face<18;face++)
 {
  int p=116+8*face;uint prior=BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(p+4));uint actual=BinaryPrimitives.ReadUInt32LittleEndian(asset.Package.AsSpan(p));
  Check(actual==(prior&~3u)&&BinaryPrimitives.ReadUInt32LittleEndian(asset.Package.AsSpan(p+4))==actual,"native RGB faces preserve geometry and own colour indices");
 }
 for(int p=60;p<116;p+=4)
 {
  Check(asset.Package[p+3]==original[p+3],"colour command byte preserved");
  Check(Math.Max(asset.Package[p],Math.Max(asset.Package[p+1],asset.Package[p+2]))>=224,"bright native palette never attenuated by donor gray");
  if(asset.ClassId==UniversalHighValueGems.TealClass)Check(asset.Package.AsSpan(p,3).SequenceEqual(new byte[]{48,255,232}),"consistent vivid teal RGB");
 }
 Check(UniversalSharedModelAssets.CountDerivedSimplePackage(asset.Package).SequenceEqual(asset.Package),"native simple colour-index bounds readback");
 var preview=NativeSimpleGouraudModelGeometry.Decode(asset.Package);
 Check(preview.Vertices.Count==14&&preview.Faces.Count==18&&preview.PackedRgbWords.Count==14&&preview.RequiredSourceWindowByteLength==268,"selected native model preview closes all owners");
}
foreach(byte overflow in new byte[]{64,191})
{
 var oversized=original.ToArray();oversized[16]=overflow;
 Check(Refuses(()=>UniversalHighValueGems.Recolour(oversized,classes[0])),"positive and negative native coordinate overflow rejected");
 Check(oversized[16]==overflow,"rejected input model not mutated");
}
Check(lib.SharedModelPackage(0x57).SequenceEqual(original),"retail purple shared model unchanged");
UniversalHighValueGems.RequireNativeOwner(lib.Disc.Exe);
Check(true,"certified native collection/text owners");
foreach(var (address,n) in new[]{(0x80053570u,0x98),(0x80053608u,0x9C),(0x80052568u,0x64),(0x800181ACu,0x2A0),(0x80018880u,0x80),(0x8003B854u,0x180)})
 for(int i=0;i<n;i++){var altered=lib.Disc.Exe.ToArray();altered[(int)(address-UniversalExeBindings.ExeBase)+i]^=1;Check(Refuses(()=>UniversalHighValueGems.RequireNativeOwner(altered)),"every native owner byte guarded");}
var mapped=lib.Disc.Exe.ToArray();Spyro.Editor.Core.Exporting.NativeMobySaveMap.PatchExe(mapped);UniversalHighValueGems.RequireNativeOwner(mapped);Check(true,"canonical native logical-save mapping allowed");
Console.WriteLine($"HIGH_VALUE_GEM_SOURCE_OK checks={checks}");
