using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Resolved actual assets, not saved references or caller-authored dependency policies.</summary>
internal sealed class NativeFreshMobyFamilyPlacement
{
    private NativeFreshMobyFamilyPlacement(string id,NativeWizardFamilyAssetPlan? wizard,NativeMobyFamilyAssetPlan? passive,
        NativeMobyFamilyPassiveHostBinding? dispatch,int x,int y,int z,int yaw)
    {
        if(id is null||id.Length!=32||id.All(c=>c=='0')||id.Any(c=>c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("Resolved family placement requires a stable nonzero lowercase editor ID.");
        if(z is <0 or >0xFFFF||yaw is <0 or >255)
            throw new InvalidDataException("Resolved passive position/yaw exceeds the neutral authoring domain.");
        EditorId=id;WizardAsset=wizard;PassiveAsset=passive;Dispatch=dispatch;X=x;Y=y;Z=z;NativeYawByte=yaw;
    }
    internal string EditorId{get;}
    internal NativeWizardFamilyAssetPlan? WizardAsset{get;}
    internal NativeMobyFamilyAssetPlan? PassiveAsset{get;}
    internal NativeMobyFamilyPassiveHostBinding? Dispatch{get;}
    internal int X{get;} internal int Y{get;} internal int Z{get;} internal int NativeYawByte{get;}
    internal static NativeFreshMobyFamilyPlacement RetainedWizard(string id,NativeWizardFamilyAssetPlan asset)
    {ArgumentNullException.ThrowIfNull(asset);return new(id,asset,null,null,0,0,0,0);}
    internal static NativeFreshMobyFamilyPlacement Passive(string id,NativeMobyFamilyAssetPlan asset,
        NativeMobyFamilyPassiveHostBinding dispatch,int x,int y,int z,int nativeYawByte)
    {ArgumentNullException.ThrowIfNull(asset);ArgumentNullException.ThrowIfNull(dispatch);return new(id,null,asset,dispatch,x,y,z,nativeYawByte);}
}

/// <summary>
/// Actual-family storage composition using the retail/shared dependency allocator,
/// registration grammar and multi-subfile growth kernel. The retained Host currently
/// admits its one Wizard at ordinal0 and the proven passive Grass source family.
/// These are closed family resolvers, not a new output writer or complete behavior
/// certificate. Only the separately checked, closed family research arm may
/// consume this result; structural composition alone grants no export scope.
/// </summary>
internal sealed class NativeFreshMobyFamilyCompositionPlan
{
    private readonly byte[] _body,_scene,_inverse;
    private readonly NativeLevelDataGrowthLayout _growth;
    private readonly IReadOnlyList<NativeMobyDependencyClosurePlan> _graphs;
    private NativeFreshMobyFamilyCompositionPlan(NativeId65FreshWizardResearchBodyPlan body,NativeFreshMobyFamilyPlacement[] placements)
    {
        BaseBodyAncestor=body;Placements=Array.AsReadOnly(placements);RequireAncestry(body);
        byte[] baseline=body.CopyLevelData();var oldScene=body.HostAncestor.SceneAncestor;
        byte[] baselineScene=oldScene.CopyScene();var wizard=oldScene.FamilyAncestor;
        NativeMobyFamilyAssetPlan? passive=placements.Skip(1).Select(p=>p.PassiveAsset).FirstOrDefault();
        // Equal reissued sources must be resolved to one actual owner explicitly,
        // not merged merely because a caller supplied matching names or hashes.
        if(placements.Skip(1).Any(p=>!ReferenceEquals(p.PassiveAsset,passive)))
            throw new InvalidDataException("Resolve one actual source owner per passive family before composing repeated instances.");
        var wizardRows=wizard.CapturePrivateStorageRows();var wizardPointers=wizard.CapturePrivateStorageInternalPointers();
        passive?.RequireRetainedOwners();
        RowSpans=NativeFreshMobyFamilyRowSpanPlan.Create(placements.Select(p=>new NativeFreshMobyFamilyRowSpanRequest(p.EditorId,
            p.WizardAsset!=null?wizardRows:passive!.InstanceTemplate.Rows,p.WizardAsset!=null?wizardPointers:[])).ToArray());
        // Current family resolvers own one Wizard/Grass row each. The neutral
        // projection supports larger schemas without admitting new behaviors.
        if(RowSpans.Placements.Any(p=>p.RowCount!=1))
            throw new InvalidDataException("Current Wizard/Grass transform policies require their exact single-row source schemas.");
        Layout=RowSpans.Layout;
        int modelOffset=body.Descriptors[2].Offset,oldModelLength=body.Descriptors[2].ByteLength;
        byte[] appended=passive?.CopyPackage()??[];
        int modelLength=checked(oldModelLength+AlignSector(appended.Length));
        int finalLength=checked(baseline.Length+modelLength-oldModelLength+Layout.SceneByteLength-baselineScene.Length);
        FinalBodyDomain=new(NativeAddressKind.LevelData,"fresh-family-body:"+body.PlanSha256,NativeAddressRevision.Final,finalLength);
        FinalSceneDomain=new(NativeAddressKind.Scene,"fresh-family-scene:"+body.PlanSha256,NativeAddressRevision.Final,Layout.SceneByteLength);
        ModelRuntimeAddress=body.ModelRuntimeAddress;
        SceneRuntimeAddress=checked(ModelRuntimeAddress+(uint)modelLength);
        KnownSceneEndAddress=checked(SceneRuntimeAddress+(uint)Layout.SceneByteLength);
        if(KnownSceneEndAddress>body.NormalReservedLowerAddress||body.CopyBufferAddress+0x80000>body.NormalReservedLowerAddress)
            throw new InvalidDataException("Derived family owners exceed known native address bounds; these bounds are not peak-memory proof.");

        // Storage-dependency subset: source-form Wizard reads remain Original;
        // their already composed model bytes come only from the retained actual
        // Body/fallback producer. Never label generated model/scene bytes Original.
        var wizardReads=wizard.SourceSpans.Where(s=>s.OwnerId is "wizard.model" or "bolt.model" or "player.animation8")
            .Select(s=>new NativeMobyDependencyRead(s.OwnerId,new(wizard.OriginalDataDomain,
                s.SourceWadOffset-NativeWizardFamilyAssetSource.DonorWadOffset,s.ByteLength),s.Sha256)).ToArray();
        if(wizardReads.Length!=3||wizardReads.Select(r=>r.Id).Distinct().Count()!=3)
            throw new InvalidDataException("Wizard storage requires its three complete actual original model/animation owners.");
        byte[] retainedModels=baseline.AsSpan(modelOffset,oldModelLength).ToArray();
        var wizardGraph=NativeMobyDependencyClosureCompiler.Compile([
            new("retained-host.models",NativeMobyDependencyKind.ActorPackage,NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable,body.PlanSha256,[],wizardReads,
                context=>
                {
                    foreach(var read in wizardReads)
                        if(!context.Read(read.Id).SequenceEqual(wizard.CopyOwner(read.Id)))
                            throw new InvalidDataException("Actual Wizard source-form model owner changed.");
                    body.RequireExactOutputs(baseline,body.CopyOverlay(),body.CopyInitializerPatch(),body.CopyTransferSourceImage());
                    return new(retainedModels,[new("retained-model-allocation",new(FinalBodyDomain,modelOffset,oldModelLength))],[]);
                })], ["retained-host.models"],wizard.ReadPrivateSource);
        var wizardTemplate=wizard.CapturePrivateStorageTemplate(wizardGraph);
        NativeMobyDependencyInstanceTemplate? passiveTemplate=null;
        List<NativeMobyDependencyClosurePlan> graphs=[wizardGraph];
        if(passive is not null)
        {
            wizard.RequireNonSharedModelClass(passive.NativeClass);
            var package=passive.AssetGraph.Nodes.Single(n=>n.Id=="grass.package");
            var dispatch=passive.AssetGraph.Nodes.Single(n=>n.Id=="grass.donor-dispatch");
            var graph=NativeMobyDependencyClosureCompiler.Compile([
                new(dispatch.Id,dispatch.Kind,dispatch.Resolution,dispatch.Sharing,dispatch.EvidenceSha256,dispatch.Dependencies,dispatch.Reads),
                new(package.Id,package.Kind,package.Resolution,package.Sharing,package.EvidenceSha256,package.Dependencies,package.Reads,
                    context=>new(context.Read("complete-registration"),[new("registered-passive-package",
                        new(FinalBodyDomain,modelOffset+oldModelLength,appended.Length))],[]))], [package.Id],passive.ReadSource);
            passiveTemplate=NativeMobyDependencyInstanceTemplate.Capture(passive.InstanceTemplate.Policy,
                passive.InstanceTemplate.Rows,passive.InstanceTemplate.Globals,graph,passive.ReadSource);
            graphs.Add(graph);
        }
        _graphs=graphs.AsReadOnly();
        var requests=placements.Select(p=>new NativeMobyDependencyInstantiationRequest(p.EditorId,
            p.WizardAsset!=null?wizardTemplate:passiveTemplate!)).ToArray();
        RowSpans.RequireTemplates(requests);
        Allocation=Layout.Allocate(FinalSceneDomain,requests,[FinalBodyDomain,FinalSceneDomain],wizard.OriginalExecutableDomain);
        RowSpans.RequireAllocation(Allocation);
        var storage=Layout.CompileAllocated(baselineScene.AsSpan(0,0xCC),Allocation,RowSpans.PointerReferences,ReadSource);
        _scene=storage.CopyScene();
        foreach(var mutation in oldScene.Mutations)
        {
            int at=mutation.SceneOffset>=0xF88?Layout.PrivateOffsets[RowSpans.Placements[0].FirstRowIndex]+mutation.SceneOffset-0xF88:mutation.SceneOffset;
            if(Word(_scene,at)!=mutation.Before)throw new InvalidDataException("Retained authored Wizard coordinate preimage changed.");
            Put(_scene,at,mutation.After);
        }
        for(int i=1;i<placements.Length;i++)
        {
            var p=placements[i];int at=checked((int)Allocation.Rows[RowSpans.Placements[i].FirstRowIndex].Row.Offset);
            Put(_scene,at+12,p.X);Put(_scene,at+16,p.Y);Put(_scene,at+20,p.Z);
            Moby.YawByteToMatrixBytes(p.NativeYawByte).CopyTo(_scene,at+0x20);_scene[at+0x46]=(byte)p.NativeYawByte;
        }
        // Payload WAD locators are unused by the neutral outer-layout kernel;
        // this detached Body deliberately has no claimed physical archive slot.
        List<NativeSkyRelocationPayload> payloads=[];
        if(passive!=null)payloads.Add(new(0,0,0,0,oldModelLength,0,appended,2,false));
        payloads.Add(new(1,0,0,0,0,baselineScene.Length,_scene,3,false));
        _growth=new(baseline.AsSpan(0,0x800),baseline.Length,payloads);
        _body=_growth.Expand(baseline);
        if(_body.Length!=finalLength||_growth.Subfiles[2].FinalLength!=modelLength||
            _growth.Subfiles[3].FinalLength!=Layout.SceneByteLength)
            throw new InvalidDataException("Shared growth geometry disagrees with the family storage allocation.");
        var registrations=NativeMobyRegistrationTableLayout.Decode(_body.AsSpan(0,0x800),modelOffset,modelLength);
        if(passive!=null)registrations=registrations.Append(passive.NativeClass,(uint)(modelOffset+oldModelLength));
        registrations.CopyHeader().CopyTo(_body,0);
        foreach(var graph in _graphs)
        foreach(var install in graph.Installs)
            if(!ReferenceEquals(install.Destination.Space,FinalBodyDomain)||!graph.CopyPayload(install.NodeId).AsSpan().SequenceEqual(
                _body.AsSpan((int)install.Destination.Offset,(int)install.Destination.ByteLength)))
                throw new InvalidDataException("A shared family model dependency is not installed in its exact actual final owner.");
        _inverse=Inverse(baseline,baselineScene,storage);
        if(!_inverse.AsSpan().SequenceEqual(baseline))throw new InvalidDataException("Family layout failed complete inverse to its actual base Body.");
        LevelDataSha256=Hash(_body);SceneSha256=Hash(_scene);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            profile="native-fresh-family-storage-composition-v1",baseBody=body.PlanSha256,
            placements=placements.Select(p=>new{p.EditorId,family=p.WizardAsset?.PlanSha256??p.PassiveAsset!.PlanSha256,
                dispatch=p.Dispatch?.PlanSha256,p.X,p.Y,p.Z,p.NativeYawByte}),
            LevelDataSha256,SceneSha256,ModelRuntimeAddress,SceneRuntimeAddress,KnownSceneEndAddress,
            graphs=_graphs.Select(g=>g.Fingerprint),nativeCapacity=Layout.DynamicCapacity,
            completeBehavior=false,runtime=false,publication=false}));
        RequireAncestry(body);

        byte[] ReadSource(NativeByteExtent extent)=>ReferenceEquals(extent.Space,wizard.OriginalDataDomain)||
            ReferenceEquals(extent.Space,wizard.OriginalExecutableDomain)?wizard.ReadPrivateSource(extent):
            passive?.ReadSource(extent)??throw new InvalidDataException("Family read has no actual retained original owner.");
    }

    internal NativeId65FreshWizardResearchBodyPlan BaseBodyAncestor{get;}
    internal IReadOnlyList<NativeFreshMobyFamilyPlacement> Placements{get;}
    internal NativeFreshMobyFamilyRowSpanPlan RowSpans{get;}
    internal NativeFreshMobySceneStorageLayout Layout{get;}
    internal NativeMobyDependencyInstantiationPlan Allocation{get;}
    internal NativeAddressSpace FinalBodyDomain{get;}
    internal NativeAddressSpace FinalSceneDomain{get;}
    internal uint ModelRuntimeAddress{get;} internal uint SceneRuntimeAddress{get;} internal uint KnownSceneEndAddress{get;}
    internal string LevelDataSha256{get;} internal string SceneSha256{get;} internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool CompleteNativeBehaviorProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal byte[] CopyLevelData()=>_body.ToArray();
    internal byte[] CopyScene()=>_scene.ToArray();
    internal byte[] CopyInverseBaseBody()=>_inverse.ToArray();
    internal void RequireExactLevelData(ReadOnlySpan<byte> bytes)
    {RequireAncestry(BaseBodyAncestor);if(!bytes.SequenceEqual(_body))throw new InvalidDataException("Family composed body changed an owned byte or complement.");}
    internal void RequireAncestry(NativeId65FreshWizardResearchBodyPlan body)
    {
        if(!ReferenceEquals(body,BaseBodyAncestor))throw new InvalidDataException("Family composition belongs to another actual base Body.");
        var host=body.HostAncestor;body.RequireAncestry(host);
        body.RequireExactOutputs(body.CopyLevelData(),body.CopyOverlay(),body.CopyInitializerPatch(),body.CopyTransferSourceImage());
        if(Placements.Count==0||!ReferenceEquals(Placements[0].WizardAsset,host.SceneAncestor.FamilyAncestor)||
            Placements.Skip(1).Any(p=>p.WizardAsset!=null||p.PassiveAsset==null||p.Dispatch==null))
            throw new InvalidDataException("Current actual Host requires its retained Wizard ordinal0 followed by resolved passive family instances.");
        var floor=host.SceneAncestor.CompilationAncestor.Intent;
        foreach(var p in Placements.Skip(1))
        {
            p.Dispatch!.RequireAncestry(host,p.PassiveAsset!);
            if(p.X<=floor.FloorX||p.X>=floor.FloorX+floor.FloorWidth||p.Y<=floor.FloorY||p.Y>=floor.FloorY+floor.FloorDepth)
                throw new InvalidDataException("Passive authored point must lie inside the floor; no collision clearance is implied.");
        }
    }
    internal static NativeFreshMobyFamilyCompositionPlan Compose(NativeId65FreshWizardResearchBodyPlan body,
        IReadOnlyList<NativeFreshMobyFamilyPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(body);ArgumentNullException.ThrowIfNull(placements);
        if(placements.Count is <1 or >128)throw new InvalidDataException("Family composition exceeds its bounded authoring count.");
        var copy=placements.Take(129).ToArray();
        if(copy.Length!=placements.Count||copy.Any(p=>p==null)||copy.Select(p=>p.EditorId).Distinct(StringComparer.Ordinal).Count()!=copy.Length)
            throw new InvalidDataException("Family composition needs complete unique ordered placement identities.");
        return new(body,copy);
    }

    private byte[] Inverse(byte[] baseline,byte[] baselineScene,NativeFreshMobySceneStoragePlan storage)
    {
        byte[] inverse=new byte[baseline.Length];_body.AsSpan(0,0x800).CopyTo(inverse);
        foreach(var part in _growth.Subfiles)
        {
            Put(inverse,part.Index*8,part.OriginalOffset);Put(inverse,part.Index*8+4,part.OriginalLength);
            if(part.Index!=3)_body.AsSpan(part.FinalOffset,part.OriginalLength).CopyTo(inverse.AsSpan(part.OriginalOffset));
        }
        if(Placements.Count>1)
        {
            int oldCount=BaseBodyAncestor.ModelRoots.Count;
            inverse.AsSpan(0x50+oldCount*4,4).Clear();inverse.AsSpan(0x150+oldCount*2,2).Clear();
        }
        // Recover the complete old scene from actual retained row/private bytes;
        // the old generated storage is rebuilt by the same neutral grammar.
        byte[] recovered=NativeFreshMobySceneStorageLayout.Create([0x50],32,2).Compile(_scene.AsSpan(0,0xCC),
            [new("retained-wizard",_scene.AsSpan(0xD4,0x58),_scene.AsSpan(Layout.PrivateOffsets[0],0x50))],[
                new(0,NativeFreshMobySceneOwnerKind.Row,0,0,NativeFreshMobySceneOwnerKind.Private,0,(uint)Layout.PrivateOffsets[0]),
                new(0,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x28,(uint)(Layout.PrivateOffsets[0]+0x28))]).CopyScene();
        if(!recovered.AsSpan().SequenceEqual(baselineScene)||!ReferenceEquals(storage.Allocation,Allocation))
            throw new InvalidDataException("Family inverse lost retained Wizard bytes or its actual private allocator.");
        recovered.CopyTo(inverse,_growth.Subfiles[3].OriginalOffset);
        return inverse;
    }
    private static int AlignSector(int n)=>checked((n+0x7FF)&~0x7FF);
    private static int Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
