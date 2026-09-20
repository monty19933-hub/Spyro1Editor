using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Installs the detached source-coordinate P9 scene in its actual resource
/// child. This is a child-relative Body transaction, not an Original-disc
/// inverse, authored placement, updated treasure census or final executable.
/// </summary>
internal sealed class NativeFreshLockedChestResearchBody
{
    private readonly byte[] _body,_inverse,_header,_scene;
    private NativeFreshLockedChestResearchBody(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,NativeFreshLockedChestSceneExtension scene)
    {
        ResourceAncestor=resources;HostAncestor=host;SceneAncestor=scene;
        RequireAncestry(resources,host,scene);
        byte[] before=resources.CopyLevelData(),oldScene=resources.CopyScene();_scene=scene.CopyScene();
        _header=before.AsSpan(0,0x800).ToArray();
        int at=Word(_header,0x18),length=Word(_header,0x1C);
        if(before.Length!=0x20F000||at!=0x102000||length!=0x3800||oldScene.Length!=length||_scene.Length!=length||
            !before.AsSpan(at,length).SequenceEqual(oldScene))
            throw new InvalidDataException("Chest Body requires the complete actual resource-child P2 scene owner and same-size P9 replacement.");
        var layout=CreateLayout(before.Length);_body=layout.Expand(before);
        if(layout.Growth!=0||layout.Subfiles.Count!=8||_body.Length!=before.Length||
            !_body.AsSpan(0,at).SequenceEqual(before.AsSpan(0,at))||
            !_body.AsSpan(at+length).SequenceEqual(before.AsSpan(at+length))||
            !_body.AsSpan(at,length).SequenceEqual(_scene))
            throw new InvalidDataException("Chest Body changed a byte outside its one complete scene replacement.");
        _inverse=_body.ToArray();scene.RevertExactScene(_body.AsSpan(at,length)).CopyTo(_inverse,at);
        resources.RequireExactLevelData(_inverse);
        if(!_inverse.AsSpan().SequenceEqual(before))throw new InvalidDataException("Chest Body inverse lost its actual immediate resource child.");
        uint anchor=host.AppendLayout.CopyAnchor;
        RuntimeMemory=NativeLevelRuntimeMemoryReport.DecodeFixture(65,_body,anchor);
        var memory=RuntimeMemory;var basis=resources.BaseBodyAncestor;
        if(anchor!=0x8008CA38||basis.NormalReservedLowerAddress!=memory.NormalReservedBufferStart||
            basis.ResidentPeteAddress!=0x801C5BB8||memory.UsedLevelDataBytes!=0xA6C||memory.ModelBytesRead!=0x1B000||
            memory.SceneBytesRead!=length||memory.ModelRamStart!=0x8008D4A4||memory.SceneRamStart!=0x800A84A4||
            memory.SceneReadEnd!=0x800ABCA4||memory.PlacedMobyCount!=9||memory.DynamicMobyCapacity!=32||
            !memory.KnownPostLoadExtentsDisjoint||(long)anchor+0x80000>basis.NormalReservedLowerAddress||
            memory.NativeSourceCertified||memory.RuntimeFitProven||memory.PeakTransientFitProven)
            throw new InvalidDataException("Chest Body normal-load accounting differs from its actual retained Host/resource bounds.");
        LevelDataSha256=Hash(_body);
        FinalBodyDomain=new(NativeAddressKind.LevelData,"fresh-chest-body:"+scene.PlanSha256,
            NativeAddressRevision.Final,_body.Length);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="child-relative-source-coordinate-fresh-chest-body-v1",ResourcePlan=resources.PlanSha256,
            HostPlan=host.PlanSha256,ScenePlan=scene.PlanSha256,LevelDataSha256,PriorBodySha256=Hash(_inverse),
            HeaderSha256=Hash(_header),scene.SceneSha256,memory.CopyBufferAddress,memory.UsedLevelDataBytes,
            memory.ModelBytesRead,memory.ModelRamStart,memory.SceneBytesRead,memory.SceneRamStart,memory.SceneReadEnd,
            memory.PlacedMobyCount,memory.DynamicMobyCapacity,memory.NormalReservedBufferStart,
            SourceCertified,FinalExecutableIssued,CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven,
            RuntimeVerified,PublicationApproved}));
    }

    internal NativeFreshMobyFamilyCompositionPlan ResourceAncestor{get;}
    internal NativeFreshLockedChestResearchHostExtension HostAncestor{get;}
    internal NativeFreshLockedChestSceneExtension SceneAncestor{get;}
    // The neutral layout exposes byte-array payloads. Return a detached layout
    // so diagnostic callers cannot mutate this Body's immutable source owners.
    internal NativeLevelDataGrowthLayout Layout=>CreateLayout(_body.Length);
    internal NativeLevelRuntimeMemoryReport RuntimeMemory{get;}
    internal NativeAddressSpace FinalBodyDomain{get;}
    internal string LevelDataSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool FinalExecutableIssued=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal void RequireAncestry(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,NativeFreshLockedChestSceneExtension scene)
    {
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(host);ArgumentNullException.ThrowIfNull(scene);
        if(!ReferenceEquals(resources,ResourceAncestor)||!ReferenceEquals(host,HostAncestor)||!ReferenceEquals(scene,SceneAncestor))
            throw new InvalidDataException("Chest Body belongs to another actual resource/Host/Scene tuple.");
        scene.RequireAncestry(resources,host);scene.RequireExactScene(scene.CopyScene());
        host.RequireExactOutputs(host.CopyOverlay(),host.CopyInitializer(),host.CopyExecutablePrototype());
        resources.RequireLockedChestResourceAncestry(host.CurrentExecutableAncestor.FamilyCompositionAncestor!,host.FamilySourceAncestor);
    }
    internal void RequireExactLevelData(ReadOnlySpan<byte> data)
    {
        RequireAncestry(ResourceAncestor,HostAncestor,SceneAncestor);
        if(!data.SequenceEqual(_body))throw new InvalidDataException("Complete chest Body afterimage or complement changed.");
    }
    internal byte[] RevertExactLevelData(ReadOnlySpan<byte> data)
    {RequireExactLevelData(data);return _inverse.ToArray();}
    internal static NativeFreshLockedChestResearchBody Compose(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,NativeFreshLockedChestSceneExtension scene)
    {
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(host);ArgumentNullException.ThrowIfNull(scene);
        return new(resources,host,scene);
    }
    private NativeLevelDataGrowthLayout CreateLayout(int length)=>new(_header,length,[new(0,0,0,0,0,_scene.Length,_scene,3,false)]);
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
