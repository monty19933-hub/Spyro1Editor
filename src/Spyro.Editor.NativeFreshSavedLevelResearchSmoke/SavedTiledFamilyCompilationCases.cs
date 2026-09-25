using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

// Tests first: this compiler binds a saved aggregate to the existing closed
// source chain. It does not change Candidate, publication or UI admission.
internal static class SavedTiledFamilyCompilationCases
{
    internal const string ExpectedImage="689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192";
    internal static NativeFreshLevelDocumentIntent Document()
    {
        var legacy=SavedLockedChestCompilationCases.Document();
        var a=Retail(14,1);var b=Retail(70,1);
        var aa=NativeFreshFloorMaterialsIntent.Create(a,a);
        var ab=NativeFreshFloorMaterialsIntent.Create(a,b);
        var bb=NativeFreshFloorMaterialsIntent.Create(b,b);
        return NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create([
            NativeFreshFlatTileIntent.Create(new('4',32),8448,8448,8704,1536,1536,new(80,128,112),aa),
            NativeFreshFlatTileIntent.Create(new('5',32),9984,8448,8704,1536,1536,new(80,128,112),ab),
            NativeFreshFlatTileIntent.Create(new('6',32),8448,9984,8704,1536,1536,new(80,128,112),ab),
            NativeFreshFlatTileIntent.Create(new('7',32),9984,9984,8704,1536,1536,new(80,128,112),bb)],
            8960,8960,9216,64,new(128,96,64),19),[]).UpgradeForFamilies().WithPlacements(legacy.Placements);
    }

    internal static async Task FreeAsync()
    {
        var type=typeof(NativeFreshSavedTiledFamilyCompilation);
        Check(type.IsSealed&&!type.IsPublic&&
            type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.DeclaredOnly).Length==0,
            "Saved tiled compiler is a closed internal source issuer, not public byte/domain/callback ingress.");
        var capture=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Single(m=>m.Name=="CaptureAsync");
        Check(capture.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
            new[]{typeof(string),typeof(NativeFreshSavedLevelDocumentReadContext),typeof(CancellationToken)}),
            "Capture requires the actual saved context rather than a caller document or source plan.");
        var document=Document();byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        var projection=NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(document);
        var intent=NativeFreshTiledFamilyTerrainIntent.Create(document);
        Check(projection.SchemaVersion==2&&projection.TiledContent is null&&projection.Placements.Count==1&&
            ReferenceEquals(projection.Placements[0],document.Placements[0])&&
            ReferenceEquals(projection.FloorMaterials!.Triangle0,intent.MaterialSelections[0])&&
            ReferenceEquals(projection.FloorMaterials.Triangle1,intent.MaterialSelections[1])&&
            projection.Base.FloorX==8448&&projection.Base.FloorY==8448&&projection.Base.FloorZ==8704&&
            projection.Base.FloorWidth==3072&&projection.Base.FloorDepth==3072&&
            projection.Base.SpawnX==8960&&projection.Base.SpawnY==8960&&projection.Base.SpawnZ==9216&&
            NativeFreshLevelDocumentCodec.SerializeCanonical(projection).SequenceEqual(
                NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(
                    SavedLockedChestCompilationCases.Document())))&&
            NativeFreshLevelDocumentCodec.SerializeCanonical(document).SequenceEqual(canonical),
            "Exact complete preflight projects only the legacy resource input, retaining actual Wizard and first-occurrence material owners.");
        await Throws<ArgumentNullException>(()=>{NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(null!);return Task.CompletedTask;});
        await Throws<ArgumentNullException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",null!));
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Throws<OperationCanceledException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",null!,cancel.Token));}
        var (_,valid)=Save("tiled-family-preflight-valid",document);
        await Throws<ArgumentException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",valid));
        await Throws<ArgumentException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureAsync("\0","\0",valid));
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),
            "Exact Candidate source issuance does not change saved/UI profile eligibility or normal export.");
        foreach(var (name,invalid) in Unsupported())
        {
            await Throws<InvalidDataException>(()=>{NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(invalid);return Task.CompletedTask;});
            var (_,saved)=Save("tiled-family-preflight-"+name,invalid);
            await Throws<InvalidDataException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",saved));
        }
        foreach(string owner in new[]{"raw","manifest"})
        {
            var (project,saved)=Save("tiled-family-preflight-stale-"+owner,document);
            File.AppendAllText(owner=="raw"?saved.Snapshot.Path:project.ManifestPath," ");
            await Throws<InvalidDataException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",saved));
        }
        var renamed=Rename(document);var renamedIntent=NativeFreshTiledFamilyTerrainIntent.Create(renamed);
        Check(!NativeFreshLevelDocumentCodec.SerializeCanonical(renamed).SequenceEqual(canonical)&&
            renamedIntent.CopyBoundSurface().SequenceEqual(intent.CopyBoundSurface())&&
            ReferenceEquals(NativeFreshSavedTiledFamilyCompilation.ProjectBaseDocument(renamed).Placements[0],renamed.Placements[0]),
            "Tile and all actor IDs are authorship, not fixed admission values or discarded native aliases.");
        Console.WriteLine($"SAVED_TILED_FAMILY_COMPILATION_FREE_OK checks={Checks} sourceOpened=false candidate=false publication=false");
    }

    private static IReadOnlyList<(string Name,NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var d=Document();var t=d.TiledContent!;var wizard=d.Placements[0];var grass=d.Placements[1];
        var chest=(NativeFreshLockedChestFamilyPlacementIntent)d.Placements[2];
        var result=new List<(string,NativeFreshLevelDocumentIntent)>{
            ("legacy",SavedLockedChestCompilationCases.Document()),("no-family",d.WithPlacements([wizard,grass])),
            ("actor-order",d.WithPlacements([wizard,chest,grass])),
            ("default-rewards",d.WithPlacements([wizard,grass,chest.WithRewardDestinations(null)])),
            ("reward-value",d.WithPlacements([wizard,grass,chest.WithRewardDestinations(
                NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward2,10455,10503,9216)]))])),
            ("anchor",d.WithPlacements([wizard,grass,chest.WithAnchors(9729,9120,8704,10864,9888,8704)])),
            ("wizard-route",d.WithPlacements([NativeFreshWizardRouteIntent.Create(wizard.EditorId,9984,9984,8704,9456,9984),grass,chest])),
            ("tile-order",d.WithTiledContent(t.WithTiles([t.Tiles[1],t.Tiles[0],t.Tiles[2],t.Tiles[3]]))),
            ("heading",d.WithTiledContent(t.WithSettings(8960,8960,9216,65,t.SkyColor,19))),
            ("spawn",d.WithTiledContent(t.WithSettings(8976,8960,9216,64,t.SkyColor,19))),
            ("sky",d.WithTiledContent(t.WithSettings(8960,8960,9216,64,new(128,96,65),19))),
            ("track",d.WithTiledContent(t.WithSettings(8960,8960,9216,64,t.SkyColor,20)))};
        foreach(var (name,x,y,z,width,depth,materials) in new[]{
            ("seam-gap",10000,8448,8704,1536,1536,t.Tiles[1].Materials),
            ("seam-overlap",9968,8448,8704,1536,1536,t.Tiles[1].Materials),
            ("floor-z",9984,8448,8720,1536,1536,t.Tiles[1].Materials),
            ("outer-width",9984,8448,8704,1520,1536,t.Tiles[1].Materials),
            ("face-order",9984,8448,8704,1536,1536,NativeFreshFloorMaterialsIntent.Create(Retail(70,1),Retail(14,1))),
            ("face-source",9984,8448,8704,1536,1536,NativeFreshFloorMaterialsIntent.Create(Retail(14,1),Retail(70,0)))})
        {
            var tiles=t.Tiles.ToArray();var old=tiles[1];
            tiles[1]=NativeFreshFlatTileIntent.Create(old.EditorId,x,y,z,width,depth,old.FloorColor,materials);
            // Some malformed unions are already refused by the immutable
            // aggregate (for example a missing quadrant at the Wizard seam).
            // Any still-valid authored near miss must reach compiler preflight.
            try{result.Add((name,d.WithTiledContent(t.WithTiles(tiles))));}
            catch(InvalidDataException){Refusals++;Check(true,"Malformed union refused before saved compiler: "+name);}
        }
        return result;
    }

    internal static async Task SourceAsync(string bin,string cue)
    {
        _=cue; // The runner owns the complete BIN/CUE pair; this compiler only captures typed Original resources.
        var (_,saved)=Save("tiled-family-compilation",Document());
        var compilation=await NativeFreshSavedTiledFamilyCompilation.CaptureAsync(bin,saved);
        await VerifySourceAsync(bin,compilation);
        var (_,renamedSaved)=Save("tiled-family-compilation-renamed",Rename(saved.Document));
        var renamed=await NativeFreshSavedTiledFamilyCompilation.CaptureAsync(bin,renamedSaved);
        Check(renamed.Body.CopyLevelData().SequenceEqual(compilation.Body.CopyLevelData())&&
            renamed.Executable.CopyExecutable().SequenceEqual(compilation.Executable.CopyExecutable())&&
            renamed.CanonicalDocumentSha256!=compilation.CanonicalDocumentSha256&&renamed.PlanSha256!=compilation.PlanSha256&&
            renamed.Body.RowSpans.Placements.Select(p=>p.EditorId).SequenceEqual(renamedSaved.Document.Placements.Select(p=>p.EditorId)),
            "Saved tile/actor identity changes retain exact native bytes but change complete compiler ownership and fingerprint.");
        await Throws<InvalidDataException>(()=>{compilation.RequireAncestry(renamedSaved);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{compilation.Clearance.RequireStaticLandingClearance(renamed.Executable,compilation.ParentClearance);return Task.CompletedTask;});
        File.AppendAllText(saved.Snapshot.Path," ");
        await Throws<InvalidDataException>(()=>NativeFreshSavedTiledFamilyCompilation.CaptureAsync("\0",saved));
    }

    internal static async Task VerifySourceAsync(string bin,NativeFreshSavedTiledFamilyCompilation compilation)
    {
        var saved=compilation.Saved;var d=saved.Document;var family=(NativeFreshLockedChestFamilyPlacementIntent)d.Placements[2];
        compilation.RequireAncestry(saved);
        Check(ReferenceEquals(compilation.Document,d)&&ReferenceEquals(compilation.Intent.DocumentAncestor,d)&&
            ReferenceEquals(compilation.BaseDocument.Placements[0],d.Placements[0])&&
            ReferenceEquals(compilation.BaseDocument.FloorMaterials!.Triangle0,compilation.Intent.MaterialSelections[0])&&
            ReferenceEquals(compilation.BaseDocument.FloorMaterials.Triangle1,compilation.Intent.MaterialSelections[1])&&
            ReferenceEquals(compilation.ParentPlacement.RewardDestinationsAncestor,family.RewardDestinations)&&
            ReferenceEquals(compilation.Terrain.PlacementAncestor,compilation.ParentPlacement)&&
            ReferenceEquals(compilation.Body.TerrainAncestor,compilation.Terrain)&&
            ReferenceEquals(compilation.Executable.BodyAncestor,compilation.Body)&&
            ReferenceEquals(compilation.Executable.ParentExecutableAncestor,compilation.ParentExecutable)&&
            ReferenceEquals(compilation.Clearance.ParentClearanceAncestor,compilation.ParentClearance)&&
            ReferenceEquals(compilation.Disc.ParentDiscAncestor,compilation.ParentDisc)&&
            ReferenceEquals(compilation.ParentDisc.ExecutableAncestor,compilation.ParentExecutable),
            "Saved tiled compilation retains one actual complete document and one exact shared R03 parent/source chain.");
        compilation.Clearance.RequireStaticLandingClearance(compilation.Executable,compilation.ParentClearance);
        Check(compilation.Body.LevelDataSha256=="c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca"&&
            compilation.Body.SceneSha256=="4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8"&&
            compilation.Executable.ExecutableSha256=="81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431"&&
            compilation.ParentPlacement.LevelDataSha256=="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"&&
            compilation.Body.CopyScene().SequenceEqual(compilation.ParentPlacement.CopyScene())&&
            compilation.Body.RevertExactLevelData(compilation.Body.CopyLevelData()).SequenceEqual(compilation.ParentPlacement.CopyLevelData())&&
            compilation.Executable.CopyExecutable().SequenceEqual(compilation.ParentExecutable.CopyExecutable()),
            "Typed child matches independent complete Body/Scene/EXE references and the immediate unchanged-parent inverse.");
        Check(compilation.Body.RowSpans.Placements.Select(p=>p.EditorId).SequenceEqual(d.Placements.Select(p=>p.EditorId))&&
            compilation.Body.RowSpans.Placements.Select(p=>p.RowCount).SequenceEqual(new[]{1,1,7})&&
            compilation.Clearance.OuterUnionEquivalentProven&&compilation.Clearance.StaticLandingClearanceProven&&
            compilation.Clearance.TileCount==4&&!compilation.RuntimeVerified&&!compilation.PublicationApproved&&
            !compilation.CompleteNativeDependencyClosureProven&&!compilation.KeyZeroContributionProven&&
            !compilation.Disc.NormalCreateBinEnabled,
            "Ordered full authorship and mandatory exact union clearance do not grant runtime, generic family or publication authority.");
        using(var source=File.OpenRead(bin))
        {
            RequireOriginal(source);source.Position=0;
            var measured=NativeId65FreshDiscStreaming.Measure(compilation.Disc,source,CancellationToken.None);
            Check(measured.OutputSha256==ExpectedImage&&measured.ChangedSectors==1297,
                "Complete streamed image matches the independent child physical reference without writing an image.");
            RequireOriginal(source);
        }
        byte[] canonical=compilation.CopyCanonicalDocument();
        Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(d))&&
            Hash(canonical)==compilation.CanonicalDocumentSha256&&compilation.Intent.CanonicalDocumentSha256==compilation.CanonicalDocumentSha256,
            "Complete actual saved schema5 canonical bytes include every tile, material face, actor ID and reward destination.");
        canonical[0]^=1;Check(!canonical.SequenceEqual(compilation.CopyCanonicalDocument()),"Canonical output is detached.");
        var equal=NativeFreshSavedLevelDocumentReadContext.Capture(saved.Snapshot);
        Check(ReferenceEquals(equal.Document,d),"Foreign context isolates owner identity while keeping actual document.");
        await Throws<InvalidDataException>(()=>{compilation.RequireAncestry(equal);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{saved.RequireDocument(NativeFreshLevelDocumentCodec.Parse(compilation.CopyCanonicalDocument()));return Task.CompletedTask;});
        Console.WriteLine($"SAVED_TILED_FAMILY_COMPILATION_SOURCE_OK checks={Checks} image={ExpectedImage} runtime=false publication=false");
    }

    private static NativeFreshLevelDocumentIntent Rename(NativeFreshLevelDocumentIntent d)
    {
        var t=d.TiledContent!;var tiles=t.Tiles.ToArray();var old=tiles[0];
        tiles[0]=NativeFreshFlatTileIntent.Create(new('8',32),old.X,old.Y,old.Z,old.Width,old.Depth,old.FloorColor,old.Materials);
        var w=(NativeFreshWizardRouteIntent)d.Placements[0];var g=(NativeFreshMobyPlacementIntent)d.Placements[1];
        var c=(NativeFreshLockedChestFamilyPlacementIntent)d.Placements[2];
        return d.WithTiledContent(t.WithTiles(tiles)).WithPlacements([
            NativeFreshWizardRouteIntent.Create(new('9',32),w.OriginX,w.OriginY,w.OriginZ,w.EndX,w.EndY),
            NativeFreshMobyPlacementIntent.Create(new('a',32),g.Source,g.X,g.Y,g.Z,g.NativeYawByte),
            NativeFreshLockedChestFamilyPlacementIntent.Create(new('b',32),c.Source,c.KeyX,c.KeyY,c.KeyZ,
                c.ChestX,c.ChestY,c.ChestZ,c.RewardDestinations)]);
    }
}
