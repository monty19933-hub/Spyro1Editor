using System.Reflection;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;

// Actual headless controls and tiny saved aggregates. No successful source
// result, Candidate or Publication is fabricated by the delayed worker.
internal static class NativeFreshTiledFamilyReadinessCases
{
    private static int checks;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static NativeFreshLevelDocumentIntent Document()
    {
        var legacy = NativeFreshFamilyDocumentSessionCases.Document();
        var a = NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 14, 1);
        var b = NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 1);
        var aa = NativeFreshFloorMaterialsIntent.Create(a, a);
        var ab = NativeFreshFloorMaterialsIntent.Create(a, b);
        var bb = NativeFreshFloorMaterialsIntent.Create(b, b);
        return NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create([
            NativeFreshFlatTileIntent.Create(new('4', 32), 8448, 8448, 8704, 1536, 1536, new(80, 128, 112), aa),
            NativeFreshFlatTileIntent.Create(new('5', 32), 9984, 8448, 8704, 1536, 1536, new(80, 128, 112), ab),
            NativeFreshFlatTileIntent.Create(new('6', 32), 8448, 9984, 8704, 1536, 1536, new(80, 128, 112), ab),
            NativeFreshFlatTileIntent.Create(new('7', 32), 9984, 9984, 8704, 1536, 1536, new(80, 128, 112), bb)],
            8960, 8960, 9216, 64, new(128, 96, 64), 19), []).UpgradeForFamilies().WithPlacements(legacy.Placements);
    }

    internal static void Run(string parent)
    {
        checks = 0;
        Readiness(parent);
        ApplySaveAndPending(parent);
        foreach (string stage in new[] { "picker", "prepare" })
        foreach (string change in new[] { "tile-width-aba", "spawn-z-aba", "family-key", "reward-aba", "reward-mode-aba",
            "material-id-aba", "material-kind-aba", "tile-selection", "nonselected-material", "snapshot", "source", "disk" })
            Delayed(parent, stage, change);
        Console.WriteLine($"FRESH_TILED_FAMILY_READINESS_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
    }

    private static void Readiness(string parent)
    {
        var exact = Document(); var content = exact.TiledContent!;
        var chest = (NativeFreshLockedChestFamilyPlacementIntent)exact.Placements[2];
        Check(exact.SchemaVersion == 5 && content.Tiles.Count == 4 && exact.Placements.Count == 3 &&
            content.Tiles.SelectMany(t => new[] { t.Materials.Triangle0, t.Materials.Triangle1 })
                .Cast<NativeFreshRetailTerrainMaterialIntent>().Select(m => (m.SourceWadEntry, m.TextureId))
                .SequenceEqual(new[] { (14, 1), (14, 1), (14, 1), (70, 1), (14, 1), (70, 1), (70, 1), (70, 1) }),
            "The full row-major four-square fixture retains all eight material faces and the ordered family actors.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(null) &&
            NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(exact) &&
            NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(NativeFreshLevelDocumentCodec.Parse(Canonical(exact))),
            "Exact neutral tiled-family readiness survives a real canonical round trip, without source authority.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(exact) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(exact) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(exact) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(NativeFreshFamilyDocumentSessionCases.Document()),
            "The new predicate is separate from every old rectangle/P0 admission arm.");
        var last = content.Tiles[3];
        var near = new Dictionary<string, NativeFreshLevelDocumentIntent>
        {
            ["tile-order"] = exact.WithTiledContent(content.WithTiles([content.Tiles[1], content.Tiles[0], content.Tiles[2], last])),
            ["tile-size"] = Tile(NativeFreshFlatTileIntent.Create(last.EditorId, last.X, last.Y, last.Z, 1520, last.Depth, last.FloorColor, last.Materials)),
            ["material-face"] = Tile(NativeFreshFlatTileIntent.Create(last.EditorId, last.X, last.Y, last.Z, last.Width, last.Depth, last.FloorColor, F.Pair(70, 1, 14, 1))),
            ["material-source"] = Tile(NativeFreshFlatTileIntent.Create(last.EditorId, last.X, last.Y, last.Z, last.Width, last.Depth, last.FloorColor, F.Pair(70, 1, 70, 0))),
            ["actor-order"] = exact.WithPlacements([exact.Placements[1], exact.Placements[0], chest]),
            ["missing-grass"] = exact.WithPlacements([exact.Placements[0], chest]),
            ["default-reward"] = Family(chest.WithRewardDestinations(null)),
            ["reward-value"] = Family(chest.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create(
                [new(NativeFreshChestRewardRole.Reward2, 10455, 10503, 9216)]))),
            ["settings"] = exact.WithTiledContent(content.WithSettings(8960, 8960, 9216, 65, content.SkyColor, 19))
        };
        foreach (var item in near.Prepend(new KeyValuePair<string, NativeFreshLevelDocumentIntent>("exact", exact)))
        {
            bool supported = item.Key == "exact";
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(item.Value) == supported,
                "Complete neutral readiness rejects the authored near miss: " + item.Key);
            WithFixture(parent, "ready-" + item.Key, item.Value, f =>
            {
                string capability = f.Find<TextBlock>("FreshDocumentStatus").Text ?? "";
                Check(capability.Contains("Normal Create BIN is unavailable", StringComparison.Ordinal) &&
                    (supported ? capability.Contains("exact saved four-tile", StringComparison.Ordinal) &&
                        !capability.Contains("authored-only", StringComparison.Ordinal)
                        : capability.Contains("authored-only", StringComparison.Ordinal)),
                    "The visible capability explains exact tiled admission without claiming general native support.");
                int picked = 0; var work = new DelayedResearchWork();
                f.Window.SavedResearchWorkForTesting = work;
                f.Window.ResearchOutputPickerForTesting = _ => { picked++; return Task.FromResult<string?>(null); };
                var applied = f.Window.CurrentAggregateIntent; var snapshot = Session(f).Snapshot;
                Check(f.Find<Button>("FreshDocumentResearch").IsEnabled == supported, "Actual saved window readiness: " + item.Key);
                f.Fire("FreshDocumentResearch"); if (f.Window.ResearchTaskForTesting is { } task) F.Wait(task);
                Check(picked == (supported ? 1 : 0) && f.AcquiredOwners == picked && f.ReleaseAttempts == picked &&
                    work.PrepareCalls == 0 && work.PublishCalls == 0 && (supported ? f.SourceCalls > 0 : f.SourceCalls == 0),
                    "Unsupported direct clicks refuse before owner/source access; exact picker cancellation never prepares.");
                Check(ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && ReferenceEquals(snapshot, Session(f).Snapshot) &&
                    !f.Window.HasUnsavedChanges && f.Window.ResearchPublicationForTesting is null,
                    "Readiness never substitutes, applies, saves or publishes the complete authored document.");
            });
        }
        NativeFreshLevelDocumentIntent Tile(NativeFreshFlatTileIntent replacement) =>
            exact.WithTiledContent(content.WithTiles([content.Tiles[0], content.Tiles[1], content.Tiles[2], replacement]));
        NativeFreshLevelDocumentIntent Family(NativeFreshLockedChestFamilyPlacementIntent replacement) =>
            exact.WithPlacements([exact.Placements[0], exact.Placements[1], replacement]);
    }

    private static void ApplySaveAndPending(string parent)
    {
        var exact = Document(); var t = exact.TiledContent!;
        WithFixture(parent, "apply-save", exact.WithTiledContent(t.WithSettings(8960, 8960, 9216, 65, t.SkyColor, 19)), f =>
        {
            f.Select(NativeFreshFamilyDocumentSessionCases.FamilyId); SelectTile(f, 1);
            var saved = Session(f).Snapshot; var old = f.Window.CurrentAggregateIntent;
            f.Set("FreshHeading", "64");
            Check(f.Window.HasUnappliedFields && !f.Find<Button>("FreshDocumentResearch").IsEnabled &&
                ReferenceEquals(old, f.Window.CurrentAggregateIntent), "Raw edits are not applied saved eligibility.");
            f.Fire("FreshDocumentApply");
            Check(!f.Window.HasUnappliedFields && f.Window.HasUnsavedChanges &&
                NativeFreshSavedLevelResearchEligibility.IsSupportedTiledFamilyProfile(f.Window.CurrentAggregateIntent) &&
                ReferenceEquals(saved, Session(f).Snapshot) && !f.Find<Button>("FreshDocumentResearch").IsEnabled,
                "Even an exact applied profile remains gated while its actual saved version differs.");
            f.Fire("FreshDocumentResearch");
            Check(f.AcquiredOwners == 0 && f.SourceCalls == 0, "Dirty direct research cannot start source work.");
            f.Fire("FreshDocumentSave"); f.ExpectedDocument = Canonical(exact);
            Check(!f.Window.HasUnsavedChanges && !ReferenceEquals(saved, Session(f).Snapshot) &&
                f.Find<Button>("FreshDocumentResearch").IsEnabled && File.ReadAllBytes(f.DocumentPath).SequenceEqual(f.ExpectedDocument),
                "Only real Save makes the complete exact document research-ready.");
            var applied = f.Window.CurrentAggregateIntent;
            f.Set("FreshMaterial1TextureId", "01");
            Check(f.Window.HasUnappliedFields && !f.Find<Button>("FreshDocumentResearch").IsEnabled,
                "Selected-tile material raw spelling must be explicitly applied, even when numerically equal.");
            f.Fire("FreshDocumentResearch"); f.Fire("FreshDocumentApply");
            Check(!f.Window.HasUnsavedChanges && f.Find<Button>("FreshDocumentResearch").IsEnabled &&
                Canonical(f.Window.CurrentAggregateIntent!).SequenceEqual(Canonical(applied!)) && f.AcquiredOwners == 0,
                "Explicit equal-value Apply clears raw draft without source work or another saved version.");
            f.Fire("FreshFamilyAdd");
            Check(f.Window.HasUnappliedFields && !f.Find<Button>("FreshDocumentResearch").IsEnabled,
                "A pending family cannot borrow readiness from the previously saved exact recipe.");
            f.Fire("FreshDocumentResearch");
            Check(f.AcquiredOwners == 0 && f.SourceCalls == 0, "Pending Add direct research remains blocked.");
            f.Fire("FreshFamilyCancelAdd");
        });
    }

    private static void Delayed(string parent, string stage, string change)
    {
        WithFixture(parent, stage + "-" + change, Document(), f =>
        {
            f.Select(NativeFreshFamilyDocumentSessionCases.FamilyId); SelectTile(f, 1);
            var applied = f.Window.CurrentAggregateIntent!; var snapshot = Session(f).Snapshot;
            var content = applied.TiledContent!;
            var tileId = Session(f).SelectedTileId; long generation = Generation(f);
            var work = new DelayedResearchWork(); var picker = new TaskCompletionSource<string?>(); Task? task = null;
            string output = Path.Combine(f.Project.RootPath, "never-created-tiled-family-output");
            try
            {
                f.Window.SavedResearchWorkForTesting = work;
                f.Window.ResearchOutputPickerForTesting = _ => stage == "picker" ? picker.Task : Task.FromResult<string?>(output);
                f.Fire("FreshDocumentResearch"); task = f.Window.ResearchTaskForTesting;
                Check(task is not null && !task.IsCompleted && f.Window.ResearchBusyForTesting &&
                    f.AcquiredOwners == 1 && f.ReleaseAttempts == 0, "Actual owner spans the " + stage + " await.");
                if (stage == "prepare")
                {
                    F.Wait(work.Entered.Task);
                    Check(ReferenceEquals(work.Snapshot, snapshot) && ReferenceEquals(f.Window.SavedResearchSnapshotForTesting, snapshot) &&
                        Canonical(work.Snapshot!.Intent!).SequenceEqual(Canonical(applied)),
                        "Prepare receives the actual complete saved four-tile/family snapshot, not a projected floor.");
                }
                foreach (string button in new[] { "FreshDocumentApply", "FreshDocumentSave", "FreshDocumentReopen", "FreshTileAddAdjacent", "FreshTileRemove", "FreshFamilyAdd" })
                { Check(!f.Find<Button>(button).IsEnabled, "Busy direct action is disabled: " + button); f.Fire(button); }
                var tiles = f.Find<ListBox>("FreshLevelTiles");
                Check(!tiles.IsEnabled, "Busy tile selection is disabled.");
                tiles.SelectedItem = content.Tiles[3]; F.Pump();
                Check(Session(f).SelectedTileId == tileId && (tiles.SelectedItem as NativeFreshFlatTileIntent)?.EditorId == tileId,
                    "A direct busy tile-selection event restores the actual selected owner.");
                foreach (string field in new[] { "FreshFloorWidth", "FreshSpawnZ", "FreshFamilyKeyX", "FreshMaterial1TextureId" })
                    Check(!f.Find<TextBox>(field).IsEffectivelyEnabled, "Busy raw field is disabled: " + field);

                string? rawField = change switch { "tile-width-aba" => "FreshFloorWidth", "spawn-z-aba" => "FreshSpawnZ",
                    "family-key" => "FreshFamilyKeyX", "reward-aba" => "FreshFamilyReward2X", "material-id-aba" => "FreshMaterial1TextureId", _ => null };
                string? expectedRaw = null;
                if (rawField != null)
                {
                    string before = f.Text(rawField); f.Set(rawField, "0" + before);
                    if (change.EndsWith("-aba", StringComparison.Ordinal)) f.Set(rawField, before);
                    expectedRaw = f.Text(rawField);
                }
                else if (change is "reward-mode-aba" or "material-kind-aba")
                {
                    string name = change == "reward-mode-aba" ? "FreshFamilyReward5Mode" : "FreshMaterial1Kind";
                    var combo = f.Find<ComboBox>(name); object? before = combo.SelectedItem;
                    combo.SelectedItem = combo.ItemsSource!.Cast<object>().First(v => !Equals(v, before)); F.Pump();
                    combo.SelectedItem = before; F.Pump();
                    Check(Equals(combo.SelectedItem, before), "Raw selector ABA restored its exact final value.");
                }
                else if (change == "tile-selection") Session(f).SelectTile(content.Tiles[3].EditorId);
                else if (change == "nonselected-material")
                {
                    var t = content; var last = t.Tiles[3];
                    var replacement = NativeFreshFlatTileIntent.Create(last.EditorId, last.X, last.Y, last.Z,
                        last.Width, last.Depth, last.FloorColor, F.Pair(70, 1, 14, 1));
                    Session(f).Apply(applied.WithTiledContent(t.WithTiles([t.Tiles[0], t.Tiles[1], t.Tiles[2], replacement])));
                }
                else if (change == "snapshot") Session(f).Save();
                else if (change == "source") f.SelectedSource = (f.SelectedSource.Image + ".changed", f.SelectedSource.Cue);
                else if (change == "disk") { File.AppendAllText(f.DocumentPath, " "); f.ExpectedDocument = File.ReadAllBytes(f.DocumentPath); }
                if (rawField != null || change.EndsWith("-aba", StringComparison.Ordinal))
                    Check(Generation(f) > generation, "Raw tile/material/family edits, including ABA, invalidate the asynchronous ticket.");
                if (change.EndsWith("-aba", StringComparison.Ordinal))
                    Check(!f.Window.HasUnsavedChanges && ReferenceEquals(applied, f.Window.CurrentAggregateIntent) &&
                        ReferenceEquals(snapshot, Session(f).Snapshot), "Equal final raw ABA is stale even with clean unchanged document/saved owners.");
                if (change == "tile-selection") Check(Generation(f) == generation && ReferenceEquals(snapshot, Session(f).Snapshot) &&
                    ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && !f.Window.HasUnsavedChanges,
                    "Selected tile identity drift is isolated from raw generation, saved version and applied document.");
                var expectedApplied = f.Window.CurrentAggregateIntent; var expectedSnapshot = Session(f).Snapshot;
                Check(!task!.IsCompleted && f.ReleaseAttempts == 0, "Invalidation does not abandon outstanding work or release its owner early.");
                picker.TrySetResult(output); work.Released.TrySetResult(); F.Wait(task);
                Check(f.ReleaseAttempts == 1 && !f.Window.ResearchBusyForTesting && work.PublishCalls == 0 &&
                    f.Window.SavedResearchFailureForTesting is InvalidDataException && f.Window.ResearchPublicationForTesting is null,
                    "Stale " + change + " is refused after the await and before publication.");
                Check(work.PrepareCalls == (stage == "picker" ? 0 : 1) &&
                    ReferenceEquals(expectedApplied, f.Window.CurrentAggregateIntent) && ReferenceEquals(expectedSnapshot, Session(f).Snapshot) &&
                    (rawField == null || f.Text(rawField) == expectedRaw) && !Directory.Exists(output) && !File.Exists(output) && f.NoImages,
                    "Stale completion preserves exact caller state; picker drift cannot even begin preparation.");
            }
            finally { picker.TrySetResult(null); work.Released.TrySetResult(); if (task != null) F.Drain(task); }
        });
    }

    private static void WithFixture(string parent, string id, NativeFreshLevelDocumentIntent document, Action<F> run)
    {
        id = "tiled-family-" + id;
        if (Directory.Exists(Path.Combine(parent, id)) || File.Exists(Path.Combine(parent, id)))
            throw new InvalidDataException("Tiled family UI fixtures must be new; existing data is never reused.");
        // F owns a separately protected legacy floor and calls Initial.Base.
        // Save/Reopen the actual tiled aggregate instead of fabricating a legacy
        // projection or replacing the Window's private session for setup.
        var f = new F(parent, id, document: NativeFreshFamilyDocumentSessionCases.Document());
        List<Exception> failures = [];
        try
        {
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project), document);
            f.ExpectedDocument = Canonical(document); f.Fire("FreshDocumentReopen");
            Check(f.Window.CurrentAggregateIntent is { SchemaVersion: 5, TiledContent: not null } &&
                Canonical(f.Window.CurrentAggregateIntent!).SequenceEqual(f.ExpectedDocument) && !f.Window.HasUnsavedChanges,
                "Real Store/Reopen retains the entire tiled schema5 document and protected independent legacy owner.");
            run(f);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { f.Close(); } catch (Exception error) { failures.Add(error); }
            try { f.RequireProtected(); } catch (Exception error) { failures.Add(error); }
            try { Check(!File.Exists(f.SelectedSource.Image) && !File.Exists(f.SelectedSource.Cue), "No native source fixture was opened or created."); }
            catch (Exception error) { failures.Add(error); }
        }
        F.Throw(failures);
    }

    private static void SelectTile(F f, int index)
    { f.Find<ListBox>("FreshLevelTiles").SelectedItem = f.Window.CurrentAggregateIntent!.TiledContent!.Tiles[index]; F.Pump(); }
    private static NativeFreshLevelDocumentSession Session(F f) =>
        (NativeFreshLevelDocumentSession)typeof(NativeFreshDocumentWindow).GetField("_aggregateSession", Private)!.GetValue(f.Window)!;
    private static long Generation(F f) =>
        (long)typeof(NativeFreshDocumentWindow).GetField("_materialInspectionGeneration", Private)!.GetValue(f.Window)!;
    private static byte[] Canonical(NativeFreshLevelDocumentIntent document) => NativeFreshLevelDocumentCodec.SerializeCanonical(document);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }

    // Same private delay/failing-publication seam as FamilyAsyncReadinessCases:
    // Prepare has no source result to forge, and Publish can never issue one.
    private sealed class DelayedResearchWork : INativeFreshSavedLevelResearchUiWork
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal NativeFreshLevelDocumentSnapshot? Snapshot;
        internal int PrepareCalls, PublishCalls;
        public async Task PrepareAsync(NativeFreshLevelDocumentSnapshot snapshot, string image, string cue, CancellationToken token)
        { PrepareCalls++; Snapshot = snapshot; Entered.TrySetResult(); await Released.Task; }
        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string output, CancellationToken token)
        { PublishCalls++; return Task.FromException<NativeId65FreshDiscResearchPublication>(new IOException("No native publication exists in this neutral UI test.")); }
    }
}
