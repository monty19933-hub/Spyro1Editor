using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
if(args.Length!=3)throw new Exception("USA BIN, catalog root, NEW report directory");
string dir=Path.GetFullPath(args[2]);if(Directory.Exists(dir))throw new Exception("Preserve report directory");Directory.CreateDirectory(dir);
var catalog=LevelCatalog.Load(args[1]);var lib=new UniversalSourceLibrary(args[0],catalog);var haunted=lib.Get("hauntedtowers");var peace=lib.Get("peacekeepers");
var doc=SceneDocument.New("Haunted source QA") with{TargetBinding=new("peacekeepers",20,peace.Level.SourceWadEntry,peace.Level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,peace.Data.Scene().RowCount)};var floor=new List<AuthoredSurface>();foreach(int x in new[]{368,560})foreach(int y in new[]{368,560})floor.Add(new(Guid.NewGuid().ToString("N"),[new(x,y,544),new(x+192,y,544),new(x+192,y+192,544),new(x,y+192,544)],new(128,112,80),new AuthoredMaterial(10,66)));doc=doc with{Surfaces=floor};
var cleared=await NativeClearedLevelCompilation.CaptureAsync(args[0],doc,catalog,Path.Combine(dir,"baseline.bin"));
int checks=0;List<object> receipts=[];void Check(bool v,string msg){if(!v)throw new Exception(msg);checks++;}
void Case(IReadOnlyList<UniversalMobyPlacement> placements,string name,bool alias=false){
 UniversalComposeResult result;
 if(alias){var host=new UniversalLevelData(cleared.CopyLevelData());foreach(int c in new[]{0xCB,0xCC,0xD7,0x26})host.AppendModel(c,haunted.Data.Package(c));var prepared=NativeFreshPlacementArena.Prepare(host.Bytes,placements.Count);result=UniversalMobyComposer.Compose(peace.Level,prepared,cleared.CopyOverlay(),cleared.CopyExecutable(),lib.Get,lib.SharedClasses,UniversalMobyExport.DefaultProtectedVram(Path.Combine(dir,"baseline.bin"),peace.Level),placements,sharedModelPackage:lib.SharedModelPackage,playerAnimationSource:lib.PlayerAnimations,persistentNativePrefixRows:peace.Data.Scene().RowCount);Check(result.Classes.Where(c=>c.ClassId is 0xCB or 0xCC).All(c=>c.DestinationClassId!=c.ClassId),"forced occupied-namespace aliases");}
 else result=cleared.Compose(lib,placements);if(name=="variant0"){File.WriteAllBytes(Path.Combine(dir,"variant0-baseline-data.bin"),cleared.CopyLevelData());File.WriteAllBytes(Path.Combine(dir,"variant0-final-data.bin"),result.LevelData);File.WriteAllBytes(Path.Combine(dir,"variant0-final-executable.bin"),result.Executable);}
 var data=new UniversalLevelData(result.LevelData);Check(result.UnresolvedObjectLinks==0,name+" links");
 if(result.Classes.Any(c=>c.SourceLevelKey=="hauntedtowers"&&c.ClassId==0xCC)){
  Check(result.ParticleDependencies.Single(p=>p.SourceLevelKey=="hauntedtowers"&&p.TypeId==29).Status=="source-certified-copied",name+" issued resurrection status");
  Check(!result.Warnings.Any(w=>w.StartsWith("Particle 0x1D from hauntedtowers:")),name+" no stale resurrection warning");
  Check(result.ParticleDependencies.Where(p=>p.SourceLevelKey=="hauntedtowers"&&p.TypeId!=29).All(p=>p.Status=="dependency-unverified"),name+" other effects retain evidence boundary");
 }
 for(int i=0;i<placements.Count;i++){
  var p=placements[i];var original=UniversalIndexedRows.Capture(haunted.Data,haunted.Overlay,haunted.Data.CaptureRow("hauntedtowers",p.SourceRowIndex)) with{LinkGroupId=p.LinkGroupId};
  var resolved=UniversalIndexedRows.Resolve(original,placements.Select(q=>UniversalIndexedRows.Capture(haunted.Data,haunted.Overlay,haunted.Data.CaptureRow("hauntedtowers",q.SourceRowIndex)) with{LinkGroupId=q.LinkGroupId}).ToArray());
  var emitted=data.CaptureRow("peacekeepers",result.NewRowIndices[i]);var translated=UniversalHauntedWizardPlacement.Rebase(haunted.Data,haunted.Overlay,original,data.Row(result.NewRowIndices[i]));
  foreach(var link in resolved.ImportedRowIndices)Check(BitConverter.ToInt32(emitted.Properties,link.Offset)==result.NewRowIndices[link.PlacementIndex],name+" physical link");
  var changed=resolved.ImportedRowIndices.SelectMany(l=>Enumerable.Range(l.Offset,4)).Concat(translated.InternalPointers.SelectMany(l=>Enumerable.Range(l.Offset,4))).Concat(translated.ExternalRowPointers.SelectMany(l=>Enumerable.Range(l.Offset,4))).ToHashSet();
  Check(emitted.Properties.Length==translated.Properties.Length,name+" props span");for(int b=0;b<emitted.Properties.Length;b++)if(!changed.Contains(b))Check(emitted.Properties[b]==translated.Properties[b],name+" emitted source/property/path byte"+b);
  Check(BitConverter.ToInt32(emitted.Row,12)==p.X&&BitConverter.ToInt32(emitted.Row,16)==p.Y&&BitConverter.ToInt32(emitted.Row,20)==p.Z,name+" pose");
 }
 receipts.Add(new{name,Placements=placements.Count,result.NewRowIndices,result.PlacementSaveRows,result.Classes,result.Ram,result.ParticleDependencies,ParticleUpdate=UniversalExeBindings.Read(result.Executable,20)[UniversalExeBindings.ParticleUpdateGlobal].Value});Console.WriteLine(name+" static-composed");
}
foreach(int row in Enumerable.Range(0,haunted.Data.Scene().RowCount).Where(i=>haunted.Data.CaptureRow("hauntedtowers",i).ClassId is 0xCB or 0xCC)){var p=new UniversalMobyPlacement("hauntedtowers",row,8960,8960,9216,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:0);Case(UniversalPlacementGroups.Expand(lib.Get,p).Select((v,i)=>v with{PersistentSlot=i}).ToArray(),"variant"+row);}
var duplicate=new List<UniversalMobyPlacement>();foreach(int x in new[]{9000,19000})duplicate.AddRange(UniversalPlacementGroups.Expand(lib.Get,new("hauntedtowers",52,x,9000,13000,EditorId:Guid.NewGuid().ToString("N"))));Case(duplicate.Select((v,i)=>v with{PersistentSlot=i}).ToArray(),"independent duplicate groups52");
Case(UniversalPlacementGroups.Expand(lib.Get,new("hauntedtowers",52,9000,9000,13000,EditorId:Guid.NewGuid().ToString("N"))).Select((v,i)=>v with{PersistentSlot=i}).ToArray(),"explicit occupied-namespace mapped group52",true);
File.WriteAllText(Path.Combine(dir,"report.json"),JsonSerializer.Serialize(new{Checks=checks,Cases=receipts,Scope="Current-Core integrated source composition and emitted property/index/path readback only; runtime pending"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"HAUNTED_INTEGRATED_SOURCE_COMPOSE_OK checks={checks} cases={receipts.Count}");
