using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class NewArtifactPublicationCases
{
    public static string Run(string fixtureRoot)
    {
        int checks = 0;
        string[] boundaries = ["prepared", "after-backup:0", "after-backup:1", "after-backup:2",
            "after-publish:0", "after-publish:1", "after-publish:2"];
        foreach (string boundary in boundaries)
        {
            var (root, artifacts) = Fixture("fault-" + boundary.Replace(':', '-'));
            Reject(() => Invoke(root, artifacts, phase =>
            {
                if (phase == boundary) throw new IOException("Injected new-only publication fault.");
            }));
            Check(artifacts.All(a => !File.Exists(a.DestinationPath) && File.ReadAllText(a.StagedPath!) == Path.GetFileName(a.DestinationPath)),
                "failed new-only publication must restore all staged files and no final paths");
            Check(!File.Exists(Path.Combine(root, ".export-publication.lock")), "rolled-back lock removed");
        }
        var (successRoot, success) = Fixture("success");
        ExportArtifactTransaction.PublishNew(successRoot, success);
        Check(success.All(a => File.ReadAllText(a.DestinationPath) == Path.GetFileName(a.DestinationPath) && !File.Exists(a.StagedPath)),
            "complete new set published");
        foreach (var artifact in success) File.WriteAllText(artifact.StagedPath!, "unrelated new content");
        Reject(() => ExportArtifactTransaction.PublishNew(successRoot, success));
        Check(success.All(a => File.ReadAllText(a.DestinationPath) == Path.GetFileName(a.DestinationPath) &&
            File.ReadAllText(a.StagedPath!) == "unrelated new content"), "repeat cannot overwrite immutable set");

        foreach (string boundary in new[] { "prepared", "after-backup:2", "after-publish:0" })
        {
            var (root, artifacts) = Fixture("race-" + boundary.Replace(':', '-'));
            string competing = artifacts[2].DestinationPath;
            Reject(() => Invoke(root, artifacts, phase =>
            {
                if (phase == boundary) File.WriteAllText(competing, "competing owner");
            }));
            Check(File.ReadAllText(competing) == "competing owner" &&
                artifacts.Take(2).All(a => !File.Exists(a.DestinationPath)) &&
                artifacts.All(a => File.Exists(a.StagedPath)), "competing destination preserved with full own rollback");
        }
        var (partialRoot, partial) = Fixture("partial");
        File.WriteAllText(partial[1].DestinationPath, "incomplete candidate");
        Reject(() => ExportArtifactTransaction.PublishNew(partialRoot, partial));
        Check(File.ReadAllText(partial[1].DestinationPath) == "incomplete candidate" &&
            !File.Exists(partial[0].DestinationPath), "incomplete set is not repaired by overwriting");
        var (nullRoot, nullArtifacts) = Fixture("null-staging");
        Reject(() => ExportArtifactTransaction.PublishNew(nullRoot,
            [new(nullArtifacts[0].DestinationPath, null)]));
        using CancellationTokenSource cancellation = new(); cancellation.Cancel();
        Reject(() => ExportArtifactTransaction.PublishNew(nullRoot, nullArtifacts, cancellation.Token));
        Check(nullArtifacts.All(a => File.Exists(a.StagedPath) && !File.Exists(a.DestinationPath)), "prepublication cancellation is write-free");
        return $"newOnlyPublicationChecks={checks}";

        (string Root, ExportArtifactReplacement[] Artifacts) Fixture(string name)
        {
            string root = Path.Combine(fixtureRoot, "new-only-" + name);
            string stage = Path.Combine(root, "staging"); Directory.CreateDirectory(stage);
            var entries = new[] { "candidate.bin", "receipt.json", "candidate.cue" }
                .Select(name => new ExportArtifactReplacement(Path.Combine(root, name), Path.Combine(stage, name))).ToArray();
            foreach (var entry in entries) File.WriteAllText(entry.StagedPath!, Path.GetFileName(entry.DestinationPath));
            return (root, entries);
        }
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException) { checks++; return; }
            throw new InvalidOperationException("Expected new-only publication rejection.");
        }
    }

    private static void Invoke(string root, IReadOnlyList<ExportArtifactReplacement> artifacts, Action<string> checkpoint)
    {
        try
        {
            typeof(ExportArtifactTransaction).GetMethod("PublishCoreWithPolicy", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [root, artifacts, CancellationToken.None, checkpoint, true]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
