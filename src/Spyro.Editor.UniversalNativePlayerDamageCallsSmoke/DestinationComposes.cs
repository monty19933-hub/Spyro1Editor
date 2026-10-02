using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Universal;

internal static class DestinationComposes
{
    internal static int Run(UniversalSourceLibrary library, string reportPath)
    {
        int checks=0;var reports=new List<object>();
        void Check(bool value,string label){checks++;if(!value)throw new InvalidOperationException(label);}
        foreach(var spec in new[]{(Source:"drycanyon",Class:0x138,Destination:"darkhollow"),(Source:"alpineridge",Class:0x138,Destination:"darkhollow"),
            (Source:"highcaves",Class:0x131,Destination:"darkhollow"),(Source:"hauntedtowers",Class:0x138,Destination:"darkhollow"),
            (Source:"loftycastle",Class:0x138,Destination:"darkhollow"),(Source:"gnastysloot",Class:0x138,Destination:"darkhollow"),(Source:"gnorccove",Class:0x9A,Destination:"darkhollow")})
        {
            var source=library.Get(spec.Source);var destination=library.Get(spec.Destination);
            int row=Enumerable.Range(0,source.Data.Scene().RowCount).First(i=>BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(i).AsSpan(0x36))==spec.Class);
            var original=source.Data.Row(row);var placement=new UniversalMobyPlacement(spec.Source,row,BitConverter.ToInt32(original,12),BitConverter.ToInt32(original,16),BitConverter.ToInt32(original,20));
            var compose=UniversalMobyComposer.Compose(destination.Level,destination.Data.Bytes,destination.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,
                UniversalMobyExport.DefaultProtectedVram(library.ImagePath,destination.Level),[placement],sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
            var behavior=compose.Behavior.Single(b=>b.SourceLevelKey==spec.Source);
            Check(behavior.NativePlayerDamageCalls.Count>0,"Actual destination compose records retained source sphere calls");
            uint mask=behavior.NativePlayerDamageCalls.Aggregate(0u,(m,p)=>m|p.DamageMask);
            Check((behavior.PlayerDamageMask&mask)==mask,"Native call mask survives behavior report union");
            var reactions=UniversalPlayerDamageDependencies.MapDamageMask(mask,library.Disc.Exe);
            if(spec.Source=="gnorccove") Check(mask==0xC0096 && reactions.SequenceEqual(new[]{13,15,26,29,32}),"Hatch retains the previously certified CA explosion reaction mask");
            Check(reactions.All(id=>compose.PlayerAnimationImports.SelectMany(p=>p.RequestedAnimationIds).Contains(id)),"Native damage reactions travel through actual source-owned animation merger");
            var readback=new UniversalLevelData(compose.LevelData);int physical=compose.NewRowIndices.Single();
            Check(readback.Row(physical).AsSpan(12,12).SequenceEqual(original.AsSpan(12,12)),"Independent output readback preserves placement position");
            int writtenClass=BinaryPrimitives.ReadUInt16LittleEndian(readback.Row(physical).AsSpan(0x36));
            Check( compose.Classes.Any(c=>c.ClassId==spec.Class&&c.DestinationClassId==writtenClass),"Physical output class matches its authentic source import");
            var newOverlay=new UniversalOverlay(compose.Overlay,UniversalExeBindings.Read(compose.Executable,destination.Level.LevelId));
            Check(newOverlay.Update!=destination.Overlay.Update,"Independent emitted callback readback points to imported behavior");
            reports.Add(new{spec.Source,spec.Class,sourceRow=row,spec.Destination,physical,writtenClass,mask,reactions,calls=behavior.NativePlayerDamageCalls,imports=compose.PlayerAnimationImports,scope="composed and independent LevelData/callback readback; runtime not tested"});
            Console.WriteLine($"NATIVE_DAMAGE_COMPOSE_OK source={spec.Source} destination={spec.Destination} mask={mask:X8} reactions={string.Join(',',reactions)}");
        }
        File.WriteAllText(reportPath,JsonSerializer.Serialize(new{checks,reports},new JsonSerializerOptions{WriteIndented=true}));return checks;
    }
}
