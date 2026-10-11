using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class SourceCases
{
    internal static async Task Run(string original,string workspace,string artifacts,Action<bool,string> check,bool replacementOnly=false)
    {
        var catalog=LevelCatalog.Load(workspace);
        var donors=new UniversalSourceLibrary(original,catalog);
        MeshFaceCacheCases.RunSource(donors,catalog,check);
        await ThiefEggCases.RunSource(original,catalog,donors,artifacts,check);
        var old=donors.Get("artisans");
        int prefix=old.Data.Scene().RowCount;
        var binding=new SceneTargetBinding(old.Level.Key,old.Level.LevelId,old.Level.SourceWadEntry,old.Level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,prefix);
        var document=SceneDocument.New("Blank Artisans") with {TargetBinding=binding};
        var baseline=Path.Combine(artifacts,"cleared-host.bin");
        var cleared=await NativeClearedLevelCompilation.CaptureAsync(original,document,catalog,baseline);
        byte[] data=cleared.CopyLevelData(),overlay=cleared.CopyOverlay(),exe=cleared.CopyExecutable();
        check(new UniversalLevelData(data).Scene().RowCount==0,"real rebuilt retail host is empty");
        check(cleared.Target.LevelId==10 && cleared.Target.SourceWadEntry==10,"exact original retail target");
        int gnorcRow=Enumerable.Range(0,prefix).First(i=>BinaryPrimitives.ReadUInt16LittleEndian(old.Data.Row(i).AsSpan(0x36))==0x72);
        int gemRow=Enumerable.Range(0,prefix).First(i=>BinaryPrimitives.ReadUInt16LittleEndian(old.Data.Row(i).AsSpan(0x36))==0x53);
        var placements=new[]{
            new UniversalMobyPlacement("artisans",gemRow,556*16,560*16,544*16){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=0},
            new UniversalMobyPlacement("artisans",gemRow,558*16,560*16,544*16){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=1,Retired=true},
            new UniversalMobyPlacement("artisans",gnorcRow,600*16,560*16,544*16){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=2}};
        document=document with {Ledger=new(1,placements.Select(p=>new UniversalPlacementIdentity(p.EditorId!,p.PersistentSlot!.Value)).ToArray())};
        var result=cleared.Compose(donors,placements);
        check(result.NewRowIndices.SequenceEqual(new[]{0,-1,1}),"retired row consumes no physical actor");
        check(result.PlacementSaveRows.SequenceEqual(new[]{prefix,-1,prefix+2}),"original save prefix and retired lifetime slot retained");
        check(result.PhysicalToLogicalSaveRows.SequenceEqual(new[]{prefix,prefix+2}),"actual physical to old-prefix logical mapping");
        check(result.ArenaSlotsRemaining==16,"native dynamic reserve remains16");
        check(result.Ram.FinalHeadroom>=1024,"final actual RAM floor");
        var finalData=new UniversalLevelData(result.LevelData);
        check(finalData.Scene().RowCount==2,"real two-actor scene");
        check(result.Classes.Any(c=>c.SourceLevelKey=="artisans"&&c.ClassId==0x53),"same-level source donor remains available after clear");
        var localEnemy=cleared.Compose(donors,[new("artisans",gnorcRow,600*16,560*16,544*16){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=0}]);
        check(localEnemy.Classes.Any(c=>c.SourceLevelKey=="artisans"&&c.ClassId==0x72),"same-level local enemy model imported from original donor");
        check(localEnemy.Behavior.Any(b=>b.SourceLevelKey=="artisans"&&b.Classes.Contains(0x72)),"same-level local enemy behavior imported from original donor");
        check(localEnemy.PhysicalToLogicalSaveRows.SequenceEqual(new[]{prefix}),"same-level local enemy retains original-prefix identity");
        // This supported native Gnorc has no foreign reaction-animation owner.
        // Preserve the complete retained player stream rather than asserting an Alpine-only dependency.
        var initial=new UniversalLevelData(data);int playerRoot=initial.U32(0x50),finalPlayerRoot=finalData.U32(0x50);
        int playerLength=initial.Registrations().Where(r=>r.Root>playerRoot).Min(r=>r.Root)-playerRoot;
        check(finalData.Bytes.AsSpan(finalPlayerRoot,playerLength).SequenceEqual(data.AsSpan(playerRoot,playerLength)),"supported native Gnorc preserves complete retained player stream");
        check(result.PlayerAnimationImports.Count==0,"native Gnorc introduces no unrelated foreign reaction animations");
        check(result.Classes.Any(c=>c.SourceLevelKey=="artisans"&&c.ClassId==0x72)&&result.Behavior.Any(b=>b.SourceLevelKey=="artisans"&&b.Classes.Contains(0x72)),"replacement contains full original native Gnorc model and behavior");
        RewardFallbackCases.RequirePreserved(donors,data,check,"blank cleared host");
        RewardFallbackCases.RequireProtected(baseline,cleared.Target,check,"blank cleared host");
        RewardFallbackCases.RequirePreserved(donors,result.LevelData,check,"replacement after native imports");
        RewardFallbackCases.RequirePreserved(donors,localEnemy.LevelData,check,"standalone native enemy import");
        for(int i=0;i<placements.Length;i++)
        {
            if(placements[i].Retired)continue;
            byte[] row=finalData.Row(result.NewRowIndices[i]);var p=placements[i];
            check(BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(12))==p.X&&BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(16))==p.Y&&BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(20))==p.Z,"native placed XYZ "+i);
        }
        UniversalComposeResult Direct(int requestedPrefix,UniversalOverlay? owner=null,byte[]? input=null)=>UniversalMobyComposer.Compose(cleared.Target,input??data,overlay,exe,donors.Get,donors.SharedClasses,[],[placements[0]],
            sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations,persistentNativePrefixRows:requestedPrefix,nativeDestinationOwner:owner??new(overlay,UniversalExeBindings.Read(exe,10)));
        void Refuse(Action action,string name)
        {
            try { action();throw new InvalidOperationException(name+" accepted"); }
            catch(InvalidDataException) {check(true,name);}
        }
        Refuse(()=>cleared.Compose(donors,[new("alpineridge",1,600*16,560*16,544*16){EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=0}]),"lone Alpine actor cannot bypass complete original-host ownership");
        var unsupportedAlpineGroup=UniversalPlacementGroups.Expand(donors.Get,new("alpineridge",1,600*16,560*16,544*16,EditorId:Guid.NewGuid().ToString("N"),PersistentSlot:0));
        Refuse(()=>cleared.Compose(donors,unsupportedAlpineGroup),"complete Alpine group still requires its issued original-host capability");
        Refuse(()=>Direct(prefix-1),"incorrect original prefix refuses");
        Refuse(()=>Direct(-1),"negative prefix refuses");
        Refuse(()=>Direct(257),"prefix above bitmap refuses");
        Refuse(()=>Direct(prefix,input:old.Data.Bytes),"prefix requires empty rebuilt host");
        var changed=overlay.ToArray();changed[^1]^=1;
        Refuse(()=>Direct(prefix,new(changed,UniversalExeBindings.Read(exe,10))),"different native destination byte owner refuses");
        var bindings=UniversalExeBindings.Read(exe,10);
        var changedExe=exe.ToArray();UniversalExeBindings.Patch(changedExe,bindings[UniversalExeBindings.UpdateGlobal],bindings[UniversalExeBindings.UpdateGlobal].Value+4);
        Refuse(()=>UniversalMobyComposer.Compose(cleared.Target,data,overlay,changedExe,donors.Get,donors.SharedClasses,[],[placements[0]],persistentNativePrefixRows:prefix,nativeDestinationOwner:new(overlay,bindings)),"callback mismatch refuses before composition");
        var last=placements[0] with {PersistentSlot=255-prefix};
        var lifetime=Enumerable.Range(0,last.PersistentSlot!.Value+1).Select(i=>placements[0] with {EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=i,Retired=i!=last.PersistentSlot}).ToArray();
        var boundary=cleared.Compose(donors,lifetime);
        check(boundary.PlacementSaveRows.Last()==255,"last supported logical bitmap bit");
        var overflow=lifetime.Append(placements[0] with {EditorId=Guid.NewGuid().ToString("N"),PersistentSlot=lifetime.Length}).ToArray();
        Refuse(()=>cleared.Compose(donors,overflow),"active lifetime bitmap overflow refuses");
        check(cleared.CopyLevelData().AsSpan().SequenceEqual(data)&&cleared.CopyOverlay().AsSpan().SequenceEqual(overlay)&&cleared.CopyExecutable().AsSpan().SequenceEqual(exe),"captured host bytes immutable after success/refusal");
        check(donors.Disc.EntryBytes(10).AsSpan().SequenceEqual(old.Data.Bytes),"original donor body untouched");
        // Rebuilt retail scenes must support the complete native portal-arrival footprint.
        var newTerrain=document with {Name="Rebuilt Artisans",Surfaces=[new(Guid.NewGuid().ToString("N"),
            [new(320,320,544),new(800,320,544),new(800,800,544),new(320,800,544)],new(64,160,200),null)]};
        var newTerrainHost=await NativeClearedLevelCompilation.CaptureAsync(original,newTerrain,catalog,Path.Combine(artifacts,"new-terrain-host.bin"));
        var newTerrainPlacements=placements.Select(p=>p.SourceRowIndex==gnorcRow?p with {X=660*16}:p).ToArray();
        var newTerrainResult=newTerrainHost.Compose(donors,newTerrainPlacements);
        RewardFallbackCases.RequirePreserved(donors,newTerrainResult.LevelData,check,"new authored terrain and native enemy");
        check(!newTerrainResult.LevelData.AsSpan().SequenceEqual(result.LevelData),"replacement authors new quad geometry and colour");
        check(newTerrainResult.PhysicalToLogicalSaveRows.SequenceEqual(result.PhysicalToLogicalSaveRows),"new terrain preserves imported lifetime identities");
        foreach(var (name,doc,rows,expectedBody,expectedOverlay) in new[]{
            ("blank",document with {Ledger=UniversalPlacementLedger.Empty},Array.Empty<UniversalMobyPlacement>(),data,overlay),
            ("replacement",document,placements,result.LevelData,result.Overlay),
            ("replacement-new-terrain",newTerrain,newTerrainPlacements,newTerrainResult.LevelData,newTerrainResult.Overlay)}
            .Where(v=>!replacementOnly || v.Item1=="replacement-new-terrain"))
        {
            string folder=Path.Combine(artifacts,name);Directory.CreateDirectory(folder);
            string bin=Path.Combine(folder,"candidate.bin");
            string hash=await NativeAuthoredProjectExporter.ExportAsync(donors,catalog,[new(doc,rows)],null,bin,folder,10);
            var written=UniversalDiscImage.Read(bin);
            RewardFallbackCases.RequirePreserved(donors,written.EntryBytes(10),check,name+" independent written readback");
            RewardFallbackCases.RequireProtected(bin,cleared.Target,check,name+" written readback");
            check(written.EntryBytes(10).AsSpan().SequenceEqual(expectedBody),name+" actual exporter body readback");
            check(written.EntryBytes(9).AsSpan().SequenceEqual(expectedOverlay),name+" actual exporter overlay readback");
            for(int entry=0;entry<256;entry++)if(entry is not 9 and not 10)
                check(written.EntryBytes(entry).AsSpan().SequenceEqual(donors.Disc.EntryBytes(entry)),name+" unrelated WAD owner "+entry);
            check(BinaryPrimitives.ReadUInt32LittleEndian(written.Exe.AsSpan(0x2F80))==0x2402000A&&BinaryPrimitives.ReadUInt32LittleEndian(written.Exe.AsSpan(0x1DCF0))==0x2402000A,name+" original Artisans NewGame");
            check(File.ReadAllLines(Path.ChangeExtension(bin,".cue")).Length==3,name+" valid three-line CUE");
            File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{ImageSha256=hash,CoreSha256=Hash(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)),Evidence="source-bound composition and written readback; native runtime not tested",prefix,Placements=rows,PhysicalMap=name=="blank"?[]:result.PhysicalToLogicalSaveRows,Target=cleared.Target,Ram=name=="blank"?null:result.Ram},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("NATIVE_CLEARED_WRITTEN_FIXTURE_READY "+name+" "+hash+" "+bin);
        }
        if(!replacementOnly) await BatchCases.Run(donors,catalog,artifacts,newTerrain,newTerrainPlacements,newTerrainResult,check);
    }
    private static string Hash(byte[] value)=>Convert.ToHexStringLower(SHA256.HashData(value));
}
