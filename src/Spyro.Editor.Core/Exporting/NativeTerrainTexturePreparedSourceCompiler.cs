using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public sealed class NativeTerrainTexturePreparedRequest
{
    internal NativeTerrainTexturePreparedRequest(NativeTerrainTexturePrivateRecordBatchRequest request,
        NativeTerrainTextureRecordAppendSourceBinding? authorizationBinding)
    { Request=request;AuthorizationBinding=authorizationBinding; }
    public NativeTerrainTexturePrivateRecordBatchRequest Request { get; }
    /// <summary>The original owner admitted by the retail promotion profile, authenticated
    /// before staging. Final packing still validates its separate actual prepared binding.</summary>
    public NativeTerrainTextureRecordAppendSourceBinding? AuthorizationBinding { get; }
}

/// <summary>Stages an authenticated growing terrain body before the existing private texture
/// allocator. Saved relocation identities are rebound in memory only after their original
/// binding and unchanged texture/VRAM owners are proved; donor/project files are never edited.</summary>
public static class NativeTerrainTexturePreparedSourceCompiler
{
    public static async Task<NativeTerrainTexturePreparedRequest> PrepareAsync(
        NativeTerrainTexturePrivateRecordBatchRequest request, string outputPrefix, CancellationToken token=default)
    {
        if(request.TerrainPreparation==null)return new(request,null);
        var plan=request.TerrainPreparation;
        var replacement=plan.NativeLevelReplacement ?? throw new InvalidDataException("Prepared private terrain request has no complete growing body.");
        if(!string.Equals(Path.GetFullPath(plan.SourceImagePath),Path.GetFullPath(request.SourceImagePath),StringComparison.Ordinal)||
            !string.Equals(plan.LevelKey,request.TargetLevel.Key,StringComparison.OrdinalIgnoreCase)||
            replacement.WadEntry!=request.TargetLevel.SourceWadEntry||request.OrdinaryLevelDataPatches.Count!=0||
            request.HasCustomImageTexturePatches)
            throw new InvalidDataException("Growing private terrain preparation must own the same original source/level with no duplicate ordinary or image texture writes.");
        var original=NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(request.SourceImagePath,request.TargetLevel);
        foreach(var edit in request.SavedEdits.Where(e=>e.UsesAppendedPrivateRecord))
            if(edit.TargetWadEntry!=original.TargetWadEntry||edit.SourceTextureRecordCount!=original.ExpectedSourceTextureCount||
                !Equal(edit.SourceImageSha256,original.SourceImageSha256)||
                !Equal(edit.SourceTextureComponentSha256,original.ExpectedTextureComponentSha256)||
                !Equal(edit.SourceLevelDataSha256,original.ExpectedLevelDataSha256))
                throw new InvalidDataException("Growing terrain cannot rebind a stale original private texture contract.");
        ValidateNativeBody(request,replacement);
        var before=new UniversalLevelData(replacement.Before);var after=new UniversalLevelData(replacement.After);
        var oldVram=before.Subfiles()[0];var newVram=after.Subfiles()[0];
        if(oldVram.Length!=newVram.Length||!replacement.Before.AsSpan(oldVram.Offset,oldVram.Length).SequenceEqual(replacement.After.AsSpan(newVram.Offset,newVram.Length)))
            throw new InvalidDataException("Growing terrain preparation changed native VRAM storage; private texture rebinding is refused.");
        var written=await TerrainPatchExporter.ExportPreparedPlanAsync(plan with{
            OutputImagePath=outputPrefix+".bin",OutputCuePath=outputPrefix+".cue"},token:token);
        var prepared=NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(written.OutputImagePath,request.TargetLevel);
        if(prepared.TargetWadEntry!=original.TargetWadEntry||prepared.ExpectedSourceTextureCount!=original.ExpectedSourceTextureCount||
            !Equal(prepared.ExpectedTextureComponentSha256,original.ExpectedTextureComponentSha256))
            throw new InvalidDataException("Growing terrain preparation changed the bound native texture table; private texture rebinding is refused.");
        var rebound=request with{SourceImagePath=written.OutputImagePath,SourceCuePath=written.OutputCuePath,TerrainPreparation=null,
            SavedEdits=request.SavedEdits.Select(e=>!e.UsesAppendedPrivateRecord?e:e with{
                SourceImageSha256=prepared.SourceImageSha256,SourceTextureComponentSha256=prepared.ExpectedTextureComponentSha256,
                SourceLevelDataSha256=prepared.ExpectedLevelDataSha256}).ToArray()};
        return new(rebound,original);
    }

    private static bool Equal(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);

    private static void ValidateNativeBody(NativeTerrainTexturePrivateRecordBatchRequest request,NativeTerrainLevelReplacement replacement)
    {
        var disc=UniversalDiscImage.Read(request.SourceImagePath);
        if(replacement.LevelId!=request.TargetLevel.LevelId||!disc.EntryBytes(replacement.WadEntry).AsSpan().SequenceEqual(replacement.Before))
            throw new InvalidDataException("Growing private terrain body does not match its original native level owner.");
        uint copy=UniversalExeBindings.Read(disc.Exe,replacement.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
        uint original=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(replacement.Before),copy);
        uint final=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(replacement.After),copy);
        if(original!=replacement.OriginalRuntimeEnd||final!=replacement.FinalRuntimeEnd||
            GameRam.Exceeds(new UniversalLevelData(replacement.After),copy,new UniversalLevelData(replacement.Before),copy))
            throw new InvalidDataException("Growing private terrain body failed its authenticated original/final native memory guard.");
    }
}
