using System.Text.Json;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public static partial class TerrainPatchExporter
{
    /// <summary>Materializes an already-complete source-bound terrain plan, including
    /// a growing level body. All other level entries, PETE and executable bytes are
    /// authenticated unchanged before publication; donor sources stay the original disc.</summary>
    public static async Task<TerrainPatchResult> ExportPreparedPlanAsync(TerrainPatchPlan plan,
        bool consumeDisposableSourceImage=false,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        string output=Path.GetFullPath(plan.OutputImagePath),cue=Path.GetFullPath(plan.OutputCuePath);
        string source=Path.GetFullPath(plan.SourceImagePath);
        ExportArtifactTransaction.RequireSourceSeparation(source, Path.ChangeExtension(source,".cue"), output, cue, Path.ChangeExtension(output,"terrain-patch-plan.json"));
        if(plan.SkippedEdits.Any(IsAtomicTerrainSwapBlock))throw new InvalidOperationException("An incomplete terrain plan cannot be materialized.");
        var disc=UniversalDiscImage.Read(source);
        var replacement=plan.NativeLevelReplacement;
        if(replacement!=null&&(replacement.WadEntry!=plan.TextureAssetWadIndex||!disc.EntryBytes(replacement.WadEntry).AsSpan().SequenceEqual(replacement.Before)))
            throw new InvalidDataException("Growing terrain body no longer matches its complete original level preimage.");
        var target=disc.Entry(plan.TextureAssetWadIndex);
        byte[] body=replacement?.After.ToArray()??disc.EntryBytes(plan.TextureAssetWadIndex);
        foreach(var patch in plan.Patches)
        {
            long at=ParseRequiredLong(patch.WadRelativeOffset,"patch offset");
            byte[] before=HexToBytes(patch.BeforeHexPreview),after=HexToBytes(patch.AfterHexPreview);
            if(before.Length!=after.Length||after.Length!=patch.ByteLength||at<target.Offset||at+before.Length>target.Offset+target.Length)
                throw new InvalidDataException("Prepared terrain patches must own equal-length bytes inside the same source level entry.");
            if(!disc.Wad.AsSpan(checked((int)at),before.Length).SequenceEqual(before))throw new InvalidDataException("Prepared terrain patch source preimage changed.");
            after.CopyTo(body,checked((int)(at-target.Offset)));
        }
        if(replacement!=null)
        {
            if(replacement.LevelId<10||replacement.LevelId>64||10+2*((replacement.LevelId/10-1)*6+replacement.LevelId%10)!=replacement.WadEntry)
                throw new InvalidDataException("Growing terrain level identity does not match its native WAD owner.");
            uint copy=UniversalExeBindings.Read(disc.Exe,replacement.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
            uint original=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(replacement.Before),copy);
            if(original!=replacement.OriginalRuntimeEnd)throw new InvalidDataException("Growing terrain original memory ownership changed.");
            uint actual=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(body),copy);
            if(actual!=replacement.FinalRuntimeEnd || actual>UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin&&actual>replacement.OriginalRuntimeEnd)
                throw new InvalidDataException("Growing terrain body failed its final native memory ownership gate.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp",temporaryCue=cue+"."+Guid.NewGuid().ToString("N")+".tmp";
        string report=Path.ChangeExtension(output,"terrain-patch-plan.json"),temporaryReport=report+"."+Guid.NewGuid().ToString("N")+".tmp";
        bool preserveRecovery=false;
        try
        {
            token.ThrowIfCancellationRequested();
            disc.WriteImage(temporary,disc.RebuildWad(new Dictionary<int,byte[]>{{plan.TextureAssetWadIndex,body}}),disc.Exe);
            var readback=UniversalDiscImage.Read(temporary);
            if(!readback.Exe.AsSpan().SequenceEqual(disc.Exe)||!readback.EntryBytes(plan.TextureAssetWadIndex).AsSpan().SequenceEqual(body))
                throw new InvalidDataException("Prepared terrain full-body or executable readback failed.");
            for(int i=0;i<256;i++)
                if(i!=plan.TextureAssetWadIndex&&!readback.EntryBytes(i).AsSpan().SequenceEqual(disc.EntryBytes(i)))
                    throw new InvalidDataException($"Prepared terrain changed unrelated native entry {i}.");
            await File.WriteAllTextAsync(temporaryCue,DiscImage.BuildCueText(Path.ChangeExtension(source,".cue"),Path.GetFileName(output),source,temporary),DiscCueSheet.TextEncoding,token);
            await File.WriteAllTextAsync(temporaryReport,JsonSerializer.Serialize(plan,new JsonSerializerOptions{WriteIndented=true}),token);
            ExportArtifactTransaction.RequireSourceSeparation(source, Path.ChangeExtension(source,".cue"), output, cue, report);
            ExportArtifactTransaction.Publish(Path.GetDirectoryName(output)!, [new(output,temporary),new(cue,temporaryCue),new(report,temporaryReport)],token);
            if(consumeDisposableSourceImage)File.Delete(source);
            return new TerrainPatchResult(output,cue,report,plan,true);
        }
        catch(ExportArtifactRollbackException){preserveRecovery=true;throw;}
        finally
        {
            if(!preserveRecovery)
            {
                if(File.Exists(temporary))File.Delete(temporary);
                if(File.Exists(temporaryCue))File.Delete(temporaryCue);
                if(File.Exists(temporaryReport))File.Delete(temporaryReport);
            }
        }
    }

}
