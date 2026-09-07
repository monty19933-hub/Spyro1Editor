using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class PostcommitCases
{
    internal static async Task RunApiTests()
    {
        int checks = 0;
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        foreach (var token in new[] { CancellationToken.None, canceled.Token })
        {
            await Refuse(() => NativeId65FreshDocumentResearchPublisher.PublishPostcommitForSmokeAsync(null!, "\0", null!, token), "context");
            await Refuse(() => NativeId65FreshResearchPublicationEngine.PublishDocumentPostcommitForSmokeAsync(null!, "\0", null!, token), "plan");
        }
        Check(Enum.GetValues<NativeId65FreshDocumentPostcommitPoint>().Select(v => (int)v).SequenceEqual(new[] { 0, 1 }),
            "separate postcommit points, no new value in precommit fault enums");
        Check(Enum.GetValues<NativeId65FreshDocumentResearchFault>().Select(v => (int)v).SequenceEqual(new[] { 0, 1, 2, 3, 4 }) &&
            Enum.GetValues<NativeId65FreshResearchFault>().Select(v => (int)v).SequenceEqual(new[] { 0, 1, 2, 3 }), "old forced fault domains unchanged");
        Console.WriteLine($"FRESH_DOCUMENT_POSTCOMMIT_API_FREE_OK checks={checks} sourceOrOutputIo=0");

        async Task Refuse(Func<Task<NativeId65FreshDiscResearchPublication>> invoke, string parameter)
        {
            Task<NativeId65FreshDiscResearchPublication> task;
            try { task = invoke(); }
            catch (Exception error) { throw new InvalidDataException("postcommit entry must fault Task, not invocation", error); }
            Check(task.IsCompleted && task.IsFaulted && !task.IsCanceled, "null owner faults before token/path/IO");
            try { await task; }
            catch (ArgumentNullException error) when (error.ParamName == parameter) { checks++; return; }
            throw new InvalidDataException("Wrong postcommit entry refusal");
        }
        void Check(bool condition, string label) { ReviewedPublicationCases.Check(condition, label); checks++; }
    }

    internal static async Task RunSourceGuards(string original, string cue)
    {
        string before = ReviewedPublicationCases.HashFile(original, NativeId65FreshDiscSource.ImageByteLength);
        string cueBefore = ReviewedPublicationCases.HashFile(cue);
        string root = Directory.CreateTempSubdirectory("fresh-document-postcommit-guards-").FullName;
        int checks = 0;
        try
        {
            var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(original, SourceCases.Intent);
            var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(original, cue, compiled);
            var context = SourceCases.SaveContext(Path.Combine(root, "project"), plan);
            var fixedPlan = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, compiled);
            string output = Path.Combine(root, "must-not-exist");
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            foreach (var token in new[] { CancellationToken.None, cancel.Token })
            {
                await Refuse(() => NativeId65FreshDocumentResearchPublisher.PublishPostcommitForSmokeAsync(context, output, null!, token), false);
                await Refuse(() => NativeId65FreshResearchPublicationEngine.PublishDocumentPostcommitForSmokeAsync(
                    NativeId65FreshResearchPublicationBinding.FromSaved(context), output, null!, token), false);
                await Refuse(() => NativeId65FreshResearchPublicationEngine.PublishDocumentPostcommitForSmokeAsync(
                    NativeId65FreshResearchPublicationBinding.FromEdited(fixedPlan), output, (_, _) => throw new InvalidDataException("must not reach hook"), token), true);
            }
            Check(!Directory.Exists(output) && !File.Exists(output), "invalid hooks/bindings cannot create output");
            Check(Directory.GetFileSystemEntries(context.Snapshot.RootPath).Length == 2, "invalid positive entry preserves saved project owners");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Check(ReviewedPublicationCases.HashFile(original, NativeId65FreshDiscSource.ImageByteLength) == before &&
                ReviewedPublicationCases.HashFile(cue) == cueBefore, "source guard tests leave complete original inputs unchanged");
        }
        Console.WriteLine($"FRESH_DOCUMENT_POSTCOMMIT_SOURCE_GUARDS_OK checks={checks} outputWritten=false");

        async Task Refuse(Func<Task<NativeId65FreshDiscResearchPublication>> invoke, bool fixedBinding)
        {
            Task<NativeId65FreshDiscResearchPublication> task;
            try { task = invoke(); }
            catch (Exception error) { throw new InvalidDataException("positive entry validation must fault its Task", error); }
            Check(task.IsCompleted && task.IsFaulted, "hook/closed-binding validation precedes token/output IO");
            try { await task; }
            catch (ArgumentNullException error) when (!fixedBinding && error.ParamName == "checkpoint") { checks++; return; }
            catch (InvalidDataException error) when (fixedBinding && error.Message.Contains("actual saved-document binding", StringComparison.Ordinal)) { checks++; return; }
            throw new InvalidDataException("Wrong actual-source positive entry refusal");
        }
        void Check(bool condition, string label) { ReviewedPublicationCases.Check(condition, label); checks++; }
    }

    internal static async Task RunAsync(NativeId65FreshSavedDocumentExportContext context, string workspace)
    {
        ReviewedPublicationCases.RequireOutput(workspace, absent: true);
        string[] beforeInputs = ReviewedPublicationCases.ReadInputs(context);
        // Parent explicitly approves this absent workspace and existing saved project.
        // Neither test changes the project or deletes committed output/evidence.
        Directory.CreateDirectory(workspace);
        int checks = 0;
        string cancelOutput = Path.Combine(workspace, "output-late-cancel");
        using var cancel = new CancellationTokenSource();
        int committedCalls = 0, cleanupCalls = 0;
        var result = await NativeId65FreshDocumentResearchPublisher.PublishPostcommitForSmokeAsync(context, cancelOutput, (point, path) =>
        {
            if (point == NativeId65FreshDocumentPostcommitPoint.AfterCommit)
            {
                committedCalls++;
                Check(path == Path.Combine(cancelOutput, NativeId65FreshDocumentResearchPlan.ImageName), "actual final image path at known commit");
                Check(File.Exists(path) && File.Exists(Path.Combine(cancelOutput, NativeId65FreshDocumentResearchPublisher.CueName)) &&
                    File.Exists(Path.Combine(cancelOutput, NativeId65FreshDocumentResearchPublisher.ReceiptName)), "real transaction placed all three artifacts");
                Check(!File.Exists(Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock")), "saved guard already released after real commit");
                cancel.Cancel();
            }
            else if (point == NativeId65FreshDocumentPostcommitPoint.BeforeStageCleanup) cleanupCalls++;
        }, cancel.Token);
        Check(committedCalls == 1 && cleanupCalls == 1 && cancel.IsCancellationRequested, "late cancellation happened only after actual commit");
        ReviewedPublicationCases.RequireResult(context, result); checks++;
        string[] cancelHashes = ReviewedPublicationCases.ReadArtifacts(context, cancelOutput);
        var verified = await NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, cancelOutput, CancellationToken.None);
        Check(verified == result, "noncancellable known-commit result independently reopens");
        bool normalCancel = false;
        try { await NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, cancelOutput, cancel.Token); }
        catch (OperationCanceledException) { normalCancel = true; }
        Check(normalCancel, "ordinary Verify retains caller cancellation semantics");
        Check(ReviewedPublicationCases.ReadArtifacts(context, cancelOutput).SequenceEqual(cancelHashes), "late cancellation and Verify preserve all artifacts");

        string failureOutput = Path.Combine(workspace, "output-cleanup-failure"), stage = "";
        int actualCommit = 0, actualCleanup = 0;
        NativeId65FreshDocumentResearchCommittedException? committedFailure = null;
        try
        {
            await NativeId65FreshDocumentResearchPublisher.PublishPostcommitForSmokeAsync(context, failureOutput, (point, path) =>
            {
                if (point == NativeId65FreshDocumentPostcommitPoint.AfterCommit) { actualCommit++; return; }
                if (point != NativeId65FreshDocumentPostcommitPoint.BeforeStageCleanup) return;
                actualCleanup++; stage = path;
                Check(Directory.Exists(stage) && !Directory.EnumerateFileSystemEntries(stage).Any(), "real committed stage is now empty before cleanup");
                Check(File.Exists(Path.Combine(failureOutput, NativeId65FreshDocumentResearchPlan.ImageName)), "real final image exists before cleanup");
                using FileStream unknown = new(Path.Combine(stage, "unknown-stage-owner.txt"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                unknown.Write("preserve actual postcommit unknown owner\n"u8); unknown.Flush(true);
            });
        }
        catch (NativeId65FreshDocumentResearchCommittedException error) { committedFailure = error; }
        Check(committedFailure != null && actualCommit == 1 && actualCleanup == 1, "actual unknown owner yields typed committed failure, not simulated result");
        var failure = committedFailure!;
        Check(failure.OutputDirectory == failureOutput && failure.PlanSha256 == context.Plan.PlanSha256 &&
            failure.ImagePath == Path.Combine(failureOutput, NativeId65FreshDocumentResearchPlan.ImageName) &&
            failure.CuePath == Path.Combine(failureOutput, NativeId65FreshDocumentResearchPublisher.CueName) &&
            failure.ReceiptPath == Path.Combine(failureOutput, NativeId65FreshDocumentResearchPublisher.ReceiptName), "typed actual artifact paths and plan identity");
        Check(failure.RetainedStageDirectory == stage && failure.RetainedOutputLeasePath == Path.Combine(failureOutput, ".native-id65-fresh-document-research.writer.lock") &&
            failure.RetainedRecoveryDirectory == "" && failure.RetainedProjectLeasePath == "" && failure.InnerException is IOException,
            "cleanup failure reports distinct actual retained owners, no fictional transaction/project recovery");
        Check(File.ReadAllText(Path.Combine(stage, "unknown-stage-owner.txt")) == "preserve actual postcommit unknown owner\n" &&
            new FileInfo(failure.RetainedOutputLeasePath).Length == 32, "unknown evidence and identified output lease retained");
        Check(!File.Exists(Path.Combine(context.Snapshot.RootPath, ".native-id65-fresh-document.writer.lock")), "project lease not leaked after typed failure");
        var artifactReadback = context.Plan.VerifyImage(failure.ImagePath, CancellationToken.None);
        Check(artifactReadback.OutputSha256 == context.Plan.ExpectedImageSha256 &&
            artifactReadback.ChangedSectors == context.Plan.ExpectedChangedRawSectors, "committed image still independently source-authenticated");
        string[] failureHashes = ReviewedPublicationCases.ReadArtifacts(context, failureOutput, exactSet: false);
        Check(cancelHashes.SequenceEqual(failureHashes), "both real outputs contain identical image/CUE/context-bound receipt");
        bool extraRefused = false;
        try { await NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, failureOutput); }
        catch (IOException) { extraRefused = true; }
        Check(extraRefused, "public clean-set Verify refuses retained ownership evidence without deleting it");
        Check(ReviewedPublicationCases.ReadInputs(context).SequenceEqual(beforeInputs), "both real commits leave source/project raw bytes unchanged");
        Console.WriteLine("FRESH_DOCUMENT_POSTCOMMIT_EXERCISED " + JsonSerializer.Serialize(new
        {
            checks, result, lateCancellation = true, knownCommittedFailure = true, failureOutput,
            failure.RetainedStageDirectory, failure.RetainedOutputLeasePath, failure.RetainedRecoveryDirectory,
            failure.RetainedProjectLeasePath, imageHashes = cancelHashes, originalAndProjectUnchanged = true,
            runtime = false, normalExport = false
        }));

        void Check(bool condition, string label) { ReviewedPublicationCases.Check(condition, label); checks++; }
    }
}
