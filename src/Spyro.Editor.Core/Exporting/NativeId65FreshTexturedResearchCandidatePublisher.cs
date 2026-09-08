using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65TexturedResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal enum NativeId65FreshTexturedResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }
internal enum NativeId65FreshTexturedPostcommitPoint { AfterCommit, BeforeStageCleanup }

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
    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(
        NativeId65FreshTexturedResearchCandidatePlan plan, string directory, NativeId65FreshTexturedResearchFault fault,
        Action<NativeId65FreshTexturedResearchFault, string>? checkpoint = null, CancellationToken token = default)
    {
        if (fault == NativeId65FreshTexturedResearchFault.None)
            throw new InvalidDataException("A textured staging exercise must force a precommit fault.");
        return PublishFaultCore(plan, directory, fault, checkpoint, token);
    }
    private static async Task<NativeId65FreshDiscResearchPublication> PublishFaultCore(
        NativeId65FreshTexturedResearchCandidatePlan plan, string directory, NativeId65FreshTexturedResearchFault fault,
        Action<NativeId65FreshTexturedResearchFault, string>? checkpoint, CancellationToken token)
    {
        var binding = NativeId65FreshResearchPublicationBinding.FromTextured(plan);
        var shared = fault switch
        {
            NativeId65FreshTexturedResearchFault.AfterFirstSector => NativeId65FreshResearchFault.AfterFirstSector,
            NativeId65FreshTexturedResearchFault.AfterImageWrite => NativeId65FreshResearchFault.AfterImageWrite,
            NativeId65FreshTexturedResearchFault.BeforePublication => NativeId65FreshResearchFault.BeforePublication,
            _ => throw new InvalidDataException("Unknown textured precommit fault.")
        };
        return await NativeId65FreshResearchPublicationEngine.PublishAsync(binding, directory, shared,
            checkpoint == null ? null : (point, image) => checkpoint(point switch
            {
                NativeId65FreshResearchFault.AfterFirstSector => NativeId65FreshTexturedResearchFault.AfterFirstSector,
                NativeId65FreshResearchFault.AfterImageWrite => NativeId65FreshTexturedResearchFault.AfterImageWrite,
                NativeId65FreshResearchFault.BeforePublication => NativeId65FreshTexturedResearchFault.BeforePublication,
                _ => throw new InvalidDataException("Unknown shared precommit checkpoint.")
            }, image), token);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishPostcommitForSmokeAsync(
        NativeId65FreshTexturedResearchCandidatePlan plan, string directory,
        Action<NativeId65FreshTexturedPostcommitPoint, string> checkpoint, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishTexturedPostcommitForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromTextured(plan), directory, checkpoint, token);

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishUnconfirmedForSmokeAsync(
        NativeId65FreshTexturedResearchCandidatePlan plan, string directory, Action<string> checkpoint,
        CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishTexturedUnconfirmedForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromTextured(plan), directory, checkpoint, token);

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
