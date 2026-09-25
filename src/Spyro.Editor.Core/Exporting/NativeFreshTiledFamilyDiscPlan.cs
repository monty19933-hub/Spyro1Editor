using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached physical composition for an actual tiled executable and its actual
/// captured parent disc. Reuses that parent's private Original/index/preimage
/// owners through the shared afterimage kernel. It captures no new source and
/// grants no loader, runtime, candidate, normal export or publication approval.
/// </summary>
internal sealed class NativeFreshTiledFamilyDiscPlan
{
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly NativeFreshResearchPhysicalAfterimages _afterimages;
    private NativeFreshTiledFamilyDiscPlan(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestResearchDiscPlan parent)
    {
        ExecutableAncestor=executable;ParentDiscAncestor=parent;RequireAncestry(executable,parent);
        _geometry=NativeId65FreshWizardDiscGeometry.CompileTiledFamily(executable);
        _afterimages=parent.CreateTiledFamilyAfterimages(executable);
        byte[] body=_afterimages.CopyBody(),exe=_afterimages.CopyExecutable();
        executable.RequireExactOutputs(body,exe);
        if(!_afterimages.CopyOverlay().AsSpan().SequenceEqual(parent.CopyOverlay())||
            !exe.AsSpan().SequenceEqual(parent.CopyExecutable())||
            !_afterimages.CopyWadDirectory().AsSpan().SequenceEqual(parent.CopyWadDirectory())||
            !_afterimages.CopyIsoRootDirectory().AsSpan().SequenceEqual(parent.CopyIsoRootDirectory())||
            !executable.RevertExactLevelData(body).AsSpan().SequenceEqual(parent.CopyBody())||
            BodyLba!=parent.BodyLba||FinalExecutableLba!=parent.FinalExecutableLba||
            FinalWadByteLength!=parent.FinalWadByteLength||FinalExecutableEndExclusive!=parent.FinalExecutableEndExclusive||
            ChangedSectorOwnerCount!=parent.ChangedSectorOwnerCount)
            throw new InvalidDataException("Tiled physical composition changed a retained parent owner, physical allocation or immediate Body inverse.");
        BodySha256=Hash(body);ExecutableSha256=Hash(exe);OverlaySha256=Hash(_afterimages.CopyOverlay());
        WadDirectorySha256=Hash(_afterimages.CopyWadDirectory());IsoRootSha256=Hash(_afterimages.CopyIsoRootDirectory());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            ProfileId,SourceImageSha256,CanonicalDocumentSha256,ExecutablePlanSha256,ParentDiscPlanSha256,
            BodyPlan=executable.BodyAncestor.PlanSha256,TerrainPlan=executable.TerrainAncestor.PlanSha256,
            PlacementPlan=executable.PlacementAncestor.PlanSha256,HostPlan=executable.HostAncestor.PlanSha256,
            BodySha256,OverlaySha256,ExecutableSha256,WadDirectorySha256,IsoRootSha256,
            BodyLba,FinalExecutableLba,FinalWadByteLength,FinalExecutableEndExclusive,ChangedSectorOwnerCount,
            ExperimentalTreasureTarget,ExperimentalKeyZeroAssumption,SourceCertified,FullNativeLoaderProven,
            CompleteStaticCensusProven,CompleteNativeDependencyClosureProven,PeakRuntimeMemoryProven,
            RuntimeVerified,PublicationApproved,NormalCreateBinEnabled}));
    }
    internal const string ProfileId="source-bound-tiled-family-shared-parent-physical-owners-v1";
    internal NativeFreshTiledFamilyExecutablePlan ExecutableAncestor{get;}
    internal NativeFreshLockedChestResearchDiscPlan ParentDiscAncestor{get;}
    internal string SourceImageSha256=>ParentDiscAncestor.SourceImageSha256;
    internal string CanonicalDocumentSha256=>ExecutableAncestor.CanonicalDocumentSha256;
    internal string ExecutablePlanSha256=>ExecutableAncestor.PlanSha256;
    internal string ParentDiscPlanSha256=>ParentDiscAncestor.PlanSha256;
    internal string BodySha256{get;}
    internal string OverlaySha256{get;}
    internal string ExecutableSha256{get;}
    internal string WadDirectorySha256{get;}
    internal string IsoRootSha256{get;}
    internal string PlanSha256{get;}
    internal int BodyLba=>_geometry.BodyLba;
    internal int FinalExecutableLba=>_geometry.FinalExecutableLba;
    internal int FinalWadByteLength=>_geometry.FinalWadByteLength;
    internal int FinalExecutableEndExclusive=>_geometry.FinalExecutableEndExclusive;
    internal int ChangedSectorOwnerCount=>_geometry.ChangedSectorOwnerCount;
    internal int ExperimentalTreasureTarget=>ParentDiscAncestor.ExperimentalTreasureTarget;
    internal bool ExperimentalKeyZeroAssumption=>ParentDiscAncestor.ExperimentalKeyZeroAssumption;
    internal bool SourceCertified=>true;
    internal bool FullNativeLoaderProven=>false;
    internal bool CompleteStaticCensusProven=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool PeakRuntimeMemoryProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool NormalCreateBinEnabled=>false;
    internal byte[] CopyBody()=>_afterimages.CopyBody();
    internal byte[] CopyOverlay()=>_afterimages.CopyOverlay();
    internal byte[] CopyExecutable()=>_afterimages.CopyExecutable();
    internal byte[] CopyWadDirectory()=>_afterimages.CopyWadDirectory();
    internal byte[] CopyIsoRootDirectory()=>_afterimages.CopyIsoRootDirectory();
    internal void RequireAncestry(NativeFreshTiledFamilyExecutablePlan executable,NativeFreshLockedChestResearchDiscPlan parent)
    {
        ArgumentNullException.ThrowIfNull(executable);ArgumentNullException.ThrowIfNull(parent);
        if(!ReferenceEquals(executable,ExecutableAncestor)||!ReferenceEquals(parent,ParentDiscAncestor)||
            !ReferenceEquals(parent.ExecutableAncestor,executable.ParentExecutableAncestor))
            throw new InvalidDataException("Tiled disc requires its actual tiled executable and captured parent/Original ownership, not equal replacements.");
        executable.RequireAncestry(executable.BodyAncestor,executable.ParentExecutableAncestor);
        parent.RequireAncestry(executable.ParentExecutableAncestor);
    }
    internal static NativeFreshTiledFamilyDiscPlan Compose(NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestResearchDiscPlan parent)
    {ArgumentNullException.ThrowIfNull(executable);ArgumentNullException.ThrowIfNull(parent);return new(executable,parent);}
    internal byte[] CreateExpectedRawSector(int lba,ReadOnlySpan<byte> sourceSector)=>
        _afterimages.CreateExpectedRawSector(lba,sourceSector);
    internal byte[] CreateOriginalRawSector(int lba,ReadOnlySpan<byte> finalSector)=>
        _afterimages.CreateOriginalRawSector(lba,finalSector);
    internal NativeId65FreshDiscVerification VerifyImage(Stream original,Stream output,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();RequireAncestry(ExecutableAncestor,ParentDiscAncestor);
        return _afterimages.VerifyImage(original,output,token);
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>NativeId65FreshExecutablePlan.Hash(bytes);
}
