using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
if(args is ["--plant-snow"]){PlantSnowCases.Run();return;}
int checks=0;void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
void Refused(Action action,string label){try{action();}catch(InvalidDataException){Check(true,label);return;}throw new Exception(label);}
if(args.Length==0)
{
    var random=new Random(17291);
    for(int n=0;n<256;n++)
    {
        byte[] row=new byte[0x58];random.NextBytes(row);row[0x36]=250;row[0x37]=0;byte[] before=row.ToArray();
        var colour=new ColorRgba((byte)n,(byte)(255-n),(byte)(n^85));DragonStatueColor.Apply(row,colour);
        Check(row[0x4B]==(before[0x4B]|128)&&row[0x4C]==colour.R&&row[0x4D]==colour.G&&row[0x4E]==colour.B&&row[0x4F]==126,"lossless RGB and renderer tag");
        Check(row[..0x4B].SequenceEqual(before[..0x4B])&&row[0x50..].SequenceEqual(before[0x50..]),"unrelated row bytes intact");
    }
    Refused(()=>DragonStatueColor.Apply(new byte[0x58],new(1,2,3)),"nonstatue refused");
    Refused(()=>DragonStatueColor.Apply(new byte[8],new(1,2,3)),"truncated row refused");
    byte[] package=new byte[0x900];
    void Put(int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(package.AsSpan(at),value);
    Put(0,1);Put(0x34,0x100);Put(0x38,0x40);Put(0x58,0);Put(0x60,0x400);
    foreach(ushort n in new ushort[]{1,255,256}){BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(0x42),n);NativeDragonStatueColors.RequirePalette(package);Check(true,"bounded near/far palettes");}
    foreach(ushort n in new ushort[]{0,257,65535}){BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(0x42),n);Refused(()=>NativeDragonStatueColors.RequirePalette(package),"invalid palette count");}
    BinaryPrimitives.WriteUInt16LittleEndian(package.AsSpan(0x42),255);Put(0x60,int.MaxValue);Refused(()=>NativeDragonStatueColors.RequirePalette(package),"palette extent overflow");
    Put(0x38,-1);Refused(()=>NativeDragonStatueColors.RequirePalette(package),"absent palettes refused");
    Refused(()=>NativeDragonStatueColors.Plan(new byte[0x800]),"truncated executable refused");
    // "To finish 100%": a level's own dragons (rows 5, 9) and thief (row 20), kept, removed, added back, added twice.
    var own=new LevelOwnRows([5,9],[20],[]);
    var whole=LevelCompletionCheck.Check(own,"townsquare",false,[],[]);
    Check(whole.Complete&&LevelCompletionCheck.Describe(whole,"Town Square")==null&&LevelCompletionCheck.Summary(whole)=="To finish 100%: dragons 2 of 2, egg thief 1 of 1 ✓","untouched level complete");
    var blank=LevelCompletionCheck.Check(own,"townsquare",true,[],[("townsquare",5),("stonehill",9),("townsquare",21)]);
    Check(blank is {DragonsPresent:1,ThievesPresent:0,MissingDragons:1,MissingThieves:1}&&
        LevelCompletionCheck.Describe(blank,"Town Square","Cloudspire Isles")=="Cloudspire Isles (in Town Square's place) needs 1 of Town Square's dragons and Town Square's egg thief (Add Object: dragons and egg thief from Town Square).",
        "rebuilt level: only its own rows count");
    var twice=LevelCompletionCheck.Check(own,"townsquare",false,[9],[("TOWNSQUARE",20),("townsquare",9)]);
    Check(twice is {Complete:true,DuplicateThieves:1,DuplicateDragons:0}&&
        LevelCompletionCheck.Describe(twice,"Town Square")=="Town Square has the same egg thief twice; the game counts only one, so remove the extra copy."&&
        !LevelCompletionCheck.Summary(twice)!.EndsWith("✓"),"a thief kept and added again is flagged");
    Check(LevelCompletionCheck.Summary(LevelCompletionCheck.Check(LevelOwnRows.None,"sunnyflight",false,[],[]))==null,"no line for a level without dragons, thieves or exit");
    // Return Home: the level's own vortex (row 30) kept, or any level's vortex (class 9) added to the rebuilt level.
    var exits=new LevelOwnRows([5],[],[30]);int SourceClass(string k,int r)=>k=="darkhollow"&&r==118?9:r==30?9:0x55;
    var kept=LevelCompletionCheck.Check(exits,"stonehill",false,[],[],SourceClass);
    Check(kept is {Complete:true,ExitNeeded:true,ExitPresent:true}&&LevelCompletionCheck.Summary(kept)=="To finish 100%: dragons 1 of 1  ·  Return Home vortex: yes ✓","own vortex kept");
    var none=LevelCompletionCheck.Check(exits,"stonehill",true,[],[("stonehill",5),("darkhollow",117)],SourceClass);
    Check(none is {Complete:false,ExitPresent:false}&&LevelCompletionCheck.Describe(none,"Stone Hill","Frost")=="Frost (in Stone Hill's place) needs a Return Home vortex (Add Object: Return Home from any level).","rebuilt level without a vortex");
    Check(LevelCompletionCheck.Check(exits,"stonehill",true,[],[("stonehill",5),("darkhollow",118)],SourceClass) is {Complete:true,ExitPresent:true},"another level's vortex counts");
    Console.WriteLine($"DRAGON_STATUE_COLOUR_SOURCE_FREE_OK checks={checks}");return;
}
if(args.Length!=3)throw new ArgumentException("USA.bin catalogRoot newOutput");
UniversalSourceLibrary.RequireOriginal(args[0]);var catalog=LevelCatalog.Load(args[1]);var library=new UniversalSourceLibrary(args[0],catalog);
string root=Path.GetFullPath(args[2]);if(Directory.Exists(root))throw new IOException("Preserve existing fixture.");Directory.CreateDirectory(root);
foreach(var patch in NativeDragonStatueColors.Plan(library.Disc.Exe))
{
    for(int n=0;n<patch.Before.Length;n++)
    {
        byte[] changed=library.Disc.Exe.ToArray();changed[patch.Address-0x8000F800+n]^=1;
        Refused(()=>NativeDragonStatueColors.Plan(changed),"every original host/site byte authenticated");
        byte[] installed=library.Disc.Exe.ToArray();NativeDragonStatueColors.Install(installed);installed[patch.Address-0x8000F800+n]^=1;
        Refused(()=>NativeDragonStatueColors.OriginalRendererView(installed),"every installed host/site byte authenticated");
    }
}
int nativeRows=0;
foreach(var level in catalog.Levels.Where(l=>l.SourceWadEntry>0))
{
    var src=library.Get(level.Key);
    for(int i=0;i<src.Data.Scene().RowCount;i++)
    {
        byte[] original=src.Data.Row(i);if(BitConverter.ToUInt16(original,54)!=250)continue;nativeRows++;
        foreach(var c in new[]{new ColorRgba(255,0,0),new ColorRgba(0,255,0),new ColorRgba(0,0,255),new ColorRgba(230,42,184),new ColorRgba(0,0,0),new ColorRgba(255,255,255)})
        {
            byte[] row=original.ToArray();DragonStatueColor.Apply(row,c);
            Check(row.Where((b,n)=>n<0x4B||n>0x4F).SequenceEqual(original.Where((b,n)=>n<0x4B||n>0x4F)),"unrelated statue fields preserved");
            Check((row[0x4B]&127)==(original[0x4B]&127)&&(row[0x4B]&128)!=0&&row[0x4F]==0x7E,"native distance preserved and specular renderer enabled");
            uint packed=BitConverter.ToUInt32(row,0x4C);
            Check(packed==(0x7E000000u|c.R|(uint)c.G<<8|(uint)c.B<<16),"lossless per-instance RGB888 renderer tag");
            NativeDragonStatueColors.RequirePalette(src.Data.Package(250));

        }
    }
}
var target=library.Get("darkhollow");var pose=FlyInLandingLocator.Locate(args[0],target.Level);
Moby Statue(int index)
{
    var r=target.Data.Row(index);var p=new Vector3f(BitConverter.ToInt32(r,12)/16f,BitConverter.ToInt32(r,16)/16f,BitConverter.ToInt32(r,20)/16f);
    return new(){Index=index,TrueIndex=index,LegacyIndex=MobyLoader.GetLegacyAliasIndex(index),Position=p,OriginalPosition=p,Label="Dragon",OriginalLabel="Dragon",
        Type=r[0x50],OriginalType=r[0x50],State=r[0x51],OriginalState=r[0x51],YawByte=r[0x46],OriginalYawByte=r[0x46],SourceByte36=r[0x36],OriginalSourceByte36=r[0x36],SourceByte37=r[0x37],OriginalSourceByte37=r[0x37],SourceByte4F=r[0x4F],OriginalSourceByte4F=r[0x4F],Flag4A=r[0x52],OriginalFlag4A=r[0x52],Flag4B=r[0x53],OriginalFlag4B=r[0x53]};
}
var red=Statue(65);red.DragonColor=new(255,0,0);red.Position=new((pose.RawX+2000)/16f,(pose.RawY-1400)/16f,(pose.RawZ-255)/16f);
var blue=Statue(95);blue.DragonColor=new(0,0,255);blue.Position=new((pose.RawX+3200)/16f,(pose.RawY+900)/16f,(pose.RawZ-255)/16f);
string manifest=Path.Combine(root,"darkhollow-native-edits.json");await MobyEditStore.SaveAsync(manifest,[red,blue],"Dark Hollow");
var output=await MobySourcePatchExporter.ExportAsync(new(args[0],Path.ChangeExtension(args[0],".cue"),Path.Combine(root,"ordinary"),target.Level,manifest,true));
Check(output.WroteImage,"normal retail colour export writes disc");
Check(output.Plan.Patches.Count(p=>p.Kind=="moby-dragon-colour")==2,"two independent per-statue native colour patches");
var ordinary=UniversalDiscImage.Read(output.OutputImagePath);var data=new UniversalLevelData(ordinary.EntryBytes(target.Level.SourceWadEntry));
foreach(var m in new[]{red,blue})
{
    byte[] expect=target.Data.Row(m.TrueIndex);DragonStatueColor.Apply(expect,m.DragonColor!.Value);
    Check(data.Row(m.TrueIndex).AsSpan(0x4B,5).SequenceEqual(expect.AsSpan(0x4B,5)),"written colour readback");
}
Check(data.Row(111).SequenceEqual(target.Data.Row(111)),"third native dragon remains unchanged");
byte[] expectedExe=library.Disc.Exe.ToArray();NativeDragonStatueColors.Install(expectedExe);Check(ordinary.Exe.SequenceEqual(expectedExe),"normal export installed exact renderer");
Check(NativeDragonStatueColors.Plan(ordinary.Exe).Count==0,"renderer installation is idempotent");
Check(NativeDragonStatueColors.OriginalRendererView(ordinary.Exe).SequenceEqual(library.Disc.Exe),"native consumer view restores only exact extension");
UniversalCliffMetalTextures.ProveNative(ordinary.Exe);Check(true,"native metal renderer coexists with statue tint");
var combined = UniversalMobyComposer.Compose(target.Level, data.Bytes, target.Overlay.Bytes.ToArray(), ordinary.Exe,
    library.Get, library.SharedClasses, UniversalMobyExport.DefaultProtectedVram(output.OutputImagePath, target.Level),
    [new("clifftown", 24, pose.RawX+1800, pose.RawY-1600, pose.RawZ-213)],
    sharedModelPackage:library.SharedModelPackage, playerAnimationSource:library.PlayerAnimations);
Check(combined.MetalTextureBindings.Count==1,"actual metal enemy composes after a normal statue-colour export");
Check(NativeDragonStatueColors.Plan(combined.Executable).Count==0,"combined export preserves exact installed tint extension");
for(int i=0;i<256;i++)if(i!=target.Level.SourceWadEntry)Check(ordinary.EntryBytes(i).SequenceEqual(library.Disc.EntryBytes(i)),"unrelated WAD entry"+i);
byte[] exe=ordinary.Exe.ToArray();NativeClearedRetailBinding.ApplyNewGameStart(library.Disc.Exe,exe,target.Level.LevelId);
string bin=Path.Combine(root,"candidate.bin");ordinary.WriteImage(bin,ordinary.Wad,exe);
File.WriteAllLines(Path.ChangeExtension(bin,".cue"),["FILE \"candidate.bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);
string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexStringLower(SHA256.HashData(f));}
File.WriteAllText(Path.Combine(root,"candidate.receipt.json"),JsonSerializer.Serialize(new{Checks=checks,NativeStatueRows=nativeRows,pose,Statues=new[]{red,blue}.Select(m=>new{m.TrueIndex,m.Position,m.DragonColor}),SourceSha256=Hash(args[0]),CandidateSha256=Hash(bin),CoreSha256=Hash(typeof(DragonStatueColor).Assembly.Location),output.Plan.Patches,Runtime="not-tested"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"DRAGON_STATUE_COLOUR_SOURCE_OK checks={checks} statues={nativeRows} output={bin}");
