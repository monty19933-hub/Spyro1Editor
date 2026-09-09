using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached full native body, actual Host overlay/initializer copies and
/// same-fallback transfer bytes. No final EXE, installed callbacks, physical
/// archive/image, runtime, audio compatibility, peak fit or publication.
/// </summary>
public sealed class NativeId65FreshWizardResearchBodyPlan
{
    private readonly NativeId65FreshWizardResearchBodySource _source;
    private readonly NativeId65FreshWizardBodyLayout _layout;
    private readonly NativeId65FreshWizardUploadTransferLayout _transfer;
    private NativeId65FreshWizardResearchBodyPlan(NativeId65FreshWizardResearchBodySource source)
    {
        _source=source;var host=source.Host;var fallback=host.FallbackAncestor;var scene=host.SceneAncestor;
        var mixed=source.MixedResourceAncestor;
        byte[] upload,data,models,sceneBytes;
        if(mixed is null)
        {
            NativeId65FreshWizardResearchBodySource.RequireHost(host);
            upload=fallback.CopyUploadPayload();data=fallback.CopyDataComponents();models=fallback.CopyModelPayload();sceneBytes=scene.CopyScene();
            fallback.RequireExactOutputs(upload,data,models);scene.RequireExactScene(sceneBytes);
            _layout=scene.ResourceAncestor.ResourceProfile==NativeFreshWizardResourceProfile.FixedA
                ?NativeId65FreshWizardBodyLayout.Compile(upload,data,models,sceneBytes,source.CopyAuxiliaryAllocations())
                :NativeId65FreshWizardBodyLayout.CompileAuthored(host,source.CopyAuxiliaryAllocations());
        }
        else
        {
            NativeId65FreshWizardResearchBodySource.RequireMixed(mixed);
            upload=mixed.CopyUploadPayload();data=mixed.CopyDataComponents();models=mixed.CopyModelPayload();sceneBytes=mixed.CopyScene();
            mixed.RequireExactOutputs(upload,data,models,sceneBytes);
            _layout=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(mixed,source.CopyAuxiliaryAllocations());
        }
        if(!_layout.ModelRoots.Select(r=>(r.ClassId,r.ModelRelativeOffset,r.PayloadByteLength,r.RegistrationAllocationByteLength))
            .SequenceEqual(fallback.ModelRoots.Select(r=>((ushort?)r.ClassId,r.Offset,r.ByteLength,r.RegistrationExtentByteLength))))
            throw new InvalidDataException("Body root grammar no longer equals its actual fallback roots.");
        _transfer=NativeId65FreshWizardUploadTransferLayout.Compile(upload);
        if(_transfer.PairedUploadSha256!=(mixed?.UploadSha256??fallback.UploadSha256)||_transfer.TransferSourceSha256!="92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883")
            throw new InvalidDataException("Body transfer must derive from this same actual revised fallback upload.");
        CopyBufferAddress=host.CopyAnchor;ModelRuntimeAddress=checked(CopyBufferAddress+(uint)data.Length);
        SceneRuntimeAddress=checked(ModelRuntimeAddress+(uint)models.Length);KnownSceneEndAddress=checked(SceneRuntimeAddress+(uint)sceneBytes.Length);
        bool wrongKnownAddresses=mixed is null
            ?ModelRuntimeAddress!=0x8008C334||SceneRuntimeAddress!=0x800A5334||KnownSceneEndAddress!=0x800A8334
            :ModelRuntimeAddress!=0x8008C4A4||SceneRuntimeAddress!=0x800A54A4||KnownSceneEndAddress!=0x800A84A4;
        if(wrongKnownAddresses||
            KnownSceneEndAddress>source.NormalReservedLowerAddress||CopyBufferAddress+0x80000>source.NormalReservedLowerAddress)
            throw new InvalidDataException("Known copied body extents escape their native-address budget; not a peak certificate.");
        byte[] body=_layout.CopyLevelData();LevelDataSha256=Hash(body);HeaderSha256=Hash(body.AsSpan(0,0x800));
        var owners=new List<NativeId65NormalBodyOwner>();
        foreach(var owner in _layout.Owners)
        {
            var aux=_layout.Descriptors.FirstOrDefault(d=>d.RetainedAuxiliary&&d.Offset==owner.Offset);
            owners.Add(aux is not null?owner with{Provenance="complete-original-Town-auxiliary",SourceWadOffset=0x119E000L+NativeId65NormalBodySource.AuxiliarySourceOffsets[aux.Index-4]}:
                owner with{Provenance=owner.Provenance=="neutral-input-not-authority"
                    ?mixed is not null&&(owner.Offset==0x800||owner.Offset==0xE6000)?"actual-issued-mixed-resource":"actual-issued-Host-descendant"
                    :owner.Provenance});
        }
        Owners=owners.AsReadOnly();
        ReadPhases=Array.AsReadOnly(new[]{
            new NativeId65NormalBodyReadPhase("overlay-install",0x8007AA38,host.OverlayByteLength,"Complete incoming enlarged Host allocation",true),
            new("header-read",CopyBufferAddress,0x800,"Completed header read then actual1D0 native memcpy",true),
            new("vram-read",CopyBufferAddress,0x80000,mixed is null?"Same fallback VRAM source read":"Mixed VRAM source read; complete fallback owners/residue retained",true),
            new("bank-read",CopyBufferAddress,0x65800,"Same fallback bank overwrites first65800 bytes",true),
            new("cpu-spu-source",CopyBufferAddress,0x7EFF0,"CPU requested source, not FIFO completion",true),
            new("dma-spu-source",CopyBufferAddress,0x7F000,"DMA rounded source including16 extra bytes",true),
            new("data-read",CopyBufferAddress,0x1000,mixed is null?"Padded CD data; native used end is8FC":"Padded CD data; actual mixed native used end isA6C",true),
            new("model-read",ModelRuntimeAddress,0x19000,"Native used-data-end model CD owner",true),
            new("scene-read",SceneRuntimeAddress,0x3000,"P1/N32 after full model allocation",true)});
        PhaseOverlaps=Array.AsReadOnly(new[]{new NativeId65NormalBodyPhaseOverlap("data-sector-padding","model-CD-read",ModelRuntimeAddress,mixed is null?0x704:0x594,
            "Discarded data CD padding is overwritten by models after required completion; no live used-data/Host overlap.")});
        // Original EXE/WAD consumer pins remain source evidence, not claims
        // that emitted Host code equals the original retail callbacks.
        Evidence=Array.AsReadOnly(source.Evidence.Concat(new[]{
            new NativeId65EngineEvidence("complete-issued-Host-overlay","actual-issued-Host",0,host.OverlayByteLength,host.OverlaySha256),
            new("complete-issued-conditional-initializer","actual-issued-Host",0,host.CopyInitializerPatch().Length,host.InitializerSha256),
            new("complete-issued-P1-scene","actual-issued-scene",0,sceneBytes.Length,scene.SceneSha256),
            new(mixed is null?"complete-issued-fallback-upload":"complete-issued-mixed-upload",
                mixed is null?"actual-issued-fallback":"actual-issued-mixed-resource",0,upload.Length,mixed?.UploadSha256??fallback.UploadSha256),
            new(mixed is null?"complete-issued-fallback-data":"complete-issued-mixed-data",
                mixed is null?"actual-issued-fallback":"actual-issued-mixed-resource",0,data.Length,mixed?.DataSha256??fallback.DataSha256),
            new("complete-issued-fallback-models","actual-issued-fallback",0,models.Length,fallback.ModelSha256),
            new("complete-derived-bank-and-residue","derived-same-fallback-transfer",0,0x7F000,_transfer.TransferSourceSha256)}).ToArray());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImageSha256,HostPlanSha256,CanonicalDocumentSha256,
            LevelDataSha256,HeaderSha256,OverlaySha256,InitializerSha256,TransferSourceSha256,CopyBufferAddress,ModelRuntimeAddress,SceneRuntimeAddress,
            KnownSceneEndAddress,ResidentPeteAddress,NormalReservedLowerAddress,Descriptors,ModelRoots,Owners,AuxiliaryOwners,TransferOwners,TransferSourceReads,
            ReadPhases,PhaseOverlaps,Evidence,PendingOwners,SourceCertified,FinalExecutableIssued,InstalledDispatchProven,RuntimeVerified,PublicationApproved,
            PersistentOwnerIssued,CompleteNativeDependencyClosureProven,CompleteLevelResourcesProven,GlobalAudioCompatibilityProven,PeakRuntimeMemoryProven,
            HardwareTransfersProven,CompleteIncomingEdgeClosureProven}));
        // The historical-shaped body evidence above remains byte-identical
        // for old arms. Only this closed arm gets a new binding envelope.
        if(mixed is not null)
            PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
                Profile="internal-schema2-separated-B-three-T1-pairs-detached-mixed-body-v1",
                BodyEvidenceSha256=PlanSha256,MixedResourcePlanSha256=mixed.PlanSha256,
                AggregateCanonicalSha256=mixed.SceneAncestor.MaterialCanonicalSha256}));
    }
    internal NativeFreshWizardMaterialResource? MixedResourceAncestor=>_source.MixedResourceAncestor;
    internal bool IsMaterialAuthored=>MixedResourceAncestor is not null;
    internal NativeId65FreshWizardResearchHostPlan HostAncestor=>_source.Host;
    internal void RequireAncestry(NativeId65FreshWizardResearchHostPlan host)
    {
        if(!ReferenceEquals(host,HostAncestor))throw new InvalidDataException("Body belongs to another actual Host, even if all hashes match.");
        if(MixedResourceAncestor is { } mixed)RequireMixedAncestry(mixed);else NativeId65FreshWizardResearchBodySource.RequireHost(host);
    }
    internal void RequireMixedAncestry(NativeFreshWizardMaterialResource mixed)
    {
        ArgumentNullException.ThrowIfNull(mixed);
        if(!ReferenceEquals(mixed,MixedResourceAncestor)||!ReferenceEquals(mixed.HostAncestor,HostAncestor))
            throw new InvalidDataException("Mixed Body belongs to its exact actual mixed component, not an equal reissued component.");
        NativeId65FreshWizardResearchBodySource.RequireMixed(mixed);
    }
    internal void RequireOwners(IReadOnlyList<NativeId65NormalBodyOwner> owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        if(owners.Count!=Owners.Count||!owners.Take(Owners.Count+1).SequenceEqual(Owners))throw new InvalidDataException("Body owner attribution/order/bounds changed.");
    }
    internal void RequireAuxiliarySelector(int selector)
    {if(selector<0||selector>=4)throw new InvalidDataException("No complete auxiliary owner for this selector; this is not a native runtime guard.");}
    public const string ProfileId="fresh-A-P1-N32-Town-Host-R1-detached-native-body-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string HostPlanSha256=>HostAncestor.PlanSha256;
    public string CanonicalDocumentSha256=>HostAncestor.CanonicalDocumentSha256;
    public string PlanSha256{get;}
    public string LevelDataSha256{get;}
    public string HeaderSha256{get;}
    public string OverlaySha256=>HostAncestor.OverlaySha256;
    public string InitializerSha256=>HostAncestor.InitializerSha256;
    public string TransferSourceSha256=>_transfer.TransferSourceSha256;
    public uint CopyBufferAddress{get;}
    public uint ModelRuntimeAddress{get;}
    public uint SceneRuntimeAddress{get;}
    public uint KnownSceneEndAddress{get;}
    public uint ResidentPeteAddress=>_source.ResidentPeteAddress;
    public uint NormalReservedLowerAddress=>_source.NormalReservedLowerAddress;
    public IReadOnlyList<NativeId65NormalBodyDescriptor> Descriptors=>_layout.Descriptors;
    public IReadOnlyList<NativeId65NormalBodyRoot> ModelRoots=>_layout.ModelRoots;
    public IReadOnlyList<NativeId65NormalBodyOwner> Owners{get;}
    public IReadOnlyList<NativeId65AuxiliaryBodyOwner> AuxiliaryOwners=>_layout.AuxiliaryOwners;
    public IReadOnlyList<NativeId65TransferSourceOwner> TransferOwners=>_transfer.Owners;
    public IReadOnlyList<NativeId65TransferSourceRead> TransferSourceReads=>_transfer.SourceReads;
    public IReadOnlyList<NativeId65NormalBodyReadPhase> ReadPhases{get;}
    public IReadOnlyList<NativeId65NormalBodyPhaseOverlap> PhaseOverlaps{get;}
    public IReadOnlyList<NativeId65EngineEvidence> Evidence{get;}
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Detached body/overlay/initializer copies only: final EXE must separately compose LAB/music/classifier/initializer table and complete inverse.",
        "Actual source bytes are not installed dispatch, native entry, complete incoming/helper/callback/reward graph, global1F compatibility, persistence or publication.",
        "All CD/GPU/SPU phases require external completion. CPU7EFF0/DMA7F000, SPU wrap/low16 and interference/timing remain unproved.",
        "Disc body20C800 is not simultaneous RAM. Known address bounds/N32 storage do not certify peak actor/particle/stack use, allocation33 or async lifetime.",
        "Auxiliary4..7 retain complete original allocations; rescue/replay entry, selector4+, foreign/checkpoint/state12 transitions remain unapproved.",
        "The actual source-valid P1 placement is retained verbatim. Neither body radius/trajectory, all terrain-query results nor later grid/pointer/flag writers are closed."});
    public bool SourceCertified=>true;
    public bool FinalExecutableIssued=>false;
    public bool InstalledDispatchProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool PersistentOwnerIssued=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool CompleteLevelResourcesProven=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public bool HardwareTransfersProven=>false;
    public bool CompleteIncomingEdgeClosureProven=>false;
    public byte[] CopyLevelData()=>_layout.CopyLevelData();
    public byte[] CopyOverlay()=>HostAncestor.CopyOverlay();
    public byte[] CopyInitializerPatch()=>HostAncestor.CopyInitializerPatch();
    public byte[] CopyTransferSourceImage()=>_transfer.CopyTransferSourceImage();
    public void RequireExactOutputs(ReadOnlySpan<byte> body,ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> initializer,ReadOnlySpan<byte> transfer)
    {_layout.RequireExactLevelData(body);HostAncestor.RequireExactOutputs(overlay,initializer);_transfer.RequireExactTransferSource(transfer);}
    public static async Task<NativeId65FreshWizardResearchBodyPlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(host);NativeId65FreshWizardResearchBodySource.RequireHost(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            var source=await NativeId65FreshWizardResearchBodySource.CaptureAsync(path,host,token);
            token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchBodyPlan(source);token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            image.Position=0;await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);token.ThrowIfCancellationRequested();
        }
    }
    internal static async Task<NativeId65FreshWizardResearchBodyPlan> CaptureMaterialAuthoredAsync(string originalBin,
        NativeFreshWizardMaterialResource mixed,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchBodySource.RequireMixed(mixed);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        FileStream? held=null;NativeId65FreshWizardResearchBodyPlan? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await RequireOriginal(held,token);
            var source=await NativeId65FreshWizardResearchBodySource.CaptureMaterialAuthoredAsync(path,mixed,token);
            token.ThrowIfCancellationRequested();result=new(source);
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            if(held is not null)await TryAsync(()=>RequireOriginal(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await RequireOriginal(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Try(current.Dispose);}
            if(held is not null)Try(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Mixed Body source capture returned no result.");
        async Task TryAsync(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Try(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        static Task RequireOriginal(Stream image,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();image.Position=0;
            return NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,
                NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,ct);
        }
    }
    private static string Hash(ReadOnlySpan<byte> b)=>NativeId65FreshWizardBodyLayout.Hash(b);
}
