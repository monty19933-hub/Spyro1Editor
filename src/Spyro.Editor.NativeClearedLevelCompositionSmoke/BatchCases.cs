using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class BatchCases
{
    internal static async Task Run(UniversalSourceLibrary donors,LevelCatalog catalog,string artifacts,
        SceneDocument artisans,IReadOnlyList<UniversalMobyPlacement> placements,UniversalComposeResult result,Action<bool,string> check)
    {
        var peace=donors.Get("peacekeepers");
        var doc=SceneDocument.New("Blank Peace Keepers") with {TargetBinding=new(peace.Level.Key,peace.Level.LevelId,peace.Level.SourceWadEntry,peace.Level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,peace.Data.Scene().RowCount)};
        var peaceHost=await NativeClearedLevelCompilation.CaptureAsync(donors.ImagePath,doc,catalog,Path.Combine(artifacts,"peace-host.bin"));
        var freshDoc=SceneDocument.New("New Authored Level");
        var freshHost=await NativeFreshMainCompilation.CaptureAsync(donors.ImagePath,freshDoc,Path.Combine(artifacts,"fresh-host.bin"));
        // An ordinary, independently staged native sheep transform in Stone Hill.
        // This explicit owned row coordinate must survive the aggregate rebuild.
        var stone=donors.Get("stonehill");var stoneData=new UniversalLevelData(stone.Data.Bytes.ToArray());
        int sheep=Enumerable.Range(0,stoneData.Scene().RowCount).First(i=>BinaryPrimitives.ReadUInt16LittleEndian(stoneData.Row(i).AsSpan(0x36))==0xA);
        int at=stoneData.Subfiles()[3].Offset+stoneData.Scene().RowsOffset+sheep*88+12;
        BinaryPrimitives.WriteInt32LittleEndian(stoneData.Bytes.AsSpan(at),checked(BinaryPrimitives.ReadInt32LittleEndian(stoneData.Bytes.AsSpan(at))+16));
        string stage=Path.Combine(artifacts,"ordinary-retail-stage.bin");
        donors.Disc.WriteImage(stage,donors.Disc.RebuildWad(new Dictionary<int,byte[]>{{stone.Level.SourceWadEntry,stoneData.Bytes}}),donors.Disc.Exe);
        string folder=Path.Combine(artifacts,"batch");Directory.CreateDirectory(folder);string output=Path.Combine(folder,"candidate.bin");
        string hash=await NativeAuthoredProjectExporter.ExportAsync(donors,catalog,[new(artisans,placements),new(doc,[]),new(freshDoc,[])],null,output,folder,20,stagedRetailImage:stage);
        var written=UniversalDiscImage.Read(output);
        check(written.EntryBytes(10).AsSpan().SequenceEqual(result.LevelData),"aggregate cleared Artisans actors and body exact");
        check(written.EntryBytes(9).AsSpan().SequenceEqual(result.Overlay),"aggregate cleared Artisans overlay exact");
        check(written.EntryBytes(peace.Level.SourceWadEntry).AsSpan().SequenceEqual(peaceHost.CopyLevelData()),"aggregate second retail authored body exact");
        check(written.EntryBytes(peace.Level.SourceWadEntry-1).AsSpan().SequenceEqual(peaceHost.CopyOverlay()),"aggregate second retail overlay exact");
        check(written.EntryBytes(80).AsSpan().SequenceEqual(freshHost.CopyLevelData())&&written.EntryBytes(79).AsSpan().SequenceEqual(freshHost.CopyOverlay()),"aggregate authored65 body and overlay exact");
        check(written.EntryBytes(stone.Level.SourceWadEntry).AsSpan().SequenceEqual(stoneData.Bytes),"ordinary other-level native transform remains byte exact");
        var owned=new HashSet<int>{9,10,peace.Level.SourceWadEntry-1,peace.Level.SourceWadEntry,79,80,stone.Level.SourceWadEntry};
        for(int entry=0;entry<256;entry++)if(!owned.Contains(entry))
            check(written.EntryBytes(entry).AsSpan().SequenceEqual(donors.Disc.EntryBytes(entry)),"aggregate unrelated owner "+entry);
        check(BinaryPrimitives.ReadUInt32LittleEndian(written.Exe.AsSpan(0x2F80))==0x24020014&&BinaryPrimitives.ReadUInt32LittleEndian(written.Exe.AsSpan(0x1DCF0))==0x24020014,"both native NewGame owners select retail20");
        File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{hash,StartLevel=20,RebuiltSlots=new[]{10,20,65},OtherEditedEntry=stone.Level.SourceWadEntry,OtherEditedNativeRow=sheep,Evidence="source-bound written aggregate only; native runtime not tested"},new JsonSerializerOptions{WriteIndented=true}));

        string failed=Path.Combine(artifacts,"publication-refusal");Directory.CreateDirectory(failed);
        string failedBin=Path.Combine(failed,"candidate.bin"),failedCue=Path.ChangeExtension(failedBin,".cue");
        byte[] priorBin=[1,2,3,4],priorCue=[5,6,7,8];File.WriteAllBytes(failedBin,priorBin);File.WriteAllBytes(failedCue,priorCue);
        try
        {
            await NativeAuthoredProjectExporter.ExportAsync(donors,catalog,[new(artisans with {Surfaces=[]},[])],null,failedBin,failed,10);
            throw new InvalidOperationException("unsupported start exported");
        }
        catch(InvalidDataException) {check(true,"unsupported start atomic rejection");}
        check(File.ReadAllBytes(failedBin).AsSpan().SequenceEqual(priorBin)&&File.ReadAllBytes(failedCue).AsSpan().SequenceEqual(priorCue),"unsupported floor preserves previous BIN/CUE");
        int callbacks=0;
        try
        {
            await NativeAuthoredProjectExporter.ExportAsync(donors,catalog,[new(artisans with {Ledger=UniversalPlacementLedger.Empty},[])],null,failedBin,failed,10,
                requireCurrent:()=>{if(++callbacks==2)throw new OperationCanceledException("stale publication callback");});
            throw new InvalidOperationException("failed publication callback exported");
        }
        catch(OperationCanceledException) {check(callbacks==2,"publication-time callback rejection");}
        check(File.ReadAllBytes(failedBin).AsSpan().SequenceEqual(priorBin)&&File.ReadAllBytes(failedCue).AsSpan().SequenceEqual(priorCue),"publication callback preserves previous BIN/CUE");
        check(!File.Exists(UniversalNativeLayoutStore.PathFor(failed,"artisans")),"failed publication creates no persistent native identity binding");
        var original=donors.Get("artisans");
        var originalRam=UniversalMobyComposer.RuntimeEnd(original.Data,original.Overlay.CopyBuffer);
        var blankHost=await NativeClearedLevelCompilation.CaptureAsync(donors.ImagePath,artisans,catalog,Path.Combine(artifacts,"capacity-host.bin"));
        var blankBody=new UniversalLevelData(blankHost.CopyLevelData());
        var blankOverlay=new UniversalOverlay(blankHost.CopyOverlay(),UniversalExeBindings.Read(blankHost.CopyExecutable(),10));
        File.WriteAllText(Path.Combine(artifacts,"capacity.json"),JsonSerializer.Serialize(new{
            OriginalRows=original.Data.Scene().RowCount,BlankRows=blankBody.Scene().RowCount,ReplacementRows=new UniversalLevelData(result.LevelData).Scene().RowCount,
            OriginalBodyBytes=original.Data.Bytes.Length,BlankBodyBytes=blankBody.Bytes.Length,ReplacementBodyBytes=result.LevelData.Length,
            OriginalOverlayBytes=original.Overlay.Bytes.Length,BlankOverlayBytes=blankOverlay.Bytes.Length,ReplacementOverlayBytes=result.Overlay.Length,
            OriginalRuntimeEnd=originalRam,BlankRuntimeEnd=UniversalMobyComposer.RuntimeEnd(blankBody,blankOverlay.CopyBuffer),ReplacementRuntimeEnd=result.Ram.FinalEnd,
            OriginalArena=UniversalMobyComposer.ArenaSlots(original.Data),BlankArena=UniversalMobyComposer.ArenaSlots(blankBody),ReplacementArena=result.ArenaSlotsRemaining,
            result.Ram,Scope="physical authored replacement and reclaimed resident storage; static only"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
