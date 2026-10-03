using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;
int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
if(args.Length!=3)throw new Exception("USA BIN, catalog root, NEW candidate directory");
string dir=Path.GetFullPath(args[2]);if(Directory.Exists(dir))throw new Exception("Preserve existing candidate directory");Directory.CreateDirectory(dir);
var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var source=lib.Get("mistybog");var host=lib.Get("peacekeepers");
int[] delta={36000-57897,36000-107274,8352-4558};var original=source.Data.CaptureRow("mistybog",9);
var placement=new UniversalMobyPlacement("mistybog",9,36000,36000,8352,199,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:0);
var placements=UniversalPlacementGroups.Expand(lib.Get,placement).ToArray();var surfaces=new List<AuthoredSurface>();
for(int i=0;i<4;i++)for(int j=0;j<8;j++){if(i==0&&j==0)continue;float x=2000+i*240,y=400+j*240;surfaces.Add(new(Guid.NewGuid().ToString("N"),[new(x,y,522),new(x+240,y,522),new(x+240,y+240,522),new(x,y+240,522)],new ColorRgba(128,112,80),new AuthoredMaterial(10,66)));}
var document=SceneDocument.New("MOVED MISTY BOAR") with{Spawn=new Vector3f(2250,1937.5f,554),Heading=0,Surfaces=surfaces,TargetBinding=new("peacekeepers",20,host.Level.SourceWadEntry,host.Level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,host.Data.Scene().RowCount)};
NativeAuthoredSpawn.RequirePortalArrivalSupported(document);
var pathPoints=original.InternalPointers.Where(p=>p.Offset is 0x18 or 0x1C).SelectMany(path=>Enumerable.Range(0,original.Properties[path.Target]).Select(n=>new[]{BitConverter.ToInt32(original.Properties,path.Target+8+16*n)+delta[0],BitConverter.ToInt32(original.Properties,path.Target+12+16*n)+delta[1],BitConverter.ToInt32(original.Properties,path.Target+16+16*n)+delta[2]})).ToArray();
Check(surfaces.Count==31,"31 bounded textured quads");
Check(pathPoints.All(p=>p[0]>=32000+1024&&p[0]<=47360-1024&&p[1]>=6400+1024&&p[1]<=37120-1024),"all translated path XY plus1024 envelope edge clearance covered; source path Z retained");
int[] pathMin=Enumerable.Range(0,3).Select(a=>pathPoints.Min(p=>p[a])).ToArray(),pathMax=Enumerable.Range(0,3).Select(a=>pathPoints.Max(p=>p[a])).ToArray();

File.WriteAllText(Path.Combine(dir,"scene.json"),JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true}));
var cleared=await NativeClearedLevelCompilation.CaptureAsync(args[0],document,catalog,Path.Combine(dir,"baseline.bin"));var compose=cleared.Compose(lib,placements);var baseline=UniversalDiscImage.Read(Path.Combine(dir,"baseline.bin"));var exe=compose.Executable.ToArray();NativeClearedRetailBinding.ApplyNewGameStart(lib.Disc.Exe,exe,20);
string bin=Path.Combine(dir,"candidate.bin");baseline.WriteImage(bin,baseline.RebuildWad(new Dictionary<int,byte[]>{{host.Level.SourceWadEntry,compose.LevelData},{host.Level.SourceWadEntry-1,compose.Overlay}}),exe);
var read=UniversalDiscImage.Read(bin);
Check(read.Exe.SequenceEqual(exe),"EXE readback");Check(read.EntryBytes(host.Level.SourceWadEntry).SequenceEqual(compose.LevelData),"data readback");Check(read.EntryBytes(host.Level.SourceWadEntry-1).SequenceEqual(compose.Overlay),"overlay readback");Check(compose.UnresolvedObjectLinks==0&&placements.Length==1,"one scoped boar");
var data=new UniversalLevelData(compose.LevelData);int row=compose.NewRowIndices[0];var r=data.Row(row);int props=BitConverter.ToInt32(r,0),so=data.Subfiles()[3].Offset;Check(BitConverter.ToInt32(r,12)==36000&&BitConverter.ToInt32(r,16)==36000&&BitConverter.ToInt32(r,20)==8352,"actor moved XYZ");Check(r[0x43]==255&&r[0x4A]==255,"native unrestricted activation/visibility");
for(int a=0;a<3;a++)Check(BitConverter.ToInt32(data.Bytes,so+props+4+4*a)==BitConverter.ToInt32(original.Properties,4+4*a)+delta[a],"saved XYZ moved");
foreach(var path in original.InternalPointers.Where(p=>p.Offset is 0x18 or 0x1C)){int actual=BitConverter.ToInt32(data.Bytes,so+props+path.Offset);for(int n=0;n<original.Properties[path.Target];n++)for(int a=0;a<3;a++)Check(BitConverter.ToInt32(data.Bytes,so+actual+8+16*n+4*a)==BitConverter.ToInt32(original.Properties,path.Target+8+16*n+4*a)+delta[a],"complete path readback");}
int state=BitConverter.ToInt32(data.Bytes,so+props+0x40);Check(BitConverter.ToInt32(data.Bytes,so+state)==0&&data.Scene().Fixups.Contains(props+0x40),"owned zero charge state");
File.WriteAllLines(Path.ChangeExtension(bin,".cue"),["FILE \"candidate.bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexStringLower(SHA256.HashData(f));}
File.WriteAllText(Path.Combine(dir,"candidate.receipt.json"),JsonSerializer.Serialize(new{Scope="NativeClearedLevelCompilation + ordinary composer, moved Misty9, original native flags and source behavior; gameplay pending",Checks=checks,SourceSha256=Hash(args[0]),CandidateSha256=Hash(bin),CoreSha256=Hash(typeof(UniversalMobyComposer).Assembly.Location),placements,compose.NewRowIndices,compose.PlacementSaveRows,compose.Classes,compose.Behavior,compose.Ram,compose.ParticleDependencies,SpawnRaw=new[]{36000,31000,8864},NewGameStartLevelId=20,RawFloorZ=8352,ChargeStateSceneOffset=state,PathMin=pathMin,PathMax=pathMax,FloorRawMin=new[]{32000,6400},FloorRawMax=new[]{47360,37120},NativeTriangles=62,RemovedCorner="southwest tile raw32000..35840 X and6400..10240 Y",Texture="31 authored collision tiles,62 native triangles; material10/66; rawX32000..47360,Y6400..37120; southwest corner omitted",Runtime="pending",ClaimsExcluded=new[]{"Gameplay","All boar variants runtime","Chicken reciprocal runtime","Sound listening","Collection stars semantics","Card reload"}},new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(dir,"RUNTIME_CHECKS.md"),"Cold BIOS with blank test card, New Game level20. Start raw(36000,31000,8864), boar physical0 at(36000,36000,8352), class reported in receipt, native yaw199. Textured flat collision floor8352 covers X32000..47360,Y6400..37120 with southwest corner omitted. Walk toward boar; verify body/animation, charge movement, contact hurt, flame/charge defeat, native green2 gem release/pickup and both counters, parent retirement/checkpoint/durable flags. Check saved home XYZ and charge state through read-only RAM if chase returns. Card reload, all9 runtime, chicken pair, sound listening and particle12 collection-star ownership remain outside this fixture. No emulator launch by builder.\n");Console.WriteLine($"BOAR_FLAT_CANDIDATE_READBACK_OK checks={checks} sha={Hash(bin)}");
