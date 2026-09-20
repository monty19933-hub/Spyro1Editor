using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Neutral session/history and actual tiny Store fixtures only. No source-owned
// compilation, window admission, native outputs or gameplay claims.
internal static class NativeFreshFamilyDocumentSessionCases
{
    internal const string WizardId = "11111111111111111111111111111111";
    internal const string GrassId = "22222222222222222222222222222222";
    internal const string FamilyId = "33333333333333333333333333333333";
    private const string TileA = "44444444444444444444444444444444", TileB = "55555555555555555555555555555555";
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private static int checks;
    private static string root = "";

    // Shared neutral R03 authorship fixture; this factory does not resolve any
    // source bytes or certify the documented runtime experiment.
    internal static NativeFreshLevelDocumentIntent Document()
    {
        var basis = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 3072, 3072,
            8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
        var materials = NativeFreshFloorMaterialsIntent.Create(
            NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, 1),
            NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 70, 1));
        return NativeFreshLevelDocumentIntent.Create(basis, []).WithFloorMaterials(materials).UpgradeForFamilies()
            .WithPlacements([
                NativeFreshWizardRouteIntent.Create(WizardId, 9984, 9984, 8704, 9472, 9984),
                NativeFreshMobyPlacementIntent.Create(GrassId, NativeFreshRetailMobyReference.Create(Variant, 10, 121, 0x1F5),
                    9152, 9152, 8704, 64),
                NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId, Source(),
                    9728, 9120, 8704, 10864, 9888, 8704, OneOverride())]);
    }

    internal static NativeFreshLevelDocumentIntent TiledDocument()
    {
        var legacy = Document();
        var content = NativeFreshTiledLevelContent.Create([
            NativeFreshFlatTileIntent.Create(TileA, 8448, 8448, 8704, 1536, 2048, new(80, 128, 112), legacy.FloorMaterials!),
            NativeFreshFlatTileIntent.Create(TileB, 9984, 8448, 8704, 1536, 2048, new(80, 128, 112), legacy.FloorMaterials!)],
            8960, 8960, 9216, 64, new(128, 96, 64), 19);
        return NativeFreshLevelDocumentIntent.CreateTiled(content, []).UpgradeForFamilies().WithPlacements(legacy.Placements);
    }

    internal static void Run()
    {
        checks = 0; root = Directory.CreateTempSubdirectory("spyro-family-session-").FullName;
        bool passed = false;
        try
        {
            AddReplaceAndHistory();
            AtomicTiledContentAndAnchors();
            MaximumAndAbsence();
            SaveReopenAndRemoval();
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(root, recursive: true); // Only this run's new tiny fixtures.
            else Console.Error.WriteLine("Retained failed family session fixtures: " + root);
        }
        Console.WriteLine($"FRESH_FAMILY_DOCUMENT_SESSION_OK checks={checks} sourceOpened=false windowStarted=false exportInvoked=false");
    }

    private static void AddReplaceAndHistory()
    {
        var fixture = Document(); var source = Source();
        foreach (int schema in new[] { 1, 2, 3, 4 })
        {
            var initial = NativeFreshLevelDocumentIntent.Create(fixture.Base, fixture.Placements.Take(1));
            if (schema >= 2) initial = initial.WithFloorMaterials(fixture.FloorMaterials!);
            if (schema == 3) initial = initial.UpgradeForNativeMobys().WithPlacements(fixture.Placements.Take(2));
            if (schema == 4) initial = NativeFreshLevelDocumentIntent.CreateTiled(TiledDocument().TiledContent!, fixture.Placements.Take(2));
            var session = Saved("add-" + schema, initial); session.Select(WizardId);
            if (schema == 4) session.SelectTile(TileB);
            var original = session.CurrentIntent!; var snapshot = session.Snapshot; byte[] raw = File.ReadAllBytes(snapshot.Path);
            foreach (var point in new[] { (8448, 9120, 8704), (9728, 8448, 8704), (9728, 9120, -1), (9728, 9120, 262129) })
            {
                var before = new State(session);
                Throws<InvalidDataException>(() => session.AddLockedChestFamily(source, point.Item1, point.Item2, point.Item3,
                    10864, 9888, 8704, null));
                before.Require(session, "invalid Add does not install an ID/schema, selection or history");
            }
            Throws<ArgumentNullException>(() => session.AddLockedChestFamily(null!, 9728, 9120, 8704, 10864, 9888, 8704, null));
            Check(ReferenceEquals(session.CurrentIntent, original) && !session.CanUndo && !session.CanRedo,
                "null source also leaves the original schema/history intact");
            var added = session.AddLockedChestFamily(source, 9729, 9121, 262128, 10865, 9889, 0, null);
            var upgraded = session.CurrentIntent!;
            Check(upgraded.SchemaVersion == 5 && ReferenceEquals(upgraded.Content, original.Content) &&
                upgraded.Placements.Take(original.Placements.Count).SequenceEqual(original.Placements) &&
                ReferenceEquals(upgraded.Placements[^1], added) && ReferenceEquals(added.Source, source) &&
                added.RewardDestinations == null && added.KeyX == 9729 && added.KeyZ == 262128 && added.ChestZ == 0 &&
                added.EditorId.Length == 32 && !original.Placements.Any(p => p.EditorId == added.EditorId) &&
                session.SelectedEditorId == added.EditorId && session.SelectedTileId == (schema == 4 ? TileB : null),
                "one explicit Add retains complete content/source and raw off-grid WORLD anchors, upgrading only the final aggregate");
            Check(session.Undo() && ReferenceEquals(session.CurrentIntent, original) && session.SelectedEditorId == WizardId && session.CanRedo,
                "one undo removes the entire family and restores the actual old schema/selection");
            Check(session.Redo() && ReferenceEquals(session.CurrentIntent, upgraded) && session.SelectedEditorId == added.EditorId,
                "redo restores the same family ID and exact aggregate without regeneration");

            var one = OneOverride();
            session.ReplaceLockedChestFamily(added.EditorId, source, 9728, 9120, 8704, 10864, 9888, 8704, one);
            var changed = session.CurrentIntent!; var replacement = (NativeFreshLockedChestFamilyPlacementIntent)changed.Placements[^1];
            Check(replacement.EditorId == added.EditorId && ReferenceEquals(replacement.Source, source) &&
                ReferenceEquals(replacement.RewardDestinations, one) && ReferenceEquals(changed.Content, upgraded.Content) &&
                changed.Placements.Take(original.Placements.Count).SequenceEqual(original.Placements),
                "replacement keeps actual source/override owners and unrelated content/placements");
            Check(session.Undo() && ReferenceEquals(session.CurrentIntent, upgraded) && session.CanRedo, "family replacement is one undo entry");
            session.ReplaceLockedChestFamily(added.EditorId, Source(), 9729, 9121, 262128, 10865, 9889, 0, null);
            Check(ReferenceEquals(session.CurrentIntent, upgraded) && ReferenceEquals(((NativeFreshLockedChestFamilyPlacementIntent)
                session.CurrentIntent!.Placements[^1]).Source, source) && session.CanRedo,
                "equal-value replacement preserves actual original source/document and meaningful redo");
            foreach (Action invalid in new Action[] {
                () => session.ReplaceLockedChestFamily(WizardId, source, 9728, 9120, 8704, 10864, 9888, 8704, one),
                () => session.ReplaceLockedChestFamily("missing", source, 9728, 9120, 8704, 10864, 9888, 8704, one),
                () => session.ReplaceLockedChestFamily(added.EditorId, source, 9728, 9120, 8704, 11520, 9888, 8704, one),
                () => session.ReplaceWizardRoute(added.EditorId, 9984, 9984, 8704, 9472, 9984),
                () => session.ReplaceNativeMoby(added.EditorId, NativeFreshRetailMobyReference.Create(Variant, 10, 121, 501), 9152, 9152, 8704, 64) })
            {
                var before = new State(session); Throws<InvalidDataException>(invalid);
                before.Require(session, "wrong-type/missing/outside edits preserve family variant, exact owners and redo");
            }
            Check(session.Redo() && ReferenceEquals(session.CurrentIntent, changed), "refused edits did not consume the pending redo");
            var five = FiveOverrides();
            session.ReplaceLockedChestFamily(added.EditorId, source, 9728, 9120, 8704, 10864, 9888, 8704, five);
            var fiveDocument = session.CurrentIntent!;
            Check(ReferenceEquals(((NativeFreshLockedChestFamilyPlacementIntent)fiveDocument.Placements[^1]).RewardDestinations, five) &&
                five.Values.Select(v => v.Role).SequenceEqual(Enum.GetValues<NativeFreshChestRewardRole>()),
                "all five sorted semantic WORLD destinations remain explicit, including destinations outside the floor");
            session.Apply(fiveDocument.WithPlacements(fiveDocument.Placements.Reverse())); var reversed = session.CurrentIntent!;
            Check(reversed.Placements[0].EditorId == added.EditorId && session.SelectedEditorId == added.EditorId,
                "generic reorder retains family identity rather than expanding native rows");
            Check(session.Undo() && ReferenceEquals(session.CurrentIntent, fiveDocument) && session.Redo() &&
                ReferenceEquals(session.CurrentIntent, reversed), "whole-family reorder uses existing exact-object history");
            Check(ReferenceEquals(session.Snapshot, snapshot) && File.ReadAllBytes(snapshot.Path).SequenceEqual(raw),
                "all add/replace/history/reorder work is in-memory until explicit Save");
        }
    }

    private static void AtomicTiledContentAndAnchors()
    {
        var fixture = TiledDocument(); var family = (NativeFreshLockedChestFamilyPlacementIntent)fixture.Placements[2];
        var session = Saved("atomic-tiled", fixture.WithPlacements([family]));
        session.Select(FamilyId); session.SelectTile(TileB);
        var original = session.CurrentIntent!; var content = original.TiledContent!;
        var shifted = NativeFreshTiledLevelContent.Create(content.Tiles.Select(t => NativeFreshFlatTileIntent.Create(
            t.EditorId, t.X + 4096, t.Y, t.Z, t.Width, t.Depth, t.FloorColor, t.Materials)),
            content.SpawnX + 4096, content.SpawnY, content.SpawnZ, content.Heading, content.SkyColor, content.MusicTrackId);
        var movedFamily = family.WithAnchors(family.KeyX + 4096, family.KeyY, family.KeyZ,
            family.ChestX + 4096, family.ChestY, family.ChestZ);
        var before = new State(session);
        Throws<InvalidDataException>(() => original.WithTiledContent(shifted));
        Throws<InvalidDataException>(() => original.WithPlacements([movedFamily]));
        before.Require(session, "both intermediate aggregate orders are invalid and install nothing");
        int enumerations = 0, yielded = 0, disposed = 0;
        IEnumerable<NativeFreshPlacementIntent> Once()
        {
            enumerations++;
            try { yielded++; yield return movedFamily; }
            finally { disposed++; }
        }
        var combined = original.WithTiledContent(shifted, Once()); session.Apply(combined);
        Check(enumerations == 1 && yielded == 1 && disposed == 1 && combined.SchemaVersion == 5 &&
            ReferenceEquals(combined.TiledContent, shifted) && ReferenceEquals(combined.Placements[0], movedFamily) &&
            ReferenceEquals(movedFamily.Source, family.Source) && ReferenceEquals(movedFamily.RewardDestinations, family.RewardDestinations) &&
            session.SelectedEditorId == FamilyId && session.SelectedTileId == TileB,
            "combined geometry/anchor update validates the final schema5 aggregate once, preserving WORLD overrides and both stable selections");
        Check(session.Undo() && ReferenceEquals(session.CurrentIntent, original) && !session.CanUndo && session.CanRedo,
            "the valid simultaneous move is exactly one atomic history entry");
        Check(session.Redo() && ReferenceEquals(session.CurrentIntent, combined), "redo restores exact combined geometry and family object");
        var schema4 = NativeFreshLevelDocumentIntent.CreateTiled(content, []);
        Check(schema4.WithTiledContent(shifted, []).SchemaVersion == 4 &&
            original.WithTiledContent(content).SchemaVersion == 5, "new overload retains schema4 and old one-argument schema5 behavior");
        Throws<InvalidDataException>(() => Document().WithTiledContent(shifted, []));
        Throws<ArgumentNullException>(() => original.WithTiledContent(null!, []));
        Throws<ArgumentNullException>(() => original.WithTiledContent(content, null!));
        List<NativeFreshPlacementIntent> mutable = [family];
        var captured = original.WithTiledContent(content, mutable); mutable.Clear();
        Check(captured.Placements.Count == 1 && ReferenceEquals(captured.Placements[0], family), "new overload snapshots caller collection without copying immutable owners");
        int bounded = 0, closed = 0;
        IEnumerable<NativeFreshPlacementIntent> TooMany()
        {
            try
            {
                for (int i = 1; i <= 1000; i++)
                {
                    bounded++;
                    yield return NativeFreshLockedChestFamilyPlacementIntent.Create(i.ToString("x32"), family.Source,
                        family.KeyX, family.KeyY, family.KeyZ, family.ChestX, family.ChestY, family.ChestZ);
                }
            }
            finally { closed++; }
        }
        var state = new State(session);
        Throws<InvalidDataException>(() => original.WithTiledContent(content, TooMany()));
        Check(bounded == 129 && closed == 1, "final aggregate validation bounds and disposes enumeration at the129th placement");
        Throws<InvalidDataException>(() => original.WithTiledContent(content, [family, family]));
        state.Require(session, "bounded/duplicate rejection cannot partially apply tiled content or placement history");
    }

    private static void MaximumAndAbsence()
    {
        var basis = Document().Base;
        var full = NativeFreshLevelDocumentIntent.Create(basis, Enumerable.Range(1, 128).Select(i =>
            NativeFreshWizardRouteIntent.Create(i.ToString("x32"), 9984, 9984, 8704, 9472, 9984)));
        var session = Saved("maximum", full); var state = new State(session);
        Throws<InvalidDataException>(() => session.AddLockedChestFamily(Source(), 9728, 9120, 8704, 10864, 9888, 8704, null));
        state.Require(session, "the129th authored entry cannot install an upgrade, family ID or history");
        var absent = new NativeFreshLevelDocumentSession(NativeFreshLevelDocumentStore.Capture(Project("absent")));
        var empty = new State(absent);
        Throws<InvalidOperationException>(() => absent.AddLockedChestFamily(Source(), 9728, 9120, 8704, 10864, 9888, 8704, null));
        empty.Require(absent, "family Add cannot infer a missing document or floor");
    }

    private static void SaveReopenAndRemoval()
    {
        foreach (bool tiled in new[] { false, true })
        {
            var session = Saved("persist-" + tiled.ToString().ToLowerInvariant(), tiled ? TiledDocument() : Document());
            session.Select(FamilyId); if (tiled) session.SelectTile(TileB);
            var family = (NativeFreshLockedChestFamilyPlacementIntent)session.CurrentIntent!.Placements[2]; var five = FiveOverrides();
            session.ReplaceLockedChestFamily(FamilyId, family.Source, family.KeyX, family.KeyY, family.KeyZ,
                family.ChestX, family.ChestY, family.ChestZ, five);
            var applied = session.CurrentIntent!; byte[] canonical = Canonical(applied);
            session.Save(); Check(ReferenceEquals(session.CurrentIntent, applied) && !session.HasUnsavedChanges && session.CanUndo,
                "actual Save retains the applied object and meaningful history after successful Store return");
            session.Reopen();
            Check(Canonical(session.CurrentIntent!).SequenceEqual(canonical) && session.CurrentIntent!.SchemaVersion == 5 &&
                session.SelectedEditorId == FamilyId && session.SelectedTileId == (tiled ? TileB : null) &&
                !session.CanUndo && !session.CanRedo && !session.HasUnsavedChanges &&
                ((NativeFreshLockedChestFamilyPlacementIntent)session.CurrentIntent.Placements[2]).RewardDestinations!.Values.SequenceEqual(five.Values),
                "Save/Reopen preserves every authored destination and both identities through the actual schema5 codec/Store");
            var restored = session.CurrentIntent;
            session.RemovePlacement(FamilyId); var removed = session.CurrentIntent!;
            Check(removed.SchemaVersion == 5 && removed.Placements.Count == 2 && session.SelectedEditorId == null &&
                ReferenceEquals(removed.Content, restored!.Content), "removing the final family retains schema5/content and clears only its selection");
            Check(session.Undo() && ReferenceEquals(session.CurrentIntent, restored) && session.SelectedEditorId == FamilyId &&
                session.Redo() && ReferenceEquals(session.CurrentIntent, removed), "generic family removal/restoration is one whole-entry history action");
            session.Save(); session.Reopen();
            Check(session.CurrentIntent!.SchemaVersion == 5 && session.CurrentIntent.Placements.Count == 2 &&
                session.SelectedEditorId == null, "family-free schema5 persists without automatic downgrade");
        }
    }

    private static NativeFreshLockedChestFamilyReference Source() => NativeFreshLockedChestFamilyReference.Create(Variant,
        NativeFreshLockedChestFamilyReference.SupportedDefinitionId);
    private static NativeFreshChestRewardDestinationOverrides OneOverride() => NativeFreshChestRewardDestinationOverrides.Create(
        [new(NativeFreshChestRewardRole.Reward2, 10454, 10503, 9216)]);
    private static NativeFreshChestRewardDestinationOverrides FiveOverrides() => NativeFreshChestRewardDestinationOverrides.Create(
        Enum.GetValues<NativeFreshChestRewardRole>().Reverse().Select((role, i) => new NativeFreshChestRewardDestinationOverride(
            role, i == 0 ? 262128 : i, i == 4 ? 262128 : i, i * 65532)).ToArray());
    private static NativeFreshLevelDocumentSession Saved(string id, NativeFreshLevelDocumentIntent document) =>
        new(NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project(id)), document));
    private static EditorProjectLayout Project(string id)
    {
        string path = Path.Combine(root, id); Directory.CreateDirectory(path); var project = new EditorProjectLayout(path, id);
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"unrelated\":true}\n");
        return project;
    }
    private static byte[] Canonical(NativeFreshLevelDocumentIntent document) => NativeFreshLevelDocumentCodec.SerializeCanonical(document);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
    private static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { checks++; return; } throw new InvalidDataException("Expected " + typeof(T).Name); }
    private sealed class State(NativeFreshLevelDocumentSession session)
    {
        private readonly NativeFreshLevelDocumentSnapshot snapshot = session.Snapshot;
        private readonly NativeFreshLevelDocumentIntent? document = session.CurrentIntent;
        private readonly string? selected = session.SelectedEditorId, tile = session.SelectedTileId;
        private readonly bool undo = session.CanUndo, redo = session.CanRedo, dirty = session.HasUnsavedChanges;
        internal void Require(NativeFreshLevelDocumentSession current, string message) => Check(
            ReferenceEquals(snapshot, current.Snapshot) && ReferenceEquals(document, current.CurrentIntent) &&
            selected == current.SelectedEditorId && tile == current.SelectedTileId && undo == current.CanUndo &&
            redo == current.CanRedo && dirty == current.HasUnsavedChanges, message);
    }
}
