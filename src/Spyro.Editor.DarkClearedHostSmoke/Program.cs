using System.Buffers.Binary;
using Spyro.Editor.Core.Primitives;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Editing;
int checks=0;void Check(bool b,string why){if(!b)throw new Exception(why);checks++;}
string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
if(args.Length!=3)throw new Exception("USA BIN, catalog root, NEW proof directory");
var dir=Path.GetFullPath(args[2]);if(Directory.Exists(dir))throw new Exception("Preserve existing proof directory");Directory.CreateDirectory(dir);
var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var source=lib.Get("darkpassage");
var placements=UniversalPlacementGroups.Expand(lib.Get,new UniversalMobyPlacement("darkpassage",26,36000,36000,8352,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:0));
var surfaces=new List<AuthoredSurface>();for(int i=0;i<6;i++)for(int j=0;j<6;j++){float x=1563+i*240,y=1563+j*240;surfaces.Add(new(Guid.NewGuid().ToString("N"),[new(x,y,522),new(x+240,y,522),new(x+240,y+240,522),new(x,y+240,522)],new ColorRgba(128,112,80),new AuthoredMaterial(10,66)));}
List<object> results=[];
foreach(var profile in NativeClearedRetailBinding.Census(lib.Disc.Exe).Where(p=>p.NineSettersSupported)){
 var target=catalog.Levels.Single(l=>l.LevelId==profile.LevelId);var original=lib.Get(target.Key);var doc=SceneDocument.New("DARK HOST PROOF") with{Spawn=new(2250,1937.5f,554),Heading=0,Surfaces=surfaces,TargetBinding=new(target.Key,target.LevelId,target.SourceWadEntry,target.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,original.Data.Scene().RowCount)};
 var compiled=await NativeClearedLevelCompilation.CaptureAsync(args[0],doc,catalog,Path.Combine(dir,target.Key+"-baseline.bin"));
 var initialExe=compiled.CopyExecutable();var initialOverlay=new UniversalOverlay(compiled.CopyOverlay(),UniversalExeBindings.Read(initialExe,target.LevelId));var certificate=UniversalDarkPassageProjectileParticles.Capture("darkpassage",target.Key,source.Data,source.Overlay,initialOverlay,initialExe);Check(certificate.CopiesImpact,"cleared Town needs scoped13");
 var composed=compiled.Compose(lib,placements);var data=new UniversalLevelData(composed.LevelData);Check(composed.UnresolvedObjectLinks==0&&composed.NewRowIndices.Count==2,"two linked rows");
 UniversalDarkPassageProjectileParticles.RequireTextures(certificate,source.Data,data);checks++;
 Check(composed.ParticleDependencies.Count(p=>p.SourceLevelKey=="darkpassage"&&p.TypeId==1&&p.Status=="source-certified-native-equivalent")==1,"native trail");Check(composed.ParticleDependencies.Count(p=>p.SourceLevelKey=="darkpassage"&&p.TypeId==13&&p.Status=="source-certified-copied")==1,"scoped impact");
 var bindings=UniversalExeBindings.Read(composed.Executable,target.LevelId);Check(bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==initialOverlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value,"retained native constructor");Check(bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=initialOverlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,"one chained updater");
 var root=data.Row(composed.NewRowIndices[0]);var lamp=data.Row(composed.NewRowIndices[1]);int props=BinaryPrimitives.ReadInt32LittleEndian(root);int lampProps=BinaryPrimitives.ReadInt32LittleEndian(lamp);int start=data.Subfiles()[3].Offset;
 Check(BinaryPrimitives.ReadInt32LittleEndian(data.Bytes.AsSpan(start+props+12))==composed.NewRowIndices[1],"translated physical lantern index");Check(BinaryPrimitives.ReadInt32LittleEndian(data.Bytes.AsSpan(start+lampProps+24))==-1,"native imported negative lighting context");
 results.Add(new{target.Key,target.LevelId,certificate.DestinationParticleProfile,composed.NewRowIndices,composed.Ram,composed.ParticleDependencies,DataSha256=Hash(composed.LevelData),OverlaySha256=Hash(composed.Overlay),ExecutableSha256=Hash(composed.Executable),Runtime=false});Console.WriteLine(target.Key+" cleared composed");}
File.WriteAllText(Path.Combine(dir,"receipt.json"),JsonSerializer.Serialize(new{Checks=checks,Marker="DARK_PROJECTILE_CLEARED_HOST_COMPOSE_OK",results,Runtime=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("DARK_PROJECTILE_CLEARED_HOST_COMPOSE_OK checks="+checks);
