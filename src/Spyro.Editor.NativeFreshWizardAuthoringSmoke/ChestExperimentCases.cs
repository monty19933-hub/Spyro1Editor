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
    internal static bool Matches(string[] args)=>args.Length>0&&args[0] is "--chest-source" or "--chest-experiment" or
        "--chest-wide-source" or "--chest-wide-experiment" or "--chest-r03-source" or "--chest-r03-experiment" or "--tiled-family-experiment";
    internal static async Task RunAsync(string[] args)
    {
        bool tiled=args.Length>0&&args[0]=="--tiled-family-experiment";
        bool write=tiled||args.Length>0&&args[0] is "--chest-experiment" or "--chest-wide-experiment" or "--chest-r03-experiment";
        bool authored=tiled||args.Length>0&&args[0] is "--chest-r03-source" or "--chest-r03-experiment";
        bool wide=authored||args.Length>0&&args[0] is "--chest-wide-source" or "--chest-wide-experiment";
        string stem=tiled?"fresh-tiled-family-p9-experiment-r01":authored?"fresh-chest-p9-experiment-r03":wide?"fresh-chest-p9-experiment-r02":"fresh-chest-p9-experiment-r01";
        if(!Matches(args)||args.Length!=(write?4:3)||args.Skip(1).Any(a=>string.IsNullOrWhiteSpace(a)||a.StartsWith("--",StringComparison.Ordinal)))
            throw new ArgumentException("Usage: --chest-source|--chest-wide-source|--chest-r03-source ORIGINAL_BIN ORIGINAL_CUE | --chest-r03-experiment|--tiled-family-experiment ORIGINAL_BIN ORIGINAL_CUE NEW_DIRECTORY (R01/R02 write recipes are retired)");
        // R02 passes the bounded static clearance assessment but its observed
        // final red gem escaped the finite floor. Retire only this writer;
        // retain its exact source recipe without manufacturing a Core flag.
        if(write&&wide&&!authored)
            throw new InvalidDataException("R02 artifact creation is retired after a known escaped reward and incomplete treasure collection. Use --chest-wide-source for exact source-only reproduction; no source was opened or output directory created.");
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
            if(wide)await SameLayoutMaterialCases.Source(bin,Check);
            int extent=wide?3072:2048;
            var floor=NativeId65FreshDocumentIntent.Create(8448,8448,8704,extent,extent,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var route=NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,8704,9472,9984);
            var document=NativeFreshLevelDocumentIntent.Create(floor,[route]).WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",14,1),
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",70,1)));
            var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(bin,floor);
            var wizard=await NativeWizardFamilyAssetPlan.CaptureAsync(bin);
            var resources=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin,compilation,wizard);
            var scene=wide?await NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(bin,document,compilation,wizard,resources):
                await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(bin,document,compilation,wizard,resources);
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(bin,resources);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,scene,fallback);
            var material0=NativeStaticTerrainTextureSource.Capture(bin,14,1);
            var material1=NativeStaticTerrainTextureSource.Capture(bin,70,1);
            var mixed=wide?await NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(bin,host,material0,material1):
                await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,host,material0,material1);
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
            await LockedChestSourceCaptureCases.Source(bin,wizard,current,chestSource,child,capturedFallback,Check);
            var chestScene=NativeFreshLockedChestSceneExtension.Compose(child,chestHost,"33333333333333333333333333333333");
            var body=NativeFreshLockedChestResearchBody.Compose(child,chestHost,chestScene);
            if(wide)WideChestPlacementCases.Source(body,Check);
            else FamilyChestPlacementCases.Source(body,Check);
            var placement=authored?AuthoredChestDestinationCases.Source(body,Check):
                NativeFreshLockedChestPlacementPlan.Compose(body,wide?new(9728,9120,8704,10864,9888,8704):
                new(8960,9728,8704,9472,8960,8704));
            // Existing R03 writers retain their old Body. Only the explicit
            // tiled experiment may carry the new typed composition to a file.
            NativeFreshTiledFamilyBodyPlan? tiledBody=null;
            if(authored&&(!write||tiled))
            {
                var terrain=TiledFamilyTerrainRevisionCases.Source(placement,Check);
                tiledBody=TiledFamilyBodyCases.Source(terrain,Check);
            }
            var executable=NativeFreshLockedChestExperimentalExecutable.Compose(placement,chestHost);
            var tiledExecutable=tiledBody is null?null:TiledFamilyExecutableCases.Source(tiledBody,executable,Check);
            var clearance=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
            var tiledClearance=tiledExecutable is null?null:TiledFamilyClearanceCases.Source(tiledExecutable,clearance,Check);
            NativeFreshTiledFamilyDiscPlan? tiledDisc=null;
            if(write)
            {
                // Physical correctness never overrides a known unsafe spawn.
                // Keep the failed R01 source fixture reproducible in memory,
                // but refuse artifact creation before creating its directory.
                clearance.RequireStaticLandingClearance(placement);
                if(tiled)(tiledClearance??throw new InvalidDataException("Missing actual tiled union clearance."))
                    .RequireStaticLandingClearance(tiledExecutable!,clearance);
            }
            if(authored)
            {
                var parentDisc=await FamilyChestDiscCases.SourceAuthoredDestinations(bin,executable,Check);
                if(tiledExecutable is not null)await TiledFamilyDiscCases.Source(bin,tiledExecutable,parentDisc,Check);
                if(tiled)tiledDisc=NativeFreshTiledFamilyDiscPlan.Compose(tiledExecutable!,parentDisc);
            }
            else if(wide)await FamilyChestDiscCases.SourceSameLayout(bin,executable,Check);
            else await FamilyChestDiscCases.Source(bin,executable,Check);
            if(write)
            {
                var disc=tiledDisc?.ParentDiscAncestor??await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(bin,executable);
                if(tiled&&(tiledDisc is null||tiledClearance is null||
                    !ReferenceEquals(tiledDisc.ExecutableAncestor,tiledClearance.ExecutableAncestor)))
                    throw new InvalidDataException("Tiled artifact requires the same actual physical and strict union-clearance owners.");
                // New private directory, CreateNew files only, never a retail or
                // normal editor project destination. Failures retain evidence.
                if(Directory.Exists(output)||File.Exists(output))throw new IOException("Experiment destination appeared during source work.");
                Directory.CreateDirectory(output!);
                string imagePath=Path.Combine(output!,stem+".bin");
                NativeId65FreshDiscVerification written;
                using(var source=File.OpenRead(bin))
                using(var sink=new FileStream(imagePath,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                {
                    written=tiledDisc is null?NativeId65FreshDiscStreaming.Measure(disc,source,CancellationToken.None,sink):
                        NativeId65FreshDiscStreaming.Measure(tiledDisc,source,CancellationToken.None,sink);
                    sink.Flush(true);source.Position=0;sink.Position=0;
                    var verified=tiledDisc is null?disc.VerifyImage(source,sink):tiledDisc.VerifyImage(source,sink);
                    Check(verified==written&&written.ChangedSectors==1297,"Complete actual output readback agrees with streamed source-index transformation");
                    if(tiled)Check(written.OutputSha256=="689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192",
                        "Complete actual tiled image equals independently frozen physical child reference before receipt or CUE creation");
                }
                // Reopen the actual candidate and execute its complete inverse,
                // without constructing a second output or trusting a receipt.
                using(var candidate=File.OpenRead(imagePath))
                using(var restored=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    byte[] raw=new byte[2352];
                    for(int lba=0;lba<281270;lba++)
                    {
                        candidate.ReadExactly(raw);
                        restored.AppendData(tiledDisc is null?disc.CreateOriginalRawSector(lba,raw):tiledDisc.CreateOriginalRawSector(lba,raw));
                    }
                    Check(candidate.ReadByte()==-1&&Convert.ToHexStringLower(restored.GetHashAndReset())==Original,"Complete actual candidate inverse restores Original");
                }
                // Check source owners BEFORE making a bootable companion. The
                // final independent checks below are attempted even on failure.
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None);
                using(var source=File.OpenRead(bin))await NativeId65FreshDiscSource.RequireWholeOriginalAsync(source,CancellationToken.None);
                Check(File.ReadAllBytes(cue).SequenceEqual(cueBytes),"Original CUE unchanged before companion creation");
                if(tiledDisc is not null)
                {
                    byte[] canonical=tiledExecutable!.TerrainAncestor.IntentAncestor.CopyCanonicalDocument();
                    Check(Hash(canonical)==tiledDisc.CanonicalDocumentSha256,"Complete authored tiled document matches the physical owner");
                    WriteNew(Path.Combine(output!,stem+".document.json"),canonical);
                    WriteNew(Path.Combine(output!,stem+".receipt.json"),JsonSerializer.SerializeToUtf8Bytes(new{
                        profile="private-tiled-family-p9-runtime-experiment-v1",sourceImage=Original,sourceCue=Cue,
                        tiledDisc.PlanSha256,tiledDisc.CanonicalDocumentSha256,tiledDisc.BodySha256,tiledDisc.ExecutableSha256,
                        overlaySha256=tiledDisc.OverlaySha256,sceneSha256=tiledBody!.SceneSha256,
                        written.OutputSha256,written.ChangedSectors,staticLandingClearance=tiledClearance!.PlanSha256,
                        tiledDisc.ExperimentalTreasureTarget,tiledDisc.ExperimentalKeyZeroAssumption,
                        tiledDisc.CompleteStaticCensusProven,tiledDisc.CompleteNativeDependencyClosureProven,
                        tiledDisc.FullNativeLoaderProven,tiledDisc.PeakRuntimeMemoryProven,tiledDisc.NormalCreateBinEnabled,
                        runtimeVerified=false,publicationApproved=false,savedEditorPublication=false,
                        fullOriginalOnlyPayloadOracle=false,childRelativePayloadEvidence=true,
                        warning="Private four-tile Wizard/Grass/chest experiment. Strict initial/fixed-XY clearance is not route, lifecycle, peak-fit or gameplay approval."
                    },new JsonSerializerOptions{WriteIndented=true}));
                }
                else WriteNew(Path.Combine(output!,stem+".receipt.json"),JsonSerializer.SerializeToUtf8Bytes(new{
                    profile=authored?"private-fresh-chest-p9-runtime-experiment-v3":wide?"private-fresh-chest-p9-runtime-experiment-v2":"private-fresh-chest-p9-runtime-experiment-v1",sourceImage=Original,sourceCue=Cue,
                    disc.PlanSha256,placement.SceneSha256,placement.LevelDataSha256,executable.ExecutableSha256,
                    overlaySha256=chestHost.OverlaySha256,written.OutputSha256,written.ChangedSectors,
                    executable.KnownDeterministicSelectorSubtotal,executable.ExperimentalTreasureTarget,
                    executable.CompleteStaticCensusProven,executable.KeyZeroContributionProven,
                    placement.Intent,placement.MinimumCentreBoundaryMargin,
                    rewardDestinationOverrides=placement.RewardDestinationsAncestor?.Values,
                    staticLandingClearance=clearance.PlanSha256,
                    chestMargins=new[]{clearance.ChestMargins.Left.ToString(),clearance.ChestMargins.Right.ToString(),
                        clearance.ChestMargins.Top.ToString(),clearance.ChestMargins.Bottom.ToString()},
                    playerMargins=new[]{clearance.PlayerMargins.Left.ToString(),clearance.PlayerMargins.Right.ToString(),
                        clearance.PlayerMargins.Top.ToString(),clearance.PlayerMargins.Bottom.ToString()},
                    placement.FootprintClearanceProven,runtimeVerified=false,publicationApproved=false,
                    fullOriginalOnlyPayloadOracle=false,childRelativePayloadEvidence=true,
                    warning="Experimental key-zero assumption; complete native chest lifecycle and coexistence remain unverified. Not an editor release."
                },new JsonSerializerOptions{WriteIndented=true}));
                WriteNew(Path.Combine(output!,stem+".cue"),Encoding.UTF8.GetBytes($"FILE \"{stem}.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n"));
                Console.WriteLine($"{(tiled?"FRESH_TILED_FAMILY_EXPERIMENT_WRITTEN":"FRESH_CHEST_EXPERIMENT_WRITTEN")} directory={output} image={written.OutputSha256} runtime=false publication=false");
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
