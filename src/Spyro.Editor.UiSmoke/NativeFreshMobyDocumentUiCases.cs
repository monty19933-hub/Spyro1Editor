using Avalonia.Controls;
using Avalonia.Headless;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;

// Actual schema3 Store documents and the existing Window only. Source identity
// is neutral intent: no ROM, imported model, successful worker or export issued.
internal static class NativeFreshMobyDocumentUiCases
{
    private const string C = "33333333333333333333333333333333";
    private static readonly string[] NativeFields = ["FreshNativeMobyX", "FreshNativeMobyY", "FreshNativeMobyZ", "FreshNativeMobyYaw"];
    private static int checks;

    internal static void Run(string parent)
    {
        checks = 0;
        List<F> fixtures = []; List<Exception> failures = [];
        try
        {
            Editing(Add("native-edit", true), parent);
            OptionalMaterials(Add("native-no-material", false));
            DirtyAndProject(Add("native-dirty-project", true));
            Inspection(Add("native-busy", true));
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            foreach (var fixture in fixtures) Attempt(fixture.Close);
            foreach (var fixture in fixtures) Attempt(fixture.RequireProtected);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_NATIVE_MOBY_UI_OK checks={checks} sourceOpened=false nativeModelRendered=false publication=false");
        F Add(string id, bool materials)
        {
            var fixture = new F(parent, id, document: Document(materials)); fixtures.Add(fixture); return fixture;
        }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static NativeFreshLevelDocumentIntent Document(bool materials)
    {
        var floor = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048, 8960, 8960, 64,
            new(80, 128, 112), new(128, 96, 64), 19);
        var source = NativeFreshRetailMobyReference.Create(F.Variant, 10, 4, 273);
        var other = NativeFreshRetailMobyReference.Create(F.Variant, 14, 16, 51);
        var empty = NativeFreshLevelDocumentIntent.Create(floor, []).UpgradeForNativeMobys();
        var document = empty.WithContent(floor,
        [
            NativeFreshMobyPlacementIntent.Create(F.A, source, 9001, 9103, 8000, 255),
            NativeFreshWizardRouteIntent.Create(F.B, 9984, 9984, 8704, 9472, 9984),
            NativeFreshMobyPlacementIntent.Create(C, other, 9601, 9703, 65535, 0)
        ]);
        return materials ? document.WithFloorMaterials(F.Pair(14, 1, 70, 1)) : document;
    }

    private static void Editing(F f, string artifacts)
    {
        var initial = f.Window.CurrentAggregateIntent!;
        var first = Native(f, F.A); var source = first.Source; var materials = initial.FloorMaterials;
        Check(initial.SchemaVersion == 3 && NativeFields.All(name => f.Find<TextBox>(name).IsEffectivelyEnabled) &&
            f.Text(NativeFields[0]) == "9001" && f.Text(NativeFields[1]) == "9103" &&
            f.Text(NativeFields[2]) == "8000" && f.Text(NativeFields[3]) == "255",
            "Selected native record exposes exact raw XYZ and native yaw without snapping.");
        Check(!f.Find<TextBox>("FreshWizardOriginX").IsEffectivelyEnabled &&
            f.Find<StackPanel>("FreshNativeMobyFields").IsVisible && !f.Find<StackPanel>("FreshWizardRouteFields").IsVisible,
            "Only the actual selected placement type has an active editor.");
        string identity = f.Find<TextBlock>("FreshNativeMobySourceIdentity").Text!;
        Check(identity.Contains(F.Variant) && identity.Contains("WAD 10") && identity.Contains("true index 4") && identity.Contains("class 273"),
            "Read-only text exposes the entire retained source identity, not an import certificate.");
        Check(f.Window.Preview.MaterialPreviewNotice.Length != 0 &&
            f.Find<TextBlock>("FreshLevelMaterialPreviewNotice").IsVisible && f.Find<ComboBox>("FreshMaterial0Kind").IsEnabled,
            "Schema3 retains and displays its actual optional material pair.");
        Check(f.Window.Preview.GetType().GetProperty("PlacementPreviewCaption", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(f.Window.Preview) as string == "1 authored route + 2 native markers (XY only)",
            "Mixed schema3 caption distinguishes routes from neutral native markers.");
        using (var frame = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("Expected actual mixed schema3 frame."))
            frame.Save(Path.Combine(artifacts, "native-moby-schema3-selected.png"));
        Check(f.Window.Preview.SelectedEditorId == F.A && ReferenceEquals(f.Window.Preview.AggregateIntent, initial),
            "Actual mixed-type preview renders selected native marker without a Wizard cast.");
        RequireNoResearch(f);

        f.Set("FreshHeading", "75"); f.Set("FreshNativeMobyYaw", "256"); f.Fire("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, initial) && f.Window.HasUnappliedFields &&
            f.Text("FreshHeading") == "75" && f.Text("FreshNativeMobyYaw") == "256",
            "Invalid native yaw refuses the entire otherwise valid Base change and preserves raw text.");
        f.Set("FreshNativeMobyYaw", "17"); f.Set("FreshFloorWidth", "1024"); f.Fire("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, initial) && f.Text("FreshFloorWidth") == "1024",
            "Valid Base/native fields cannot strand an untouched Wizard or native placement.");
        f.Set("FreshFloorWidth", "2048"); f.Set("FreshNativeMobyX", "9123"); f.Set("FreshNativeMobyY", "9235");
        f.Set("FreshNativeMobyZ", "65535"); f.Fire("FreshDocumentApply");
        var applied = f.Window.CurrentAggregateIntent!;
        Check(!f.Window.HasUnappliedFields && applied.Base.Heading == 75 && Native(f, F.A) is { X: 9123, Y: 9235, Z: 65535, NativeYawByte: 17 } &&
            ReferenceEquals(Native(f, F.A).Source, source) && ReferenceEquals(applied.FloorMaterials, materials) &&
            ReferenceEquals(applied.Placements[1], initial.Placements[1]) && ReferenceEquals(applied.Placements[2], initial.Placements[2]),
            "One atomic whole-form Apply preserves source, ordered untouched rows, ID and materials.");
        f.Fire("FreshDocumentUndo");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, initial) && Native(f, F.A).NativeYawByte == 255,
            "Undo restores the actual whole native document without converting type.");
        f.Fire("FreshDocumentRedo");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, applied), "Redo restores the actual whole native document.");
        f.Fire("FreshWizardMoveDown");
        Check(f.Window.CurrentAggregateIntent!.Placements.Select(p => p.EditorId).SequenceEqual(new[] { F.B, F.A, C }),
            "Existing ordered placement controls move native records without re-identification.");
        f.Fire("FreshWizardRemove");
        Check(f.Window.CurrentAggregateIntent!.Placements.All(p => p.EditorId != F.A), "Existing Remove accepts selected native placement.");
        f.Fire("FreshDocumentUndo");
        Check(f.Window.Preview.SelectedEditorId == F.A && ReferenceEquals(Native(f, F.A).Source, source),
            "Undo Remove restores native selection and source identity.");
        f.Select(F.B);
        Check(f.Find<TextBox>("FreshWizardOriginZ").IsEffectivelyEnabled && NativeFields.All(name => !f.Find<TextBox>(name).IsEffectivelyEnabled),
            "Switching to Wizard makes the native editor inactive, without changing either type.");
        var beforeInactiveEdit = f.Window.CurrentAggregateIntent;
        f.Set("FreshNativeMobyX", "inactive-invalid"); f.Fire("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, beforeInactiveEdit) && f.Window.HasUnappliedFields,
            "Inactive native raw edits refuse rather than disappearing during Wizard Apply.");
        f.Fire("FreshDocumentUndo"); f.Set("FreshWizardOriginZ", "8710"); f.Fire("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent!.Placements.Single(p => p.EditorId == F.B) is NativeFreshWizardRouteIntent { OriginZ: 8710 } &&
            Native(f, F.A).X == 9123 && ReferenceEquals(Native(f, F.A).Source, source),
            "Wizard Apply in schema3 preserves the native source and placement type.");
        f.Select(F.A); f.Fire("FreshDocumentSave");
        f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(f.Window.CurrentAggregateIntent!);
        f.Fire("FreshDocumentReopen");
        var reopened = Native(f, F.A);
        Check(!f.Window.HasUnsavedChanges && f.Window.CurrentAggregateIntent!.SchemaVersion == 3 &&
            reopened is { X: 9123, Y: 9235, Z: 65535, NativeYawByte: 17 } && SameSource(reopened.Source, source) &&
            f.Window.Preview.SelectedEditorId == F.A && f.Window.CurrentAggregateIntent.Placements.Select(p => p.Tag)
                .SequenceEqual(new[] { "wizard-route", "native-moby", "native-moby" }),
            "Save/Reopen retains full mixed order, type, fields and read-only source tuple.");
        RequireNoResearch(f);
    }

    private static void OptionalMaterials(F f)
    {
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 3 && f.Window.CurrentAggregateIntent.FloorMaterials == null &&
            f.Window.Preview.MaterialPreviewNotice.Length == 0 && f.Find<Button>("FreshMaterialsEnable").IsVisible,
            "Schema3 without a pair remains material-absent, not silently schema1 or schema2.");
        f.Fire("FreshMaterialsEnable"); f.Fire("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 3 && f.Window.CurrentAggregateIntent.FloorMaterials != null &&
            f.Window.Preview.MaterialPreviewNotice.Length != 0 && !f.Find<Button>("FreshMaterialsEnable").IsVisible,
            "Explicit material addition preserves schema3 and enables the material notice.");
        foreach (string id in new[] { F.A, F.B, C }) { f.Select(id); f.Fire("FreshWizardRemove"); }
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 3 && f.Window.CurrentAggregateIntent.Placements.Count == 0,
            "Removing the final native placement never downgrades the document schema.");
        f.Fire("FreshDocumentSave");
        f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(f.Window.CurrentAggregateIntent!);
        RequireNoResearch(f);
    }

    private static void DirtyAndProject(F f)
    {
        var original = f.Window.CurrentAggregateIntent;
        f.Set("FreshNativeMobyX", "09001"); f.Select(F.B);
        Check(f.Window.HasUnappliedFields && f.Window.Preview.SelectedEditorId == F.A && f.Text("FreshNativeMobyX") == "09001",
            "Raw native spelling blocks selection drift and remains unapplied.");
        int decisions = 0;
        f.Window.DecisionForTesting = _ => { decisions++; return Task.FromResult(FreshDocumentDecision.Cancel); };
        f.Window.Close(); F.Pump();
        Check(f.Window.IsVisible && decisions == 1 && ReferenceEquals(original, f.Window.CurrentAggregateIntent),
            "Native-only dirtiness participates in the existing close decision.");
        f.CurrentProject = false;
        f.Fire("FreshDocumentSave"); f.Fire("FreshDocumentReopen");
        f.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
        Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent) && f.Text("FreshNativeMobyX") == "09001" &&
            f.Find<ComboBox>("FreshDocumentMode").SelectedItem as string == "Level aggregate",
            "Stale project blocks Save/Reopen/mode installation without replacing native raw fields.");
        f.CurrentProject = true; f.Fire("FreshDocumentUndo");
        Check(!f.Window.HasUnsavedChanges && f.Text("FreshNativeMobyX") == "9001", "Undo raw spelling discards fields only.");
    }

    private static void Inspection(F f)
    {
        var original = f.Window.CurrentAggregateIntent;
        var work = new FailingMaterialInspectionWork(); f.Window.MaterialInspectionWorkForTesting = work;
        Task? task = null; List<Exception> failures = [];
        try
        {
            task = f.Start(); F.Wait(work.Entered.Task);
            Check(f.Window.MaterialInspectionBusyForTesting && NativeFields.All(name => !f.Find<TextBox>(name).IsEffectivelyEnabled),
                "Existing material operation locks native form fields under the same owner.");
            f.Fire("FreshDocumentApply"); f.Fire("FreshDocumentSave"); f.Fire("FreshWizardRemove"); f.Fire("FreshWizardMoveDown"); f.Select(F.B);
            Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent) && f.Window.Preview.SelectedEditorId == F.A,
                "Direct busy commands cannot mutate native selection or authored records.");
            f.Set("FreshNativeMobyYaw", "0255"); f.Set("FreshNativeMobyYaw", "255");
            Check(work.Token.IsCancellationRequested && !f.Window.HasUnsavedChanges && !task.IsCompleted,
                "Native raw A-B-A invalidates the actual asynchronous inspection despite equal final spelling.");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            work.Released.TrySetResult();
            try { if (task != null) F.Drain(task); } catch (Exception error) { failures.Add(error); }
        }
        F.Throw(failures);
        Check(f.ReleaseAttempts == 1 && f.NoImages && ReferenceEquals(f.Window.MaterialInspectionFailureForTesting, work.Primary) &&
            ReferenceEquals(original, f.Window.CurrentAggregateIntent), "Actual failing work drains and releases once; no native result is fabricated.");
    }

    private static void RequireNoResearch(F f)
    {
        int sourceCalls = f.SourceCalls, ownerAttempts = f.OwnerAttempts, pickerCalls = 0;
        f.Window.ResearchOutputPickerForTesting = _ => { pickerCalls++; return Task.FromResult<string?>(null); };
        Check(!f.Find<Button>("FreshDocumentResearch").IsEnabled &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(f.Window.CurrentAggregateIntent),
            "Every schema3 document remains outside current saved research eligibility.");
        f.Fire("FreshDocumentResearch");
        if (f.Window.ResearchTaskForTesting is { } task) F.Wait(task);
        Check(f.SourceCalls == sourceCalls && f.OwnerAttempts == ownerAttempts && pickerCalls == 0 &&
            f.Window.ResearchPublicationForTesting == null && f.Find<TextBlock>("FreshDocumentCapability").Text!.Contains("authored-only") &&
            f.Find<TextBlock>("FreshDocumentCapability").Text!.Contains("native dependencies"),
            "Schema3 refuses before owner/source/picker and labels unsupported native dependencies honestly.");
    }

    private static NativeFreshMobyPlacementIntent Native(F f, string id) =>
        f.Window.CurrentAggregateIntent!.Placements.Single(p => p.EditorId == id) as NativeFreshMobyPlacementIntent ??
            throw new InvalidDataException("Native placement was lost or converted to another tag.");
    private static bool SameSource(NativeFreshRetailMobyReference a, NativeFreshRetailMobyReference b) =>
        a.SourceVariant == b.SourceVariant && a.SourceWadEntry == b.SourceWadEntry &&
        a.SourceTrueIndex == b.SourceTrueIndex && a.ExpectedNativeClass == b.ExpectedNativeClass;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); checks++; }
}
