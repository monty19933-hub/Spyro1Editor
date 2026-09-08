// Component tests of a genuine tiny transaction and Store owner,
// not a native image and not the source-authorized Engine invocation branch.
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class ActualAttemptCases
{
    internal static async Task Free()
    {
        foreach (string mode in new[] { "pre-cancel", "blocked-call", "clean-commit", "commit-release-fails", "blocked-release-fails" })
        {
            var (project, saved) = Save("real-attempt-" + mode);
            string root = Path.Combine(project.RootPath, "output"), stage = Path.Combine(root, "stage");
            Directory.CreateDirectory(stage);
            string[] names = ["small.bin", "small.receipt.json", "small.cue"];
            foreach (string name in names) File.WriteAllText(Path.Combine(stage, name), "tiny non-native " + name);
            var replacements = names.Select(name => new ExportArtifactReplacement(
                Path.Combine(root, name), Path.Combine(stage, name))).ToArray();
            using var canceled = new CancellationTokenSource();
            var attempt = new NativeFreshResearchPublicationAttempt();
            ExportArtifactPublicationResult? result = null; Exception? primary = null;
            Exception? setupFailure = null, outcome = null;
            var guard = saved.AcquireFinalReadGuard();
            try
            {
                await Throws<IOException>(() => { NativeFreshLevelDocumentStore.Save(saved.Snapshot, saved.Document); return Task.CompletedTask; });
                if (mode == "pre-cancel") canceled.Cancel();
                if (mode.StartsWith("blocked-")) File.WriteAllText(Path.Combine(root, ".export-publication.lock"), "test-owned blocker");
                try { result = attempt.Invoke(root, replacements, canceled.Token); }
                catch (Exception error) { primary = error; }
                if (mode.EndsWith("release-fails"))
                { File.Move(guard.LeasePath, guard.LeasePath + ".held"); File.WriteAllText(guard.LeasePath, "subsequent test owner"); }
            }
            catch (Exception error) { setupFailure = error; }
            finally
            {
                Exception? operationFailure = setupFailure == null ? primary : primary == null ? setupFailure :
                    new IOException("Actual transaction plus unexpected test setup failure.", new AggregateException(primary, setupFailure));
                outcome = NativeId65FreshResearchPublicationEngine.CompleteSavedLevelGuard(guard, operationFailure);
            }
            if (setupFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome!).Throw();
            if (outcome != null) attempt.RecordFailure(outcome);
            int finalFilesObserved = 0;
            // Component-level actual returned transaction + failed real Complete:
            // still inspect every tiny output. This is NOT an Engine/native file
            // test; the explicit native guard-failure/output gate remains pending.
            if (result != null)
            {
                foreach (string name in names)
                {
                    finalFilesObserved++;
                    try { Check(File.ReadAllText(Path.Combine(root, name)) == "tiny non-native " + name, "Actual tiny final bytes."); }
                    catch (Exception error) { attempt.RecordFailure(error); }
                }
                Check(finalFilesObserved == 3, "All tiny final outputs observed after actual returned call, including failed Complete.");
            }
            if (mode == "pre-cancel")
                Check(attempt.State == NativeFreshResearchPublicationState.NotInvoked && result == null &&
                    ReferenceEquals(attempt.Failure, primary), "Pre-call canceled state is actual NotInvoked; clean guard preserves exact OCE.");
            else if (mode.StartsWith("blocked-"))
                Check(attempt.State == NativeFreshResearchPublicationState.Unconfirmed && result == null &&
                    File.ReadAllText(Path.Combine(root, ".export-publication.lock")) == "test-owned blocker",
                    "Actual PublishNew call threw; later guard facts cannot fabricate acknowledgement.");
            else
                Check(attempt.State == NativeFreshResearchPublicationState.Committed && result != null &&
                    names.All(n => File.ReadAllText(Path.Combine(root, n)) == "tiny non-native " + n),
                    "Genuine tiny PublishNew returned; release failure does not erase actual committed state.");
            if (primary != null)
                Check(Leaves(attempt.Failure!).Count(e => ReferenceEquals(e, primary)) == 1,
                    "Actual transaction exception occurs once across Complete and attempt accounting.");
            if (mode.EndsWith("release-fails"))
                Check(guard.TerminalFailure != null && Leaves(attempt.Failure!).Count == (mode == "commit-release-fails" ? 1 : 2) &&
                    Leaves(attempt.Failure!).Count(e => ReferenceEquals(e, guard.TerminalFailure)) == 1,
                    "Exact complete error list: terminal once, plus actual call error only if unconfirmed; no ignored output assertion.");
            if (mode == "clean-commit") Check(attempt.Failure == null && guard.TerminalFailure == null &&
                guard.RetainedEvidencePath == "", "Tiny component success and clean owner state, not native publication approval.");
        }
    }
}
