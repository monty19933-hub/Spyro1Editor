// Tests-first UI cases. No App/Core types are implemented by this file.
// Simulated worker/publication results test UI state, not native authority.
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class NativeFreshDocumentResearchUiCases
{
    private static int _checks;

    internal static void Run()
    {
        _checks = 0;
        RefusesAbsentUnsavedAndUnapplied();
        RefusesStaleRawSnapshot();
        CapturesActualSavedObjectAndCanonicalIntent();
        BusyGuardsAndKeyboardClose();
        RechecksBeforePublishing();
        PickerAndPreparationCancellation();
        PublishedCapturedVersionAdvisories();
        UncertainFailureDoesNotBecomeSuccess();
        Console.WriteLine($"FRESH_NATIVE_DOCUMENT_RESEARCH_UI_OK checks={_checks} sourceRequired=false nativeCompilation=false physicalPublication=false");
    }

    private static void RefusesAbsentUnsavedAndUnapplied()
    {
        using (var f = new Fixture(save: false))
        {
            Check(!f.Button("FreshDocumentResearch").IsEnabled, "absent document has no export action");
            f.Fire("FreshDocumentResearch");
            Check(f.Work.PrepareCalls == 0 && f.PickerCalls == 0, "direct absent action cannot create defaults or start work");
            f.Fire("FreshDocumentNew");
            Check(f.Window.HasUnsavedChanges, "explicit starter fixture really is unsaved");
            f.Fire("FreshDocumentResearch");
            Check(f.Work.PrepareCalls == 0 && !File.Exists(f.DocumentPath), "unsaved starter is not silently saved/exported");
        }
        foreach (string text in new[] { "78", "invalid-heading" })
        {
            using var f = new Fixture();
            var intent = f.Window.CurrentIntent;
            f.Field("FreshHeading").Text = text;
            Pump();
            Check(f.Window.HasUnappliedFields, "unapplied fixture is genuinely dirty");
            f.Fire("FreshDocumentResearch");
            Check(f.Work.PrepareCalls == 0 && f.PickerCalls == 0 && ReferenceEquals(f.Window.CurrentIntent, intent) &&
                f.Field("FreshHeading").Text == text, "valid or invalid unapplied text is neither applied nor discarded by export");
            f.AssertSavedBytes();
        }
        using (var f = new Fixture())
        {
            f.Field("FreshHeading").Text = "78";
            f.Fire("FreshDocumentApply");
            Check(f.Window.HasUnsavedChanges && !f.Window.HasUnappliedFields, "applied-unsaved fixture is distinct");
            f.Fire("FreshDocumentResearch");
            Check(f.Work.PrepareCalls == 0, "applied but unsaved document is not exported");
            f.AssertSavedBytes();
        }
    }

    private static void RefusesStaleRawSnapshot()
    {
        foreach (string mutation in new[] { "document-whitespace", "manifest-whitespace", "manifest-id", "missing-document" })
        {
            using var f = new Fixture();
            switch (mutation)
            {
                case "document-whitespace": File.AppendAllText(f.DocumentPath, " "); break;
                case "manifest-whitespace": File.AppendAllText(f.Project.ManifestPath, " "); break;
                case "manifest-id": File.WriteAllText(f.Project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"other-id\"}"); break;
                case "missing-document": File.Delete(f.DocumentPath); break;
            }
            byte[]? document = ReadOptional(f.DocumentPath);
            byte[] manifest = File.ReadAllBytes(f.Project.ManifestPath);
            f.Fire("FreshDocumentResearch");
            Check(f.Work.PrepareCalls == 0 && f.PickerCalls == 0, "stale raw snapshot refuses before picker/compile: " + mutation);
            Check(SameOptional(document, ReadOptional(f.DocumentPath)) && manifest.SequenceEqual(File.ReadAllBytes(f.Project.ManifestPath)),
                "stale export never rewrites external document/manifest: " + mutation);
            Check(f.Status.Contains("Reopen", StringComparison.OrdinalIgnoreCase), "stale export offers actual reopen: " + mutation);
        }
    }

    private static void CapturesActualSavedObjectAndCanonicalIntent()
    {
        using var f = new Fixture();
        f.Fire("FreshDocumentResearch");
        Check(f.Window.ResearchBusyForTesting && f.Work.PrepareCalls == 1 && f.Work.Captured != null, "clean saved document starts one preparation");
        var captured = f.Work.Captured!;
        Check(ReferenceEquals(captured, f.Window.ResearchSnapshotForTesting), "worker and window retain the EXACT recaptured Snapshot object");
        Check(captured.RootPath == f.Project.RootPath && captured.ProjectId == f.Project.ProjectId &&
            captured.ManifestSha256 == f.Saved.ManifestSha256 && captured.ContentSha256 == f.Saved.ContentSha256,
            "actual saved raw lineage, not only canonical equality, is preserved");
        Check(f.Work.Canonical!.SequenceEqual(f.Canonical) &&
            NativeId65FreshDocumentCodec.SerializeCanonical(captured.Intent!).SequenceEqual(f.Canonical),
            "all edited floor/spawn/heading/RGB/music fields reach preparation unchanged; no starter recipe");
        Check(f.Work.CapturedImage == f.ImagePath && f.Work.CapturedCue == Path.Combine(f.Root, "source-ui-placeholder.cue"),
            "preparation receives captured selected source paths, not hard-coded research sources");
        f.Work.PrepareGate.SetResult();
        Pump();
        Check(f.Work.PublishCalls == 1 && ReferenceEquals(f.Work.Captured, captured), "same prepared snapshot continues to publication");
        f.Work.PublishGate.SetResult();
        Finish(f.Window.ResearchTaskForTesting!);
        Check(ReferenceEquals(f.Window.ResearchSnapshotForTesting, captured) &&
            ReferenceEquals(f.Window.ResearchPublicationForTesting, f.Work.Returned), "result retains captured Snapshot and actual returned result objects");
        Check(f.Status.Contains("research", StringComparison.OrdinalIgnoreCase) &&
            !f.Status.Contains("runtime verified", StringComparison.OrdinalIgnoreCase), "UI success remains research-only");
        f.AssertSavedBytes();
        f.AssertFakeArtifacts();
    }

    private static void BusyGuardsAndKeyboardClose()
    {
        using var f = new Fixture();
        // A real saved edit makes Undo capable of changing state if not blocked.
        f.Field("FreshHeading").Text = "78";
        f.Fire("FreshDocumentSave");
        Check(f.Button("FreshDocumentUndo").IsEnabled, "busy fixture has meaningful prior history");
        byte[] saved = File.ReadAllBytes(f.DocumentPath);
        var applied = f.Window.CurrentIntent;
        f.Fire("FreshDocumentResearch");
        foreach (string action in new[] { "New", "Apply", "Save", "Undo", "Redo", "Reopen", "Research" })
            Check(!f.Button("FreshDocument" + action).IsEnabled, "busy disables " + action);
        Check(f.Button("FreshDocumentCancelResearch").IsEnabled, "only cooperative cancellation remains enabled");

        // Programmatic valid text is a hostile direct-handler control, not a user
        // editing an enabled field. Without the busy check Save would persist it.
        f.Field("FreshHeading").Text = "79";
        foreach (string action in new[] { "Apply", "Save", "Undo", "Redo", "Reopen", "New", "Research" })
            f.Fire("FreshDocument" + action);
        var key = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent, Key = Key.S,
            KeyModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
        };
        f.Window.RaiseEvent(key);
        Pump();
        Check(key.Handled, "busy command-S is consumed, not bubbled to parent Save");
        Check(ReferenceEquals(applied, f.Window.CurrentIntent) && f.Field("FreshHeading").Text == "79" &&
            saved.SequenceEqual(File.ReadAllBytes(f.DocumentPath)) && f.Work.PrepareCalls == 1 && f.PickerCalls == 1,
            "direct handlers cannot alter saved/applied state or duplicate work while busy");
        f.Window.Close();
        f.Window.Close();
        Pump();
        Check(f.Window.IsVisible && f.DecisionCalls == 0, "busy close is blocked before dirty/clean decision return");
        f.Fire("FreshDocumentCancelResearch");
        Finish(f.Window.ResearchTaskForTesting!);
        Check(!f.Window.ResearchBusyForTesting && f.Work.PublishCalls == 0 && f.Field("FreshHeading").Text == "79",
            "cancellation releases busy only on completed operation and retains draft");
        Check(saved.SequenceEqual(File.ReadAllBytes(f.DocumentPath)), "cancellation leaves saved history state unchanged");

        using (var clean = new Fixture())
        {
            clean.Fire("FreshDocumentResearch");
            Check(!clean.Window.HasUnsavedChanges, "clean-close control has no dirty fallback");
            clean.Window.Close(); Pump();
            Check(clean.Window.IsVisible && clean.Window.ResearchBusyForTesting && clean.DecisionCalls == 0,
                "busy guard precedes OnClosing's clean-return path");
        }
        using (var redo = new Fixture())
        {
            redo.Field("FreshHeading").Text = "78";
            redo.Fire("FreshDocumentApply");
            redo.Fire("FreshDocumentUndo");
            var savedIntent = redo.Window.CurrentIntent;
            Check(!redo.Window.HasUnsavedChanges && redo.Button("FreshDocumentRedo").IsEnabled,
                "redo fixture is saved/clean but has meaningful forward history");
            redo.Fire("FreshDocumentResearch");
            redo.Fire("FreshDocumentRedo");
            Check(ReferenceEquals(savedIntent, redo.Window.CurrentIntent) && !redo.Window.HasUnsavedChanges,
                "direct Redo cannot consume armed history while export is busy");
            redo.AssertSavedBytes();
            redo.Fire("FreshDocumentCancelResearch");
            Finish(redo.Window.ResearchTaskForTesting!);
            Check(redo.Button("FreshDocumentRedo").IsEnabled, "cancel preserves forward history");
            redo.Fire("FreshDocumentRedo");
            Check(redo.Window.CurrentIntent!.Heading == 78 && redo.Window.HasUnsavedChanges,
                "post-cancel Redo proves the guarded history was real, not an empty stack");
        }
    }

    private static void RechecksBeforePublishing()
    {
        foreach (string mutation in new[] { "project-id", "project-root", "workspace", "source", "document", "manifest" })
        {
            using var f = new Fixture();
            f.Fire("FreshDocumentResearch");
            var captured = f.Work.Captured;
            switch (mutation)
            {
                case "project-id": f.ActiveProject = new(f.Root, "different-project-id"); break;
                case "project-root": f.ActiveProject = new(Path.Combine(f.Root, "different-root"), f.Project.ProjectId); break;
                case "workspace": f.ActiveWorkspace = Path.Combine(f.Root, "different-workspace"); break;
                case "source": f.ImagePath = Path.Combine(f.Root, "different-source.bin"); break;
                case "document": File.AppendAllText(f.DocumentPath, " "); break;
                case "manifest": File.AppendAllText(f.Project.ManifestPath, " "); break;
            }
            f.Work.PrepareGate.SetResult();
            Finish(f.Window.ResearchTaskForTesting!);
            Check(f.Work.PublishCalls == 0 && !Directory.Exists(f.Output), "after-prepare change refuses before publication: " + mutation);
            Check(ReferenceEquals(f.Window.ResearchSnapshotForTesting, captured) && f.Window.ResearchPublicationForTesting == null,
                "refusal retains captured lineage without inventing a result: " + mutation);
        }
        foreach (string mutation in new[] { "project-id", "document", "manifest", "source" })
        {
            using var picker = new Fixture();
            var pending = new TaskCompletionSource<string?>();
            picker.Window.ResearchOutputPickerForTesting = _ => pending.Task;
            picker.Fire("FreshDocumentResearch");
            Check(picker.Window.ResearchBusyForTesting && picker.Work.PrepareCalls == 0, "busy begins before output picker await");
            switch (mutation)
            {
                case "project-id": picker.ActiveProject = new(picker.Root, "different-project-id"); break;
                case "document": File.AppendAllText(picker.DocumentPath, " "); break;
                case "manifest": File.AppendAllText(picker.Project.ManifestPath, " "); break;
                case "source": picker.ImagePath = Path.Combine(picker.Root, "different-selection.bin"); break;
            }
            pending.SetResult(picker.Output);
            Finish(picker.Window.ResearchTaskForTesting!);
            Check(picker.Work.PrepareCalls == 0 && picker.Work.PublishCalls == 0, "captured context is rechecked after picker before compile: " + mutation);
        }
    }

    private static void PickerAndPreparationCancellation()
    {
        using (var f = new Fixture())
        {
            f.Window.ResearchOutputPickerForTesting = _ => Task.FromResult<string?>(null);
            f.Fire("FreshDocumentResearch");
            Finish(f.Window.ResearchTaskForTesting!);
            Check(!f.Window.ResearchBusyForTesting && f.Work.PrepareCalls == 0 && !Directory.Exists(f.Output), "picker cancel is not a publication");
            f.AssertSavedBytes();
        }
        using (var f = new Fixture())
        {
            f.Work.PrepareFailure = new InvalidDataException("simulated compile refusal");
            f.Fire("FreshDocumentResearch");
            Finish(f.Window.ResearchTaskForTesting!);
            Check(f.Work.PublishCalls == 0 && f.Window.ResearchPublicationForTesting == null &&
                !f.Window.ResearchBusyForTesting && !Directory.Exists(f.Output), "compile failure cannot produce success/output");
            f.AssertSavedBytes();
        }
    }

    private static void PublishedCapturedVersionAdvisories()
    {
        foreach (string change in new[] { "none", "late-cancel", "document", "manifest", "project", "source", "unreadable" })
        {
            using var f = new Fixture();
            f.Fire("FreshDocumentResearch");
            var captured = f.Work.Captured;
            f.Work.PrepareGate.SetResult(); Pump();
            Check(f.Work.PublishCalls == 1, "advisory fixture actually entered simulated publication");
            f.Work.AfterSimulatedCommit = () =>
            {
                switch (change)
                {
                    case "late-cancel": f.Fire("FreshDocumentCancelResearch"); break;
                    case "document": File.AppendAllText(f.DocumentPath, " "); break;
                    case "manifest": File.AppendAllText(f.Project.ManifestPath, " "); break;
                    case "project": f.ActiveProject = new(f.Root, "different-project-id"); break;
                    case "source": f.ImagePath = Path.Combine(f.Root, "postcommit-source-selection.bin"); break;
                    case "unreadable": File.WriteAllText(f.DocumentPath, "malformed external document"); break;
                }
            };
            f.Work.PublishGate.SetResult();
            Finish(f.Window.ResearchTaskForTesting!);
            Check(ReferenceEquals(f.Window.ResearchPublicationForTesting, f.Work.Returned) &&
                ReferenceEquals(f.Window.ResearchSnapshotForTesting, captured), "committed result keeps captured object lineage: " + change);
            Check(!f.Status.Contains("aborted", StringComparison.OrdinalIgnoreCase) &&
                !f.Status.Contains("no output", StringComparison.OrdinalIgnoreCase), "known success never becomes aborted/no-output: " + change);
            if (change is "document" or "manifest" or "project" or "source" or "unreadable")
                Check(f.Status.Contains("captured version", StringComparison.OrdinalIgnoreCase) &&
                    (f.Status.Contains("changed", StringComparison.OrdinalIgnoreCase) || f.Status.Contains("could not", StringComparison.OrdinalIgnoreCase)),
                    "postcommit saved/project change is a precise advisory: " + change);
            Check(NativeId65FreshDocumentCodec.SerializeCanonical(f.Window.CurrentIntent!).SequenceEqual(f.Canonical),
                "postcommit observation does not silently reopen/adopt external content: " + change);
            f.AssertFakeArtifacts();
        }
    }

    private static void UncertainFailureDoesNotBecomeSuccess()
    {
        using var f = new Fixture();
        f.Work.PublishFailure = new IOException("committed according to arbitrary exception text; not a typed Core outcome");
        f.Fire("FreshDocumentResearch");
        f.Work.PrepareGate.SetResult(); Pump();
        f.Work.PublishGate.SetResult();
        Finish(f.Window.ResearchTaskForTesting!);
        Check(f.Window.ResearchPublicationForTesting == null, "message text cannot grant known committed success");
        Check(!f.Status.Contains("no output", StringComparison.OrdinalIgnoreCase) &&
            !f.Status.StartsWith("Published captured version", StringComparison.OrdinalIgnoreCase),
            "ordinary failure is honest about unknown outcome; not parsed as committed or cleaned");
        f.AssertFakeArtifacts();
    }

    // Called ONLY by the existing isolated source-aware shell, never Run().
    // Exact wrappers below are proposed App hooks around real private commands.
    internal static async Task RunMainWindowAsync(MainWindow main)
    {
        int beforeChecks = _checks;
        EditorShellSessionSnapshot before = main.CaptureEditorShellSessionSnapshotForTesting();
        string sourceSetting = Path.Combine(before.Workspace.RootPath, "_local", "settings", "source-disc.json");
        byte[]? sourceBefore = ReadOptional(sourceSetting);
        string fixture = Directory.CreateTempSubdirectory("spyro-fresh-picker-guard-").FullName;
        string alternate = Path.Combine(fixture, "not-a-native-image.bin");
        File.WriteAllText(alternate, "UI stale-picker refusal canary; not a native disc");
        try
        {
            foreach (bool returnAfterLease in new[] { false, true })
            {
                int pickerCalls = 0;
                var picker = new TaskCompletionSource<string?>();
                main.SourceDiscFilePickerOverrideForTesting = () => { pickerCalls++; return picker.Task; };
                Task priorPicker = main.ChooseSourceDiscForFreshResearchTestingAsync();
                Check(pickerCalls == 1, "source picker was genuinely outstanding before fresh export");
                using (main.BeginFreshDocumentResearchForTesting())
                {
                    if (!returnAfterLease)
                    {
                        picker.SetResult(alternate);
                        await priorPicker;
                    }
                    var after = main.CaptureEditorShellSessionSnapshotForTesting();
                    Check(before == after && SameOptional(sourceBefore, ReadOptional(sourceSetting)),
                        "busy phase preserves actual source setting and existing shell snapshot");
                    await main.ChooseSourceDiscForFreshResearchTestingAsync();
                    Check(pickerCalls == 1, "busy source chooser cannot open a new picker");
                    Check(!main.TryWorkspaceTransitionForFreshResearchTesting() && !main.TryRegularSaveForFreshResearchTesting(),
                        "fresh busy owner fences real workspace/save begin paths without a retail save lease");
                    main.Close(); Pump();
                    Check(main.IsVisible, "owner window cannot close during fresh research");
                    Check(main.CaptureEditorShellSessionSnapshotForTesting() == after && SameOptional(sourceBefore, ReadOptional(sourceSetting)),
                        "blocked operations preserve shell and source state");
                }
                if (returnAfterLease)
                {
                    picker.SetResult(alternate);
                    await priorPicker;
                }
                Check(main.CaptureEditorShellSessionSnapshotForTesting() == before && SameOptional(sourceBefore, ReadOptional(sourceSetting)),
                    "old source picker remains stale " + (returnAfterLease ? "AFTER busy lease disposal" : "during busy lease"));
            }
        }
        finally
        {
            main.SourceDiscFilePickerOverrideForTesting = null;
            Directory.Delete(fixture, recursive: true);
        }
        Console.WriteLine($"FRESH_NATIVE_DOCUMENT_RESEARCH_SHELL_OK checks={_checks - beforeChecks} publication=false");
    }

    private sealed class FakeWork : INativeFreshDocumentResearchUiWork
    {
        internal readonly TaskCompletionSource PrepareGate = new();
        internal readonly TaskCompletionSource PublishGate = new();
        internal int PrepareCalls, PublishCalls;
        internal NativeId65FreshDocumentSnapshot? Captured;
        internal byte[]? Canonical;
        internal string? CapturedImage, CapturedCue;
        internal Exception? PrepareFailure, PublishFailure;
        internal Action? AfterSimulatedCommit;
        internal NativeId65FreshDiscResearchPublication? Returned;

        public async Task PrepareAsync(NativeId65FreshDocumentSnapshot captured, string image, string cue, CancellationToken token)
        {
            PrepareCalls++;
            Captured = captured;
            CapturedImage = image; CapturedCue = cue;
            Canonical = NativeId65FreshDocumentCodec.SerializeCanonical(captured.Intent!);
            if (PrepareFailure != null) throw PrepareFailure;
            await PrepareGate.Task.WaitAsync(token);
        }

        public async Task<NativeId65FreshDiscResearchPublication> PublishAsync(string output, CancellationToken token)
        {
            PublishCalls++;
            token.ThrowIfCancellationRequested();
            await PublishGate.Task; // Deliberately models a committed noncancellable tail, NOT backend proof.
            Directory.CreateDirectory(output);
            string image = Path.Combine(output, "ui-image-sentinel.txt");
            string cue = Path.Combine(output, "ui-cue-sentinel.txt");
            string receipt = Path.Combine(output, "ui-receipt-sentinel.txt");
            foreach (string path in new[] { image, cue, receipt }) File.WriteAllText(path, "UI outcome fixture; not native content");
            AfterSimulatedCommit?.Invoke();
            if (PublishFailure != null) throw PublishFailure;
            Returned = new(output, image, cue, receipt, "ui-test-not-a-disc", "ui-test-not-a-plan", 0);
            return Returned;
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Directory.CreateTempSubdirectory("spyro-fresh-research-ui-").FullName;
        internal readonly EditorProjectLayout Project;
        internal readonly NativeId65FreshDocumentSnapshot Saved;
        internal readonly NativeFreshDocumentWindow Window;
        internal readonly FakeWork Work = new();
        internal readonly byte[] Canonical;
        internal readonly byte[] Manifest;
        internal readonly Dictionary<string, byte[]> Sentinels;
        internal EditorProjectLayout? ActiveProject;
        internal string ActiveWorkspace;
        internal int PickerCalls, DecisionCalls;
        internal string ImagePath;
        internal string DocumentPath => Path.Combine(Root, NativeId65FreshDocumentStore.FileName);
        internal string Output => Path.Combine(Root, "ui-outcome-only");
        internal string Status => Find<TextBlock>(Window, "FreshDocumentStatus").Text ?? "";

        internal Fixture(bool save = true)
        {
            Project = new(Root, "fresh-research-ui");
            ActiveProject = Project;
            ActiveWorkspace = Root;
            File.WriteAllText(Project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"fresh-research-ui\",\"displayName\":\"raw manifest sentinel\"}");
            Manifest = File.ReadAllBytes(Project.ManifestPath);
            string legacy = Path.Combine(Root, "unusedlevel65blank-authoring-document-v1.json");
            string source = Path.Combine(Root, "source-disc.json");
            File.WriteAllText(legacy, "old Lab sentinel");
            File.WriteAllText(source, "source setting sentinel");
            Sentinels = new[] { legacy, source }.ToDictionary(p => p, File.ReadAllBytes);
            ImagePath = Path.Combine(Root, "source-ui-placeholder.bin");
            string cue = Path.Combine(Root, "source-ui-placeholder.cue");
            File.WriteAllText(ImagePath, "UI source-selection fixture, not a native disc");
            File.WriteAllText(cue, "UI source-selection fixture, not a native cue");
            // Tiny fixture placeholders only. FakeWork performs no native reads.
            Sentinels.Add(ImagePath, File.ReadAllBytes(ImagePath));
            Sentinels.Add(cue, File.ReadAllBytes(cue));
            var intent = NativeId65FreshDocumentIntent.Create(8464, 8448, 8720, 1536, 1024, 9000, 8800, 77,
                new(190, 92, 52, 255), new(33, 49, 67, 255), 20);
            Canonical = NativeId65FreshDocumentCodec.SerializeCanonical(intent);
            Saved = NativeId65FreshDocumentStore.Capture(Project);
            if (save) Saved = NativeId65FreshDocumentStore.Save(Saved, intent);
            Window = new(Project, () => MainWindow.IsNativeFreshDocumentProjectCurrent(Project, ActiveProject, ActiveWorkspace));
            Window.ResearchWorkForTesting = Work;
            Window.ResearchSourcePathsForTesting = () => (ImagePath, cue);
            Window.ResearchOutputPickerForTesting = _ => { PickerCalls++; return Task.FromResult<string?>(Output); };
            Window.DecisionForTesting = _ => { DecisionCalls++; return Task.FromResult(FreshDocumentDecision.Discard); };
            Window.Show(); Pump();
        }

        internal Button Button(string name) => Find<Button>(Window, name);
        internal TextBox Field(string name) => Find<TextBox>(Window, name);
        internal void Fire(string name)
        {
            Button button = Button(name);
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, button));
            Pump();
        }
        internal void AssertSavedBytes() => Check(File.ReadAllBytes(DocumentPath).SequenceEqual(Canonical) &&
            File.ReadAllBytes(Project.ManifestPath).SequenceEqual(Manifest), "raw saved document and complete manifest remain exact");
        internal void AssertFakeArtifacts()
        {
            string[] names = Directory.GetFiles(Output).Select(Path.GetFileName).Order().ToArray()!;
            Check(names.SequenceEqual(new[] { "ui-cue-sentinel.txt", "ui-image-sentinel.txt", "ui-receipt-sentinel.txt" }),
                "UI preserves all three simulated output owners without adding/deleting files");
            foreach (string path in Directory.GetFiles(Output))
                Check(File.ReadAllText(path) == "UI outcome fixture; not native content", "simulated owner bytes preserved");
        }
        public void Dispose()
        {
            Work.PrepareGate.TrySetCanceled(); Work.PublishGate.TrySetCanceled();
            if (Window.ResearchTaskForTesting is { } task) Finish(task);
            Window.Close(); Pump();
            foreach (var pair in Sentinels) Check(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value), "old Lab/source setting sentinel unchanged");
            Directory.Delete(Root, recursive: true);
        }
    }

    private static void Finish(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5)) { Pump(); Thread.Sleep(1); }
        Check(task.IsCompleted, "bounded UI operation completion");
        task.GetAwaiter().GetResult(); // Window handles worker failures into its status; don't suppress unrelated failures.
        Pump();
    }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static byte[]? ReadOptional(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
    private static bool SameOptional(byte[]? a, byte[]? b) => a == null ? b == null : b != null && a.SequenceEqual(b);
    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException("Fresh research UI: " + label); _checks++; }
    private static T Find<T>(Control root, string name) where T : Control
    {
        T[] matches = root.GetLogicalDescendants().OfType<T>().Where(c => c.Name == name).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"Expected one {name}, found {matches.Length}.");
    }
}
