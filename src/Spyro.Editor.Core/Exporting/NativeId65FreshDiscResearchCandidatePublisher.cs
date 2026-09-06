namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65FreshDiscResearchPublication(string OutputDirectory, string ImagePath,
    string CuePath, string ReceiptPath, string ImageSha256, string PlanSha256, int ChangedRawSectors,
    string RetainedRecoveryDirectory = "")
{
    public bool WholeImageReadback => true;
    public bool ResearchOnly => true;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;
}

internal enum NativeId65FreshDiscResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }

/// <summary>Exact new-only research artifacts. Trusted ancestors and cooperating
/// writers are required; this does not claim crash atomicity or native acceptance.</summary>
public static class NativeId65FreshDiscResearchCandidatePublisher
{
    // Keep binding construction inside the async boundary: null plans historically
    // return an already-faulted Task rather than throwing from invocation.
    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(NativeId65FreshDiscResearchCandidatePlan plan,
        string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromStarter(plan), outputDirectory,
            NativeId65FreshResearchFault.None, null, token);

    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(NativeId65FreshDiscResearchCandidatePlan plan,
        string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.VerifyAsync(
            NativeId65FreshResearchPublicationBinding.FromStarter(plan), outputDirectory, token);

    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(NativeId65FreshDiscResearchCandidatePlan plan,
        string outputDirectory, NativeId65FreshDiscResearchFault fault,
        Action<NativeId65FreshDiscResearchFault, string>? checkpoint = null, CancellationToken token = default)
    {
        // This deliberate-fault requirement historically throws synchronously.
        if (fault == NativeId65FreshDiscResearchFault.None)
            throw new InvalidDataException("A staging exercise must force a precommit fault.");
        return PublishForSmokeCoreAsync(plan, outputDirectory, fault, checkpoint, token);
    }

    private static async Task<NativeId65FreshDiscResearchPublication> PublishForSmokeCoreAsync(
        NativeId65FreshDiscResearchCandidatePlan plan, string outputDirectory, NativeId65FreshDiscResearchFault fault,
        Action<NativeId65FreshDiscResearchFault, string>? checkpoint, CancellationToken token) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromStarter(plan), outputDirectory, (NativeId65FreshResearchFault)fault,
            checkpoint == null ? null : (point, image) => checkpoint((NativeId65FreshDiscResearchFault)point, image), token);

    internal static void RequireCompanion(Stream input, ReadOnlySpan<byte> expected, CancellationToken token) =>
        NativeId65FreshResearchPublicationEngine.RequireCompanion(input, expected, token);
}
