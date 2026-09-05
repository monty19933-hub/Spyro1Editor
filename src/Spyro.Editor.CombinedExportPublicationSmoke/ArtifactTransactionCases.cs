using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class ArtifactTransactionCases
{
    internal static string Run(string fixtureRoot)
    {
        string[] failurePoints = ["prepared", .. Enumerable.Range(0, 4).Select(index => $"after-backup:{index}"), .. Enumerable.Range(0, 4).Select(index => $"after-publish:{index}")];
        foreach (string failurePoint in failurePoints)
        {
            Fixture fixture = Create(fixtureRoot);
            Exception failure = ExpectFailure(() => Inject(fixture, stage =>
            {
                if (stage == failurePoint)
                    throw new IOException("injected publication failure");
            }));
            Require(failure is IOException, $"Unexpected fault at {failurePoint}: {failure}");
            RequirePrevious(fixture);
            RequireStaged(fixture);
            RequireClean(fixture);
        }

        Fixture noPrevious = Create(fixtureRoot);
        foreach (string path in noPrevious.Previous.Keys)
            File.Delete(path);
        ExpectFailure(() => Inject(noPrevious, stage =>
        {
            if (stage == "after-publish:1") throw new IOException("first publication failed");
        }));
        Require(noPrevious.Previous.Keys.All(path => !File.Exists(path)), "A failed first publication left final artifacts behind.");
        RequireStaged(noPrevious);
        RequireClean(noPrevious);

        Fixture canceled = Create(fixtureRoot);
        using CancellationTokenSource beforeCommit = new();
        Exception cancellation = ExpectFailure(() => Inject(canceled, stage =>
        {
            if (stage == "prepared") beforeCommit.Cancel();
        }, beforeCommit.Token));
        Require(cancellation is OperationCanceledException, "Precommit cancellation did not report cancellation.");
        RequirePrevious(canceled);
        RequireStaged(canceled);
        RequireClean(canceled);

        Fixture preCanceled = Create(fixtureRoot);
        using CancellationTokenSource alreadyCanceled = new();
        alreadyCanceled.Cancel();
        Require(ExpectFailure(() => ExportArtifactTransaction.Publish(preCanceled.Output, preCanceled.Artifacts, alreadyCanceled.Token)) is OperationCanceledException,
            "Already-canceled publication was accepted.");
        RequirePrevious(preCanceled);
        RequireClean(preCanceled);

        Fixture complete = Create(fixtureRoot);
        using CancellationTokenSource duringCommit = new();
        Inject(complete, stage =>
        {
            if (stage == "after-backup:0") duringCommit.Cancel();
        }, duringCommit.Token);
        RequirePublished(complete);
        RequireClean(complete);

        int invalidCases = 0;
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0] with { StagedPath = Path.Combine(fixture.Output, "missing") }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0], fixture.Artifacts[0]], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0], fixture.Artifacts[1] with { StagedPath = fixture.Artifacts[0].StagedPath }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0] with { StagedPath = fixture.Artifacts[0].DestinationPath }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0] with { DestinationPath = Path.Combine(fixtureRoot, "outside.bin") }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0] with { DestinationPath = Path.Combine(fixture.Output, ".export-publication.lock") }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture => [fixture.Artifacts[0] with { DestinationPath = Path.Combine(fixture.Output, ".export-recovery-forged") }], ref invalidCases);
        CheckInvalid(fixtureRoot, fixture =>
        {
            string outside = Path.Combine(fixtureRoot, $"outside-{Guid.NewGuid():N}.bin");
            File.WriteAllText(outside, "outside source");
            return [fixture.Artifacts[0] with { StagedPath = outside }];
        }, ref invalidCases);
        CheckInvalid(fixtureRoot, fixture =>
        {
            string directory = Path.Combine(fixture.Output, "directory.bin");
            Directory.CreateDirectory(directory);
            return [fixture.Artifacts[0] with { DestinationPath = directory }];
        }, ref invalidCases);
        CheckInvalid(fixtureRoot, fixture =>
        {
            string link = Path.Combine(fixture.Output, "linked.bin");
            File.CreateSymbolicLink(link, fixture.Artifacts[0].DestinationPath);
            return [fixture.Artifacts[0] with { DestinationPath = link }];
        }, ref invalidCases);
        CheckInvalid(fixtureRoot, fixture =>
        {
            string link = Path.Combine(fixture.Output, "linked-stage.bin");
            File.CreateSymbolicLink(link, fixture.Artifacts[0].StagedPath!);
            return [fixture.Artifacts[0] with { StagedPath = link }];
        }, ref invalidCases);
        CheckInvalid(fixtureRoot, fixture =>
        {
            string link = Path.Combine(fixture.Output, "linked-directory");
            Directory.CreateSymbolicLink(link, Path.GetDirectoryName(fixture.Artifacts[0].StagedPath!)!);
            return [fixture.Artifacts[0] with { StagedPath = Path.Combine(link, "new.bin") }];
        }, ref invalidCases);

        Fixture recovery = Create(fixtureRoot);
        Exception recoveryFailure = ExpectFailure(() => Inject(recovery, stage =>
        {
            if (stage != "after-publish:0") return;
            // An external writer occupies the old stage path. Rollback must
            // preserve the original backup and report recovery, never delete
            // or overwrite the competing file to force restoration.
            File.WriteAllText(recovery.Artifacts[0].StagedPath!, "competing writer");
            throw new IOException("injected failure with rollback conflict");
        }));
        string recoveryDirectory = Directory.GetDirectories(recovery.Output, ".export-recovery-*").Single();
        Require(recoveryFailure is IOException && recoveryFailure.Message.Contains(recoveryDirectory, StringComparison.Ordinal),
            "Rollback conflict did not report the exact recovery directory.");
        Require(File.ReadAllText(Path.Combine(recoveryDirectory, "previous-000")) == recovery.Previous[recovery.Artifacts[0].DestinationPath],
            "Rollback conflict lost the previous BIN backup.");
        Require(File.Exists(Path.Combine(recoveryDirectory, "recovery.json")) &&
            File.ReadAllText(Path.Combine(recovery.Output, ".export-publication.lock")).Contains(recoveryDirectory, StringComparison.Ordinal),
            "Rollback conflict did not retain its journal and recovery lock.");
        Require(File.ReadAllText(recovery.Artifacts[0].StagedPath!) == "competing writer", "Rollback overwrote a competing staged file.");
        Require(ExpectFailure(() => ExportArtifactTransaction.Publish(recovery.Output, recovery.Artifacts)) is IOException,
            "A retained recovery lock allowed another publication.");

        return $"renameFaultsRolledBack={failurePoints.Length} firstPublicationRollback=1 cancellationBeforeCommit=1 noncancellableCommit=1 invalidPathsRefused={invalidCases} recoveryConflictPreserved=1";
    }

    private static void CheckInvalid(string root, Func<Fixture, ExportArtifactReplacement[]> mutate, ref int count)
    {
        Fixture fixture = Create(root);
        ExportArtifactReplacement[] invalidArtifacts = mutate(fixture);
        ExpectFailure(() => ExportArtifactTransaction.Publish(fixture.Output, invalidArtifacts));
        RequirePrevious(fixture);
        RequireClean(fixture);
        count++;
    }

    private static Fixture Create(string root)
    {
        string output = Path.Combine(root, "transaction-" + Guid.NewGuid().ToString("N"));
        string staging = Path.Combine(output, "stage");
        Directory.CreateDirectory(staging);
        ExportArtifactReplacement[] artifacts =
        [
            new(Path.Combine(output, "game.bin"), Path.Combine(staging, "new.bin")),
            new(Path.Combine(output, "summary.json"), Path.Combine(staging, "new.json")),
            new(Path.Combine(output, "stale-diagnostics.txt"), null),
            new(Path.Combine(output, "game.cue"), Path.Combine(staging, "new.cue"))
        ];
        Dictionary<string, string> previous = [];
        Dictionary<string, string> staged = [];
        foreach (ExportArtifactReplacement artifact in artifacts)
        {
            previous.Add(artifact.DestinationPath, "old " + Path.GetFileName(artifact.DestinationPath));
            File.WriteAllText(artifact.DestinationPath, previous[artifact.DestinationPath]);
            if (artifact.StagedPath == null) continue;
            staged.Add(artifact.StagedPath, "new " + Path.GetFileName(artifact.DestinationPath));
            File.WriteAllText(artifact.StagedPath, staged[artifact.StagedPath]);
        }
        return new Fixture(output, artifacts, previous, staged);
    }

    private static void Inject(Fixture fixture, Action<string> checkpoint, CancellationToken cancellationToken = default)
    {
        MethodInfo method = typeof(ExportArtifactTransaction).GetMethod("PublishCore", BindingFlags.Static | BindingFlags.NonPublic)!;
        try { method.Invoke(null, [fixture.Output, fixture.Artifacts, cancellationToken, checkpoint]); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    private static Exception ExpectFailure(Action action)
    {
        try { action(); }
        catch (Exception exception) { return exception; }
        throw new InvalidOperationException("Expected publication to fail.");
    }

    private static void RequirePrevious(Fixture fixture)
    {
        foreach ((string path, string expected) in fixture.Previous)
            Require(File.Exists(path) && File.ReadAllText(path) == expected, "Previous artifact changed: " + path);
    }

    private static void RequireStaged(Fixture fixture)
    {
        foreach ((string path, string expected) in fixture.Staged)
            Require(File.Exists(path) && File.ReadAllText(path) == expected, "Staged artifact changed: " + path);
    }

    private static void RequirePublished(Fixture fixture)
    {
        foreach (ExportArtifactReplacement artifact in fixture.Artifacts)
            Require(artifact.StagedPath == null
                    ? !File.Exists(artifact.DestinationPath)
                    : File.ReadAllText(artifact.DestinationPath) == fixture.Staged[artifact.StagedPath],
                "Incomplete successful publication: " + artifact.DestinationPath);
    }

    private static void RequireClean(Fixture fixture) => Require(
        !File.Exists(Path.Combine(fixture.Output, ".export-publication.lock")) &&
        Directory.GetDirectories(fixture.Output, ".export-recovery-*").Length == 0,
        "Completed publication/rollback leaked a recovery directory or lock.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record Fixture(string Output, ExportArtifactReplacement[] Artifacts,
        Dictionary<string, string> Previous, Dictionary<string, string> Staged);
}
