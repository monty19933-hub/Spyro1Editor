using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreshWizardHostRole(string Id,uint RuntimePc,uint SourcePc);
public sealed record NativeFreshWizardHostFragment(string Package,uint SourcePc,int ByteLength,string SourceSha256,int OverlayOffset,bool HasFallthrough);
public sealed record NativeFreshWizardHostTransfer(string Package,string Kind,uint SourcePc,uint? SourceTargetPc,
    int OverlayOffset,uint FinalPc,uint FinalTargetPc,uint? BeforeWord,uint AfterWord,uint DelayWord);
public sealed record NativeFreshWizardHostDispatchRequirement(int ExecutableOffset,uint SourceWord,uint RequiredWord);

/// <summary>Exact source/afterimage owner; copies never alias its internal bytes.</summary>
public sealed class NativeFreshWizardHostPatch
{
    private readonly byte[] _before,_after;
    internal NativeFreshWizardHostPatch(string id,int offset,uint pc,ReadOnlySpan<byte> before,ReadOnlySpan<byte> after)
    {
        if(before.Length<=0||before.Length!=after.Length||offset<0)throw new InvalidDataException("Host patch needs one complete equal-size source owner.");
        Id=id;Offset=offset;RuntimePc=pc;_before=before.ToArray();_after=after.ToArray();
        BeforeSha256=NativeId65FreshWizardHostLayout.Hash(_before);AfterSha256=NativeId65FreshWizardHostLayout.Hash(_after);
    }
    public string Id{get;}
    public int Offset{get;}
    public uint RuntimePc{get;}
    public int ByteLength=>_before.Length;
    public string BeforeSha256{get;}
    public string AfterSha256{get;}
    public byte[] CopyBeforeBytes()=>_before.ToArray();
    public byte[] CopyAfterBytes()=>_after.ToArray();
}

/// <summary>
/// Detached Town/Wizard code recipe bound to actual P1 scene and fallback R1
/// sharing the SAME issued R0. Neither a complete EXE/body nor installed code,
/// full dependency closure, native gameplay or publication is issued here.
/// </summary>
public sealed class NativeId65FreshWizardResearchHostPlan
{
    private readonly NativeId65FreshWizardResearchHostSource _source;
    private NativeId65FreshWizardHostLayout Layout=>_source.Layout;
    private NativeId65FreshWizardResearchHostPlan(NativeId65FreshWizardResearchHostSource source)
    {
        _source=source;
        PlanSha256=NativeId65FreshWizardHostLayout.Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,ScenePlanSha256,FallbackResourcePlanSha256,CanonicalDocumentSha256,
            OverlaySha256,InitializerSha256,OverlayByteLength,UsedByteLength,CopyAnchor,AppendedOwners,
            RoleEntries,Fragments,SourceToFinalPc,Transfers,TownPatches,InitializerPatch,RequiredInitializerDispatch,
            PendingOwners,SourceCertified,RuntimeVerified,PublicationApproved,GlobalAudioCompatibilityProven,
            CompleteNativeDependencyClosureProven,CompleteIncomingEdgeClosureProven,RuntimeStackFitProven,
            NativeArenaExhaustionSafe,BodyIssued,FinalExecutableIssued,PersistentOwnerIssued,HardwareTransferProven,InstalledDispatchProven}));
    }
    internal NativeId65FreshWizardResearchScenePlan SceneAncestor=>_source.Scene;
    internal NativeId65FreshWizardResearchFallbackResourcePlan FallbackAncestor=>_source.Fallback;
    internal void RequireAncestry(NativeId65FreshWizardResearchScenePlan scene,NativeId65FreshWizardResearchFallbackResourcePlan fallback)
    {
        if(!ReferenceEquals(scene,SceneAncestor)||!ReferenceEquals(fallback,FallbackAncestor))
            throw new InvalidDataException("Host belongs to another actual scene/fallback pair even if bytes/hash match.");
        NativeId65FreshWizardResearchHostSource.RequireAncestry(scene,fallback);
    }
    public const string ProfileId="fresh-A-P1-N32-Town-distinct-Wizard-roles-detached-host-v1";
    public string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    public string ScenePlanSha256=>SceneAncestor.PlanSha256;
    public string FallbackResourcePlanSha256=>FallbackAncestor.PlanSha256;
    public string CanonicalDocumentSha256=>SceneAncestor.CanonicalDocumentSha256;
    public string PlanSha256{get;}
    public string OverlaySha256=>Layout.OverlaySha256;
    public string InitializerSha256=>Layout.InitializerSha256;
    public int OverlayByteLength=>0x11000;
    public int UsedByteLength=>0x10DB4;
    public uint CopyAnchor=>NativeId65FreshWizardHostLayout.Base+(uint)OverlayByteLength;
    public IReadOnlyList<NativeWizardOverlayCodeOwner> AppendedOwners=>Layout.Owners;
    public IReadOnlyList<NativeFreshWizardHostRole> RoleEntries=>Layout.Roles;
    public IReadOnlyList<NativeFreshWizardHostFragment> Fragments=>Layout.Fragments;
    public IReadOnlyDictionary<uint,uint> SourceToFinalPc=>Layout.Mapping;
    public IReadOnlyList<NativeFreshWizardHostTransfer> Transfers=>Layout.Transfers;
    public IReadOnlyList<NativeFreshWizardHostPatch> TownPatches=>Layout.TownPatches;
    public NativeFreshWizardHostPatch InitializerPatch=>Layout.InitializerPatch;
    public NativeFreshWizardHostDispatchRequirement RequiredInitializerDispatch{get;}=new(0x1CA8,0x8005B6E0,0x8005A898);
    public IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Actual detached code/initializer bytes only; no body/header/final EXE/disc or installed dispatch is issued.",
        "Existing ID65 initializer table substitution is a prerequisite; LAB/music/classifier/save executable complement is not composed here.",
        "Global1F compatibility and complete reachable callback/helper/effect/reward/audio/collider graph remain unresolved.",
        "Native source witnesses stop at unowned visibility/allocator/vector/delete/GTE bodies; whole main/spawn or hardware behavior is not approved.",
        "Nominal2B0 frame/N32 storage is not measured peak or exhaustion safety; allocation33 can corrupt existing native owners.",
        "All incoming/skip/reentry/computed edges and asynchronous lifetimes are not closed; ordinal0 remains scene-only, not durable/card identity.",
        "Later staging must derive from this SAME fallback upload; equal transfer hash is not resource ancestry."});
    public bool SourceCertified=>true;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public bool GlobalAudioCompatibilityProven=>false;
    public bool CompleteNativeDependencyClosureProven=>false;
    public bool CompleteIncomingEdgeClosureProven=>false;
    public bool RuntimeStackFitProven=>false;
    public bool NativeArenaExhaustionSafe=>false;
    public bool BodyIssued=>false;
    public bool FinalExecutableIssued=>false;
    public bool PersistentOwnerIssued=>false;
    public bool HardwareTransferProven=>false;
    public bool InstalledDispatchProven=>false;
    public byte[] CopyOverlay()=>Layout.CopyOverlay();
    public byte[] CopyInitializerPatch()=>Layout.CopyInitializer();
    public void RequireExactOutputs(ReadOnlySpan<byte> overlay,ReadOnlySpan<byte> initializer)=>Layout.RequireExact(overlay,initializer);
    public static async Task<NativeId65FreshWizardResearchHostPlan> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchScenePlan scene,NativeId65FreshWizardResearchFallbackResourcePlan fallback,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(scene);ArgumentNullException.ThrowIfNull(fallback);
        NativeId65FreshWizardResearchHostSource.RequireAncestry(scene,fallback);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        await using FileStream image=new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,token);
        try
        {
            var source=NativeId65FreshWizardResearchHostSource.Capture(image,scene,fallback,token);
            token.ThrowIfCancellationRequested();var result=new NativeId65FreshWizardResearchHostPlan(source);token.ThrowIfCancellationRequested();return result;
        }
        finally
        {
            image.Position=0;
            await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
}
