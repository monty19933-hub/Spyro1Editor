using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Binds the actual parent's strict initial and fixed-XY landing-column proof
/// to the exact four1536-tile union replacing that parent's3072-square floor.
/// The complete Scene/model owners and parent collider/player assessment are
/// unchanged. Internal tile seams are not outer floor boundaries. This closed
/// equivalence does not prove routes, trajectories, family footprints, first
/// native query, complete loading, peak memory, runtime or publication.
/// </summary>
internal sealed class NativeFreshTiledFamilySpawnClearancePlan
{
    private NativeFreshTiledFamilySpawnClearancePlan(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestSpawnClearancePlan parent)
    {
        ExecutableAncestor=executable;ParentClearanceAncestor=parent;
        RequireAncestry(executable,parent);
        // Mandatory existing source-owned collider/player math and strict
        // floor-disk gate. No second model/collider parser or classifier.
        parent.RequireStaticLandingClearance(PlacementAncestor);
        var body=BodyAncestor;var terrain=TerrainAncestor;var intent=terrain.IntentAncestor;
        var document=DocumentAncestor;var placement=PlacementAncestor;
        var floor=parent.BodyAncestor.ResourceAncestor.BaseBodyAncestor.HostAncestor.SceneAncestor.CompilationAncestor.Intent;
        byte[] level=executable.CopyLevelData(),prior=placement.CopyLevelData(),scene=body.CopyScene();
        byte[] bound=intent.CopyBoundSurface(),surface=intent.CopySurface(),used=terrain.CopyUsedData();
        byte[] canonical=intent.CopyCanonicalDocument();
        executable.RequireExactOutputs(level,executable.CopyExecutable());
        body.RequireExactLevelData(level);body.RequireExactScene(scene);
        placement.RequireExactLevelData(prior);placement.RequireExactScene(scene);
        intent.RequireExactSurface(surface);intent.RequireExactBoundSurface(bound);terrain.RequireExactUsedData(used);
        if(level.Length!=0x20F000||prior.Length!=level.Length||scene.Length!=0x3800||
            Word(level,8)!=0xE6000||Word(level,12)!=0x1000||
            Word(level,16)!=0xE7000||Word(level,20)!=0x1B000||
            Word(level,24)!=0x102000||Word(level,28)!=scene.Length||
            !level.AsSpan(0,0x800).SequenceEqual(prior.AsSpan(0,0x800))||
            !level.AsSpan(0xE7000,0x1B000).SequenceEqual(prior.AsSpan(0xE7000,0x1B000))||
            !level.AsSpan(0x102000,scene.Length).SequenceEqual(scene)||
            !prior.AsSpan(0x102000,scene.Length).SequenceEqual(scene)||
            !scene.AsSpan().SequenceEqual(placement.CopyScene())||
            surface.Length!=0x390||bound.Length!=surface.Length||used.Length!=0xCB8||
            !used.AsSpan(0x178,0x390).SequenceEqual(bound)||
            !level.AsSpan(0xE6000,used.Length).SequenceEqual(used)||
            level.AsSpan(0xE6000+used.Length,0x1000-used.Length).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidDataException("Tiled clearance requires the complete actual new terrain allocation and unchanged parent Scene/model/collider owners.");
        // Exact bound-surface equality links this canonical decoded geometry
        // to the actual new Body. The intent already owns the full reversible
        // eight-word material binding, including all collision/guard bytes.
        var decoded=NativeFlatTileSurfaceLayout.DecodeAndValidate(surface);
        if(document.SchemaVersion!=5||document.TiledContent is not {} content||content.Tiles.Count!=4||
            decoded.Tiles.Count!=4||!decoded.Tiles.SequenceEqual(content.Tiles.Select(t=>t.ToNativeTile()))||
            !canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document))||
            floor.FloorX!=8448||floor.FloorY!=8448||floor.FloorZ!=8704||floor.FloorWidth!=3072||floor.FloorDepth!=3072||
            content.SpawnX!=floor.SpawnX||content.SpawnY!=floor.SpawnY||content.SpawnZ!=floor.SpawnZ||
            content.SpawnX!=8960||content.SpawnY!=8960||content.SpawnZ!=9216||floor.SpawnZ<floor.FloorZ||
            content.Heading!=floor.Heading||scene[14]!=content.Heading||
            new NativeSpawnClearancePoint(Word(scene,0),Word(scene,4),Word(scene,8))!=parent.InitialFinding.Spawn||
            parent.InitialFinding.Spawn!=new NativeSpawnClearancePoint(content.SpawnX,content.SpawnY,content.SpawnZ))
            throw new InvalidDataException("Tiled clearance is limited to the exact complete schema5 floor/spawn interval and actual decoded four-tile owner.");
        // Four fixed quadrants partition [8448,11520] on each axis. Their
        // interiors are disjoint and shared endpoints close both seams, so
        // their union equals the parent's entire rectangle, not just centres.
        // Retain this explicit closed premise if a future terrain issuer grows.
        (int X,int Y)[] positions=[(8448,8448),(9984,8448),(8448,9984),(9984,9984)];
        long area=0;
        for(int i=0;i<4;i++)
        {
            var tile=decoded.Tiles[i];
            if(tile.X!=positions[i].X||tile.Y!=positions[i].Y||tile.Z!=floor.FloorZ||
                tile.Width!=1536||tile.Depth!=1536||tile.FloorRgb!=0x708050||
                (long)tile.X+tile.Width>floor.FloorX+floor.FloorWidth||
                (long)tile.Y+tile.Depth>floor.FloorY+floor.FloorDepth)
                throw new InvalidDataException("Tiled clearance cannot reuse a fixed-union proof for another tile order, gap, overlap, extent or elevation.");
            area+=checked((long)tile.Width*tile.Depth);
        }
        if(area!=(long)floor.FloorWidth*floor.FloorDepth||
            decoded.Tiles[0].X+decoded.Tiles[0].Width!=decoded.Tiles[1].X||
            decoded.Tiles[2].X+decoded.Tiles[2].Width!=decoded.Tiles[3].X||
            decoded.Tiles[0].Y+decoded.Tiles[0].Depth!=decoded.Tiles[2].Y||
            decoded.Tiles[1].Y+decoded.Tiles[1].Depth!=decoded.Tiles[3].Y)
            throw new InvalidDataException("The complete decoded tile union does not exactly partition the parent floor rectangle.");
        if(document.Placements.Count!=3||document.Placements[0] is not NativeFreshWizardRouteIntent wizard||
            document.Placements[1] is not NativeFreshMobyPlacementIntent grass||
            document.Placements[2] is not NativeFreshLockedChestFamilyPlacementIntent family||
            placement.Intent!=new NativeFreshLockedChestPlacementIntent(family.KeyX,family.KeyY,family.KeyZ,
                family.ChestX,family.ChestY,family.ChestZ)||
            !Point(scene,0xE0,wizard.OriginX,wizard.OriginY,wizard.OriginZ)||
            Word(scene,0x1288)!=wizard.EndX||Word(scene,0x128C)!=wizard.EndY||
            !Point(scene,0x138,grass.X,grass.Y,grass.Z)||
            !Point(scene,0x190,family.KeyX,family.KeyY,family.KeyZ)||
            !Point(scene,0x1E8,family.ChestX,family.ChestY,family.ChestZ))
            throw new InvalidDataException("Tiled clearance lost a complete authored actor anchor or its unchanged actual placed Scene owner.");
        if(family.RewardDestinations is null)throw new InvalidDataException("Tiled clearance requires the actual full authored reward owner.");
        placement.RequireRewardDestinationsAncestry(family.RewardDestinations);
        UnionX=floor.FloorX;UnionY=floor.FloorY;FloorZ=floor.FloorZ;
        UnionWidth=floor.FloorWidth;UnionDepth=floor.FloorDepth;
        CanonicalDocumentSha256=Hash(canonical);LevelDataSha256=Hash(level);SceneSha256=Hash(scene);
        if(CanonicalDocumentSha256!=terrain.CanonicalDocumentSha256||CanonicalDocumentSha256!=executable.CanonicalDocumentSha256||
            LevelDataSha256!=body.LevelDataSha256||LevelDataSha256!=executable.LevelDataSha256||SceneSha256!=placement.SceneSha256)
            throw new InvalidDataException("Tiled clearance full document/Body/Scene fingerprints lost their actual owners.");
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="actual-four-tile-union-initial-and-fixed-XY-parent-clearance-v1",parent.SourceImageSha256,
            Executable=executable.PlanSha256,Body=body.PlanSha256,Terrain=terrain.PlanSha256,
            ParentClearance=parent.PlanSha256,Placement=placement.PlanSha256,Host=HostAncestor.PlanSha256,
            CanonicalDocumentSha256,LevelDataSha256,SceneSha256,executable.ExecutableSha256,
            PriorBodySha256=Hash(prior),ModelSha256=Hash(level.AsSpan(0xE7000,0x1B000)),
            intent.SurfaceSha256,intent.BoundSurfaceSha256,terrain.DataSha256,
            Tiles=decoded.Tiles,UnionX,UnionY,FloorZ,UnionWidth,UnionDepth,TileCount,
            content.SpawnX,content.SpawnY,content.SpawnZ,content.Heading,
            InitialFinding.Status,InitialFinding.UnknownReason,InitialFinding.Spawn,InitialFinding.Centre,
            InitialFinding.ColliderRadius,InitialFinding.PlayerRadius,LandingColumnStatus,
            HorizontalDistanceSquared=HorizontalDistanceSquared.ToString(CultureInfo.InvariantCulture),
            CombinedRadiusSquared=CombinedRadiusSquared.ToString(CultureInfo.InvariantCulture),
            ChestMargins=Margins(ChestMargins),PlayerMargins=Margins(PlayerMargins),
            Policy="exact-outer-union-equivalence-and-mandatory-parent-strict-initial-fixed-XY-gate",
            SourceCertified,OuterUnionEquivalentProven,StaticLandingClearanceProven,
            FirstQueryProven,TrajectoryProven,RouteProven,WholeFamilyFootprintClearanceProven,
            FullNativeLoaderProven,CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven,RuntimeVerified,PublicationApproved}));
        RequireStaticLandingClearance(executable,parent);
    }

    internal NativeFreshTiledFamilyExecutablePlan ExecutableAncestor{get;}
    internal NativeFreshLockedChestSpawnClearancePlan ParentClearanceAncestor{get;}
    internal NativeFreshTiledFamilyBodyPlan BodyAncestor=>ExecutableAncestor.BodyAncestor;
    internal NativeFreshTiledFamilyTerrainRevision TerrainAncestor=>ExecutableAncestor.TerrainAncestor;
    internal NativeFreshLockedChestPlacementPlan PlacementAncestor=>ExecutableAncestor.PlacementAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor=>ExecutableAncestor.HostAncestor;
    internal NativeFreshLevelDocumentIntent DocumentAncestor=>ExecutableAncestor.DocumentAncestor;
    internal NativeInitialSpawnClearanceFinding InitialFinding=>ParentClearanceAncestor.InitialFinding;
    internal NativeInitialSpawnClearanceStatus LandingColumnStatus=>ParentClearanceAncestor.LandingColumnStatus;
    internal BigInteger HorizontalDistanceSquared=>ParentClearanceAncestor.HorizontalDistanceSquared;
    internal BigInteger CombinedRadiusSquared=>ParentClearanceAncestor.CombinedRadiusSquared;
    internal NativeInitialFloorMargins ChestMargins=>ParentClearanceAncestor.ChestMargins;
    internal NativeInitialFloorMargins PlayerMargins=>ParentClearanceAncestor.PlayerMargins;
    internal int UnionX{get;}
    internal int UnionY{get;}
    internal int FloorZ{get;}
    internal int UnionWidth{get;}
    internal int UnionDepth{get;}
    internal int TileCount=>4;
    internal string CanonicalDocumentSha256{get;}
    internal string LevelDataSha256{get;}
    internal string SceneSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool OuterUnionEquivalentProven=>true;
    internal bool StaticLandingClearanceProven=>true;
    internal bool FirstQueryProven=>false;
    internal bool TrajectoryProven=>false;
    internal bool RouteProven=>false;
    internal bool WholeFamilyFootprintClearanceProven=>false;
    internal bool FullNativeLoaderProven=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal void RequireAncestry(NativeFreshTiledFamilyExecutablePlan executable,NativeFreshLockedChestSpawnClearancePlan parent)
    {
        ArgumentNullException.ThrowIfNull(executable);ArgumentNullException.ThrowIfNull(parent);
        if(!ReferenceEquals(executable,ExecutableAncestor)||!ReferenceEquals(parent,ParentClearanceAncestor)||
            !ReferenceEquals(parent.PlacementAncestor,executable.PlacementAncestor)||
            !ReferenceEquals(parent.HostAncestor,executable.HostAncestor))
            throw new InvalidDataException("Tiled clearance belongs to its actual executable/parent-clearance and common placement/Host, not equal replacements.");
        executable.RequireAncestry(BodyAncestor,ExecutableAncestor.ParentExecutableAncestor);
        TerrainAncestor.RequireAncestry(TerrainAncestor.IntentAncestor,PlacementAncestor);
        TerrainAncestor.IntentAncestor.RequireAncestry(DocumentAncestor);
        parent.RequireAncestry(PlacementAncestor);
    }
    internal void RequireStaticLandingClearance(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestSpawnClearancePlan parent)
    {
        RequireAncestry(executable,parent);
        parent.RequireStaticLandingClearance(PlacementAncestor);
    }
    internal static NativeFreshTiledFamilySpawnClearancePlan Compose(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestSpawnClearancePlan parent)
    {ArgumentNullException.ThrowIfNull(executable);ArgumentNullException.ThrowIfNull(parent);return new(executable,parent);}
    private static bool Point(ReadOnlySpan<byte> scene,int at,int x,int y,int z)=>
        Word(scene,at)==x&&Word(scene,at+4)==y&&Word(scene,at+8)==z;
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string[] Margins(NativeInitialFloorMargins margins)=>new[]{margins.Left,margins.Right,margins.Top,margins.Bottom}
        .Select(value=>value.ToString(CultureInfo.InvariantCulture)).ToArray();
}
