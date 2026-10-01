using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class WrittenModelLookupCases
{
    internal static void Run(UniversalSourceLibrary library,LevelCatalog catalog,string output,Action<bool,string> check)
    {
        if(File.Exists(output))throw new InvalidOperationException("Refuse to replace an existing fixture.");
        string originalHash;
        using(var source=File.OpenRead(library.ImagePath))originalHash=Convert.ToHexStringLower(SHA256.HashData(source));
        var donor=library.Get("icecavern");var destination=library.Get("artisans");var row=donor.Data.Row(2);
        ushort sourceClass=BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
        var placement=new UniversalMobyPlacement("icecavern",2,BitConverter.ToInt32(row,0x0C),BitConverter.ToInt32(row,0x10),BitConverter.ToInt32(row,0x14),EditorId:"class-owner-ice2",PersistentSlot:0);
        var result=UniversalMobyExport.Apply(library,library.ImagePath,output,catalog,
            new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"artisans",new[]{placement}}});
        var composed=result.Levels.Single().Compose;
        var imported=composed.Classes.Single(c=>!c.DependencyOnly&&c.SourceLevelKey=="icecavern"&&c.ClassId==sourceClass);
        check(imported.DestinationClassId!=sourceClass,"written fixture exercises a genuine source namespace collision");
        var disc=UniversalDiscImage.Read(output);var data=new UniversalLevelData(disc.EntryBytes(destination.Level.SourceWadEntry));
        check(data.Scene().RowCount==destination.Data.Scene().RowCount+1,"one distinct new physical row is written");
        check(BinaryPrimitives.ReadUInt16LittleEndian(data.Row(composed.NewRowIndices[0]).AsSpan(0x36))==imported.DestinationClassId,"row readback uses the mapped destination class");
        check(data.Registrations().Any(r=>r.ClassId==imported.DestinationClassId),"mapped native model-table registration survives disk readback");
        check(destination.Data.Registrations().Any(r=>r.ClassId==sourceClass)
            ? data.Registrations().Any(r=>r.ClassId==sourceClass)
            : library.SharedClasses.Contains(sourceClass)&&disc.EntryBytes(8).SequenceEqual(library.Disc.EntryBytes(8)),
            "original shared/native destination class remains registered without a PETE rewrite");
        check(disc.EntryBytes(destination.Level.SourceWadEntry-1).SequenceEqual(composed.Overlay),"written relocated behavior matches the exact assembled bundle");
        byte[] owner=Enumerable.Range(0,6).SelectMany(n=>BitConverter.GetBytes(donor.Overlay.Word(0x8007D3B0+(uint)n*4))).ToArray();
        byte[] appended=composed.Overlay.AsSpan(destination.Overlay.Bytes.Length).ToArray();
        check(Enumerable.Range(0,Math.Max(0,appended.Length-owner.Length+1)).Any(at=>appended.AsSpan(at,owner.Length).SequenceEqual(owner)),"source-owned model lookup retains every instruction and destination model index");
        check(composed.Ram.FinalHeadroom>=UniversalMobyComposer.RamSafetyMargin,"final written RAM safety margin is unchanged");
        string afterHash;using(var source=File.OpenRead(library.ImagePath))afterHash=Convert.ToHexStringLower(SHA256.HashData(source));
        check(originalHash==afterHash&&originalHash==UniversalSourceLibrary.OriginalUsaSha256,"original source disc is unchanged");
        File.WriteAllText(Path.ChangeExtension(output,".readback.json"),JsonSerializer.Serialize(new{Scope="written disc/native class remap/model-lookup instruction readback; gameplay not tested",
            result.OutputImageSha256,ImplementationModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId,
            CoreModuleSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location))),
            SourceImageSha256=afterHash,SourceClassId=sourceClass,imported.DestinationClassId,imported.ModelOrigin,
            composed.NewRowIndices,composed.PlacementSaveRows,composed.Ram,composed.ParticleDependencies,RuntimeStatus="not-tested"},new JsonSerializerOptions{WriteIndented=true}));
    }
}
