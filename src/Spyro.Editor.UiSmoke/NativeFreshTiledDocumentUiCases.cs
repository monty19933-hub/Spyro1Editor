using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Actual schema4 Store + existing Window/session, with no source ROM or export.
internal static class NativeFreshTiledDocumentUiCases
{
    internal static void Run()
    {
        string root = Directory.CreateTempSubdirectory("spyro-tiled-ui-").FullName;
        NativeFreshDocumentWindow? window = null;
        bool passed = false; int checks = 0, sourceReads = 0, ownerAttempts = 0;
        try
        {
            var project = new EditorProjectLayout(root, "tiled-ui");
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"tiled-ui\"}\n");
            byte[] manifest = File.ReadAllBytes(project.ManifestPath);
            var floor = NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,
                new(80,128,112),new(128,96,64),19);
            NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), floor);
            byte[] legacy = File.ReadAllBytes(Path.Combine(root, NativeId65FreshDocumentStore.FileName));
            var original = NativeFreshLevelDocumentIntent.Create(floor, []);
            var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), original);
            byte[] originalBytes = File.ReadAllBytes(saved.Path);
            window = new(project, () => true, beginResearch: () => { ownerAttempts++; return new Owner(); });
            window.ResearchSourcePathsForTesting = () => { sourceReads++; throw new InvalidOperationException("Tiled UI must not read a ROM."); };
            window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
            window.Show(); Pump();
            Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; Pump();
            Click("FreshTilesUpgrade");
            var one = Current(); string firstId = one.TiledContent!.Tiles.Single().EditorId;
            Check(one.SchemaVersion == 4 && one.LegacyContent == null && one.SpawnX == 8960 && window.HasUnsavedChanges,
                "Explicit upgrade installs one active tile authority in the existing session.");
            Check(File.ReadAllBytes(saved.Path).SequenceEqual(originalBytes), "Upgrade does not save implicitly.");
            Click("FreshDocumentUndo"); Check(Current().SchemaVersion == 1, "Upgrade undo returns exact old schema.");
            Click("FreshDocumentRedo"); Check(Current().TiledContent!.Tiles.Single().EditorId == firstId, "Upgrade redo keeps stable tile ID.");
            Click("FreshTileAddAdjacent"); Click("FreshTileAddAdjacent");
            var three = Current(); string thirdId = three.TiledContent!.Tiles[2].EditorId;
            Check(three.TiledContent.Tiles.Select(t => t.X).SequenceEqual(new[] { 8448,10496,12544 }) &&
                SelectedTile() == thirdId && three.TiledContent.Tiles.Select(t => t.EditorId).Distinct().Count() == 3,
                "Explicit adjacent additions author three native-size tiles and select actual stable IDs.");
            Check(window.Preview.AggregateIntent == three && window.Preview.FloorSchematicBounds is { Width: > 0 },
                "Existing schematic consumes actual tiled document, not a fake legacy floor.");
            var projected = window.Preview.FloorSchematicBounds!.Value;
            Check(projected.Top >= 76 && projected.Bottom <= window.Preview.SchematicCaptionTop &&
                projected.Left >= 0 && projected.Right <= window.Preview.Bounds.Width,
                "Every projected tile remains inside the shared schematic and outside captions.");
            Set("FreshFloorColor", "#AA2211"); Set("FreshHeading", "80");
            Find<ComboBox>("FreshMaterial0Kind").SelectedItem = "Retail terrain record"; Pump();
            Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem = 70; Pump();
            Set("FreshMaterial0TextureId", "1"); Click("FreshDocumentApply");
            var colored = Current();
            Check(colored.TiledContent!.Tiles[2].FloorColor.R == 170 && colored.Heading == 80 &&
                colored.TiledContent.Tiles[0].FloorColor.R == 80 &&
                colored.TiledContent.Tiles[2].Materials.Triangle0 is NativeFreshRetailTerrainMaterialIntent { SourceWadEntry: 70, TextureId: 1 } &&
                colored.TiledContent.Tiles[0].Materials.Triangle0.Tag == "base-floor-color",
                "Selected tile geometry/materials and common settings apply atomically without changing other tiles.");
            Click("FreshDocumentUndo"); Check(ReferenceEquals(Current(), three) && SelectedTile() == thirdId, "Whole tile/settings undo preserves selection.");
            Click("FreshDocumentRedo"); Check(ReferenceEquals(Current(), colored), "Whole redo restores the same immutable document.");
            Find<ListBox>("FreshLevelTiles").SelectedItem = colored.TiledContent.Tiles[0]; Pump();
            Check(Find<ComboBox>("FreshMaterial0Kind").SelectedItem as string == "Base floor color",
                "Tile selection presents that tile's real material slots.");
            Find<ListBox>("FreshLevelTiles").SelectedItem = colored.TiledContent.Tiles[2]; Pump();
            Check(ReferenceEquals(Current(), colored) && Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem is 70,
                "Selection roundtrip restores material identity without a document edit.");
            Set("FreshFloorX", "8448"); Set("FreshHeading", "81"); Click("FreshDocumentApply");
            Check(ReferenceEquals(Current(), colored) && Find<TextBox>("FreshFloorX").Text == "8448",
                "Overlapping tile rejects all fields while retaining raw input and history.");
            Fire("FreshTileRemove"); Check(ReferenceEquals(Current(), colored), "Pending raw text blocks destructive tile edits.");
            Click("FreshDocumentUndo");
            Click("FreshWizardAdd");
            Set("FreshWizardOriginX", "13824"); Set("FreshWizardOriginY", "9216"); Set("FreshWizardOriginZ", "8909");
            Set("FreshWizardEndX", "13312"); Set("FreshWizardEndY", "9216"); Click("FreshDocumentApply");
            var withRoute = Current();
            Check(withRoute.SchemaVersion == 4 && withRoute.Placements.Single() is NativeFreshWizardRouteIntent { OriginX: 13824 },
                "Existing placement workflow accepts real tile-union positions outside the old Base without downgrade.");
            Click("FreshTileRemove");
            Check(ReferenceEquals(Current(), withRoute) && SelectedTile() == thirdId,
                "Removing the tile beneath an authored endpoint refuses atomically.");
            Click("FreshTileMoveUp");
            Check(Current().TiledContent!.Tiles[1].EditorId == thirdId && SelectedTile() == thirdId,
                "Tile ordering edits native identity order without regenerating IDs.");
            Click("FreshDocumentSave");
            byte[] persisted = File.ReadAllBytes(saved.Path);
            Check(!window.HasUnsavedChanges && NativeFreshLevelDocumentCodec.Parse(persisted).SchemaVersion == 4 &&
                !System.Text.Encoding.UTF8.GetString(persisted).Contains("\"base\"", StringComparison.Ordinal),
                "Actual Save/reopen writes only the schema4 active content.");
            Click("FreshDocumentReopen");
            Check(!window.HasUnsavedChanges && !Find<Button>("FreshDocumentUndo").IsEnabled && SelectedTile() == thirdId &&
                NativeFreshLevelDocumentCodec.SerializeCanonical(Current()).SequenceEqual(persisted),
                "Reopen retains tile selection by ID and resets history over actual persisted bytes.");
            Check(!Find<Button>("FreshDocumentResearch").IsEnabled &&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(Current()) &&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(Current()),
                "Neutral tiled editing cannot inherit an old single-floor export certificate.");
            Fire("FreshDocumentResearch"); Check(ownerAttempts == 0 && sourceReads == 0,
                "With a real owner/provider available, direct unsupported export refuses before acquiring either.");
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest) &&
                File.ReadAllBytes(Path.Combine(root,NativeId65FreshDocumentStore.FileName)).SequenceEqual(legacy),
                "Tiled Save preserves legacy floor and project manifest.");
            passed = true;
        }
        finally
        {
            window?.Close(); Pump();
            if (passed) Directory.Delete(root, recursive: true);
            else Console.WriteLine("Retained failed tiled UI fixture: " + root);
        }
        Console.WriteLine($"FRESH_TILED_DOCUMENT_UI_OK checks={checks} nativeWindow=false publication=false");

        NativeFreshLevelDocumentIntent Current() => window!.CurrentAggregateIntent!;
        string? SelectedTile() => (Find<ListBox>("FreshLevelTiles").SelectedItem as NativeFreshFlatTileIntent)?.EditorId;
        T Find<T>(string name) where T : Control => window!.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
        void Set(string name,string value) { Find<TextBox>(name).Text = value; Pump(); }
        void Fire(string name) { var b = Find<Button>(name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,b)); Pump(); }
        void Click(string name) { Check(Find<Button>(name).IsEffectivelyEnabled, "Enabled " + name); Fire(name); }
        void Check(bool value,string label) { if (!value) throw new InvalidDataException("Tiled UI: " + label); checks++; }
    }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private sealed class Owner : IDisposable { public void Dispose() { } }
}
