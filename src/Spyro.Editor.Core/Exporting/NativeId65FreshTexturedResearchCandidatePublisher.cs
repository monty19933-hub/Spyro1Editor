namespace Spyro.Editor.Core.Exporting;

/// <summary>PublishNew actually returned, but final verification or accounted
/// cleanup failed. Retained paths are evidence, not verified success.</summary>
public sealed class NativeId65FreshTexturedResearchCommittedException : IOException
{
    internal NativeId65FreshTexturedResearchCommittedException(string root,
        NativeId65FreshTexturedResearchCandidatePlan plan, string recovery, string stage, string lease, Exception inner)
        : base("Fresh textured artifacts were committed but final verification or cleanup did not complete. Preserve " + root, inner)
    {
        OutputDirectory = root;
        ImagePath = Path.Combine(root, NativeId65FreshTexturedResearchCandidatePlan.ImageName);
        CuePath = Path.Combine(root, NativeId65FreshTexturedResearchCandidatePlan.CueName);
        ReceiptPath = Path.Combine(root, NativeId65FreshTexturedResearchCandidatePlan.ReceiptName);
        PlanSha256 = plan.PlanSha256;
        RetainedRecoveryDirectory = recovery; RetainedStageDirectory = stage; RetainedOutputLeasePath = lease;
    }
    public string OutputDirectory { get; }
    public string ImagePath { get; }
    public string CuePath { get; }
    public string ReceiptPath { get; }
    public string PlanSha256 { get; }
    public string RetainedRecoveryDirectory { get; }
    public string RetainedStageDirectory { get; }
    public string RetainedOutputLeasePath { get; }
}

/// <summary>Exact new-only fixed-a research publication through the existing
/// transaction. Trusted ancestors/cooperating writers required; no runtime approval.</summary>
public static class NativeId65FreshTexturedResearchCandidatePublisher
{
    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(
        NativeId65FreshTexturedResearchCandidatePlan plan, string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromTextured(plan), outputDirectory,
            NativeId65FreshResearchFault.None, null, token);

    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(
        NativeId65FreshTexturedResearchCandidatePlan plan, string outputDirectory, CancellationToken token = default)
    {
        var binding = NativeId65FreshResearchPublicationBinding.FromTextured(plan);
        token.ThrowIfCancellationRequested();
        NativeId65FreshDiscResearchPublication? result = null;
        List<Exception> failures = [];
        try { result = await NativeId65FreshResearchPublicationEngine.VerifyAsync(binding, outputDirectory, token); }
        catch (Exception error) { failures.Add(error); }
        // Invalid/missing artifacts still receive independent final SOURCE checks.
        // This cannot infer a commit or repair a failed artifact observation.
        try { await plan.RequireFreshAsync(CancellationToken.None); }
        catch (Exception error) { failures.Add(error); }
        NativeId65FreshTexturedResearchCandidatePlan.ThrowFailures(failures);
        return result!;
    }
}
