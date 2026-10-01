using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

if(args.Length is <3 or >5)throw new ArgumentException("original-USA catalog-workspace new-artifacts-directory [--matrix|--export|--fresh private-baseline-BIN|--near-portal routed-BIN]");
args[0]=Path.GetFullPath(args[0]);args[1]=Path.GetFullPath(args[1]);args[2]=Path.GetFullPath(args[2]);
if(Directory.Exists(args[2]))throw new IOException("Choose a new isolated artifact directory.");
Directory.CreateDirectory(args[2]);
int checks=0;
void Check(bool value,string label){checks++;if(!value)throw new InvalidOperationException(label);}
void Refuse(Action action,string label){try{action();throw new InvalidOperationException(label+" unexpectedly admitted");}catch(InvalidDataException){checks++;}}
UniversalSourceLibrary.RequireOriginal(args[0]);
var catalog=LevelCatalog.Load(args[1]);var library=new UniversalSourceLibrary(args[0],catalog);
string store=Path.Combine(args[2],"project");
var missing=PortalRouteStore.Capture(store);
Check(missing.Fingerprint=="absent"&&!missing.Document.HasEdits,"new project has native routes");
foreach(var original in HomeworldPortalControlCatalog.All)
{
    var endpoint=new PortalEndpoint(original.HomeworldKey,original.PathTrueIndex);
    var doc=PortalRouteDocument.Empty.WithDestination(endpoint,33,true);
    var saved=PortalRouteStore.Save(store,doc,PortalRouteStore.Capture(store));
    var reopened=PortalRouteStore.Capture(store);
    Check(reopened.Fingerprint==saved.Fingerprint,"exact route file hash survives reopen");
    Check(reopened.Document.DestinationFor(endpoint)==33,"foreign destination survives save");
    Check(reopened.Document.ReturnFor(33)==endpoint,"paired return endpoint survives save");
    Check(endpoint.Resolve()==original,"endpoint preserves original physical portal identity");
    Check(File.Exists(Path.Combine(store,PortalRouteStore.PreviousFileName)),"previous route version retained");
}
var stone=new PortalEndpoint("artisans",38);var town=new PortalEndpoint("artisans",39);
var duplicate=PortalRouteDocument.Empty.WithDestination(stone,13,true).WithDestination(town,13,false);
Check(duplicate.DestinationFor(stone)==13&&duplicate.DestinationFor(town)==13,"two physical portals may share destination");
Check(duplicate.ReturnFor(13)==stone,"explicit return disambiguates duplicated destination");
var switched=duplicate.WithReturn(13,town);
Check(switched.ReturnFor(13)==town&&duplicate.ReturnFor(13)==stone,"return edits are immutable");
Check(switched.WithReturn(13,null).ReturnFor(13)==town,"return reset restores native endpoint");
Check(PortalRouteDocument.Empty.WithDestination(stone,33,true).ReturnFor(33)==stone,"cross-homeworld return points to origin home");
Check(PortalRouteDocument.Empty.WithReturn(65,stone).ReturnFor(65)==stone,"authored65 explicit return identity retained");
var before=PortalRouteStore.Capture(store);
PortalRouteStore.Save(store,duplicate,before);
string latest=PortalRouteStore.Capture(store).Fingerprint;
Refuse(()=>before.RequireCurrent(store),"saved input change invalidates captured export snapshot");
Refuse(()=>PortalRouteStore.Save(store,switched,before),"stale writer refuses");
Check(PortalRouteStore.Capture(store).Fingerprint==latest,"stale refusal preserves latest route file");
var lockSnapshot=PortalRouteStore.Capture(store);
string mainPath=Path.Combine(store,PortalRouteStore.FileName),previousPath=Path.Combine(store,PortalRouteStore.PreviousFileName),lockPath=Path.Combine(store,".portal-routes.lock");
byte[] mainBeforeLock=File.ReadAllBytes(mainPath),previousBeforeLock=File.ReadAllBytes(previousPath);
Check(File.Exists(lockPath),"route store retains its stable writer lock file");
string lockRefusal="";
using(var held=new FileStream(lockPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
{
    bool secondWriterRejected=await Task.Run(()=>
    {
        try{PortalRouteStore.Save(store,switched,lockSnapshot);return false;}
        catch(IOException ex){lockRefusal=ex.Message;return true;}
    });
    Check(secondWriterRejected,"held store lock rejects a concurrent second writer");
    Check(File.ReadAllBytes(mainPath).SequenceEqual(mainBeforeLock),"rejected concurrent writer preserves main route bytes");
    Check(File.ReadAllBytes(previousPath).SequenceEqual(previousBeforeLock),"rejected concurrent writer preserves previous recovery bytes");
    Check(!Directory.EnumerateFiles(store,PortalRouteStore.FileName+".stage-*").Any(),"lock refusal creates no stage artifacts");
}
Check(File.Exists(lockPath),"released writer lease preserves the stable lock file");
var unlockedSave=PortalRouteStore.Save(store,switched,lockSnapshot);
var unlockedRead=PortalRouteStore.Capture(store);
Check(unlockedRead.Fingerprint==unlockedSave.Fingerprint&&unlockedRead.Document.ReturnFor(13)==town,"released lock permits the expected writer and saved route reopens");
Check(File.ReadAllBytes(previousPath).SequenceEqual(mainBeforeLock),"successful save atomically publishes the exact prior main as recovery copy");
byte[] unlockedMain=File.ReadAllBytes(mainPath),unlockedPrevious=File.ReadAllBytes(previousPath);
Refuse(()=>PortalRouteStore.Save(store,duplicate,lockSnapshot),"released lease does not admit a stale snapshot writer");
Check(File.ReadAllBytes(mainPath).SequenceEqual(unlockedMain)&&File.ReadAllBytes(previousPath).SequenceEqual(unlockedPrevious),"stale writer preserves both committed main and recovery versions");
Check(!Directory.EnumerateFiles(store,PortalRouteStore.FileName+".stage-*").Any(),"success and stale refusal leave no abandoned stage artifacts");
var storeReceipt=new{LockFile=lockPath,LockRefusal=lockRefusal,HeldMainHash=Convert.ToHexStringLower(SHA256.HashData(mainBeforeLock)),HeldPreviousHash=Convert.ToHexStringLower(SHA256.HashData(previousBeforeLock)),CommittedMainHash=unlockedRead.Fingerprint,CommittedPreviousHash=Convert.ToHexStringLower(SHA256.HashData(unlockedPrevious)),ConcurrentWriterRejected=true,StaleWriterRejected=true};
Refuse(()=>duplicate.WithDestination(stone,66,true),"out-of-range destination refuses");
Refuse(()=>duplicate.WithDestination(new("artisans",0),13,true),"unowned source portal refuses");
Refuse(()=>(duplicate with{Destinations=duplicate.Destinations.Append(duplicate.Destinations[0]).ToArray()}).Validate(),"duplicate physical override refuses");
Refuse(()=>(duplicate with{SourceImageSha256="changed"}).Validate(),"different source disc refuses");

var sourceReports=new List<object>();
foreach(var group in HomeworldPortalControlCatalog.All.GroupBy(d=>d.HomeworldKey))
{
    var owner=library.Get(group.Key);var source=PortalSourceDataLocator.Locate(args[0],owner.Level);
    Check(source.Portals.Count==group.Count(),"complete homeworld portal owner count");
    foreach(var portal in source.Portals)
    {
        var original=group.Single(d=>d.PathTrueIndex==portal.SourcePathMobyTrueIndex);
        Check(portal.DestinationLevelId==original.DestinationLevelId,"native matching token agrees with catalog");
        Check(portal.Path.NodeCount==2&&portal.Path.Nodes.Count==2,"native two-node route owner");
        var surface=source.SpecialSurfaces.Single(s=>s.Type==6&&s.PortalIndex==portal.Index);
        Check(surface.DestinationLevelId==original.DestinationLevelId,"native travel surface agrees with portal token");
        Check(source.TriggerTriangles.Any(t=>t.PortalIndex==portal.Index&&t.DestinationLevelId==original.DestinationLevelId),"native walk-in collision owned");
        byte[] lettering=owner.Data.CaptureRow(group.Key,original.LetteringTrueIndex).Properties;
        Check(lettering.Length==4,"lettering has exact four-byte table selector");
        sourceReports.Add(new{group.Key,portal.Index,portal.DestinationLevelId,portal.SourcePathMobyTrueIndex,portal.WadOffset,portal.Path,
            Type6WadOffset=surface.WadOffset,LetteringRow=original.LetteringTrueIndex,LetteringSelector=BinaryPrimitives.ReadInt32LittleEndian(lettering)});
    }
}
File.WriteAllText(Path.Combine(args[2],"source-store-checks.json"),JsonSerializer.Serialize(new{checks,Scope="saved-route persistence and authenticated original native field owners",SourceHash=UniversalSourceLibrary.OriginalUsaSha256,
    CoreModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId,CoreModuleHash=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location))),Store=storeReceipt,Portals=sourceReports,RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PORTAL_ROUTE_SOURCE_STORE_CHECKS_OK checks="+checks);

if(args.Length==5){if(args[3]=="--fresh")FreshVerification.Run(args[4],args[2],catalog,library,Check);else if(args[3]=="--near-portal")await ApproachVerification.Run(args[4],args[2],catalog,Check);else throw new ArgumentException("Unknown option");Console.WriteLine("PORTAL_ROUTE_EXTENDED_CHECKS_OK checks="+checks);}
if(args.Length==4){if(args[3] is not ("--matrix" or "--export"))throw new ArgumentException("Unknown option");await ExportVerification.Run(args[0],args[2],catalog,library,Check,args[3]=="--export");Console.WriteLine("PORTAL_ROUTE_PRODUCTION_CHECKS_OK checks="+checks);}
