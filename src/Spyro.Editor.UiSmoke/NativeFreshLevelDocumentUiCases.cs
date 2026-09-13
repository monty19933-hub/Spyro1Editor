using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Actual tiny Store files and Avalonia Headless controls only. No source ROM,
// native window, fabricated publication result, or aggregate export context.
internal static class NativeFreshLevelDocumentUiCases
{
    private const string Legacy = "Legacy floor (v1)", Aggregate = "Level aggregate";
    private const string A = "11111111111111111111111111111111", B = "22222222222222222222222222222222";
    private static readonly string[] RouteFields = ["FreshWizardOriginX", "FreshWizardOriginY", "FreshWizardOriginZ", "FreshWizardEndX", "FreshWizardEndY"];
    private static string _root = "";
    private static int _checks;
    private static readonly List<Fixture> Fixtures = [];

    internal static void Run(string artifacts)
    {
        _checks = 0; Fixtures.Clear();
        _root = Directory.CreateTempSubdirectory("spyro-fresh-level-ui-").FullName;
        bool passed = false;
        try
        {
            ModesAndActualFork();
            UnconfirmedForkAndCloseOutcomes();
            AppliedRoutesAndAtomicBase(artifacts);
            BlankAddLifecycle();
            ModeDecisionFailures();
            RawCasAndFailedReads();
            MaximumAndNeutralCoordinates();
            ProjectAndBusyGuards();
            UnsupportedAggregateResearchRefused();
            foreach (var f in Fixtures) f.AssertSentinels();
            passed = true;
        }
        finally
        {
            foreach (var f in Fixtures) f.Dispose();
            if (passed) Directory.Delete(_root, recursive: true);
            else Console.WriteLine($"FRESH_LEVEL_UI_FAILED_FIXTURES path={_root}");
        }
        Console.WriteLine($"FRESH_NATIVE_LEVEL_UI_OK checks={_checks} sourceRequired=false nativeWindow=false publication=false");
    }

    private static void ModesAndActualFork()
    {
        foreach (bool legacy in new[] { false, true })
        foreach (bool modern in new[] { false, true })
        {
            var f = new Fixture($"owners-{legacy}-{modern}", legacy, modern ? Document(Wizard(A)) : null);
            Check(f.Mode.SelectedItem as string == Legacy && f.Mode.ItemsSource!.Cast<string>().SequenceEqual(new[] { Legacy, Aggregate }), "explicit legacy default and exactly two labeled modes");
            Check(f.Window.CurrentAggregateIntent == null && f.ActiveFile.Contains(NativeId65FreshDocumentStore.FileName, StringComparison.Ordinal), "no implicit aggregate selection by existence/mtime");
            f.Switch(Aggregate);
            Check(f.ActiveFile.Contains(NativeFreshLevelDocumentStore.FileName, StringComparison.Ordinal) && f.Window.CurrentAggregateIntent?.Placements.Count == (modern ? 1 : null), "actual aggregate presence/absence is visible");
            Check(!f.Button("FreshDocumentNew").IsEnabled && !f.Button("FreshDocumentResearch").IsEnabled, "absent/schema1-Wizard aggregate offers neither legacy starter nor supported schema2 research export");
            Check(Find<TextBlock>(f.Window,"FreshDocumentCapability").Text!.Contains("two retail materials",StringComparison.Ordinal) &&
                Find<TextBlock>(f.Window,"FreshDocumentCapability").Text!.Contains("no placements or the supported one-Wizard mixed profile",StringComparison.Ordinal),
                "visible capability states both bounded research arms, not a legacy export promise");
            if (!modern)
            {
                Check(!f.Window.HasUnsavedChanges && !File.Exists(f.ModernPath) && f.Button("FreshDocumentForkLegacy").IsEnabled == legacy, "absence has no default; only actual saved legacy can be explicitly forked");
                f.Fire("FreshDocumentNew");
                Check(f.Window.CurrentAggregateIntent == null && !File.Exists(f.ModernPath), "direct legacy starter event cannot populate aggregate absence");
                if (legacy)
                {
                    byte[] before = File.ReadAllBytes(f.LegacyPath);
                    f.Click("FreshDocumentForkLegacy");
                    var actual = NativeFreshLevelDocumentStore.Capture(f.Project);
                    Check(actual.Exists && actual.Intent!.Placements.Count == 0 && actual.Intent.Base.MusicTrackId == 20 &&
                        Canonical(actual.Intent).SequenceEqual(Canonical(f.Window.CurrentAggregateIntent!)) && !f.Window.HasUnsavedChanges, "Fork uses actual saved non-preset Base and returns clean empty aggregate");
                    Check(File.ReadAllBytes(f.LegacyPath).SequenceEqual(before), "Fork leaves legacy bytes exact");
                    f.Fire("FreshDocumentForkLegacy");
                    Check(Canonical(actual.Intent!).SequenceEqual(File.ReadAllBytes(f.ModernPath)), "duplicate/direct Fork does not overwrite existing aggregate");
                }
            }
            f.Switch(Legacy);
            Check(f.Window.CurrentAggregateIntent == null && ReferenceEquals(f.Window.CurrentIntent, f.Window.Preview.Intent), "legacy mode restores actual existing legacy preview identity");
        }
        foreach (string mutation in new[] { "source", "destination" })
        {
            var f = new Fixture("fork-stale-" + mutation, true, null); f.Switch(Aggregate);
            if (mutation == "source") File.AppendAllText(f.LegacyPath, " ");
            else NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project), Document(Wizard(B)));
            byte[] legacy = File.ReadAllBytes(f.LegacyPath); byte[]? modern = Optional(f.ModernPath);
            f.Fire("FreshDocumentForkLegacy");
            Check(f.Window.CurrentAggregateIntent == null && Same(modern, Optional(f.ModernPath)) && File.ReadAllBytes(f.LegacyPath).SequenceEqual(legacy), "stale actual source/absent destination Fork refuses without adoption: " + mutation);
        }
    }

    private static void AppliedRoutesAndAtomicBase(string artifacts)
    {
        var f = new Fixture("atomic", true, Document(Wizard(A), Wizard(B, 9728))); f.Switch(Aggregate); f.Select(A);
        var original = f.Window.CurrentAggregateIntent!; byte[] saved = File.ReadAllBytes(f.ModernPath), legacy = File.ReadAllBytes(f.LegacyPath);
        Check(ReferenceEquals(original, f.Window.Preview.AggregateIntent) && f.Window.Preview.SelectedEditorId == A && f.Text("FreshWizardOriginZ") == "8909", "preview and selected form retain actual aggregate/non-grid Z");
        Check(!f.Window.HasUnsavedChanges && !f.Button("FreshDocumentUndo").IsEnabled, "selection alone is neither an edit nor undo history");
        f.Set("FreshHeading", "77"); f.Set("FreshWizardOriginZ", "8910");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.Window.HasUnappliedFields, "raw base plus route edits do not mutate applied preview");
        f.Click("FreshDocumentApply"); var changed = f.Window.CurrentAggregateIntent!;
        Check(changed.Base.Heading == 77 && Route(changed, A).OriginZ == 8910 && ReferenceEquals(changed.Placements[1], original.Placements[1]) &&
            changed.Placements.Select(p => p.EditorId).SequenceEqual(new[] { A, B }) && ReferenceEquals(f.Window.Preview.AggregateIntent, changed), "one whole Apply changes independently edited Base/selected route atomically, preserving unrelated actual row and IDs");
        Check(File.ReadAllBytes(f.ModernPath).SequenceEqual(saved), "Apply is not Save");
        f.Click("FreshDocumentUndo");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.SelectedId == A && f.Button("FreshDocumentRedo").IsEnabled, "whole undo restores both parts and selection");
        f.Click("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.Button("FreshDocumentRedo").IsEnabled, "canonical no-op Apply preserves real redo and actual object");
        f.Click("FreshDocumentSave");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.Button("FreshDocumentRedo").IsEnabled, "no-op Save also preserves real redo through the Window");
        f.Click("FreshDocumentRedo"); Check(ReferenceEquals(f.Window.CurrentAggregateIntent, changed), "redo restores same whole applied object");
        f.Set("FreshHeading", "78"); f.Set("FreshWizardOriginZ", "invalid"); f.Click("FreshDocumentSave");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, changed) && f.Text("FreshHeading") == "78" && f.Text("FreshWizardOriginZ") == "invalid" &&
            File.ReadAllBytes(f.ModernPath).SequenceEqual(saved), "invalid selected route prevents valid Base from applying or saving");
        f.Fire("FreshWizardRemove"); f.Fire("FreshWizardMoveDown"); f.Select(B);
        Check(f.SelectedId == A && f.Text("FreshWizardOriginZ") == "invalid" && ReferenceEquals(f.Window.CurrentAggregateIntent, changed), "unapplied fields prevent selection/removal/reorder from silently dropping raw text");
        f.Click("FreshDocumentUndo");
        // Selected route now fits the smaller floor; the untouched B does not.
        f.Set("FreshFloorWidth", "1024"); f.Set("FreshWizardOriginX", "9216"); f.Set("FreshWizardEndX", "8960");
        f.Click("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, changed) && f.Text("FreshFloorWidth") == "1024", "floor shrink invalidating another route refuses entire aggregate");
        f.Click("FreshDocumentUndo");
        f.Set("FreshHeading", "79"); f.Set("FreshWizardOriginZ", "8909"); f.Click("FreshDocumentSave");
        var applied = f.Window.CurrentAggregateIntent!; var captured = NativeFreshLevelDocumentStore.Capture(f.Project);
        Check(applied.Base.Heading == 79 && Route(applied, A).OriginZ == 8909 && !ReferenceEquals(applied, captured.Intent) &&
            Canonical(applied).SequenceEqual(Canonical(captured.Intent!)) && !f.Window.HasUnsavedChanges, "Save validates/applies full raw form and binds actual parsed Store bytes without claiming same reference");
        f.Click("FreshWizardMoveDown"); var reordered = f.Window.CurrentAggregateIntent!;
        Check(reordered.Placements.Select(p => p.EditorId).SequenceEqual(new[] { B, A }) && f.SelectedId == A, "explicit reorder retains selected editor ID, never sorts/regenerates");
        f.Click("FreshDocumentUndo"); Check(f.Window.CurrentAggregateIntent!.Placements[0].EditorId == A && f.SelectedId == A, "undo reorder restores order/selection");
        f.Click("FreshDocumentRedo"); f.Click("FreshWizardRemove");
        Check(f.Window.CurrentAggregateIntent!.Placements.Single().EditorId == B && f.SelectedId == null, "selected removal clears selection without selecting a replacement");
        f.Click("FreshDocumentUndo"); Check(ReferenceEquals(f.Window.CurrentAggregateIntent, reordered) && f.SelectedId == A, "undo removal restores exact document and ID");
        f.Click("FreshDocumentRedo"); Check(f.SelectedId == null, "redo removal clears selection again");
        f.Click("FreshDocumentUndo"); f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen");
        Check(f.SelectedId == A && f.Window.CurrentAggregateIntent!.Placements.Select(p => p.EditorId).SequenceEqual(new[] { B, A }) &&
            !f.Button("FreshDocumentUndo").IsEnabled && !f.Button("FreshDocumentRedo").IsEnabled, "actual Reopen retains existing selected ID/order and resets history");
        Check(File.ReadAllBytes(f.LegacyPath).SequenceEqual(legacy), "all modern edits leave coexisting legacy unchanged");
        f.Window.InvalidateVisual(); Pump();
        using var image = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("No aggregate headless frame.");
        image.Save(Path.Combine(artifacts, "fresh-level-two-routes.png"));
    }

    private static void UnconfirmedForkAndCloseOutcomes()
    {
        var uncertain = new Fixture("fork-unconfirmed",true,null); uncertain.Switch(Aggregate);
        byte[] source = File.ReadAllBytes(uncertain.LegacyPath);
        string publicationLock = Path.Combine(uncertain.Project.RootPath,".export-publication.lock");
        File.WriteAllText(publicationLock,"actual foreign publication owner");
        uncertain.Click("FreshDocumentForkLegacy");
        Check(uncertain.Window.CurrentAggregateIntent == null && !File.Exists(uncertain.ModernPath) &&
            File.ReadAllBytes(uncertain.LegacyPath).SequenceEqual(source) && !uncertain.Window.HasUnsavedChanges &&
            uncertain.Status.Contains("unconfirmed",StringComparison.OrdinalIgnoreCase),
            "actual PublishNew foreign-lock refusal remains Unconfirmed and never installs an observed destination");
        Check(File.ReadAllText(publicationLock) == "actual foreign publication owner" &&
            File.ReadAllBytes(Path.Combine(uncertain.Project.RootPath,".native-id65-fresh-document.writer.lock")).Length == 32 &&
            File.ReadAllBytes(Path.Combine(uncertain.Project.RootPath,".native-fresh-level.writer.lock")).Length == 32,
            "actual Fork uncertainty retains foreign transaction lock and both acquired Store owner markers");
        string stage = Directory.GetFiles(uncertain.Project.RootPath,"*.tmp").Single();
        Check(File.ReadAllBytes(stage).SequenceEqual(Canonical(NativeFreshLevelDocumentIntent.Create(
            NativeId65FreshDocumentStore.Capture(uncertain.Project).Intent!,[]))),
            "actual Fork staged aggregate remains intact; this is not a simulated Dispose-after-rename failure");
        foreach (var decision in new[] { FreshDocumentDecision.Save,FreshDocumentDecision.Discard })
        {
            var f = new Fixture("close-" + decision,true,Document(Wizard(A))); f.Switch(Aggregate); f.Select(A);
            byte[] before = File.ReadAllBytes(f.ModernPath), legacy = File.ReadAllBytes(f.LegacyPath);
            f.Set("FreshHeading","76"); f.Set("FreshWizardOriginZ","8911"); f.Decision = decision;
            f.Window.Close(); Pump();
            Check(!f.Window.IsVisible && f.DecisionCalls == 1,"successful aggregate Close resolves exactly one dirty decision: " + decision);
            var saved = NativeFreshLevelDocumentStore.Capture(f.Project).Intent!;
            Check(decision == FreshDocumentDecision.Save
                ? saved.Base.Heading == 76 && Route(saved,A).OriginZ == 8911 && !File.ReadAllBytes(f.ModernPath).SequenceEqual(before)
                : File.ReadAllBytes(f.ModernPath).SequenceEqual(before),"Close Save/Discard has exact intended actual disk outcome");
            Check(File.ReadAllBytes(f.LegacyPath).SequenceEqual(legacy),"aggregate Close never synchronizes legacy file");
        }
    }

    private static void BlankAddLifecycle()
    {
        var f = new Fixture("add", true, Document(Wizard(A))); f.Switch(Aggregate); f.Select(A);
        var before = f.Window.CurrentAggregateIntent!; byte[] saved = File.ReadAllBytes(f.ModernPath);
        f.Set("FreshHeading", "70"); f.Fire("FreshWizardAdd");
        Check(f.Text("FreshHeading") == "70" && !f.Button("FreshWizardCancelAdd").IsVisible, "Add refuses pending Base fields instead of applying them implicitly");
        f.Click("FreshDocumentUndo"); f.Click("FreshWizardAdd");
        Check(RouteFields.All(name => f.Text(name) == "") && f.Window.HasUnsavedChanges && f.Window.HasUnappliedFields &&
            ReferenceEquals(f.Window.CurrentAggregateIntent, before) && !f.Field("FreshHeading").IsEffectivelyEnabled, "explicit blank Add is dirty with zero applied rows/IDs and locked Base");
        f.Decision = FreshDocumentDecision.Cancel; f.Switch(Legacy); f.Click("FreshDocumentReopen"); f.Window.Close(); Pump();
        Check(f.Mode.SelectedItem as string == Aggregate && f.Window.IsVisible && f.DecisionCalls == 3 && RouteFields.All(name => f.Text(name) == ""), "blank Add participates in mode/Reopen/Close dirty decisions");
        f.Decision = FreshDocumentDecision.Save; f.Window.Close(); Pump();
        Check(f.Window.IsVisible && f.Window.HasUnsavedChanges && File.ReadAllBytes(f.ModernPath).SequenceEqual(saved), "blank Add Save cannot invent a route or close");
        f.Click("FreshWizardCancelAdd");
        Check(!f.Window.HasUnsavedChanges && ReferenceEquals(f.Window.CurrentAggregateIntent, before) && f.SelectedId == A, "explicit Cancel new route restores applied selection with no ID/history mutation");
        f.Click("FreshWizardAdd"); f.RouteText(9728,9728,8909,9472,9728); f.Set("FreshHeading","75"); f.Fire("FreshDocumentSave");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent,before) && f.Text("FreshHeading") == "75" && File.ReadAllBytes(f.ModernPath).SequenceEqual(saved),
            "hostile direct edit of locked Base cannot be silently ignored by pending Add Save");
        f.Click("FreshWizardCancelAdd");
        f.Click("FreshWizardAdd"); f.RouteText(9728,9728,8909,9472,9728); f.Click("FreshDocumentApply");
        var added = f.Window.CurrentAggregateIntent!; string id = added.Placements[1].EditorId;
        Check(id != A && id.Length == 32 && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && f.SelectedId == id &&
            Route(added,id).OriginZ == 8909 && ReferenceEquals(added.Placements[0], before.Placements[0]), "successful explicit Add creates one ID and preserves all previous rows");
        f.Click("FreshDocumentUndo"); Check(ReferenceEquals(f.Window.CurrentAggregateIntent,before) && f.SelectedId == A, "undo Add returns previous selection");
        f.Click("FreshDocumentRedo"); Check(ReferenceEquals(f.Window.CurrentAggregateIntent,added) && f.SelectedId == id, "redo Add does not generate another ID");
        f.Click("FreshDocumentSave"); f.Click("FreshWizardAdd"); f.Decision = FreshDocumentDecision.Discard; f.Click("FreshDocumentReopen");
        Check(!f.Window.HasUnsavedChanges && f.Window.CurrentAggregateIntent!.Placements[1].EditorId == id, "Discard/Reopen removes only pending blank Add and reopens actual saved aggregate");
        f.Click("FreshWizardAdd"); f.Click("FreshDocumentUndo");
        Check(!f.Window.HasUnsavedChanges && !f.Button("FreshWizardCancelAdd").IsVisible, "first Undo cancels blank pending Add, not prior applied history");
    }

    private static void ModeDecisionFailures()
    {
        foreach (string sourceMode in new[] { Legacy, Aggregate })
        foreach (var decision in new[] { FreshDocumentDecision.Cancel, FreshDocumentDecision.Discard, FreshDocumentDecision.Save })
        {
            var f = new Fixture("switch-" + Fixtures.Count, true, Document(Wizard(A)));
            if (sourceMode == Aggregate) f.Switch(Aggregate);
            string targetMode = sourceMode == Legacy ? Aggregate : Legacy;
            string sourcePath = sourceMode == Legacy ? f.LegacyPath : f.ModernPath;
            string targetPath = sourceMode == Legacy ? f.ModernPath : f.LegacyPath;
            var oldBase = f.Window.CurrentIntent; var oldAggregate = f.Window.CurrentAggregateIntent;
            byte[] sourceBytes = File.ReadAllBytes(sourcePath); f.Set("FreshHeading", "71");
            File.WriteAllText(targetPath, "{bad target}"); f.Decision = decision; f.Switch(targetMode);
            Check(f.Mode.SelectedItem as string == sourceMode && f.Text("FreshHeading") == "71" && File.ReadAllText(targetPath) == "{bad target}", "failed/canceled target switch restores selector and raw fields: " + decision);
            if (decision == FreshDocumentDecision.Save)
            {
                Check(!File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes) && !f.Window.HasUnsavedChanges &&
                    f.Status.Contains("saved", StringComparison.OrdinalIgnoreCase), "source Save legitimately succeeded before target failure and status reports that fact");
                int heading = sourceMode == Legacy ? NativeId65FreshDocumentStore.Capture(f.Project).Intent!.Heading : NativeFreshLevelDocumentStore.Capture(f.Project).Intent!.Base.Heading;
                Check(heading == 71, "actual source Store confirms successful pre-switch Save");
            }
            else Check(File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes) && ReferenceEquals(oldBase,f.Window.CurrentIntent) &&
                ReferenceEquals(oldAggregate,f.Window.CurrentAggregateIntent) && f.Window.HasUnappliedFields, "Discard is deferred until successful target capture; Cancel never loses source state");
        }
        var pending = new Fixture("pending-mode", true, Document(Wizard(A))); pending.Switch(Aggregate); pending.Set("FreshHeading","72");
        var gate = new TaskCompletionSource<FreshDocumentDecision>(); pending.Window.DecisionForTesting = _ => { pending.DecisionCalls++; return gate.Task; };
        pending.Switch(Legacy); pending.Switch(Legacy); pending.Window.Close(); Pump();
        Check(pending.DecisionCalls == 1 && !pending.Mode.IsEnabled && !pending.Button("FreshDocumentSave").IsEnabled, "one pending dirty decision owns mode/close and disables duplicate mutations");
        gate.SetResult(FreshDocumentDecision.Cancel); Pump();
        Check(pending.Mode.SelectedItem as string == Aggregate && pending.Text("FreshHeading") == "72" && pending.Window.IsVisible, "async cancel restores mode controls and exact pending raw text");
        pending.Decision = FreshDocumentDecision.Discard;
        pending.Window.DecisionForTesting = _ => Task.FromResult(pending.Decision);
        pending.Switch(Legacy); pending.Switch(Aggregate);
        Check(pending.Window.CurrentAggregateIntent!.Base.Heading == 64 && !pending.Window.HasUnsavedChanges && !pending.Button("FreshDocumentUndo").IsEnabled, "successful Discard switch reopens actual target instead of retaining stale hidden drafts");
    }

    private static void RawCasAndFailedReads()
    {
        foreach (string mutation in new[] { "space", "id", "order", "placement", "malformed", "oversize", "manifest", "bad-manifest" })
        {
            var f = new Fixture("cas-" + mutation, true, Document(Wizard(A),Wizard(B))); f.Switch(Aggregate); f.Select(A);
            f.Set("FreshWizardOriginZ","8910"); f.Click("FreshDocumentApply"); var applied = f.Window.CurrentAggregateIntent;
            if (mutation == "manifest") File.AppendAllText(f.Project.ManifestPath," ");
            else if (mutation == "bad-manifest") File.WriteAllText(f.Project.ManifestPath,"{bad manifest}");
            else if (mutation == "space") File.AppendAllText(f.ModernPath," \n");
            else if (mutation == "malformed") File.WriteAllText(f.ModernPath,"{bad}");
            else if (mutation == "oversize") File.WriteAllBytes(f.ModernPath,new byte[65537]);
            else File.WriteAllBytes(f.ModernPath,Canonical(mutation == "order" ? Document(Wizard(B),Wizard(A)) :
                Document(Wizard(mutation == "id" ? "33333333333333333333333333333333" : A,z:mutation == "placement" ? 8908 : 8909),Wizard(B))));
            byte[] external = File.ReadAllBytes(f.ModernPath); f.Click("FreshDocumentSave");
            Check(ReferenceEquals(f.Window.CurrentAggregateIntent,applied) && f.SelectedId == A && f.Window.HasUnsavedChanges && File.ReadAllBytes(f.ModernPath).SequenceEqual(external), "raw CAS refusal preserves applied aggregate/selection/external bytes: " + mutation);
            f.Set("FreshWizardOriginZ","invalid"); f.Decision = FreshDocumentDecision.Discard;
            if (mutation is "malformed" or "oversize" or "bad-manifest")
            {
                f.Click("FreshDocumentReopen");
                Check(ReferenceEquals(f.Window.CurrentAggregateIntent,applied) && f.SelectedId == A && f.Text("FreshWizardOriginZ") == "invalid", "failed Reopen after Discard preserves entire prior applied/raw state");
            }
            f.ExpectedManifest = File.ReadAllBytes(f.Project.ManifestPath);
        }
        var absent = new Fixture("deleted-modern", true, Document(Wizard(A))); absent.Switch(Aggregate); File.Delete(absent.ModernPath);
        absent.Click("FreshDocumentReopen");
        Check(absent.Window.CurrentAggregateIntent == null && !absent.Window.HasUnsavedChanges && absent.Window.Preview.AggregateIntent == null &&
            !absent.Button("FreshDocumentSave").IsEnabled, "actual deletion reopens as aggregate absence without legacy/P0 fallback");
    }

    private static void MaximumAndNeutralCoordinates()
    {
        var routes = Enumerable.Range(1,128).Select(i => Wizard(i.ToString("x32"))).ToArray();
        var f = new Fixture("maximum", false, Document(routes)); f.Switch(Aggregate); f.Select(routes[64].EditorId);
        Check(f.Rows.ItemsSource!.Cast<NativeFreshPlacementIntent>().Count() == 128 && !f.Button("FreshWizardAdd").IsEnabled, "all valid128 placements remain visible and no implicit single-row truncation");
        f.Fire("FreshWizardAdd"); Check(!f.Window.HasUnsavedChanges && f.Window.CurrentAggregateIntent!.Placements.Count == 128, "direct 129th Add refuses before pending mutation");
        foreach (int z in new[] { 0,262128,8909 }) { f.Set("FreshWizardOriginZ",z.ToString()); f.Click("FreshDocumentSave"); }
        f.Click("FreshDocumentReopen");
        Check(f.Window.CurrentAggregateIntent!.Placements.Select(p => p.EditorId).SequenceEqual(routes.Select(p => p.EditorId)) &&
            Route(f.Window.CurrentAggregateIntent,routes[64].EditorId).OriginZ == 8909 && f.Text("FreshWizardOriginZ") == "8909", "neutral Z domain, stable IDs/order and actual Save/Reopen survive without recipe or snapping");
    }

    private static void ProjectAndBusyGuards()
    {
        var f = new Fixture("project", true, Document(Wizard(A))); f.Switch(Aggregate); f.Select(A); f.Set("FreshWizardOriginZ","8910");
        byte[] before = File.ReadAllBytes(f.ModernPath); f.CurrentProject = false;
        f.Fire("FreshDocumentSave"); f.Fire("FreshDocumentReopen"); f.Switch(Legacy);
        Check(f.Mode.SelectedItem as string == Aggregate && f.Text("FreshWizardOriginZ") == "8910" && File.ReadAllBytes(f.ModernPath).SequenceEqual(before), "project change blocks persistence/Reopen/mode capture without losing raw fields");
        var fork = new Fixture("project-fork",true,null); fork.Switch(Aggregate); fork.CurrentProject = false; fork.Fire("FreshDocumentForkLegacy");
        Check(!File.Exists(fork.ModernPath) && fork.Window.CurrentAggregateIntent == null, "project change blocks direct Fork");
        var pending = new Fixture("project-after-decision",true,Document(Wizard(A))); pending.Set("FreshHeading","73");
        var decision = new TaskCompletionSource<FreshDocumentDecision>(); pending.Window.DecisionForTesting = _ => decision.Task;
        pending.Switch(Aggregate); pending.CurrentProject = false; decision.SetResult(FreshDocumentDecision.Discard); Pump();
        Check(pending.Mode.SelectedItem as string == Legacy && pending.Text("FreshHeading") == "73" && pending.Window.HasUnappliedFields, "current-project identity rechecked after awaited decision before mode installation");

        var busy = new Fixture("research-busy",true,Document(Wizard(A))); var picker = new TaskCompletionSource<string?>();
        busy.Window.ResearchOutputPickerForTesting = _ => { busy.PickerCalls++; return picker.Task; };
        busy.Fire("FreshDocumentResearch");
        Check(busy.Window.ResearchBusyForTesting && busy.PickerCalls == 1 && !busy.Mode.IsEnabled, "actual old research busy begins before picker; mode disabled");
        var legacy = busy.Window.CurrentIntent; byte[] aggregate = File.ReadAllBytes(busy.ModernPath);
        busy.Switch(Aggregate);
        foreach (string name in new[] { "FreshDocumentForkLegacy", "FreshWizardAdd", "FreshWizardCancelAdd", "FreshWizardRemove", "FreshWizardMoveUp", "FreshWizardMoveDown" }) busy.Fire(name);
        busy.Window.Close(); Pump();
        Check(busy.Mode.SelectedItem as string == Legacy && ReferenceEquals(legacy,busy.Window.CurrentIntent) && busy.Window.CurrentAggregateIntent == null &&
            busy.Window.IsVisible && busy.DecisionCalls == 0 && File.ReadAllBytes(busy.ModernPath).SequenceEqual(aggregate), "direct new handlers and Close cannot mutate/switch while old research owns window");
        picker.SetResult(null); Finish(busy.Window.ResearchTaskForTesting!);
        Check(!busy.Window.ResearchBusyForTesting && busy.Work.Calls == 0 && busy.Mode.IsEnabled, "picker cancellation ends fake orchestration without compilation/publication");
    }

    private static void UnsupportedAggregateResearchRefused()
    {
        foreach (var document in new[] { Document(), Document(Wizard(A,z:8704)), Document(Wizard(A),Wizard(B)) })
        {
            var f = new Fixture("no-export-" + Fixtures.Count,true,document); f.Switch(Aggregate);
            byte[] legacy = File.ReadAllBytes(f.LegacyPath), modern = File.ReadAllBytes(f.ModernPath);
            Check(!f.Button("FreshDocumentResearch").IsEnabled, "aggregate research disabled even exact separated single-route geometry");
            f.Fire("FreshDocumentResearch"); if (f.Window.ResearchTaskForTesting is { } task) Finish(task);
            Check(f.SourceCalls == 0 && f.PickerCalls == 0 && f.Work.Calls == 0 && !f.Window.ResearchBusyForTesting &&
                f.Window.ResearchSnapshotForTesting == null && File.ReadAllBytes(f.LegacyPath).SequenceEqual(legacy) && File.ReadAllBytes(f.ModernPath).SequenceEqual(modern),
                "direct aggregate research refuses before source/picker/worker, never inactive legacy or first-row export");
            Check(f.Status.Contains("aggregate",StringComparison.OrdinalIgnoreCase) &&
                f.Status.Contains("two retail materials",StringComparison.Ordinal) && f.Status.Contains("no placements or the supported one-Wizard mixed profile",StringComparison.Ordinal),
                "schema1/Wizard refusal states both bounded research arms, not a legacy or partial fallback");
        }
    }

    private sealed class NeverWork : INativeFreshDocumentResearchUiWork
    {
        internal int Calls;
        public Task PrepareAsync(NativeId65FreshDocumentSnapshot captured,string image,string cue,CancellationToken token)
        { Calls++; throw new InvalidOperationException("Aggregate UI must not reach compilation."); }
        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string output,CancellationToken token)
        { Calls++; throw new InvalidOperationException("Aggregate UI must not reach publication."); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly EditorProjectLayout Project;
        internal readonly NativeFreshDocumentWindow Window;
        internal readonly NeverWork Work = new();
        internal byte[] ExpectedManifest;
        private readonly string _sentinel;
        internal bool CurrentProject = true;
        internal FreshDocumentDecision Decision = FreshDocumentDecision.Cancel;
        internal int DecisionCalls,SourceCalls,PickerCalls;
        internal string LegacyPath => Path.Combine(Project.RootPath,NativeId65FreshDocumentStore.FileName);
        internal string ModernPath => Path.Combine(Project.RootPath,NativeFreshLevelDocumentStore.FileName);
        internal ComboBox Mode => Find<ComboBox>(Window,"FreshDocumentMode");
        internal ListBox Rows => Find<ListBox>(Window,"FreshLevelPlacements");
        internal string? SelectedId => (Rows.SelectedItem as NativeFreshPlacementIntent)?.EditorId;
        internal string ActiveFile => Find<TextBlock>(Window,"FreshDocumentActiveFile").Text ?? "";
        internal string Status => Find<TextBlock>(Window,"FreshDocumentStatus").Text ?? "";

        internal Fixture(string name,bool legacy,NativeFreshLevelDocumentIntent? aggregate)
        {
            name = name.ToLowerInvariant();
            string path = Path.Combine(_root,name); Directory.CreateDirectory(path); Project = new(path,name);
            File.WriteAllText(Project.ManifestPath,"{\"schemaVersion\":1,\"projectId\":\"" + name + "\",\"unrelated\":\"preserve\"}\n");
            ExpectedManifest = File.ReadAllBytes(Project.ManifestPath);
            _sentinel = Path.Combine(path,"retail-source-and-old-lab-sentinel.txt"); File.WriteAllText(_sentinel,"unrelated protected bytes");
            if (legacy) NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(Project),Base(20));
            if (aggregate != null) NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project),aggregate);
            Window = new(Project,() => CurrentProject);
            Window.ResearchWorkForTesting = Work;
            Window.ResearchSourcePathsForTesting = () => { SourceCalls++; return (Path.Combine(path,"never-read.bin"),Path.Combine(path,"never-read.cue")); };
            Window.ResearchOutputPickerForTesting = _ => { PickerCalls++; return Task.FromResult<string?>(null); };
            Window.DecisionForTesting = _ => { DecisionCalls++; return Task.FromResult(Decision); };
            Fixtures.Add(this); Window.Show(); Pump();
        }
        internal Button Button(string name) => Find<Button>(Window,name);
        internal TextBox Field(string name) => Find<TextBox>(Window,name);
        internal string Text(string name) => Field(name).Text ?? "";
        internal void Set(string name,string text) { Field(name).Text = text; Pump(); }
        internal void Switch(string mode) { Mode.SelectedItem = mode; Pump(); }
        internal void Select(string id) { Rows.SelectedItem = Rows.ItemsSource!.Cast<NativeFreshPlacementIntent>().Single(p => p.EditorId == id); Pump(); }
        internal void Fire(string name) { var button = Button(name); button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent,button)); Pump(); }
        internal void Click(string name) { Check(Button(name).IsEffectivelyEnabled,"enabled " + name); Fire(name); }
        internal void RouteText(params int[] values) { for (int i=0;i<RouteFields.Length;i++) Set(RouteFields[i],values[i].ToString()); }
        internal void AssertSentinels() => Check(File.ReadAllBytes(Project.ManifestPath).SequenceEqual(ExpectedManifest) && File.ReadAllText(_sentinel) == "unrelated protected bytes", "complete manifest and unrelated source/retail/Lab sentinel unchanged");
        public void Dispose()
        {
            if (!Window.IsVisible) return;
            Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
            Window.Close(); Pump();
        }
    }

    private static NativeId65FreshDocumentIntent Base(int music=19) => NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),music);
    private static NativeFreshWizardRouteIntent Wizard(string id,int x=9984,int z=8909) => NativeFreshWizardRouteIntent.Create(id,x,9984,z,9472,9984);
    private static NativeFreshLevelDocumentIntent Document(params NativeFreshPlacementIntent[] rows) => NativeFreshLevelDocumentIntent.Create(Base(),rows);
    private static NativeFreshWizardRouteIntent Route(NativeFreshLevelDocumentIntent value,string id) => (NativeFreshWizardRouteIntent)value.Placements.Single(p => p.EditorId == id);
    private static byte[] Canonical(NativeFreshLevelDocumentIntent value) => NativeFreshLevelDocumentCodec.SerializeCanonical(value);
    private static byte[]? Optional(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
    private static bool Same(byte[]? a,byte[]? b) => a == null ? b == null : b != null && a.SequenceEqual(b);
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Finish(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(5)) { Pump(); Thread.Sleep(1); }
        Check(task.IsCompleted,"bounded headless continuation completed"); task.GetAwaiter().GetResult(); Pump();
    }
    private static void Check(bool value,string label) { if (!value) throw new InvalidDataException("Aggregate UI: " + label); _checks++; }
    private static T Find<T>(Control root,string name) where T : Control
    {
        T[] matches = root.GetLogicalDescendants().OfType<T>().Where(c => c.Name == name).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"Expected one {name}, found {matches.Length}.");
    }
}
