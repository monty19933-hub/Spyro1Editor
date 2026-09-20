using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Detached physical afterimages for an actual placed chest and experimental
/// target20 executable. Source identity is not a complete census, gameplay or
/// publication certificate. No destination writer or candidate is supplied.
/// </summary>
internal sealed class NativeFreshLockedChestResearchDiscPlan
{
    private readonly NativeId65FreshDiscSource _original;
    private readonly NativeFreshResearchPhysicalSourceOwners _owners;
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly NativeFreshResearchPhysicalAfterimages _afterimages;
    private NativeFreshLockedChestResearchDiscPlan(NativeId65FreshDiscSource original,
        NativeFreshResearchPhysicalSourceOwners owners,NativeFreshLockedChestExperimentalExecutable executable)
    {
        _original=original;_owners=owners;ExecutableAncestor=executable;
        _geometry=NativeId65FreshWizardDiscGeometry.CompileChest(executable);
        RequireAncestry(executable);RequireRetainedOriginalOwners();
        _afterimages=NativeFreshResearchPhysicalAfterimages.ForChest(owners,executable);
        BodySha256=Hash(_afterimages.CopyBody());OverlaySha256=Hash(_afterimages.CopyOverlay());
        ExecutableSha256=Hash(_afterimages.CopyExecutable());WadDirectorySha256=Hash(_afterimages.CopyWadDirectory());
        IsoRootSha256=Hash(_afterimages.CopyIsoRootDirectory());
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{ProfileId,SourceImageSha256,
            ExecutablePlanSha256,PlacementPlanSha256,BodyPlanSha256,HostPlanSha256,
            ResourcePlanSha256,BodySha256,OverlaySha256,ExecutableSha256,WadDirectorySha256,IsoRootSha256,
            BodyLba,FinalExecutableLba,FinalWadByteLength,FinalExecutableEndExclusive,ChangedSectorOwnerCount,
            ExperimentalTreasureTarget,ExperimentalKeyZeroAssumption,PendingOwners,SourceCertified,
            CompleteStaticCensusProven,CompleteNativeDependencyClosureProven,RuntimeVerified,PublicationApproved,NormalCreateBinEnabled}));
    }
    internal const string ProfileId="internal-placed-fresh-chest-experimental-target20-detached-disc-v1";
    internal NativeFreshLockedChestExperimentalExecutable ExecutableAncestor{get;}
    internal NativeFreshLockedChestPlacementPlan PlacementAncestor=>ExecutableAncestor.PlacementAncestor;
    internal string SourceImageSha256=>NativeId65FreshDiscSource.OriginalSha256;
    internal string ExecutablePlanSha256=>ExecutableAncestor.PlanSha256;
    internal string PlacementPlanSha256=>PlacementAncestor.PlanSha256;
    internal string BodyPlanSha256=>ExecutableAncestor.BodyAncestor.PlanSha256;
    internal string HostPlanSha256=>ExecutableAncestor.HostAncestor.PlanSha256;
    internal string ResourcePlanSha256=>ExecutableAncestor.BodyAncestor.ResourceAncestor.PlanSha256;
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
    internal int ExperimentalTreasureTarget=>ExecutableAncestor.ExperimentalTreasureTarget;
    internal bool ExperimentalKeyZeroAssumption=>true;
    internal bool SourceCertified=>true;
    internal bool CompleteStaticCensusProven=>false;
    internal bool CompleteNativeDependencyClosureProven=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool NormalCreateBinEnabled=>false;
    internal IReadOnlyList<string> PendingOwners{get;}=Array.AsReadOnly(new[]{
        "Experimental target20 retains the explicit unproven key-zero contribution premise; no complete static census or runtime drop guarantee is issued.",
        "Decoded centre containment and exact coordinate linkage do not certify actor footprint, approach clearance or Wizard interference.",
        "Physical full-image authentication/inverse does not prove full native callback, GTE, dynamic lifetime, hardware transfer or gameplay behavior.",
        "No destination writer, CUE, receipt, saved-project candidate, normal Create BIN route or publication approval is supplied."});
    internal byte[] CopyBody()=>_afterimages.CopyBody();
    internal byte[] CopyOverlay()=>_afterimages.CopyOverlay();
    internal byte[] CopyExecutable()=>_afterimages.CopyExecutable();
    internal byte[] CopyWadDirectory()=>_afterimages.CopyWadDirectory();
    internal byte[] CopyIsoRootDirectory()=>_afterimages.CopyIsoRootDirectory();
    internal void RequireAncestry(NativeFreshLockedChestExperimentalExecutable executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if(!ReferenceEquals(executable,ExecutableAncestor))
            throw new InvalidDataException("Chest disc belongs to another actual experimental executable, even when all hashes are equal.");
        RequireExecutable(executable);
    }
    private void RequireRetainedOriginalOwners()
    {
        RequireOriginalExecutable(_original,ExecutableAncestor);
        _owners.RequireRetainedOriginalOwners(_original,_geometry);
    }
    private static void RequireExecutable(NativeFreshLockedChestExperimentalExecutable executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        executable.RequireAncestry(executable.PlacementAncestor,executable.HostAncestor);
        executable.RequireExactExecutable(executable.CopyExecutable());
        executable.PlacementAncestor.RequireExactLevelData(executable.PlacementAncestor.CopyLevelData());
        if(!executable.SourceCertified||!executable.Experimental||!executable.GrassPassiveDispatchPreserved||
            executable.SourceImageSha256!=NativeId65FreshDiscSource.OriginalSha256||
            executable.ExperimentalTreasureTarget!=20||executable.KnownDeterministicSelectorSubtotal!=20||
            executable.KnownSelectorCount!=7||executable.Rows.Count!=9||
            executable.Rows[2].Premise!="experimental-key-zero-not-certified"||executable.Rows[2].AssumedContribution!=0||
            executable.KeyZeroContributionProven||executable.CompleteStaticCensusProven||executable.RuntimeDropGuarantee||
            executable.CompleteNativeDependencyClosureProven||executable.RuntimeVerified||executable.PublicationApproved)
            throw new InvalidDataException("Chest physical capture requires the actual placed, explicitly experimental target20 executable and unresolved key premise.");
        _=NativeId65FreshWizardDiscGeometry.CompileChest(executable);
    }
    private static void RequireOriginalExecutable(NativeId65FreshDiscSource original,
        NativeFreshLockedChestExperimentalExecutable executable)
    {
        if(!executable.RevertExactOriginal(executable.CopyExecutable()).AsSpan().SequenceEqual(original.CopyExecutable()))
            throw new InvalidDataException("Chest executable is not bound to this complete authenticated Original executable owner.");
    }
    internal static async Task<NativeFreshLockedChestResearchDiscPlan> CaptureAsync(string originalBin,
        NativeFreshLockedChestExperimentalExecutable executable,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();RequireExecutable(executable);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);string path=Path.GetFullPath(originalBin);
        FileStream? held=null;NativeId65FreshDiscSource? original=null;
        NativeFreshResearchPhysicalSourceOwners? owners=null;NativeId65FreshWizardDiscGeometry? geometry=null;
        NativeFreshLockedChestResearchDiscPlan? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,token);
            original=await NativeId65FreshDiscSource.CaptureAsync(held,token);
            RequireOriginalExecutable(original,executable);
            geometry=NativeId65FreshWizardDiscGeometry.CompileChest(executable);
            owners=NativeFreshResearchPhysicalSourceOwners.Capture(held,original,geometry,token);
            token.ThrowIfCancellationRequested();result=new(original,owners,executable);
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            // Every acquired observation finalizes independently, even after
            // cancellation/partial acquisition. Preserve the primary failure.
            if(held is not null)await TryAsync(()=>NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Try(current.Dispose);}
            Try(()=>RequireExecutable(executable));
            if(original is not null)Try(()=>RequireOriginalExecutable(original,executable));
            if(owners is not null&&original is not null&&geometry is not null)
                Try(()=>owners.RequireRetainedOriginalOwners(original,geometry));
            if(held is not null)Try(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();return result??throw new InvalidDataException("Chest physical source capture returned no result.");
        async Task TryAsync(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Try(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }
    internal byte[] CreateExpectedRawSector(int lba,ReadOnlySpan<byte> sourceSector)=>
        _afterimages.CreateExpectedRawSector(lba,sourceSector);
    internal byte[] CreateOriginalRawSector(int lba,ReadOnlySpan<byte> finalSector)=>
        _afterimages.CreateOriginalRawSector(lba,finalSector);
    internal NativeId65FreshDiscVerification VerifyImage(Stream original,Stream output,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();RequireAncestry(ExecutableAncestor);
        return _afterimages.VerifyImage(original,output,token);
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>NativeId65FreshExecutablePlan.Hash(bytes);
}
