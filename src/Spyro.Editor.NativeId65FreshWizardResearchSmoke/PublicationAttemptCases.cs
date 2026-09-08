using System.Reflection;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Exporting;

// Real tiny text-artifact transactions plus neutral failure accounting. These
// do not issue native plans, reproduce a post-rename Dispose fault, or exercise
// full-image engine cleanup. That is a separate reviewed physical test gate.
internal static class PublicationAttemptCases
{
    internal static async Task Run()
    {
        int checks = 0;
        string temporary = Directory.CreateTempSubdirectory("spyro-fresh-publication-attempt-").FullName;
        bool passed = false;
        try
        {
            Type errorType = typeof(NativeId65FreshResearchPublicationUnconfirmedException);
            Check(errorType.IsSealed && errorType.GetConstructors().Length == 0 && typeof(IOException).IsAssignableFrom(errorType),
                "Unconfirmed outcome is internally issued, distinct and sealed");
            Check(errorType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .All(property => property.SetMethod == null), "Unconfirmed inspection paths cannot be edited");
            Check(errorType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name).Order().SequenceEqual(new[] {
                    "CuePath", "ImagePath", "OutputDirectory", "PlanSha256", "PublicationLockPath", "ReceiptPath",
                    "RetainedOutputLeasePath", "RetainedProjectLeasePath", "RetainedRecoveryDirectory", "RetainedStageDirectory" }.Order()),
                "Unconfirmed exposes no fabricated success, verification or commit flag");

            var (preRoot, preArtifacts) = Fixture("precancel");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            var pre = new NativeFreshResearchPublicationAttempt();
            Throws<OperationCanceledException>(() => pre.Invoke(preRoot, preArtifacts, canceled.Token));
            Check(pre.State == NativeFreshResearchPublicationState.NotInvoked && pre.Failure == null &&
                preArtifacts.All(artifact => File.Exists(artifact.StagedPath) && !File.Exists(artifact.DestinationPath)) &&
                !File.Exists(Path.Combine(preRoot, ".export-publication.lock")), "pre-cancel is before the actual call and writes nothing");
            _ = pre.Invoke(preRoot, preArtifacts, default);
            Check(pre.State == NativeFreshResearchPublicationState.Committed,
                "pre-cancel did not consume an actual attempt; a later uncanceled invocation may make the one real call");

            var (successRoot, successArtifacts) = Fixture("returned");
            var returned = new NativeFreshResearchPublicationAttempt();
            var actualResult = returned.Invoke(successRoot, successArtifacts, default);
            Check(returned.State == NativeFreshResearchPublicationState.Committed && actualResult.RetainedRecoveryDirectory == "" &&
                returned.Failure == null, "actual tiny PublishNew return is acknowledged");
            Check(successArtifacts.All(artifact => File.ReadAllText(artifact.DestinationPath) == "tiny " + Path.GetFileName(artifact.DestinationPath) &&
                !File.Exists(artifact.StagedPath)), "actual successful renames consumed the tiny stage files");
            string[] successfulFiles = Files(successRoot);
            Throws<InvalidOperationException>(() => returned.Invoke(successRoot, successArtifacts, default));
            Check(returned.State == NativeFreshResearchPublicationState.Committed && Files(successRoot).SequenceEqual(successfulFiles),
                "one attempt cannot silently retry or change its acknowledged state");

            foreach (string fixture in new[] { "foreign-lock", "equal-destination", "different-destination", "missing-stage" })
            {
                var (root, artifacts) = Fixture(fixture);
                string lockPath = Path.Combine(root, ".export-publication.lock");
                if (fixture == "foreign-lock") File.WriteAllText(lockPath, "foreign marker");
                else if (fixture == "missing-stage") File.Delete(artifacts[0].StagedPath!);
                else File.WriteAllText(artifacts[0].DestinationPath, fixture == "equal-destination" ? "tiny document-0" : "foreign different bytes");
                var attempt = new NativeFreshResearchPublicationAttempt();
                var primary = Throws<IOException>(() => attempt.Invoke(root, artifacts, default));
                attempt.RecordFailure(primary);
                Check(attempt.State == NativeFreshResearchPublicationState.Unconfirmed && ReferenceEquals(attempt.Failure, primary),
                    "actual invoked generic refusal is unconfirmed with its exact original failure");
                Check(artifacts.Skip(fixture == "missing-stage" ? 1 : 0).All(artifact => File.Exists(artifact.StagedPath)),
                    "wrapper never deletes existing stages after an unreturned invocation");
                if (fixture == "foreign-lock") Check(File.ReadAllText(lockPath) == "foreign marker", "foreign transaction marker retained");
                if (fixture.EndsWith("destination", StringComparison.Ordinal)) Check(File.Exists(artifacts[0].DestinationPath),
                    "equal or different occupied bytes never upgrade uncertainty to commit");
                string[] beforeRetry = Files(root);
                Throws<InvalidOperationException>(() => attempt.Invoke(root, artifacts, default));
                Check(Files(root).SequenceEqual(beforeRetry), "unconfirmed call cannot auto-retry");
            }

            // Obtain a REAL typed rollback from the existing unchanged private
            // transaction fault fixture. It is later a callback error only.
            var (rollbackRoot, rollbackArtifacts) = Fixture("genuine-rollback");
            Action<string> conflict = phase =>
            {
                if (phase != "after-publish:0") return;
                File.WriteAllText(rollbackArtifacts[0].StagedPath!, "competing stage owner");
                throw new IOException("actual rollback fixture");
            };
            var actualRollback = Throws<ExportArtifactRollbackException>(() =>
            {
                try
                {
                    typeof(ExportArtifactTransaction).GetMethod("PublishCoreWithPolicy", BindingFlags.NonPublic | BindingFlags.Static)!
                        .Invoke(null, [rollbackRoot, rollbackArtifacts, CancellationToken.None, conflict, true]);
                }
                catch (TargetInvocationException error) when (error.InnerException != null)
                { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
            });
            Check(Directory.Exists(actualRollback.RetainedRecoveryDirectory), "real typed rollback retains transaction evidence");
            returned.Capture(() => throw actualRollback);
            Check(returned.State == NativeFreshResearchPublicationState.Committed && ReferenceEquals(returned.Failure, actualRollback),
                "rollback-named callback after actual return cannot reclassify committed");

            var ledger = new NativeFreshResearchPublicationAttempt();
            var primaryFailure = new IOException("primary");
            var sourceFailure = new InvalidDataException("source observation");
            var closeFailure = new OperationCanceledException("synthetic closure failure, not transaction cancellation");
            ledger.RecordFailure(primaryFailure);
            ledger.Capture(() => throw sourceFailure);
            await ledger.CaptureAsync(() => Task.FromException(closeFailure));
            Check(ledger.Failure is AggregateException aggregate && aggregate.InnerExceptions.Count == 3 &&
                ReferenceEquals(aggregate.InnerExceptions[0], primaryFailure) && ReferenceEquals(aggregate.InnerExceptions[1], sourceFailure) &&
                ReferenceEquals(aggregate.InnerExceptions[2], closeFailure), "all ordered original failures survive independent accounting");
            Check(ledger.State == NativeFreshResearchPublicationState.NotInvoked, "neutral failure recording cannot certify a transaction state");
            int finalCalls = 0;
            ledger.Capture(() => finalCalls++);
            await ledger.CaptureAsync(() => { finalCalls++; return Task.CompletedTask; });
            Check(finalCalls == 2, "later closure work still runs after previous failures");
            Throws<ArgumentNullException>(() => ledger.RecordFailure(null!));
            Throws<ArgumentNullException>(() => ledger.Capture(null!));
            await ThrowsAsync<ArgumentNullException>(() => ledger.CaptureAsync(null!));
            Check(ledger.Failure is AggregateException unchanged && unchanged.InnerExceptions.Count == 3,
                "null helper delegates refuse before changing recorded failures");
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(temporary, recursive: true);
            else Console.Error.WriteLine("Retained failed publication-attempt fixture: " + temporary);
        }
        Console.WriteLine($"FRESH_RESEARCH_PUBLICATION_ATTEMPT_OK checks={checks} nativeOutputs=0 fullEngineRetentionExercised=false disposeAfterRenameExercised=false");

        (string Root, ExportArtifactReplacement[] Artifacts) Fixture(string name)
        {
            string root = Path.Combine(temporary, name), stage = Path.Combine(root, "stage");
            Directory.CreateDirectory(stage);
            var artifacts = Enumerable.Range(0, 2).Select(index => new ExportArtifactReplacement(
                Path.Combine(root, "document-" + index), Path.Combine(stage, "document-" + index))).ToArray();
            foreach (var artifact in artifacts) File.WriteAllText(artifact.StagedPath!, "tiny " + Path.GetFileName(artifact.DestinationPath));
            return (root, artifacts);
        }
        void Check(bool value, string why) { if (!value) throw new InvalidDataException(why); checks++; }
        T Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T error) { checks++; return error; }
            throw new InvalidDataException("Expected " + typeof(T).Name);
        }
        async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try { await action(); } catch (T) { checks++; return; }
            throw new InvalidDataException("Expected " + typeof(T).Name);
        }
    }

    private static string[] Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))).Order().ToArray();
}
