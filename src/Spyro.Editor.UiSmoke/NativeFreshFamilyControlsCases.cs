using Avalonia.Controls;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = NativeFreshMaterialInspectionUiFixture;

internal static class NativeFreshFamilyControlsCases
{
    internal static void Run(string root)
    {
        int checks = 0;
        foreach (bool tiled in new[] { false, true })
        {
            var f = new F(root, ("family-controls-" + tiled).ToLowerInvariant());
            try
            {
                var doc = tiled ? NativeFreshFamilyDocumentSessionCases.TiledDocument() : NativeFreshFamilyDocumentSessionCases.Document();
                NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project), doc);
                f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(doc);
                f.Fire("FreshDocumentReopen");
                Check(f.Window.CurrentAggregateIntent?.SchemaVersion == 5, "schema5 editor admission");
                f.Select(NativeFreshFamilyDocumentSessionCases.FamilyId);
                Check(f.Text("FreshFamilyKeyX") == "9728" && f.Text("FreshFamilyChestY") == "9888", "independent anchors displayed exactly");
                for (int i = 1; i <= 5; i++)
                    Check(Mode(i).SelectedIndex == (i == 2 ? 1 : 0), "all five explicit/default modes visible");
                Check(f.Text("FreshFamilyReward2X") == "10454" && f.Text("FreshFamilyReward1X") == "", "no inferred default coordinates");
                var initial = f.Window.CurrentAggregateIntent;
                f.Set("FreshFamilyReward1X", "9999"); f.Fire("FreshDocumentApply");
                Check(ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && f.Text("FreshFamilyReward1X") == "9999" &&
                    f.Window.HasUnappliedFields, "inactive default raw coordinate refuses, is not silently discarded");
                f.Fire("FreshDocumentUndo");
                f.Set("FreshFamilyChestX", "10865"); f.Fire("FreshDocumentApply");
                Check(Family().ChestX == 10865 && Family().RewardDestinations!.Values.Single().X == 10454,
                    "moving chest retains explicit WORLD reward destination");
                var moved = f.Window.CurrentAggregateIntent;
                f.Fire("FreshDocumentUndo");
                Check(ReferenceEquals(initial, f.Window.CurrentAggregateIntent) && !f.Window.HasUnappliedFields &&
                    f.Find<Button>("FreshDocumentRedo").IsEnabled, "Undo restores actual initial aggregate and meaningful redo");
                f.Fire("FreshDocumentApply");
                Check(f.Find<Button>("FreshDocumentRedo").IsEnabled, "no-op Apply retains redo availability");
                f.Fire("FreshDocumentRedo");
                Check(ReferenceEquals(moved, f.Window.CurrentAggregateIntent), "no-op Apply preserves redo and exact family identity");
                for (int i = 1; i <= 5; i++)
                {
                    Mode(i).SelectedIndex = 1; F.Pump();
                    f.Set($"FreshFamilyReward{i}X", (10400 + i).ToString());
                    f.Set($"FreshFamilyReward{i}Y", (10500 + i).ToString());
                    f.Set($"FreshFamilyReward{i}Z", "262128");
                }
                f.Fire("FreshDocumentApply");
                Check(Family().RewardDestinations?.Values.Count == 5 && Family().RewardDestinations!.Values.All(v => v.Z == 262128),
                    "all five raw WORLD destinations apply without clamping to floor/ushort");
                Check(Family().RewardDestinations!.Values.Select(v => (v.Role, v.X, v.Y, v.Z)).SequenceEqual(
                    Enumerable.Range(1, 5).Select(i => ((NativeFreshChestRewardRole)(i - 1), 10400 + i, 10500 + i, 262128))),
                    "all five role-to-XYZ mappings retained exactly");
                for (int i = 1; i <= 5; i++) { Mode(i).SelectedIndex = 0; F.Pump(); }
                f.Fire("FreshDocumentApply");
                Check(Family().RewardDestinations == null && Enumerable.Range(1, 5).All(i => f.Text($"FreshFamilyReward{i}X") == ""),
                    "explicit default selection clears authored overrides, not synthetic positions");
                f.Set("FreshNativeMobyX", "123"); var applied = f.Window.CurrentAggregateIntent; f.Fire("FreshDocumentApply");
                Check(ReferenceEquals(applied, f.Window.CurrentAggregateIntent) && f.Window.HasUnappliedFields,
                    "inactive native Moby fields do not get discarded by family Apply");
                f.Fire("FreshDocumentUndo");
                f.Fire("FreshDocumentSave"); f.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(f.Window.CurrentAggregateIntent!);
                f.Fire("FreshDocumentReopen");
                Check(Family().RewardDestinations == null && !f.Window.HasUnsavedChanges, "complete family survives Save/Reopen");
                f.Fire("FreshFamilyAdd"); var beforeAdd = f.Window.CurrentAggregateIntent;
                f.Fire("FreshDocumentApply");
                Check(ReferenceEquals(beforeAdd, f.Window.CurrentAggregateIntent) && f.Window.HasUnappliedFields, "incomplete Add installs no ID/schema/history");
                f.Fire("FreshFamilyCancelAdd");
                Check(ReferenceEquals(beforeAdd, f.Window.CurrentAggregateIntent) && !f.Window.HasUnappliedFields, "cancel pending family restores applied form");
                f.Fire("FreshFamilyAdd");
                foreach (var pair in new[] { ("KeyX", "9728"), ("KeyY", "9120"), ("KeyZ", "8704"), ("ChestX", "10864"), ("ChestY", "9888"), ("ChestZ", "8704") })
                    f.Set("FreshFamily" + pair.Item1, pair.Item2);
                f.Fire("FreshDocumentApply");
                Check(f.Window.CurrentAggregateIntent!.Placements.Count == 4 &&
                    f.Window.CurrentAggregateIntent.Placements[^1] is NativeFreshLockedChestFamilyPlacementIntent { RewardDestinations: null }, "one complete family Add, no synthetic supporting Mobys");
                f.Fire("FreshDocumentUndo");
                Check(ReferenceEquals(beforeAdd, f.Window.CurrentAggregateIntent), "whole-family Add is exactly one history entry");
                Check(f.SourceCalls == 0 && f.OwnerAttempts == 0, "neutral controls never open source or acquire export owner");
            }
            finally { f.Close(); f.RequireProtected(); }
            ComboBox Mode(int i) => f.Find<ComboBox>($"FreshFamilyReward{i}Mode");
            NativeFreshLockedChestFamilyPlacementIntent Family() => (NativeFreshLockedChestFamilyPlacementIntent)
                f.Window.CurrentAggregateIntent!.Placements.Single(p => p.EditorId == NativeFreshFamilyDocumentSessionCases.FamilyId);
        }
        var atomic = new F(root, "family-controls-atomic-tile");
        try
        {
            var basis = NativeFreshFamilyDocumentSessionCases.Document();
            var family = ((NativeFreshLockedChestFamilyPlacementIntent)basis.Placements[2])
                .WithAnchors(9728, 9120, 8704, 10064, 9888, 8704);
            var tile = NativeFreshFlatTileIntent.Create(new string('4', 32), 8448, 8448, 8704, 2048, 2048,
                new(80, 128, 112), basis.FloorMaterials!);
            var content = NativeFreshTiledLevelContent.Create([tile], 8960, 8960, 9216, 64, new(128, 96, 64), 19);
            var original = NativeFreshLevelDocumentIntent.CreateTiled(content, []).UpgradeForFamilies().WithPlacements([family]);
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(atomic.Project), original);
            atomic.ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(original);
            atomic.Fire("FreshDocumentReopen"); atomic.Select(family.EditorId);
            var applied = atomic.Window.CurrentAggregateIntent;
            atomic.Set("FreshFloorX", "12544"); atomic.Set("FreshSpawnX", "13056");
            atomic.Fire("FreshDocumentApply");
            Check(ReferenceEquals(applied, atomic.Window.CurrentAggregateIntent) && atomic.Window.HasUnappliedFields,
                "moving tile alone refuses while preserving all raw fields and applied anchors");
            atomic.Set("FreshFamilyKeyX", "13824"); atomic.Set("FreshFamilyChestX", "14160");
            atomic.Fire("FreshDocumentApply");
            var moved = atomic.Window.CurrentAggregateIntent!;
            var movedFamily = (NativeFreshLockedChestFamilyPlacementIntent)moved.Placements.Single();
            Check(moved.SchemaVersion == 5 && moved.TiledContent!.Tiles.Single().X == 12544 && moved.TiledContent.SpawnX == 13056 &&
                movedFamily.KeyX == 13824 && movedFamily.ChestX == 14160 && movedFamily.RewardDestinations!.Values.Single().X == 10454,
                "tile plus family apply atomically even though either intermediate document is invalid; WORLD reward remains fixed");
            atomic.Fire("FreshDocumentUndo");
            Check(ReferenceEquals(applied, atomic.Window.CurrentAggregateIntent), "atomic tile/family move is exactly one history entry");
        }
        finally { atomic.Close(); atomic.RequireProtected(); }
        Console.WriteLine($"FRESH_FAMILY_CONTROLS_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
        void Check(bool value, string message) { if (!value) throw new InvalidDataException(message); checks++; }
    }
}
