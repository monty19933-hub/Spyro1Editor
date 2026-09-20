using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using static Cases;

internal static class SavedLockedChestCompilationCases
{
    // Saved Wizard/Grass IDs differ from the old CLI composition's fff/111.
    // IDs are authorship, not a source/native-image admission discriminator.
    internal const string WizardId = "11111111111111111111111111111111";
    internal const string GrassId = "22222222222222222222222222222222";
    internal const string ChestId = "33333333333333333333333333333333";
    internal const string ExpectedImage = "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50";
    internal static NativeFreshLevelDocumentIntent Document()
    {
        var old = SavedFamilyDocumentFixture.Document();
        return NativeFreshLevelDocumentIntent.Create(Floor(), []).WithFloorMaterials(old.FloorMaterials!).UpgradeForFamilies()
            .WithPlacements([
                NativeFreshWizardRouteIntent.Create(WizardId,9984,9984,8704,9472,9984),
                NativeFreshMobyPlacementIntent.Create(GrassId,((NativeFreshMobyPlacementIntent)old.Placements[1]).Source,
                    9152,9152,8704,64),
                NativeFreshLockedChestFamilyPlacementIntent.Create(ChestId,
                    NativeFreshLockedChestFamilyReference.Create(Variant,"peace-keepers-locked-chest-v1"),
                    9728,9120,8704,10864,9888,8704,Overrides())]);
    }
    private static NativeFreshChestRewardDestinationOverrides Overrides(NativeFreshChestRewardRole role=NativeFreshChestRewardRole.Reward2,
        int x=10454,int y=10503,int z=9216) => NativeFreshChestRewardDestinationOverrides.Create([new(role,x,y,z)]);
    private static NativeId65FreshDocumentIntent Floor(int x=8448,int y=8448,int z=8704,int width=3072,int depth=3072,
        int spawnX=8960,int spawnY=8960,int heading=64,int track=19,ColorRgba? floor=null,ColorRgba? sky=null) =>
        NativeId65FreshDocumentIntent.Create(x,y,z,width,depth,spawnX,spawnY,heading,floor??new(80,128,112),sky??new(128,96,64),track);

    internal static IReadOnlyList<(string Name,NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var valid=Document();var wizard=(NativeFreshWizardRouteIntent)valid.Placements[0];
        var grass=(NativeFreshMobyPlacementIntent)valid.Placements[1];var chest=(NativeFreshLockedChestFamilyPlacementIntent)valid.Placements[2];
        var tiled=NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create([
            NativeFreshFlatTileIntent.Create("77777777777777777777777777777777",8448,8448,8704,1536,2048,new(80,128,112),valid.FloorMaterials!),
            NativeFreshFlatTileIntent.Create("88888888888888888888888888888888",9984,8448,8704,1536,2048,new(80,128,112),valid.FloorMaterials!)],
            8960,8960,9216,64,new(128,96,64),19),[]).UpgradeForFamilies().WithPlacements(valid.Placements);
        NativeFreshLevelDocumentIntent ReplaceChest(NativeFreshLockedChestFamilyPlacementIntent value)=>valid.WithPlacements([wizard,grass,value]);
        var values=new List<(string,NativeFreshLevelDocumentIntent)>{
            ("schema3",SavedFamilyDocumentFixture.Document()),
            ("tiled",tiled),
            ("empty",valid.WithPlacements([])),("no-chest",valid.WithPlacements([wizard,grass])),
            ("no-grass",valid.WithPlacements([wizard,chest])),("order",valid.WithPlacements([wizard,chest,grass])),
            ("second-family",valid.WithPlacements([wizard,grass,chest,NativeFreshLockedChestFamilyPlacementIntent.Create(
                "88888888888888888888888888888888",chest.Source,9728,9120,8704,10864,9888,8704,chest.RewardDestinations)])),
            ("default-destinations",ReplaceChest(chest.WithRewardDestinations(null))),
            ("wrong-child",ReplaceChest(chest.WithRewardDestinations(Overrides(NativeFreshChestRewardRole.Reward4)))),
            ("destination-x",ReplaceChest(chest.WithRewardDestinations(Overrides(x:10455)))),
            ("destination-y",ReplaceChest(chest.WithRewardDestinations(Overrides(y:10504)))),
            ("destination-z",ReplaceChest(chest.WithRewardDestinations(Overrides(z:9215)))),
            ("extra-destination",ReplaceChest(chest.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create([
                new(NativeFreshChestRewardRole.Reward2,10454,10503,9216),new(NativeFreshChestRewardRole.Reward4,10454,10503,9216)])))),
            ("key-x",ReplaceChest(chest.WithAnchors(9729,9120,8704,10864,9888,8704))),
            ("key-y",ReplaceChest(chest.WithAnchors(9728,9121,8704,10864,9888,8704))),
            ("key-z",ReplaceChest(chest.WithAnchors(9728,9120,8705,10864,9888,8704))),
            ("chest-x",ReplaceChest(chest.WithAnchors(9728,9120,8704,10865,9888,8704))),
            ("chest-y",ReplaceChest(chest.WithAnchors(9728,9120,8704,10864,9889,8704))),
            ("chest-z",ReplaceChest(chest.WithAnchors(9728,9120,8704,10864,9888,8705))),
            ("materials-order",valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(70,1),Retail(14,1)))),
            ("material-source",valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(14,1),Retail(14,1)))),
            ("material-id",valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(14,0),Retail(70,1)))),
            ("material-base",valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),Retail(70,1)))),
            ("no-materials",NativeFreshLevelDocumentIntent.Create(valid.Base,[]).UpgradeForFamilies().WithPlacements(valid.Placements)),
            ("wizard-origin",valid.WithPlacements([NativeFreshWizardRouteIntent.Create(WizardId,10000,9984,8704,9472,9984),grass,chest])),
            ("wizard-z",valid.WithPlacements([NativeFreshWizardRouteIntent.Create(WizardId,9984,9984,8705,9472,9984),grass,chest])),
            ("wizard-end",valid.WithPlacements([NativeFreshWizardRouteIntent.Create(WizardId,9984,9984,8704,9456,9984),grass,chest]))};
        foreach(var (name,basis) in new[]{("floor-x",Floor(x:8464)),("floor-y",Floor(y:8464)),("floor-z",Floor(z:8720)),
            ("width",Floor(width:3056)),("depth",Floor(depth:3056)),("spawn-x",Floor(spawnX:8976)),("spawn-y",Floor(spawnY:8976)),
            ("heading",Floor(heading:65)),("track",Floor(track:20)),("floor-color",Floor(floor:new(80,129,112))),
            ("sky-color",Floor(sky:new(128,96,65)))})values.Add((name,valid.WithContent(basis,valid.Placements)));
        foreach(var (name,entry,index,cls,x,y,z,yaw) in new[]{("grass-source",14,121,501,9152,9152,8704,64),
            ("grass-index",10,122,501,9152,9152,8704,64),("grass-class",10,121,500,9152,9152,8704,64),
            ("grass-x",10,121,501,9153,9152,8704,64),("grass-y",10,121,501,9152,9153,8704,64),
            ("grass-z",10,121,501,9152,9152,8705,64),("grass-yaw",10,121,501,9152,9152,8704,65)})
            values.Add((name,valid.WithPlacements([wizard,NativeFreshMobyPlacementIntent.Create(GrassId,
                NativeFreshRetailMobyReference.Create(Variant,entry,index,cls),x,y,z,yaw),chest])));
        return values;
    }

    internal static async Task FreeAsync()
    {
        Type type=typeof(NativeFreshSavedLockedChestFamilyCompilation);
        Check(type.IsNotPublic&&type.IsSealed&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,
            "Saved chest compilation is a closed internal source-issued owner, not a caller-byte constructor.");
        var document=Document();byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        var projection=NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(document);
        Check(projection.SchemaVersion==2&&projection.Placements.Count==1&&ReferenceEquals(projection.Base,document.Base)&&
            ReferenceEquals(projection.FloorMaterials,document.FloorMaterials)&&ReferenceEquals(projection.Placements[0],document.Placements[0])&&
            NativeFreshLevelDocumentCodec.SerializeCanonical(document).SequenceEqual(canonical),
            "Exact whole-profile preflight yields a subordinate schema2 producer with actual Base/material/Wizard owners and no saved mutation.");
        await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureMixedBodyAsync("\0",projection,CancellationToken.None));
        await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSameLayoutMixedBodyAsync("\0",document,CancellationToken.None));
        await Throws<ArgumentNullException>(()=>{NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(null!);return Task.CompletedTask;});
        await Throws<ArgumentNullException>(()=>NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync("\0",null!));
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Throws<OperationCanceledException>(()=>NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync("\0",null!,cancel.Token));}
        foreach(var (name,invalid) in Unsupported())
        {
            await Throws<InvalidDataException>(()=>{NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(invalid);return Task.CompletedTask;});
            var (_,saved)=Save("chest-preflight-"+name,invalid);
            await Throws<InvalidDataException>(()=>NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync("\0",saved));
        }
        foreach(string owner in new[]{"raw","manifest"})
        {
            var (project,saved)=Save("chest-preflight-stale-"+owner,document);
            File.AppendAllText(owner=="raw"?saved.Snapshot.Path:project.ManifestPath," ");
            await Throws<InvalidDataException>(()=>NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync("\0",saved));
        }
        // These remain the old, separately named supported neutral profiles.
        Check(NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(SavedFamilyDocumentFixture.Document())&&
            NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(MixedSavedCases.Document())&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(document),
            "New exact chest compilation does not weaken legacy mixed or Wizard/Grass profile predicates.");
        Console.WriteLine($"SAVED_LOCKED_CHEST_COMPILATION_FREE_OK checks={Checks} sourceFree=true");
    }

    internal static async Task SourceAsync(string bin,string cue)
    {
        _=cue; // CUE/publication belongs to the separate outer Candidate, not this source compilation.
        var (_,saved)=Save("locked-chest-compilation",Document());
        var compilation=await NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync(bin,saved);
        await VerifySourceAsync(bin,compilation);
        File.AppendAllText(saved.Snapshot.Path," ");
        await Throws<InvalidDataException>(()=>NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync("\0",saved));
    }

    // Candidate source tests can reuse their actual compilation rather than
    // recapturing every source owner. This verification does not edit its saved files.
    internal static async Task VerifySourceAsync(string bin,NativeFreshSavedLockedChestFamilyCompilation compilation)
    {
        var saved=compilation.Saved;
        compilation.RequireAncestry(saved);
        var actual=saved.Document;var family=(NativeFreshLockedChestFamilyPlacementIntent)actual.Placements[2];
        Check(ReferenceEquals(compilation.Saved,saved)&&ReferenceEquals(compilation.Document,actual)&&
            ReferenceEquals(compilation.BaseDocument.Base,actual.Base)&&ReferenceEquals(compilation.BaseDocument.Placements[0],actual.Placements[0])&&
            ReferenceEquals(compilation.BaseDocument.FloorMaterials,actual.FloorMaterials)&&
            ReferenceEquals(compilation.Placement.RewardDestinationsAncestor,family.RewardDestinations),
            "Source chain retains actual saved context, full document and all projected/override owners without reparsing substitutions.");
        Check(compilation.Composition.Placements.Select(p=>p.EditorId).SequenceEqual(actual.Placements.Take(2).Select(p=>p.EditorId))&&
            compilation.Scene.RowSpans.Placements.Select(p=>p.EditorId).SequenceEqual(actual.Placements.Select(p=>p.EditorId))&&
            compilation.Scene.RowSpans.Placements.Select(p=>p.RowCount).SequenceEqual(new[]{1,1,7})&&
            compilation.Scene.Layout.PlacedCount==9&&compilation.Scene.Layout.DynamicCapacity==32,
            "Actual three authored IDs expand in saved order to1+1+7 native rows, not three native rows or CLI identity aliases.");
        compilation.ChestSource.RequireWizardOriginalOwner(compilation.BaseBody.HostAncestor.SceneAncestor.FamilyAncestor);
        compilation.Placement.RequireRewardDestinationsAncestry(family.RewardDestinations!);
        compilation.Clearance.RequireStaticLandingClearance(compilation.Placement);
        compilation.Executable.RequireAncestry(compilation.Placement,compilation.Host);
        compilation.Disc.RequireAncestry(compilation.Executable);
        Check(compilation.Placement.SceneSha256=="4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8"&&
            compilation.Placement.LevelDataSha256=="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"&&
            compilation.Executable.ExecutableSha256=="81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431",
            "Saved IDs and actual schema5 ownership preserve the retained R03 native Scene/Body/EXE regression bytes, not plan hashes.");
        using(var source=File.OpenRead(bin))
        {
            RequireOriginal(source);source.Position=0;
            var measured=NativeId65FreshDiscStreaming.Measure(compilation.Disc,source,CancellationToken.None);
            Check(measured.OutputSha256==ExpectedImage&&measured.ChangedSectors==1297,
                "Whole source-index transformation matches the retained, runtime-observed R03 full image without writing an artifact.");
            RequireOriginal(source);
        }
        Check(compilation.Placement.RevertExactLevelData(compilation.Placement.CopyLevelData()).SequenceEqual(compilation.Body.CopyLevelData())&&
            compilation.Body.RevertExactLevelData(compilation.Body.CopyLevelData()).SequenceEqual(compilation.Resources.CopyLevelData())&&
            compilation.Resources.CopyInverseResourceAncestorBody().SequenceEqual(compilation.Composition.CopyLevelData())&&
            !compilation.RuntimeVerified&&!compilation.PublicationApproved&&!compilation.CompleteNativeDependencyClosureProven&&
            !compilation.KeyZeroContributionProven&&!compilation.Executable.CompleteStaticCensusProven,
            "Full immediate body inverses and unchanged experimental/runtime/public boundaries remain explicit.");
        byte[] copied=compilation.CopyCanonicalDocument();Check(copied.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(actual))&&
            compilation.CanonicalDocumentSha256==Hash(copied),"Retained complete canonical document includes actual saved family destinations.");
        copied[0]^=1;Check(!copied.SequenceEqual(compilation.CopyCanonicalDocument()),"Canonical copy is detached.");
        var equalContext=NativeFreshSavedLevelDocumentReadContext.Capture(saved.Snapshot);
        Check(ReferenceEquals(equalContext.Document,actual),"Equal-context negative keeps exact document to isolate actual context identity.");
        await Throws<InvalidDataException>(()=>{compilation.RequireAncestry(equalContext);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{compilation.Placement.RequireRewardDestinationsAncestry(Overrides());return Task.CompletedTask;});
        var equalDocument=NativeFreshLevelDocumentCodec.Parse(compilation.CopyCanonicalDocument());
        Check(!ReferenceEquals(equalDocument,actual),"Equal canonical document is separately issued.");
        await Throws<InvalidDataException>(()=>{saved.RequireDocument(equalDocument);return Task.CompletedTask;});
        Console.WriteLine($"SAVED_LOCKED_CHEST_COMPILATION_SOURCE_OK checks={Checks} image={ExpectedImage} runtime=false publication=false");
    }
}
