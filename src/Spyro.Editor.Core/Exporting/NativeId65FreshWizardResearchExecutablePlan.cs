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
    }
    internal NativeId65FreshWizardResearchBodyPlan BodyAncestor=>_source.Body;
    internal void RequireAncestry(NativeId65FreshWizardResearchBodyPlan body)
    {
        if(!ReferenceEquals(body,BodyAncestor))throw new InvalidDataException("Executable belongs to another actual Body, even when every executable byte/hash is equal.");
        NativeId65FreshWizardResearchExecutableSource.RequireBody(body);
    }
    internal byte[] RevertExact(ReadOnlySpan<byte> executable)=>Layout.RevertExact(executable);
    public const string ProfileId="fresh-A-P1-N32-Town-Host-R1-static-target10-detached-executable-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string BodyPlanSha256=>BodyAncestor.PlanSha256;
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
}
