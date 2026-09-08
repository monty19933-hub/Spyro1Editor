// Actual tiny saved-owner tests; no original source or native candidate.
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class LeaseOutcomeCases
{
    internal static async Task Free()
    {
        foreach (bool foreign in new[] { false, true })
        foreach (bool withPrimary in new[] { false, true })
        {
            var (project, saved) = Save("guard-outcome");
            Exception? primary = withPrimary ? new IOException("synthetic primary; real guard cleanup only") : null;
            Exception? setupFailure = null, outcome = null;
            var guard = saved.AcquireFinalReadGuard();
            try
            {
                NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, CancellationToken.None);
                await Throws<IOException>(() => { NativeFreshLevelDocumentStore.Save(saved.Snapshot, saved.Document); return Task.CompletedTask; });
                if (foreign)
                {
                    File.Move(guard.LeasePath, guard.LeasePath + ".held");
                    File.WriteAllText(guard.LeasePath, "test-owned-later-marker");
                }
            }
            catch (Exception setupError) { setupFailure = setupError; }
            finally
            {
                outcome = NativeId65FreshResearchPublicationEngine.CompleteSavedLevelGuard(guard, setupFailure ?? primary);
            }
            if (setupFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome!).Throw();
            // This tiny concrete-owner seam returns the actual Complete outcome.
            // It cannot set transaction state, acquire/issue source or publish.
            var attempt = new NativeFreshResearchPublicationAttempt();
            if (outcome != null) attempt.RecordFailure(outcome);
            Check(attempt.State == NativeFreshResearchPublicationState.NotInvoked, "No simulated commit/Unconfirmed authority.");
            Check(primary == null || Leaves(attempt.Failure!).Count(e => ReferenceEquals(e, primary)) == 1,
                "Record completion once, not primary plus primary-containing wrapper.");
            if (!foreign)
                Check(ReferenceEquals(outcome, primary) && !File.Exists(guard.LeasePath) &&
                    guard.TerminalFailure == null && guard.RetainedEvidencePath == "",
                    "Clean real release returns exact primary/null and both unresolved-state properties are clear.");
            else
                Check(outcome is IOException && ReferenceEquals(Leaves(outcome)[^1], guard.TerminalFailure) &&
                    File.ReadAllText(guard.LeasePath) == "test-owned-later-marker", "Actual unknown marker retained, terminal object once.");
            var repeat = NativeId65FreshResearchPublicationEngine.CompleteSavedLevelGuard(guard, primary);
            Check(ReferenceEquals(repeat, outcome), "Repeated Complete returns cached outcome; no second ownership release.");
        }
        // Acquisition never returns an owner after its own internal Complete.
        var (p, s) = Save("acquire-failed-owned");
        var primaryOwned = new IOException("actual acquisition checkpoint, not publication failure");
        var error = await Throws<IOException>(() =>
        {
            NativeFreshLevelDocumentStore.ReadLease? unexpectedOwner = null;
            try
            {
                unexpectedOwner = NativeFreshLevelDocumentStore.AcquireReadLeaseForSmoke(s.Snapshot, phase =>
                { if (phase == "owned") throw primaryOwned; });
            }
            finally { unexpectedOwner?.Complete(); } // Even a regression returning an unexpected owner must release it.
            return Task.CompletedTask;
        });
        Check(Leaves(error).Count(e => ReferenceEquals(e, primaryOwned)) == 1 &&
            new FileInfo(Path.Combine(p.RootPath, LockName)).Length == 0,
            "Actual acquired-empty marker retained; caller must not re-Complete an unreturned guard.");
    }
}
