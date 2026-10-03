using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new InvalidOperationException(label); }
void Refuse(Action action, string label, string? reason = null)
{
    try { action(); throw new InvalidOperationException(label + " was accepted"); }
    catch (InvalidDataException e) { Check(reason == null || e.Message.Contains(reason,StringComparison.Ordinal),label+" refusal reason: "+e.Message); }
}
byte[] canonical = NativeId65FreshSceneLayout.Compile(new(8960,8960,9216,64),16,new byte[120],
    NativeId65ZeroSceneAnimationLists.CreateNeutral().CopyComponents()).CopyScene();
// Native entry-relative model roots and a following subfile catch unintended
// broad relocation. This synthetic container is not a source-certified level.
byte[] seed = new byte[0x2800 + canonical.Length];
for (int i=0;i<3;i++) { Put(seed,i*8,0x800+i*0x800); Put(seed,i*8+4,0x800); }
Put(seed,24,0x2000); Put(seed,28,canonical.Length);
Put(seed,32,0x2000+canonical.Length); Put(seed,36,0x800);
Put(seed,0x50,0x1800); canonical.CopyTo(seed,0x2000);
seed.AsSpan(0x2000+canonical.Length,0x800).Fill(0xA7);
var old = new UniversalLevelData(seed); var before = old.Scene();
for (int count=0;count<=256;count++)
{
    byte[] after=NativeFreshPlacementArena.Prepare(seed,count);var body=new UniversalLevelData(after);var layout=body.Scene();
    int added=Math.Max(0,count-1)*0x58;
    Check(layout.MobyComponentSize==before.MobyComponentSize+added,"exact row storage " + count);
    Check(layout.RowCount==0 && layout.Fixups.Count==0,"zero actor/fixup grammar " + count);
    Check((layout.ArenaEnd-layout.RowsOffset-count*0x58)/0x70>=16,"full final dynamic reserve " + count);
    Check(after.AsSpan(0x800,0x1800).SequenceEqual(seed.AsSpan(0x800,0x1800)),"all preceding subfiles exact " + count);
    Check(body.U32(0x50)==old.U32(0x50),"model root unchanged " + count);
    Check(after.AsSpan(body.Subfiles()[4].Offset,0x800).SequenceEqual(seed.AsSpan(old.Subfiles()[4].Offset,0x800)),"following subfile relocated byteexact " + count);
    var file=body.Subfiles()[3];
    Check(after.AsSpan(file.Offset+layout.PropertiesComponent,layout.UsedEnd-layout.PropertiesComponent).SequenceEqual(seed.AsSpan(0x2000+before.PropertiesComponent,before.UsedEnd-before.PropertiesComponent)),"properties/pods/buckets/fixups exact " + count);
    Check(after.AsSpan(file.Offset+layout.UsedEnd,file.Length-layout.UsedEnd).IndexOfAnyExcept((byte)0)<0,"zero sector padding " + count);
    if(count<=1) Check(after.AsSpan().SequenceEqual(seed),"existing zero/single output byteexact " + count);
}
Check(seed.AsSpan(0x2000,canonical.Length).SequenceEqual(canonical),"original input never mutated");
foreach(int count in new[]{0,1,3,165})
{
    var expanded=new UniversalLevelData(NativeFreshPlacementArena.Prepare(seed,count,91));
    var layout=expanded.Scene();var file=expanded.Subfiles()[3];
    Check(layout.MobyComponentSize==before.MobyComponentSize+Math.Max(0,count-1)*0x58+75*0x70,"real source-sized dynamic row/property storage "+count);
    Check((layout.ArenaEnd-layout.RowsOffset-count*0x58)/0x70==91,"complete native91-slot reserve "+count);
    Check(expanded.Bytes.AsSpan(file.Offset+layout.PropertiesComponent,layout.UsedEnd-layout.PropertiesComponent).SequenceEqual(seed.AsSpan(0x2000+before.PropertiesComponent,before.UsedEnd-before.PropertiesComponent)),"expanded reserve preserves all following scene components "+count);
    Check(expanded.Bytes.AsSpan(expanded.Subfiles()[4].Offset,0x800).SequenceEqual(seed.AsSpan(old.Subfiles()[4].Offset,0x800)),"expanded reserve preserves following subfile "+count);
}
Refuse(()=>NativeFreshPlacementArena.Prepare(seed,3,15),"cannot reduce native minimum reserve");
Refuse(()=>NativeFreshPlacementArena.Prepare(seed,166,91),"expanded physical row byte range");
Refuse(()=>NativeFreshPlacementArena.Prepare(seed,0,257),"oversized dynamic reserve");
Refuse(()=>NativeFreshPlacementArena.Prepare(seed,-1),"negative count");
Refuse(()=>NativeFreshPlacementArena.Prepare(seed,257),"native save-count limit");
foreach (int offset in new[]{0xD0,0xD4,before.PropertiesComponent+4,before.CollisionComponent+4,before.FixupsOffset,canonical.Length-1})
{
    byte[] bad=seed.ToArray();bad[0x2000+offset]^=1;
    Refuse(()=>NativeFreshPlacementArena.Prepare(bad,2),"malformed/nonempty canonical owner " + offset.ToString("X"));
}
if(args.Length==0) { Console.WriteLine("NATIVE_FRESH_PLACEMENT_ARENA_OK checks="+checks);return; }
if(args.Length!=3) throw new ArgumentException("Optional source mode: original-USA catalog-workspace artifacts-directory");
Directory.CreateDirectory(args[2]);
var donors=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));
var fresh=await NativeFreshMainCompilation.CaptureAsync(args[0],SceneDocument.New(),Path.Combine(args[2],"fresh-before.bin"));
byte[] original=fresh.CopyLevelData();var placements=new[]{new UniversalMobyPlacement("alpineridge",1,9000,9000,8704){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=0},
    new UniversalMobyPlacement("alpineridge",1,9300,9000,8704){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=1}};
(LevelDefinition Level,UniversalLevelData Data,UniversalOverlay Overlay) OriginalSource(string key) =>
    key==fresh.Target.Key?(fresh.Target,new UniversalLevelData(original),new UniversalOverlay(fresh.CopyOverlay(),UniversalExeBindings.Read(fresh.CopyExecutable(),65))):donors.Get(key);
var direct=UniversalMobyComposer.Compose(fresh.Target,original,fresh.CopyOverlay(),fresh.CopyExecutable(),
    OriginalSource,
    donors.SharedClasses,UniversalMobyExport.DefaultProtectedVram(Path.Combine(args[2],"fresh-before.bin"),fresh.Target),placements.Take(1).ToArray(),
    sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations);
var one=fresh.Compose(donors,placements.Take(1).ToArray());
Check(one.LevelData.AsSpan().SequenceEqual(direct.LevelData) && one.Overlay.AsSpan().SequenceEqual(direct.Overlay) && one.Executable.AsSpan().SequenceEqual(direct.Executable),"one-placement full legacy composition unchanged");
// Negative control: the old host genuinely refuses the same second row.
Refuse(()=>UniversalMobyComposer.Compose(fresh.Target,original,fresh.CopyOverlay(),fresh.CopyExecutable(),OriginalSource,donors.SharedClasses,
    UniversalMobyExport.DefaultProtectedVram(Path.Combine(args[2],"fresh-before.bin"),fresh.Target),placements,
    sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations),"unprepared two-row arena","Only 15 dynamic object slots");
var two=fresh.Compose(donors,placements);var twoData=new UniversalLevelData(two.LevelData);
Check(two.ArenaSlotsRemaining==16 && twoData.Scene().RowCount==2,"two actual source actors preserve16 dynamic slots");
var oneData=new UniversalLevelData(one.LevelData);
for(int fileIndex=0;fileIndex<3;fileIndex++)
{
    var a=oneData.Subfiles()[fileIndex];var b=twoData.Subfiles()[fileIndex];
    Check(a.Length==b.Length && one.LevelData.AsSpan(a.Offset,a.Length).SequenceEqual(two.LevelData.AsSpan(b.Offset,b.Length)),"complete VRAM/data/models unaffected by extra actor "+fileIndex);
}
foreach(var (offset,length) in new[]{(0x4724,0x164),(0x42CC4,0xA4)})
    Check(two.Executable.AsSpan(offset,length).SequenceEqual(donors.Disc.Exe.AsSpan(offset,length)),"certified native scene-loader/allocator unchanged "+offset.ToString("X"));
var finalScene=twoData.Scene();var finalFile=twoData.Subfiles()[3];
foreach(int fixup in finalScene.Fixups)
    Check(fixup>=0 && fixup+4<=finalScene.UsedEnd && twoData.U32(finalFile.Offset+fixup)>=0 && twoData.U32(finalFile.Offset+fixup)<finalScene.UsedEnd,"placed property fixup remains scene-relative and bounded");
Check(two.NewRowIndices.SequenceEqual(new[]{0,1}) && two.PlacementSaveRows.SequenceEqual(new[]{0,1}),"two physical and lifetime save rows");
Check(two.PlayerAnimationImports.Single().AddedAnimationIds.Contains(23),"required reaction23 remains imported");
Check(two.Ram.FinalEnd==UniversalMobyComposer.RuntimeEnd(twoData,new UniversalOverlay(two.Overlay,UniversalExeBindings.Read(two.Executable,65)).CopyBuffer) && two.Ram.FinalHeadroom>=1024,"real final runtime size includes prepared arena");
var retired=placements.Append(placements[0] with {EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=2,Retired=true}).ToArray();
var withRetired=fresh.Compose(donors,retired);
Check(withRetired.LevelData.AsSpan().SequenceEqual(two.LevelData) && withRetired.NewRowIndices[2]==-1,"retired identities consume no actor arena");
Check(fresh.CopyLevelData().AsSpan().SequenceEqual(original),"fresh host immutable after repeated probes");
string output=Path.Combine(args[2],"fresh-two-alpine.bin");
string hash=await fresh.ExportAsync(donors,placements,null,output,args[2],LevelCatalog.Load(args[1]));
var written=UniversalDiscImage.Read(output);
Check(written.EntryBytes(80).AsSpan().SequenceEqual(two.LevelData),"normal fresh export exact two-actor body readback");
Check(written.EntryBytes(79).AsSpan().SequenceEqual(two.Overlay),"normal fresh export exact overlay readback");
UniversalSourceLibrary.RequireOriginal(args[0]);
File.WriteAllText(Path.Combine(args[2],"result.json"),JsonSerializer.Serialize(new{checks,Scope="source-bound static composition and written-disc readback",output,hash,two.Ram,two.ArenaSlotsRemaining,two.PlayerAnimationImports,SceneBeforeSHA256=Convert.ToHexStringLower(SHA256.HashData(original)),RuntimeClaim=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("NATIVE_FRESH_PLACEMENT_ARENA_OK checks="+checks);

static void Put(byte[] bytes,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);
