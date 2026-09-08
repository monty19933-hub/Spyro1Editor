using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using F = NativeFreshMaterialInspectionUiFixture;

// Private source-free protocol cases. No test implementation can return fake success.
internal static class NativeFreshMaterialInspectionLifecycleCases
{
    private static int checks;
    internal static void Run()
    {
        checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-inspection-lifecycle-").FullName;
        List<F> fixtures = []; List<Exception> failures = [];
        try
        {
            Commands(Add("commands"));
            Cancellation(Add("cancel"));
            foreach (string dirty in new[] { "clean", "base", "route", "material", "invalid" })
                foreach (var decision in new[] { FreshDocumentDecision.Cancel, FreshDocumentDecision.Discard, FreshDocumentDecision.Save })
                    Closing(Add("close-" + dirty + "-" + decision.ToString().ToLowerInvariant()), dirty, decision);
            EarlyFailures(Add("owner-failure"), Add("provider-failure"), Add("null-owner"), Add("stale-project"));
            OwnerFailureAfterWork(Add("owner-close-failure"));
            F noOwner = new(root, "no-owner", owner: false); fixtures.Add(noOwner);
            noOwner.Window.MaterialInspectionWorkForTesting = new FailingMaterialInspectionWork();
            noOwner.Fire("FreshMaterialsInspect");
            Check(noOwner.OwnerAttempts == 0 && noOwner.SourceCalls == 0 &&
                noOwner.Window.MaterialInspectionTaskForTesting?.IsCompleted == true && noOwner.NoImages,
                "test worker presence cannot substitute for a required editor owner");
            ActualContextIdentity(Add("context-identity"));
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            foreach (var f in fixtures) Attempt(f.Close);
            foreach (var f in fixtures) Attempt(f.RequireProtected);
            Console.WriteLine("MATERIAL_INSPECTION_LIFECYCLE_RETAINED path=" + root);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_LIFECYCLE_OK checks={checks} nativeSourceCaptured=false publication=false");
        F Add(string id) { var f = new F(root, id); fixtures.Add(f); return f; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void Commands(F f)
    {
        var original = f.Window.CurrentAggregateIntent!;
        var work = new FailingMaterialInspectionWork();
        f.Window.MaterialInspectionWorkForTesting = work;
        Task? task = null; List<Exception> failures = [];
        try
        {
            task = f.Start(); F.Wait(work.Entered.Task);
            Check(f.Window.MaterialInspectionBusyForTesting && !task.IsCompleted && work.Calls == 1 &&
                f.AcquiredOwners == 1 && f.ReleaseAttempts == 0 && f.SourceCalls == 1 &&
                ReferenceEquals(work.Request!.Document, original), "one actual UI request and retained owner");
            Check(f.Find<Button>("FreshMaterialsCancelInspect").IsEffectivelyEnabled &&
                !f.Find<TextBox>("FreshMaterial0TextureId").IsEffectivelyEnabled, "Cancel remains outside disabled form ancestor");
            foreach (string name in new[] { "FreshDocumentApply", "FreshDocumentSave", "FreshDocumentReopen",
                "FreshDocumentNew", "FreshDocumentUndo", "FreshDocumentRedo", "FreshWizardAdd", "FreshWizardCancelAdd",
                "FreshWizardRemove", "FreshWizardMoveUp", "FreshWizardMoveDown", "FreshMaterialsEnable",
                "FreshDocumentForkLegacy", "FreshDocumentResearch", "FreshMaterialsInspect" })
                f.Fire(name);
            f.CmdSave();
            f.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
            f.Select(F.B);
            Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) &&
                f.Window.Preview.SelectedEditorId == F.A && !f.Window.HasUnsavedChanges &&
                f.Find<ComboBox>("FreshDocumentMode").SelectedItem as string == "Level aggregate" &&
                work.Calls == 1 && f.SourceCalls == 1 && f.Window.ResearchTaskForTesting == null,
                "all direct editing/mode/selection/research events refused without new work or state adoption");
            try { f.Window.MaterialInspectionWorkForTesting = new FailingMaterialInspectionWork(); throw new Exception("Missing busy setter refusal."); }
            catch (InvalidOperationException) { Check(true, "active worker cannot be replaced"); }
        }
        catch (Exception error) { failures.Add(error); }
        finally { ReleaseAndDrain(f, work, task, failures); }
        F.Throw(failures);
        Outcome(f, !f.Window.MaterialInspectionBusyForTesting && f.ReleaseAttempts == 1 && f.NoImages &&
            ReferenceEquals(f.Window.MaterialInspectionFailureForTesting, work.Primary), "actual expected failed work settles once without success");
    }

    private static void Cancellation(F f)
    {
        var original = f.Window.CurrentAggregateIntent!;
        var work = new FailingMaterialInspectionWork();
        f.Window.MaterialInspectionWorkForTesting = work;
        Task? task = null; List<Exception> failures = [];
        try
        {
            task = f.Start(); F.Wait(work.Entered.Task);
            f.Fire("FreshMaterialsCancelInspect"); f.Fire("FreshMaterialsCancelInspect");
            f.Set("FreshMaterial0TextureId", "1"); f.Set("FreshMaterial0TextureId", "0"); // Actual ABA events.
            f.Fire("FreshMaterialsInspect");
            Check(work.Token.IsCancellationRequested && !task.IsCompleted && f.ReleaseAttempts == 0 &&
                f.Window.MaterialInspectionBusyForTesting && work.Calls == 1 &&
                !f.Find<Button>("FreshMaterialsCancelInspect").IsEffectivelyEnabled,
                "cancel/direct drift cannot shortcut real task settlement or start another job");
            f.Set("FreshMaterial0TextureId", "invalid-last-raw");
        }
        catch (Exception error) { failures.Add(error); }
        finally { ReleaseAndDrain(f, work, task, failures); }
        F.Throw(failures);
        Outcome(f, ReferenceEquals(f.Window.MaterialInspectionFailureForTesting, work.Primary) &&
            ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.Text("FreshMaterial0TextureId") == "invalid-last-raw" &&
            f.ReleaseAttempts == 1 && f.NoImages && f.InspectionStatus.Contains(work.Primary.Message),
            "earlier true worker failure survives cancellation; raw text and applied reference remain");
    }

    private static void Closing(F f, string dirty, FreshDocumentDecision decision)
    {
        var original = f.Window.CurrentAggregateIntent!;
        if (dirty == "base") f.Set("FreshHeading", "77");
        if (dirty == "route") f.Set("FreshWizardOriginZ", "8910");
        if (dirty == "material") f.Set("FreshMaterial0TextureId", "1");
        if (dirty == "invalid") f.Set("FreshFloorWidth", "invalid-base");
        // Materials remain valid: inspection does not parse the independent dirty Base/route.
        var work = new FailingMaterialInspectionWork();
        f.Window.MaterialInspectionWorkForTesting = work;
        int decisions = 0;
        f.Window.DecisionForTesting = _ => { decisions++; return Task.FromResult(decision); };
        Task? task = null; List<Exception> failures = [];
        try
        {
            task = f.Start(); F.Wait(work.Entered.Task);
            f.Window.Close(); f.Window.Close(); F.Pump();
            Check(f.Window.IsVisible && !task.IsCompleted && decisions == 0 && f.ReleaseAttempts == 0 &&
                work.Token.IsCancellationRequested, "duplicate clean/dirty close waits actual worker before normal decision");
        }
        catch (Exception error) { failures.Add(error); }
        finally { ReleaseAndDrain(f, work, task, failures); }
        F.Throw(failures); F.Pump();
        if (dirty == "clean")
            Check(!f.Window.IsVisible && decisions == 0, "clean close resumes once without a dirty prompt");
        else
        {
            Check(decisions == 1, "one normal dirty decision after true worker settlement");
            bool stays = decision == FreshDocumentDecision.Cancel || decision == FreshDocumentDecision.Save && dirty == "invalid";
            Check(f.Window.IsVisible == stays, "normal dirty choice and invalid-save policy preserved");
            if (decision == FreshDocumentDecision.Save && dirty != "invalid")
            {
                var expected = dirty switch
                {
                    "base" => original.WithContent(NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048, 8960, 8960, 77,
                        original.Base.FloorColor, original.Base.SkyColor, 19), original.Placements),
                    "route" => original.WithContent(original.Base, original.Placements.Select(row => row.EditorId == F.A
                        ? NativeFreshWizardRouteIntent.Create(F.A, 9984, 9984, 8910, 9472, 9984) : row)),
                    _ => original.WithFloorMaterials(F.Pair(14, 1, 70, 1))
                };
                f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(expected);
                Check(File.ReadAllBytes(f.DocumentPath).SequenceEqual(f.ExpectedDocument), "close Save writes exact intended full form only after drain");
            }
            else if (stays)
                Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && f.Window.HasUnappliedFields,
                    "Cancel/invalid Save retain actual applied intent and raw dirty form");
        }
        Check(f.ReleaseAttempts == 1, "close never retries owner cleanup");
    }

    private static void EarlyFailures(F owner, F source, F nullOwner, F stale)
    {
        var ownerError = new IOException("actual test owner acquire refusal");
        owner.OwnerFailure = ownerError;
        F.Drain(owner.Start());
        Outcome(owner, ReferenceEquals(owner.Window.MaterialInspectionFailureForTesting, ownerError) &&
            owner.OwnerAttempts == 1 && owner.AcquiredOwners == 0 && owner.SourceCalls == 0, "failed owner acquisition is not fabricated ownership");
        var sourceError = new IOException("actual provider refusal");
        source.SourceOverride = () => throw sourceError;
        F.Drain(source.Start());
        Outcome(source, ReferenceEquals(source.Window.MaterialInspectionFailureForTesting, sourceError) &&
            source.ReleaseAttempts == 1 && source.SourceCalls == 1 && source.NoImages, "source refusal releases actual acquired editor owner");
        nullOwner.ReturnNullOwner = true; F.Drain(nullOwner.Start());
        Outcome(nullOwner, nullOwner.Window.MaterialInspectionFailureForTesting is InvalidDataException &&
            nullOwner.SourceCalls == 0 && nullOwner.AcquiredOwners == 0, "null returned owner is refused before source resolution");
        stale.CurrentProject = false; F.Drain(stale.Start());
        Outcome(stale, stale.Window.MaterialInspectionFailureForTesting is InvalidDataException &&
            stale.OwnerAttempts == 0 && stale.SourceCalls == 0, "stale current project refuses before owner and source");
    }

    private static void OwnerFailureAfterWork(F f)
    {
        var work = new FailingMaterialInspectionWork();
        var close = new IOException("actual test editor Dispose refusal");
        f.Window.MaterialInspectionWorkForTesting = work; f.ReleaseFailure = close;
        Task? task = null; List<Exception> failures = [];
        try { task = f.Start(); F.Wait(work.Entered.Task); }
        catch (Exception error) { failures.Add(error); }
        finally { ReleaseAndDrain(f, work, task, failures); }
        F.Throw(failures);
        var leaves = f.Window.MaterialInspectionFailureForTesting is { } actualFailure ? F.Leaves(actualFailure) : [];
        Outcome(f, leaves.Count == 2 && ReferenceEquals(leaves[0], work.Primary) && ReferenceEquals(leaves[1], close) &&
            f.ReleaseAttempts == 1 && !f.Window.MaterialInspectionBusyForTesting && f.NoImages &&
            f.InspectionStatus.Contains(work.Primary.Message) && f.InspectionStatus.Contains(close.Message),
            "primary and cleanup errors both preserved; no success or claim editor guard released");
    }

    private static void ActualContextIdentity(F f)
    {
        var snapshot = NativeFreshLevelDocumentStore.Capture(f.Project);
        var session = new NativeFreshLevelDocumentSession(snapshot);
        var equalSnapshot = NativeFreshLevelDocumentStore.Capture(f.Project);
        var equalIntent = NativeFreshLevelDocumentCodec.Parse(NativeFreshLevelDocumentCodec.SerializeCanonical(session.CurrentIntent!));
        Check(NativeFreshDocumentWindow.IsMaterialInspectionContextCurrent(session, snapshot, session.CurrentIntent!, session),
            "same actual neutral Session/Snapshot/Intent accepted by production-used comparison");
        Check(!NativeFreshDocumentWindow.IsMaterialInspectionContextCurrent(session, equalSnapshot, session.CurrentIntent!, session) &&
            !NativeFreshDocumentWindow.IsMaterialInspectionContextCurrent(session, snapshot, equalIntent, session) &&
            !NativeFreshDocumentWindow.IsMaterialInspectionContextCurrent(session, snapshot, session.CurrentIntent!, new NativeFreshLevelDocumentSession(snapshot)),
            "genuinely equal-byte snapshots/intents or same-snapshot independent sessions are not substituted");
    }
    private static void ReleaseAndDrain(F f, FailingMaterialInspectionWork work, Task? task, List<Exception> failures)
    {
        work.Released.TrySetResult();
        if (task != null) try { F.Drain(task); } catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0 && f.Window.MaterialInspectionFailureForTesting is { } actual &&
            !ReferenceEquals(actual, work.Primary) && !failures.Any(e => ReferenceEquals(e, actual)))
            failures.Add(actual);
    }
    private static void Outcome(F f, bool condition, string message)
    {
        if (condition) { checks++; return; }
        var assertion = new InvalidDataException("Inspection lifecycle: " + message);
        if (f.Window.MaterialInspectionFailureForTesting is { } actual)
            throw new IOException("Unexpected actual UI failure retained before its failed expectation.", new AggregateException(actual, assertion));
        throw assertion;
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Inspection lifecycle: " + message); checks++; }
}
