// Tests first: default controls never inspect a source, fixture or output.
using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class TexturedPublicationArgumentCases
{
    private static readonly string[] Modes =
    [
        "--publish-textured-reviewed", "--verify-textured-reviewed",
        "--stage-textured-reviewed", "--unconfirmed-textured-reviewed", "--postcommit-textured-reviewed"
    ];

    internal static async Task Free()
    {
        foreach (string mode in Modes)
        {
            string[] valid = [mode, "source.bin", "source.cue", "/explicit-parent/absent-or-existing"];
            var request = TexturedPublicationCases.Parse(valid);
            Check(request.Mode == mode && request.Output == valid[3], "Exactly four tokens; mode is no source authority.");
            for (int length = 0; length < 4; length++) RefuseArguments(valid[..length]);
            RefuseArguments([..valid, "extra"]);
            for (int field = 1; field < 4; field++)
            foreach (string? bad in new string?[] { null, "", " \t\r\n", "\0", "a\0b", "--unknown", "--source",
                "--publish-reviewed", "--publish-separated-reviewed", mode, mode + "=value" }.Concat(Modes))
            {
                string[] changed = valid.ToArray(); changed[field] = bad!; RefuseArguments(changed);
            }
            foreach (string relative in new[] { "output", "./output", "../output", "~/output" })
                RefuseArguments([mode, "bin", "cue", relative]);
        }
        foreach (string other in new[] { "--source", "--publish-reviewed", "--verify-reviewed",
            "--publish-separated-reviewed", "--postcommit-reviewed", "--Publish-textured-reviewed",
            "--publish-textured-reviewed ", "--publish-textured-reviewed=x" })
        {
            Check(!TexturedPublicationCases.IsMode([other]), "Only five exact new names enter physical dispatch.");
            RefuseArguments([other, "bin", "cue", "/parent/output"]);
        }
        RefuseArguments(null!);
        Check(!TexturedPublicationCases.IsMode([]), "Default never dispatches physical work.");

        // Neutral completion only. No actual IO, publication, typed commit or source result is manufactured.
        var primary = new InvalidDataException("simulated operation failure");
        var binFinal = new IOException("simulated BIN final");
        var cueFinal = new IOException("simulated CUE final");
        var namedBin = new IOException("simulated current named BIN final");
        var namedCue = new IOException("simulated current named CUE final");
        var close = new IOException("simulated terminal close");
        int finals = 0;
        var failed = await Failure(() => TexturedPublicationCases.FinishAsync(
            () => Task.FromException<string>(primary), [
                () => { finals++; throw binFinal; }, () => { finals++; throw cueFinal; },
                () => { finals++; throw namedBin; }, () => { finals++; throw namedCue; },
                () => { finals++; throw close; }, () => { finals++; }]));
        Check(finals == 6 && failed is IOException { InnerException: AggregateException a } &&
            a.InnerExceptions.SequenceEqual(new Exception[] { primary, binFinal, cueFinal, namedBin, namedCue, close }),
            "Every final and close attempted; exact primary and additional errors remain ordered.");
        Check(ReferenceEquals(await Failure(() => TexturedPublicationCases.FinishAsync(
            () => Task.FromException<string>(primary), [])), primary), "Single failure identity is exact.");
        Check(ReferenceEquals(await Failure(() => TexturedPublicationCases.FinishAsync(
            () => Task.FromResult("must-not-report"), [() => throw cueFinal])), cueFinal),
            "Final failure cannot emit a success report.");
        Check(TexturedPublicationCases.FailureOutcome(failed, false, false) == "verification-failed" &&
            TexturedPublicationCases.FailureOutcome(failed, false, true) == "not-confirmed",
            "Simulated errors imply neither known commit nor Unconfirmed transaction entry.");
    }

    internal static async Task ActualInitialSourceFailure()
    {
        string root = Directory.CreateTempSubdirectory("textured-cli-initial-sources-").FullName;
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal) &&
            new DirectoryInfo("/var").ResolveLinkTarget(true) is { } resolved)
            root = Path.Combine(resolved.FullName, root[5..]);
        string bin = Path.Combine(root, "bad.bin"), cue = Path.Combine(root, "bad.cue"), output = Path.Combine(root, "must-remain-absent");
        byte[] binBytes = [1,2,3], cueBytes = new byte[88]; bool passed = false;
        TextWriter priorError = Console.Error; using var diagnostic = new StringWriter();
        try
        {
            File.WriteAllBytes(bin, binBytes); File.WriteAllBytes(cue, cueBytes);
            Console.SetError(diagnostic);
            var error = await RefuseAsync<IOException>(() => TexturedPublicationCases.RunAsync(
                ["--publish-textured-reviewed", bin, cue, output]), "Wrong initial BIN refuses before full chain or any candidate output.");
            Check(error.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count >= 3,
                "Initial source failure plus BOTH current named source checks are retained independently.");
            Check(!Directory.Exists(output) && !File.Exists(output) &&
                diagnostic.ToString().Contains("\"outcome\":\"not-confirmed\"", StringComparison.Ordinal),
                "Pre-Candidate failure cannot create output or acknowledge a transaction.");
            using (var first = new FileStream(bin, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var second = new FileStream(cue, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(first.Length == 3 && second.Length == 88, "Current CUE acquired before its hash refusal is closed.");
            passed = true;
        }
        finally
        {
            Console.SetError(priorError);
            if (passed && Directory.GetFileSystemEntries(root).Order().SequenceEqual(new[] { bin, cue }.Order()) &&
                new DirectoryInfo(root).LinkTarget == null && new FileInfo(bin).LinkTarget == null && new FileInfo(cue).LinkTarget == null &&
                File.ReadAllBytes(bin).SequenceEqual(binBytes) && File.ReadAllBytes(cue).SequenceEqual(cueBytes))
            { File.Delete(bin); File.Delete(cue); Directory.Delete(root); }
            else Console.Error.WriteLine("Preserve early CLI source fixture: " + root + "\n" + diagnostic);
        }
        Console.WriteLine("TEXTURED_CLI_INITIAL_SOURCE_REFUSAL_OK candidateCapture=false physicalOutput=false");
    }

    private static void RefuseArguments(string[] args)
    {
        try { _ = TexturedPublicationCases.Parse(args); }
        catch (ArgumentException) { Check(true, "Malformed physical CLI before any path IO."); return; }
        throw new InvalidDataException("Malformed physical CLI was accepted.");
    }
    private static async Task<Exception> Failure(Func<Task> action)
    {
        try { await action(); } catch (Exception error) { return error; }
        throw new InvalidDataException("Expected failure.");
    }
}

// This source method will receive real issued owners from the existing source
// matrix, not construct private state. It never invokes a physical output path.
internal static class TexturedFaultApiCases
{
    internal static async Task Free()
    {
        Check(Enum.GetNames<NativeId65FreshTexturedResearchFault>().SequenceEqual(
            new[] { "None", "AfterFirstSector", "AfterImageWrite", "BeforePublication" }),
            "Exactly three forced precommit faults plus forbidden None.");
        Refuse<InvalidDataException>(() => NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(
            null!, "\0", NativeId65FreshTexturedResearchFault.None), "None refuses synchronously before plan/token.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        foreach (Func<Task> call in new Func<Task>[] {
            () => NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(null!, "\0",
                NativeId65FreshTexturedResearchFault.AfterFirstSector, null, canceled.Token),
            () => NativeId65FreshTexturedResearchCandidatePublisher.PublishPostcommitForSmokeAsync(null!, "\0", null!, canceled.Token),
            () => NativeId65FreshTexturedResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(null!, "\0", null!, canceled.Token),
            () => NativeId65FreshResearchPublicationEngine.PublishTexturedPostcommitForSmokeAsync(null!, "\0", null!, canceled.Token),
            () => NativeId65FreshResearchPublicationEngine.PublishTexturedUnconfirmedForSmokeAsync(null!, "\0", null!, canceled.Token) })
        {
            Task task = call(); Check(task.IsFaulted, "Null new seam returns faulted Task without IO.");
            await RefuseAsync<ArgumentNullException>(() => task, "Null binding precedes token/callback/path.");
        }
    }

    internal static async Task Source(NativeId65FreshTexturedResearchCandidatePlan actual,
        NativeId65FreshResearchPublicationBinding old)
    {
        Refuse<InvalidDataException>(() => NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(
            actual, "\0", NativeId65FreshTexturedResearchFault.None), "Always-forced route refuses None.");
        await RefuseAsync<ArgumentNullException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishPostcommitForSmokeAsync(actual, "\0", null!),
            "Actual new binding requires explicit postcommit hook before path IO.");
        await RefuseAsync<ArgumentNullException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(actual, "\0", null!),
            "Actual new binding requires explicit unconfirmed hook.");
        await RefuseAsync<InvalidDataException>(() =>
            NativeId65FreshResearchPublicationEngine.PublishTexturedUnconfirmedForSmokeAsync(old, "\0", _ => { }, default),
            "A genuine old binding cannot enter the new textured role.");
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); bool called = false;
        await RefuseAsync<OperationCanceledException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishPostcommitForSmokeAsync(
                actual, "\0", (_, _) => called = true, canceled.Token), "Pre-cancel actual new role before path.");
        await RefuseAsync<OperationCanceledException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishUnconfirmedForSmokeAsync(
                actual, "\0", _ => called = true, canceled.Token), "Pre-cancel before unconfirmed hook.");
        await RefuseAsync<OperationCanceledException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(
                actual, "\0", NativeId65FreshTexturedResearchFault.AfterFirstSector, (_, _) => called = true, canceled.Token),
                "Pre-cancel before forced write.");
        await RefuseAsync<InvalidDataException>(() =>
            NativeId65FreshTexturedResearchCandidatePublisher.PublishForSmokeAsync(
                actual, "\0", (NativeId65FreshTexturedResearchFault)999), "Unknown fault refuses before path.");
        await RefuseAsync<InvalidDataException>(() =>
            NativeId65FreshResearchPublicationEngine.PublishTexturedPostcommitForSmokeAsync(old, "\0", (_, _) => { }, default),
            "Genuine old role cannot use new postcommit scope.");
        Check(!called, "No hook invocation or filesystem path under pre-cancel.");
    }
}
