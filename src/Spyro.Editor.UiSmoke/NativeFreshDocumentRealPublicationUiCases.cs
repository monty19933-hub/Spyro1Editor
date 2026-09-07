using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

// Opt-in integration evidence only. Argument/free modes never call Exercise.
// The output picker is the ONLY substituted operation in the actual UI flow.
internal static class NativeFreshDocumentRealPublicationUiCases
{
    internal const string ExerciseMode = "--exercise-fresh-document-research-ui";
    internal const string ArgumentsMode = "--fresh-document-research-ui-arguments-only";
    private const string RemovedVerifyMode = "--verify-fresh-document-research-ui";
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string EditedHash = "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16";
    private const long ImageLength = 661547040;
    private const string ImageName = "native-id65-fresh-document-research.bin";
    private const string CueName = "native-id65-fresh-document-research.cue";
    private const string ReceiptName = "native-id65-fresh-document-research.receipt.json";
    private static int _checks;

    internal sealed record Invocation(bool ArgumentsOnly, string Workspace, string Image, string Cue, string RunDirectory);

    internal static bool Handles(string[] args) => args.Any(a =>
        EqualSwitch(a, ExerciseMode) || EqualSwitch(a, ArgumentsMode) || EqualSwitch(a, RemovedVerifyMode));

    internal static Invocation Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1 && EqualSwitch(args[0], ArgumentsMode)) return new(true, "", "", "", "");
        if (args.Length != 5 || !EqualSwitch(args[0], ExerciseMode) ||
            args.Skip(1).Any(a => string.IsNullOrWhiteSpace(a) || a.Contains('\0') || a.StartsWith("--", StringComparison.Ordinal)))
            throw new ArgumentException($"Usage: UiSmoke {ExerciseMode} source-workspace original-BIN original-CUE absent-run-directory. " +
                $"Source-free argument checks: {ArgumentsMode}. Separate readback uses the reviewed generic publisher CLI; no UiSmoke verify mode exists.");
        return new(false, Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), Path.GetFullPath(args[3]), Path.GetFullPath(args[4]));
    }

    internal static void ArgumentCases()
    {
        int start = _checks;
        string[] valid = [ExerciseMode, "workspace", "original.bin", "original.cue", "absent-run"];
        Check(Handles(valid) && !Parse(valid).ArgumentsOnly && Path.IsPathFullyQualified(Parse(valid).RunDirectory), "exact exercise grammar is parsed without I/O");
        Check(Parse([ArgumentsMode]).ArgumentsOnly, "argument-only mode contains no source/output paths");
        Check(!Handles([]) && !Handles(["--source"]) && !Handles(["--fresh-native-document-only"]), "legacy and neutral modes cannot select publication");
        Check(Handles(["workspace", ExerciseMode]) && Handles([RemovedVerifyMode]), "misplaced and removed explicit modes cannot fall through to old fixtures");
        foreach (string[] bad in new[] { Array.Empty<string>(), [ExerciseMode], [ArgumentsMode, "extra"],
            [RemovedVerifyMode, "a", "b", "c"], ["workspace", ExerciseMode, "image", "cue", "run"],
            valid.Append("extra").ToArray(), valid.Take(4).ToArray(), [ExerciseMode, ArgumentsMode, "image", "cue", "run"] })
            Reject(() => Parse(bad));
        for (int index = 1; index < valid.Length; index++)
        foreach (string badValue in new[] { "", " ", "\0", ExerciseMode, ArgumentsMode, RemovedVerifyMode, "--source", "--fresh-native-document-only" })
        {
            string[] bad = valid.ToArray(); bad[index] = badValue; Reject(() => Parse(bad));
        }
        Console.WriteLine($"FRESH_DOCUMENT_REAL_UI_ARGUMENTS_OK checks={_checks - start} sourceRequired=false publicationExecuted=false");
    }

    // Called before the existing allowlisted cache copier can create its private
    // temporary workspace. This is fixture safety, not source publication authority.
    internal static void Preflight(Invocation invocation)
    {
        if (invocation.ArgumentsOnly) throw new ArgumentException("Argument-only invocation cannot set up a publication fixture.");
        RequireAbsent(invocation.RunDirectory);
        foreach (string path in new[] { invocation.Workspace, invocation.Image, invocation.Cue, invocation.RunDirectory }) RefuseLinkedAncestors(path);
        if (!Directory.Exists(invocation.Workspace) || !Directory.Exists(Path.Combine(invocation.Workspace, "editor-cache")))
            throw new DirectoryNotFoundException("The explicit source workspace and its existing read-only editor cache are required.");
        foreach (string protectedPath in new[] { invocation.Image, invocation.Cue, Path.Combine(invocation.Workspace, "editor-cache"), Path.Combine(invocation.Workspace, "support") })
            if (Nested(invocation.RunDirectory, protectedPath) || Nested(protectedPath, invocation.RunDirectory))
                throw new InvalidDataException("The fixture run directory overlaps protected source owners.");
        if (Nested(invocation.Workspace, invocation.RunDirectory))
            throw new InvalidDataException("A fixture cannot own the source workspace or one of its ancestors.");
        Check(HashFile(invocation.Image, ImageLength, ImageLength) == OriginalHash, "exact original BIN before any fixture writes");
        _ = HashFile(invocation.Cue, 65536);
        DiscImageSelection selection = DiscImageLocator.ResolveSelection(invocation.Cue);
        Check(selection.ImageExists && selection.CueExists && SamePath(selection.ImagePath, invocation.Image) && SamePath(selection.CuePath, invocation.Cue),
            "explicit CUE resolves the supplied original BIN before private setup");
        Check(SamePath(DiscImageLocator.FindCueForImage(invocation.Image), invocation.Cue), "the production main source picker will resolve the explicit CUE");
    }

    internal static void Exercise(Invocation invocation, string isolatedWorkspace)
    {
        // This private directory was produced by Program's existing copier. It
        // has no manifest/document, and is never a user project or output folder.
        RequireAbsent(invocation.RunDirectory);
        if (!Directory.Exists(isolatedWorkspace) || File.Exists(Path.Combine(isolatedWorkspace, "spyro-project.json")) ||
            File.Exists(Path.Combine(isolatedWorkspace, NativeId65FreshDocumentStore.FileName)))
            throw new InvalidDataException("The UI fixture requires an actual new isolated metadata/cache copy.");
        string cueHash = HashFile(invocation.Cue, 65536);
        string projectRoot = Path.Combine(invocation.RunDirectory, "project");
        string candidate = Path.Combine(invocation.RunDirectory, "candidate");
        string install = Path.Combine(invocation.RunDirectory, "install");
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
        string? previousRelease = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable);
        string? previousDiagnostics = Environment.GetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR");
        MainWindow? main = null;
        NativeFreshDocumentWindow? window = null;
        Dictionary<string, string>? sentinels = null;
        bool complete = false;
        string? completionEvidence = null;
        Directory.CreateDirectory(invocation.RunDirectory);
        try
        {
            Directory.Move(isolatedWorkspace, projectRoot);
            string support = Directory.CreateDirectory(Path.Combine(install, "support")).FullName;
            foreach (string name in new[] { "spyro-level-catalog.json", "spyro-object-templates.json" })
                File.Copy(Path.Combine(projectRoot, name), Path.Combine(support, name), overwrite: false);
            var userData = new EditorUserDataLayout(Path.Combine(invocation.RunDirectory, "user-data"));
            Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, projectRoot);
            Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable, "1");
            string diagnostics = Directory.CreateDirectory(Path.Combine(invocation.RunDirectory, "diagnostics")).FullName;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", diagnostics);
            Environment.CurrentDirectory = projectRoot;
            EditorDiagnostics.Initialize(AppReleaseIdentity.DiagnosticVersion);
            Check(SamePath(EditorDiagnostics.Current.LogDirectory, diagnostics), "actual diagnostics remain in the explicit private run directory");
            ReleaseProjectContext actual = ReleaseProjectBootstrap.PrepareAsync(AppReleaseIdentity.DiagnosticVersion,
                appBaseDirectory: install, userData: userData, explicitInstallRoot: install, forceReleaseMode: true).GetAwaiter().GetResult()
                ?? throw new InvalidDataException("The real release bootstrap did not issue a project context.");
            Check(ReferenceEquals(actual, ReleaseProjectBootstrap.Current) && SamePath(actual.Project.RootPath, projectRoot) &&
                actual.Project.ProjectId == "project" && actual.AutomaticMigration == null && ReferenceEquals(actual.UserData, userData),
                "actual bootstrap issues the private current project and explicit user-data owner without migration");
            Check(!NativeId65FreshDocumentStore.Capture(actual.Project).Exists, "actual project starts without a saved or inferred fresh intent");
            DiscImageSelection selected = DiscImageLocator.ConfigureImage(new EditorWorkspace(projectRoot), invocation.Cue);
            Check(SamePath(selected.ImagePath, invocation.Image) && SamePath(selected.CuePath, invocation.Cue), "only the private project selects the explicit original CUE");
            // Use the existing startup state schema and policy, not a replaced
            // network client or synthetic MainWindow context. Only this run's
            // private user-data is touched; no installed-editor settings are read.
            string updates = Path.Combine(userData.SettingsPath, "update-check.json");
            File.WriteAllBytes(updates, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 3, lastCheckedUtc = DateTimeOffset.UtcNow,
                availablePublicVersion = "", lastAutoNotificationPublicVersion = "" }));
            string reminder = Path.Combine(userData.SettingsPath, "beta-v2-project-reminder.json");
            File.WriteAllText(reminder, "{\"privateFixtureAlreadyHandled\":true}");
            string legacy = Path.Combine(projectRoot, "unusedlevel65blank-authoring-document-v1.json");
            File.WriteAllText(legacy, "unchanged old Lab sentinel; not a fresh-native intent");
            string sourceSetting = Path.Combine(projectRoot, "_local", "settings", "source-disc.json");
            sentinels = Directory.EnumerateFiles(projectRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Concat(Directory.EnumerateFiles(Path.Combine(projectRoot, "editor-cache"), "*", SearchOption.AllDirectories))
                .Concat([updates, reminder, sourceSetting]).Distinct(StringComparer.Ordinal)
                .ToDictionary(p => p, p => HashFile(p, 256L * 1024 * 1024, permitEmpty: true), StringComparer.Ordinal);

            HeadlessEntryPoint.BuildAvaloniaApp().SetupWithoutStarting();
            main = new MainWindow { Width = 1320, Height = 860 };
            main.Show();
            EditorViewport viewport = main.GetLogicalDescendants().OfType<EditorViewport>().Single();
            WaitUntil(() => viewport.Geometry != null && viewport.Mobys.Count != 0, TimeSpan.FromSeconds(20), "actual retail shell initial cache load");
            Click(Find<Button>(main, "FreshNativeDocumentButton"));
            WaitUntil(() => main.OwnedWindows.OfType<NativeFreshDocumentWindow>().Any(), TimeSpan.FromSeconds(5), "actual toolbar opens Fresh Level");
            window = main.OwnedWindows.OfType<NativeFreshDocumentWindow>().Single();
            Check(window.CurrentIntent == null && !window.HasUnsavedChanges, "real owned window offers no implicit starter");
            Click(Find<Button>(window, "FreshDocumentNew"));
            foreach (var field in new Dictionary<string, string>
            {
                ["FreshFloorX"] = "8192", ["FreshFloorY"] = "8192", ["FreshFloorZ"] = "8704",
                ["FreshFloorWidth"] = "2048", ["FreshFloorDepth"] = "2048", ["FreshSpawnX"] = "9216", ["FreshSpawnY"] = "9216",
                ["FreshHeading"] = "96", ["FreshFloorColor"] = "#488478", ["FreshSkyColor"] = "#2C4264"
            }) Find<TextBox>(window, field.Key).Text = field.Value;
            Find<ComboBox>(window, "FreshDocumentMusic").SelectedItem = MusicTrackCatalog.Find(2);
            Click(Find<Button>(window, "FreshDocumentApply"));
            byte[] canonical = NativeId65FreshDocumentCodec.SerializeCanonical(EditedIntent());
            Check(window.CurrentIntent != null && NativeId65FreshDocumentCodec.SerializeCanonical(window.CurrentIntent).AsSpan().SequenceEqual(canonical) &&
                ReferenceEquals(window.Preview.Intent, window.CurrentIntent), "all real form fields drive the edited immutable intent and schematic");
            Check(!File.Exists(Path.Combine(projectRoot, NativeId65FreshDocumentStore.FileName)) && !Find<Button>(window, "FreshDocumentResearch").IsEnabled,
                "Apply alone neither saves nor enables research publication");
            Click(Find<Button>(window, "FreshDocumentSave"));
            NativeId65FreshDocumentSnapshot saved = NativeId65FreshDocumentStore.Capture(actual.Project);
            Check(saved.Exists && saved.Intent != null && saved.RetainedRecoveryDirectory.Length == 0 &&
                NativeId65FreshDocumentCodec.SerializeCanonical(saved.Intent).AsSpan().SequenceEqual(canonical) && !window.HasUnsavedChanges && !window.HasUnappliedFields,
                "real Save returns the exact persisted applied version");
            sentinels.Add(saved.Path, saved.ContentSha256!);
            // Let the real startup's read-only old-Lab admission check finish;
            // do not replace its validator or manufacture an unlocked owner.
            WaitTask(main.CurrentId65BlankLabCatalogValidationTaskForTesting, TimeSpan.FromSeconds(30), "actual startup old-Lab admission check");
            Check(window.ResearchWorkForTesting == null && window.ResearchSourcePathsForTesting == null, "no fake work or source provider installed");
            int pickerCalls = 0;
            window.ResearchOutputPickerForTesting = token =>
            {
                token.ThrowIfCancellationRequested(); RequireAbsent(candidate); pickerCalls++;
                return Task.FromResult<string?>(candidate);
            };
            Click(Find<Button>(window, "FreshDocumentResearch"));
            Task task = window.ResearchTaskForTesting ?? throw new InvalidOperationException("Actual research button did not start the production task.");
            Check(window.ResearchBusyForTesting && !Find<Button>(window, "FreshDocumentSave").IsEnabled &&
                !Find<Button>(window, "FreshDocumentApply").IsEnabled, "real production task owns and locks the saved form");
            WaitTask(task, TimeSpan.FromMinutes(20), "actual UI compilation and publication");
            NativeId65FreshDiscResearchPublication result = window.ResearchPublicationForTesting
                ?? throw new InvalidOperationException("No verified UI publication was returned. Preserve the run and inspect status: " + Find<TextBlock>(window, "FreshDocumentStatus").Text);
            Check(pickerCalls == 1 && !window.ResearchBusyForTesting && window.ResearchWorkForTesting == null && window.ResearchSourcePathsForTesting == null,
                "one output picker and the true production worker completed");
            RequireSnapshot(saved, window.ResearchSnapshotForTesting ?? throw new InvalidOperationException("No actual captured snapshot retained."));
            RequireSnapshot(saved, NativeId65FreshDocumentStore.Capture(actual.Project));
            Check(result.ImageSha256 == EditedHash && result.ChangedRawSectors == 1253 && result.WholeImageReadback && result.ResearchOnly &&
                !result.RuntimeVerified && !result.NormalCreateBinApproved && !result.CompleteNativeDependencyClosureProven && result.RetainedRecoveryDirectory.Length == 0,
                "real result matches independent edited image golden with all runtime gates false");
            RequireArtifactSet(candidate, result);
            Check(HashFile(result.ImagePath, ImageLength, ImageLength) == EditedHash, "independent full image bytes match the known edited golden");
            var artifactHashes = new Dictionary<string, string>
            {
                [result.ImagePath] = EditedHash, [result.CuePath] = HashFile(result.CuePath, 65536),
                [result.ReceiptPath] = HashFile(result.ReceiptPath, 256 * 1024)
            };
            string status = Find<TextBlock>(window, "FreshDocumentStatus").Text ?? "";
            Check(status.Contains("Published captured version", StringComparison.Ordinal) && status.Contains("Gameplay is not certified", StringComparison.Ordinal) &&
                !status.Contains("changed", StringComparison.OrdinalIgnoreCase), "actual status reports byte-verified captured-version research, not gameplay");

            // Independent recapture, not the worker's retained internal context
            // and not receipt-derived authority. A NEW process repeats this via
            // the generic reviewed CLI using the printed project identity.
            var compilation = NativeId65FreshDocumentCompilation.CaptureAsync(invocation.Image, saved.Intent!);
            WaitTask(compilation, TimeSpan.FromMinutes(10), "independent source compilation");
            var planTask = NativeId65FreshDocumentResearchPlan.CaptureAsync(invocation.Image, invocation.Cue, compilation.Result);
            WaitTask(planTask, TimeSpan.FromMinutes(10), "independent physical expectation");
            var plan = planTask.Result;
            var context = NativeId65FreshSavedDocumentExportContext.Capture(NativeId65FreshDocumentStore.Capture(actual.Project), plan);
            var readback = NativeId65FreshDocumentResearchPublisher.VerifyAsync(context, candidate);
            WaitTask(readback, TimeSpan.FromMinutes(10), "independent full publication readback");
            Check(readback.Result == result && plan.ExpectedImageSha256 == EditedHash && plan.PlanSha256 == result.PlanSha256,
                "actual newly issued plan and context reproduce the complete publication result");
            RequireArtifactSet(candidate, result);
            foreach (var artifact in artifactHashes) Check(HashFile(artifact.Key, ImageLength) == artifact.Value, "all three actual artifact owners unchanged across independent readback");
            RequireSnapshot(saved, NativeId65FreshDocumentStore.Capture(actual.Project));
            foreach (string lease in new[] { ".native-id65-fresh-document.writer.lock", ".export-publication.lock" }) RequireAbsent(Path.Combine(projectRoot, lease));
            Check(!Directory.EnumerateFileSystemEntries(invocation.RunDirectory).Any(p => Path.GetFileName(p).Contains("stage", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(p).Contains("writer.lock", StringComparison.OrdinalIgnoreCase)), "no run-level stage or lease residue");
            using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No final headless UI frame rendered."))
                frame.Save(Path.Combine(invocation.RunDirectory, "fresh-document-real-publication.png"));
            completionEvidence = JsonSerializer.Serialize(new
            {
                projectRoot, projectId = actual.Project.ProjectId, saved.ManifestSha256, saved.ContentSha256,
                canonicalSha256 = Convert.ToHexStringLower(SHA256.HashData(canonical)), candidate, result.ImagePath, result.CuePath, result.ReceiptPath,
                result.PlanSha256, result.ImageSha256, result.ChangedRawSectors, artifactHashes, originalBinSha256 = OriginalHash, originalCueSha256 = cueHash,
                actualUiApplySave = true, fakeWork = false, fakeSource = false, separateProcessReadbackExecuted = false,
                postcommitCancellationExecuted = false, runtimeVerified = false
            });
            complete = true;
        }
        finally
        {
            // Never erase a successful/incomplete publication or infer cleanup
            // ownership. A timed-out operation also keeps its windows and run.
            try
            {
                if (window?.ResearchBusyForTesting != true)
                {
                    window?.Close(); main?.Close(); Dispatcher.UIThread.RunJobs();
                }
                if (sentinels != null)
                    foreach (var owner in sentinels) Check(HashFile(owner.Key, 256L * 1024 * 1024, permitEmpty: true) == owner.Value,
                        "manifest/document/old Lab/source setting/retail cache/private startup owner unchanged after the operation and close");
            }
            finally
            {
                Environment.CurrentDirectory = previousDirectory;
                Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, previousWorkspace);
                Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable, previousRelease);
                Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", previousDiagnostics);
            }
            Check(HashFile(invocation.Image, ImageLength, ImageLength) == OriginalHash && HashFile(invocation.Cue, 65536) == cueHash, "original BIN and CUE unchanged after the actual UI operation");
            Console.WriteLine($"FRESH_DOCUMENT_REAL_UI_RETAINED run={invocation.RunDirectory} complete={complete} noCleanupAttempted=true");
        }
        // Only emit the success marker after the post-operation source checks
        // above have passed. Exit failure must never leave an early OK marker.
        if (complete) Console.WriteLine($"FRESH_DOCUMENT_REAL_UI_PUBLICATION_OK checks={_checks} " + completionEvidence);
    }

    private static NativeId65FreshDocumentIntent EditedIntent() => NativeId65FreshDocumentIntent.Create(8192, 8192, 8704, 2048, 2048,
        9216, 9216, 96, new(72, 132, 120), new(44, 66, 100), 2);
    private static void RequireSnapshot(NativeId65FreshDocumentSnapshot expected, NativeId65FreshDocumentSnapshot actual) => Check(
        actual.RootPath == expected.RootPath && actual.ProjectId == expected.ProjectId && actual.ManifestSha256 == expected.ManifestSha256 &&
        actual.ContentSha256 == expected.ContentSha256 && actual.RetainedRecoveryDirectory.Length == 0, "exact actual saved root/project/raw lineage retained");
    private static void RequireArtifactSet(string root, NativeId65FreshDiscResearchPublication result)
    {
        Check(Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).SequenceEqual(
            new[] { ImageName, CueName, ReceiptName }.Order(StringComparer.Ordinal)), "exact three output companion owners, no stage/lease/recovery residue");
        Check(result.OutputDirectory == root && result.ImagePath == Path.Combine(root, ImageName) && result.CuePath == Path.Combine(root, CueName) &&
            result.ReceiptPath == Path.Combine(root, ReceiptName), "publication returns only the selected absent candidate directory");
        foreach (string path in new[] { result.ImagePath, result.CuePath, result.ReceiptPath })
            Check(File.Exists(path) && new FileInfo(path).LinkTarget == null, "artifact is a regular non-link file");
    }
    private static string HashFile(string path, long maximum, long? exact = null, bool permitEmpty = false)
    {
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long declared = source.Length;
        if (declared < 0 || (!permitEmpty && declared == 0) || declared > maximum || (exact.HasValue && declared != exact.Value) || source.Position != 0)
            throw new InvalidDataException("Unexpected bounded owner length: " + path);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536]; long count = 0;
        while (count < declared)
        {
            int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, declared - count));
            if (read <= 0) throw new EndOfStreamException(path);
            sha.AppendData(buffer, 0, read); count += read;
        }
        if (source.Position != declared || source.ReadByte() != -1 || source.Length != declared || source.Position != declared)
            throw new InvalidDataException("Owner changed length/position or had extra EOF bytes: " + path);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
    private static void RequireAbsent(string path)
    {
        if (File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("An explicit fixture/candidate target must be absent, including links: " + path);
    }
    private static void RefuseLinkedAncestors(string path)
    {
        for (string? cursor = Path.GetFullPath(path); cursor != null; cursor = Path.GetDirectoryName(cursor))
            if (new FileInfo(cursor).LinkTarget != null || new DirectoryInfo(cursor).LinkTarget != null)
                throw new InvalidDataException("Explicit source/fixture paths must not traverse links: " + cursor);
    }
    private static bool Nested(string child, string parent) => SamePath(child, parent) || Path.GetFullPath(child).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);
    private static bool EqualSwitch(string? actual, string expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    private static T Find<T>(Control root, string name) where T : Control => root.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Click(Button button) { Check(button.IsEnabled, "actual enabled button " + button.Name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    private static void WaitTask(Task task, TimeSpan limit, string operation) { WaitUntil(() => task.IsCompleted, limit, operation); task.GetAwaiter().GetResult(); }
    private static void WaitUntil(Func<bool> condition, TimeSpan limit, string operation)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < limit) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        if (!condition()) throw new TimeoutException(operation + " is not confirmed complete. Preserve this run and inspect any ongoing operation/artifacts; do not retry this folder.");
        Dispatcher.UIThread.RunJobs();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); _checks++; }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { _checks++; return; } throw new InvalidOperationException("Invalid CLI arguments were accepted."); }
}
