using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached full physical afterimages for one actual Wizard EXE/Body/Host chain.
/// Every changed and unchanged source sector authenticates its original LBA.
/// No image writer, CUE, receipt, output path or runtime/publication capability.
/// </summary>
public sealed class NativeId65FreshWizardResearchDiscPlan
{
    private readonly NativeId65FreshWizardResearchDiscSource _source;
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly NativeFreshResearchPhysicalAfterimages _physical;
    private NativeId65FreshWizardResearchDiscPlan(NativeId65FreshWizardResearchDiscSource source)
    {
        _physical=NativeFreshResearchPhysicalAfterimages.ForWizard(source);
        _source=source;var exe=source.Executable;_geometry=source.Geometry;
        BodySha256=Hash(_physical.CopyBody());OverlaySha256=Hash(_physical.CopyOverlay());ExecutableSha256=Hash(_physical.CopyExecutable());
        WadDirectorySha256=Hash(_physical.CopyWadDirectory());IsoRootSha256=Hash(_physical.CopyIsoRootDirectory());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImageSha256,ExecutablePlanSha256,BodyPlanSha256,
            HostPlanSha256,CanonicalDocumentSha256,BodySha256,OverlaySha256,ExecutableSha256,WadDirectorySha256,IsoRootSha256,
            FinalExecutableLba,FinalWadByteLength,FinalExecutableEndExclusive,ChangedSectorOwnerCount,PendingOwners,
            SourceCertified,InstalledDispatchProven,GlobalTransitionState12Supported,PersistentOwnerIssued,CardOrHistoryCompatibilityProven,
            WholePickupOrDeathProven,GlobalAudioCompatibilityProven,PeakRuntimeMemoryProven,HardwareTransfersProven,
            CompleteIncomingEdgeClosureProven,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved,NormalCreateBinEnabled}));
        if(source.MixedResourceAncestor is { } mixed)
            PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
                Profile="internal-schema2-separated-B-three-T1-pairs-detached-mixed-disc-v1",
                DiscEvidenceSha256=PlanSha256,ExecutablePlanSha256,BodyPlanSha256,
                MixedResourcePlanSha256=mixed.PlanSha256,AggregateCanonicalSha256=mixed.SceneAncestor.MaterialCanonicalSha256}));
        if(source.FamilyCompositionAncestor is { } family)
            PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
                Profile="internal-mixed-B14-70-one-wizard-one-passive-grass-disc-v1",
                DiscEvidenceSha256=PlanSha256,FamilyCompositionPlanSha256=family.PlanSha256,
                BaseBodyPlanSha256=family.BaseBodyAncestor.PlanSha256,FinalBodySha256=family.LevelDataSha256,
                FamilyCensus=exe.FamilyCensus}));
        var original=new NativeAddressSpace(NativeAddressKind.DiscLogical,SourceImageSha256,NativeAddressRevision.Original,281270L*2048);
        var final=new NativeAddressSpace(NativeAddressKind.DiscLogical,PlanSha256,NativeAddressRevision.Final,original.ByteLength);
        AddressMap=new(original,final,[
            new("ISO-root-fixed-position",new(original,22*2048L,2048),new(final,22*2048L,2048)),
            new("original-WAD-fixed-position",new(original,37*2048L,0x6927000),new(final,37*2048L,0x6927000)),
            new("actual-Wizard-Host",null,new(final,53875*2048L,_geometry.OverlayByteLength)),
            new("actual-Wizard-Body",null,new(final,_geometry.BodyLba*2048L,_geometry.BodyByteLength)),
            new("relocated-actual-Wizard-EXE",new(original,53875*2048L,0x66000),new(final,FinalExecutableLba*2048L,0x66000))]);
        Regions=Array.AsReadOnly(AddressMap.Regions.Select(r=>new NativeId65FreshDiscRegion(r.Id,r.Original?.Offset,r.Final.Offset,r.Final.ByteLength)).ToArray());
    }
    public const string ProfileId="fresh-A-P1-N32-Town-Host-R1-target10-detached-physical-disc-v1";
    internal bool IsMaterialAuthored=>_source.MixedResourceAncestor is not null;
    internal NativeFreshMobyFamilyCompositionPlan? FamilyCompositionAncestor=>_source.FamilyCompositionAncestor;
    internal NativeId65FreshWizardResearchExecutablePlan ExecutableAncestor=>_source.Executable;
    internal NativeRelocationMap AddressMap{get;}
    internal void RequireAncestry(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        if(!ReferenceEquals(executable,ExecutableAncestor))throw new InvalidDataException("Disc belongs to another actual EXE, even when executable bytes/hashes are equal.");
        _source.RequireAncestry();
    }
    public string SourceImageSha256=>NativeId65FreshDiscSource.OriginalSha256;
    public string ExecutablePlanSha256=>ExecutableAncestor.PlanSha256;
    public string BodyPlanSha256=>ExecutableAncestor.BodyPlanSha256;
    public string HostPlanSha256=>ExecutableAncestor.HostPlanSha256;
    public string CanonicalDocumentSha256=>ExecutableAncestor.CanonicalDocumentSha256;
    public string BodySha256{get;}
    public string OverlaySha256{get;}
    public string ExecutableSha256{get;}
    public string WadDirectorySha256{get;}
    public string IsoRootSha256{get;}
    public string PlanSha256{get;}
    public int FinalExecutableLba=>_geometry.FinalExecutableLba;
    public int FinalWadByteLength=>_geometry.FinalWadByteLength;
    public int FinalExecutableEndExclusive=>_geometry.FinalExecutableEndExclusive;
    public int ChangedSectorOwnerCount=>_geometry.ChangedSectorOwnerCount;
    public IReadOnlyList<NativeId65FreshDiscRegion> Regions{get;}
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Detached exact physical bytes only. No destination writer, CUE, receipt, transaction, project authority, candidate or normal Create BIN route is supplied.",
        "The later physical source index authenticates this disc capture/readback, not historical nested payload-capture reads. Actual immutable ancestry and complete source bytes bind those separate captures.",
        "Static target10 and placed ordinal0 are not durable ID65 owner/card/history/repeated-award or complete death/pickup compatibility.",
        "Complete callback/effect/collider/global1F audio and computed/asynchronous incoming/lifetime closure remain unproved. Global transition state12 and other skip/checkpoint entries remain unsupported.",
        "Physical file placement and known address bounds do not prove peak actor/particle/stack use, allocation33 safety or CD/GPU/SPU completion/interference.",
        "No installed executable/dispatch, hardware, gameplay, runtime, public distribution or publication approval follows from full physical readback."});
    public bool SourceCertified=>true;
    public bool InstalledDispatchProven=>false;
    public bool GlobalTransitionState12Supported=>false;
    public bool PersistentOwnerIssued=>false;
    public bool CardOrHistoryCompatibilityProven=>false;
    public bool WholePickupOrDeathProven=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public bool HardwareTransfersProven=>false;
    public bool CompleteIncomingEdgeClosureProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool NormalCreateBinEnabled=>false;
    public byte[] CopyBody()=>_physical.CopyBody();
    public byte[] CopyOverlay()=>_physical.CopyOverlay();
    public byte[] CopyExecutable()=>_physical.CopyExecutable();
    public byte[] CopyWadDirectory()=>_physical.CopyWadDirectory();
    public byte[] CopyIsoRootDirectory()=>_physical.CopyIsoRootDirectory();
    public static async Task<NativeId65FreshWizardResearchDiscPlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchDiscSource.RequireExecutable(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        await using FileStream image=new(Path.GetFullPath(originalBin),FileMode.Open,FileAccess.Read,FileShare.Read);
        var source=await NativeId65FreshWizardResearchDiscSource.CaptureAsync(image,executable,token);
        token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchDiscPlan(source);token.ThrowIfCancellationRequested();return result;
    }
    internal static async Task<NativeId65FreshWizardResearchDiscPlan> CaptureMaterialAuthoredAsync(string originalBin,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchDiscSource.RequireMaterialExecutable(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        return await CaptureBoundAsync(path,executable,false,token);
    }
    internal static async Task<NativeId65FreshWizardResearchDiscPlan> CaptureFamilyComposedAsync(string originalBin,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchDiscSource.RequireFamilyExecutable(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        return await CaptureBoundAsync(path,executable,true,token);
    }
    // Same physical writer and acquired-owner lifecycle; the internal callers
    // perform distinct actual-arm checks before opening an input stream.
    private static async Task<NativeId65FreshWizardResearchDiscPlan> CaptureBoundAsync(string path,
        NativeId65FreshWizardResearchExecutablePlan executable,bool family,CancellationToken token)
    {
        FileStream? held=null;NativeId65FreshWizardResearchDiscSource? source=null;
        NativeId65FreshWizardResearchDiscPlan? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,token);
            source=family?await NativeId65FreshWizardResearchDiscSource.CaptureFamilyComposedAsync(held,executable,token):
                await NativeId65FreshWizardResearchDiscSource.CaptureMaterialAuthoredAsync(held,executable,token);
            token.ThrowIfCancellationRequested();result=new(source);
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            if(held is not null)await TryAsync(()=>NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Try(current.Dispose);}
            if(source is not null)
            {
                Try(source.RequireAncestry);
                Try(source.RequireRetainedOriginalOwners);
            }
            if(held is not null)Try(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException(family?"Family-composed disc source capture returned no result.":
            "Material-authored disc source capture returned no result.");
        async Task TryAsync(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Try(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }
    internal static void PatchRoot(Span<byte> root,NativeFlatIsoFile row,int lba,int length)
        =>NativeFreshResearchPhysicalAfterimages.PatchRoot(root,row,lba,length);
    internal byte[] CreateExpectedRawSector(int lba,ReadOnlySpan<byte> sourceSector)
        =>_physical.CreateExpectedRawSector(lba,sourceSector);
    internal byte[] CreateOriginalRawSector(int lba,ReadOnlySpan<byte> finalSector)
        =>_physical.CreateOriginalRawSector(lba,finalSector);
    public NativeId65FreshDiscVerification VerifyImage(Stream original,Stream output,CancellationToken token=default)
        =>_physical.VerifyImage(original,output,token);
    private static string Hash(ReadOnlySpan<byte> b)=>NativeId65FreshExecutablePlan.Hash(b);
}
