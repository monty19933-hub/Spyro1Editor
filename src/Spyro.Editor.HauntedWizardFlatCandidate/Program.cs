using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;
if(args.Length!=3)throw new Exception("USA BIN, catalog root, NEW candidate directory");
string dir=Path.GetFullPath(args[2]);if(Directory.Exists(dir))throw new Exception("Preserve existing candidate directory");Directory.CreateDirectory(dir);
var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var source=lib.Get("hauntedtowers");var peace=lib.Get("peacekeepers");
var placement=new UniversalMobyPlacement("hauntedtowers",52,32000,32000,12653,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:0);
var placements=UniversalPlacementGroups.Expand(lib.Get,placement).Select((p,i)=>p with{PersistentSlot=i}).ToArray();
var surfaces=new List<AuthoredSurface>();
float Floor(float x,float y){float low=522f+96*Math.Clamp((y-2100)/128,0,1);return low+(762f-low)*Math.Clamp((x-1600)/250,0,1);}
float[] xs=[1000,1240,1480,1600,1850,2090,2330,2450],ys=[1400,1640,1880,2100,2228,2468,2708,2948,3188,3320];
for(int i=0;i<xs.Length-1;i++)for(int j=0;j<ys.Length-1;j++){float x=xs[i],xx=xs[i+1],y=ys[j],yy=ys[j+1];surfaces.Add(new(Guid.NewGuid().ToString("N"),[new(x,y,Floor(x,y)),new(xx,y,Floor(xx,y)),new(xx,yy,Floor(xx,yy)),new(x,yy,Floor(x,yy))],new ColorRgba(128,112,80),new AuthoredMaterial(10,66)));}
var document=SceneDocument.New("Haunted Armor Test") with{Spawn=new Vector3f(2000,1812,794),Heading=64,Surfaces=surfaces,TargetBinding=new("peacekeepers",20,peace.Level.SourceWadEntry,peace.Level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,peace.Data.Scene().RowCount)};
File.WriteAllText(Path.Combine(dir,"scene.json"),JsonSerializer.Serialize(document,new JsonSerializerOptions{WriteIndented=true}));
var cleared=await NativeClearedLevelCompilation.CaptureAsync(args[0],document,catalog,Path.Combine(dir,"baseline.bin"));
var compose=cleared.Compose(lib,placements);var baseline=UniversalDiscImage.Read(Path.Combine(dir,"baseline.bin"));var exe=compose.Executable.ToArray();NativeClearedRetailBinding.ApplyNewGameStart(lib.Disc.Exe,exe,20);
string bin=Path.Combine(dir,"candidate.bin");baseline.WriteImage(bin,baseline.RebuildWad(new Dictionary<int,byte[]>{{peace.Level.SourceWadEntry,compose.LevelData},{peace.Level.SourceWadEntry-1,compose.Overlay}}),exe);
var read=UniversalDiscImage.Read(bin);int checks=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
Check(read.Exe.SequenceEqual(exe),"EXE readback");Check(read.EntryBytes(peace.Level.SourceWadEntry).SequenceEqual(compose.LevelData),"data readback");Check(read.EntryBytes(peace.Level.SourceWadEntry-1).SequenceEqual(compose.Overlay),"overlay readback");
Check(compose.UnresolvedObjectLinks==0 && placements.Length==8,"full seven armor group");var data=new UniversalLevelData(compose.LevelData);var root=data.CaptureRow("peacekeepers",compose.NewRowIndices[0]);for(int i=0;i<7;i++)Check(BitConverter.ToInt32(root.Properties,0x20+4*i)==compose.NewRowIndices[i+1],"physical armor link");
for(int i=0;i<placements.Length;i++){var r=data.Row(compose.NewRowIndices[i]);Check(BitConverter.ToInt32(r,12)==placements[i].X && BitConverter.ToInt32(r,16)==placements[i].Y && BitConverter.ToInt32(r,20)==placements[i].Z,"relative pose");Check(r[0x43]==255&&r[0x4A]==255,"unrestricted activation/visibility");}
Check(UniversalExeBindings.Read(exe,20)[UniversalExeBindings.ParticleUpdateGlobal].Value>=0x8007AA38,"owned update binding");
File.WriteAllLines(Path.ChangeExtension(bin,".cue"),["FILE \"candidate.bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexStringLower(SHA256.HashData(f));}
File.WriteAllText(Path.Combine(dir,"candidate.receipt.json"),JsonSerializer.Serialize(new{Scope="NativeClearedLevelCompilation + ordinary composer; native runtime and particle29 activation pending",Checks=checks,SourceSha256=Hash(args[0]),CandidateSha256=Hash(bin),CoreSha256=Hash(typeof(UniversalMobyComposer).Assembly.Location),placements,compose.NewRowIndices,compose.PlacementSaveRows,compose.Classes,compose.Ram,compose.ParticleDependencies,ParticleUpdateBinding=UniversalExeBindings.Read(exe,20)[UniversalExeBindings.ParticleUpdateGlobal],Terrain="broad three altitude platforms connected by ramps; exact original seven actor and path poses retained; raw floorZ8352,9888,12192",Runtime="pending"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"HAUNTED_FLAT_CANDIDATE_READBACK_OK checks={checks} sha={Hash(bin)}");
