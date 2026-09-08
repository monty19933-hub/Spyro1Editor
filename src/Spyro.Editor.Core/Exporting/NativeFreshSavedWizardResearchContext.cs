using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Actual saved-aggregate lineage paired with its issued Wizard Candidate.
/// No writer, publication role, receipt change or UI/normal-export authority.
/// Capture is a raw revision recheck, not a long-lived lock or source refresh.
/// </summary>
internal sealed class NativeFreshSavedWizardResearchContext
{
    private NativeFreshSavedWizardResearchContext(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshWizardResearchCandidatePlan candidate)
    {
        Saved = saved;
        Candidate = candidate;
    }

    internal NativeFreshSavedLevelDocumentReadContext Saved { get; }
    internal NativeId65FreshWizardResearchCandidatePlan Candidate { get; }

    internal static NativeFreshSavedWizardResearchContext Capture(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshWizardResearchCandidatePlan candidate, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(candidate);
        RequirePair(saved, candidate); // Wrong actual ancestry refuses before any project I/O.
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        token.ThrowIfCancellationRequested();
        return new(saved, candidate); // Never adopt the independently parsed freshness recheck.
    }

    internal void RequireCandidate(NativeId65FreshWizardResearchCandidatePlan actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (!ReferenceEquals(actual, Candidate))
            throw new InvalidDataException("Saved Wizard context belongs to another actual Candidate, even when all bytes/hashes agree.");
        RequirePair(Saved, actual);
    }

    internal NativeFreshLevelDocumentStore.ReadLease AcquireFinalReadGuard(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        RequireCandidate(Candidate);
        // The future caller must acquire only after long work, hold across its
        // actual final operation, and Complete(primary) with terminal accounting.
        // This context neither invokes nor classifies artifact publication.
        return Saved.AcquireFinalReadGuard(token);
    }

    private static void RequirePair(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshWizardResearchCandidatePlan candidate)
    {
        var disc = candidate.DiscAncestor;
        candidate.RequireAncestry(disc); // Includes the actual separated clearance, not a caller boolean/hash.
        var scene = disc.ExecutableAncestor.BodyAncestor.HostAncestor.SceneAncestor;
        var document = scene.AuthoringDocumentAncestor ??
            throw new InvalidDataException("Saved Wizard binding requires an actual authored aggregate; legacy P1 cannot supply one.");
        saved.RequireDocument(document);
        scene.RequireAncestry(scene.CompilationAncestor, scene.FamilyAncestor, scene.ResourceAncestor);
        if (!ReferenceEquals(saved.Document.Base, scene.CompilationAncestor.Intent) ||
            !candidate.CopyCanonicalDocument().AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(saved.Document.Base)) ||
            !NativeFreshLevelDocumentCodec.SerializeCanonical(document).AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Actual saved aggregate or compiled Base canonical ownership changed.");
        // Canonical bytes only cross-check actual references. Raw saved version
        // checks remain the existing Store's job; source freshness the issuer's.
    }
}
