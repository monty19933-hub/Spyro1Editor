namespace Spyro.Editor.Core.Exporting;

internal enum NativeId65FreshEditedDocumentResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }

/// <summary>Exact new-only research artifacts. Trusted ancestors and cooperating
/// writers are required; this does not claim crash atomicity or native acceptance.</summary>
public static class NativeId65FreshEditedDocumentResearchPublisher
{
    // Keep binding construction inside the async boundary: null plans historically
    // return an already-faulted Task rather than throwing from invocation.
    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromEdited(plan), outputDirectory,
            NativeId65FreshResearchFault.None, null, token);

    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.VerifyAsync(
            NativeId65FreshResearchPublicationBinding.FromEdited(plan), outputDirectory, token);

    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, NativeId65FreshEditedDocumentResearchFault fault,
        Action<NativeId65FreshEditedDocumentResearchFault, string>? checkpoint = null, CancellationToken token = default)
    {
        // This deliberate-fault requirement historically throws synchronously.
        if (fault == NativeId65FreshEditedDocumentResearchFault.None)
            throw new InvalidDataException("A staging exercise must force a precommit fault.");
        return PublishForSmokeCoreAsync(plan, outputDirectory, fault, checkpoint, token);
    }

    private static async Task<NativeId65FreshDiscResearchPublication> PublishForSmokeCoreAsync(
        NativeId65FreshEditedDocumentResearchPlan plan, string outputDirectory, NativeId65FreshEditedDocumentResearchFault fault,
        Action<NativeId65FreshEditedDocumentResearchFault, string>? checkpoint, CancellationToken token) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromEdited(plan), outputDirectory, (NativeId65FreshResearchFault)fault,
            checkpoint == null ? null : (point, image) => checkpoint((NativeId65FreshEditedDocumentResearchFault)point, image), token);

}
