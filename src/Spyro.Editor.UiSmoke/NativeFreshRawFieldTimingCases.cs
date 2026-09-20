using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using F = NativeFreshMaterialInspectionUiFixture;

// Actual headless controls, tiny saved projects and neutral catalogue metadata.
// No successful native inspection, source Inventory or publication is fabricated.
internal static class NativeFreshRawFieldTimingCases
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string[] NativeFields = ["FreshNativeMobyX", "FreshNativeMobyY", "FreshNativeMobyZ", "FreshNativeMobyYaw"];
    private static readonly string[] FamilyTextFields = ["FreshFamilyKeyX", "FreshFamilyKeyY", "FreshFamilyKeyZ",
        "FreshFamilyChestX", "FreshFamilyChestY", "FreshFamilyChestZ",
        .. Enumerable.Range(1, 5).SelectMany(i => new[] { "X", "Y", "Z" }.Select(axis => $"FreshFamilyReward{i}{axis}"))];
    private static readonly string[] FamilyModes = [.. Enumerable.Range(1, 5).Select(i => $"FreshFamilyReward{i}Mode")];
    private static readonly string[] AuthoredTextFields = ["FreshFloorX", "FreshFloorY", "FreshFloorZ", "FreshFloorWidth", "FreshFloorDepth",
        "FreshSpawnX", "FreshSpawnY", "FreshHeading", "FreshFloorColor", "FreshSkyColor",
        "FreshWizardOriginX", "FreshWizardOriginY", "FreshWizardOriginZ", "FreshWizardEndX", "FreshWizardEndY",
        .. NativeFields, "FreshMaterial0SourceVariant", "FreshMaterial0TextureId", "FreshMaterial1SourceVariant", "FreshMaterial1TextureId",
        .. FamilyTextFields];
    private static int checks;

    internal static void Run(string root)
    {
        checks = 0;
        WithFixture(root, "raw-timing-fields", f =>
        {
            f.Select(NativeFreshFamilyDocumentSessionCases.FamilyId);
            Settle(f);
            foreach (string name in AuthoredTextFields) ImmediateRawChange(f, name);
            Check(AuthoredTextFields.Length == 44 && AuthoredTextFields.Distinct(StringComparer.Ordinal).Count() == 44,
                "Every authored legacy aggregate TextBox is covered exactly once.");
            Check(f.SourceCalls == 0 && f.OwnerAttempts == 0, "Raw timing does not read source or acquire export authority.");
        });
        WithFixture(root, "raw-timing-tiled", f =>
        {
            var tiled = NativeFreshFamilyDocumentSessionCases.TiledDocument();
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project), tiled);
            f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(tiled);
            f.Fire("FreshDocumentReopen");
            Check(f.Window.CurrentAggregateIntent?.TiledContent != null, "Tiled SpawnZ timing uses actual saved tiled content.");
            Settle(f);
            ImmediateRawChange(f, "FreshSpawnZ");
        });
        WithFixture(root, "raw-timing-refresh", RefreshDoesNotLeakEvents);
        foreach (string selection in new[] { NativeFreshFamilyDocumentSessionCases.FamilyId, NativeFreshFamilyDocumentSessionCases.GrassId })
            WithFixture(root, "raw-timing-stage-" + (selection == NativeFreshFamilyDocumentSessionCases.FamilyId ? "family" : "native"),
                f => StageFromRetainedSelection(f, selection));
        Console.WriteLine($"FRESH_RAW_FIELD_TIMING_OK checks={checks} authoredTextFields=45 inactiveFamilyFields=26 sourceOpened=false nativeOutputWritten=false");
    }

    private static void ImmediateRawChange(F f, string name)
    {
        var box = f.Find<TextBox>(name);
        string? before = box.Text;
        var applied = f.Window.CurrentAggregateIntent;
        long generation = Generation(f);
        Check(!f.Window.HasUnappliedFields, "Timing probe begins from the applied raw form: " + name);
        try
        {
            // Deliberately NO pump/render between A, B and A. Avalonia documents
            // TextChanged as asynchronous; the raw fence must observe Text now.
            box.Text = before == "x" ? "y" : "x";
            Check(f.Window.HasUnappliedFields && Generation(f) > generation,
                "Dirty state and raw generation update synchronously before pump: " + name);
            long changed = Generation(f);
            box.Text = before;
            Check(!f.Window.HasUnappliedFields && Generation(f) > changed && Generation(f) >= generation + 2 &&
                ReferenceEquals(applied, f.Window.CurrentAggregateIntent),
                "Raw A-B-A is observable before pump without changing the applied owner: " + name);
            long restored = Generation(f);
            Settle(f);
            Check(Generation(f) == restored && !f.Window.HasUnappliedFields,
                "Rendered TextChanged notifications do not add a second latent invalidation: " + name);
        }
        finally { box.Text = before; Settle(f); }
    }

    private static void RefreshDoesNotLeakEvents(F f)
    {
        var applied = f.Window.CurrentAggregateIntent;
        var session = Session(f);
        var snapshot = session.Snapshot;
        foreach (string selected in new[] { NativeFreshFamilyDocumentSessionCases.FamilyId,
            NativeFreshFamilyDocumentSessionCases.GrassId, NativeFreshFamilyDocumentSessionCases.WizardId })
        {
            // Actual retained Session selection, followed by the actual owner's
            // WriteFields transaction. Its _refreshing guard must cover writes,
            // not leave notifications queued past that transaction's end.
            session.Select(selected);
            Invoke(f, "WriteFields");
            long generation = Generation(f);
            Check(!f.Window.HasUnappliedFields && session.SelectedEditorId == selected,
                "Owner refresh installed the selected complete raw form synchronously.");
            Settle(f);
            Check(Generation(f) == generation && ReferenceEquals(applied, f.Window.CurrentAggregateIntent) &&
                ReferenceEquals(snapshot, session.Snapshot) && !f.Window.HasUnappliedFields,
                "WriteFields produces no deferred raw-generation drift after its owner transaction.");

            ClickWithoutPump(f, "FreshDocumentApply");
            generation = Generation(f);
            Settle(f);
            Check(Generation(f) == generation && ReferenceEquals(applied, f.Window.CurrentAggregateIntent) &&
                !f.Window.HasUnappliedFields && !session.CanUndo && !session.CanRedo,
                "No-op Apply retains document/history and cannot enqueue later field invalidation.");
        }
        Check(f.SourceCalls == 0 && f.OwnerAttempts == 0, "Owner refresh and no-op Apply remain neutral editor work.");
    }

    private static void StageFromRetainedSelection(F f, string selected)
    {
        f.Select(selected); Settle(f);
        var applied = f.Window.CurrentAggregateIntent;
        var session = Session(f); var snapshot = session.Snapshot;
        object?[] family = FamilySnapshot(f);
        Check(family.Length == 26, "Complete inactive family snapshot includes every coordinate and reward mode.");
        if (selected == NativeFreshFamilyDocumentSessionCases.FamilyId)
            Check(f.Text("FreshFamilyReward2X") == "10454" && f.Find<ComboBox>("FreshFamilyReward2Mode").SelectedIndex == 1,
                "Family Stage begins with a real explicit WORLD destination that must not be cleared.");
        else Check(NativeFields.All(name => f.Text(name).Length != 0),
            "Existing-native Stage must actually clear four previously populated native fields.");
        var provider = new CatalogueMetadata();
        f.Window.MobyCatalogueProviderForTesting = provider;
        f.Fire("FreshNativeMobyBrowse");
        var task = f.Window.MobyCatalogueTaskForTesting ?? throw new InvalidDataException("Expected actual catalogue operation.");
        Check(!task.IsCompleted && f.AcquiredOwners == 1 && f.ReleaseAttempts == 0,
            "Actual catalogue chooser holds its editor owner.");
        RequireVisibleWithoutScrolling(f, ["FreshMobyCatalogueSearch", "FreshMobyCatalogueEntries"]);
        var list = f.Find<ListBox>("FreshMobyCatalogueEntries");
        list.SelectedItem = list.ItemsSource!.Cast<NativeFreshMobyCatalogueItem>().Single(); F.Pump();
        f.Fire("FreshMobyCatalogueStage"); F.Wait(task);
        Check(f.ReleaseAttempts == 1 && provider.Calls == 1 && !f.Window.MobyCatalogueBusyForTesting &&
            NativeFields.All(name => f.Text(name) == "" && f.Find<TextBox>(name).IsEffectivelyEnabled) &&
            FamilySnapshot(f).SequenceEqual(family) && session.SelectedEditorId == selected &&
            ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && ReferenceEquals(snapshot, session.Snapshot),
            "Stage opens one blank native form while retaining actual selection, owners and every inactive family field.");
        RequireVisibleWithoutScrolling(f, ["FreshNativeMobySourceIdentity", .. NativeFields]);
        long generation = Generation(f); Settle(f);
        Check(Generation(f) == generation, "Completed Stage leaves no late raw notification to invalidate its guarded reveal.");

        foreach (var value in new[] { ("FreshNativeMobyX", "9152"), ("FreshNativeMobyY", "9152"),
            ("FreshNativeMobyZ", "8704"), ("FreshNativeMobyYaw", "64") }) f.Set(value.Item1, value.Item2);
        f.Set("FreshFamilyReward5Z", "inactive-draft"); f.Fire("FreshDocumentApply");
        Check(ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && f.Text("FreshFamilyReward5Z") == "inactive-draft",
            "Even valid new native fields cannot discard a changed inactive family draft.");
        f.Fire("FreshNativeMobyCancelAdd");
        Check(ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && session.SelectedEditorId == selected &&
            FamilySnapshot(f).SequenceEqual(family) && !f.Window.HasUnappliedFields && !session.CanUndo,
            "Explicit Cancel restores the exact prior form without an added placement or history entry.");
        Check(f.Window.ResearchPublicationForTesting == null && f.NoImages,
            "Metadata Stage grants no preview-source or native publication authority.");
    }

    private static object?[] FamilySnapshot(F f) => [.. FamilyTextFields.Select(name => (object?)f.Find<TextBox>(name).Text),
        .. FamilyModes.Select(name => f.Find<ComboBox>(name).SelectedItem)];
    private static NativeFreshLevelDocumentSession Session(F f) => (NativeFreshLevelDocumentSession)
        typeof(NativeFreshDocumentWindow).GetField("_aggregateSession", Private)!.GetValue(f.Window)!;
    private static long Generation(F f) => (long)typeof(NativeFreshDocumentWindow)
        .GetField("_materialInspectionGeneration", Private)!.GetValue(f.Window)!;
    private static void Invoke(F f, string name) => typeof(NativeFreshDocumentWindow).GetMethod(name, Private)!.Invoke(f.Window, null);
    private static void ClickWithoutPump(F f, string name)
    { var button = f.Find<Button>(name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); }
    private static void Settle(F f)
    { f.Window.InvalidateVisual(); F.Pump(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); F.Pump(); }
    private static void RequireVisibleWithoutScrolling(F f, string[] names)
    {
        // No BringIntoView, focus request or test-assigned scroll offset. Only
        // settle actual layout/render callbacks, then inspect their outcome.
        Settle(f);
        List<string> clipped = [];
        foreach (string name in names)
        {
            var target = f.Find<Control>(name);
            var scroll = target.GetLogicalAncestors().OfType<ScrollViewer>().First();
            Point? top = target.TranslatePoint(default, scroll);
            if (!target.IsVisible || target.Bounds.Width <= 0 || target.Bounds.Height <= 0 || top is not { } point ||
                point.X < -1 || point.Y < -1 || point.X + target.Bounds.Width > scroll.Viewport.Width + 1 ||
                point.Y + target.Bounds.Height > scroll.Viewport.Height + 1)
                clipped.Add($"{name}: top={top}, size={target.Bounds.Size}, viewport={scroll.Viewport}, offset={scroll.Offset}");
        }
        Check(clipped.Count == 0, "Actual native catalogue/Stage reveals its complete controls without test scrolling. " + string.Join("; ", clipped));
    }
    private static void WithFixture(string root, string id, Action<F> test)
    {
        var f = new F(root, id, document: NativeFreshFamilyDocumentSessionCases.Document());
        List<Exception> failures = [];
        try { test(f); } catch (Exception error) { failures.Add(error); }
        finally
        {
            Attempt(() => f.Fire("FreshMobyCatalogueCancel"));
            Attempt(() => { if (f.Window.MobyCatalogueTaskForTesting is { } task) F.Drain(task); });
            Attempt(f.Close); Attempt(f.RequireProtected);
        }
        F.Throw(failures);
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidDataException(message); checks++; }
    private sealed class CatalogueMetadata : INativeFreshMobyCatalogueProvider
    {
        internal int Calls;
        public Task<NativeFreshMobyCatalogueListing> CaptureAsync(string image)
        {
            Calls++;
            return Task.FromResult(new NativeFreshMobyCatalogueListing(NativeMobySourceInventory.OriginalImageSha256,
                [new("timing-neutral-grass", "artisans", "Artisans", 10, 10, 121, null, 0x1F5,
                    NativeMobyCatalogueEntryKind.PlacedRow, "Neutral Grass", "Scenery", "Synthetic catalogue metadata only; no source owner.")]));
        }
    }
}
