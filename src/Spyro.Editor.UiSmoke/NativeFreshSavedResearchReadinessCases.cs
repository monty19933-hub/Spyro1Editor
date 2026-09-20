using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = NativeFreshMaterialInspectionUiFixture;

// Genuine tiny Store documents only; the picker cancels before source preparation.
internal static class NativeFreshSavedResearchReadinessCases
{
    internal static void Run(string parent)
    {
        int checks = 0;
        var unsupportedFamilies = SavedFamilyDocumentFixture.Unsupported().ToDictionary(
            item => "family-" + item.Name, item => item.Document, StringComparer.Ordinal);
        var unsupportedTiles = SavedTiledDocumentFixture.Unsupported().ToDictionary(
            item => "tiled-" + item.Name, item => item.Document, StringComparer.Ordinal);
        var supportedMaterials = new Dictionary<string, NativeFreshLevelDocumentIntent>(StringComparer.Ordinal)
        {
            ["tiled-mat"] = SavedTiledMaterialDocumentFixture.Document(),
            ["tiled-mat-mixed"] = SavedTiledMaterialDocumentFixture.Document([0, 1, 255, 1, 0, 255]),
            ["tiled-mat-nonselected"] = SavedTiledMaterialDocumentFixture.Document([255, 255, 0, 1, 0, 1]),
            ["tiled-mat-ffc"] = SavedTiledMaterialDocumentFixture.BoundaryDocument(56),
            ["tiled-mat-full"] = SavedTiledMaterialDocumentFixture.BoundaryDocument(58),
            ["tiled-mat-one"] = SavedTiledMaterialDocumentFixture.Document([0, 255, 0, 255, 0, 255]),
            ["tiled-mat-third"] = SavedTiledMaterialDocumentFixture.Document([0, 1, 0, 1, 0, 2]),
            ["tiled-mat-selected-one"] = SelectedMaterialDocument([0, 0, 0, 0, 0, 0]),
            ["tiled-mat-selected-three"] = SelectedMaterialDocument([0, 1, 2, 0, 1, 2]),
            ["tiled-mat-selected-three-mixed"] = SelectedMaterialDocument([0, 1, 255, 2, 0, 255]),
            ["tiled-mat-selected-three-reordered"] = SelectedMaterialDocument([2, 0, 1, 2, 255, 0])
        };
        // These are all single-source material documents, including a choice
        // only on the unselected third tile. They remain invalid for the Base
        // predicate but must no longer be blanket UI research refusals.
        foreach (string kind in unsupportedTiles.Keys.Where(k => k.EndsWith("-retail", StringComparison.Ordinal)).ToArray())
        {
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(unsupportedTiles[kind]),
                "Selected material admission does not widen the Base-only predicate: " + kind);
            supportedMaterials.Add(kind, unsupportedTiles[kind]);
            unsupportedTiles.Remove(kind);
        }
        var unsupportedMaterials = new Dictionary<string, NativeFreshLevelDocumentIntent>(StringComparer.Ordinal)
        {
            ["tiled-mat-over"] = SavedTiledMaterialDocumentFixture.BoundaryDocument(60),
            ["tiled-mat-wizard"] = SavedTiledMaterialDocumentFixture.Document().WithPlacements([
                NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8704, 13568, 8960)]),
            ["tiled-mat-native"] = SavedTiledMaterialDocumentFixture.Document().WithPlacements([
                NativeFreshMobyPlacementIntent.Create(new string('b', 32),
                    NativeFreshRetailMobyReference.Create("spyro1-usa-retail-mode2-2352-v1", 10, 121, 501),
                    13056, 8960, 8704, 64)]),
            ["tiled-mat-selected-one-wizard"] = SelectedMaterialDocument([0, 0, 0, 0, 0, 0]).WithPlacements([
                NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8704, 13568, 8960)]),
            ["tiled-mat-selected-three-native"] = SelectedMaterialDocument([0, 1, 2, 0, 1, 2]).WithPlacements([
                NativeFreshMobyPlacementIntent.Create(new string('b', 32),
                    NativeFreshRetailMobyReference.Create("spyro1-usa-retail-mode2-2352-v1", 10, 121, 501),
                    13056, 8960, 8704, 64)])
        };
        foreach (var (kind, expected) in new[]
        {
            ("tiled-mat-selected-one", new[] { (14, 0) }),
            ("tiled-mat-selected-three", new[] { (14, 0), (70, 0), (14, 1) }),
            ("tiled-mat-selected-three-mixed", new[] { (14, 0), (70, 0), (14, 1) }),
            ("tiled-mat-selected-three-reordered", new[] { (14, 1), (14, 0), (70, 0) })
        })
            Check(supportedMaterials[kind].TiledContent!.Tiles.SelectMany(t => new[] { t.Materials.Triangle0, t.Materials.Triangle1 })
                .OfType<NativeFreshRetailTerrainMaterialIntent>().Select(m => (m.SourceWadEntry, m.TextureId)).Distinct().SequenceEqual(expected),
                "Actual selected fixture preserves ordered first-occurrence descriptors across all tiles and Base faces: " + kind);
        foreach (var supported in supportedMaterials.Values)
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(supported) &&
                NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(NativeFreshLevelDocumentCodec.Parse(
                    NativeFreshLevelDocumentCodec.SerializeCanonical(supported))) &&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(supported),
                "Material readiness examines all faces and full allocation, without widening the Base-only profile.");
        foreach (var unsupported in unsupportedMaterials.Values)
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(unsupported),
                "Selected material readiness retains both placement-tag refusals and actual complete allocation overflow.");
        foreach (var supported in new[] { SavedTiledDocumentFixture.Document(), SavedTiledDocumentFixture.BoundaryDocument(44),
            SavedTiledDocumentFixture.BoundaryDocument(46) })
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(supported) &&
                NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(NativeFreshLevelDocumentCodec.Parse(
                    NativeFreshLevelDocumentCodec.SerializeCanonical(supported))),
                "Neutral tiled readiness accepts actual complete canonical geometry through data1000 without granting source or publication authority.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(null) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(Document("retail")) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(SavedFamilyDocumentFixture.Document()),
            "The tiled predicate cannot project or adopt either legacy research arm.");
        foreach (var (kind, unsupported) in unsupportedTiles)
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(unsupported),
                "Every tile face, every placement tag and actual allocation participate in readiness: " + kind);
        foreach (string kind in new[] { "retail", "retail-t2", "schema1", "base-color", "mixed", "wizard", "dirty", "no-owner", "no-source", "owner-null", "owner-throw",
            "mixed-14-70", "mixed-70-14", "mixed-14-14", "mixed-route", "mixed-base", "mixed-music", "mixed-t2", "mixed-pair", "mixed-two-wizards", "mixed-nonretail",
            "mixed-dirty", "mixed-no-owner", "mixed-no-source", "mixed-owner-null", "mixed-owner-throw",
            "family", "family-dirty", "family-native-dirty", "family-no-owner", "family-no-source", "family-owner-null", "family-owner-throw",
            "tiled", "tiled-data1000", "tiled-dirty", "tiled-tile-dirty", "tiled-no-owner", "tiled-no-source", "tiled-owner-null", "tiled-owner-throw",
            "tiled-mat-dirty", "tiled-mat-no-owner", "tiled-mat-no-source", "tiled-mat-owner-null", "tiled-mat-owner-throw" }
            .Concat(unsupportedFamilies.Keys).Concat(unsupportedTiles.Keys)
            .Concat(supportedMaterials.Keys).Concat(unsupportedMaterials.Keys))
        {
            string root = Path.Combine(parent, "saved-research-ready-" + kind);
            Directory.CreateDirectory(root);
            var project = new EditorProjectLayout(root, "saved-research-ready-" + kind);
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + project.ProjectId + "\",\"sentinel\":true}");
            bool family = kind.StartsWith("family", StringComparison.Ordinal);
            bool tiled = kind.StartsWith("tiled", StringComparison.Ordinal);
            bool tiledMaterial = kind.StartsWith("tiled-mat", StringComparison.Ordinal);
            var intent = unsupportedFamilies.TryGetValue(kind, out var unsupported) ? unsupported :
                unsupportedTiles.TryGetValue(kind, out var unsupportedTile) ? unsupportedTile :
                unsupportedMaterials.TryGetValue(kind, out var unsupportedMaterial) ? unsupportedMaterial :
                supportedMaterials.TryGetValue(kind, out var supportedMaterial) ? supportedMaterial :
                tiledMaterial ? SavedTiledMaterialDocumentFixture.Document() :
                family ? SavedFamilyDocumentFixture.Document() : kind == "tiled-data1000" ? SavedTiledDocumentFixture.BoundaryDocument(46) :
                tiled ? SavedTiledDocumentFixture.Document() : Document(kind);
            bool dirty = kind is "dirty" or "mixed-dirty" or "family-dirty" or "family-native-dirty" or "tiled-dirty" or "tiled-tile-dirty" or "tiled-mat-dirty";
            bool noOwner = kind is "no-owner" or "mixed-no-owner" or "family-no-owner" or "tiled-no-owner" or "tiled-mat-no-owner";
            bool noSource = kind is "no-source" or "mixed-no-source" or "family-no-source" or "tiled-no-source" or "tiled-mat-no-source";
            bool nullOwner = kind is "owner-null" or "mixed-owner-null" or "family-owner-null" or "tiled-owner-null" or "tiled-mat-owner-null";
            bool throwingOwner = kind is "owner-throw" or "mixed-owner-throw" or "family-owner-throw" or "tiled-owner-throw" or "tiled-mat-owner-throw";
            bool ready = kind is "retail" or "retail-t2" or "mixed-14-70" or "mixed-70-14" or "mixed-14-14" or "family" or "tiled" or "tiled-data1000" ||
                supportedMaterials.ContainsKey(kind) || nullOwner || throwingOwner;
            bool reachesPicker = ready && !nullOwner && !throwingOwner;
            string dirtyField = kind switch { "family-native-dirty" => "FreshNativeMobyX", "tiled-dirty" => "FreshSpawnZ", "tiled-tile-dirty" => "FreshFloorX", "tiled-mat-dirty" => "FreshMaterial0TextureId", _ => "FreshHeading" };
            string dirtyValue = kind switch { "family-native-dirty" => "09152", "tiled-dirty" => "09216", "tiled-tile-dirty" => "012544",
                "mixed-dirty" or "family-dirty" => "0064", "tiled-mat-dirty" => "00", _ => "0128" };
            // This inactive floor is an independent sentinel, never a projected
            // legacy authority manufactured from the schema4 document.
            var legacyIntent = tiled ? Document("retail").Base : intent.Base;
            var old = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), legacyIntent);
            var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), intent);
            byte[] manifest = File.ReadAllBytes(project.ManifestPath), legacy = File.ReadAllBytes(old.Path), aggregate = File.ReadAllBytes(saved.Path);
            NativeFreshDocumentWindow? window = null;
            List<Exception> failures = [];
            int ownerAttempts = 0, acquired = 0, released = 0, sourceSelections = 0, pickers = 0;
            try
            {
                Func<(string, string)> source = () => { sourceSelections++; return (Path.Combine(root, "never-open.bin"), Path.Combine(root, "never-open.cue")); };
                Func<IDisposable> begin = () =>
                {
                    ownerAttempts++;
                    if (nullOwner) return null!;
                    if (throwingOwner) throw new IOException("exact-UI-owner-acquisition-simulation");
                    acquired++; return new Owner(() => released++);
                };
                window = new(project, () => true, noSource ? null : source, noOwner ? null : begin);
                window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
                window.ResearchOutputPickerForTesting = _ => { pickers++; return Task.FromResult<string?>(null); };
                window.Show(); F.Pump();
                Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; F.Pump();
                if (kind == "family-native-dirty")
                {
                    Find<ListBox>("FreshLevelPlacements").SelectedItem = window.CurrentAggregateIntent!.Placements[1]; F.Pump();
                    Check(Find<TextBox>(dirtyField).Text == "9152", "Native dirty control starts from the actual saved Grass row.");
                }
                if (kind == "tiled-tile-dirty")
                {
                    Find<ListBox>("FreshLevelTiles").SelectedItem = window.CurrentAggregateIntent!.TiledContent!.Tiles[2]; F.Pump();
                    Check(Find<TextBox>(dirtyField).Text == "12544", "Tiled dirty control edits the actual selected third tile.");
                }
                if (tiled)
                    Check(window.CurrentAggregateIntent!.LegacyContent == null && legacyIntent.FloorWidth == 1024 &&
                        (Find<ListBox>("FreshLevelTiles").SelectedItem as NativeFreshFlatTileIntent)?.EditorId ==
                            intent.TiledContent!.Tiles[kind == "tiled-tile-dirty" ? 2 : 0].EditorId,
                        "Whole tiled intent and actual selection remain distinct from the independent inactive legacy sentinel.");
                var actual = window.CurrentAggregateIntent;
                if (supportedMaterials.ContainsKey(kind))
                    Check(NativeFreshLevelDocumentCodec.SerializeCanonical(actual!).SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(intent)),
                        "Saved readiness retains every actual tile, stable ID, ordered material/Base choice and common setting; no two-source projection.");
                foreach (string text in new[] { Find<TextBlock>("FreshLevelPlacementSummary").Text!, Find<TextBlock>("FreshDocumentStatus").Text! })
                {
                    if (tiled) continue; // Capability wording is not the readiness authority.
                    if (!family) Check(text.Contains("no placements or the supported one-Wizard mixed profile", StringComparison.Ordinal),
                        "Old placement summary and saved-open message retain both old bounded research arms.");
                    else if (unsupported == null) Check(text.Contains("exact saved Wizard + Grass profile", StringComparison.Ordinal) &&
                        text.Contains("Normal Create BIN", StringComparison.Ordinal) && !text.Contains("native dependencies are unsupported", StringComparison.Ordinal),
                        "Exact family summary/open text distinguishes bounded research from normal export and generic native imports.");
                    else Check(text.Contains("authored-only", StringComparison.Ordinal) && text.Contains("Normal Create BIN", StringComparison.Ordinal),
                        "Other native documents remain authored-only in summary and saved-open text: " + kind);
                }
                if (dirty) { Find<TextBox>(dirtyField).Text = dirtyValue; F.Pump(); }
                var button = Find<Button>("FreshDocumentResearch");
                Check(button.IsEnabled == ready, "Exact saved old-arm or bounded family-profile readiness: " + kind);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); F.Pump();
                if (window.ResearchTaskForTesting is { } task) F.Wait(task);
                Check(ReferenceEquals(actual, window.CurrentAggregateIntent) && window.ResearchPublicationForTesting == null,
                    "Readiness/cancel never applies or fabricates a publication.");
                Check(acquired == (reachesPicker ? 1 : 0) && released == acquired && pickers == acquired,
                    "Only supported clean arm acquires owner and picker; actual owner releases once.");
                Check(ownerAttempts == (ready ? 1 : 0),
                    "Owner invocation is distinct from an actually returned owner.");
                Check(reachesPicker ? sourceSelections > 0 : sourceSelections == 0,
                    "Unsupported/dirty direct click refuses before source provider.");
                if (dirty) Check(Find<TextBox>(dirtyField).Text == dirtyValue && window.HasUnappliedFields,
                    "Unapplied raw spelling remains untouched.");
            }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                Attempt(() => { if (window?.ResearchTaskForTesting is { } task) F.Drain(task); });
                Attempt(() => { window?.Close(); F.Pump(); });
                Attempt(() => Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "Complete manifest unchanged."));
                Attempt(() => Check(File.ReadAllBytes(old.Path).SequenceEqual(legacy), "Inactive legacy unchanged."));
                Attempt(() => Check(File.ReadAllBytes(saved.Path).SequenceEqual(aggregate), "Saved aggregate unchanged."));
                Attempt(() => Check(acquired == released, "Every acquired UI owner attempted."));
                Attempt(() => Check(!File.Exists(Path.Combine(root, "never-open.bin")) && !File.Exists(Path.Combine(root, "never-open.cue")),
                    "No native source fixture or output created."));
            }
            F.Throw(failures);
            T Find<T>(string name) where T : Control => window!.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        }
        Console.WriteLine($"FRESH_SAVED_RESEARCH_READINESS_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
    }

    // The same actual source descriptors used by the independent selected-N
    // source fixtures. This is neutral intent, never a source-certified plan.
    internal static NativeFreshLevelDocumentIntent SelectedMaterialDocument(int[] faces)
    {
        if (faces.Length != 6) throw new ArgumentException("All six selected tile faces are explicit.", nameof(faces));
        var document = SavedTiledDocumentFixture.Document();
        return document.WithTiledContent(document.TiledContent!.WithTiles(document.TiledContent.Tiles.Select((tile, index) =>
            NativeFreshFlatTileIntent.Create(tile.EditorId, tile.X, tile.Y, tile.Z, tile.Width, tile.Depth, tile.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(Material(faces[2 * index]), Material(faces[2 * index + 1]))))));
        static NativeFreshFloorMaterialIntent Material(int slot) => slot switch
        {
            0 => NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 14, 0),
            1 => NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 0),
            2 => NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 14, 1),
            255 => NativeFreshFloorMaterialIntent.BaseFloorColor(),
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }

    private static NativeFreshLevelDocumentIntent Document(string kind)
    {
        bool mixed = kind.StartsWith("mixed-", StringComparison.Ordinal);
        var floor = mixed
            ? NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, kind == "mixed-base" ? 1920 : 2048, 2048, 8960, 8960, 64,
                new(80, 128, 112), new(128, 96, 64), kind == "mixed-music" ? 20 : 19)
            : NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 128,
                new(80, 128, 112), new(128, 96, 64), 20);
        NativeFreshPlacementIntent[] placements = mixed
            ? [NativeFreshWizardRouteIntent.Create(F.A, 9984, 9984, 8704, kind == "mixed-route" ? 9456 : 9472, 9984)]
            : kind == "wizard" ? [NativeFreshWizardRouteIntent.Create(F.A, 9216, 9216, 8704, 9088, 9216)] : [];
        if (kind == "mixed-two-wizards")
            placements = [.. placements, NativeFreshWizardRouteIntent.Create(F.B, 9728, 9984, 8704, 9472, 9984)];
        var plain = NativeFreshLevelDocumentIntent.Create(floor, placements);
        var pair = kind switch
        {
            "base-color" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()),
            "mixed" or "mixed-nonretail" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 1)),
            "retail-t2" or "mixed-t2" => F.Pair(14, 2, 70, 1),
            "mixed-70-14" => F.Pair(70, 1, 14, 1),
            "mixed-14-14" => F.Pair(14, 1, 14, 1),
            "mixed-pair" => F.Pair(70, 1, 70, 1),
            _ => F.Pair(14, 1, 70, 1)
        };
        return kind == "schema1" ? plain : plain.WithFloorMaterials(pair);
    }

    private sealed class Owner(Action release) : IDisposable
    {
        private bool attempted;
        public void Dispose()
        {
            if (attempted) throw new InvalidDataException("Repeated actual UI owner release.");
            attempted = true; release();
        }
    }
}
