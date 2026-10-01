using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class PreparedPrivateTerrainGrowthCases
{
    internal static async Task Run(string image,string workspace,string snapshot,string planPath,Action<bool,string> check)
    {
        var plan=JsonSerializer.Deserialize<TerrainPatchPlan>(File.ReadAllText(planPath))!;
        image=plan.SourceImagePath;
        var library=new UniversalSourceLibrary(image,LevelCatalog.Load(workspace));
        var source=library.Get("townsquare");
        var original=NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(image,source.Level);
        var replacement=plan.NativeLevelReplacement ?? throw new InvalidDataException("Expected actual grown geometry fixture");
        check(replacement.FinalRuntimeEnd>replacement.OriginalRuntimeEnd && replacement.After.Length==replacement.Before.Length+0x800,
            "Real TownSquare96-unit sculpt grows both authenticated native geometry allocation and used runtime bytes");
        var donor=NativeTerrainTextureRelocationEditStore.Load(snapshot,"artisans").First();
        var edit=donor with{TargetTextureId=original.ExpectedSourceTextureCount,TargetWadEntry=source.Level.SourceWadEntry,
            SourceImageSha256=original.SourceImageSha256,SourceTextureRecordCount=original.ExpectedSourceTextureCount,
            SourceTextureComponentSha256=original.ExpectedTextureComponentSha256,SourceLevelDataSha256=original.ExpectedLevelDataSha256,
            MaterialTemplateTextureId=28,PrivateRecordEditId=NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(original.ExpectedSourceTextureCount)};
        var sheep=library.Get("stonehill");
        int sheepRow=Enumerable.Range(0,sheep.Data.Scene().RowCount).First(i=>BinaryPrimitives.ReadUInt16LittleEndian(sheep.Data.Row(i).AsSpan(0x36))==0x0A);
        var row=sheep.Data.Row(sheepRow);
        int[] removed=[6,10];
        check(removed.All(i=>BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(i).AsSpan(0x36))==0x18B) &&
            UniversalMobyComposer.DroppableClasses(source.Data,source.Overlay,library.Disc.Exe,library.SharedClasses,removed,new HashSet<int>()).Classes.Contains(0x18B),
            "Isolated combo fixture explicitly reclaims complete source-proven class18B rows6/10; no user project changes");
        UniversalMobyPlacement[] placements=[new("stonehill",sheepRow,BitConverter.ToInt32(row,12),BitConverter.ToInt32(row,16),BitConverter.ToInt32(row,20),
            EditorId:"prepared-growth-sheep",PersistentSlot:0)];
        string scratch=Path.Combine(Path.GetTempPath(),"spyro-real-private-growth-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        try
        {
            var terrainEdits=JsonNode.Parse(File.ReadAllText(plan.TerrainEditsPath))!;
            var face=terrainEdits["edits"]!.AsArray().Single(node=>node!["runtimeKey"]!.GetValue<string>()=="213:37:hp")!;
            face["textureIdEdited"]=edit.TargetTextureId;
            string editedPath=Path.Combine(scratch,"assigned-private-sculpt.json");
            File.WriteAllText(editedPath,terrainEdits.ToJsonString());
            plan=TerrainPatchExporter.BuildPlan(image,Path.ChangeExtension(image,".cue"),Path.Combine(scratch,"sculpt.bin"),Path.Combine(scratch,"sculpt.cue"),
                source.Level,plan.RamPath,plan.SourceSearchPath,editedPath);
            plan=TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(plan,source.Level,[edit]);
            replacement=plan.NativeLevelReplacement ?? throw new InvalidDataException("Assigned appearance lost actual growing body");
            check(TextureId(new UniversalLevelData(replacement.After),213,37)==edit.TargetTextureId,
                "Real 96-unit sculpt face carries assigned private material after coupled HP/LP/collision compilation");
            var request=new NativeTerrainTexturePrivateRecordBatchRequest(image,Path.ChangeExtension(image,".cue"),Path.Combine(scratch,"private"),"",source.Level,[edit],[])
            {TerrainPreparation=plan,AdditionalProtectedRanges=UniversalTerrainReservations.Capture(library,source.Level,placements,removedRows:removed)};
            var prepared=await NativeTerrainTexturePreparedSourceCompiler.PrepareAsync(request,Path.Combine(scratch,"terrain"));
            check(prepared.AuthorizationBinding==original,"Real sculpt/private stage retains original verified promotion owner");
            var packed=NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(prepared.Request);
            check(packed.SourcePreimagesVerified && packed.FixedTailPlan is {GlobalPackingProofComplete:true,CombinedPatchesDisjoint:true},
                "Real growing model and private append retain exact global pixel/palette/material/preimage proofs");
            var host=UniversalCompositionHost.FromPrivateTerrainPlan(image,source.Level,packed,prepared.Request.SourceImagePath);
            var result=host.DryRun(library,placements,removedRows:removed);
            check(result.Error==null,"Real geometry-growth/private-record/sourceSheep composition: "+result.Error);
            var data=new UniversalLevelData(result.Result!.LevelData);
            check(BitConverter.ToInt32(data.Bytes,data.Subfiles()[1].Offset+4)==original.ExpectedSourceTextureCount+1,
                "Real growing terrain retains appended private record");
            check(TextureId(data,213,37)==edit.TargetTextureId,"Final private append and sourceSheep composition preserve assigned sculpt face material");
            int originalEnvironment=source.Data.Subfiles()[1].Offset+BitConverter.ToInt32(source.Data.Bytes,source.Data.Subfiles()[1].Offset);
            int finalEnvironment=data.Subfiles()[1].Offset+BitConverter.ToInt32(data.Bytes,data.Subfiles()[1].Offset);
            int originalSectors=BitConverter.ToInt32(source.Data.Bytes,originalEnvironment+4), finalSectors=BitConverter.ToInt32(data.Bytes,finalEnvironment+4);
            check(finalSectors>originalSectors && Enumerable.Range(originalSectors,finalSectors-originalSectors).All(i=>
            {
                int sector=finalEnvironment+4+BitConverter.ToInt32(data.Bytes,finalEnvironment+8+i*4);
                return data.Bytes[sector+20]==0 && (BitConverter.ToUInt16(data.Bytes,sector+4)&0xC000)==0xC000;
            }),"Joint grown/private/model composition preserves verified LP-only radius flags and excludes empty HP rendering");
            check(result.Result.NewRowIndices.Count==1 &&
                result.Result.PlacementSaveRows.Single()==source.Data.Scene().RowCount,"SourceSheep emitted once with stable native save identity");
            check(result.Result.Ram.FinalHeadroom>=UniversalMobyComposer.RamSafetyMargin && data.Subfiles()[1].Length>=source.Data.Subfiles()[1].Length+0x800,
                "Actual expanded geometry footprint included under unchanged runtime safety margin");
            Console.WriteLine(JsonSerializer.Serialize(new{Scope="actual v25 TownSquare213:37 two-corner96-unit sculpt + private record + sourceSheep",
                PrivateRecordAssignedToFace=true,RemovedNativeRows=removed,RemovedNativeClass="0x18B",PrivateRecordId=edit.TargetTextureId,SourceSheepRow=sheepRow,result.Result.NewRowIndices,result.Result.PlacementSaveRows,
                result.Result.Ram,replacement.OriginalRuntimeEnd,replacement.FinalRuntimeEnd,Status="assigned private paint/sculpt/sourceSheep composed-static/readback; gameplay not tested"}));
        }
        finally{Directory.Delete(scratch,true);}
    }
    private static int TextureId(UniversalLevelData data,int sectorIndex,int faceIndex)
    {
        int model=data.Subfiles()[1].Offset;
        int environment=checked(model+BitConverter.ToInt32(data.Bytes,model));
        int sector=checked(environment+4+BitConverter.ToInt32(data.Bytes,environment+8+sectorIndex*4));
        if(faceIndex>=data.Bytes[sector+22])throw new InvalidDataException("Native HP face outside sector");
        int faces=checked(sector+28+4*(data.Bytes[sector+16]+data.Bytes[sector+17]+2*data.Bytes[sector+18]+data.Bytes[sector+20]+2*data.Bytes[sector+21]));
        return BitConverter.ToInt32(data.Bytes,faces+faceIndex*16+8)&0x7F;
    }
}
