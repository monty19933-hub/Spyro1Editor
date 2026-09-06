using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class NativeLevelRuntimeMemoryCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] Fixture(int placed=1)
        {
            byte[] b=new byte[0x2800];
            for(int i=0;i<4;i++){Put(b,i*8,0x800+i*0x800);Put(b,i*8+4,0x800);}
            Put(b,0x50,0x1800);Put(b,0x54,0x1804);b[0x152]=10;
            int p=0x1000;for(int i=0;i<6;i++){Put(b,p,8);p+=8;}
            Put(b,p,0);p+=4;Put(b,p,8);p+=8;Put(b,p,8);
            p=0x2088;for(int i=0;i<7;i++){Put(b,p,8);p+=8;}
            Put(b,p,8+88+112*3);Put(b,p+4,placed);
            return b;
        }
        var b=Fixture();var report=NativeLevelRuntimeMemoryReport.DecodeFixture(13,b,0x80080000);
        check(report.UsedLevelDataBytes==68&&report.LevelDataSectorPaddingBytes==1980,"RAM used-data walk retained sector padding or skipped a component.");
        check(report.ModelRamStart==0x80080044&&report.SceneRamStart==0x80080844&&report.SceneReadEnd==0x80081044,"RAM model/scene CD-read endpoints changed.");
        check(report.DynamicArenaBytes==336&&report.DynamicMobyCapacity==3&&report.PlacedMobyCount==1,"Dynamic Moby pool is not 88-byte rows plus 24-byte properties.");
        check(!report.RuntimeFitProven&&!report.PeakTransientFitProven&&!report.PublicationApproved&&!report.NativeSourceCertified,"Synthetic structural report granted native/runtime approval.");
        var shrunk=NativeLevelRuntimeMemoryReport.DecodeFixture(13,Fixture(2),0x80080000);
        check(shrunk.DynamicArenaBytes==248&&shrunk.DynamicMobyCapacity==2,"One new placed row did not reduce the shared dynamic pool.");
        var crowded=NativeLevelRuntimeMemoryReport.DecodeFixture(13,b,0x80187000);
        check(!crowded.KnownPostLoadExtentsDisjoint&&crowded.NormalReservedBufferGapBytes<0,"Known RAM overlap was accepted as disjoint.");
        var bad=b.ToArray();Put(bad,0x1000,0);reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,bad,0x80080000),"component");
        bad=b.ToArray();Put(bad,0x1030,int.MaxValue);reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,bad,0x80080000),"portal");
        bad=b.ToArray();Put(bad,0x1030,1);reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,bad,0x80080000),"portal point");
        bad=b.ToArray();bad[0x1050]=1;reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,bad,0x80080000),"padding");
        bad=b.ToArray();Put(bad,0x20C4,4096);reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,bad,0x80080000),"placed");
        reject(()=>NativeLevelRuntimeMemoryReport.DecodeFixture(13,b,0x7FFFFFFC),"RAM");
        Console.WriteLine("NATIVE_LEVEL_RAM_USED_DATA_MODEL_SCENE_AND_DYNAMIC_POOL_BOUNDS_OK");
    }

    internal static void Source(NativeTownSquareLockedChestClosure compiled,Action<bool,string> check)
    {
        var report=NativeLevelRuntimeMemoryReport.AnalyzeTownClosure(compiled);
        check(report.NativeSourceCertified&&report.LevelId==13&&report.SourceDataSha256=="7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0","Town RAM report lost exact source identity.");
        check(report.CopyBufferAddress==0x8008B238&&report.UsedLevelDataBytes==0x94508&&report.ModelRamStart==0x8011F740&&report.SceneRamStart==0x8017DF40,"Town native relocated memory bases changed.");
        check(report.SceneReadEnd==0x80186740&&report.NormalReservedBufferGapBytes==0x1470,"Town normal-buffer gap arithmetic changed.");
        check(report.PlacedMobyCount==114&&report.DynamicArenaBytes==0x2598&&report.DynamicMobyCapacity==85,"Town private rows silently reused dynamic arena without accounting.");
        check(report.KnownPostLoadExtentsDisjoint&&!report.RuntimeFitProven&&!report.PeakTransientFitProven,"Known Town extents became a complete runtime certificate.");
    }

    internal static void AuditSource(string path,Action<bool,string> check,Action<Action,string> reject)
    {
        var inventory=NativeMobySourceInventory.Capture(path,LevelCatalog.Load(Directory.GetCurrentDirectory()));
        using var image=File.OpenRead(path);var layout=DiscImage.DetectLayout(path);
        byte[] exe=DiscImage.ReadFileBytes(image,layout,53875,0,0x66000);
        byte[] peteHeader=DiscImage.ReadFileBytes(image,layout,37,0x7B8800,0x800);
        byte[] pete=DiscImage.ReadFileBytes(image,layout,37,0x7B9000,0x39C48);
        List<NativeLevelRuntimeMemoryReport> reports=[];
        foreach(var level in inventory.Levels)
        {
            byte[] data=DiscImage.ReadFileBytes(image,layout,37,level.DataWadOffset,level.SourceDataByteLength);
            var r=NativeLevelRuntimeMemoryReport.CaptureOriginal(level,data,exe,peteHeader,pete);reports.Add(r);
            check(r.NativeSourceCertified&&r.KnownPostLoadExtentsDisjoint&&r.NormalReservedBufferStart==0x80187BB0,"A native retail normal-load baseline changed.");
            check(r.PlacedMobyCount==level.Rows.Count&&r.DynamicArenaBytes==level.ReservedMobyArenaByteLength,"RAM pool accounting disagrees with independent native inventory.");
            if(level.LevelId==13)
            {
                check(r.UsedLevelDataBytes==0x94508&&r.LevelDataSectorPaddingBytes==0x2F8&&r.SceneReadEnd==0x80183BD4&&r.NormalReservedBufferGapBytes==0x3FDC,"Retail Town used-data/post-load endpoints changed.");
                check(r.DynamicMobyCapacity==91,"Retail Town original dynamic capacity changed.");
                byte[] stale=data.ToArray();stale[^1]^=1;
                reject(()=>NativeLevelRuntimeMemoryReport.CaptureOriginal(level,stale,exe,peteHeader,pete),"source inventory");
                byte[] staleExe=exe.ToArray();staleExe[0x4B098]^=1;
                reject(()=>NativeLevelRuntimeMemoryReport.CaptureOriginal(level,data,staleExe,peteHeader,pete),"source certificate");
                byte[] stalePete=pete.ToArray();stalePete[^1]^=1;
                reject(()=>NativeLevelRuntimeMemoryReport.CaptureOriginal(level,data,exe,peteHeader,stalePete),"source certificate");
            }
        }
        check(reports.Count==35&&reports.Min(r=>r.DynamicMobyCapacity)==91&&reports.Max(r=>r.DynamicMobyCapacity)==272,"Native dynamic arena capacity extremes changed.");
        check(reports.MinBy(r=>r.NormalReservedBufferGapBytes) is {LevelId:62,NormalReservedBufferGapBytes:700},"Native normal-load tightest baseline changed.");
        var player=NativePlayerAnimationSource.CaptureAsync(path).GetAwaiter().GetResult();
        var toastyMerge=player.CompileMergeAsync(path,33,14,[8]).GetAwaiter().GetResult();
        var toasty=reports.Single(r=>r.LevelId==14).AnalyzePlayerMerge(toastyMerge);
        check(toasty.NormalReservedBufferGapBytes==0x9F80&&toasty.KnownPostLoadExtentsDisjoint&&!toasty.RuntimeFitProven,"Plain retail Toasty player merge acquired complete RAM approval or wrong source footprint.");
        var townMerge=player.CompileMergeAsync(path,33,13,[8]).GetAwaiter().GetResult();
        var town=reports.Single(r=>r.LevelId==13).AnalyzePlayerMerge(townMerge);
        check(town.NormalReservedBufferGapBytes==-0x24&&!town.KnownPostLoadExtentsDisjoint,"Town animation-8 0x4000 growth escaped known reserved-buffer overlap accounting.");
        reject(()=>reports.Single(r=>r.LevelId==13).AnalyzePlayerMerge(toastyMerge),"own this original");
        reject(()=>town.AnalyzePlayerMerge(townMerge),"own this original");
        foreach(var r in reports.OrderByDescending(r=>r.NormalReservedBufferGapBytes))
            Console.WriteLine($"NATIVE_RAM_NORMAL_BASELINE level={r.LevelId} gap=0x{r.NormalReservedBufferGapBytes:X} used=0x{r.UsedLevelDataBytes:X} model=0x{r.ModelBytesRead:X} scene=0x{r.SceneBytesRead:X} dynamic={r.DynamicMobyCapacity} runtime=false");
        Console.WriteLine("NATIVE_LEVEL_RAM_ALL_35_SOURCE_BOUNDS_AND_TYPED_PLAYER_GROWTH_OK");
    }
    private static void Put(byte[] b,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(offset),value);
}
