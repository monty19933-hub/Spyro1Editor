using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached used-data revision of one actual R03 family child. The entire tiled
/// document and the installed source owners remain ancestors; no legacy/P0
/// substitute document, reallocation, new Body/domain or executable is issued.
/// Source certification covers this byte composition, not its runtime placement.
/// </summary>
internal sealed class NativeFreshTiledFamilyTerrainRevision
{
    private const int MetadataLength=0x178,OldSurfaceLength=0x144,OldUsedLength=0xA6C,
        SurfaceLength=0x390,TailLength=0x7B0,AllocationLength=0x1000;
    private readonly byte[] _used,_priorAllocation;

    private NativeFreshTiledFamilyTerrainRevision(NativeFreshTiledFamilyTerrainIntent intent,
        NativeFreshLockedChestPlacementPlan placement)
    {
        IntentAncestor=intent;PlacementAncestor=placement;
        var mixed=RequireInputs(intent,placement);
        MaterialSources=Array.AsReadOnly(mixed.MaterialSources.ToArray());
        byte[] body=placement.CopyLevelData();placement.RequireExactLevelData(body);
        if(body.Length!=0x20F000)throw new InvalidDataException("Tiled family data requires the complete current R03 child allocation.");
        int cursor=0x800;
        for(int index=0;index<8;index++)
        {
            int at=Word(body,index*8),length=Word(body,index*8+4);
            if(at!=cursor||length<=0||length>body.Length-at)
                throw new InvalidDataException("Actual child descriptors must partition every retained subfile without overlap or omission.");
            cursor=checked(at+length);
        }
        int dataAt=Word(body,8),dataLength=Word(body,12);
        if(cursor!=body.Length||body.AsSpan(64,16).IndexOfAnyExcept((byte)0)>=0||
            dataAt!=0xE6000||dataLength!=AllocationLength||Word(body,4)!=0xE5800||
            Word(body,20)!=0x1B000||Word(body,24)!=0x102000||Word(body,28)!=0x3800||
            !Part(body,3).AsSpan().SequenceEqual(placement.CopyScene()))
            throw new InvalidDataException("Tiled family data lost the actual final chest upload/model/placed-scene owners.");
        _priorAllocation=body.AsSpan(dataAt,dataLength).ToArray();
        byte[] oldUsed=mixed.CopyDataComponents();
        if(oldUsed.Length!=OldUsedLength||!_priorAllocation.AsSpan(0,OldUsedLength).SequenceEqual(oldUsed)||
            _priorAllocation.AsSpan(OldUsedLength).IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidDataException("Current child data must retain the complete actual mixed used data and zero allocation complement.");

        // Native records are split: all LQ rows precede all HQ rows. Never
        // interpret this table as contiguous184-byte record slices.
        var records=NativeTerrainMaterialTableCodec.Decode(oldUsed.AsSpan(0,MetadataLength));
        if(records.Count!=2||mixed.Allocation.Records.Count!=2||
            !records.Zip(mixed.Allocation.Records).All(p=>p.First.CopyRecord().AsSpan().SequenceEqual(p.Second.CopyRecord()))||
            !NativeTerrainMaterialTableCodec.Encode(mixed.Allocation.Records.ToArray()).AsSpan().SequenceEqual(oldUsed.AsSpan(0,MetadataLength)))
            throw new InvalidDataException("Actual installed material metadata no longer contains its two complete allocated source records.");
        RequireSurfaceLengths(oldUsed,MetadataLength,[0xAC,0x20,8,0x60,0x10],MetadataLength+OldSurfaceLength);
        int oldTail=MetadataLength+OldSurfaceLength;
        if(Word(oldUsed,oldTail)!=0||Word(oldUsed,oldTail+4)!=0x8C||Word(oldUsed,oldTail+8)!=11||
            Word(oldUsed,oldTail+0x90)!=0x720||Word(oldUsed,oldTail+0x194)!=78||
            oldUsed.Length-oldTail!=TailLength)
            throw new InvalidDataException("The actual family portal/eleven-particle/78-sound suffix must remain complete; a P0 tail is not interchangeable.");
        byte[] surface=intent.CopyBoundSurface();intent.RequireExactBoundSurface(surface);
        if(surface.Length!=SurfaceLength)throw new InvalidDataException("Exact four-tile family surface size changed.");
        RequireSurfaceLengths(surface,0,[0x298,0x24,8,0xBC,0x10],SurfaceLength);
        _used=new byte[checked(MetadataLength+surface.Length+TailLength)];
        oldUsed.AsSpan(0,MetadataLength).CopyTo(_used);
        surface.CopyTo(_used,MetadataLength);
        oldUsed.AsSpan(oldTail,TailLength).CopyTo(_used.AsSpan(MetadataLength+surface.Length));
        if(_used.Length!=0xCB8||_used.Length>AllocationLength||
            !_used.AsSpan(0,MetadataLength).SequenceEqual(_priorAllocation.AsSpan(0,MetadataLength))||
            !_used.AsSpan(MetadataLength+surface.Length).SequenceEqual(_priorAllocation.AsSpan(oldTail,TailLength)))
            throw new InvalidDataException("Detached used-data composition changed its complete metadata or family-tail complement.");

        // Capture consumer evidence from the actual shared Wizard/Chest Original
        // executable owner, never from the generated Host prototype.
        var wizard=ResourceAncestor.BaseBodyAncestor.HostAncestor.SceneAncestor.FamilyAncestor;
        byte[] original=wizard.ReadPrivateSource(new(wizard.OriginalExecutableDomain,0,wizard.OriginalExecutableDomain.ByteLength));
        var surfaceEvidence=NativeId65AuthoredSurfaceConsumers.Capture(original);
        var materialEvidence=NativeId65FreshTerrainTexturePlan.CaptureConsumerEvidence(original);
        CanonicalDocumentSha256=intent.CanonicalDocumentSha256;
        DataSha256=Hash(_used);PriorDataAllocationSha256=Hash(_priorAllocation);
        RetainedBodySha256=Hash(body);HeaderSha256=Hash(body.AsSpan(0,0x800));
        UploadSha256=Hash(Part(body,0));ModelSha256=Hash(Part(body,2));SceneSha256=Hash(Part(body,3));
        using var complement=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        complement.AppendData(body.AsSpan(0,dataAt));complement.AppendData(body.AsSpan(dataAt+dataLength));
        RetainedComplementSha256=Convert.ToHexStringLower(complement.GetHashAndReset());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="source-derived-detached-four-tile-family-used-data-v1",SourceImageSha256,CanonicalDocumentSha256,
            Placement=placement.PlanSha256,Body=BodyAncestor.PlanSha256,Resources=ResourceAncestor.PlanSha256,
            Host=HostAncestor.PlanSha256,Mixed=mixed.PlanSha256,intent.PlacementIds,intent.OrderedFaceMaterials,
            Sources=MaterialSources.Select(s=>new{s.Handle,s.SourceDataSha256,s.SourceSceneSha256,SourceRecord=s.Layout.RecordSha256}),
            AllocatedRecords=records.Select(r=>r.RecordSha256),DataSha256,PriorDataAllocationSha256,
            RetainedBodySha256,HeaderSha256,UploadSha256,ModelSha256,SceneSha256,RetainedComplementSha256,
            MetadataLength,OldSurfaceLength,SurfaceLength,TailLength,AllocationLength,UsedLength=_used.Length,
            SurfaceEvidence=surfaceEvidence,MaterialEvidence=materialEvidence,SourceCertified,BodyIssued,
            RuntimeVerified,PublicationApproved,CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven}));
    }

    internal NativeFreshTiledFamilyTerrainIntent IntentAncestor{get;}
    internal NativeFreshLockedChestPlacementPlan PlacementAncestor{get;}
    internal NativeFreshLockedChestResearchBody BodyAncestor=>PlacementAncestor.BodyAncestor;
    internal NativeFreshMobyFamilyCompositionPlan ResourceAncestor=>BodyAncestor.ResourceAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor=>BodyAncestor.HostAncestor;
    internal IReadOnlyList<NativeStaticTerrainTextureSource> MaterialSources{get;}
    internal string SourceImageSha256=>NativeId65AuthoredSurfaceConsumers.ImageSha256;
    internal string CanonicalDocumentSha256{get;}
    internal string DataSha256{get;}
    internal string PriorDataAllocationSha256{get;}
    internal string RetainedBodySha256{get;}
    internal string HeaderSha256{get;}
    internal string UploadSha256{get;}
    internal string ModelSha256{get;}
    internal string SceneSha256{get;}
    internal string RetainedComplementSha256{get;}
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool BodyIssued=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal byte[] CopyUsedData()=>_used.ToArray();
    internal void RequireExactUsedData(ReadOnlySpan<byte> data)
    {if(!data.SequenceEqual(_used))throw new InvalidDataException("Complete detached used data changed.");}
    internal byte[] RevertExactDataAllocation(ReadOnlySpan<byte> allocation)
    {
        if(allocation.Length!=AllocationLength||!allocation[.._used.Length].SequenceEqual(_used)||
            allocation[_used.Length..].IndexOfAnyExcept((byte)0)>=0)
            throw new InvalidDataException("Inverse requires the exact complete zero-padded data allocation, not only used bytes.");
        return _priorAllocation.ToArray();
    }
    internal void RequireAncestry(NativeFreshTiledFamilyTerrainIntent intent,NativeFreshLockedChestPlacementPlan placement)
    {
        ArgumentNullException.ThrowIfNull(intent);ArgumentNullException.ThrowIfNull(placement);
        if(!ReferenceEquals(intent,IntentAncestor)||!ReferenceEquals(placement,PlacementAncestor))
            throw new InvalidDataException("Detached family data retains its actual complete intent/placement tuple, not equal replacements.");
        var mixed=RequireInputs(intent,placement);
        if(!MaterialSources.Zip(mixed.MaterialSources).All(p=>ReferenceEquals(p.First,p.Second)))
            throw new InvalidDataException("Detached family data lost an actual ordered source owner.");
    }
    internal static NativeFreshTiledFamilyTerrainRevision Compose(NativeFreshTiledFamilyTerrainIntent intent,
        NativeFreshLockedChestPlacementPlan placement)
    {ArgumentNullException.ThrowIfNull(intent);ArgumentNullException.ThrowIfNull(placement);return new(intent,placement);}

    private static NativeFreshWizardMaterialResource RequireInputs(NativeFreshTiledFamilyTerrainIntent intent,
        NativeFreshLockedChestPlacementPlan placement)
    {
        var document=intent.DocumentAncestor;intent.RequireAncestry(document);
        var body=placement.BodyAncestor;placement.RequireAncestry(body,placement.Intent);
        var resources=body.ResourceAncestor;var host=body.HostAncestor;var scene=body.SceneAncestor;
        body.RequireAncestry(resources,host,scene);
        var parent=host.CurrentExecutableAncestor.FamilyCompositionAncestor??
            throw new InvalidDataException("Detached family data requires its actual retained family executable ancestor.");
        resources.RequireLockedChestResourceAncestry(parent,host.FamilySourceAncestor);
        NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(parent);
        NativeId65FreshWizardResearchExecutableSource.RequireWideFamilyMaterialBody(resources.BaseBodyAncestor);
        var mixed=resources.BaseBodyAncestor.MixedResourceAncestor!;
        mixed.RequireAncestry(resources.BaseBodyAncestor.HostAncestor,mixed.DocumentAncestor,mixed.MaterialSources[0],mixed.MaterialSources[1]);
        host.FamilySourceAncestor.RequireWizardOriginalOwner(mixed.SceneAncestor.FamilyAncestor);
        var family=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        if(family.RewardDestinations is null||placement.RewardDestinationsAncestor is null||
            placement.Intent!=new NativeFreshLockedChestPlacementIntent(family.KeyX,family.KeyY,family.KeyZ,family.ChestX,family.ChestY,family.ChestZ)||
            !intent.PlacementIds.SequenceEqual(scene.RowSpans.Placements.Select(p=>p.EditorId))||
            !scene.RowSpans.Placements.Select(p=>(p.FirstRowIndex,p.RowCount)).SequenceEqual(new[]{(0,1),(1,1),(2,7)})||
            scene.Layout.PlacedCount!=9||scene.Layout.DynamicCapacity!=32||scene.Allocation.Rows.Count!=9)
            throw new InvalidDataException("Complete authored family IDs, native row spans, anchors and reward owner must match the actual placed R03 scene.");
        placement.RequireRewardDestinationsAncestry(family.RewardDestinations);
        if(intent.MaterialSelections.Count!=2||mixed.MaterialSources.Count!=2)
            throw new InvalidDataException("Detached family data requires its exact ordered two material owners.");
        for(int i=0;i<2;i++)
        {
            var descriptor=intent.MaterialSelections[i];var source=mixed.MaterialSources[i];
            if(descriptor.SourceVariant!=NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant||
                descriptor.SourceWadEntry!=source.SourceWadEntry||descriptor.TextureId!=source.SourceTextureId||
                !source.SourceCertified||!source.StaticRecordProven||source.SourceImageSha256!=NativeId65AuthoredSurfaceConsumers.ImageSha256)
                throw new InvalidDataException("Authored face descriptors must resolve to the actual retained ordered source materials.");
            source.RequireExactSourceRecord(source.Layout.CopyRecord());
        }
        return mixed;
    }
    private static void RequireSurfaceLengths(ReadOnlySpan<byte> bytes,int start,int[] lengths,int end)
    {
        foreach(int length in lengths)
        {if(start>bytes.Length-4||Word(bytes,start)!=length||length>bytes.Length-start)throw new InvalidDataException("Complete surface component boundary changed.");start=checked(start+length);}
        if(start!=end)throw new InvalidDataException("Complete five-component surface end changed.");
    }
    private static byte[] Part(byte[] body,int index)=>body.AsSpan(Word(body,index*8),Word(body,index*8+4)).ToArray();
    private static int Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
