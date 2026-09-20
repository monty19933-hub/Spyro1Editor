using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Frozen neutral Python oracle references, not production-derived expectations.
// No ROM, source-issued owner, Body, Scene, publication or emulator is involved.
internal static class TiledFamilyTerrainIntentCases
{
    private const string Variant="spyro1-usa-retail-mode2-2352-v1";
    private const string CanonicalHash="2bc9a53da03fdc5d5c8bfe6bc11b1c46fbbdd1db7ae089c1df8a664c20e5b375";
    private const string SurfaceHash="6af708ea930471156cf3e68406eef231d2ff8b195d6ca1a832a9406c731af23f";
    private const string BoundHash="a18fbf9c2bc48e8d4b6290e7250236773daaf13fe4d8733fb6526cc28d24c11b";
    private static readonly int[] Faces=[0,0,0,1,0,1,1,1];
    private static readonly int[] FaceOffsets=[156,172,316,332,476,492,636,652];

    internal static NativeFreshLevelDocumentIntent Document()
    {
        var first=NativeFreshFloorMaterialIntent.RetailTerrain(Variant,14,1);
        var second=NativeFreshFloorMaterialIntent.RetailTerrain(Variant,70,1);
        var aa=NativeFreshFloorMaterialsIntent.Create(first,first);
        var ab=NativeFreshFloorMaterialsIntent.Create(first,second);
        var bb=NativeFreshFloorMaterialsIntent.Create(second,second);
        var tiles=NativeFreshTiledLevelContent.Create([
            NativeFreshFlatTileIntent.Create(new('4',32),8448,8448,8704,1536,1536,new(80,128,112),aa),
            NativeFreshFlatTileIntent.Create(new('5',32),9984,8448,8704,1536,1536,new(80,128,112),ab),
            NativeFreshFlatTileIntent.Create(new('6',32),8448,9984,8704,1536,1536,new(80,128,112),ab),
            NativeFreshFlatTileIntent.Create(new('7',32),9984,9984,8704,1536,1536,new(80,128,112),bb)],
            8960,8960,9216,64,new(128,96,64),19);
        return NativeFreshLevelDocumentIntent.CreateTiled(tiles,[]).UpgradeForFamilies().WithPlacements([
            NativeFreshWizardRouteIntent.Create(new('1',32),9984,9984,8704,9472,9984),
            NativeFreshMobyPlacementIntent.Create(new('2',32),NativeFreshRetailMobyReference.Create(Variant,10,121,501),
                9152,9152,8704,64),
            NativeFreshLockedChestFamilyPlacementIntent.Create(new('3',32),
                NativeFreshLockedChestFamilyReference.Create(Variant,"peace-keepers-locked-chest-v1"),
                9728,9120,8704,10864,9888,8704,NativeFreshChestRewardDestinationOverrides.Create([
                    new(NativeFreshChestRewardRole.Reward2,10454,10503,9216)]))]);
    }

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilyTerrainIntent);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0&&
            type.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).All(c=>c.IsPrivate),
            "Terrain preflight is internal/private-construction, not a new public admission surface.");
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyTerrainIntent.Create(null!),check);
        var document=Document();var plan=NativeFreshTiledFamilyTerrainIntent.Create(document);
        byte[] canonical=plan.CopyCanonicalDocument(),surface=plan.CopySurface(),bound=plan.CopyBoundSurface();
        check(ReferenceEquals(plan.DocumentAncestor,document)&&canonical.Length==2615&&Hash(canonical)==CanonicalHash&&
            plan.CanonicalDocumentSha256==CanonicalHash&&canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)),
            "Complete actual schema5 canonical document matches independent oracle2615-byte reference.");
        check(surface.Length==912&&Hash(surface)==SurfaceHash&&plan.SurfaceSha256==SurfaceHash&&
            bound.Length==912&&Hash(bound)==BoundHash&&plan.BoundSurfaceSha256==BoundHash,
            "Both complete neutral surfaces match independently executed oracle references.");
        check(plan.PlacementIds.SequenceEqual(new[]{new string('1',32),new string('2',32),new string('3',32)})&&
            plan.OrderedFaceMaterials.SequenceEqual(Faces)&&plan.MaterialSelections.Count==2&&
            ReferenceEquals(plan.MaterialSelections[0],document.TiledContent!.Tiles[0].Materials.Triangle0)&&
            ReferenceEquals(plan.MaterialSelections[1],document.TiledContent.Tiles[1].Materials.Triangle1),
            "All three authored IDs, both actual first-occurrence descriptors and all eight slots are retained.");
        check(!plan.SourceCertified&&!plan.BodyIssued&&!plan.RuntimeVerified&&!plan.PublicationApproved&&
            !plan.CompleteNativeDependencyClosureProven,
            "Neutral intent has no native/source/resource/body/runtime or publication authority.");
        var spans=new[]{(0,664,"c2044410114a81f59effc512c70f537d2b6e8ddab50ea88d452331b293f62b60"),
            (664,36,"4c0fc4a651ff0b049cf45e644d5cc255329995436d0b7edb1c461978b92510a0"),
            (700,8,"6cc16abd70eefb90dc0ba0d14fb088630873b2c6ad943f7442356735984c35a3"),
            (708,188,"bfbd5b2344cf1bced9b8a69b9c436e1d3e203a167868817d28e4c6bc18262188"),
            (896,16,"7087a293dfe99594734f35672b06831f0da00b001907678d16d63786c023ef53")};
        check(plan.Components.Count==5&&plan.Components.Select(c=>(c.BlockOffset,c.ByteLength,c.Sha256)).SequenceEqual(spans),
            "Every component offset, length and full hash matches the independent reference.");
        byte[] inverse=bound.ToArray();var writes=new HashSet<int>();
        for(int face=0;face<8;face++)
        {
            int at=FaceOffsets[face];
            check(Word(surface,at)==255&&Word(bound,at)==Faces[face]&&Word(bound,at+4)==128,
                "Each complete material word and following flag has the independent expected offset.");
            BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(at),255);
            for(int b=0;b<4;b++)writes.Add(at+b);
        }
        check(inverse.SequenceEqual(surface)&&Enumerable.Range(0,912).All(i=>writes.Contains(i)||surface[i]==bound[i]),
            "Complete surface complement and independent neutral face inverse retain every other byte.");
        plan.RequireAncestry(document);plan.RequireExactSurface(surface);plan.RequireExactBoundSurface(bound);
        var equal=NativeFreshLevelDocumentCodec.Parse(canonical);
        var parsedPlan=NativeFreshTiledFamilyTerrainIntent.Create(equal);
        check(!ReferenceEquals(equal.TiledContent!.Tiles[0].Materials.Triangle0,
                equal.TiledContent.Tiles[0].Materials.Triangle1)&&
            !ReferenceEquals(equal.TiledContent.Tiles[1].Materials.Triangle1,
                equal.TiledContent.Tiles[2].Materials.Triangle1)&&
            ReferenceEquals(parsedPlan.MaterialSelections[0],equal.TiledContent.Tiles[0].Materials.Triangle0)&&
            ReferenceEquals(parsedPlan.MaterialSelections[1],equal.TiledContent.Tiles[1].Materials.Triangle1),
            "Distinct equal parsed descriptors retain the actual first occurrence, not a later equal object.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(equal),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!),check);
        var repeat=NativeFreshTiledFamilyTerrainIntent.Create(document);
        check(repeat.CopyCanonicalDocument().SequenceEqual(canonical)&&repeat.CopySurface().SequenceEqual(surface)&&
            repeat.CopyBoundSurface().SequenceEqual(bound),"Repeated neutral preparation is deterministic.");
        canonical[0]^=1;surface[^1]^=1;bound[FaceOffsets[^1]]^=1;
        check(Hash(plan.CopyCanonicalDocument())==CanonicalHash&&Hash(plan.CopySurface())==SurfaceHash&&
            Hash(plan.CopyBoundSurface())==BoundHash,"All returned byte arrays are detached from retained intent.");
        if(plan.PlacementIds is IList<string> ids)Reject<NotSupportedException>(()=>ids[0]=new('9',32),check);
        if(plan.MaterialSelections is IList<NativeFreshRetailTerrainMaterialIntent> selections)
            Reject<NotSupportedException>(()=>selections[0]=selections[1],check);
        if(plan.OrderedFaceMaterials is IList<int> faces)Reject<NotSupportedException>(()=>faces[0]=1,check);
        foreach(int at in FaceOffsets.Concat(new[]{0,8,180,664,700,720,780,908}))
        {
            var changed=plan.CopyBoundSurface();changed[at]^=1;
            Reject<InvalidDataException>(()=>plan.RequireExactBoundSurface(changed),check);
        }
        Reject<InvalidDataException>(()=>plan.RequireExactSurface(plan.CopySurface()[..^1]),check);
        Reject<InvalidDataException>(()=>plan.RequireExactBoundSurface(plan.CopyBoundSurface().Concat(new byte[4]).ToArray()),check);
        IdentityEdits(document,plan,check);
        NearMisses(document,check);
        check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),
            "Well-formed new neutral terrain remains unsupported by every relevant saved/UI profile.");
        Reject<InvalidDataException>(()=>NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(document),check);
        Reject<InvalidDataException>(()=>NativeId65TiledSurfaceBundle.PrepareDocument(document),check);
        Reject<InvalidDataException>(()=>NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(document),check);
        Reject<InvalidDataException>(()=>NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync("\0",document).GetAwaiter().GetResult(),check);
        Reject<InvalidDataException>(()=>NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(document),check);
        Console.WriteLine("TILED_FAMILY_TERRAIN_INTENT_FREE_OK sourceOpened=false bodyIssued=false runtime=false publication=false");
    }

    private static void IdentityEdits(NativeFreshLevelDocumentIntent document,NativeFreshTiledFamilyTerrainIntent old,Action<bool,string> check)
    {
        string text=System.Text.Encoding.UTF8.GetString(old.CopyCanonicalDocument());
        foreach(char id in new[]{'1','2','3','4','5','6','7'})
        {
            var changed=NativeFreshLevelDocumentCodec.Parse(System.Text.Encoding.UTF8.GetBytes(text.Replace(new string(id,32),new string('9',32))));
            var next=NativeFreshTiledFamilyTerrainIntent.Create(changed);
            check(ReferenceEquals(next.DocumentAncestor,changed)&&next.CanonicalDocumentSha256!=old.CanonicalDocumentSha256&&
                next.CopySurface().SequenceEqual(old.CopySurface())&&next.CopyBoundSurface().SequenceEqual(old.CopyBoundSurface()),
                "Each valid ID-only edit changes full canonical identity without altering geometry or native recipe eligibility.");
            Reject<InvalidDataException>(()=>old.RequireAncestry(changed),check);
        }
        foreach(string replacement in new[]{new string('1',32),new string('0',32),""})
            Reject<InvalidDataException>(()=>NativeFreshLevelDocumentCodec.Parse(System.Text.Encoding.UTF8.GetBytes(
                text.Replace(new string('2',32),replacement))),check);
        Reject<InvalidDataException>(()=>NativeFreshLevelDocumentCodec.Parse(System.Text.Encoding.UTF8.GetBytes(
            text.Replace(new string('5',32),new string('4',32)))),check);
    }

    private static void NearMisses(NativeFreshLevelDocumentIntent document,Action<bool,string> check)
    {
        var content=document.TiledContent!;
        NativeFreshLevelDocumentIntent Tile(int index,int? x=null,int? y=null,int? z=null,int? width=null,
            NativeFreshFloorMaterialsIntent? materials=null)
        {
            var rows=content.Tiles.ToArray();var t=rows[index];
            rows[index]=NativeFreshFlatTileIntent.Create(t.EditorId,x??t.X,y??t.Y,z??t.Z,width??t.Width,t.Depth,t.FloorColor,materials??t.Materials);
            return document.WithTiledContent(content.WithTiles(rows));
        }
        foreach(Func<NativeFreshLevelDocumentIntent> invalid in new Func<NativeFreshLevelDocumentIntent>[]{
            ()=>Tile(3,x:10000),()=>Tile(1,x:10000),()=>Tile(1,x:9968),()=>Tile(2,z:8720),()=>Tile(0,width:1520),
            ()=>Tile(0,materials:content.Tiles[1].Materials),
            ()=>Tile(3,materials:NativeFreshFloorMaterialsIntent.Create(content.Tiles[3].Materials.Triangle0,NativeFreshFloorMaterialIntent.BaseFloorColor())),
            ()=>Tile(3,materials:NativeFreshFloorMaterialsIntent.Create(content.Tiles[3].Materials.Triangle0,NativeFreshFloorMaterialIntent.RetailTerrain(Variant,70,0))),
            ()=>document.WithTiledContent(content.WithTiles(content.Tiles.Reverse())),
            ()=>document.WithTiledContent(content.WithTiles(content.Tiles.Take(3))),
            ()=>document.WithTiledContent(content.WithSettings(8976,8960,9216,64,new(128,96,64),19)),
            ()=>document.WithTiledContent(content.WithSettings(8960,8960,9216,65,new(128,96,64),19)),
            ()=>document.WithTiledContent(content.WithSettings(8960,8960,9216,64,new(128,96,65),19)),
            ()=>document.WithTiledContent(content.WithSettings(8960,8960,9216,64,new(128,96,64),18)),
            ()=>document.WithPlacements(document.Placements.Take(2)),()=>document.WithPlacements(document.Placements.Reverse()),
            ()=>NativeFreshLevelDocumentIntent.CreateTiled(content,document.Placements.Take(2))})
            Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainIntent.Create(invalid()),check);
        var wizard=(NativeFreshWizardRouteIntent)document.Placements[0];
        var grass=(NativeFreshMobyPlacementIntent)document.Placements[1];
        var family=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        foreach(var replacement in new NativeFreshPlacementIntent[]{
            NativeFreshWizardRouteIntent.Create(wizard.EditorId,9984,9984,8704,9488,9984),
            NativeFreshMobyPlacementIntent.Create(grass.EditorId,NativeFreshRetailMobyReference.Create(Variant,10,120,501),9152,9152,8704,64),
            NativeFreshMobyPlacementIntent.Create(grass.EditorId,grass.Source,9153,9152,8704,64),
            family.WithAnchors(9728,9120,8704,10864,9904,8704),family.WithRewardDestinations(null),
            family.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward5,10454,10503,9216)])),
            family.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward2,10454,10504,9216)])),
            family.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create([
                new(NativeFreshChestRewardRole.Reward2,10454,10503,9216),new(NativeFreshChestRewardRole.Reward1,10454,10503,9216)]))})
            Reject<InvalidDataException>(()=>NativeFreshTiledFamilyTerrainIntent.Create(document.WithPlacements(
                document.Placements.Select(p=>p.EditorId==replacement.EditorId?replacement:p))),check);
    }
    private static int Word(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected neutral refusal: "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
