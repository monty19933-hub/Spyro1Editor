using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;

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
if(args.Length==2&&args[0]=="--emit")
{
 var remapped=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemUpdate(classes,"high_update",true)],basis).Code;
 var mark=Spyro.Editor.Core.Exporting.NativeMobySaveMap.Windows().Single(w=>w.Address==0x8003B854);
 File.WriteAllText(args[1],JsonSerializer.Serialize(new{Base=basis,Update=Convert.ToHexString(update),RemappedUpdate=Convert.ToHexString(remapped),Draw=Convert.ToHexString(draw),Classes=classes,PopupFrames=UniversalHighValueGems.PopupFrames,RemappedMark=Convert.ToHexString(mark.Code)}));
 Console.WriteLine($"HIGH_VALUE_GEM_EMIT_OK checks={checks}");return;
}
if(args.Length==4&&args[0]=="--emit-particles")
{ParticleSourceCases.Run(args[1],args[2],args[3],Check);Console.WriteLine($"HIGH_VALUE_GEM_PARTICLE_SOURCE_OK checks={checks}");return;}
if(args.Length==0){Console.WriteLine($"HIGH_VALUE_GEM_FREE_OK checks={checks}");return;}
if(args.Length!=2)throw new ArgumentException("USA.bin catalog-workspace");
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var assets=UniversalHighValueGems.Assets(lib);
Check(ReferenceEquals(assets,UniversalHighValueGems.Assets(lib)),"certified asset cache reused");
Check(assets.Count==2&&assets.All(a=>a.SourceKey=="editor"),"USA assets need no Japanese disc");
Check(assets.All(a=>a.Behavior==UniversalSpecialBehavior.HighValueGem&&a.Package.Length==272),"separate native simple gem models");
var original=lib.SharedModelPackage(0x57);
Check(Convert.ToHexStringLower(SHA256.HashData(original))==UniversalHighValueGems.PurplePackageSha256,"purple source model authenticated");
foreach(var asset in assets)
{
 Check(!asset.Package.SequenceEqual(original),"new native visible colour commands");
 Check(asset.RowTemplate[0x4F]==0&&asset.RowTemplate[0x50]==0x18&&asset.RowTemplate[0x53]==0xFF,"native loose gem kind and no retail reward selector");
 Check(BinaryPrimitives.ReadUInt16LittleEndian(asset.RowTemplate.AsSpan(0x36))==asset.ClassId,"separate runtime class");
 for(int p=0;p<272;p++)if(p<60||p>=264)Check(asset.Package[p]==original[p],"every geometry/header/read-ahead byte preserved");
 for(int face=0;face<18;face++)
 {
  int p=120+8*face;uint prior=BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(p));uint actual=BinaryPrimitives.ReadUInt32LittleEndian(asset.Package.AsSpan(p));
  Check(actual==(prior&~3u)&&BinaryPrimitives.ReadUInt32LittleEndian(asset.Package.AsSpan(p+4))==actual,"native RGB faces preserve geometry and own colour indices");
 }
 for(int p=60;p<120;p+=4)Check(asset.Package[p+3]==original[p+3],"colour command byte preserved");
 Check(UniversalSharedModelAssets.CountDerivedSimplePackage(asset.Package).SequenceEqual(asset.Package),"native simple colour-index bounds readback");
}
Check(lib.SharedModelPackage(0x57).SequenceEqual(original),"retail purple shared model unchanged");
UniversalHighValueGems.RequireNativeOwner(lib.Disc.Exe);
Check(true,"certified native collection/text owners");
foreach(var (address,n) in new[]{(0x80053570u,0x98),(0x80053608u,0x9C),(0x80052568u,0x64),(0x800181ACu,0x2A0),(0x80018880u,0x80),(0x8003B854u,0x180)})
 for(int i=0;i<n;i++){var altered=lib.Disc.Exe.ToArray();altered[(int)(address-UniversalExeBindings.ExeBase)+i]^=1;Check(Refuses(()=>UniversalHighValueGems.RequireNativeOwner(altered)),"every native owner byte guarded");}
var mapped=lib.Disc.Exe.ToArray();Spyro.Editor.Core.Exporting.NativeMobySaveMap.PatchExe(mapped);UniversalHighValueGems.RequireNativeOwner(mapped);Check(true,"canonical native logical-save mapping allowed");
Console.WriteLine($"HIGH_VALUE_GEM_SOURCE_OK checks={checks}");
