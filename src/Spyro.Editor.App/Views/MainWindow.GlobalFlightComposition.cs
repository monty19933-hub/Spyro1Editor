using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private async Task<(string Image,string Cue)> MergeGlobalFlightAnimationsAsync(NativeFreeFlightCompositionPlan flight,
        string currentImage,string outputImage)
    {
        await Task.Run(()=>
        {
            var disc=UniversalDiscImage.Read(currentImage);Dictionary<int,byte[]> replacements=[];
            foreach(var level in _retailCatalog.Levels.Where(l=>l.SourceWadEntry>0))
            {
                byte[] before=disc.EntryBytes(level.SourceWadEntry);
                var merged=flight.MergeLevelData(level.LevelId,before);byte[] after=merged.CopyLevelData();
                if(after.AsSpan().SequenceEqual(before))continue;
                var overlay=new UniversalOverlay(disc.EntryBytes(level.SourceWadEntry-1),UniversalExeBindings.Read(disc.Exe,level.LevelId));
                // The level's game RAM in the export's memory mode (GameRam: one 2 MB budget, or two with 8 MB RAM).
                if(!GameRam.Fits(new UniversalLevelData(after),overlay.CopyBuffer))
                    throw new InvalidDataException($"{level.DisplayName}: flight animations exceed the native RAM budget in the actual edited body.");
                replacements[level.SourceWadEntry]=after;
            }
            disc.WriteImage(outputImage,disc.RebuildWad(replacements),disc.Exe);
            var read=UniversalDiscImage.Read(outputImage);
            foreach(var(entry,bytes)in replacements)
                if(!read.EntryBytes(entry).AsSpan().SequenceEqual(bytes))throw new InvalidDataException("Global flight body readback changed.");
        });
        string cue=Path.ChangeExtension(outputImage,".cue");
        await File.WriteAllLinesAsync(cue,["FILE \""+Path.GetFileName(outputImage)+"\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);
        return(outputImage,cue);
    }
    private async Task ApplyGlobalFlightExecutableAsync(NativeFreeFlightCompositionPlan flight,string image)
    {
        await Task.Run(()=>
        {
            var disc=UniversalDiscImage.Read(image);
            flight.RequireResidentEntry8(disc.EntryBytes(8));
            byte[] exe=flight.ApplyToExecutable(disc.Exe);
            if (flight.Enabled)
                foreach(var level in _retailCatalog.Levels.Where(l=>NativeFreeFlightCompositionPlan.IsNormalLevel(l.LevelId) && flight.IsLevelEnabled(l.LevelId)))
                    _ = flight.RequireFinalLevelClosure(level.LevelId,disc.EntryBytes(level.SourceWadEntry),exe);
            disc.WriteImage(image,disc.Wad,exe);
            if(!UniversalDiscImage.Read(image).Exe.AsSpan().SequenceEqual(exe))throw new InvalidDataException("Global flight executable readback changed.");
        });
    }
}
