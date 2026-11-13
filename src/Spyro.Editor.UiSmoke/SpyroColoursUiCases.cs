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
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

/// <summary>Game Settings › Spyro's colours.
/// <list type="bullet">
/// <item><c>--spyro-colours-ui-only</c>: a throwaway project, no disc: the card sits with the other whole-game cards and
/// lists every part; a preset saves its colours at once (spyro-colours.json), the picker's #RRGGBB box and Use set one
/// part, a part's "His own" puts it back, Classic removes the file, a reopened editor shows the saved colours, and a
/// damaged file is reported (Create BIN then stops) rather than overwritten.</item>
/// <item><c>--spyro-colours-export-ui &lt;output&gt; [project]</c>: without a project, a new one with only the original
/// disc: a preset and Create BIN, which makes the game from Game Settings alone (every level, the title screen and the
/// cutscenes recoloured, the executable the USA game's). With a project (cloned, never changed): the same preset on its
/// edits, built levels and world. The BIN is kept for the emulator.</item>
/// </list></summary>
internal static class SpyroColoursUiCases
{
    private static int _checks;
    private static readonly string[] IsolatedRoots = ["SPYRO_EDITOR_DATA_ROOT", "SPYRO_EDITOR_PROJECTS_ROOT", "SPYRO_EDITOR_DIAGNOSTICS_DIR"];

    internal static void Run()
    {
        _checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-colours-ui-").FullName;
        string root = Directory.CreateDirectory(Path.Combine(parent, "project")).FullName;
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
        {
            levels = new[] { new { key = "stonehill", displayName = "Stone Hill", levelId = 11 } }
        }));
        string previousDirectory = Environment.CurrentDirectory;
        string?[] previous = [.. new[] { "SPYRO_EDITOR_WORKSPACE", "SPYRO_EDITOR_RELEASE" }.Concat(IsolatedRoots).Select(Environment.GetEnvironmentVariable)];
        string file = SpyroColourSettings.PathFor(root);
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            foreach (string name in IsolatedRoots) Environment.SetEnvironmentVariable(name, Directory.CreateDirectory(Path.Combine(parent, name)).FullName);
            var window = Measure();
            var section = Find<Control>(window, "SpyroColourSection");
            Check(section.Parent != null && section.Parent == Find<Control>(window, "HoverSection").Parent, "Spyro's colours is a card beside Hover");
            Check(NativeSpyroColours.Parts.All(p => Has(window, "SpyroColourSwatch_" + p.Key)), "every part has a colour swatch");
            string text = string.Join(" ", section.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Check(text.Contains("Head spikes") && text.Contains("spiky fins behind his head") && text.Contains("Tail tip") && text.Contains("Pupils") &&
                text.Contains("keeps his shading") && text.Contains("cutscenes"), "the card names the parts and says what changes");
            Check(Summary(window) == "Spyro has his own colours." && !File.Exists(file) && !Has(window, "SpyroColourReset_body"), "his own colours by default, nothing saved");
            Check(Has(window, "SpyroColourPreviewHost") && Find<TextBlock>(window, "SpyroColourPreviewNote").Text == "Open your game to see Spyro here." &&
                !Find<Image>(window, "SpyroColourPreview").IsVisible, "the picture of Spyro waits for the game to be opened");

            Click(Find<Button>(window, "SpyroColourPresetEmber"));
            var saved = SpyroColourSettings.Load(root);
            Check(saved.Count == 7 && saved["body"] == (0xC0, 0x28, 0x2D) && !saved.ContainsKey("eyes"), "a preset saves its colours at once");
            Check(Status(window).Contains("Ember look") && Summary(window).StartsWith("7 of 9 parts changed", StringComparison.Ordinal), "status and summary say so: " + Summary(window));
            Check(Has(window, "SpyroColourReset_body") && !Has(window, "SpyroColourReset_eyes"), "changed parts offer His own");

            // The picker: type a colour and use it.
            var eyes = Find<Button>(window, "SpyroColourSwatch_eyes");
            var picker = (Flyout)eyes.Flyout!;
            var content = (Control)picker.Content!;
            var hex = Find<TextBox>(content, "SpyroColourHex");
            hex.Text = "#FFE066"; Pump();
            Check(Find<Slider>(content, "SpyroColourHue").Value is > 40 and < 55, "typing a colour moves the sliders to it");
            Click(Find<Button>(content, "SpyroColourUse"));
            Check(SpyroColourSettings.Load(root).TryGetValue("eyes", out var e) && e == (0xFF, 0xE0, 0x66) && Status(window).Contains("#FFE066"), "Use sets the part's colour");
            // Sliders: a full-brightness choice is white whatever the hue.
            var wings = Find<Button>(window, "SpyroColourSwatch_wingSkin");
            var wc = (Control)((Flyout)wings.Flyout!).Content!;
            Find<Slider>(wc, "SpyroColourBrightness").Value = 100; Pump();
            Check(Find<TextBox>(wc, "SpyroColourHex").Text == "#FFFFFF", "the brightness slider reaches white");
            Click(Find<Button>(wc, "SpyroColourUse"));
            Check(SpyroColourSettings.Load(root)["wingSkin"] == (255, 255, 255), "a slider colour is used too");

            Click(Find<Button>(window, "SpyroColourReset_body"));
            Check(!SpyroColourSettings.Load(root).ContainsKey("body") && Status(window).Contains("his own colour again"), "His own puts one part back");
            var reopened = Measure();
            Check(Summary(reopened).StartsWith("7 of 9 parts changed", StringComparison.Ordinal) && Has(reopened, "SpyroColourReset_eyes") && !Has(reopened, "SpyroColourReset_body"),
                "a reopened editor shows the saved colours");
            // Surprise me: a made-up look, different each time, saved like any other.
            Click(Find<Button>(reopened, "SpyroColourSurprise"));
            var first = SpyroColourSettings.Load(root);
            Click(Find<Button>(reopened, "SpyroColourSurprise"));
            var second = SpyroColourSettings.Load(root);
            Check(first.Count == 7 && second.Count == 7 && first["tailTip"] == first["spikes"] && !first.ContainsKey("eyes") &&
                (first["body"] != second["body"] || first["horns"] != second["horns"]) && Status(reopened).StartsWith("A new look for Spyro", StringComparison.Ordinal),
                "Surprise me makes up a look, another each time");
            Click(Find<Button>(reopened, "SpyroColourPresetClassic"));
            Check(!File.Exists(file) && Summary(reopened) == "Spyro has his own colours.", "Classic removes the file");

            File.WriteAllText(file, "{ not json");
            var damaged = Measure();
            Check(Summary(damaged).StartsWith("The saved colours could not be read", StringComparison.Ordinal) && File.ReadAllText(file) == "{ not json",
                "a damaged file is reported and left as it was");
            var use = typeof(MainWindow).GetMethod("UseProjectSpyroColours", BindingFlags.Instance | BindingFlags.NonPublic)!;
            bool stops = false;
            try { ((IDisposable)use.Invoke(damaged, null)!).Dispose(); }
            catch (TargetInvocationException x) when (x.InnerException is InvalidDataException d && d.Message.Contains("Spyro's colours")) { stops = true; }
            Check(stops && !NativeSpyroColours.Enabled, "Create BIN stops on the damaged file instead of guessing");
            Click(Find<Button>(damaged, "SpyroColourPresetGold"));
            Check(SpyroColourSettings.Load(root).Count == 7, "choosing again replaces the damaged file");
            File.Delete(file);

            // Colours level by level (2.1): the card's "whole game / this level" switch, and the level's Creatures' colours card.
            var levelled = Measure();
            var scope = Find<ComboBox>(levelled, "SpyroColourScope");
            Check(scope.ItemCount == 2 && scope.SelectedIndex == 0 && !Find<Button>(levelled, "SpyroColourSameAsGame").IsVisible, "the card starts on the whole game's look");
            levelled.SetSpyroColourScopeForTesting(true);
            if (scope.IsEnabled)
            {
                // A level is open: its own look is chosen, kept beside the whole game's, and taken away again.
                levelled.ChooseSpyroPresetForTesting("Shadow");
                var book = SpyroColourSettings.LoadAll(root);
                Check(book.Whole.Count == 0 && book.Levels.Count == 1 && book.Levels.Values.Single().Count == 8 && levelled.SpyroLevelHasOwnLookForTesting &&
                      Find<Button>(levelled, "SpyroColourSameAsGame") is { IsVisible: true, IsEnabled: true }, "set to the open level, a look is that level's own and the whole game keeps his colours");
                levelled.SameSpyroLookAsGameForTesting();
                Check(!File.Exists(file) && !levelled.SpyroLevelHasOwnLookForTesting, "Same as the Whole Game takes the level's look away (and the file, when nothing is left)");
            }
            else
            {
                Check(scope.SelectedIndex == 0 && !Find<Button>(levelled, "SpyroColourSameAsGame").IsVisible, "without an open level there is only the whole game's look");
                Check(!File.Exists(file), "and nothing is saved");
            }
            Check(Has(levelled, "CreatureColourSection") && Has(levelled, "CreatureColourRandomize") && !Find<Button>(levelled, "CreatureColourRandomize").IsEnabled &&
                  !Find<Button>(levelled, "CreatureColourOwn").IsEnabled && levelled.CreatureKindsForTesting.Count == 0,
                "the level's Creatures' colours card is there; without the game's disc it lists nothing and its buttons are off");
            Check(levelled.CreatureColourNoteForTesting.Length > 0 && !File.Exists(CreatureColourSettings.PathFor(root)), "it says why, and saves nothing: " + levelled.CreatureColourNoteForTesting);
            File.WriteAllText(CreatureColourSettings.PathFor(root), "{ not json");
            bool creaturesStop = false;
            try { ((IDisposable)use.Invoke(levelled, null)!).Dispose(); }
            catch (TargetInvocationException x) when (x.InnerException is InvalidDataException d && d.Message.Contains("level's colours", StringComparison.OrdinalIgnoreCase)) { creaturesStop = true; }
            Check(creaturesStop && !NativeSpyroColours.Enabled, "Create BIN stops on a damaged creatures' colours file instead of guessing");
            File.Delete(CreatureColourSettings.PathFor(root));
            Check(File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected" &&
                !Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any(), "no other file is touched and no game is written");
            Console.WriteLine($"SPYRO_COLOURS_UI_OK checks={_checks}");
        }
        finally
        {
            Environment.CurrentDirectory = previousDirectory;
            string[] names = [.. new[] { "SPYRO_EDITOR_WORKSPACE", "SPYRO_EDITOR_RELEASE" }.Concat(IsolatedRoots)];
            for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], previous[i]);
            try { Directory.Delete(parent, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The export case (see the class notes). <paramref name="disc"/>: the original USA image for a new project.</summary>
    internal static void RunExport(string outDir, string? project, string? disc, bool hover = false)
    {
        _checks = 0;
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        if (project != null)
        {
            var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
            clone!.WaitForExit();
            if (clone.ExitCode != 0 || !Directory.Exists(workspace)) throw new IOException("Could not clone the project folder.");
            foreach (string old in Directory.Exists(Path.Combine(workspace, "output")) ? Directory.GetFiles(Path.Combine(workspace, "output"), "*.bin") : []) File.Delete(old);
        }
        else
        {
            if (disc == null || !File.Exists(disc)) throw new ArgumentException("A new project needs the original disc image.");
            string settings = Directory.CreateDirectory(Path.Combine(workspace, "_local", "settings")).FullName;
            string cue = Path.ChangeExtension(disc, ".cue");
            File.WriteAllText(Path.Combine(settings, "source-disc.json"), JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), selectedPath = disc, imagePath = disc, cuePath = cue }));
            File.WriteAllText(Path.Combine(settings, "last-level.json"), "{\"levelKey\":\"artisans\"}");
            // A project folder is one with the level catalog (the repository's, as a new project has).
            string support = Directory.CreateDirectory(Path.Combine(workspace, "support")).FullName;
            string repo = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(repo, "spyro-level-catalog.json"))) repo = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repo)) ?? throw new IOException("No level catalog found.");
            foreach (string name in new[] { "spyro-level-catalog.json", "spyro-object-templates.json" })
                if (File.Exists(Path.Combine(repo, name))) File.Copy(Path.Combine(repo, name), Path.Combine(support, name));
        }
        // Hover too (Game Settings › Hover): its code and wing flap go in with the colours.
        if (hover) SpyroMovesSettings.SaveHover(workspace, true);
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
            // Never act on another folder (the editor falls back to the repository or the user's own workspace).
            string opened = Get<Spyro.Editor.Core.Workspace.EditorWorkspace>(window, "_workspace").RootPath;
            if (Path.GetFullPath(opened).TrimEnd('/') != Path.GetFullPath(workspace).TrimEnd('/'))
                throw new InvalidOperationException("The editor opened " + opened + " instead of the test project; stopping before any change.");
            ShowCard(window);
            Snapshot(window, Path.Combine(outDir, "0-colours-card.png"));
            Click(Find<Button>(window, "SpyroColourPresetEmber"));
            var eyes = (Control)((Flyout)Find<Button>(window, "SpyroColourSwatch_eyes").Flyout!).Content!;
            Find<TextBox>(eyes, "SpyroColourHex").Text = "#FFE066"; Pump();
            Click(Find<Button>(eyes, "SpyroColourUse"));
            var colours = SpyroColourSettings.Load(workspace);
            Check(colours.Count == 8, "Ember with yellow eyes is chosen");
            ShowCard(window);
            // The picture of Spyro: drawn from the disc's own stand-in model, in the chosen colours (Ember: mostly red).
            var picture = Find<Image>(window, "SpyroColourPreview");
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(60) && !(picture.IsVisible && picture.Source != null && Get<Dictionary<string, (byte R, byte G, byte B)>?>(window, "_spyroPreviewPending") == null && !Get<bool>(window, "_spyroPreviewBusy"))) Pump(4);
            Check(picture.IsVisible && picture.Source is Avalonia.Media.Imaging.WriteableBitmap, "the card shows a picture of Spyro");
            using (var locked = ((Avalonia.Media.Imaging.WriteableBitmap)picture.Source!).Lock())
            {
                int red = 0, purple = 0, drawn = 0;
                byte[] line = new byte[locked.RowBytes];
                for (int y = 0; y < locked.Size.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(locked.Address + y * locked.RowBytes, line, 0, line.Length);
                    for (int x = 0; x < locked.Size.Width; x++)
                    {
                        int r = line[4 * x], g = line[4 * x + 1], b = line[4 * x + 2], a = line[4 * x + 3];
                        if (a == 0) continue;
                        drawn++;
                        if (r > g + 60 && r > b + 60) red++;
                        if (b > g + 25 && b > r && r > g) purple++;
                    }
                }
                Check(drawn > 4000 && red > drawn / 4 && purple < drawn / 20, $"the picture is Spyro in the chosen look (drawn {drawn}, red {red}, purple {purple})");
            }
            Snapshot(window, Path.Combine(outDir, "1-colours-chosen.png"));
            string created = CreateBin(window);
            File.WriteAllText(Path.Combine(outDir, "status.txt"), created + "\n");
            string output = Path.Combine(workspace, "output");
            string[] bins = Directory.Exists(output) ? Directory.GetFiles(output, "*.bin") : [];
            Check(created.StartsWith("Created ", StringComparison.Ordinal) && bins.Length == 1, "Create BIN publishes the game: " + created);
            string sourceImage;
            using (var s = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace, "_local", "settings", "source-disc.json"))))
                sourceImage = s.RootElement.GetProperty("imagePath").GetString()!;
            var original = UniversalDiscImage.Read(sourceImage);
            var made = UniversalDiscImage.Read(bins[0]);
            byte[] originals = NativeSpyroColours.FindOriginals(original.EntryBytes)!;
            byte[] recoloured = NativeSpyroColours.Recolour(originals, colours);
            // The palettes these colours change (the mouth's, and parts left alone, stay as they were).
            int[] changes = [.. Enumerable.Range(0, 17).Where(k => !recoloured.AsSpan(k * 32, 32).SequenceEqual(originals.AsSpan(k * 32, 32)))];
            Check(changes.Length >= 14, $"the colours change most of his palettes ({changes.Length})");
            int levels = 0, cutscenes = 0;
            for (int entry = 0; entry < 256; entry++)
            {
                // A new world's levels sit after the movies, outside WAD.WAD's own sectors.
                byte[] bytes;
                try { bytes = made.EntryBytes(entry); }
                catch (ArgumentOutOfRangeException) { bytes = NativeLevelWindowDisc.ReadEntry(bins[0], entry); }
                if (bytes.Length < 0x80800 || BitConverter.ToInt32(bytes, 0) != 0x800 || BitConverter.ToInt32(bytes, 4) < 0x80000) continue;
                byte[] vram = bytes.AsSpan(0x800, 0x80000).ToArray();
                if (NativeSpyroColours.IsCutsceneEntry(entry))
                {
                    bool anyOld = false, anyNew = false;
                    for (int at = 0; at < 0x80000; at += 32)
                        foreach (int k in changes)
                        {
                            anyOld |= vram.AsSpan(at, 32).SequenceEqual(originals.AsSpan(k * 32, 32));
                            anyNew |= vram.AsSpan(at, 32).SequenceEqual(recoloured.AsSpan(k * 32, 32));
                        }
                    if (anyNew && !anyOld) cutscenes++;
                    continue;
                }
                bool all = Enumerable.Range(0, 17).All(k => vram.AsSpan(NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(recoloured.AsSpan(k * 32, 32)));
                bool any = changes.Any(k => vram.AsSpan(NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(originals.AsSpan(k * 32, 32)));
                Check(!any, $"WAD entry {entry} has none of Spyro's palettes left in his own colours");
                if (all) levels++;
            }
            Check(cutscenes == 4, $"the title screen and 3 cutscenes have only his new colours ({cutscenes})");
            Check(levels >= 35, $"every level has his new colours ({levels} entries)");
            int world = Enumerable.Range(NativeNewWorldExport.FirstEntry, 10).Count(e =>
            {
                byte[] b;
                try { b = NativeLevelWindowDisc.ReadEntry(bins[0], e); } catch (Exception x) when (x is ArgumentOutOfRangeException or InvalidDataException or IOException) { return false; }
                return b.Length >= 0x80800 && BitConverter.ToInt32(b, 0) == 0x800 &&
                    Enumerable.Range(0, 17).All(k => b.AsSpan(0x800 + NativeSpyroColours.PaletteOffset(NativeSpyroColours.Palettes[k]), 32).SequenceEqual(recoloured.AsSpan(k * 32, 32)));
            });
            if (File.Exists(NewWorldDocumentPath(workspace))) Check(world >= 5, $"the new world's 5 levels have his new colours ({world})");
            if (project == null && !hover)
                Check(made.Exe.AsSpan().SequenceEqual(original.Exe), "from Game Settings alone the executable is the USA game's");
            if (hover)
            {
                byte[] expected = original.Exe.ToArray(); NativeHover.Install(expected);
                Check(project != null || made.Exe.AsSpan().SequenceEqual(expected), "from Game Settings alone the executable is the USA game's with Hover");
                Check(NativeHover.IsInstalled(made.Exe.ToArray()), "the game has Hover");
                var flap = Task.Run(() => NativeHoverFlap.CaptureAsync(sourceImage)).GetAwaiter().GetResult();
                var catalog = Get<Spyro.Editor.Core.Levels.LevelCatalog>(window, "_retailCatalog");
                int flapping = catalog.Levels.Count(l => flap.Carries(made.EntryBytes(l.SourceWadEntry)));
                Check(flapping >= 32, $"the levels have Spyro's wing flap ({flapping} of {catalog.Levels.Count})");
                if (project == null)
                    Check(flapping == 32 && created.Contains("no room for Spyro's wing flap in") && created.Contains("Twilight Harbor"),
                        "with 2 MB all but three levels flap, and Create BIN names those: " + created);
            }
            string copyPath = Path.Combine(outDir, "spyro-colours.bin");
            File.Copy(bins[0], copyPath, overwrite: true);
            File.WriteAllText(Path.ChangeExtension(copyPath, ".cue"), "FILE \"spyro-colours.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            File.Delete(bins[0]);
            Console.WriteLine($"SPYRO_COLOURS_EXPORT_UI_OK checks={_checks} levels={levels}");
        }
        finally { window.Close(); Pump(10); }
    }

    private static string NewWorldDocumentPath(string workspace) => Path.Combine(workspace, "new-world.json");

    private static void ShowCard(MainWindow window)
    {
        Get<TabItem>(window, "_modernGameWorkspaceTab").IsSelected = true; Pump(20);
        var card = Find<Expander>(window, "SpyroColourSection");
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
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope") is { } scope)
            {
                Click(Find<Button>(scope, "ExportCreate"));
                continue;
            }
            foreach (Window other in window.OwnedWindows.ToArray())
            {
                Console.WriteLine("spyro colours export: dialog '" + other.Title + "': " +
                    string.Join(" | ", other.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6)));
                other.Close();
            }
        }
        Pump(100);
        Check(command.IsCompleted, "Create BIN finished");
        Console.WriteLine("spyro colours export: " + Status(window));
        return Status(window);
    }

    private static MainWindow Measure()
    {
        var window = new MainWindow { Width = 1200, Height = 760 };
        window.Measure(new Size(1200, 760)); window.Arrange(new Rect(0, 0, 1200, 760)); Pump();
        return window;
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
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static string Summary(MainWindow window) => Find<TextBlock>(window, "SpyroColourSummary").Text ?? "";
    private static string Status(MainWindow window) => Get<TextBlock>(window, "_statusText").Text ?? "";
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(5); }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static bool Has(Control root, string name) => root.GetLogicalDescendants().OfType<Control>().Any(c => c.Name == name);
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Check(bool condition, string label) { _checks++; if (!condition) throw new InvalidOperationException("Spyro's colours UI: " + label); }
    private static void Snapshot(Window window, string path) { window.InvalidateVisual(); Pump(3); using var image = window.CaptureRenderedFrame(); image?.Save(path); }
}
