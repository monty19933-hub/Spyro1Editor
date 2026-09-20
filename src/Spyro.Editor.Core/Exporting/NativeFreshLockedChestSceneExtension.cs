using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached source-coordinate chest scene. The actual resource child and Host
/// remain separate owners: this does not install a Body, update its census, or
/// authorize gameplay. Original Peace chest positions are deliberately retained.
/// </summary>
internal sealed class NativeFreshLockedChestSceneExtension
{
    private readonly byte[] _scene,_inverse;
    private NativeFreshLockedChestSceneExtension(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,string chestEditorId)
    {
        ResourceAncestor=resources;HostAncestor=host;SourceAncestor=host.FamilySourceAncestor;
        RequireAncestry(resources,host);
        var wizard=resources.Placements[0].WizardAsset!;
        var source=NativeFreshLockedChestSceneStorageSource.Capture(resources,host);
        var requests=new NativeMobyDependencyInstantiationRequest[]{
            new(resources.Placements[0].EditorId,source.WizardTemplate),
            new(resources.Placements[1].EditorId,source.PassiveTemplate),
            new(chestEditorId,source.ChestTemplate)};
        RowSpans=NativeFreshMobyFamilyRowSpanPlan.Create([
            new(requests[0].Id,source.WizardTemplate.Rows,wizard.CapturePrivateStorageInternalPointers()),
            new(requests[1].Id,source.PassiveTemplate.Rows,[]),
            new(requests[2].Id,source.ChestTemplate.Rows,[])]);
        Layout=RowSpans.Layout;RowSpans.RequireTemplates(requests);
        if(Layout.PlacedCount!=9||Layout.DynamicCapacity!=32||Layout.PrivateByteLength!=0x13C||
            Layout.SceneByteLength!=0x3800||Layout.FixupCount!=10)
            throw new InvalidDataException("Closed chest scene source geometry changed.");
        FinalSceneDomain=new(NativeAddressKind.Scene,"fresh-chest-scene:"+resources.PlanSha256+":"+chestEditorId,
            NativeAddressRevision.Final,Layout.SceneByteLength);
        var globals=SourceAncestor.MutableGlobals;
        if(globals.Count!=2||!ReferenceEquals(globals[0].ExecutableDomain,globals[1].ExecutableDomain))
            throw new InvalidDataException("Chest singleton globals require one actual source executable domain.");
        Allocation=Layout.Allocate(FinalSceneDomain,requests,[resources.FinalBodyDomain,FinalSceneDomain],globals[0].ExecutableDomain);
        RowSpans.RequireAllocation(Allocation);
        byte[] old=resources.CopyScene();
        _scene=Layout.CompileAllocated(old.AsSpan(0,0xCC),Allocation,RowSpans.PointerReferences,source.ReadSource).CopyScene();

        // Preserve the complete *actual authored* Wizard/Grass owners, not a
        // second coordinate writer. Only their explicit scene pointers move.
        // The retained typed parent still validates these generated owners.
        PreserveAuthoredRows(old,resources.Allocation,resources.RowSpans.PointerReferences,_scene,Allocation);
        _inverse=RecoverPriorScene(_scene);
        if(!_inverse.AsSpan().SequenceEqual(old))throw new InvalidDataException("Chest scene failed complete inverse to its actual resource-parent scene.");
        SceneSha256=Hash(_scene);
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="source-coordinate-fresh-chest-scene-v1",Resources=resources.PlanSha256,Host=host.PlanSha256,
            ChestEditorId=chestEditorId,SceneSha256,SharedStorage=source.Graph.Fingerprint,
            Rows=RowSpans.Placements,Layout.DynamicCapacity,Layout.PrivateByteLength,Layout.FixupCount,
            SourceCertified,BodyIssued,AuthoredChestTransformProven,RuntimeVerified,PublicationApproved,CompleteNativeDependencyClosureProven}));
        RequireAncestry(resources,host);
    }

    internal NativeFreshMobyFamilyCompositionPlan ResourceAncestor{get;}
    internal NativeFreshLockedChestResearchHostExtension HostAncestor{get;}
    internal NativeLockedChestFamilySource SourceAncestor{get;}
    internal NativeFreshMobyFamilyRowSpanPlan RowSpans{get;}
    internal NativeFreshMobySceneStorageLayout Layout{get;}
    internal NativeMobyDependencyInstantiationPlan Allocation{get;}
    internal NativeAddressSpace FinalSceneDomain{get;}
    internal string SceneSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool BodyIssued=>false;
    internal bool AuthoredChestTransformProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal byte[] CopyScene()=>_scene.ToArray();
    internal void RequireAncestry(NativeFreshMobyFamilyCompositionPlan resources,NativeFreshLockedChestResearchHostExtension host)
    {
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(host);
        if(!ReferenceEquals(resources,ResourceAncestor)||!ReferenceEquals(host,HostAncestor)||
            !ReferenceEquals(host.FamilySourceAncestor,SourceAncestor))
            throw new InvalidDataException("Chest scene belongs to another actual resource/Host/source tuple.");
        var parent=host.CurrentExecutableAncestor.FamilyCompositionAncestor??
            throw new InvalidDataException("Chest scene requires the actual current mixed Family executable.");
        host.RequireAncestry(host.CurrentExecutableAncestor,SourceAncestor);
        resources.RequireLockedChestResourceAncestry(parent,SourceAncestor);
        if(resources.Placements.Count!=2||resources.Layout.PlacedCount!=2||resources.Allocation.Rows.Count!=2||
            resources.BaseBodyAncestor.MixedResourceAncestor is null)
            throw new InvalidDataException("Chest scene requires the retained mixed Wizard/Grass two-row owner.");
    }
    internal void RequireExactScene(ReadOnlySpan<byte> scene)
    {
        RequireAncestry(ResourceAncestor,HostAncestor);
        if(!scene.SequenceEqual(_scene))throw new InvalidDataException("Complete chest scene afterimage or complement changed.");
    }
    internal byte[] RevertExactScene(ReadOnlySpan<byte> scene)
    {RequireExactScene(scene);return _inverse.ToArray();}
    private byte[] RecoverPriorScene(byte[] scene)
    {
        var oldLayout=ResourceAncestor.Layout;
        var rows=Enumerable.Range(0,2).Select(i=>new NativeFreshMobySceneStorageRow(ResourceAncestor.Placements[i].EditorId,
            scene.AsSpan(checked((int)Allocation.Rows[i].Row.Offset),0x58),
            scene.AsSpan(Layout.PrivateOffsets[i],Layout.PrivateByteLengths[i]))).ToArray();
        var pointers=ResourceAncestor.RowSpans.PointerReferences.Select(p=>p with{
            ExpectedSourceValue=checked((uint)((p.TargetOwner==NativeFreshMobySceneOwnerKind.Row?
                Allocation.Rows[p.TargetRowIndex].Row.Offset:Allocation.Rows[p.TargetRowIndex].Properties.Offset)+p.TargetOffset))}).ToArray();
        return oldLayout.Compile(scene.AsSpan(0,0xCC),rows,pointers).CopyScene();
    }
    internal static NativeFreshLockedChestSceneExtension Compose(NativeFreshMobyFamilyCompositionPlan resources,
        NativeFreshLockedChestResearchHostExtension host,string chestEditorId)
    {
        ArgumentNullException.ThrowIfNull(resources);ArgumentNullException.ThrowIfNull(host);
        // Reject invalid identity before doing any source/storage materialization.
        if(chestEditorId is not {Length:32}||!chestEditorId.Any(c=>c!='0')||
            chestEditorId.Any(c=>c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))||
            resources.Placements.Any(p=>p.EditorId==chestEditorId))
            throw new InvalidDataException("Chest scene requires a distinct stable lowercase editor identity.");
        return new(resources,host,chestEditorId);
    }
    // Shared byte mechanics only. The two callers first validate their actual
    // typed source/placement owners; this helper cannot issue scene authority.
    internal static void PreserveAuthoredRows(byte[] old,NativeMobyDependencyInstantiationPlan priorAllocation,
        IReadOnlyList<NativeFreshMobyScenePointerReference> pointers,byte[] scene,NativeMobyDependencyInstantiationPlan allocation)
    {
        for(int i=0;i<priorAllocation.Rows.Count;i++)
        {
            var prior=priorAllocation.Rows[i];var next=allocation.Rows[i];
            byte[] row=old.AsSpan(checked((int)prior.Row.Offset),0x58).ToArray();
            byte[] properties=old.AsSpan(checked((int)prior.Properties.Offset),checked((int)prior.Properties.ByteLength)).ToArray();
            if(prior.Source.Row!=next.Source.Row||prior.Source.Properties!=next.Source.Properties||
                prior.Properties.ByteLength!=next.Properties.ByteLength||Word(row,0)!=prior.Properties.Offset)
                throw new InvalidDataException("Retained authored scene lost its actual source schema/private owner.");
            Put(row,0,checked((int)next.Properties.Offset));
            foreach(var pointer in pointers.Where(p=>p.FieldRowIndex==i&&
                !(p.FieldOwner==NativeFreshMobySceneOwnerKind.Row&&p.FieldOffset==0)))
            {
                if(pointer.TargetRowIndex>=priorAllocation.Rows.Count)throw new InvalidDataException("Retained pointer cannot acquire a new chest target.");
                var priorTarget=priorAllocation.Rows[pointer.TargetRowIndex];
                var nextTarget=allocation.Rows[pointer.TargetRowIndex];
                byte[] field=pointer.FieldOwner==NativeFreshMobySceneOwnerKind.Row?row:properties;
                long before=(pointer.TargetOwner==NativeFreshMobySceneOwnerKind.Row?priorTarget.Row.Offset:priorTarget.Properties.Offset)+pointer.TargetOffset;
                long after=(pointer.TargetOwner==NativeFreshMobySceneOwnerKind.Row?nextTarget.Row.Offset:nextTarget.Properties.Offset)+pointer.TargetOffset;
                if(Word(field,pointer.FieldOffset)!=before)throw new InvalidDataException("Retained authored pointer preimage changed.");
                Put(field,pointer.FieldOffset,checked((int)after));
            }
            row.CopyTo(scene,checked((int)next.Row.Offset));properties.CopyTo(scene,checked((int)next.Properties.Offset));
        }
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes[at..],value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
