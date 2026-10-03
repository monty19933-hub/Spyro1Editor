using System.Reflection;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
int checks=0;void Check(bool b,string name){if(!b)throw new Exception(name);checks++;}
void Refuse(Action f){try{f();throw new Exception("Unsafe world owner accepted");}catch(InvalidDataException e){Check(e.Message.Contains("Green Druid world dependencies are not relocated"),"exact semantic refusal");}}
var library=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var dh=library.Get("darkhollow");var cases=new List<object>();
var method=typeof(UniversalMobyComposer).Assembly.GetType("Spyro.Editor.Core.Universal.UniversalGreenDruidWorldOwnership")!.GetMethod("Require",BindingFlags.NonPublic|BindingFlags.Static)!;
void Direct(string destination,string source,int c,UniversalLevelData current,UniversalLevelData original,HashSet<int>? removed=null){try{method.Invoke(null,[destination,source,c,current,original,removed??[]]);}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){throw e.InnerException;}}
foreach(string key in new[]{"magiccrafters","alpineridge"}){
 var donor=library.Get(key);var originalData=donor.Data.Bytes.ToArray();var originalOverlay=donor.Overlay.Bytes.ToArray();var hostData=dh.Data.Bytes.ToArray();var hostOverlay=dh.Overlay.Bytes.ToArray();var exe=library.Disc.Exe.ToArray();
 for(int i=0;i<donor.Data.Scene().RowCount;i++){
  var row=donor.Data.Row(i);if(BitConverter.ToUInt16(row,0x36)!=0x10E)continue;
  var p=new UniversalMobyPlacement(key,i,16945,60751,17152);
  Refuse(()=>UniversalMobyComposer.Compose(dh.Level,dh.Data.Bytes,dh.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,[],[p]));
  cases.Add(new{key,row=i,Status="world-owner-refused"});
 }
 Direct(key.ToUpperInvariant(),key,0x10E,new(donor.Data.Bytes.ToArray()),donor.Data);Check(true,"same-level original world retained");
 var worldChanged=new UniversalLevelData(donor.Data.Bytes.ToArray());worldChanged.Bytes[worldChanged.Subfiles()[1].Offset+4]^=1;Refuse(()=>Direct(key,key,0x10E,worldChanged,donor.Data));
 var animationChanged=new UniversalLevelData(donor.Data.Bytes.ToArray());animationChanged.Bytes[animationChanged.Subfiles()[3].Offset+0x88+4]^=1;Refuse(()=>Direct(key,key,0x10E,animationChanged,donor.Data));
 var rowChanged=new UniversalLevelData(donor.Data.Bytes.ToArray());rowChanged.Bytes[rowChanged.Subfiles()[3].Offset+rowChanged.Scene().RowsOffset+0x36]^=1;Refuse(()=>Direct(key,key,0x10E,rowChanged,donor.Data));Refuse(()=>Direct(key,key,0x10E,donor.Data,donor.Data,[0]));
 int sourceRow=key=="magiccrafters"?7:5;var original=donor.Data.Row(sourceRow);var same=UniversalMobyComposer.Compose(donor.Level,donor.Data.Bytes,donor.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,UniversalMobyExport.DefaultProtectedVram(args[0],donor.Level).ToArray(),[new(key,sourceRow,BitConverter.ToInt32(original,12),BitConverter.ToInt32(original,16),BitConverter.ToInt32(original,20))],sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
 var composed=new UniversalLevelData(same.LevelData);var retained=composed.Subfiles()[1];var owned=donor.Data.Subfiles()[1];Check(composed.Bytes.AsSpan(retained.Offset,retained.Length).SequenceEqual(donor.Data.Bytes.AsSpan(owned.Offset,owned.Length)),"actual same-level Compose retains entire native world");
 Check(donor.Data.Bytes.AsSpan().SequenceEqual(originalData)&&donor.Overlay.Bytes.SequenceEqual(originalOverlay)&&dh.Data.Bytes.AsSpan().SequenceEqual(hostData)&&dh.Overlay.Bytes.SequenceEqual(hostOverlay)&&library.Disc.Exe.AsSpan().SequenceEqual(exe),"all caller-owned assets immutable");
}
// The refusal is scoped to the exact known native family, not all class IDs or pairs.
foreach(var (key,c) in new[]{("magiccrafters",0x10F),("alpineridge",0x98),("darkhollow",0x10E)}){Direct("darkhollow",key,c,new(new byte[1]),new(new byte[1]));Check(true,"unrelated actor scope unchanged");}
UniversalSourceLibrary.RequireOriginal(args[0]);File.WriteAllText(args[2],JsonSerializer.Serialize(new{checks,cases,RuntimeVerified=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("GREEN_DRUID_WORLD_OWNERSHIP_OK checks="+checks);
