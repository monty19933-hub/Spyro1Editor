using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Explicit opt-in experimental artifact only. Ordinary source/free smoke and
// every editor publication API remain read-only/unchanged by this entry point.
internal static class ChestExperimentCases
{
    private const string Original="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string Cue="05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    private const string Stem="fresh-chest-p9-experiment-r01";
    internal static bool Matches(string[] args)=>args.Length>0&&args[0] is "--chest-source" or "--chest-experiment";
    internal static async Task RunAsync(string[] args)
    {
        bool write=args.Length>0&&args[0]=="--chest-experiment";
        if(!Matches(args)||args.Length!=(write?4:3)||args.Skip(1).Any(a=>string.IsNullOrWhiteSpace(a)||a.StartsWith("--",StringComparison.Ordinal)))
            throw new ArgumentException("Usage: --chest-source ORIGINAL_BIN ORIGINAL_CUE | --chest-experiment ORIGINAL_BIN ORIGINAL_CUE NEW_DIRECTORY");
        string bin=Path.GetFullPath(args[1]),cue=Path.GetFullPath(args[2]);
        string? output=write?Path.GetFullPath(args[3]):null;
        if(bin==cue||output is not null&&(File.Exists(output)||Directory.Exists(output)))
            throw new InvalidDataException("Experiment requires distinct original files and a never-existing output directory.");
        int checks=0;List<Exception> failures=[];
        await using var held=new FileStream(bin,FileMode.Open,FileAccess.Read,FileShare.Read);
        byte[] cueBytes=File.ReadAllBytes(cue);
        try
        {
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None);
            Check(Hash(cueBytes)==Cue,"Exact original CUE");
            NativeId65FreshWizardResearchCandidatePlan.ValidateSourceCue(cueBytes,cue,bin);
            var floor=NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var route=NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,8704,9472,9984);
            var document=NativeFreshLevelDocumentIntent.Create(floor,[route]).WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",14,1),
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",70,1)));
            var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(bin,floor);
            var wizard=await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
            var resources=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin,compilation,wizard);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(bin,document,compilation,wizard,resources);
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin,resources);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,scene,fallback);
            var mixed=await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,host,
                NativeStaticTerrainTextureSource.Capture(bin,14,1),NativeStaticTerrainTextureSource.Capture(bin,70,1));
            var baseBody=await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(bin,mixed);
            var grass=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(bin);
            var family=NativeFreshMobyFamilyCompositionPlan.Compose(baseBody,[
                NativeFreshMobyFamilyPlacement.RetainedWizard("ffffffffffffffffffffffffffffffff",wizard),
                NativeFreshMobyFamilyPlacement.Passive("11111111111111111111111111111111",grass,
                    NativeMobyFamilyPassiveHostBinding.Bind(host,grass),9152,9152,8704,64)]);
            var current=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(bin,family);
            var chestSource=await FamilySoundResidencyCases.CaptureSource(bin,wizard);
            var chestHost=NativeFreshLockedChestResearchHostExtension.Compose(current,chestSource);
            var sound=NativeFreshMobySoundResidencyBinding.BindLockedChest(chestSource,family);
            var capturedFallback=await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(bin,resources,CancellationToken.None);
            var child=NativeFreshMobyFamilyCompositionPlan.ComposeWithLockedChestResources(family,sound,capturedFallback);
            var chestScene=NativeFreshLockedChestSceneExtension.Compose(child,chestHost,"33333333333333333333333333333333");
            var body=NativeFreshLockedChestResearchBody.Compose(child,chestHost,chestScene);
            FamilyChestPlacementCases.Source(body,Check);
            var placement=NativeFreshLockedChestPlacementPlan.Compose(body,new(8960,9728,8704,9472,8960,8704));
            var executable=NativeFreshLockedChestExperimentalExecutable.Compose(placement,chestHost);
            await FamilyChestDiscCases.Source(bin,executable,Check);
            if(write)
            {
                var disc=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(bin,executable);
                // New private directory, CreateNew files only, never a retail or
                // normal editor project destination. Failures retain evidence.
                if(Directory.Exists(output)||File.Exists(output))throw new IOException("Experiment destination appeared during source work.");
                Directory.CreateDirectory(output!);
                string imagePath=Path.Combine(output!,Stem+".bin");
                NativeId65FreshDiscVerification written;
                using(var source=File.OpenRead(bin))
                using(var sink=new FileStream(imagePath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {
                    written=NativeId65FreshDiscStreaming.Measure(disc,source,CancellationToken.None,sink);
                    sink.Flush(true);source.Position=0;sink.Position=0;
                    var verified=disc.VerifyImage(source,sink);
                    Check(verified==written&&written.ChangedSectors==1297,"Complete actual output readback agrees with streamed source-index transformation");
                }
                // Reopen the actual candidate and execute its complete inverse,
                // without constructing a second output or trusting a receipt.
                using(var candidate=File.OpenRead(imagePath))
                using(var restored=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    byte[] raw=new byte[2352];
                    for(int lba=0;lba<281270;lba++){candidate.ReadExactly(raw);restored.AppendData(disc.CreateOriginalRawSector(lba,raw));}
                    Check(candidate.ReadByte()==-1&&Convert.ToHexStringLower(restored.GetHashAndReset())==Original,"Complete actual candidate inverse restores Original");
                }
                // Check source owners BEFORE making a bootable companion. The
                // final independent checks below are attempted even on failure.
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None);
                using(var source=File.OpenRead(bin))await NativeId65FreshDiscSource.RequireWholeOriginalAsync(source,CancellationToken.None);
                Check(File.ReadAllBytes(cue).SequenceEqual(cueBytes),"Original CUE unchanged before companion creation");
                WriteNew(Path.Combine(output!,Stem+".receipt.json"),JsonSerializer.SerializeToUtf8Bytes(new{
                    profile="private-fresh-chest-p9-runtime-experiment-v1",sourceImage=Original,sourceCue=Cue,
                    disc.PlanSha256,placement.SceneSha256,placement.LevelDataSha256,executable.ExecutableSha256,
                    overlaySha256=chestHost.OverlaySha256,written.OutputSha256,written.ChangedSectors,
                    executable.KnownDeterministicSelectorSubtotal,executable.ExperimentalTreasureTarget,
                    executable.CompleteStaticCensusProven,executable.KeyZeroContributionProven,
                    placement.Intent,placement.MinimumCentreBoundaryMargin,
                    placement.FootprintClearanceProven,runtimeVerified=false,publicationApproved=false,
                    fullOriginalOnlyPayloadOracle=false,childRelativePayloadEvidence=true,
                    warning="Experimental key-zero assumption; complete native chest lifecycle and coexistence remain unverified. Not an editor release."
                },new JsonSerializerOptions{WriteIndented=true}));
                WriteNew(Path.Combine(output!,Stem+".cue"),Encoding.UTF8.GetBytes($"FILE \"{Stem}.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n"));
                Console.WriteLine($"FRESH_CHEST_EXPERIMENT_WRITTEN directory={output} image={written.OutputSha256} runtime=false publication=false");
            }
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            try{await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None);}catch(Exception error){failures.Add(error);}
            try{using var named=File.OpenRead(bin);await NativeId65FreshDiscSource.RequireWholeOriginalAsync(named,CancellationToken.None);}catch(Exception error){failures.Add(error);}
            try{Check(File.ReadAllBytes(cue).SequenceEqual(cueBytes)&&Hash(cueBytes)==Cue,"Final original CUE unchanged");}catch(Exception error){failures.Add(error);}
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        Console.WriteLine($"FRESH_CHEST_EXPERIMENT_FINAL_OK checks={checks} originalAndCueUnchanged=true outputWritten={write}");
        void Check(bool value,string message){if(!value)throw new InvalidDataException(message);checks++;}
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void WriteNew(string path,byte[] bytes)
    {using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes);file.Flush(true);}
}
