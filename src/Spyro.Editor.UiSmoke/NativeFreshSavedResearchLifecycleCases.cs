using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = NativeFreshMaterialInspectionUiFixture;

// Private source-free failure/delay drafts. No successful Publication is fabricated.
internal static class NativeFreshSavedResearchLifecycleCases
{
    internal static void Run(string parent)
    {
        int checks = 0;
        foreach (string stage in new[] { "picker", "prepare" })
        foreach (string change in new[] { "none", "raw-aba", "raw", "native-raw-aba", "native-raw", "source", "project", "disk", "manifest", "session", "snapshot", "applied", "close", "cancel", "prepare-refusal", "owner-release" })
        {
            var fixture = new Fixture(parent, stage + "-" + change);
            List<Exception> failures = [];
            var picker = new TaskCompletionSource<string?>();
            var work = new FailureWork();
            Task? task = null;
            var original = fixture.Window.CurrentAggregateIntent;
            try
            {
                fixture.Window.SavedResearchWorkForTesting = work;
                fixture.Window.ResearchOutputPickerForTesting = _ => stage == "picker" ? picker.Task : Task.FromResult<string?>(fixture.Output);
                fixture.Fire("FreshDocumentResearch");
                task = fixture.Window.ResearchTaskForTesting ?? throw new InvalidDataException("Missing actual research task.");
                Check(fixture.Window.ResearchBusyForTesting && fixture.Acquired == 1 && fixture.Released == 0, "Owner held across await.");
                if (stage == "prepare") Check(work.Snapshot != null && ReferenceEquals(work.Snapshot, fixture.Window.SavedResearchSnapshotForTesting),
                    "Actual retained Snapshot delivered to preparation, not applied object.");
                foreach (string button in new[] { "FreshDocumentApply", "FreshDocumentSave", "FreshDocumentUndo", "FreshDocumentRedo",
                    "FreshDocumentReopen", "FreshWizardAdd", "FreshWizardRemove", "FreshWizardMoveUp", "FreshWizardMoveDown", "FreshMaterialsInspect" })
                {
                    Check(!fixture.Find<Button>(button).IsEnabled, "Busy UI control disabled: " + button);
                    fixture.Fire(button); // Direct routed events must remain guarded.
                }
                fixture.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
                Check(fixture.Window.CurrentAggregateIntent == original && fixture.Find<ComboBox>("FreshDocumentMode").SelectedItem as string == "Level aggregate",
                    "Busy direct mode/command calls retain aggregate and raw form.");
                try { fixture.Window.SavedResearchWorkForTesting = new FailureWork(); throw new InvalidDataException("Busy worker replacement accepted."); }
                catch (InvalidOperationException) { checks++; }
                switch (change)
                {
                    case "raw-aba": fixture.Set("FreshHeading", "0128"); fixture.Set("FreshHeading", "128"); break;
                    case "raw": fixture.Set("FreshHeading", "129"); break;
                    case "native-raw-aba": fixture.Set("FreshNativeMobyX", "1"); fixture.Set("FreshNativeMobyX", ""); break;
                    case "native-raw": fixture.Set("FreshNativeMobyX", "1"); break;
                    case "source": fixture.Source = (fixture.Source.Item1 + ".different", fixture.Source.Item2); break;
                    case "project": fixture.Current = false; break;
                    case "disk": fixture.ChangeSavedRaw(); break;
                    case "manifest": fixture.ChangeManifest(); break;
                    case "session": fixture.ReplaceActualSession(); break;
                    case "snapshot": fixture.CurrentSession.Save(); break;
                    case "applied": fixture.CurrentSession.Apply(fixture.CurrentSession.CurrentIntent!.WithContent(
                        Spyro.Editor.Core.Editing.NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 129,
                            new(80, 128, 112), new(128, 96, 64), 20), [])); break;
                    case "close": fixture.Window.Close(); F.Pump(); Check(fixture.Window.IsVisible && fixture.Window.ResearchBusyForTesting,
                        "Research close defers to settled work and does not auto-close."); break;
                    case "cancel": fixture.Fire("FreshDocumentCancelResearch"); break;
                    case "prepare-refusal": work.RefusePreparation = true; break;
                    case "owner-release": fixture.ReleaseFailure = new IOException("exact-ui-owner-release-simulation"); break;
                }
                var expectedAfterDeliberateChange = fixture.Window.CurrentAggregateIntent;
                picker.TrySetResult(fixture.Output); work.Gate.TrySetResult();
                F.Wait(task);
                Check(!fixture.Window.ResearchBusyForTesting && fixture.Released == 1, "Actual task settled before owner release.");
                Check(ReferenceEquals(expectedAfterDeliberateChange, fixture.Window.CurrentAggregateIntent) && fixture.Window.ResearchPublicationForTesting == null,
                    "No fake publication or applied-state replacement.");
                if (change == "close") Check(fixture.Window.IsVisible, "Research requires a fresh user Close after settlement, preserving old behavior.");
                bool shouldReachPublish = change is "none" or "owner-release";
                Check(work.PublishCalls == (shouldReachPublish ? 1 : 0), "Every stale/cancel fence precedes publication.");
                if (change == "raw-aba") Check(fixture.Find<TextBox>("FreshHeading").Text == "128" && !fixture.Window.HasUnsavedChanges &&
                    fixture.Window.SavedResearchFailureForTesting is InvalidDataException, "Actual A-B-A remains refused via generation despite equal final raw state.");
                if (change == "native-raw-aba") Check(fixture.Find<TextBox>("FreshNativeMobyX").Text == "" && !fixture.Window.HasUnsavedChanges &&
                    fixture.Window.SavedResearchFailureForTesting is InvalidDataException, "Inactive native raw A-B-A remains stale across the old schema2 research ticket.");
                if (change == "owner-release")
                {
                    var leaves = F.Leaves(fixture.Window.SavedResearchFailureForTesting!);
                    Check(leaves.Count == 2 && ReferenceEquals(leaves[0], work.Failure) && ReferenceEquals(leaves[1], fixture.ReleaseFailure),
                        "Simulated UI-work primary then actual UI-owner failure are retained exactly once.");
                    Check(fixture.Status.Contains(work.Failure.Message) && fixture.Status.Contains(fixture.ReleaseFailure!.Message),
                        "Both ordered leaf messages remain visible through accounting wrapper.");
                }
                var simulatedAssertion = new InvalidDataException("failed-test-accounting-control-simulation");
                List<Exception> failedTest = [simulatedAssertion];
                RetainWindowFailure(fixture, failedTest); RetainWindowFailure(fixture, failedTest);
                Check(failedTest.Count == 2 && ReferenceEquals(failedTest[0], simulatedAssertion) &&
                    ReferenceEquals(failedTest[1], fixture.Window.SavedResearchFailureForTesting),
                    "Failed-test accounting preserves assertion primary and actual stored Window tree once by reference.");
                List<Exception> passingTest = [];
                RetainWindowFailure(fixture, passingTest);
                Check(passingTest.Count == 0, "Expected refusal/warning does not turn a passing control into failure.");
                Check(!Directory.Exists(fixture.Output) && !File.Exists(fixture.Output), "Failure/delay tests never create native output.");
            }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                // Always release suspended fake delivery first, then drain the
                // actual UI task before Close or verifying the acquired owner.
                picker.TrySetResult(fixture.Output); work.Gate.TrySetResult();
                Attempt(() => { if (task != null) F.Drain(task); });
                RetainWindowFailure(fixture, failures);
                Attempt(fixture.Close); Attempt(() => fixture.RequireProtected());
                RetainWindowFailure(fixture, failures);
            }
            F.Throw(failures);
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        }
        Console.WriteLine($"FRESH_SAVED_RESEARCH_LIFECYCLE_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
        void Check(bool value, string message) { if (!value) throw new InvalidDataException(message); checks++; }
    }

    internal static void RetainWindowFailure(Fixture? fixture, List<Exception> failures)
    {
        // Task completion alone does not propagate the Window's stored Core/UI
        // error. Only a failed test retains it, after its earlier assertion or
        // timeout primary, and never twice through an existing exception tree.
        if (failures.Count == 0 || fixture?.Window.SavedResearchFailureForTesting is not { } actual) return;
        if (!failures.SelectMany(Nodes).Any(error => ReferenceEquals(error, actual))) failures.Add(actual);
        static IEnumerable<Exception> Nodes(Exception error)
        {
            yield return error;
            if (error is AggregateException group)
                foreach (var child in group.InnerExceptions)
                    foreach (var node in Nodes(child)) yield return node;
            else if (error.InnerException != null)
                foreach (var node in Nodes(error.InnerException)) yield return node;
        }
    }

    private sealed class FailureWork : INativeFreshSavedLevelResearchUiWork
    {
        internal readonly TaskCompletionSource Gate = new();
        internal readonly IOException Failure = new("exact-no-publication-work-simulation");
        internal NativeFreshLevelDocumentSnapshot? Snapshot;
        internal int PublishCalls;
        internal bool RefusePreparation;
        public async Task PrepareAsync(NativeFreshLevelDocumentSnapshot snapshot, string image, string cue, CancellationToken token)
        { Snapshot = snapshot; await Gate.Task; token.ThrowIfCancellationRequested(); if (RefusePreparation) throw Failure; }
        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string output, CancellationToken token)
        { PublishCalls++; return Task.FromException<NativeId65FreshDiscResearchPublication>(Failure); }
    }

    internal sealed class Fixture
    {
        private readonly EditorProjectLayout project;
        private readonly string documentPath, legacyPath;
        private byte[] manifest, aggregate;
        private readonly byte[] legacy;
        internal readonly NativeFreshDocumentWindow Window;
        internal readonly string Output;
        internal bool Current = true;
        internal int Acquired, Released;
        internal Exception? ReleaseFailure;
        internal Action? OnRelease;
        internal (string, string) Source;
        internal Fixture(string parent, string id, string? reviewedOutput = null)
        {
            string root = Path.Combine(parent, "saved-research-lifecycle-" + id);
            if (Directory.Exists(root) || File.Exists(root) || new FileInfo(root).LinkTarget != null)
                throw new InvalidDataException("A fixture must be newly created; existing files are never reused.");
            Directory.CreateDirectory(root);
            project = new(root, "saved-research-lifecycle-" + id);
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + project.ProjectId + "\"}");
            var floor = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 128,
                new(80, 128, 112), new(128, 96, 64), 20);
            var old = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), floor);
            var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project),
                NativeFreshLevelDocumentIntent.Create(floor, []).WithFloorMaterials(F.Pair(14, 1, 70, 1)));
            documentPath = saved.Path; legacyPath = old.Path;
            manifest = File.ReadAllBytes(project.ManifestPath); aggregate = File.ReadAllBytes(documentPath); legacy = File.ReadAllBytes(legacyPath);
            Output = reviewedOutput ?? Path.Combine(root, "never-created-native-output"); Source = (Path.Combine(root, "absent.bin"), Path.Combine(root, "absent.cue"));
            Window = new(project, () => Current, () => Source, () => { Acquired++; return new Owner(this); });
            try
            {
                Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
                Window.Show(); F.Pump(); Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; F.Pump();
            }
            catch (Exception primary)
            {
                // Constructor failure must not strand the actual acquired Window
                // merely because the caller's fixture assignment never returned.
                List<Exception> failures = [primary];
                try { Close(); } catch (Exception error) { failures.Add(error); }
                try { RequireProtected(); } catch (Exception error) { failures.Add(error); }
                F.Throw(failures); throw;
            }
        }
        internal string Status => Find<TextBlock>("FreshDocumentStatus").Text ?? "";
        // Observe the actual App session, not a fabricated Core Snapshot/plan.
        // Test mutations below use real session operations and real Store captures.
        internal NativeFreshLevelDocumentSession CurrentSession => (NativeFreshLevelDocumentSession)typeof(NativeFreshDocumentWindow)
            .GetField("_aggregateSession", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(Window)!;
        internal void ReplaceActualSession() => typeof(NativeFreshDocumentWindow)
            .GetField("_aggregateSession", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(Window, new NativeFreshLevelDocumentSession(CurrentSession.Snapshot));
        internal T Find<T>(string name) where T : Control => Window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
        internal void Fire(string name) { var b = Find<Button>(name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b)); F.Pump(); }
        internal void Set(string name, string value) { Find<TextBox>(name).Text = value; F.Pump(); }
        internal void ChangeSavedRaw() { File.AppendAllText(documentPath, " "); aggregate = File.ReadAllBytes(documentPath); }
        internal void ChangeManifest() { File.AppendAllText(project.ManifestPath, " "); manifest = File.ReadAllBytes(project.ManifestPath); }
        internal void Close() { Window.Close(); F.Pump(); }
        internal void RequireProtected(bool expectOutputAbsent = true)
        {
            List<Exception> failures = [];
            Attempt(() => F.Require(File.ReadAllBytes(documentPath).SequenceEqual(aggregate), "Exact expected aggregate bytes."));
            Attempt(() => F.Require(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "Exact expected full manifest."));
            Attempt(() => F.Require(File.ReadAllBytes(legacyPath).SequenceEqual(legacy), "Inactive legacy unchanged."));
            Attempt(() => F.Require(Acquired == Released, "All actual UI owners attempted."));
            if (expectOutputAbsent) Attempt(() => F.Require(!Directory.Exists(Output) && !File.Exists(Output), "No native output."));
            F.Throw(failures);
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        }
        private sealed class Owner(Fixture fixture) : IDisposable
        {
            private bool attempted;
            public void Dispose()
            {
                if (attempted) throw new InvalidDataException("Repeated actual UI owner release.");
                attempted = true; fixture.Released++;
                fixture.OnRelease?.Invoke();
                if (fixture.ReleaseFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fixture.ReleaseFailure).Throw();
            }
        }
    }
}
