using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Written before the production wrapper. The old actual collider assessment
// remains the sole math/source authority; these cases exercise union binding.
internal static class TiledFamilyClearanceCases
{
    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilySpawnClearancePlan);
        check(type.IsSealed&&!type.IsPublic&&
            type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate)&&
            type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.DeclaredOnly).Length==0,
            "Tiled clearance is closed internal composition without public byte/domain/callback ingress.");
        var compose=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Single(m=>m.Name=="Compose");
        check(compose.IsAssembly&&compose.ReturnType==type&&compose.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
            new[]{typeof(NativeFreshTiledFamilyExecutablePlan),typeof(NativeFreshLockedChestSpawnClearancePlan)}),
            "Clearance requires the actual tiled executable and actual verified parent clearance.");
        foreach(var item in new[]{("ExecutableAncestor",typeof(NativeFreshTiledFamilyExecutablePlan)),
            ("ParentClearanceAncestor",typeof(NativeFreshLockedChestSpawnClearancePlan)),
            ("BodyAncestor",typeof(NativeFreshTiledFamilyBodyPlan)),
            ("TerrainAncestor",typeof(NativeFreshTiledFamilyTerrainRevision)),
            ("PlacementAncestor",typeof(NativeFreshLockedChestPlacementPlan)),
            ("HostAncestor",typeof(NativeFreshLockedChestResearchHostExtension)),
            ("DocumentAncestor",typeof(NativeFreshLevelDocumentIntent))})
        {
            var property=type.GetProperty(item.Item1,BindingFlags.Instance|BindingFlags.NonPublic);
            check(property is not null&&property.PropertyType==item.Item2&&property.GetMethod?.IsAssembly==true&&
                property.GetSetMethod(true) is null,"Read-only actual clearance owner "+item.Item1);
        }
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilySpawnClearancePlan.Compose(null!,null!),check);

        // Future geometry cannot silently retain the fixed-union premise.
        var document=TiledFamilyTerrainIntentCases.Document();var content=document.TiledContent!;
        foreach((int Index,int X,int Y,int Z,int Width,int Depth) edit in new[]{
            (1,10000,8448,8704,1536,1536), // gap at internal X seam
            (1,9968,8448,8704,1536,1536),  // overlap at internal X seam
            (2,8448,10000,8704,1536,1536), // gap at internal Y seam
            (3,9984,9984,8720,1536,1536), // unequal floor elevation
            (0,8448,8448,8704,1520,1536), // missing outer/seam strip
            (3,9984,9984,8704,1536,1520)})
        {
            Reject<InvalidDataException>(()=>
            {
                var tiles=content.Tiles.ToArray();var old=tiles[edit.Index];
                tiles[edit.Index]=NativeFreshFlatTileIntent.Create(old.EditorId,edit.X,edit.Y,edit.Z,
                    edit.Width,edit.Depth,old.FloorColor,old.Materials);
                _=NativeFreshTiledFamilyTerrainIntent.Create(document.WithTiledContent(content.WithTiles(tiles)));
            },check);
        }
        Console.WriteLine("TILED_FAMILY_CLEARANCE_FREE_OK sourceOpened=false runtime=false publication=false");
    }

    internal static NativeFreshTiledFamilySpawnClearancePlan Source(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestSpawnClearancePlan parent,Action<bool,string> check)
    {
        var body=executable.BodyAncestor;var terrain=executable.TerrainAncestor;var placement=executable.PlacementAncestor;
        var host=executable.HostAncestor;var document=executable.DocumentAncestor;var intent=terrain.IntentAncestor;
        byte[] level=executable.CopyLevelData(),oldLevel=placement.CopyLevelData(),scene=placement.CopyScene();
        byte[] exe=executable.CopyExecutable(),canonical=intent.CopyCanonicalDocument(),surface=intent.CopySurface();
        string executablePlan=executable.PlanSha256,parentPlan=parent.PlanSha256,bodyPlan=body.PlanSha256;
        parent.RequireStaticLandingClearance(placement);
        var plan=NativeFreshTiledFamilySpawnClearancePlan.Compose(executable,parent);
        plan.RequireAncestry(executable,parent);plan.RequireStaticLandingClearance(executable,parent);
        check(ReferenceEquals(plan.ExecutableAncestor,executable)&&ReferenceEquals(plan.ParentClearanceAncestor,parent)&&
            ReferenceEquals(plan.BodyAncestor,body)&&ReferenceEquals(plan.TerrainAncestor,terrain)&&
            ReferenceEquals(plan.PlacementAncestor,placement)&&ReferenceEquals(plan.HostAncestor,host)&&
            ReferenceEquals(plan.DocumentAncestor,document)&&ReferenceEquals(parent.PlacementAncestor,placement)&&
            ReferenceEquals(parent.HostAncestor,host),"Union binding keeps the complete exact executable/clearance/placement/Host ancestry.");
        check(plan.CanonicalDocumentSha256==Hash(canonical)&&plan.CanonicalDocumentSha256==terrain.CanonicalDocumentSha256&&
            plan.LevelDataSha256==Hash(level)&&plan.SceneSha256==Hash(scene)&&IsHash(plan.PlanSha256)&&
            Hash(level)=="c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca"&&
            Hash(scene)=="4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8",
            "Complete schema5/document, new Body and unchanged placed Scene are bound, not just geometry summaries.");
        check(body.CopyScene().SequenceEqual(scene)&&level.AsSpan(0x102000,0x3800).SequenceEqual(scene)&&
            oldLevel.AsSpan(0x102000,0x3800).SequenceEqual(scene)&&
            level.AsSpan(0xE7000,0x1B000).SequenceEqual(oldLevel.AsSpan(0xE7000,0x1B000))&&
            level.AsSpan(0xE6178,0x390).SequenceEqual(intent.CopyBoundSurface()),
            "New actual terrain lives in the Body while the complete model/collider and placed Scene owners remain identical.");
        var decoded=NativeFlatTileSurfaceLayout.DecodeAndValidate(surface);
        check(decoded.Tiles.Select(t=>(t.X,t.Y,t.Z,t.Width,t.Depth)).SequenceEqual(new[]{
            (8448,8448,8704,1536,1536),(9984,8448,8704,1536,1536),
            (8448,9984,8704,1536,1536),(9984,9984,8704,1536,1536)})&&
            decoded.Tiles.Sum(t=>(long)t.Width*t.Depth)==3072L*3072&&
            plan.UnionX==8448&&plan.UnionY==8448&&plan.FloorZ==8704&&plan.UnionWidth==3072&&plan.UnionDepth==3072&&
            plan.TileCount==4&&plan.OuterUnionEquivalentProven&&plan.StaticLandingClearanceProven,
            "Four complete decoded quadrants cover the exact continuous outer rectangle with no gap or overlap.");
        check(ReferenceEquals(plan.InitialFinding,parent.InitialFinding)&&plan.LandingColumnStatus==parent.LandingColumnStatus&&
            plan.HorizontalDistanceSquared==parent.HorizontalDistanceSquared&&plan.CombinedRadiusSquared==parent.CombinedRadiusSquared&&
            plan.ChestMargins==parent.ChestMargins&&plan.PlayerMargins==parent.PlayerMargins&&
            plan.InitialFinding.Centre==new NativeSpawnClearancePoint(10864,9888,9040)&&
            plan.InitialFinding.Spawn==new NativeSpawnClearancePoint(8960,8960,9216)&&
            plan.InitialFinding.ColliderRadius==600&&plan.InitialFinding.PlayerRadius==356&&
            plan.InitialFinding.DistanceSquared==(BigInteger)4517376&&plan.HorizontalDistanceSquared==(BigInteger)4486400&&
            plan.CombinedRadiusSquared==(BigInteger)913936,
            "The exact parent finding, native source collider/player math and fixed-XY result are reused unchanged.");
        check(plan.ChestMargins.Left==2416&&plan.ChestMargins.Right==656&&plan.ChestMargins.Top==1440&&
            plan.ChestMargins.Bottom==1632&&plan.ChestMargins.StrictlyContains(600)&&
            plan.PlayerMargins.Left==512&&plan.PlayerMargins.Right==2560&&plan.PlayerMargins.Top==512&&
            plan.PlayerMargins.Bottom==2560&&plan.PlayerMargins.StrictlyContains(356)&&
            9888+600>9984&&9984-9888==96&&656-600==56,
            "Chest disk crosses the internal Y seam by504 but strictly fits the union by56; no per-tile disk rejection or centre-only shortcut.");
        check(plan.SourceCertified&&!plan.RuntimeVerified&&!plan.PublicationApproved&&!plan.FirstQueryProven&&
            !plan.TrajectoryProven&&!plan.RouteProven&&!plan.WholeFamilyFootprintClearanceProven&&
            !plan.FullNativeLoaderProven&&!plan.CompleteNativeDependencyClosureProven&&!plan.PeakRuntimeMemoryProven,
            "Exact initial/fixed-XY union binding does not grant route, trajectory, family, loading, peak-memory, runtime or publication proof.");

        var equalExe=NativeFreshTiledFamilyExecutablePlan.Compose(body,executable.ParentExecutableAncestor);
        var equalClear=NativeFreshLockedChestSpawnClearancePlan.Compose(placement);
        check(equalExe.PlanSha256==executable.PlanSha256&&equalClear.PlanSha256==parent.PlanSha256&&
            equalExe.CopyLevelData().SequenceEqual(level),"Foreign controls are actual equal-byte and equal-fingerprint issuances.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(equalExe,parent),check);
        Reject<InvalidDataException>(()=>plan.RequireAncestry(executable,equalClear),check);
        Reject<InvalidDataException>(()=>plan.RequireStaticLandingClearance(equalExe,parent),check);
        Reject<InvalidDataException>(()=>plan.RequireStaticLandingClearance(executable,equalClear),check);
        var repeat=NativeFreshTiledFamilySpawnClearancePlan.Compose(executable,parent);
        var equalOwn=NativeFreshTiledFamilySpawnClearancePlan.Compose(equalExe,equalClear);
        check(repeat.PlanSha256==plan.PlanSha256&&equalOwn.PlanSha256==plan.PlanSha256&&
            ReferenceEquals(equalOwn.ExecutableAncestor,equalExe)&&ReferenceEquals(equalOwn.ParentClearanceAncestor,equalClear),
            "Equal issuances remain deterministic while every result owns its actual tuple.");
        var foreignPlacement=NativeFreshLockedChestPlacementPlan.ComposeWithRewardDestinations(placement.BodyAncestor,
            placement.Intent,placement.RewardDestinationsAncestor!);
        var foreignClear=NativeFreshLockedChestSpawnClearancePlan.Compose(foreignPlacement);
        check(foreignPlacement.CopyLevelData().SequenceEqual(oldLevel)&&foreignClear.PlanSha256==parent.PlanSha256,
            "Foreign parent-clearance control preserves every byte and finding but owns another actual placement.");
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilySpawnClearancePlan.Compose(executable,foreignClear),check);
        var overlapping=NativeFreshLockedChestPlacementPlan.Compose(placement.BodyAncestor,new(8960,9728,8704,9472,8960,8704));
        var overlapClear=NativeFreshLockedChestSpawnClearancePlan.Compose(overlapping);
        Reject<InvalidDataException>(()=>overlapClear.RequireStaticLandingClearance(overlapping),check);
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilySpawnClearancePlan.Compose(executable,overlapClear),check);

        // An ID-only document edit leaves every native byte equal but must
        // alter the full binding fingerprint. Reuse actual placement owners.
        var content=document.TiledContent!;var tiles=content.Tiles.ToArray();var first=tiles[0];
        tiles[0]=NativeFreshFlatTileIntent.Create(new('8',32),first.X,first.Y,first.Z,first.Width,first.Depth,
            first.FloorColor,first.Materials);
        var renamedDocument=document.WithTiledContent(content.WithTiles(tiles));
        var renamedIntent=NativeFreshTiledFamilyTerrainIntent.Create(renamedDocument);
        var renamedTerrain=NativeFreshTiledFamilyTerrainRevision.Compose(renamedIntent,placement);
        var renamedBody=NativeFreshTiledFamilyBodyPlan.Compose(renamedTerrain);
        var renamedExe=NativeFreshTiledFamilyExecutablePlan.Compose(renamedBody,executable.ParentExecutableAncestor);
        var renamed=NativeFreshTiledFamilySpawnClearancePlan.Compose(renamedExe,parent);
        check(renamedBody.CopyLevelData().SequenceEqual(level)&&renamed.CanonicalDocumentSha256!=plan.CanonicalDocumentSha256&&
            renamed.PlanSha256!=plan.PlanSha256&&ReferenceEquals(renamed.DocumentAncestor,renamedDocument),
            "Complete schema5 identity survives equal native Body/Scene bytes and changes the clearance fingerprint.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(renamedExe,parent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilySpawnClearancePlan.Compose(null!,parent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilySpawnClearancePlan.Compose(executable,null!),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!,parent),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(executable,null!),check);
        level[0]^=1;scene[0]^=1;canonical[0]^=1;surface[^1]^=1;exe[0]^=1;
        plan.RequireStaticLandingClearance(executable,parent);
        check(executable.PlanSha256==executablePlan&&parent.PlanSha256==parentPlan&&body.PlanSha256==bodyPlan&&
            plan.LevelDataSha256==Hash(executable.CopyLevelData())&&plan.SceneSha256==Hash(placement.CopyScene())&&
            plan.CanonicalDocumentSha256==Hash(intent.CopyCanonicalDocument()),
            "Detached-copy mutation and all refusal controls preserve actual source, geometry, Body and clearance ancestors.");
        Console.WriteLine($"TILED_FAMILY_CLEARANCE_SOURCE_OK plan={plan.PlanSha256} union=3072 tileCount=4 chestDiskMargin=56 playerDiskMargin=156 fixedXYOnly=true runtime=false publication=false");
        return plan;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsHash(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Tiled family clearance refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
