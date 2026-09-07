using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshDocumentPublicationSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>PublishNew returned, but final verification or ownership cleanup did
/// not complete. These paths are actual retained output/evidence locations, not
/// a successful readback result. Preserve them; never retry by overwriting.</summary>
public sealed class NativeId65FreshDocumentResearchCommittedException : IOException
{
    internal NativeId65FreshDocumentResearchCommittedException(string root, string planSha256,
        string recovery, string stage, string outputLease, string projectLease, Exception inner)
        : base("Saved fresh document artifacts were committed but final verification or cleanup did not complete. Preserve " + root, inner)
    {
        OutputDirectory = root;
        ImagePath = Path.Combine(root, NativeId65FreshDocumentResearchPlan.ImageName);
        CuePath = Path.Combine(root, NativeId65FreshDocumentResearchPublisher.CueName);
        ReceiptPath = Path.Combine(root, NativeId65FreshDocumentResearchPublisher.ReceiptName);
        PlanSha256 = planSha256;
        RetainedRecoveryDirectory = recovery;
        RetainedStageDirectory = stage;
        RetainedOutputLeasePath = outputLease;
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
    public string RetainedProjectLeasePath { get; }
}

// Separate domain: old fixed-profile fault enums/invalid values are unchanged.
internal enum NativeId65FreshDocumentResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication, AfterSavedGuard }

/// <summary>Explicit new-only research publication of an actual saved document.
/// The final Store guard excludes cooperating saves across commit. Source and
/// artifact readback finish noncancellably after commit; subsequent project
/// edits are a separate caller/UI advisory, not artifact failure. No runtime or
/// ordinary export approval is granted. Trusted ancestors remain required.</summary>
public static class NativeId65FreshDocumentResearchPublisher
{
    internal const string CueName = "native-id65-fresh-document-research.cue";
    internal const string ReceiptName = "native-id65-fresh-document-research.receipt.json";

    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(
        NativeId65FreshSavedDocumentExportContext context, string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromSaved(context), outputDirectory,
            NativeId65FreshResearchFault.None, null, token);

    // A retained context can verify captured artifacts after the document has
    // changed. Never manufacture a historical Snapshot from receipt metadata.
    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(
        NativeId65FreshSavedDocumentExportContext context, string outputDirectory, CancellationToken token = default) =>
        await NativeId65FreshResearchPublicationEngine.VerifyAsync(
            NativeId65FreshResearchPublicationBinding.FromSaved(context), outputDirectory, token);

    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(
        NativeId65FreshSavedDocumentExportContext context, string directory, NativeId65FreshDocumentResearchFault fault,
        Action<NativeId65FreshDocumentResearchFault, string>? checkpoint = null, CancellationToken token = default)
    {
        if (fault == NativeId65FreshDocumentResearchFault.None)
            throw new InvalidDataException("A saved-document staging exercise must force a precommit fault.");
        return PublishForSmokeCoreAsync(context, directory, fault, checkpoint, token);
    }

    private static async Task<NativeId65FreshDiscResearchPublication> PublishForSmokeCoreAsync(
        NativeId65FreshSavedDocumentExportContext context, string directory, NativeId65FreshDocumentResearchFault fault,
        Action<NativeId65FreshDocumentResearchFault, string>? checkpoint, CancellationToken token) =>
        await NativeId65FreshResearchPublicationEngine.PublishDocumentForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromSaved(context), directory, fault, checkpoint, token);

    internal static byte[] BuildReceiptForSmoke(NativeId65FreshSavedDocumentExportContext context, NativeId65FreshDiscVerification verified) =>
        NativeId65FreshResearchPublicationBinding.FromSaved(context).BuildReceipt(verified);

    internal static byte[] BuildReceipt(NativeId65FreshSavedDocumentExportContext context, NativeId65FreshDiscVerification verified)
    {
        ArgumentNullException.ThrowIfNull(verified);
        var plan = context.Plan;
        if (verified.OutputSha256 != plan.ExpectedImageSha256 || verified.ChangedSectors != plan.ExpectedChangedRawSectors)
            throw new InvalidDataException("Saved-document receipt requires the issued plan's complete derived afterimage.");
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = "native-id65-fresh-saved-document-research-v1", planSha256 = plan.PlanSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            sourceCueSha256 = plan.SourceCueSha256, sourceImagePath = plan.SourceImagePath, sourceCuePath = plan.SourceCuePath,
            discPlanSha256 = plan.DiscPlanSha256, bodyPlanSha256 = plan.BodyPlanSha256,
            canonicalDocumentJson = Encoding.UTF8.GetString(plan.CopyCanonicalDocument()),
            canonicalDocumentSha256 = plan.CanonicalDocumentSha256,
            savedProject = new { rootPath = context.Snapshot.RootPath, projectId = context.Snapshot.ProjectId,
                manifestSha256 = context.Snapshot.ManifestSha256, documentSha256 = context.Snapshot.ContentSha256 },
            bodySha256 = plan.BodySha256, overlaySha256 = plan.OverlaySha256,
            executableSha256 = plan.ExecutableSha256, wadDirectorySha256 = plan.WadDirectorySha256,
            rootDirectorySha256 = plan.IsoRootDirectorySha256, cueSha256 = Convert.ToHexStringLower(SHA256.HashData(plan.CopyCueBytes())),
            imageSha256 = verified.OutputSha256, imageByteLength = plan.ExpectedImageByteLength,
            changedRawSectors = verified.ChangedSectors, sourceReadbackRequired = true, researchOnly = true,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            checkpointOrForeignStateSupported = false, pendingNativeOwners = plan.PendingNativeOwners
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
