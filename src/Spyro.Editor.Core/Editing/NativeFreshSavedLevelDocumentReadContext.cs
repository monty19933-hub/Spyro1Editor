namespace Spyro.Editor.Core.Editing;

/// <summary>
/// Neutral actual saved-aggregate lineage and a final cooperating-writer guard.
/// No compiler, source, candidate, native eligibility or publication authority.
/// Raw versions remain distinct from equal canonical content or editor drafts.
/// </summary>
internal sealed class NativeFreshSavedLevelDocumentReadContext
{
    private NativeFreshSavedLevelDocumentReadContext(NativeFreshLevelDocumentSnapshot snapshot) => Snapshot = snapshot;
    internal NativeFreshLevelDocumentSnapshot Snapshot { get; }
    internal NativeFreshLevelDocumentIntent Document => Snapshot.Intent!;

    internal static NativeFreshSavedLevelDocumentReadContext Capture(NativeFreshLevelDocumentSnapshot snapshot,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        NativeFreshDocumentStorage.RequireAggregateCurrent(snapshot, token);
        return new(snapshot); // Never adopt the independently parsed recheck's document.
    }

    internal void RequireDocument(NativeFreshLevelDocumentIntent actualDocument)
    {
        ArgumentNullException.ThrowIfNull(actualDocument);
        if (!ReferenceEquals(actualDocument, Document))
            throw new InvalidDataException("Read context belongs to another actual saved aggregate, even if canonical bytes match.");
    }

    // Acquire after long work; a context alone does not freeze future disk state.
    // Every caller must Complete with its primary error and account for failure.
    internal NativeFreshLevelDocumentStore.ReadLease AcquireFinalReadGuard(CancellationToken token = default) =>
        NativeFreshLevelDocumentStore.AcquireReadLease(Snapshot, token);
}
