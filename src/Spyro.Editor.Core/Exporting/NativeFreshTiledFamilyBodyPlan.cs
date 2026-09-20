using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-derived, child-relative tiled family Body. Rebinds shared storage and
/// reconstructs the complete authored Scene in fresh Final domains. It does not
/// adopt the old Body's runtime certificate, issue an executable/disc, or prove
/// native loading, seam traversal, reward trajectory or publication readiness.
/// </summary>
internal sealed class NativeFreshTiledFamilyBodyPlan
{
    private readonly byte[] _body,_scene,_inverse,_header,_dataAllocation;
    private NativeFreshTiledFamilyBodyPlan(NativeFreshTiledFamilyTerrainRevision terrain)
    {
        TerrainAncestor=terrain;
        terrain.RequireAncestry(terrain.IntentAncestor,terrain.PlacementAncestor);
        var placement=PlacementAncestor;var prior=placement.BodyAncestor.SceneAncestor;
        byte[] before=placement.CopyLevelData(),oldScene=placement.CopyScene();
        placement.RequireExactLevelData(before);placement.RequireExactScene(oldScene);
        _header=before.AsSpan(0,0x800).ToArray();_dataAllocation=PaddedData(terrain);
        StorageSource=NativeFreshLockedChestSceneStorageSource.CaptureForTerrainRevision(terrain);
        FinalBodyDomain=StorageSource.FinalBodyDomain;
        FinalSceneDomain=new(NativeAddressKind.Scene,"fresh-tiled-family-scene:"+terrain.PlanSha256,
            NativeAddressRevision.Final,oldScene.Length);
        var wizard=ResourceAncestor.Placements[0].WizardAsset!;
        var ids=terrain.IntentAncestor.PlacementIds;
        var requests=new NativeMobyDependencyInstantiationRequest[]{
            new(ids[0],StorageSource.WizardTemplate),new(ids[1],StorageSource.PassiveTemplate),new(ids[2],StorageSource.ChestTemplate)};
        RowSpans=NativeFreshMobyFamilyRowSpanPlan.Create([
            new(ids[0],StorageSource.WizardTemplate.Rows,wizard.CapturePrivateStorageInternalPointers()),
            new(ids[1],StorageSource.PassiveTemplate.Rows,[]),new(ids[2],StorageSource.ChestTemplate.Rows,[])]);
        SceneLayout=RowSpans.Layout;RowSpans.RequireTemplates(requests);
        if(SceneLayout.PlacedCount!=9||SceneLayout.DynamicCapacity!=32||SceneLayout.PrivateByteLength!=0x13C||
            SceneLayout.FixupCount!=10||SceneLayout.SceneByteLength!=0x3800||
            !RowSpans.Placements.SequenceEqual(prior.RowSpans.Placements))
            throw new InvalidDataException("Tiled family Body lost the actual complete authored1+1+7 row identity and P9/N32 geometry.");
        var globals=StorageSource.Source.MutableGlobals;
        if(globals.Count!=2||globals.Any(g=>!ReferenceEquals(g.ExecutableDomain,globals[0].ExecutableDomain)))
            throw new InvalidDataException("Tiled family Body must retain both globals on the actual shared Original executable owner.");
        Allocation=SceneLayout.Allocate(FinalSceneDomain,requests,[FinalBodyDomain,FinalSceneDomain],globals[0].ExecutableDomain);
        RowSpans.RequireAllocation(Allocation);
        _scene=SceneLayout.CompileAllocated(oldScene.AsSpan(0,0xCC),Allocation,RowSpans.PointerReferences,StorageSource.ReadSource).CopyScene();
        NativeFreshLockedChestSceneExtension.PreserveAuthoredRows(oldScene,prior.Allocation,prior.RowSpans.PointerReferences,_scene,Allocation);
        placement.RequireExactScene(_scene);
        if(!_scene.AsSpan().SequenceEqual(oldScene))
            throw new InvalidDataException("Rebound tiled family Scene changed a complete authored row/private/owner/fixup/reserve or padding byte.");

        var layout=Layout;_body=layout.Expand(before);
        int dataAt=Word(_header,8),dataLength=Word(_header,12),sceneAt=Word(_header,24),sceneLength=Word(_header,28);
        if(layout.Growth!=0||layout.Subfiles.Count!=8||_body.Length!=before.Length||_body.Length!=0x20F000||
            dataAt!=0xE6000||dataLength!=0x1000||sceneAt!=0x102000||sceneLength!=_scene.Length||
            !_body.AsSpan(0,dataAt).SequenceEqual(before.AsSpan(0,dataAt))||
            !_body.AsSpan(dataAt+dataLength).SequenceEqual(before.AsSpan(dataAt+dataLength))||
            !_body.AsSpan(dataAt,dataLength).SequenceEqual(_dataAllocation)||
            !_body.AsSpan(sceneAt,sceneLength).SequenceEqual(_scene))
            throw new InvalidDataException("Tiled family Body changed its zero-growth complete data replacement or any retained owner.");
        // The new allocation has genuinely rebuilt the same Scene. Install that
        // complete result, even though exact readback already proves equality.
        _scene.CopyTo(_body,sceneAt);
        foreach(var install in StorageSource.Graph.Installs)
            if(!ReferenceEquals(install.Destination.Space,FinalBodyDomain)||
                !StorageSource.Graph.CopyPayload(install.NodeId).AsSpan().SequenceEqual(
                    _body.AsSpan(checked((int)install.Destination.Offset),checked((int)install.Destination.ByteLength))))
                throw new InvalidDataException("Shared storage payload is not the actual installed fresh Final Body owner.");
        _inverse=_body.ToArray();terrain.RevertExactDataAllocation(_body.AsSpan(dataAt,dataLength)).CopyTo(_inverse,dataAt);
        placement.RequireExactLevelData(_inverse);
        if(!_inverse.AsSpan().SequenceEqual(before))throw new InvalidDataException("Tiled family Body lost its complete immediate placed-child inverse.");

        RuntimeMemory=NativeLevelRuntimeMemoryReport.DecodeFixture(65,_body,HostAncestor.AppendLayout.CopyAnchor);
        var memory=RuntimeMemory;var basis=ResourceAncestor.BaseBodyAncestor;
        // The report stores complete Body offsets, not offsets within subfile1.
        if(memory.CopyBufferAddress!=0x8008CA38||memory.UsedLevelDataBytes!=0xCB8||memory.LevelDataSectorPaddingBytes!=0x348||
            memory.ModelBytesRead!=0x1B000||memory.ModelRamStart!=0x8008D6F0||memory.SceneBytesRead!=0x3800||
            memory.SceneRamStart!=0x800A86F0||memory.SceneReadEnd!=0x800ABEF0||
            memory.ParticleComponentDataOffset!=dataAt+0x50C||memory.SoundComponentDataOffset!=dataAt+0x598||
            memory.PlacedMobyCount!=9||memory.DynamicMobyCapacity!=32||
            memory.NormalReservedBufferStart!=basis.NormalReservedLowerAddress||!memory.KnownPostLoadExtentsDisjoint||
            (long)memory.CopyBufferAddress+0x80000>basis.NormalReservedLowerAddress||
            memory.NativeSourceCertified||memory.RuntimeFitProven||memory.PeakTransientFitProven)
            throw new InvalidDataException("Tiled family Body normal-load accounting disagrees with its actual revised data/retained Host/model/Scene owners.");
        LevelDataSha256=Hash(_body);SceneSha256=Hash(_scene);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="source-derived-child-relative-tiled-family-body-v1",Terrain=terrain.PlanSha256,
            Placement=placement.PlanSha256,Resources=ResourceAncestor.PlanSha256,Host=HostAncestor.PlanSha256,
            LevelDataSha256,SceneSha256,PriorBodySha256=Hash(_inverse),HeaderSha256=Hash(_header),
            SharedStorage=StorageSource.Graph.Fingerprint,Rows=RowSpans.Placements,SceneLayout.DynamicCapacity,
            SceneLayout.PrivateByteLength,SceneLayout.FixupCount,memory.CopyBufferAddress,memory.UsedLevelDataBytes,
            memory.ModelRamStart,memory.SceneRamStart,memory.SceneReadEnd,memory.NormalReservedBufferStart,
            SourceCertified,FinalExecutableIssued,RuntimeVerified,PublicationApproved,
            CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven}));
        RequireAncestry(terrain);
    }

    internal NativeFreshTiledFamilyTerrainRevision TerrainAncestor{get;}
    internal NativeFreshLockedChestPlacementPlan PlacementAncestor=>TerrainAncestor.PlacementAncestor;
    internal NativeFreshMobyFamilyCompositionPlan ResourceAncestor=>TerrainAncestor.ResourceAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor=>TerrainAncestor.HostAncestor;
    internal NativeFreshLockedChestSceneStorageSource StorageSource{get;}
    internal NativeFreshMobyFamilyRowSpanPlan RowSpans{get;}
    internal NativeFreshMobySceneStorageLayout SceneLayout{get;}
    internal NativeMobyDependencyInstantiationPlan Allocation{get;}
    internal NativeAddressSpace FinalBodyDomain{get;}
    internal NativeAddressSpace FinalSceneDomain{get;}
    internal NativeLevelDataGrowthLayout Layout=>BuildLayout(_header,0x20F000,_dataAllocation.ToArray());
    internal NativeLevelRuntimeMemoryReport RuntimeMemory{get;}
    internal string LevelDataSha256{get;}
    internal string SceneSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool FinalExecutableIssued=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal byte[] CopyScene()=>_scene.ToArray();
    internal void RequireExactLevelData(ReadOnlySpan<byte> body)
    {if(!body.SequenceEqual(_body))throw new InvalidDataException("Complete tiled family Body changed.");}
    internal void RequireExactScene(ReadOnlySpan<byte> scene)
    {if(!scene.SequenceEqual(_scene))throw new InvalidDataException("Complete rebound placed family Scene changed.");}
    internal byte[] RevertExactLevelData(ReadOnlySpan<byte> body)
    {RequireExactLevelData(body);return _inverse.ToArray();}
    internal void RequireAncestry(NativeFreshTiledFamilyTerrainRevision terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if(!ReferenceEquals(terrain,TerrainAncestor))throw new InvalidDataException("Tiled family Body retains its actual terrain revision, not an equal reissue.");
        terrain.RequireAncestry(terrain.IntentAncestor,terrain.PlacementAncestor);
        StorageSource.RequireTerrainAncestry(terrain);
        if(!ReferenceEquals(FinalBodyDomain,StorageSource.FinalBodyDomain)||
            Allocation.Families.Any(f=>!ReferenceEquals(f.Template.Shared,StorageSource.Graph))||
            Allocation.Rows.Any(r=>!ReferenceEquals(r.Row.Space,FinalSceneDomain)||!ReferenceEquals(r.Properties.Space,FinalSceneDomain)))
            throw new InvalidDataException("Tiled family Body lost its actual shared storage/row/private Final domains.");
    }
    internal static NativeFreshTiledFamilyBodyPlan Compose(NativeFreshTiledFamilyTerrainRevision terrain)
    {ArgumentNullException.ThrowIfNull(terrain);return new(terrain);}

    // Closed neutral layout bridge used by the storage recapture arm. It only
    // derives bytes from the immutable actual revision; no caller payload or
    // domain can enter the source-owned graph through this method.
    internal static NativeLevelDataGrowthLayout CreateTerrainLayout(NativeFreshTiledFamilyTerrainRevision terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);terrain.RequireAncestry(terrain.IntentAncestor,terrain.PlacementAncestor);
        byte[] before=terrain.PlacementAncestor.CopyLevelData();
        if(before.Length!=0x20F000||Word(before,8)!=0xE6000||Word(before,12)!=0x1000)
            throw new InvalidDataException("Tiled terrain layout requires its complete fixed-capacity actual placed child.");
        return BuildLayout(before.AsSpan(0,0x800),before.Length,PaddedData(terrain));
    }
    private static NativeLevelDataGrowthLayout BuildLayout(ReadOnlySpan<byte> header,int length,byte[] data)=>
        new(header,length,[new(0,0,0,0,0,data.Length,data,1,false)]);
    private static byte[] PaddedData(NativeFreshTiledFamilyTerrainRevision terrain)
    {
        byte[] used=terrain.CopyUsedData();terrain.RequireExactUsedData(used);
        if(used.Length!=0xCB8)throw new InvalidDataException("Tiled family Body requires the exact complete family used-data grammar.");
        byte[] data=new byte[0x1000];used.CopyTo(data,0);return data;
    }
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
