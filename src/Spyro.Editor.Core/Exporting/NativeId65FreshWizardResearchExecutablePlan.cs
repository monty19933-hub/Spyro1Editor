using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Static source-row/value census only; not durable/card/repeated-award authority.</summary>
public sealed record NativeFreshWizardStaticRewardCensus(int PlacedCount,ushort ActorClassId,byte ActorKind,byte RewardClassId,
    int ValuePerActor,int StaticTreasureTarget,int TargetSlot,int TargetFileOffset,int SceneRowOffset,string SceneRowSha256);

/// <summary>
/// Complete detached source-bound executable for an actual fresh Wizard Body.
/// The unchanged bootstrap/music/LAB compiler is composed with its actual Host
/// initializer and static target10. No disc writer or installed/runtime claim.
/// </summary>
public sealed class NativeId65FreshWizardResearchExecutablePlan
{
    private readonly NativeId65FreshWizardResearchExecutableSource _source;
    private NativeId65FreshWizardExecutableLayout Layout=>_source.Layout;
    private NativeId65FreshWizardResearchExecutablePlan(NativeId65FreshWizardResearchExecutableSource source)
    {
        _source=source;
        PlanSha256=NativeId65FreshWizardExecutableLayout.Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,BodyPlanSha256,HostPlanSha256,CanonicalDocumentSha256,SelectedBuiltInTrack,
            PriorFreshExecutableSha256,HostLabMusicExecutableSha256,ExecutableSha256,Census,SequentialStages,FoldedWindows,Evidence,
            PendingOwners,SourceCertified,InstalledDispatchProven,GlobalTransitionState12Supported,PersistentOwnerIssued,
            CardOrHistoryCompatibilityProven,WholePickupOrDeathProven,GlobalAudioCompatibilityProven,PeakRuntimeMemoryProven,
            CompleteIncomingEdgeClosureProven,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved}));
        // Preserve the historical evidence shape; only the actual new source
        // arm receives this distinct binding envelope.
        if(source.MixedResourceAncestor is { } mixed)
            PlanSha256=NativeId65FreshWizardExecutableLayout.Hash(JsonSerializer.SerializeToUtf8Bytes(new{
                Profile="internal-schema2-separated-B-three-T1-pairs-detached-mixed-executable-v1",
                ExecutableEvidenceSha256=PlanSha256,BodyPlanSha256,MixedResourcePlanSha256=mixed.PlanSha256,
                AggregateCanonicalSha256=mixed.SceneAncestor.MaterialCanonicalSha256}));
        if(source.FamilyCompositionAncestor is { } family)
        {
            var ledger=FamilyCensus??throw new InvalidDataException("Family executable has no actual static census.");
            PlanSha256=NativeId65FreshWizardExecutableLayout.Hash(JsonSerializer.SerializeToUtf8Bytes(new{
                Profile="internal-mixed-B14-70-one-wizard-one-passive-grass-executable-v1",
                ExecutableEvidenceSha256=PlanSha256,FamilyCompositionPlanSha256=family.PlanSha256,
                BaseBodyPlanSha256=family.BaseBodyAncestor.PlanSha256,FinalBodySha256=family.LevelDataSha256,
                ledger.Rows,ledger.StaticTreasureTarget,ledger.TargetSlot,
                ledger.TargetFileOffset,ledger.RuntimeDropGuarantee}));
        }
    }
    internal NativeFreshMobyFamilyCompositionPlan? FamilyCompositionAncestor=>_source.FamilyCompositionAncestor;
    internal NativeFreshFamilyStaticRewardCensus? FamilyCensus=>Layout.FamilyCensus;
    internal bool IsMaterialAuthored=>_source.MixedResourceAncestor is not null;
    internal NativeFreshWizardMaterialResource? MixedResourceAncestor=>_source.MixedResourceAncestor;
    internal NativeId65FreshWizardResearchBodyPlan BodyAncestor=>_source.Body;
    internal void RequireAncestry(NativeId65FreshWizardResearchBodyPlan body)
    {
        if(FamilyCompositionAncestor is not null)
            throw new InvalidDataException("Family-composed executable requires its actual family ancestry, not its historical base Body arm.");
        if(!ReferenceEquals(body,BodyAncestor))throw new InvalidDataException("Executable belongs to another actual Body, even when every executable byte/hash is equal.");
        if(_source.MixedResourceAncestor is { } mixed)
        {
            if(!ReferenceEquals(mixed,body.MixedResourceAncestor))
                throw new InvalidDataException("Executable lost its exact retained Body/mixed source pair.");
            NativeId65FreshWizardResearchExecutableSource.RequireMaterialBody(body);
        }
        else NativeId65FreshWizardResearchExecutableSource.RequireBody(body);
    }
    internal void RequireFamilyAncestry(NativeFreshMobyFamilyCompositionPlan family)
    {
        ArgumentNullException.ThrowIfNull(family);
        if(!ReferenceEquals(family,FamilyCompositionAncestor)||!ReferenceEquals(family.BaseBodyAncestor,BodyAncestor)||
            !ReferenceEquals(MixedResourceAncestor,BodyAncestor.MixedResourceAncestor))
            throw new InvalidDataException("Family executable belongs to another actual composition/base Body/mixed resource chain.");
        NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(family);
        RequireExactExecutable(CopyExecutable());
    }
    internal byte[] RevertExact(ReadOnlySpan<byte> executable)=>Layout.RevertExact(executable);
    public const string ProfileId="fresh-A-P1-N32-Town-Host-R1-static-target10-detached-executable-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string BodyPlanSha256=>FamilyCompositionAncestor?.PlanSha256??BodyAncestor.PlanSha256;
    public string HostPlanSha256=>BodyAncestor.HostPlanSha256;
    public string CanonicalDocumentSha256=>BodyAncestor.CanonicalDocumentSha256;
    public string PlanSha256{get;}
    public int SelectedBuiltInTrack=>Layout.SelectedBuiltInTrack;
    public string PriorFreshExecutableSha256=>Layout.PriorFreshExecutableSha256;
    public string HostLabMusicExecutableSha256=>Layout.HostLabMusicExecutableSha256;
    public string ExecutableSha256=>Layout.ExecutableSha256;
    public NativeFreshWizardStaticRewardCensus Census=>Layout.Census;
    public IReadOnlyList<NativeId65FreshExecutableWindow> SequentialStages=>Layout.SequentialStages;
    public IReadOnlyList<NativeId65FreshExecutableWindow> FoldedWindows=>Layout.FoldedWindows;
    public IReadOnlyList<NativeId65EngineEvidence> Evidence=>_source.Evidence;
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Detached executable bytes only. Body/overlay/physical-disc assembly and installed initializer/dispatch require a separate actual-owner composition.",
        "Static one-Wizard reward selector56/value10 and target35 bytes do not issue persistent ordinal/card/history migration, repeated-award or complete pickup/death compatibility.",
        "Existing fresh bootstrap remains conditional on global game/transition state, not player state; global state12 and other incoming/skip/checkpoint paths remain unsupported.",
        "Source scalar witnesses stop at unexecuted visibility/allocator/award/helper bodies; full callback, global1F audio, collider and effect graph are not closed.",
        "No peak actor/particle/stack, SPU/VRAM/CD hardware, allocation33 exhaustion or computed/asynchronous lifetime certificate is issued.",
        "Source-valid placement coordinates and actual canonical intent/Body identity are retained. No physical output, publication, gameplay or installed-app approval."});
    public bool SourceCertified=>true;
    public bool InstalledDispatchProven=>false;
    public bool GlobalTransitionState12Supported=>false;
    public bool PersistentOwnerIssued=>false;
    public bool CardOrHistoryCompatibilityProven=>false;
    public bool WholePickupOrDeathProven=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool PeakRuntimeMemoryProven=>false;
    public bool CompleteIncomingEdgeClosureProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public byte[] CopyExecutable()=>Layout.CopyExecutable();
    public void RequireExactExecutable(ReadOnlySpan<byte> executable)=>Layout.RequireExactExecutable(executable);
    public static async Task<NativeId65FreshWizardResearchExecutablePlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchBodyPlan body,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(body);NativeId65FreshWizardResearchExecutableSource.RequireBody(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
            NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            var source=NativeId65FreshWizardResearchExecutableSource.Capture(image,body,token);
            token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchExecutablePlan(source);token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            image.Position=0;
            await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);token.ThrowIfCancellationRequested();
        }
    }
    internal static async Task<NativeId65FreshWizardResearchExecutablePlan> CaptureMaterialAuthoredAsync(string originalBin,
        NativeId65FreshWizardResearchBodyPlan body,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchExecutableSource.RequireMaterialBody(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        return await CaptureBoundAsync(path,body,null,token);
    }
    internal static async Task<NativeId65FreshWizardResearchExecutablePlan> CaptureFamilyComposedAsync(string originalBin,
        NativeFreshMobyFamilyCompositionPlan family,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        return await CaptureBoundAsync(path,family.BaseBodyAncestor,family,token);
    }
    // Same acquired-owner lifecycle for the two internal source-bound arms.
    // Public historical capture above retains its original behavior unchanged.
    private static async Task<NativeId65FreshWizardResearchExecutablePlan> CaptureBoundAsync(string path,
        NativeId65FreshWizardResearchBodyPlan body,NativeFreshMobyFamilyCompositionPlan? family,CancellationToken token)
    {
        FileStream? held=null;NativeId65FreshWizardResearchExecutablePlan? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await RequireOriginal(held,token);
            var source=family is null?NativeId65FreshWizardResearchExecutableSource.CaptureMaterialAuthored(held,body,token):
                NativeId65FreshWizardResearchExecutableSource.CaptureFamilyComposed(held,family,token);
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
            if(family is not null)Try(()=>NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(family));
            if(held is not null)Try(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException(family is null?"Material-authored executable source capture returned no result.":
            "Family-composed executable source capture returned no result.");
        async Task TryAsync(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Try(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        static Task RequireOriginal(Stream image,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();image.Position=0;
            return NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,
                NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,ct);
        }
    }
}
