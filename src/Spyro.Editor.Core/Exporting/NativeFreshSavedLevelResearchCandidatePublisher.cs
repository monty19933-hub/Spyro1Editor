namespace Spyro.Editor.Core.Exporting;

// New internal arm only. No UI/public snapshot-adoption facade in this tranche.
internal static class NativeFreshSavedLevelResearchCandidatePublisher
{
    internal static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(
        NativeFreshSavedLevelResearchCandidatePlan plan, string directory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan), directory,
            NativeId65FreshResearchFault.None, null, token);

    // Internal, explicit physical smoke only. Normal Publish never supplies actions.
    internal static async Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(
        NativeFreshSavedLevelResearchCandidatePlan plan, string directory,
        NativeId65FreshResearchFault fault, Action<NativeId65FreshResearchFault, string>? checkpoint,
        Action<string>? prepareTransactionLock,
        Action<NativeFreshSavedLevelResearchPoint, string>? observation, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishSavedLevelForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan), directory,
            fault, checkpoint, prepareTransactionLock, observation, token);

    internal static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(
        NativeFreshSavedLevelResearchCandidatePlan plan, string directory, CancellationToken token = default)
    {
        var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        token.ThrowIfCancellationRequested();
        NativeId65FreshDiscResearchPublication? result = null; List<Exception> failures = [];
        try { result = await NativeId65FreshResearchPublicationEngine.VerifyAsync(binding, directory, token); }
        catch (Exception error) { failures.Add(error); }
        // Missing/invalid artifacts still receive source AND raw-version finals.
        // This read-only method never creates a known-commit classification.
        try { await plan.RequireFreshAsync(CancellationToken.None); }
        catch (Exception error) { failures.Add(error); }
        NativeFreshSavedLevelResearchCandidatePlan.ThrowFailures(failures);
        return result!;
    }
}

// Constructed only after an actual returned PublishNew/result or by retaining
// that existing typed outcome while folding a later independent source error.
internal sealed class NativeFreshSavedLevelResearchCommittedException : IOException
{
    internal NativeFreshSavedLevelResearchCommittedException(string root,
        NativeFreshSavedLevelResearchCandidatePlan plan, string recovery, string stage,
        string outputLease, string projectLease, Exception inner)
        : base("Saved-level artifacts were committed but final verification or owner accounting did not complete. Preserve " + root, inner)
    {
        OutputDirectory = root;
        ImagePath = Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ImageName);
        CuePath = Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.CueName);
        ReceiptPath = Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.ReceiptName);
        PlanSha256 = plan.PlanSha256; RetainedRecoveryDirectory = recovery;
        RetainedStageDirectory = stage; RetainedOutputLeasePath = outputLease;
        RetainedProjectLeasePath = projectLease;
    }
    internal string OutputDirectory { get; }
    internal string ImagePath { get; }
    internal string CuePath { get; }
    internal string ReceiptPath { get; }
    internal string PlanSha256 { get; }
    internal string RetainedRecoveryDirectory { get; }
    internal string RetainedStageDirectory { get; }
    internal string RetainedOutputLeasePath { get; }
    // For failed acquisition, this is inspection-only: no returned owner exists.
    internal string RetainedProjectLeasePath { get; }
}

// The Engine is the sole producer of these actual temporal observation points.
internal enum NativeFreshSavedLevelResearchPoint { AfterReturnBeforeGuardComplete, AfterFinalArtifactAttempt }
