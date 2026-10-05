using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

/// <summary>End-to-end editor flow for universal Moby placement in a real headless MainWindow:
/// open Toasty, browse the game-wide library, place foreign objects, save, reopen, undo, Create BIN.</summary>
internal static class UniversalMobyUiCases
{
    private static int _checks;
    internal static void RunSelectionResponsiveness(string outputDirectory)
    {
        _checks = 0;
        var window = new MainWindow { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            Wait(() => Mobys(window).Count > 0, "initial scene");
            Load(window, Get<LevelCatalog>(window, "_retailCatalog").FindByKey("artisans")!);
            int originalCount = Mobys(window).Count;
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            Window browser = Owned(window, "Add object — Artisans");
            var list = Find<ListBox>(browser, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "complete catalog loaded", 240);
            var entry = Items(list).Where(e => e.RestrictionReason == null)
                .OrderByDescending(e => e.Variants.Count).First();
            list.SelectedItem = entry;
            var variants = Find<ComboBox>(browser, "UniversalMobyVariant");
            var last = entry.Variants.Last(v => v.RestrictionReason == null);
            var clock = Stopwatch.StartNew();
            variants.SelectedItem = last; // No dispatcher pump: a cold selection must return immediately.
            Check(clock.ElapsedMilliseconds < 250, "cold source selection returns within 250 ms");
            Check(Find<TextBlock>(browser, "UniversalMobyFitText").Text!.Contains("Checking"),
                "cold source selection shows an asynchronous fit check");
            Check(!Find<Button>(browser, "UniversalMobyPlace").IsEnabled && !Find<Button>(browser, "UniversalMobyAddHere").IsEnabled,
                "unchecked selection cannot be placed");
            var search = Find<TextBox>(browser, "UniversalMobySearch");
            search.Text = "no-match-selection-regression";
            bool heartbeat = false;
            Dispatcher.UIThread.Post(() => heartbeat = true);
            Wait(() => heartbeat, "UI dispatcher remains responsive");
            Check(Items(list).Length == 0 && !Find<Button>(browser, "UniversalMobyAddHere").IsEnabled,
                "changing search during a fit check clears selection safely");
            search.Text = ""; Pump();
            list.SelectedItem = entry;
            variants.SelectedItem = entry.Variants.First(v => v.RestrictionReason == null);
            variants.SelectedItem = last;
            Wait(() => !(Find<TextBlock>(browser, "UniversalMobyFitText").Text ?? "").Contains("Checking"),
                "latest source version finishes", 120);
            Check(Find<TextBlock>(browser, "UniversalMobyTechnical").Text!.Contains($"row T{last.SourceRow}"),
                "late results keep the latest source identity: expected " + last + "; selected " + variants.SelectedItem + "; details " + Find<TextBlock>(browser, "UniversalMobyTechnical").Text);
            bool fits = Find<TextBlock>(browser, "UniversalMobyFitText").Text!.StartsWith("✓");
            Check(Find<Button>(browser, "UniversalMobyAddHere").IsEnabled == fits,
                "placement reflects the completed exact check");
            Snapshot(browser, Path.Combine(outputDirectory, "object-picker-responsive.png"));
            // Start a previously unselected version, then close without waiting for its check.
            variants.SelectedItem = entry.Variants.First(v => v != last && v != entry.Default && v.RestrictionReason == null);
            clock.Restart();
            Find<Button>(browser, "UniversalMobyClose").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(clock.ElapsedMilliseconds < 250 && !browser.IsVisible, "close returns immediately during background checks");
            Pump();
            Check(Mobys(window).Count == originalCount, "browsing and cancellation do not mutate the level");
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            browser = Owned(window, "Add object — Artisans");
            list = Find<ListBox>(browser, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "browser reopens after cancellation", 240);
            list.SelectedItem = Items(list).Single(e => e.ClassId == 0x1CF);
            Wait(() => Find<Button>(browser, "UniversalMobyAddHere").IsEnabled, "teal gem still passes the real export check", 120);
            Click(Find<Button>(browser, "UniversalMobyClose"));
            Console.WriteLine($"UNIVERSAL_SELECTION_RESPONSIVENESS_OK checks={_checks}");
        }
        finally
        {
            foreach (var owned in window.OwnedWindows.ToArray()) owned.Close();
            window.Close(); Pump();
        }
    }

    internal static void Run(string outputDirectory)
    {
        _checks = 0;
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            Wait(() => window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count > 0, "initial retail scene");
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            string root = window.CaptureEditorShellSessionSnapshotForTesting().Workspace.RootPath;
            var toasty = catalog.FindByKey("toasty")!;
            var stone = catalog.FindByKey("stonehill")!;
            Load(window, toasty);
            int nativeCount = Mobys(window).Count;
            Snapshot(window, Path.Combine(outputDirectory, "universal-main-window.png"));
            if (Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_SCREENS_ONLY") == "1")
            {
                string? jp = Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_JAPAN_IMAGE");
                if (!string.IsNullOrWhiteSpace(jp)) UniversalJapaneseDiscLocator.Configure(window.CaptureEditorShellSessionSnapshotForTesting().Workspace, jp);
                foreach (var (button, file) in new[] { ("UniversalAddEggButton", "universal-screen-egg.png"), ("UniversalAddSignButton", "universal-screen-sign.png"), ("UniversalMobyLibraryButton", "universal-screen-all.png") })
                {
                    Click(Find<Button>(window, button));
                    Window b = Owned(window, "Add object — Toasty");
                    ListBox l = Find<ListBox>(b, "UniversalMobyList");
                    Wait(() => Items(l).Length > 0, "browser populated", 240);
                    TextBlock sum = Find<TextBlock>(b, "UniversalMobySummary");
                    Wait(() => !(sum.Text ?? "").Contains("Checking"), "fit check finished", 600);
                    Thread.Sleep(300); Pump();
                    Snapshot(b, Path.Combine(outputDirectory, file));
                    b.Close(); Pump();
                }
                Console.WriteLine("SCREENS_ONLY");
                return;
            }

            // 1. Library opens on the complete game-wide catalogue.
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            Window library = Owned(window, "Add object — Toasty");
            ListBox list = Find<ListBox>(library, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "game-wide catalogue populated", 240);
            int total = Items(list).Length;
            Check(total >= 180, $"catalogue exposes {total} objects");
            ListBox cats = Find<ListBox>(library, "UniversalMobyCategories");
            var categoryNames = cats.ItemsSource!.Cast<(string, string, int)>().Select(c => c.Item1).Distinct().ToList();
            Check(categoryNames.Contains("Enemies") && categoryNames.Contains("Collectibles") && categoryNames.Contains("Interactive"), "category hierarchy");
            CheckBrowserReleaseStates(library, list, outputDirectory);
            Snapshot(library, Path.Combine(outputDirectory, "universal-moby-library.png"));
            ComboBox fitOnly = Find<ComboBox>(library, "UniversalMobyFit");
            fitOnly.SelectedIndex = 1; Pump();
            Snapshot(library, Path.Combine(outputDirectory, "universal-moby-fit-only.png"));
            Check(!list.GetLogicalDescendants().OfType<TextBlock>().Any(t =>
                (t.Text ?? "").Contains("Checking whether it fits", StringComparison.Ordinal)),
                "fit-only list never presents an unverified estimate as a fitting object");
            fitOnly.SelectedIndex = 0; Pump();

            // 2. Search by class and place a foreign object (Stone Hill sheep 0x0A) near the selection.
            TextBox search = Find<TextBox>(library, "UniversalMobySearch");
            search.Text = "0x00a"; Pump();
            var sheep = Items(list).Single(e => e.ClassId == 0x0A);
            Check(!sheep.SourceLevels.Contains("toasty"), "sheep is foreign to Toasty");
            list.SelectedItem = sheep; Pump();
            Check(Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "foreign object fits Toasty");
            Border previewHost = Find<Border>(library, "UniversalMobyModelPreview");
            Wait(() => previewHost.Tag is UniversalMobyModelPreview.Frame, "source model preview loaded", 120);
            var previewFrame = (UniversalMobyModelPreview.Frame)previewHost.Tag!;
            Check(previewFrame.Pixels != null && previewFrame.VertexCount > 20 && previewFrame.FaceCount > 10 && previewFrame.SourceHash?.Length == 64,
                "foreign sheep preview is decoded from the actual source geometry: " + previewFrame.Description);
            var previewTask = UniversalMobyModelPreview.LoadAsync(Library(window), sheep.Default, null);
            Check(ReferenceEquals(previewTask, UniversalMobyModelPreview.LoadAsync(Library(window), sheep.Default, null)), "model preview tasks are cached");
            // A search remains global after choosing a category that excludes sheep.
            cats.SelectedItem = cats.ItemsSource!.Cast<(string, string, int)>().First(c => c.Item1 == "Interactive" && c.Item2 == ""); Pump();
            Check(Items(list).Any(e => e.ClassId == 0x0A), "search covers every category");
            Snapshot(library, Path.Combine(outputDirectory, "universal-moby-library-selected.png"));
            fitOnly.SelectedIndex = 1; Pump();
            Snapshot(library, Path.Combine(outputDirectory, "universal-moby-fit-only-selected.png"));
            Check(Items(list).Contains(sheep), "selected exact-checked object remains in the fit-only list");
            Check(!list.GetLogicalDescendants().OfType<TextBlock>().Any(t =>
                (t.Text ?? "").Contains("Checking whether it fits", StringComparison.Ordinal)),
                "fit-only results contain completed checks after selecting a source object");
            fitOnly.SelectedIndex = 0; Pump();
            if (Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_FIT_REVIEW_ONLY") == "1")
            {
                library.Close(); Pump();
                Console.WriteLine($"UNIVERSAL_FIT_REVIEW_UI_OK checks={_checks}");
                return;
            }
            Click(Find<Button>(library, "UniversalMobyAddHere"));
            Wait(() => Mobys(window).Count == nativeCount + 1, "sheep added");
            Moby added = Mobys(window).Last();
            Check(added.IsAdded && added.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature &&
                  added.CrossLevelSourceLevelKey == sheep.Default.SourceLevelKey, "added row carries universal import identity");

            // 3. A second foreign object with different dependencies (an enemy from a later world).
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            library = Owned(window, "Add object — Toasty");
            list = Find<ListBox>(library, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "catalogue reopened");
            var existing = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
            var enemy = Items(list).Where(e => e.Category == "Enemies" && !e.SourceLevels.Contains("toasty") &&
                e.SourceLevels.Any(s => catalog.FindByKey(s)?.LevelId >= 30)).OrderBy(e => e.Default.ModelBytes)
                .FirstOrDefault(e =>
                {
                    var row = Library(window).Get(e.Default.SourceLevelKey).Data.Row(e.Default.SourceRow);
                    return UniversalMobyCatalogBuilder.DryRun(Library(window), toasty, [.. existing,
                        new UniversalMobyPlacement(e.Default.SourceLevelKey, e.Default.SourceRow, BitConverter.ToInt32(row, 12), BitConverter.ToInt32(row, 16), BitConverter.ToInt32(row, 20),
                            EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: existing.Count)]).Error == null;
                });
            Check(enemy != null, "a later-world enemy that fits Toasty exists");
            list.SelectedItem = enemy; Pump();
            Wait(() => Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "exact dry-run admits the enemy alongside the sheep", 120);
            Click(Find<Button>(library, "UniversalMobyAddHere"));
            Wait(() => Mobys(window).Count == nativeCount + 2, "enemy added");

            // 4. Save, switch away and back: placements persist with their identity.
            Save(window);
            string edits = Path.Combine(root, "toasty-native-edits.json");
            var saved = UniversalPlacementReader.Read(edits);
            Check(saved.Count(p => !p.Retired) == 2 && saved.Any(p => p.SourceLevelKey == sheep.Default.SourceLevelKey && p.SourceRowIndex == sheep.Default.SourceRow),
                "native-edits.json records both universal placements");
            Load(window, stone); Load(window, toasty);
            Check(Mobys(window).Count(m => m.IsAdded && m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature) == 2,
                "universal placements reload after reopening the level");

            // 5. Undo removes an added object and the save reflects it; re-add afterwards.
            Moby enemyRow = Mobys(window).Last(m => m.IsAdded);
            string retiredEnemyId = enemyRow.UniversalPlacementId;
            int retiredEnemySlot = saved.Single(p => p.EditorId == retiredEnemyId).PersistentSlot!.Value;
            Invoke(window, "UndoMoby", enemyRow);
            Save(window);
            Check(UniversalPlacementReader.Read(edits).Count(p => !p.Retired) == 1, "undo removes the placement from the saved project");
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            library = Owned(window, "Add object — Toasty");
            list = Find<ListBox>(library, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "catalogue reopened for re-add");
            list.SelectedItem = Items(list).Single(e => e.ClassId == enemy!.ClassId); Pump();
            Click(Find<Button>(library, "UniversalMobyAddHere"));
            Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == 2, "enemy re-added");
            Save(window);

            // 6. Japanese-release objects: dragonfly egg (colour variant) and an editable sign.
            string? japan = Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_JAPAN_IMAGE");
            var workspace = window.CaptureEditorShellSessionSnapshotForTesting().Workspace;
            string jpConfig = UniversalJapaneseDiscLocator.ConfigPath(workspace);
            byte[]? jpConfigBefore = File.Exists(jpConfig) ? File.ReadAllBytes(jpConfig) : null;
            int specials = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(japan) && UniversalJapaneseDiscLocator.IsOriginal(japan))
                {
                    UniversalJapaneseDiscLocator.Configure(workspace, japan);
                    specials = SpecialObjects(window, root, toasty, stone, outputDirectory);
                }
                else Console.WriteLine("universal UI: SPYRO_UNIVERSAL_JAPAN_IMAGE not set; Japanese-release objects checked as unavailable only.");
            }
            finally
            {
                if (jpConfigBefore != null) File.WriteAllBytes(jpConfig, jpConfigBefore); else if (File.Exists(jpConfig) && specials == 0) File.Delete(jpConfig);
            }

            // 7. A second destination: a later-world object in Stone Hill (no signs there).
            Load(window, stone);
            int stoneNative = Mobys(window).Count;
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            library = Owned(window, "Add object — Stone Hill");
            list = Find<ListBox>(library, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "catalogue for Stone Hill", 240);
            var later = Items(list).Where(e => e.RestrictionReason == null && !e.SourceLevels.Contains("stonehill") &&
                    e.Default.SourceLevelKey != UniversalSpecialCatalog.SourceKey &&
                    catalog.FindByKey(e.Default.SourceLevelKey)?.LevelId >= 40)
                .OrderBy(e => e.Default.ModelBytes + e.Default.BehaviorBytes)
                .FirstOrDefault(e =>
                {
                    var row = Library(window).Get(e.Default.SourceLevelKey).Data.Row(e.Default.SourceRow);
                    return UniversalMobyCatalogBuilder.DryRun(Library(window), stone, [new UniversalMobyPlacement(e.Default.SourceLevelKey, e.Default.SourceRow,
                        BitConverter.ToInt32(row, 12), BitConverter.ToInt32(row, 16), BitConverter.ToInt32(row, 20),
                            EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: 0)]).Error == null;
                });
            Check(later != null, "a later-world object fits Stone Hill");
            int laterClass = later!.ClassId;
            bool laterHasModel = later.Default.HasLocalModel;
            list.SelectedItem = later; Pump();
            Wait(() => Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "later-world object admitted in Stone Hill", 120);
            Click(Find<Button>(library, "UniversalMobyAddHere"));
            Wait(() => Mobys(window).Count == stoneNative + 1, "Stone Hill object added");
            Save(window);
            Load(window, toasty);

            bool admittedOutputOnly = Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_ADMITTED_OUTPUT_ONLY") == "1";
            if (!admittedOutputOnly)
            {
            // 8. Make room: remove every copy of Toasty's own class 0x15D; a 73 KB Ice Cavern object then fits.
            int[] freedRows = [1, 3, 5, 8, 9, 14, 26];
            var bigRow = Library(window).Get("icecavern").Data.Row(53);
            var before = (List<UniversalMobyPlacement>)Invoke(window, "CurrentUniversalPlacements")!;
            var bigTrial = new UniversalMobyPlacement("icecavern", 53, BitConverter.ToInt32(bigRow, 12), BitConverter.ToInt32(bigRow, 16), BitConverter.ToInt32(bigRow, 20),
                EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: before.Count);
            var provider = specials > 0 ? UniversalMobyExport.SpecialProvider(new UniversalJapaneseSource(japan!)) : null;
            var beforeRemoval = UniversalMobyCatalogBuilder.DryRun(Library(window), toasty, [.. before, bigTrial], provider);
            Check(beforeRemoval.Error?.Contains("RAM") == true, "the Ice Cavern object does not fit Toasty before removing anything: " + beforeRemoval.Error);
            foreach (int r in freedRows)
            {
                SetField(window, "_selectedMoby", Mobys(window).Single(m => !m.IsAdded && m.TrueIndex == r));
                Invoke(window, "RemoveSelectedMoby");
            }
            var removedNow = (List<int>)Invoke(window, "CurrentRemovedNativeRows")!;
            Check(freedRows.All(removedNow.Contains), "removed native rows are reported to the fit check");
            var afterRemoval = UniversalMobyCatalogBuilder.DryRun(Library(window), toasty, [.. before, bigTrial], provider, removedNow);
            Check(afterRemoval.Error == null, "after removing every 0x15D the Ice Cavern object fits: " + afterRemoval.Error);
            Click(Find<Button>(window, "UniversalMobyLibraryButton"));
            library = Owned(window, "Add object — Toasty");
            list = Find<ListBox>(library, "UniversalMobyList");
            Wait(() => Items(list).Length > 150, "catalogue reopened after removals", 240);
            TextBlock bigSummary = Find<TextBlock>(library, "UniversalMobySummary");
            Wait(() => !(bigSummary.Text ?? "").Contains("Checking"), "fit check after removals", 600);
            var bigEntry = Items(list).Single(e => e.ClassId == 0xD4);
            list.SelectedItem = bigEntry; Pump();
            ComboBox bigVariant = Find<ComboBox>(library, "UniversalMobyVariant");
            if (bigEntry.Variants.Count > 1) { bigVariant.SelectedItem = bigEntry.Variants.Single(v => v.SourceLevelKey == "icecavern" && v.SourceRow == 53); Pump(); }
            Wait(() => Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "Ice Cavern object admitted after removals", 120);
            int beforeAdd = Mobys(window).Count(m => m.IsAdded && !m.IsRemoved);
            Click(Find<Button>(library, "UniversalMobyAddHere"));
            Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == beforeAdd + 1, "Ice Cavern object added");
            Save(window);
            Check(UniversalPlacementReader.ReadRemovedNativeRows(Path.Combine(root, "toasty-native-edits.json")).Count(freedRows.Contains) == freedRows.Length, "removals saved");

            }
            else Console.WriteLine("ADMITTED_OUTPUT_ONLY: large Ice Cavern dependency fixture is separate from this runtime candidate.");

            // 9. Create BIN through the normal combined export and read the output back.
            string source = Get<TextBox>(window, "_discImagePathBox").Text!;
            string cue = Path.ChangeExtension(source, ".cue");
            string sourceHash = Sha(source);
            string outDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "spyro-universal-ui-" + Guid.NewGuid().ToString("N"))).FullName;
            var targets = Invoke(window, "FindEditedLevelExportTargets")!;
            var task = (Task)Invoke(window, "CreateAllSavedEditsTestAsync", source, cue, outDir, targets)!;
            Wait(() => task.IsCompleted, "Create BIN finished", 900);
            if (task.IsFaulted) throw task.Exception!;
            object result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            string output = (string)result.GetType().GetProperty("OutputImagePath")!.GetValue(result)!;
            Check(File.Exists(output), "Create BIN produced a disc image");
            Check(Sha(source) == sourceHash, "source disc unchanged");
            var disc = UniversalDiscImage.Read(output);
            var data = new UniversalLevelData(disc.EntryBytes(toasty.SourceWadEntry));
            Check(data.Registrations().Any(r => r.ClassId == 0x0A), "exported Toasty registers the sheep model");
            var finalPlacements = UniversalPlacementReader.Read(edits);
            var activePlacements = finalPlacements.Where(p => !p.Retired).ToArray();
            var removedNative = UniversalPlacementReader.ReadRemovedNativeRows(edits).Order().ToArray();
            int[] placedPhysicalRows = activePlacements.Select((_, i) => i < removedNative.Length ? removedNative[i] : toasty.SourceRecordCount + i - removedNative.Length).ToArray();
            int expectedPhysicalCount = Enumerable.Range(0, toasty.SourceRecordCount).Except(removedNative).Concat(placedPhysicalRows).Max() + 1;
            Check(data.Scene().RowCount == expectedPhysicalCount,
                "exported Toasty reuses removed native rows and emits only active placement rows");
            Check(finalPlacements.Single(p => p.EditorId == retiredEnemyId) is { Retired: true } retired && retired.PersistentSlot == retiredEnemySlot,
                "export preserves the retired enemy's lifetime identity and original logical slot");
            byte[] toastyOverlay = disc.EntryBytes(toasty.SourceWadEntry - 1);
            int updateAt = checked((int)(UniversalExeBindings.Read(disc.Exe, toasty.LevelId)[UniversalExeBindings.UpdateGlobal].Value - UniversalOverlay.Base));
            Check(BitConverter.ToUInt32(toastyOverlay, updateAt + 8) == NativeMobySaveMap.Tag,
                "exported Toasty carries its native physical-to-logical saved row map");
            int[] saveRows = Enumerable.Range(0, data.Scene().RowCount).Select(i => (int)BitConverter.ToInt16(toastyOverlay, updateAt + 64 + i * 2)).ToArray();
            Check(!saveRows.Contains(toasty.SourceRecordCount + retiredEnemySlot),
                "retired enemy has no active physical actor or saved-row mapping");
            for (int i = 0; i < activePlacements.Length; i++)
            {
                var placement = activePlacements[i];int physical = placedPhysicalRows[i];var row = data.Row(physical);
                Check(saveRows[physical] == toasty.SourceRecordCount + placement.PersistentSlot &&
                    BitConverter.ToInt32(row, 12) == placement.X && BitConverter.ToInt32(row, 16) == placement.Y && BitConverter.ToInt32(row, 20) == placement.Z,
                    "active native actor position and logical saved identity match the durable placement");
            }
            if (!admittedOutputOnly)
            Check(!data.Registrations().Any(r => r.ClassId == 0x15D) && data.Registrations().Any(r => r.ClassId == 0xD4) &&
                  removedNative.Except(placedPhysicalRows).All(r => r >= data.Scene().RowCount ||
                      saveRows[r] == -1 && BitConverter.ToUInt16(data.Row(r), 0x36) == UniversalMobyComposer.InertClass),
                "unreused removed rows remain inert and unbound; their model was dropped and the Ice Cavern model was added");
            var stoneData = new UniversalLevelData(disc.EntryBytes(stone.SourceWadEntry));
            Check(BitConverter.ToUInt16(stoneData.Row(stoneData.Scene().RowCount - 1), 0x36) == laterClass &&
                  (stoneData.Registrations().Any(r => r.ClassId == laterClass) || Library(window).SharedClasses.Contains(laterClass) || !laterHasModel),
                $"exported Stone Hill ends with the imported later-world row 0x{laterClass:X3} and its model (rows {stoneData.Scene().RowCount}, last classes " +
                string.Join(",", Enumerable.Range(Math.Max(0, stoneData.Scene().RowCount - 4), Math.Min(4, stoneData.Scene().RowCount)).Select(i => $"0x{BitConverter.ToUInt16(stoneData.Row(i), 0x36):X3}")) +
                $", registered {stoneData.Registrations().Any(r => r.ClassId == laterClass)}, native {stone.SourceRecordCount}, saved {UniversalPlacementReader.Read(Path.Combine(root, "stonehill-native-edits.json")).Count})");
            var stoneUpdate = UniversalExeBindings.Read(disc.Exe, stone.LevelId)[UniversalExeBindings.UpdateGlobal].Value;
            byte[] stoneOverlay = disc.EntryBytes(stone.SourceWadEntry - 1);
            Check(BitConverter.ToUInt32(stoneOverlay, (int)(stoneUpdate - 12 - UniversalOverlay.Base)) != UniversalSpecialBehaviors.SignHookMarker,
                "Stone Hill carries no sign hook marker");
            if (specials > 0)
            {
                Check(data.Registrations().Any(r => r.ClassId == 0x1F2) && data.Registrations().Any(r => r.ClassId == UniversalDragonflyEggs.SignClass),
                    "exported Toasty registers the blue egg and sign models");
                int signRow = Enumerable.Range(0, data.Scene().RowCount).First(i => BitConverter.ToUInt16(data.Row(i), 0x36) == UniversalDragonflyEggs.SignClass);
                Check(UniversalMips.EncodeJ(3, UniversalSpecialBehaviors.SignHookHost) == UniversalExeBindings.Word(disc.Exe, UniversalSpecialBehaviors.SignHookCallSite),
                    "sign render hook installed");
                Check(UniversalExeBindings.Word(disc.Exe, UniversalSpecialBehaviors.EggInventoryCallSite) == UniversalMips.EncodeJ(3, UniversalSpecialBehaviors.EggInventoryHost) &&
                      UniversalExeBindings.Word(disc.Exe, 0x8001B9EC) == 0x2A62000C,
                    "dragonfly egg inventory row installed; the 12-slot dragon egg row is unchanged");
                var readPlacements = UniversalPlacementReader.Read(Path.Combine(root, "toasty-native-edits.json"));
                Check(readPlacements.Single(p => p.SpecialId == UniversalDragonflyEggs.SignId).Text!.SequenceEqual(["WELCOME TO", "TOASTY!"]), "sign text exported from the project");
                var signPlacement = finalPlacements.Single(p => !p.Retired && p.SpecialId == UniversalDragonflyEggs.SignId);
                Check(saveRows[signRow] == toasty.SourceRecordCount + signPlacement.PersistentSlot,
                    "physical sign row retains its lifetime logical saved identity");
            }
            var bindings = UniversalExeBindings.Read(disc.Exe, toasty.LevelId);
            Check(bindings[UniversalExeBindings.UpdateGlobal].Value != 0x8007AF94 &&
                  bindings[UniversalExeBindings.UpdateGlobal].Value < bindings[UniversalExeBindings.CopyBufferGlobal].Value,
                  "Toasty update callback chains through the appended behavior");
            Console.WriteLine($"universal UI: {total} catalogue objects, output {output}");
            if (Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_KEEP_OUTPUT") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                File.Copy(output, Path.Combine(keep, "ui-toasty.bin"), true);
                File.WriteAllLines(Path.Combine(keep, "ui-toasty.cue"), ["FILE \"ui-toasty.bin\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]);
                Check(File.ReadAllLines(Path.Combine(keep, "ui-toasty.cue")).SequenceEqual(["FILE \"ui-toasty.bin\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]), "preserved runtime CUE has three real lines");
                File.Copy(Path.Combine(root, "toasty-native-edits.json"), Path.Combine(keep, "ui-toasty-native-edits.json"), true);
            }
            Directory.Delete(outDir, true);
            Console.WriteLine("UNIVERSAL_MOBY_UI_OK");
        }
        finally { window.Close(); }
    }

    private static void CheckBrowserReleaseStates(Window library, ListBox list, string outputDirectory)
    {
        var all = Items(list);
        var broad = all.OrderByDescending(e => e.Variants.Count).First();
        Check(broad.Variants.Count > 200, "real catalogue fixture covers more than 200 source placements");
        list.SelectedItem = broad; Pump();
        var variants = Find<ComboBox>(library, "UniversalMobyVariant");
        Check(variants.ItemsSource!.Cast<UniversalMobyVariant>().SequenceEqual(broad.Variants),
            "every source placement remains available without a silent 200-choice limit");
        var last = broad.Variants.Last();
        variants.SelectedItem = last; Pump();
        var search = Find<TextBox>(library, "UniversalMobySearch");
        search.Text = broad.ClassHex; Pump();
        Check(ReferenceEquals(list.SelectedItem, broad) && Equals(variants.SelectedItem, last),
            "search status refresh preserves the selected exact source placement");
        var summary = Find<TextBlock>(library, "UniversalMobySummary");
        Wait(() => !(summary.Text ?? "").Contains("Checking", StringComparison.Ordinal), "catalogue fit check completed", 600);
        Check(Equals(variants.SelectedItem, last), "background fit refresh preserves the chosen source placement");
        Check((Find<TextBlock>(library, "UniversalMobyTechnical").Text ?? "").Contains($"row T{last.SourceRow}", StringComparison.Ordinal),
            "details and fit controls still describe the selected placement after asynchronous checks");
        bool fitPassed = (Find<TextBlock>(library, "UniversalMobyFitText").Text ?? "").StartsWith("✓", StringComparison.Ordinal);
        Check(Find<Button>(library, "UniversalMobyPlace").IsEnabled == fitPassed &&
              Find<Button>(library, "UniversalMobyAddHere").IsEnabled == fitPassed,
            "placement actions follow the preserved source placement's completed check");
        search.Text = "no-object-could-match-this-search-123456"; Pump();
        Check(Items(list).Length == 0 && Find<TextBlock>(library, "UniversalMobyEmpty").IsVisible,
            "empty search shows an explanation");
        Check(!Find<Button>(library, "UniversalMobyPlace").IsEnabled && !Find<Button>(library, "UniversalMobyAddHere").IsEnabled &&
              variants.ItemsSource == null, "empty search clears stale selection and disables placement");
        Check(Find<Button>(library, "UniversalMobyClearSearch").IsVisible, "search can be cleared directly");
        Click(Find<Button>(library, "UniversalMobyClearSearch"));
        Check(search.Text == "" && Items(list).Length == all.Length && !Find<TextBlock>(library, "UniversalMobyEmpty").IsVisible,
            "clear search restores the full catalogue");
        library.Width = 900; library.Height = 620; Pump();
        foreach (string name in new[] { "UniversalMobyPlace", "UniversalMobyAddHere", "UniversalMobyClose" })
        {
            var button = Find<Button>(library, name);
            var point = button.TranslatePoint(new Avalonia.Point(), library);
            Check(point != null && button.Bounds.Width > 0 && button.Bounds.Height > 0 &&
                  point.Value.X >= 0 && point.Value.Y >= 0 &&
                  point.Value.X + button.Bounds.Width <= library.ClientSize.Width &&
                  point.Value.Y + button.Bounds.Height <= library.ClientSize.Height,
                "browser action stays reachable at minimum size: " + name);
        }
        Snapshot(library, Path.Combine(outputDirectory, "universal-moby-minimum-size.png"));
        library.Width = 1080; library.Height = 760; Pump();
    }

    private static int SpecialObjects(MainWindow window, string root, LevelDefinition toasty, LevelDefinition stone, string outputDirectory)
    {
        int before = Mobys(window).Count(m => m.IsAdded && !m.IsRemoved);
        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        Window library = Owned(window, "Add object — Toasty");
        ListBox list = Find<ListBox>(library, "UniversalMobyList");
        Wait(() => Items(list).Any(e => e.ClassId == 0x1E9 && e.RestrictionReason == null), "dragonfly egg available with the Japanese disc", 240);
        ListBox cats = Find<ListBox>(library, "UniversalMobyCategories");
        var cl = cats.ItemsSource!.Cast<(string, string, int)>().ToList();
        Check(cl.Any(c => c.Item2 == "Dragonfly Eggs" && c.Item3 >= 1) && cl.Any(c => c.Item2 == "Signs" && c.Item3 >= 1), "Collectibles › Dragonfly Eggs and Interactive › Signs categories");
        cats.SelectedItem = cl.First(c => c.Item2 == "Dragonfly Eggs"); Pump();
        var egg = Items(list).Single(e => e.ClassId == 0x1E9);
        list.SelectedItem = egg; Pump();
        TextBlock eggSummary = Find<TextBlock>(library, "UniversalMobySummary");
        Wait(() => !(eggSummary.Text ?? "").Contains("Checking"), "fit check finished before choosing a colour", 600);
        ComboBox variant = Find<ComboBox>(library, "UniversalMobyVariant");
        Check(variant.IsVisible && variant.ItemsSource?.Cast<object>().Count() == UniversalDragonflyEggs.Variants.Length,
            "colour choices stay available after the fit check refreshes the list");
        variant.SelectedItem = egg.Variants.Single(v => v.ClassId == 0x1F2); Pump();
        Wait(() => Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "blue egg fits Toasty", 120);
        Snapshot(library, Path.Combine(outputDirectory, "universal-moby-library-egg.png"));
        Click(Find<Button>(library, "UniversalMobyAddHere"));
        Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == before + 1, "egg added");
        Moby eggRow = Mobys(window).Last(m => m.IsAdded);
        Check(eggRow.CrossLevelTemplateId == "special:dragonfly-egg-blue", "egg keeps its colour identity");
        Task eggEdit = (Task)Invoke(window, "EditMobyAsync", eggRow)!;
        Window eggDialog = Owned(window, "Edit " + eggRow.DisplayLabel);
        Border eggModel = Find<Border>(eggDialog, "UniversalMobyModelPreview");
        Wait(() => eggModel.Tag is UniversalMobyModelPreview.Frame, "egg edit source preview loaded", 120);
        var blueModel = (UniversalMobyModelPreview.Frame)eggModel.Tag!;
        Check(blueModel.Pixels != null && blueModel.VertexCount > 20, "egg Edit dialog shows actual no-eyes source model");
        eggDialog.GetLogicalDescendants().OfType<ScrollViewer>().First().ScrollToEnd(); Pump();
        Snapshot(eggDialog, Path.Combine(outputDirectory, "universal-egg-edit-preview.png"));
        ComboBox editColours = Find<ComboBox>(eggDialog, "UniversalEggColourBox");
        editColours.SelectedItem = editColours.ItemsSource!.Cast<object>().Single(c => c.ToString() == "Dragonfly Egg (gold)"); Pump();
        Border changedModel = Find<Border>(eggDialog, "UniversalMobyModelPreview");
        Wait(() => changedModel.Tag is UniversalMobyModelPreview.Frame, "changed egg colour preview loaded", 120);
        Check(((UniversalMobyModelPreview.Frame)changedModel.Tag!).SourceHash != blueModel.SourceHash, "egg colour preview uses the chosen variant's actual palette");
        Click(eggDialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Cancel"));
        Wait(() => eggEdit.IsCompleted, "egg edit canceled");
        Check(eggRow.CrossLevelTemplateId == "special:dragonfly-egg-blue", "canceling colour preview preserves the saved egg identity");

        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        library = Owned(window, "Add object — Toasty");
        list = Find<ListBox>(library, "UniversalMobyList");
        Wait(() => Items(list).Any(e => e.ClassId == UniversalDragonflyEggs.SignClass && e.RestrictionReason == null), "sign available", 240);
        list.SelectedItem = Items(list).Single(e => e.ClassId == UniversalDragonflyEggs.SignClass); Pump();
        Wait(() => Find<Button>(library, "UniversalMobyAddHere").IsEnabled, "sign fits Toasty", 120);
        Click(Find<Button>(library, "UniversalMobyAddHere"));
        Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == before + 2, "sign added");
        Moby sign = Mobys(window).Last(m => m.IsAdded);
        Check(sign.SignText == "HELLO!", "new sign starts with a default message");
        var liveInput = (TextBox)Invoke(window, "BuildUniversalSignTextBox", sign)!;
        Control livePanel = (Control)Invoke(window, "BuildUniversalSignPreview", liveInput)!;
        liveInput.Text = "hello @ there\nsecond!\nthird?\nfourth"; Pump();
        Check(Find<TextBlock>(livePanel, "UniversalSignLivePreview").Text == "HELLO  THERE\nSECOND!\nTHIRD?", "sign live preview reflects glyph and line constraints");
        Check((Find<TextBlock>(livePanel, "UniversalSignConstraintStatus").Text ?? "").Contains("removed"), "sign constraints disclose normalization");
        var swatches = (ComboBox)Invoke(window, "BuildUniversalEggColourBox", eggRow)!;
        Check(swatches.ItemTemplate != null && swatches.ItemsSource!.Cast<object>().Count() == 7, "egg edit choices use labeled colour swatches");
        Task signEdit = (Task)Invoke(window, "EditMobyAsync", sign)!;
        Window signDialog = Owned(window, "Edit " + sign.DisplayLabel);
        Find<TextBox>(signDialog, "UniversalSignTextBox").Text = "Welcome to\nToasty!"; Pump();
        Check(Find<TextBlock>(signDialog, "UniversalSignLivePreview").Text == "WELCOME TO\nTOASTY!", "actual sign Edit dialog previews normalized message");
        signDialog.GetLogicalDescendants().OfType<ScrollViewer>().First().ScrollToEnd(); Pump();
        Snapshot(signDialog, Path.Combine(outputDirectory, "universal-sign-edit-preview.png"));
        Click(signDialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "Apply changes"));
        Wait(() => signEdit.IsCompleted, "sign edit accepted");
        Check(sign.SignText == "WELCOME TO\nTOASTY!", "sign text normalised to the game's letters");

        // Duplicate the egg, move/rotate the copy, then delete it.
        SetField(window, "_selectedMoby", eggRow);
        Invoke(window, "CopySelectedMoby");
        var paste = (Task)Invoke(window, "PasteMobyClipboardAtViewportAsync", new Avalonia.Point(400, 300))!;
        Wait(() => paste.IsCompleted, "paste egg");
        Wait(() => Mobys(window).Count(m => m.IsAdded && !m.IsRemoved) == before + 3, "egg duplicated");
        Moby copy = Mobys(window).Last(m => m.IsAdded && !m.IsRemoved);
        Check(copy.CrossLevelTemplateId == eggRow.CrossLevelTemplateId && copy.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature, "duplicate keeps egg identity");
        Invoke(window, "SetUniversalEggColour", copy, "dragonfly-egg-gold");
        Save(window);
        var both = UniversalPlacementReader.Read(Path.Combine(root, "toasty-native-edits.json"));
        Check(both.Count(p => p.SpecialId == "dragonfly-egg-blue") == 1 && both.Count(p => p.SpecialId == "dragonfly-egg-gold") == 1, "duplicate recoloured to gold and both eggs saved");
        SetField(window, "_selectedMoby", copy);
        Invoke(window, "RemoveSelectedMoby");
        Save(window);
        var saved = UniversalPlacementReader.Read(Path.Combine(root, "toasty-native-edits.json"));
        Check(saved.Count(p => p.SpecialId == "dragonfly-egg-blue") == 1 && !saved.Any(p => p.SpecialId == "dragonfly-egg-gold"), "deleted duplicate is gone from the project");

        // Reopen: identity and message persist.
        Load(window, stone); Load(window, toasty);
        Moby reopened = Mobys(window).Single(m => m.CrossLevelTemplateId == "special:sign");
        Check(reopened.SignText == "WELCOME TO\nTOASTY!", "sign message persists after reopening");
        Check(Mobys(window).Count(m => m.CrossLevelTemplateId == "special:dragonfly-egg-blue" && !m.IsRemoved) == 1, "egg persists after reopening");
        return 2;
    }

    private static void SetField(MainWindow w, string name, object? value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(w, value);

    private static UniversalSourceLibrary Library(MainWindow w)
    {
        var field = typeof(MainWindow).GetField("_universalCatalog", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var value = field.GetValue(w)!;
        return (UniversalSourceLibrary)value.GetType().GetField("Item2")!.GetValue(value)!;
    }
    private static List<Moby> Mobys(MainWindow w) => Get<List<Moby>>(w, "_currentMobys");
    private static void Load(MainWindow w, LevelDefinition level)
    {
        var t = (Task)Invoke(w, "SelectLevelAsync", level, null)!;
        Wait(() => t.IsCompleted, "load " + level.DisplayName, 240);
        if (t.IsFaulted) throw t.Exception!;
        Wait(() => ReferenceEquals(Get<LevelDefinition>(w, "_currentLevel"), level) && Mobys(w).Count > 0, "loaded " + level.DisplayName);
    }
    private static void Save(MainWindow w)
    {
        var t = (Task<bool>)Invoke(w, "SaveCurrentEditsAsync", new object?[] { null })!;
        Wait(() => t.IsCompleted, "save", 120);
        Check(t.Result, "save succeeded");
    }
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static UniversalMobyCatalogEntry[] Items(ListBox list) => list.ItemsSource?.Cast<UniversalMobyCatalogEntry>().ToArray() ?? [];
    private static Window Owned(Window owner, string title) { Wait(() => owner.OwnedWindows.Any(w => w.Title == title), title); return owner.OwnedWindows.Last(w => w.Title == title); }
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(); }
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static void Wait(Func<bool> predicate, string label, int seconds = 60)
    { var clock = Stopwatch.StartNew(); while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Pump(); Thread.Sleep(10); } Check(predicate(), label); Pump(); }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Universal Moby UI: " + label); }
    private static string Sha(string path) { using var fs = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(fs)); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
