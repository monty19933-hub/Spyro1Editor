using System.Text;
using Spyro.Editor.Core.Exporting;
using static TexturedPublicationCases;

// Only explicit reviewed modes call this class. Every outcome comes from the
// real transaction; no fake Publication, injected writer, repair or retry.
internal static class TexturedFaultCases
{
    internal static async Task<object> Run(NativeId65FreshTexturedResearchCandidatePlan plan,
        string root, string mode, Action markReturned)
    {
        if (mode == "--unconfirmed-textured-reviewed")
            return await Observe(() => Unconfirmed(plan, root), plan);
        Check(mode is "--stage-textured-reviewed" or "--postcommit-textured-reviewed", "Only exact fault modes may create a workspace.");
        RequireLocation(root, plan.SourceImagePath, plan.SourceCuePath, false);
        Directory.CreateDirectory(root); RequireNames(root, []);
        var reports = new List<object>(); string[] children;
        if (mode == "--stage-textured-reviewed")
        {
            children = ["after-first-sector", "after-image-write", "before-publication"];
            var faults = new[] { NativeId65FreshTexturedResearchFault.AfterFirstSector,
                NativeId65FreshTexturedResearchFault.AfterImageWrite, NativeId65FreshTexturedResearchFault.BeforePublication };
            for (int i = 0; i < faults.Length; i++)
            {
                string child = Path.Combine(root, children[i]); var fault = faults[i];
                reports.Add(await Observe(() => Precommit(plan, child, fault), plan));
            }
        }
        else if (mode == "--postcommit-textured-reviewed")
        {
            children = ["late-cancel-success", "reused-image-stage-owner", "stage-replaced-by-file"];
            for (int i = 0; i < children.Length; i++)
            {
                string child = Path.Combine(root, children[i]); int kind = i;
                reports.Add(await Observe(() => Postcommit(plan, child, kind, markReturned), plan));
            }
        }
        else throw new InvalidDataException("No other mode may run a textured fault workspace.");
        RequireNames(root, children);
        return new { cases = reports, retainedAllOutputs = true, automaticRetry = false, nativeRuntime = false };
    }

    private static async Task<object> Observe(Func<Task<object>> operation, NativeId65FreshTexturedResearchCandidatePlan plan)
    {
        List<Exception> errors = []; object? report = null;
        try { report = await operation(); } catch (Exception error) { errors.Add(error); }
        try { await plan.RequireFreshAsync(CancellationToken.None); } catch (Exception error) { errors.Add(error); }
        NativeId65FreshTexturedResearchCandidatePlan.ThrowFailures(errors);
        return report!;
    }

    private static async Task<object> Precommit(NativeId65FreshTexturedResearchCandidatePlan plan,
        string output, NativeId65FreshTexturedResearchFault fault)
    {
        RequireLocation(output, plan.SourceImagePath, plan.SourceCuePath, false);
        int reached = 0;
        IOException failure = await Refuse<IOException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(plan, output, fault, (point, image) =>
            {
                if (point != fault) return;
                reached++;
                Check(Path.GetFileName(image) == Names[0] && Path.GetDirectoryName(Path.GetDirectoryName(image)) == output,
                    "Actual forced point supplies its own stage image under this child.");
            }));
        Check(reached == 1 && failure.Message.Contains("Deliberate Fresh textured LAB", StringComparison.Ordinal) &&
            !Errors(failure).Any(e => e is NativeId65FreshResearchPublicationUnconfirmedException or NativeId65FreshTexturedResearchCommittedException),
            "The actual forced precommit point was reached exactly once, with no commit acknowledgement.");
        RequireNames(output, []); // Actual engine cleanup, never fixture deletion.
        return new { output, point = fault.ToString(), forcedPrecommit = true, transactionSuccess = false, emptyOutput = true };
    }

    private static async Task<object> Unconfirmed(NativeId65FreshTexturedResearchCandidatePlan plan, string output)
    {
        RequireLocation(output, plan.SourceImagePath, plan.SourceCuePath, false);
        var binding = NativeId65FreshResearchPublicationBinding.FromTextured(plan);
        string stage = "", lease = Path.Combine(output, binding.LeaseName), transactionLock = Path.Combine(output, ".export-publication.lock");
        byte[] sentinel = Encoding.UTF8.GetBytes("new foreign textured stage owner: retain\n");
        byte[] foreignLock = Encoding.UTF8.GetBytes("new foreign textured transaction lock: retain\n");
        const string sentinelName = "unconfirmed-stage-owner.txt";
        int calls = 0;
        NativeId65FreshResearchPublicationUnconfirmedException? observed = null;
        try
        {
            try
            {
                await NativeId65FreshTexturedResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(plan, output, path =>
                {
                    calls++; Check(calls == 1 && path == transactionLock, "One exact pre-invocation transaction-lock boundary.");
                    stage = FindStage(output, binding.StagePrefix);
                    RequireNames(output, [Path.GetFileName(stage), binding.LeaseName]);
                    RequireNames(stage, Names); _ = ReadArtifacts(plan, stage);
                    Check(new FileInfo(lease).Length == 32, "The actual output lease is held at the transaction boundary.");
                    WriteTinyNew(output, Path.Combine(stage, sentinelName), sentinel);
                    WriteTinyNew(output, path, foreignLock);
                });
                throw new InvalidDataException("Blocked actual transaction unexpectedly returned success.");
            }
            catch (NativeId65FreshResearchPublicationUnconfirmedException error) { observed = error; }
            var failure = observed!;
            Check(calls == 1 && failure.InnerException is IOException and not ExportArtifactRollbackException &&
                FailureOutcome(failure, false, true) == "unconfirmed" &&
                !Errors(failure).Any(e => e is NativeId65FreshTexturedResearchCommittedException),
                "Only the actual unacknowledged call is classified Unconfirmed.");
            Check(failure.OutputDirectory == output && failure.ImagePath == Path.Combine(output, Names[0]) &&
                failure.CuePath == Path.Combine(output, Names[2]) && failure.ReceiptPath == Path.Combine(output, Names[1]) &&
                failure.PlanSha256 == plan.PlanSha256 && failure.RetainedStageDirectory == stage &&
                failure.RetainedOutputLeasePath == lease && failure.PublicationLockPath == transactionLock &&
                failure.RetainedRecoveryDirectory == output && failure.RetainedProjectLeasePath == "",
                "Actual new role supplies all uncertainty paths and no fictional project owner.");
            RequireNames(output, [Path.GetFileName(stage), binding.LeaseName, ".export-publication.lock"]);
            foreach (string name in Names) RequireAbsent(Path.Combine(output, name));
            RequireNames(stage, [..Names, sentinelName]);
            string[] hashes = ReadArtifacts(plan, stage); byte[] marker = ReadMarker(lease);
            RequireBytes(transactionLock, foreignLock); RequireBytes(Path.Combine(stage, sentinelName), sentinel);
            await Refuse<IOException>(() => NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(plan, output));
            Check(ReadArtifacts(plan, stage).SequenceEqual(hashes), "Rejected clean-set Verify does not alter the retained stage.");
            RequireBytes(lease, marker); RequireBytes(transactionLock, foreignLock); RequireBytes(Path.Combine(stage, sentinelName), sentinel);
            RequireNames(stage, [..Names, sentinelName]);
            RequireNames(output, [Path.GetFileName(stage), binding.LeaseName, ".export-publication.lock"]);
            return new { output, stage, lease, transactionLock, outcome = "Unconfirmed", actualTransactionInvoked = true,
                finalNamesObservedAbsent = true, artifactSha256 = hashes, outputLeaseSha256 = Hash(marker), runtime = false };
        }
        catch (Exception error) when (observed != null && !ReferenceEquals(error, observed))
        {
            // Later audit failure must not erase the actual transaction outcome.
            throw new IOException("Unconfirmed textured exercise also failed its retained-evidence audit.",
                new AggregateException(observed, error));
        }
    }

    private static async Task<object> Postcommit(NativeId65FreshTexturedResearchCandidatePlan plan,
        string output, int kind, Action markReturned)
    {
        Check(kind is >= 0 and <= 2, "Only three exact postcommit cases.");
        RequireLocation(output, plan.SourceImagePath, plan.SourceCuePath, false);
        var binding = NativeId65FreshResearchPublicationBinding.FromTextured(plan);
        string stage = "", image = Path.Combine(output, Names[0]), lease = Path.Combine(output, binding.LeaseName);
        byte[] foreign = Encoding.UTF8.GetBytes("new postcommit textured owner: " + kind + "\n");
        int committed = 0, cleanup = 0; bool applied = false;
        NativeId65FreshDiscResearchPublication? completed = null;
        NativeId65FreshTexturedResearchCommittedException? observed = null;
        using var canceled = new CancellationTokenSource();
        try
        {
            try
            {
                completed = await NativeId65FreshTexturedResearchCandidatePublisher.PublishPostcommitForSmokeAsync(plan, output,
                    (point, path) =>
                    {
                        if (point == NativeId65FreshTexturedPostcommitPoint.AfterCommit)
                        {
                            committed++; Check(committed == 1 && path == image, "One hook after actual PublishNew returned.");
                            stage = FindStage(output, binding.StagePrefix); RequireNames(stage, []);
                            RequireNames(output, [..Names, binding.LeaseName, Path.GetFileName(stage)]);
                            Check(new FileInfo(image).Length == ImageLength && new FileInfo(lease).Length == 32,
                                "Real complete final image and still-held output lease precede the hook.");
                            if (kind == 0) { canceled.Cancel(); applied = true; }
                        }
                        else
                        {
                            Check(point == NativeId65FreshTexturedPostcommitPoint.BeforeStageCleanup, "Only two actual new-role points.");
                            cleanup++; Check(cleanup == 1 && path == stage, "One actual empty stage cleanup boundary.");
                            RequireNames(stage, []);
                            if (kind == 1) { WriteTinyNew(output, Path.Combine(stage, Names[0]), foreign); applied = true; }
                            if (kind == 2)
                            {
                                Directory.Delete(stage, recursive: false); // This exact verified EMPTY stage only.
                                WriteTinyNew(output, stage, foreign); applied = true;
                            }
                        }
                    }, canceled.Token);
                markReturned(); // Only a genuine returned publishing result reaches this CLI bookkeeping.
            }
            catch (NativeId65FreshTexturedResearchCommittedException error) { observed = error; }
            Check(committed == 1 && cleanup == 1 && applied, "Every requested effect followed a real commit and actual cleanup boundary.");
            if (kind == 0)
            {
                Check(observed == null && completed != null && canceled.IsCancellationRequested, "Late cancellation does not bypass None finalization.");
                RequireResult(plan, output, completed!); string[] hashes = ReadCompleteOwners(plan, output);
                var verified = await NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(plan, output);
                Check(verified == completed && ReadCompleteOwners(plan, output).SequenceEqual(hashes),
                    "Actual late-cancel clean result independently reopens without change.");
                return new { output, outcome = "clean-late-cancel-success", artifactSha256 = hashes, actualCommitted = true };
            }
            Check(completed == null && observed != null, "Actual cleanup failure yields only the typed known-commit error.");
            var failure = observed!;
            Check(FailureOutcome(failure, false, true) == "committed-with-failure" &&
                failure.OutputDirectory == output && failure.ImagePath == image && failure.CuePath == Path.Combine(output, Names[2]) &&
                failure.ReceiptPath == Path.Combine(output, Names[1]) && failure.PlanSha256 == plan.PlanSha256 &&
                failure.RetainedStageDirectory == stage && failure.RetainedOutputLeasePath == lease &&
                failure.RetainedRecoveryDirectory == "" && failure.InnerException is IOException &&
                failure.InnerException.Message.Contains("preserve it.", StringComparison.Ordinal),
                "Actual typed failure retains exact stage/file, marker and original cleanup error.");
            RequireNames(output, [..Names, binding.LeaseName, Path.GetFileName(stage)]);
            byte[] marker = ReadMarker(lease); string[] before = ReadArtifacts(plan, output);
            RequireForeign();
            await Refuse<IOException>(() => NativeId65FreshTexturedResearchCandidatePublisher.VerifyAsync(plan, output));
            Check(ReadArtifacts(plan, output).SequenceEqual(before), "Failed clean-set Verify never repairs/deletes retained artifacts.");
            RequireBytes(lease, marker); RequireForeign();
            RequireNames(output, [..Names, binding.LeaseName, Path.GetFileName(stage)]);
            return new { output, stage, lease, outcome = "known-committed-cleanup-failure",
                artifactSha256 = before, outputLeaseSha256 = Hash(marker), foreignOwnerSha256 = Hash(foreign), actualCommitted = true };

            void RequireForeign()
            {
                if (kind == 1) { RequireNames(stage, [Names[0]]); RequireBytes(Path.Combine(stage, Names[0]), foreign); }
                else RequireBytes(stage, foreign);
            }
        }
        catch (Exception error) when (observed != null && !ReferenceEquals(error, observed))
        {
            throw new IOException("Known-commit textured exercise also failed its retained-evidence audit.",
                new AggregateException(observed, error));
        }
    }

    private static string FindStage(string root, string prefix)
    {
        string[] stages = Directory.GetDirectories(root);
        Check(stages.Length == 1 && Path.GetFileName(stages[0]).StartsWith(prefix, StringComparison.Ordinal), "One real engine-created stage.");
        RequireUnlinked(stages[0]); return stages[0];
    }
    private static async Task<T> Refuse<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T error) { return error; }
        throw new InvalidDataException("Expected actual " + typeof(T).Name + "; outputs retained.");
    }
}
