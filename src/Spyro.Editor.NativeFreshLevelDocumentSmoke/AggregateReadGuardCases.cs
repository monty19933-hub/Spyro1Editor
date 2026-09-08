using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

// Actual tiny Store/marker operations only, including JSON Save/Fork setup.
// No game source, compiler or BIN/CUE artifact publication.
internal static class AggregateReadGuardCases
{
    private static string _root = "";
    private static int _checks;
    internal const string LockName = ".native-fresh-level.writer.lock";
    internal static void Run()
    {
        _root = Directory.CreateTempSubdirectory("spyro-aggregate-read-guard-").FullName;
        bool passed = false;
        try
        {
            AggregateReadContextCases.Run();
            ContractAndCooperation();
            AcquisitionFailures();
            TerminalOutcomes();
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(_root, recursive: true); // Only this new test-owned tree, including test-made foreign markers.
            else Console.Error.WriteLine("Retained neutral read-guard fixture: " + _root);
        }
        Console.WriteLine($"NATIVE_FRESH_LEVEL_DOCUMENT_READ_GUARD_OK checks={_checks} sourceFree=true artifactPublicationInvoked=false");
    }

    private static void ContractAndCooperation()
    {
        var (project, saved) = Saved("cooperation");
        var legacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Cases.Base());
        byte[] document = File.ReadAllBytes(saved.Path), manifest = File.ReadAllBytes(project.ManifestPath);
        var context = NativeFreshSavedLevelDocumentReadContext.Capture(saved);
        string leasePath = Path.Combine(project.RootPath, LockName);
        Check(!File.Exists(leasePath), "context capture holds no long-running writer exclusion");
        var owner = context.AcquireFinalReadGuard();
        Check(owner.LeasePath == leasePath && new FileInfo(leasePath).Length == 32 &&
            owner.RetainedEvidencePath == "" && owner.TerminalFailure == null, "actual aggregate32 marker, no lease evidence initially");
        Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, saved.Intent!));
        Throws<IOException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(saved));
        NativeId65FreshDocumentStore.Save(legacy, Cases.Base(20));
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(document), "legacy writer remains a separate profile");
        owner.Complete(); owner.Complete();
        Check(!File.Exists(leasePath) && owner.TerminalFailure == null && owner.RetainedEvidencePath == "", "clean terminal release and repeat");
        var newer = NativeFreshLevelDocumentStore.Save(saved, Cases.Document(Cases.Wizard(Cases.Id2)));
        Check(newer.Intent!.Placements.Single().EditorId == Cases.Id2 && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "aggregate Save resumes after guard");
        File.WriteAllText(leasePath, "later-foreign");
        owner.Complete();
        Throws<InvalidOperationException>(() => owner.Complete(new IOException("different primary after success")));
        Check(File.ReadAllText(leasePath) == "later-foreign", "repeat/misuse never deletes a later owner");
        Throws<IOException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(newer));
        Check(File.ReadAllText(leasePath) == "later-foreign", "failed CreateNew never acquires foreign marker");

        var (forkProject, forkSaved) = Saved("fork-destination");
        var source = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(forkProject), Cases.Base());
        File.Delete(forkSaved.Path);
        var absent = NativeFreshLevelDocumentStore.Capture(forkProject);
        File.WriteAllBytes(forkSaved.Path, NativeFreshLevelDocumentCodec.SerializeCanonical(forkSaved.Intent!));
        var held = NativeFreshLevelDocumentStore.AcquireReadLease(forkSaved);
        // The genuine previously absent snapshot reaches actual destination lock acquisition; no forged absent observation.
        Throws<IOException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent));
        Check(new FileInfo(held.LeasePath).Length == 32 && !File.Exists(Path.Combine(forkProject.RootPath, ".native-id65-fresh-document.writer.lock")), "failed Fork destination acquisition accounts first source guard");
        held.Complete();
        Type guard = typeof(NativeFreshLevelDocumentStore.ReadLease);
        Check(guard.IsSealed && !typeof(IDisposable).IsAssignableFrom(guard) && guard.GetConstructors().Length == 0 &&
            typeof(NativeFreshLevelDocumentStore).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order().SequenceEqual(new[] { "Capture", "ForkLegacyFloor", "Save" }), "internal explicit completion only; unchanged public Store surface");
    }

    private static void AcquisitionFailures()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(null!, canceled.Token));
        Throws<ArgumentNullException>(() => NativeFreshLevelDocumentStore.AcquireReadLease(null!));
        foreach (string mode in new[] { "cancel-owned", "cancel-initialized", "changed-owned", "throw-owned", "throw-initialized", "combined" })
        {
            var (project, saved) = Saved(mode);
            string path = Path.Combine(project.RootPath, LockName), moved = path + ".held";
            using var token = new CancellationTokenSource();
            var primary = new IOException("actual acquisition checkpoint failure: " + mode);
            var phases = new List<string>();
            Exception actual = Throws<Exception>(() => NativeFreshLevelDocumentStore.AcquireReadLeaseForSmoke(saved, phase =>
            {
                phases.Add(phase);
                Check(new FileInfo(path).Length == (phase == "owned" ? 0 : 32), "owner assigned before initialization; marker flushed before initialized checkpoint");
                if (mode == "cancel-" + phase) token.Cancel();
                if (mode == "changed-owned" && phase == "owned") File.AppendAllText(saved.Path, " ");
                if (mode == "throw-" + phase) throw primary;
                if (mode == "combined" && phase == "initialized")
                { File.Move(path, moved); File.WriteAllText(path, "foreign-after-acquire"); token.Cancel(); throw primary; }
            }, token.Token));
            if (mode == "combined")
            {
                var errors = Members(actual);
                Check(errors.Count == 2 && ReferenceEquals(errors[0], primary) && errors[1] is InvalidDataException &&
                    File.ReadAllText(path) == "foreign-after-acquire" && new FileInfo(moved).Length == 32, "failed acquire keeps primary first and actual release error, despite cancellation");
            }
            else if (mode == "throw-owned")
            {
                var errors = Members(actual);
                Check(errors.Count == 2 && ReferenceEquals(errors[0], primary) && errors[1] is InvalidDataException &&
                    new FileInfo(path).Length == 0, "pre-initialization throw retains empty marker and ordered primary/release error; it never claims token authentication");
            }
            else
            {
                Check(!File.Exists(path), "failed/canceled initialized owner is released once");
                Check(mode.StartsWith("throw-") ? ReferenceEquals(actual, primary) :
                    mode.StartsWith("cancel-") ? actual is OperationCanceledException oce && oce.CancellationToken == token.Token :
                    actual is InvalidDataException, "actual acquisition failure not masked");
            }
            Check(phases[0] == "owned" && phases.Count is 1 or 2, "bounded actual acquisition checkpoints");
        }
    }

    private static void TerminalOutcomes()
    {
        foreach (string mode in new[] { "clean-primary", "cancel-primary", "foreign", "missing", "link", "directory" })
        foreach (bool withPrimary in new[] { false, true })
        {
            var (project, saved) = Saved("terminal-" + mode + withPrimary);
            var guard = NativeFreshLevelDocumentStore.AcquireReadLease(saved);
            string path = guard.LeasePath, held = path + ".held";
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            Exception? primary = withPrimary ? mode == "cancel-primary" ? new OperationCanceledException(cancel.Token) : new IOException("operation primary") : null;
            if (mode is not ("clean-primary" or "cancel-primary"))
            {
                File.Move(path, held);
                if (mode == "foreign") File.WriteAllText(path, "foreign");
                else if (mode == "link") File.CreateSymbolicLink(path, held);
                else if (mode == "directory") Directory.CreateDirectory(path);
            }
            Exception? outcome = null;
            try { guard.Complete(primary); } catch (Exception error) { outcome = error; }
            if (mode is "clean-primary" or "cancel-primary")
                Check(ReferenceEquals(outcome, primary) && guard.TerminalFailure == null && guard.RetainedEvidencePath == "" && !File.Exists(path), "clean release preserves exact primary/cancellation or success");
            else
            {
                var errors = Members(outcome!);
                Check(errors.Count == (withPrimary ? 2 : 1) && (!withPrimary || ReferenceEquals(errors[0], primary)) &&
                    ReferenceEquals(errors[^1], guard.TerminalFailure) && guard.RetainedEvidencePath == (mode == "missing" ? project.RootPath : path), "actual terminal error accounted exactly once with evidence");
                Check(new FileInfo(held).Length == 32 && (mode switch
                { "foreign" => File.ReadAllText(path) == "foreign", "missing" => !File.Exists(path), "link" => new FileInfo(path).LinkTarget == held, _ => Directory.Exists(path) }), "foreign/missing/link/directory owners preserved");
            }
            void Repeat(Exception? repeatPrimary)
            {
                Exception? repeated = null;
                try { guard.Complete(repeatPrimary); } catch (Exception error) { repeated = error; }
                Check(ReferenceEquals(repeated, outcome), "same non-null primary and argument-free repeats replay exact cached outcome");
            }
            Repeat(primary); Repeat(null);
            Throws<InvalidOperationException>(() => guard.Complete(new IOException("different later primary")));
            if (mode == "missing") Check(!File.Exists(path), "failed terminal repeat never resurrects marker");
        }
    }

    internal static EditorProjectLayout Project(string label)
    {
        string name = label + "-" + Guid.NewGuid().ToString("N");
        string root = Path.Combine(_root, name); Directory.CreateDirectory(root);
        var project = new EditorProjectLayout(root, "read-guard");
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"read-guard\",\"unrelated\":true}");
        return project;
    }
    internal static (EditorProjectLayout Project, NativeFreshLevelDocumentSnapshot Snapshot) Saved(string label, NativeFreshLevelDocumentIntent? document = null)
    {
        var project = Project(label);
        return (project, NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), document ?? Cases.Document(Cases.Wizard())));
    }
    internal static void Check(bool value, string label) { Cases.Check(value, label); _checks++; }
    internal static T Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T error) { _checks++; Cases.Checks++; Cases.Refusals++; return error; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    internal static IReadOnlyList<Exception> Members(Exception error)
    { Check(error is IOException && error.InnerException is AggregateException, "new-only accounting IOException retains ordered error objects"); return ((AggregateException)error.InnerException!).InnerExceptions; }
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
