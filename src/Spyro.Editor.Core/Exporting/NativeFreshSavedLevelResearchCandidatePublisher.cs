namespace Spyro.Editor.Core.Exporting;

// One actual saved material-only arm; no receipt adoption or caller writer authority.
public static class NativeFreshSavedLevelResearchCandidatePublisher
{
    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(
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

    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(
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
public sealed class NativeFreshSavedLevelResearchCommittedException : IOException
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
    public string OutputDirectory { get; }
    public string ImagePath { get; }
    public string CuePath { get; }
    public string ReceiptPath { get; }
    public string PlanSha256 { get; }
    public string RetainedRecoveryDirectory { get; }
    public string RetainedStageDirectory { get; }
    public string RetainedOutputLeasePath { get; }
    // For failed acquisition, this is inspection-only: no returned owner exists.
    public string RetainedProjectLeasePath { get; }
}

// The Engine is the sole producer of these actual temporal observation points.
internal enum NativeFreshSavedLevelResearchPoint { AfterReturnBeforeGuardComplete, AfterFinalArtifactAttempt }
