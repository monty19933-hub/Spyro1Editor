using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;

// Real neutral presentation listings and tiny saved documents only. The metadata
// provider below cannot issue an Inventory, dependency package or native output.
internal static class NativeFreshMobyCatalogueUiCases
{
    private const string Wizard = "fixture-row-alpine-ridge-7";
    private const string Chest = "fixture-row-artisans-4";
    private const string Resource = "fixture-root-artisans-5";
    private static readonly string[] NativeFields = ["FreshNativeMobyX", "FreshNativeMobyY", "FreshNativeMobyZ", "FreshNativeMobyYaw"];
    private static int checks;

    internal static void Run(string parent)
    {
        checks = 0;
        List<(F Fixture, MetadataProvider? Provider)> fixtures = [];
        List<Exception> failures = [];
        try
        {
            ListingCases();
            Defaults(Add("catalogue-default", null));
            StageAndApply(Add("catalogue-stage", new()), parent);
            ChangedSourceListing(Add("catalogue-modified", new(Listing(new string('0', 64)))));
            foreach (string kind in new[] { "dirty", "stale", "no-owner", "null-owner", "owner-failure", "source-failure" })
                EarlyRefusal(Add("catalogue-refuse-" + kind, new(), owner: kind != "no-owner"), kind);
            foreach (string kind in new[] { "cancel", "project", "source", "base-aba", "route-aba", "native-aba", "material-aba" })
                CaptureInvalidation(Add("catalogue-capture-" + kind, new(hold: true)), kind);
            PickerInvalidation(Add("catalogue-picker-stale", new()));
            foreach (string kind in new[] { "source-image", "source-cue", "project", "raw", "raw-aba" })
                OwnerReleaseInvalidation(Add("catalogue-release-" + kind, new()), kind);
            SelectedDonorReleaseFailure(Add("catalogue-selected-release-failure", new()));
            BusyCommands(Add("catalogue-busy", new(hold: true)));
            CloseDrains(Add("catalogue-close", new(hold: true)));
            FailurePreservation(Add("catalogue-failure", new(hold: true)));
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // These provider gates do not accept cancellation. Release first,
            // invalidate delivery second, then drain actual UI work before close.
            foreach (var (_, provider) in fixtures) provider?.Released.TrySetResult();
            foreach (var (f, _) in fixtures) Attempt(() => f.Fire("FreshMobyCatalogueCancel"));
            foreach (var (f, _) in fixtures) Attempt(() =>
            {
                if (f.Window.MobyCatalogueTaskForTesting is { } task) F.Drain(task);
                f.Close();
            });
            foreach (var (f, _) in fixtures) Attempt(f.RequireProtected);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_MOBY_CATALOGUE_UI_OK checks={checks} neutralMetadataOnly=true sourceOpened=false nativeOutputWritten=false");

        (F, MetadataProvider?) Add(string id, MetadataProvider? provider, bool owner = true)
        {
            var f = new F(parent, id, owner: owner);
            fixtures.Add((f, provider));
            if (provider != null) f.Window.MobyCatalogueProviderForTesting = provider;
            return (f, provider);
        }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void ListingCases()
    {
        NativeFreshMobyCatalogueItem[] supplied = Items();
        var listing = new NativeFreshMobyCatalogueListing(NativeMobySourceInventory.OriginalImageSha256, supplied);
        var first = supplied[0]; supplied[0] = supplied[^1];
        Check(ReferenceEquals(listing.Entries[0], first) && listing.Entries.Count == 6,
            "Neutral listing detaches its ordered collection from a caller's mutable array.");
        Check(listing.Search().Select(x => x.SourceVariantId).SequenceEqual(new[]
            { Wizard, Chest, "fixture-row-alpine-ridge-6", "fixture-row-artisans-121" }),
            "Placed view keeps all families and same-class donor variants in source catalogue order.");
        Check(listing.Search("  cHeSt\t0x0095 ").Count == 2 && listing.Search("Chest T6").Single().SourceWadEntry == 14 &&
            listing.Search(null, "Alpine Ridge").Count == 2 && listing.Search("grass", "Alpine_Ridge").Count == 0,
            "Search retains catalogue AND-term, case-insensitive class/true-index and normalized source-level semantics.");
        Check(listing.Search(view: NativeMobyCatalogueView.Resources).Count == 2 &&
            listing.Search(view: NativeMobyCatalogueView.All).Count == 6,
            "Resources are visible metadata, not silently turned into placed donors.");
        Refuse(() => listing.Search(new string('x', 257)), "Catalogue query retains the existing 256-character bound.");
        Refuse(() => listing.Search(view: (NativeMobyCatalogueView)99), "Unknown catalogue view refuses.");
        var donor = listing.Entries.Single(x => x.SourceVariantId == Wizard);
        var source = listing.GetAuthoringSource(donor);
        Check(source is { SourceWadEntry: 14, SourceTrueIndex: 7, ExpectedNativeClass: 51 } && source.SourceVariant == F.Variant &&
            source.SourceVariant != donor.SourceVariantId,
            "Original full-image metadata maps exact row identity to the fixed neutral variant, never the hash-qualified catalogue ID.");
        Check(listing.GetAuthoringSource(donor with { }) == null &&
            listing.Entries.Where(x => x.Kind != NativeMobyCatalogueEntryKind.PlacedRow).All(x => listing.GetAuthoringSource(x) == null),
            "Unowned equal-value entries and registration/player resources cannot stage a placement.");
        foreach (string hash in new[] { "", NativeMobySourceInventory.OriginalImageSha256[..63],
            "0" + NativeMobySourceInventory.OriginalImageSha256[1..], new string('0', 64) })
        {
            var changed = Listing(hash);
            Check(changed.Search(view: NativeMobyCatalogueView.All).Count == 6 && changed.Entries.All(x => changed.GetAuthoringSource(x) == null),
                "Incomplete or different full-image identity retains every item inspect-only, never relabels it original retail.");
        }
        var invalid = donor with { SourceVariantId = "fixture-invalid-wad", SourceWadEntry = 11 };
        var invalidListing = new NativeFreshMobyCatalogueListing(NativeMobySourceInventory.OriginalImageSha256, [invalid]);
        Check(invalidListing.GetAuthoringSource(invalid) == null, "Invalid neutral source tuple remains inspect-only.");
    }

    private static void Defaults((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, _) = item;
        var before = f.Window.CurrentAggregateIntent;
        Check(!f.Find<Button>("FreshNativeMobyBrowse").IsEnabled, "Existing constructor with no provider cannot browse or infer a catalogue.");
        f.Fire("FreshNativeMobyBrowse");
        Check(f.OwnerAttempts == 0 && f.SourceCalls == 0 && ReferenceEquals(before, f.Window.CurrentAggregateIntent) && !f.Window.HasUnsavedChanges,
            "Default-null provider preserves existing applied document, mode and source/owner behavior even on a direct click.");
        f.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
        Check(!f.Find<Button>("FreshNativeMobyBrowse").IsVisible, "Native catalogue stays outside legacy mode.");
    }

    private static void StageAndApply((F Fixture, MetadataProvider? Provider) item, string artifacts)
    {
        var (f, provider) = item;
        var original = f.Window.CurrentAggregateIntent!;
        f.Set("FreshHeading", "75"); f.Fire("FreshDocumentApply");
        var appliedBase = f.Window.CurrentAggregateIntent!;
        Check(f.Window.HasUnsavedChanges && !f.Window.HasUnappliedFields && f.Find<Button>("FreshNativeMobyBrowse").IsEnabled,
            "Applied-but-unsaved authoring may open the catalogue without an implicit Save.");
        Task task = Start(f);
        Check(f.Window.MobyCatalogueBusyForTesting && provider!.Calls == 1 && provider.Image == f.SelectedSource.Image &&
            f.AcquiredOwners == 1 && f.ReleaseAttempts == 0 && f.Find<StackPanel>("FreshMobyCataloguePanel").IsVisible,
            "Actual window catalogue holds one real UI owner through source capture and donor choice.");
        RequireVisibleWithoutScrolling(f, ["FreshMobyCatalogueSearch", "FreshMobyCatalogueEntries"],
            "Browse automatically reveals its actual search and populated rows without test-assisted scrolling.");
        f.Set("FreshMobyCatalogueSearch", "chest");
        Check(Rows(f).Count == 2 && Rows(f).Select(x => x.SourceVariantId).Distinct().Count() == 2,
            "Visible search does not collapse same-class source variants.");
        f.Set("FreshMobyCatalogueSearch", "");
        f.Find<ComboBox>("FreshMobyCatalogueView").SelectedItem = NativeMobyCatalogueView.All; F.Pump();
        SelectDonor(f, Resource);
        Check(!f.Find<Button>("FreshMobyCatalogueStage").IsEnabled, "Registration details remain selectable but cannot be added.");
        f.Fire("FreshMobyCatalogueStage");
        Check(!task.IsCompleted && !f.Window.HasUnappliedFields && ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent),
            "Direct resource Add cannot bypass the placed-row guard or alter history.");
        SelectDonor(f, Wizard);
        Check(f.Find<TextBlock>("FreshMobyCatalogueDetails").Text!.Contains("Authoring reference only", StringComparison.OrdinalIgnoreCase),
            "A Wizard-shaped native donor is authoring-only, not the independently supported Wizard-route recipe.");
        CaptureWindow(f, artifacts, "FreshMobyCatalogueEntries", "fresh-moby-catalogue-populated.png");
        CaptureWindow(f, artifacts, "FreshMobyCatalogueStage", "fresh-moby-catalogue-donor-details.png");
        f.Fire("FreshMobyCatalogueStage"); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && !f.Window.MobyCatalogueBusyForTesting && f.Window.HasUnappliedFields &&
            ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent) && f.Window.Preview.SelectedEditorId == F.A &&
            NativeFields.All(x => f.Text(x) == ""),
            "Choice only stages a blank native form; prior selection, schema, IDs and applied-unsaved history stay intact.");
        Check(NativeFields.All(x => f.Find<TextBox>(x).IsEffectivelyEnabled) &&
            !f.Find<TextBox>("FreshHeading").IsEffectivelyEnabled && !f.Find<TextBox>("FreshWizardOriginZ").IsEffectivelyEnabled &&
            !f.Find<TextBox>("FreshMaterial0TextureId").IsEffectivelyEnabled,
            "Pending Add enables only native values, locking Base, Wizard and material fields.");
        string identity = f.Find<TextBlock>("FreshNativeMobySourceIdentity").Text!;
        Check(identity.Contains(F.Variant) && identity.Contains("WAD 14") && identity.Contains("true index 7") && identity.Contains("class 51"),
            "Staged source identity is complete read-only text, not a writable source form.");
        RequireVisibleWithoutScrolling(f, ["FreshNativeMobySourceIdentity", .. NativeFields],
            "Successful Stage automatically reveals the selected source and every blank XYZ/yaw field without test-assisted scrolling.");
        CaptureWindow(f, artifacts, "FreshNativeMobyFields", "fresh-moby-catalogue-pending-add.png");
        f.Select(F.B);
        Check(f.Window.Preview.SelectedEditorId == F.A, "Pending Add cannot drift the retained placement selection.");
        f.Fire("FreshDocumentApply");
        Check(ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent) && NativeFields.All(x => f.Text(x) == ""),
            "Blank Apply creates neither a default placement nor an ID.");
        foreach (var (field, value) in new[] { ("FreshNativeMobyX", "8448"), ("FreshNativeMobyY", "2147483648"),
            ("FreshNativeMobyZ", "65536"), ("FreshNativeMobyYaw", "256"), ("FreshNativeMobyYaw", "not-a-number") })
        {
            SetValid(f); f.Set(field, value); f.Fire("FreshDocumentApply");
            Check(ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent) && f.Text(field) == value && f.Window.HasUnappliedFields,
                "Invalid raw native value refuses atomically and remains visible: " + field + "=" + value);
        }
        foreach (var (field, changed, baseline) in new[] { ("FreshHeading", "76", "75"),
            ("FreshWizardOriginZ", "008909", "8909"), ("FreshMaterial0TextureId", "00", "0") })
        {
            SetValid(f); f.Set(field, changed); f.Fire("FreshDocumentApply");
            Check(ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent) && f.Text(field) == changed,
                "Programmatic mutation of a locked raw field cannot disappear during Add: " + field);
            f.Set(field, baseline);
        }
        f.Fire("FreshNativeMobyCancelAdd");
        Check(ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent) && !f.Window.HasUnappliedFields &&
            f.Window.Preview.SelectedEditorId == F.A && f.Text("FreshWizardOriginZ") == "8909",
            "Cancel restores the prior selected form and retains the applied-unsaved Base.");
        f.Fire("FreshDocumentUndo");
        Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent), "Canceled Add creates no extra undo entry.");
        f.Fire("FreshDocumentRedo");
        Task second = Start(f); SelectDonor(f, Chest); f.Fire("FreshMobyCatalogueStage"); F.Wait(second);
        SetValid(f); f.Fire("FreshDocumentApply");
        var addedDocument = f.Window.CurrentAggregateIntent!;
        var added = addedDocument.Placements.Last() as NativeFreshMobyPlacementIntent;
        Check(addedDocument.SchemaVersion == 3 && addedDocument.Placements.Count == 3 && added is
            { X: 9001, Y: 9103, Z: 65535, NativeYawByte: 17, Source.SourceWadEntry: 10, Source.SourceTrueIndex: 4, Source.ExpectedNativeClass: 149 } &&
            added.Source.SourceVariant == F.Variant && added.EditorId != F.A && added.EditorId != F.B &&
            addedDocument.Placements.Take(2).Zip(appliedBase.Placements).All(x => ReferenceEquals(x.First, x.Second)) &&
            ReferenceEquals(addedDocument.FloorMaterials, appliedBase.FloorMaterials) && addedDocument.Base.Heading == 75,
            "One successful Apply appends a native-tagged source reference with exact non-grid XY and preserves Base, existing routes and materials.");
        string id = added!.EditorId;
        f.Fire("FreshDocumentUndo");
        Check(ReferenceEquals(appliedBase, f.Window.CurrentAggregateIntent), "One Undo removes exactly the applied addition.");
        f.Fire("FreshDocumentRedo");
        Check(ReferenceEquals(addedDocument, f.Window.CurrentAggregateIntent) && f.Window.Preview.SelectedEditorId == id,
            "Redo restores the same native document and editor ID.");
        f.Fire("FreshDocumentSave"); f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(addedDocument);
        f.Fire("FreshDocumentReopen");
        var reopened = (NativeFreshMobyPlacementIntent)f.Window.CurrentAggregateIntent!.Placements.Last();
        Check(!f.Window.HasUnsavedChanges && reopened.EditorId == id && reopened.Source.SourceVariant == F.Variant &&
            reopened.Source.SourceWadEntry == 10 && reopened.Source.SourceTrueIndex == 4 && reopened.Source.ExpectedNativeClass == 149 &&
            reopened.X == 9001 && reopened.Y == 9103 && reopened.Z == 65535 && reopened.NativeYawByte == 17 &&
            f.Window.Preview.SelectedEditorId == id, "Save/Reopen round-trips the newly authored exact source tuple and native values.");
        int sources = f.SourceCalls, owners = f.OwnerAttempts;
        Check(!f.Find<Button>("FreshDocumentResearch").IsEnabled &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(f.Window.CurrentAggregateIntent),
            "Generic catalogue Add grants no saved research eligibility.");
        f.Fire("FreshDocumentResearch");
        if (f.Window.ResearchTaskForTesting is { } research) F.Wait(research);
        Check(f.SourceCalls == sources && f.OwnerAttempts == owners && f.Window.ResearchPublicationForTesting == null,
            "Unsupported generic source reference refuses research before source or owner acquisition.");
        var beforePending = f.Window.CurrentAggregateIntent;
        Task third = Start(f); SelectDonor(f, Wizard); f.Fire("FreshMobyCatalogueStage"); F.Wait(third);
        int decisions = 0;
        f.Window.DecisionForTesting = _ => { decisions++; return Task.FromResult(FreshDocumentDecision.Cancel); };
        f.Set("FreshNativeMobyYaw", "pending-invalid");
        f.Window.Close(); F.Pump(); f.Fire("FreshDocumentReopen");
        f.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
        Check(decisions == 3 && f.Window.IsVisible && ReferenceEquals(beforePending, f.Window.CurrentAggregateIntent) &&
            f.Window.Preview.SelectedEditorId == id && f.Text("FreshNativeMobyYaw") == "pending-invalid" &&
            f.Find<ComboBox>("FreshDocumentMode").SelectedItem as string == "Level aggregate",
            "Pending native Add joins existing Close/Reopen/mode dirty decisions without losing the prior native selection or raw values.");
        f.Fire("FreshNativeMobyCancelAdd");
        Check(!f.Window.HasUnsavedChanges && ReferenceEquals(beforePending, f.Window.CurrentAggregateIntent) &&
            f.Window.Preview.SelectedEditorId == id && f.Text("FreshNativeMobyX") == "9001" && f.Text("FreshNativeMobyYaw") == "17" &&
            f.Find<TextBlock>("FreshNativeMobySourceIdentity").Text!.Contains("true index 4"),
            "Cancel on an already-native selection restores that exact saved source and native form, not the staged donor.");
    }

    private static void ChangedSourceListing((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, _) = item;
        var initial = f.Window.CurrentAggregateIntent;
        Task task = Start(f); SelectDonor(f, Chest);
        Check(Rows(f).Count == 4 && !f.Find<Button>("FreshMobyCatalogueStage").IsEnabled &&
            f.Find<TextBlock>("FreshMobyCatalogueDetails").Text!.Contains("inspect-only", StringComparison.OrdinalIgnoreCase),
            "Different-image catalogue remains browsable but no modified tuple is presented as original retail.");
        f.Fire("FreshMobyCatalogueStage");
        Check(!task.IsCompleted && ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && !f.Window.HasUnappliedFields,
            "Direct Add cannot bypass full source identity mismatch.");
        f.Fire("FreshMobyCatalogueCancel"); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && ReferenceEquals(initial, f.Window.CurrentAggregateIntent), "Inspect-only catalogue closes without document mutation.");
    }

    private static void EarlyRefusal((F Fixture, MetadataProvider? Provider) item, string kind)
    {
        var (f, provider) = item;
        var initial = f.Window.CurrentAggregateIntent;
        var primary = new IOException("catalogue-" + kind + "-actual-refusal");
        if (kind == "dirty") f.Set("FreshHeading", "0064");
        if (kind == "stale") f.CurrentProject = false;
        if (kind == "null-owner") f.ReturnNullOwner = true;
        if (kind == "owner-failure") f.OwnerFailure = primary;
        if (kind == "source-failure") f.SourceOverride = () => throw primary;
        f.Fire("FreshNativeMobyBrowse");
        if (f.Window.MobyCatalogueTaskForTesting is { } task) F.Wait(task);
        Check(provider!.Calls == 0 && !f.Window.MobyCatalogueBusyForTesting && ReferenceEquals(initial, f.Window.CurrentAggregateIntent),
            "Readiness/owner/source refusal prevents inventory invocation: " + kind);
        Check(kind == "source-failure" ? f.AcquiredOwners == 1 && f.ReleaseAttempts == 1 : f.AcquiredOwners == 0 && f.SourceCalls == 0,
            "No source capture precedes real ownership and project/raw readiness: " + kind);
        if (kind == "dirty") Check(f.Text("FreshHeading") == "0064" && f.Window.HasUnappliedFields,
            "Opening catalogue never implicitly applies, discards or saves dirty raw spelling.");
        if (kind is "owner-failure" or "source-failure")
            Check(ReferenceEquals(f.Window.MobyCatalogueFailureForTesting, primary), "Retain actual early failure identity: " + kind);
        f.CurrentProject = true;
    }

    private static void CaptureInvalidation((F Fixture, MetadataProvider? Provider) item, string kind)
    {
        var (f, provider) = item;
        var initial = f.Window.CurrentAggregateIntent;
        Task task = Start(f); F.Wait(provider!.Entered.Task);
        if (kind == "cancel") f.Fire("FreshMobyCatalogueCancel");
        else if (kind == "project") f.CurrentProject = false;
        else if (kind == "source") f.SelectedSource = (f.SelectedSource.Image + ".other", f.SelectedSource.Cue);
        else
        {
            string field = kind switch { "base-aba" => "FreshHeading", "route-aba" => "FreshWizardOriginZ",
                "native-aba" => "FreshNativeMobyYaw", _ => "FreshMaterial0TextureId" };
            string before = f.Text(field); f.Set(field, before + "0"); f.Set(field, before);
        }
        Check(!task.IsCompleted && f.Window.MobyCatalogueBusyForTesting && f.ReleaseAttempts == 0,
            "Cancellation/drift does not pretend non-cancelable inventory work stopped: " + kind);
        provider.Released.TrySetResult(); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && !f.Window.MobyCatalogueBusyForTesting &&
            !f.Find<StackPanel>("FreshMobyCataloguePanel").IsVisible && ReferenceEquals(initial, f.Window.CurrentAggregateIntent) &&
            f.Window.Preview.SelectedEditorId == F.A && !f.Window.HasUnappliedFields,
            "Stale capture is discarded only after actual worker drain, including raw A-B-A: " + kind);
        f.CurrentProject = true;
    }

    private static void PickerInvalidation((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, _) = item;
        var initial = f.Window.CurrentAggregateIntent;
        Task task = Start(f); SelectDonor(f, Chest);
        f.SelectedSource = (f.SelectedSource.Image, f.SelectedSource.Cue + ".different");
        f.Fire("FreshMobyCatalogueStage"); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && !f.Window.HasUnappliedFields &&
            f.Window.Preview.SelectedEditorId == F.A, "Source ticket is rechecked at donor acceptance, not only at inventory completion.");
    }

    private static void OwnerReleaseInvalidation((F Fixture, MetadataProvider? Provider) item, string kind)
    {
        var (f, provider) = item;
        var initial = f.Window.CurrentAggregateIntent;
        string heading = f.Text("FreshHeading");
        Task task = Start(f); SelectDonor(f, Chest);
        int callbacks = 0;
        bool releaseBeforeStaging = false;
        f.OnRelease = () =>
        {
            callbacks++;
            releaseBeforeStaging = f.Window.MobyCatalogueBusyForTesting && !task.IsCompleted &&
                !f.Window.HasUnappliedFields && !f.Find<Button>("FreshNativeMobyCancelAdd").IsVisible &&
                ReferenceEquals(initial, f.Window.CurrentAggregateIntent);
            if (kind == "source-image") f.SelectedSource = (f.SelectedSource.Image + ".release-drift", f.SelectedSource.Cue);
            else if (kind == "source-cue") f.SelectedSource = (f.SelectedSource.Image, f.SelectedSource.Cue + ".release-drift");
            else if (kind == "project") f.CurrentProject = false;
            else
            {
                f.Set("FreshHeading", "0064");
                if (kind == "raw-aba") f.Set("FreshHeading", heading);
            }
        };
        f.Fire("FreshMobyCatalogueStage"); F.Wait(task);
        Check(callbacks == 1 && releaseBeforeStaging && f.ReleaseAttempts == 1 && provider!.Calls == 1,
            "Accepted donor remains unstaged until the actual owner's single release returns: " + kind);
        Check(f.Window.MobyCatalogueFailureForTesting != null && !f.Window.MobyCatalogueBusyForTesting &&
            !f.Find<StackPanel>("FreshMobyCataloguePanel").IsVisible && !f.Find<Button>("FreshNativeMobyCancelAdd").IsVisible &&
            ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && f.Window.Preview.SelectedEditorId == F.A,
            "Owner-release drift rejects the already chosen donor without adding an ID, selection or applied history: " + kind);
        Check(f.Window.HasUnappliedFields == (kind == "raw") && f.Text("FreshHeading") == (kind == "raw" ? "0064" : heading) &&
            f.Text("FreshWizardOriginZ") == "8909" && NativeFields.All(name => f.Text(name) == ""),
            "Release-time raw spelling is retained, and raw A-B-A cannot stage a late donor: " + kind);
        f.OnRelease = null;
        f.CurrentProject = true;
    }

    private static void SelectedDonorReleaseFailure((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, provider) = item;
        var initial = f.Window.CurrentAggregateIntent;
        var cleanup = new IOException("selected catalogue donor owner release failed");
        Task task = Start(f); SelectDonor(f, Chest);
        f.ReleaseFailure = cleanup;
        f.Fire("FreshMobyCatalogueStage"); F.Wait(task);
        Check(ReferenceEquals(f.Window.MobyCatalogueFailureForTesting, cleanup) && f.ReleaseAttempts == 1 &&
            !f.Window.MobyCatalogueBusyForTesting && !f.Find<Button>("FreshNativeMobyCancelAdd").IsVisible &&
            !f.Find<StackPanel>("FreshMobyCataloguePanel").IsVisible && !f.Window.HasUnsavedChanges &&
            ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && f.Window.Preview.SelectedEditorId == F.A,
            "A chosen donor does not stage after uncertain actual owner release; retain the exact cleanup failure and saved document.");
        Check(!f.Find<Button>("FreshNativeMobyBrowse").IsEffectivelyEnabled &&
            !f.Find<Button>("FreshMaterialsInspect").IsEffectivelyEnabled &&
            !f.Find<Button>("FreshDocumentResearch").IsEffectivelyEnabled &&
            !f.Find<Button>("FreshDocumentApply").IsEffectivelyEnabled &&
            !f.Find<Button>("FreshDocumentSave").IsEffectivelyEnabled,
            "Uncertain catalogue owner cleanup keeps authoring and all new source operations disabled.");
        int owners = f.OwnerAttempts, sources = f.SourceCalls, captures = provider!.Calls;
        f.ReleaseFailure = null; // Clearing an external failure must not clear the Window's cleanup guard.
        foreach (string command in new[] { "FreshNativeMobyBrowse", "FreshMaterialsInspect", "FreshDocumentResearch",
            "FreshDocumentApply", "FreshDocumentSave" }) f.Fire(command);
        f.CmdSave();
        if (f.Window.MobyCatalogueTaskForTesting is { } catalogue) F.Wait(catalogue);
        if (f.Window.MaterialInspectionTaskForTesting is { } inspection) F.Wait(inspection);
        if (f.Window.ResearchTaskForTesting is { } research) F.Wait(research);
        Check(f.OwnerAttempts == owners && f.SourceCalls == sources && provider.Calls == captures && f.ReleaseAttempts == 1 &&
            ReferenceEquals(f.Window.MobyCatalogueFailureForTesting, cleanup) &&
            ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && !f.Window.HasUnsavedChanges &&
            !f.Find<Button>("FreshNativeMobyCancelAdd").IsVisible,
            "Direct commands cannot retry the uncertain owner, read a new source or adopt the rejected donor after cleanup failure.");
    }

    private static void BusyCommands((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, provider) = item;
        var initial = f.Window.CurrentAggregateIntent;
        Task task = Start(f); F.Wait(provider!.Entered.Task);
        Check(f.Find<Button>("FreshMobyCatalogueCancel").IsEffectivelyEnabled &&
            !f.Find<ComboBox>("FreshDocumentMode").IsEffectivelyEnabled && !f.Find<TextBox>("FreshHeading").IsEffectivelyEnabled,
            "Catalogue cancel remains usable outside the disabled authoring form; mode and fields are locked.");
        foreach (string name in new[] { "FreshDocumentApply", "FreshDocumentSave", "FreshDocumentReopen", "FreshDocumentUndo", "FreshDocumentRedo",
            "FreshWizardAdd", "FreshWizardRemove", "FreshWizardMoveDown", "FreshMaterialsEnable", "FreshMaterialsInspect", "FreshDocumentResearch", "FreshNativeMobyBrowse" })
            f.Fire(name);
        f.CmdSave(); f.Select(F.B);
        f.Find<ComboBox>("FreshDocumentMode").SelectedItem = "Legacy floor (v1)"; F.Pump();
        Check(provider.Calls == 1 && f.AcquiredOwners == 1 && f.ReleaseAttempts == 0 && ReferenceEquals(initial, f.Window.CurrentAggregateIntent) &&
            f.Window.Preview.SelectedEditorId == F.A && !f.Window.HasUnsavedChanges &&
            f.Find<ComboBox>("FreshDocumentMode").SelectedItem as string == "Level aggregate" && f.Window.MaterialInspectionTaskForTesting == null,
            "Direct commands, Cmd-S, selection and mode changes cannot bypass the active capture owner.");
        Refuse(() => f.Window.MobyCatalogueProviderForTesting = new MetadataProvider(), "Active catalogue provider cannot be replaced.");
        f.Fire("FreshMobyCatalogueCancel"); f.Fire("FreshMobyCatalogueCancel");
        Check(!f.Find<Button>("FreshMobyCatalogueCancel").IsEffectivelyEnabled && !task.IsCompleted && f.ReleaseAttempts == 0,
            "Repeated Cancel only invalidates delivery and does not release or restart capture.");
        provider.Released.TrySetResult(); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && !f.Window.MobyCatalogueBusyForTesting, "Busy cancellation drains and attempts owner disposal once.");
    }

    private static void CloseDrains((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, provider) = item;
        Task task = Start(f); F.Wait(provider!.Entered.Task);
        int decisions = 0;
        f.Window.DecisionForTesting = _ => { decisions++; return Task.FromResult(FreshDocumentDecision.Cancel); };
        f.Window.Close(); f.Window.Close(); F.Pump();
        Check(f.Window.IsVisible && !task.IsCompleted && f.ReleaseAttempts == 0 && decisions == 0,
            "Repeated close waits for actual non-cancelable capture before any close decision or owner release.");
        provider.Released.TrySetResult(); F.Wait(task); F.Pump();
        Check(!f.Window.IsVisible && f.ReleaseAttempts == 1 && decisions == 0,
            "Clean close resumes once after inventory settlement and never installs the late catalogue.");
    }

    private static void FailurePreservation((F Fixture, MetadataProvider? Provider) item)
    {
        var (f, provider) = item;
        var primary = new IOException("actual catalogue provider failed after cancellation");
        var cleanup = new IOException("actual catalogue owner disposal failed");
        provider!.Failure = primary; f.ReleaseFailure = cleanup;
        Task task = Start(f); F.Wait(provider.Entered.Task); f.Fire("FreshMobyCatalogueCancel");
        provider.Released.TrySetResult(); F.Wait(task);
        var leaves = f.Window.MobyCatalogueFailureForTesting is { } actual ? F.Leaves(actual) : [];
        Check(leaves.Count == 2 && ReferenceEquals(leaves[0], primary) && ReferenceEquals(leaves[1], cleanup) &&
            f.ReleaseAttempts == 1 && !f.Window.HasUnsavedChanges && !f.Find<StackPanel>("FreshMobyCataloguePanel").IsVisible,
            "Actual capture and owner failures both survive cancellation; no late catalogue or authored success is fabricated.");
    }

    private static Task Start(F f)
    {
        f.Fire("FreshNativeMobyBrowse");
        return f.Window.MobyCatalogueTaskForTesting ?? throw new InvalidDataException("Expected actual catalogue UI task.");
    }
    private static void RequireVisibleWithoutScrolling(F f, string[] controls, string message)
    {
        // Let actual queued layout/render work settle, but never BringIntoView,
        // focus a field or assign a scroll offset before measuring the transition.
        f.Window.InvalidateVisual(); F.Pump();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); F.Pump();
        List<string> clipped = [];
        foreach (string name in controls)
        {
            var target = f.Find<Control>(name);
            var scroll = target.GetLogicalAncestors().OfType<ScrollViewer>().First();
            Point? top = target.TranslatePoint(default, scroll);
            if (!target.IsVisible || target.Bounds.Width <= 0 || target.Bounds.Height <= 0 || top is not { } point ||
                point.X < -1 || point.Y < -1 || point.X + target.Bounds.Width > scroll.Viewport.Width + 1 ||
                point.Y + target.Bounds.Height > scroll.Viewport.Height + 1)
                clipped.Add($"{name}: top={top}, size={target.Bounds.Size}, viewport={scroll.Viewport}, offset={scroll.Offset}");
        }
        Check(clipped.Count == 0, message + (clipped.Count == 0 ? "" : " Clipped: " + string.Join("; ", clipped)));
    }

    private static void CaptureWindow(F f, string artifacts, string control, string name)
    {
        // Existing default-size headless Window, with actual inspector scrolling.
        // These synthetic metadata frames contain no native source or model render.
        var target = f.Find<Control>(control);
        target.BringIntoView(); F.Pump();
        f.Window.InvalidateVisual(); F.Pump();
        using var frame = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("Expected actual catalogue Window frame.");
        string path = Path.Combine(artifacts, name); frame.Save(path);
        Console.WriteLine("FRESH_MOBY_CATALOGUE_WINDOW_FRAME path=" + path);
        Check(target.GetLogicalAncestors().OfType<ScrollViewer>().Any(scroll => scroll.Offset.Y > 0),
            "Catalogue and pending source controls are reachable in the actual inspector scroll: " + control);
    }
    private static IReadOnlyList<NativeFreshMobyCatalogueItem> Rows(F f) =>
        f.Find<ListBox>("FreshMobyCatalogueEntries").ItemsSource!.Cast<NativeFreshMobyCatalogueItem>().ToArray();
    private static void SelectDonor(F f, string id)
    {
        f.Find<ListBox>("FreshMobyCatalogueEntries").SelectedItem = Rows(f).Single(x => x.SourceVariantId == id); F.Pump();
    }
    private static void SetValid(F f)
    { f.Set(NativeFields[0], "9001"); f.Set(NativeFields[1], "9103"); f.Set(NativeFields[2], "65535"); f.Set(NativeFields[3], "17"); }
    private static NativeFreshMobyCatalogueListing Listing(string? hash = null) =>
        new(hash ?? NativeMobySourceInventory.OriginalImageSha256, Items());
    private static NativeFreshMobyCatalogueItem[] Items() =>
    [
        new("fixture-player-artisans-0", "artisans", "Artisans", 10, 10, null, 0, 0,
            NativeMobyCatalogueEntryKind.PlayerAnimationStream, "Spyro animation stream", "Special player resource", "Synthetic metadata; no placed row."),
        new(Wizard, "alpineridge", "Alpine Ridge", 12, 14, 7, null, 51,
            NativeMobyCatalogueEntryKind.PlacedRow, "Wizard fixture", "Enemy", "Synthetic classification fixture; not a certified family."),
        new(Chest, "artisans", "Artisans", 10, 10, 4, null, 149,
            NativeMobyCatalogueEntryKind.PlacedRow, "Chest fixture", "Container", "Synthetic classification fixture; no dependency closure."),
        new(Resource, "artisans", "Artisans", 10, 10, null, 5, 149,
            NativeMobyCatalogueEntryKind.LocalRegistration, "Registered resource 0x0095", "Local model registration", "A registration is not a placement."),
        new("fixture-row-alpine-ridge-6", "alpineridge", "Alpine Ridge", 12, 14, 6, null, 149,
            NativeMobyCatalogueEntryKind.PlacedRow, "Chest fixture", "Container", "Distinct same-class donor fixture."),
        new("fixture-row-artisans-121", "artisans", "Artisans", 10, 10, 121, null, 501,
            NativeMobyCatalogueEntryKind.PlacedRow, "Grass fixture", "Scenery", "Synthetic metadata; exact full-document recipe not implied.")
    ];
    private static void Refuse(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException)
        { Check(true, message); return; }
        throw new InvalidDataException("Missing catalogue refusal: " + message);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Fresh catalogue: " + message); checks++; }

    private sealed class MetadataProvider : INativeFreshMobyCatalogueProvider
    {
        private readonly NativeFreshMobyCatalogueListing listing;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        internal string? Image;
        internal Exception? Failure;
        internal MetadataProvider(NativeFreshMobyCatalogueListing? listing = null, bool hold = false)
        {
            this.listing = listing ?? Listing();
            if (!hold) Released.SetResult();
        }
        public async Task<NativeFreshMobyCatalogueListing> CaptureAsync(string image)
        {
            Calls++; Image = image; Entered.TrySetResult();
            await Released.Task; // No cancellation input: mirrors Inventory.Capture's drain requirement.
            if (Failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Failure).Throw();
            return listing;
        }
    }
}
