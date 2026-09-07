using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Actual saved-document lineage paired with an issued source-neutral research
/// plan. This read-only context has no artifact writer or publication approval.
/// Its internal final guard excludes cooperating Store saves, not hostile file
/// races, external editors, moved ancestors or cross-filesystem crash windows.
/// </summary>
public sealed class NativeId65FreshSavedDocumentExportContext
{
    private NativeId65FreshSavedDocumentExportContext(NativeId65FreshDocumentSnapshot snapshot,
        NativeId65FreshDocumentResearchPlan plan)
    {
        Snapshot = snapshot;
        Plan = plan;
    }

    public NativeId65FreshDocumentSnapshot Snapshot { get; }
    public NativeId65FreshDocumentResearchPlan Plan { get; }

    public static NativeId65FreshSavedDocumentExportContext Capture(NativeId65FreshDocumentSnapshot snapshot,
        NativeId65FreshDocumentResearchPlan plan, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(plan);
        if (!snapshot.Exists || snapshot.Intent == null || snapshot.RetainedRecoveryDirectory.Length != 0)
            throw new InvalidDataException("A saved fresh document with resolved save recovery is required.");
        if (!NativeId65FreshDocumentCodec.SerializeCanonical(snapshot.Intent).AsSpan().SequenceEqual(plan.CopyCanonicalDocument()))
            throw new InvalidDataException("The actual saved intent differs from the privately compiled research plan.");
        var current = NativeId65FreshDocumentStore.Capture(new(snapshot.RootPath, snapshot.ProjectId), token);
        if (current.RootPath != snapshot.RootPath || current.ProjectId != snapshot.ProjectId ||
            current.ManifestSha256 != snapshot.ManifestSha256 || current.ContentSha256 != snapshot.ContentSha256)
            throw new InvalidDataException("The saved fresh document or project manifest changed since capture.");
        token.ThrowIfCancellationRequested();
        return new(snapshot, plan);
    }

    // The future publication service must acquire this only after long staging
    // and readback work, hold it across final commit, then account for any
    // retained release evidence. Initial Capture alone is not a commit guard.
    internal NativeId65FreshDocumentStore.ReadLease AcquireFinalPrecommitGuard(CancellationToken token = default) =>
        NativeId65FreshDocumentStore.AcquireReadLease(Snapshot, token);
}
