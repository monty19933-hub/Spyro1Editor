using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

/// <summary><c>--level-colours-export-ui &lt;output&gt; &lt;usa disc&gt;</c>: colours level by level, through the editor, in a new
/// project with nothing else. Artisans' gnorcs are made red and Dark Hollow's blue on the Creatures' colours card (each
/// level keeps its own choices, Randomize and Their Own Colours work); Spyro gets the Ember look in Artisans only and
/// Ice in Dark Hollow only on his card; Create BIN makes the game; and in the game's files Artisans has red gnorcs and
/// an Ember Spyro, Dark Hollow blue gnorcs and an Ice Spyro, and Stone Hill (and the title screen) everything as it
/// was. The BIN is left in the project's output folder for a look in an emulator.</summary>
internal static class LevelColoursUiCases
{
    private static int _checks;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    internal static void Run(string outDir, string disc)
    {
        _checks = 0;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        if (!File.Exists(disc)) throw new ArgumentException("The test needs the original disc image.");
        string settings = Directory.CreateDirectory(Path.Combine(workspace, "_local", "settings")).FullName;
        File.WriteAllText(Path.Combine(settings, "source-disc.json"), JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), selectedPath = disc, imagePath = disc, cuePath = Path.ChangeExtension(disc, ".cue") }));
        File.WriteAllText(Path.Combine(settings, "last-level.json"), "{\"levelKey\":\"artisans\"}");
        string support = Directory.CreateDirectory(Path.Combine(workspace, "support")).FullName;
        string repo = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog found.");
        foreach (string name in new[] { "spyro-level-catalog.json", "spyro-object-templates.json" })
            if (File.Exists(Path.Combine(repo, name))) File.Copy(Path.Combine(repo, name), Path.Combine(support, name));
        foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(outDir, "isolated", name)).FullName);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.CurrentDirectory = workspace;
        string found = Spyro.Editor.Core.Workspace.EditorWorkspace.Find().RootPath;
        if (Path.GetFullPath(found).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
            throw new InvalidOperationException("The editor would open " + found + " instead of the test project; stopping before it opens.");
        MainWindow window = Open();
        try
        {
            string opened = Get<Spyro.Editor.Core.Workspace.EditorWorkspace>(window, "_workspace").RootPath;
            if (Path.GetFullPath(opened).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
                throw new InvalidOperationException("The editor opened " + opened + " instead of the test project; stopping before any change.");
            var red = ((byte)200, (byte)36, (byte)36); var blue = ((byte)40, (byte)80, (byte)210);

            // ---- Artisans: its enemy kinds, Randomize, then the gnorcs red ----
            ShowCard(window, "CreatureColourSection");
            var artisans = Kinds(window);
            Console.WriteLine("Artisans: " + string.Join(", ", artisans.Select(k => $"{k.Name} ({k.Key})")));
            var gnorcs = artisans.Where(k => k.Name.Contains("Gnorc", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(artisans.Count >= 1 && gnorcs.Length >= 1 && artisans.All(k => k.Key.StartsWith("artisans:", StringComparison.Ordinal)), "Artisans' card lists its enemies, gnorcs among them");
            Check(Find<Button>(window, "CreatureColourRandomize").IsEnabled && !Find<Button>(window, "CreatureColourOwn").IsEnabled && window.CreatureColourNoteForTesting.Contains("has its own colours", StringComparison.Ordinal),
                "nothing is chosen at first: the note says so and Their Own Colours is off");
            window.RandomizeCreatureColoursForTesting(7); Pump(30);
            var random1 = new Dictionary<string, (byte R, byte G, byte B)>(window.CreatureColoursForTesting);
            window.RandomizeCreatureColoursForTesting(8); Pump(30);
            Check(random1.Count == artisans.Count && window.CreatureColoursForTesting.Count == artisans.Count && !random1.SequenceEqual(window.CreatureColoursForTesting),
                "Randomize gives every kind a colour, and other colours the next time");
            Check(CreatureColourSettings.Load(workspace).TryGetValue("artisans", out var savedA) && savedA.Count == artisans.Count, "the colours are kept in the project, under Artisans");
            Click(Find<Button>(window, "CreatureColourOwn"));
            Check(window.CreatureColoursForTesting.Count == 0 && !File.Exists(CreatureColourSettings.PathFor(workspace)), "Their Own Colours clears them (and the file, when nothing is left)");
            foreach (var g in gnorcs) window.SetCreatureColourForTesting(g.Key, red);
            Pump(60);
            Check(window.CreatureColoursForTesting.Count == gnorcs.Length && Status(window).Contains("#C82424", StringComparison.Ordinal), "the gnorcs are made red: " + Status(window));
            ShowCard(window, "CreatureColourSection"); Pump(150);
            Snapshot(window, Path.Combine(outDir, "1-artisans-creatures.png"));

            // ---- Dark Hollow: its own kinds and its own choices ----
            NewLevelBuildCases.Load(window, "darkhollow");
            ShowCard(window, "CreatureColourSection");
            var hollow = Kinds(window);
            Console.WriteLine("Dark Hollow: " + string.Join(", ", hollow.Select(k => $"{k.Name} ({k.Key})")));
            var hollowGnorcs = hollow.Where(k => k.Name.Contains("Gnorc", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(hollow.Count >= 1 && hollowGnorcs.Length >= 1 && hollow.All(k => k.Key.StartsWith("darkhollow:", StringComparison.Ordinal)) && window.CreatureColoursForTesting.Count == 0,
                "Dark Hollow lists its own enemies, none with a chosen colour (Artisans' choices are Artisans')");
            foreach (var g in hollowGnorcs) window.SetCreatureColourForTesting(g.Key, blue);
            Pump(60);
            ShowCard(window, "CreatureColourSection"); Pump(150);
            Snapshot(window, Path.Combine(outDir, "2-darkhollow-creatures.png"));
            var all = CreatureColourSettings.Load(workspace);
            Check(all.Count == 2 && all["artisans"].Values.All(c => c == red) && all["darkhollow"].Values.All(c => c == blue), "each level has its own colours in the project");

            // ---- Spyro: Ice in Dark Hollow only, Ember in Artisans only ----
            ShowCard(window, "SpyroColourSection");
            var scope = Find<ComboBox>(window, "SpyroColourScope");
            Check(scope.SelectedIndex == 0 && !Find<Button>(window, "SpyroColourSameAsGame").IsVisible, "Spyro's card starts on the whole game");
            scope.SelectedIndex = 1; Pump(20);
            Check(((string[])scope.ItemsSource!)[1] == "Dark Hollow only" && Find<Button>(window, "SpyroColourSameAsGame") is { IsVisible: true, IsEnabled: false } &&
                  window.SpyroColourSummaryForTesting.Contains("has the whole game's look", StringComparison.Ordinal), "the card can be set to the open level, which has the whole game's look at first");
            window.ChooseSpyroPresetForTesting("Ice"); Pump(30);
            Check(window.SpyroLevelHasOwnLookForTesting && window.SpyroColourSummaryForTesting.Contains("Dark Hollow has its own look", StringComparison.Ordinal) && Status(window).Contains("In Dark Hollow", StringComparison.Ordinal),
                "choosing a look gives Dark Hollow its own: " + Status(window));
            Pump(150);
            Snapshot(window, Path.Combine(outDir, "3-darkhollow-spyro.png"));
            NewLevelBuildCases.Load(window, "artisans");
            ShowCard(window, "SpyroColourSection");
            Check(((string[])scope.ItemsSource!)[1] == "Artisans only" && scope.SelectedIndex == 1 && !window.SpyroLevelHasOwnLookForTesting, "in Artisans the card shows Artisans, which has no look of its own yet");
            window.ChooseSpyroPresetForTesting("Ember"); Pump(30);
            // A level's look can be taken away again and given back.
            window.SameSpyroLookAsGameForTesting(); Pump(20);
            Check(!window.SpyroLevelHasOwnLookForTesting && !SpyroColourSettings.LoadAll(workspace).Levels.ContainsKey("artisans"), "Same as the Whole Game takes Artisans' look away");
            window.ChooseSpyroPresetForTesting("Ember"); Pump(30);
            var book = SpyroColourSettings.LoadAll(workspace);
            Check(book.Whole.Count == 0 && book.Levels.Count == 2 && book.Levels["artisans"].Count == 7 && book.Levels["darkhollow"].Count == 8, "the project keeps Ember for Artisans and Ice for Dark Hollow; the whole game's look is his own");
            scope.SelectedIndex = 0; Pump(20);
            Check(window.SpyroColourSummaryForTesting == "Spyro has his own colours.", "back on the whole game the card shows the whole game's look");
            // The creatures' choices are still each level's.
            ShowCard(window, "CreatureColourSection"); Kinds(window);
            Check(window.CreatureColoursForTesting.Count == gnorcs.Length && window.CreatureColoursForTesting.Values.All(c => c == red), "back in Artisans its gnorcs are still red");

            // ---- the game ----
            string created = CreateBin(window);
            File.WriteAllText(Path.Combine(outDir, "status.txt"), created + "\n");
            string output = Path.Combine(workspace, "output");
            string[] bins = Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            Check(created.StartsWith("Created ", StringComparison.Ordinal) && bins.Length == 1, "Create BIN makes the game from these settings alone: " + created);
            var original = UniversalDiscImage.Read(disc);
            var made = UniversalDiscImage.Read(bins[0]);
            var catalog = Get<LevelCatalog>(window, "_retailCatalog");
            byte[] Entry(UniversalDiscImage image, string key) => image.EntryBytes(catalog.FindByKey(key)!.SourceWadEntry);
            byte[] originals = NativeSpyroColours.FindOriginals(original.EntryBytes)!;
            byte[] ember = NativeSpyroColours.Recolour(originals, book.Levels["artisans"]), ice = NativeSpyroColours.Recolour(originals, book.Levels["darkhollow"]);
            bool Has(byte[] level, byte[] look) => Enumerable.Range(0, 17).All(k => level.AsSpan(0x800 + NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(look.AsSpan(k * 32, 32)));
            Check(Has(Entry(made, "artisans"), ember) && Has(Entry(made, "darkhollow"), ice), "in the game Spyro is Ember in Artisans and Ice in Dark Hollow");
            int own = catalog.Levels.Count(l => l.Key is not ("artisans" or "darkhollow") && Has(made.EntryBytes(l.SourceWadEntry), originals));
            Check(own >= 33 && Has(Entry(made, "stonehill"), originals), $"and his own colours in every other level ({own})");
            Check(Enumerable.Range(3, 4).All(e => made.EntryBytes(e).AsSpan().SequenceEqual(original.EntryBytes(e))), "the title screen and cutscenes are as they were");
            // The gnorcs: red in Artisans, blue in Dark Hollow, as they were elsewhere.
            static double Hue((byte R, byte G, byte B) c) => NativeSpyroColours.ToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0).H;
            int ClassOf(string key) => CreatureColourSettings.TryParseKind(key, out _, out int cls) ? cls : -1;
            var madeArtisans = NativeCreatureColours.Kinds(Entry(made, "artisans")).ToDictionary(k => k.ClassId);
            var madeHollow = NativeCreatureColours.Kinds(Entry(made, "darkhollow")).ToDictionary(k => k.ClassId);
            foreach (var g in gnorcs)
            {
                var c = madeArtisans[ClassOf(g.Key)].Colour;
                Check(c.R > c.G + 60 && c.R > c.B + 60, $"in the game Artisans' {g.Name} is red (#{c.R:X2}{c.G:X2}{c.B:X2})");
            }
            foreach (var g in hollowGnorcs)
            {
                var c = madeHollow[ClassOf(g.Key)].Colour;
                Check(c.B > c.R + 60 && c.B > c.G + 30, $"in the game Dark Hollow's {g.Name} is blue (#{c.R:X2}{c.G:X2}{c.B:X2})");
            }
            int changed = catalog.Levels.Count(l => !made.EntryBytes(l.SourceWadEntry).AsSpan().SequenceEqual(original.EntryBytes(l.SourceWadEntry)));
            // The program is the game's own with only the 8 MB memory setting put in (a project's setting unless it chose
            // PlayStation (2 MB), since 2.3).
            byte[] expectedExe = original.Exe.ToArray();
            NativeExtendedRam.PatchExe(expectedExe);
            Check(changed == 2 && made.Exe.AsSpan().SequenceEqual(expectedExe), $"only Artisans and Dark Hollow differ from the original game ({changed} levels), and the program is the game's own with the 8 MB setting");
            // What is not a gnorc's colour stays: the other kinds of both levels are as they were.
            var originalArtisans = NativeCreatureColours.Kinds(Entry(original, "artisans")).ToDictionary(k => k.ClassId);
            var gnorcClasses = gnorcs.Select(g => ClassOf(g.Key)).ToHashSet();
            // (A kind that shares a palette with a gnorc takes its colour; the rest keep theirs.)
            int kept = originalArtisans.Count(k => !gnorcClasses.Contains(k.Key) && madeArtisans.TryGetValue(k.Key, out var now) && now.Colour == k.Value.Colour);
            Check(kept >= originalArtisans.Count - gnorcClasses.Count - 3, $"Artisans' other kinds keep their colours ({kept} of {originalArtisans.Count - gnorcClasses.Count})");
            Console.WriteLine($"LEVEL_COLOURS_EXPORT_UI_OK checks={_checks}");
            Console.WriteLine("BIN " + bins[0]);
        }
        finally { window.Close(); Pump(10); }
    }

    /// <summary>The kinds on the card once the level's enemies have been read.</summary>
    private static IReadOnlyList<(string Key, string Name)> Kinds(MainWindow window)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(180))
        {
            Pump(10);
            if (window.CreatureKindsReadyForTesting.IsCompleted) { Pump(30); if (window.CreatureKindsReadyForTesting.IsCompleted && (window.CreatureKindsForTesting.Count > 0 || clock.Elapsed > TimeSpan.FromSeconds(20))) break; }
        }
        return window.CreatureKindsForTesting;
    }

    private static void ShowCard(MainWindow window, string name)
    {
        Get<TabItem>(window, "_modernLevelWorkspaceTab").IsSelected = true; Pump(20);
        var card = Find<Expander>(window, name);
        card.IsExpanded = true; Pump(20);
        card.BringIntoView(); Pump(20);
    }

    private static string CreateBin(MainWindow window)
    {
        var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
        var clock = Stopwatch.StartNew();
        while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(40))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope") is { } scope) { Click(Find<Button>(scope, "ExportCreate")); continue; }
            foreach (Window other in window.OwnedWindows.ToArray())
            {
                Console.WriteLine("level colours export: dialog '" + other.Title + "': " + string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                other.Close();
            }
        }
        Pump(100);
        Check(command.IsCompleted, "Create BIN finished");
        return Status(window);
    }

    private static MainWindow Open()
    {
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart();
            else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return window;
        }
        throw new TimeoutException("The editor did not become idle.");
    }

    private static object? Invoke(MainWindow w, string name, params object?[] args) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(w, args);
    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(5); }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Level colours UI: " + label); Console.WriteLine("ok   " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
