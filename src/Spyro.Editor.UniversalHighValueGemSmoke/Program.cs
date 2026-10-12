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
Check(Refuses(()=>UniversalSpecialBehaviors.HighValueGemUpdate([classes[0],classes[0]],"bad")),"duplicate class refused");
const uint basis=0x80090000;
var (update,ul)=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemUpdate(classes,"high_update")],basis);
Check(update.Length>0,"bounded native update assembled");
Check(classes.All(c=>!UniversalSpecialCatalog.NeedsRenderHook(UniversalHighValueGems.IdForClass(c))),"native pickup text needs no editor render hook");
Check(update.Length <= 928,$"wobble, shine colour and idle sparkle native code stays within its 928-byte resident allowance ({update.Length})");
// Shine/wobble contract shared with the scalar proof: entry 0 teal, rainbow hue order, retail wobble tables.
Check(UniversalHighValueGems.ShineTable==0x8006E44C&&UniversalHighValueGems.SineTable==0x8006CBF8&&UniversalHighValueGems.CosineTable==0x8006CC78&&UniversalHighValueGems.FrameStep==0x800756CC,"retail renderer table and wobble owners");
Check(Enumerable.Range(0,256).Select(UniversalHighValueGems.RainbowEntry).Distinct().Order().SequenceEqual(new[]{0,1,2,3,4,5}),"rainbow visits all six shine entries");
Check(UniversalHighValueGems.ShinePair(UniversalHighValueGems.TealEntry)==(UniversalHighValueGems.TealShineBase,UniversalHighValueGems.TealShineHighlight),"teal owns entry 0");
// Source-free shine grammar: a synthetic four-face simple model with native shine faces (flags 3 + normal).
{
 byte[] model=new byte[0x48];model[0]=4;model[1]=4;model[2]=0x12;model[3]=0xFF;
 BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(4),0x10);BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(8),0x1C);BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(12),0x20);
 sbyte[] corners=[0,0,40, 30,0,-20, -30,25,-20, -30,-25,-20];for(int i=0;i<12;i++)model[0x10+i]=unchecked((byte)corners[i]);
 int[][] tris=[[0,1,2],[0,2,3],[0,3,1],[1,3,2]];sbyte[][] normals=[[80,90,40],[-100,0,40],[60,-90,40],[0,0,-127]];
 for(int f=0;f<4;f++)
 {
  uint word=(uint)(tris[f][0]<<23|tris[f][1]<<16|tris[f][2]<<9|tris[f][2]<<2)|3;
  uint normal=(uint)((byte)normals[f][0]<<24|(byte)normals[f][1]<<16|(byte)normals[f][2]<<8);
  BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x20+8*f),word);BinaryPrimitives.WriteUInt32LittleEndian(model.AsSpan(0x24+8*f),normal);
 }
 var (sv,sf)=UniversalHighValueGems.ShineFaces(model);
 Check(sv.Count==4&&sf.Count==4&&sf.All(f=>f.Vertices.Length==3)&&sf[3].NormalZ==-127&&sf[0].NormalX==80&&sf[0].NormalY==90,"shine normals read in vertex-byte order (renderer IR3, IR1, IR2)");
 var shiny=UniversalHighValueGems.ShinyModel(model);
 Check(Enumerable.Range(0x10,12).All(p=>unchecked((sbyte)shiny[p])==2*unchecked((sbyte)model[p]))&&shiny.AsSpan(0x1C).SequenceEqual(model.AsSpan(0x1C))&&shiny.AsSpan(0,0x10).SequenceEqual(model.AsSpan(0,0x10)),"shiny copy doubles vertices only");
 foreach(int cls in classes)
 {
  var preview=NativeSimpleGouraudModelGeometry.Decode(UniversalHighValueGems.PreviewPackage(cls,shiny));
  Check(preview.Faces.Count==4&&preview.PackedRgbWords.Count==4&&preview.Faces.All(f=>f.ColorIndices.Distinct().Count()==1),"preview: one flat colour per face");
  if(cls==UniversalHighValueGems.TealClass)Check(preview.PackedRgbWords.All(c=>(c&255)<=(c>>8&255)&&(c&255)<=(c>>16&255)),"teal preview faces carry no red tint");
  else Check(preview.PackedRgbWords.Distinct().Count()==4,"rainbow preview faces differ");
 }
 var flatModel=model.ToArray();flatModel[0x20]&=0xFC;
 Check(Refuses(()=>UniversalHighValueGems.ShineFaces(flatModel))&&Refuses(()=>UniversalHighValueGems.ShinyModel(flatModel)),"a non-shine face is refused");
 var big=model.ToArray();big[0x10]=70;
 Check(Refuses(()=>UniversalHighValueGems.ShinyModel(big)),"doubling past a signed byte is refused");
}
if(args.Length==2&&args[0]=="--emit")
{
 var remapped=UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueGemUpdate(classes,"high_update",true)],basis).Code;
 var mark=Spyro.Editor.Core.Exporting.NativeMobySaveMap.Windows().Single(w=>w.Address==0x8003B854);
 File.WriteAllText(args[1],JsonSerializer.Serialize(new{Base=basis,Update=Convert.ToHexString(update),RemappedUpdate=Convert.ToHexString(remapped),Classes=classes,IdleSparkleFrames=UniversalHighValueGems.IdleSparkleFrames,IdleSparkleLimit=UniversalHighValueGems.IdleSparkleLimit,IdleSparkleRadius=UniversalHighValueGems.IdleSparkleRadius,RemappedMark=Convert.ToHexString(mark.Code)}));
 Console.WriteLine($"HIGH_VALUE_GEM_EMIT_OK checks={checks}");return;
}
if(args.Length==4&&args[0]=="--emit-particles")
{ParticleSourceCases.Run(args[1],args[2],args[3],Check);Console.WriteLine($"HIGH_VALUE_GEM_PARTICLE_SOURCE_OK checks={checks}");return;}
if(args.Length==4&&args[0]=="--bin")
{
 // Emulator image through the Create BIN stage: in Artisans, an original red gem, a copied Jacques purple gem, a teal
 // gem and a rainbow gem in one row in front of Spyro's start. They float 650 units above him, out of his and Sparx's reach.
 string image=Path.GetFullPath(args[1]),bin=Path.GetFullPath(args[3]);var catalog=LevelCatalog.Load(Path.GetFullPath(args[2]));
 var sources=new UniversalSourceLibrary(image,catalog);
 Check(BinaryPrimitives.ReadUInt16LittleEndian(sources.Get("artisans").Data.Row(8).AsSpan(0x36))==0x53,"Artisans row 8 is an original red gem");
 Check(BinaryPrimitives.ReadUInt16LittleEndian(sources.Get("jacques").Data.Row(40).AsSpan(0x36))==0x57,"Jacques row 40 is an original purple gem");
 const int x=84992,y=47173+800,z=9556+650;
 UniversalMobyPlacement[] row=[new("artisans",8,x-600,y,z){EditorId="shine-red",PersistentSlot=0},new("jacques",40,x-200,y,z){EditorId="shine-purple",PersistentSlot=1},
  new(UniversalHighValueGems.SourceKey,-1,x+200,y,z,SpecialId:UniversalHighValueGems.TealId){EditorId="shine-teal",PersistentSlot=2},
  new(UniversalHighValueGems.SourceKey,-1,x+600,y,z,SpecialId:UniversalHighValueGems.RainbowId){EditorId="shine-rainbow",PersistentSlot=3}];
 var result=UniversalMobyExport.Apply(sources,image,bin,catalog,new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{["artisans"]=row});
 File.WriteAllText(Path.ChangeExtension(bin,".cue"),$"FILE \"{Path.GetFileName(bin)}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
 var rows=result.Levels.Single().Compose.NewRowIndices;
 Console.WriteLine($"HIGH_VALUE_GEM_BIN_OK {bin} sha256={result.OutputImageSha256} rows={string.Join(",",rows)}");return;
}
if(args.Length==0){Console.WriteLine($"HIGH_VALUE_GEM_FREE_OK checks={checks}");return;}
if(args.Length!=2)throw new ArgumentException("USA.bin catalog-workspace");
var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var assets=UniversalHighValueGems.Assets(lib);
Check(ReferenceEquals(assets,UniversalHighValueGems.Assets(lib)),"certified asset cache reused");
Check(assets.Count==2&&assets.All(a=>a.SourceKey=="editor"),"USA assets need no Japanese disc");
var original=lib.SharedModelPackage(0x57);
Check(Convert.ToHexStringLower(SHA256.HashData(original))==UniversalHighValueGems.PurplePackageSha256,"purple source model authenticated");
Check(assets.All(a=>a.Behavior==UniversalSpecialBehavior.HighValueGem&&a.Package.Length==original.Length&&a.Package.Length==272),"separate native simple gem models, the purple model's own length");
int vertexEnd=16+14*3;
foreach(var asset in assets)
{
 Check(asset.RowTemplate[0x4F]==(asset.ClassId==UniversalHighValueGems.TealClass?UniversalHighValueGems.TealEntry:UniversalHighValueGems.RainbowFirstEntry),"row selects its own shine-table entry (teal 0, rainbow starting red)");
 Check((asset.RowTemplate[0x4E]&0xE0)==0&&BinaryPrimitives.ReadInt32LittleEndian(asset.RowTemplate.AsSpan(0x4C))>>21==8*asset.RowTemplate[0x4F],"renderer table offset is exactly 8 x +0x4F");
 Check(asset.RowTemplate[0x50]==0x18&&asset.RowTemplate[0x53]==0xFF,"native loose gem kind and no retail reward selector");
 Check(BinaryPrimitives.ReadUInt16LittleEndian(asset.RowTemplate.AsSpan(0x36))==asset.ClassId,"separate runtime class");
 for(int p=0;p<16;p++)Check(asset.Package[p]==original[p],"counts and pointer bytes preserved");
 for(int p=16;p<vertexEnd;p++)Check(unchecked((sbyte)asset.Package[p])==2*unchecked((sbyte)original[p]),"every native signed vertex coordinate doubled exactly");
 for(int p=vertexEnd;p<original.Length;p++)Check(asset.Package[p]==original[p],"retail shine faces, normals, RGB table and read-ahead kept byte for byte");
 var (vertices,faces)=UniversalHighValueGems.ShineFaces(asset.Package);
 Check(vertices.Count==14&&faces.Count==18,"fourteen vertices and eighteen faces");
 int faceStart=BinaryPrimitives.ReadInt32LittleEndian(asset.Package.AsSpan(12));
 for(int f=0;f<18;f++)Check((BinaryPrimitives.ReadUInt32LittleEndian(asset.Package.AsSpan(faceStart+8*f))&3)==3,"every face is a native shine face (flags 3)");
 Check(faces.All(f=>Math.Abs(Math.Sqrt(f.NormalX*f.NormalX+f.NormalY*f.NormalY+f.NormalZ*f.NormalZ)-128)<2),"each face keeps its unit (128) native normal");
 Check(Refuses(()=>NativeSimpleGouraudModelGeometry.Decode(asset.Package)),"shine faces are not indexed-RGB faces");
 Check(UniversalSharedModelAssets.CountDerivedSimplePackage(asset.Package).SequenceEqual(asset.Package),"native simple-model bounds readback");
 var previewPackage=UniversalHighValueGems.PreviewPackage(asset.ClassId,asset.Package);
 var preview=NativeSimpleGouraudModelGeometry.Decode(previewPackage);
 Check(preview.Vertices.Count==14&&preview.Faces.Count==18&&preview.PackedRgbWords.Count==18,"editor preview: one flat colour per face over the same geometry");
 Check(preview.Vertices.Select(v=>(v.X,v.Y,v.Z)).SequenceEqual(vertices),"preview vertices are the exported vertices");
 if(asset.ClassId==UniversalHighValueGems.TealClass)
  Check(preview.PackedRgbWords.All(c=>(c&255)<=(c>>8&255)&&(c&255)<=(c>>16&255)&&(c>>8&255)>=0x48),"teal preview faces are teal");
 else Check(preview.PackedRgbWords.Select(c=>c).Distinct().Count()>=6,"rainbow preview faces walk its hues");
}
Check(assets[0].Package.SequenceEqual(assets[1].Package),"teal and rainbow share geometry; colour is the row's");
for(int phase=0;phase<256;phase++)
 Check(UniversalHighValueGems.RainbowEntry(phase)==new[]{1,4,2,0,3,5}[phase*6>>8],"rainbow walks red, gold, green, teal, blue, purple");
// The renderer's table entries 1-5 are the retail gem pairs; entry 0 is free for teal.
var exe=lib.Disc.Exe;
for(int entry=0;entry<6;entry++)
{
 var (lo,hi)=UniversalHighValueGems.ShinePair(entry);
 int at=(int)(UniversalHighValueGems.ShineTable+8u*(uint)entry-UniversalExeBindings.ExeBase);
 uint nativeLo=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at)),nativeHi=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at+4));
 Check(entry==0?nativeLo==0&&nativeHi==0:(nativeLo,nativeHi)==(lo,hi),"native shine table entry "+entry);
}
var (tealLo,tealHi)=UniversalHighValueGems.ShinePair(0);
Check((tealLo&0x010101)==0&&(tealLo&0xFF)==0&&(tealHi&0xFF)==0&&(tealLo>>8&255)>(tealLo&255)&&(tealHi>>8&255)==255,"teal pair: no red, even base channels (the back-face halving keeps them apart), bright green-blue highlight");
foreach(byte overflow in new byte[]{64,191})
{
 var oversized=original.ToArray();oversized[16]=overflow;
 Check(Refuses(()=>UniversalHighValueGems.ShinyModel(oversized)),"positive and negative native coordinate overflow rejected");
 Check(oversized[16]==overflow,"rejected input model not mutated");
}
var flat=assets[0].Package.ToArray();flat[BinaryPrimitives.ReadInt32LittleEndian(flat.AsSpan(12))]&=0xFC;
Check(Refuses(()=>UniversalHighValueGems.ShineFaces(flat)),"a non-shine face is refused");
Check(lib.SharedModelPackage(0x57).SequenceEqual(original),"retail purple shared model unchanged");
UniversalHighValueGems.RequireNativeOwner(lib.Disc.Exe);
Check(true,"certified native collection/numeral popup, shine renderer, shine table and wobble table owners");
foreach(var (address,n) in new[]{(0x80053570u,0x98),(0x80053608u,0x9C),(0x80052568u,0x64),(0x800557CCu,0x190),(0x8003B854u,0x180),(0x80023268u,0x6F0),(UniversalHighValueGems.ShineTable,48),(UniversalHighValueGems.SineTable,640)})
 for(int i=0;i<n;i++){var altered=lib.Disc.Exe.ToArray();altered[(int)(address-UniversalExeBindings.ExeBase)+i]^=1;Check(Refuses(()=>UniversalHighValueGems.RequireNativeOwner(altered)),"every native owner byte guarded");}
var mapped=lib.Disc.Exe.ToArray();Spyro.Editor.Core.Exporting.NativeMobySaveMap.PatchExe(mapped);UniversalHighValueGems.RequireNativeOwner(mapped);Check(true,"canonical native logical-save mapping allowed");
Console.WriteLine($"HIGH_VALUE_GEM_SOURCE_OK checks={checks}");
