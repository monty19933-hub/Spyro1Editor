using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary><c>--level-links-ui-only</c>: File ▾ › Level Links… on a throwaway project with no disc (the game's level list
/// only): a level built in Stone Hill's place, the new level, Peace Keepers rebuilt with two doorways and a stale link of the
/// game's Peace Keepers, a world with a level that has no doorway, a game level whose Return Home vortex was deleted.
/// The real window is driven through its own controls: doorways sent to other levels, Return Home set and set back to
/// automatic, the fix for the stale link, Undo Last Change, a change refused after the routes changed elsewhere, and the
/// window's foot outside the part that scrolls. Nothing is exported and no disc is opened.</summary>
internal static class LevelLinksUiCases
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        string root = Directory.CreateTempSubdirectory("spyro-level-links-ui-").FullName;
        string previous = Environment.CurrentDirectory;
        string? workspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"), release = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        var catalog = LevelCatalog.Load(FindCatalogFolder());
        if (catalog.Levels.Count != 35) throw new InvalidOperationException("The game's level list was not found next to the test.");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), "{\"levels\":[]}");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = root; Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", root); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            // The project: Stone Hill's place rebuilt as SUNNY GLEN (no vortex; the game's Stone Hill has one), the new level,
            // Peace Keepers rebuilt with doorways to Dry Canyon and Cliff Town only, a world whose second level has no doorway
            // and whose homeworld has no doorway home, and Dark Hollow with its vortex deleted.
            string sha = UniversalSourceLibrary.OriginalUsaSha256;
            var glen = SceneDocument.New("SUNNY GLEN") with { TargetBinding = new("stonehill", 11, 12, 11, sha, 0) };
            Off(() => glen.SaveAsync(SceneDocumentStore.PathForTarget(root, "stonehill")));
            var exit = new Moby { TrueIndex = 0, SourceByte36 = LevelCompletionCheck.ReturnHomeClass, SourceByte37 = 0, IsAdded = true, Label = "Return Home" };
            var mine = SceneDocument.New("MY LEVEL") with { Objects = [new(Guid.NewGuid().ToString("N"), exit)] };
            Off(() => mine.SaveAsync(SceneDocumentStore.PathForTarget(root, "authored65")));
            var plains = SceneDocument.New("STAR PLAINS") with
            {
                TargetBinding = new("peacekeepers", 20, 22, 21, sha, 0),
                Portals = [new AuthoredPortal(21, new Vector3f(800, 600, 544), 0), new AuthoredPortal(22, new Vector3f(600, 800, 544), 64)]
            };
            Off(() => plains.SaveAsync(SceneDocumentStore.PathForTarget(root, "peacekeepers")));
            var world = new NewWorldDocument(1, "FAR SHORES", "artisans", [70, 71, 72]);
            Off(() => world.SaveAsync(root));
            Off(() => (SceneDocument.New("FAR SHORES") with { WorldLevelId = 70, Portals = [new AuthoredPortal(71, new Vector3f(700, 700, 544), 0)] })
                .SaveAsync(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(70))));
            foreach (var (id, name) in new[] { (71, "LEVEL ONE"), (72, "LEVEL TWO") })
                Off(() => (SceneDocument.New(name) with { WorldLevelId = id }).SaveAsync(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(id))));
            Directory.CreateDirectory(Path.Combine(root, "editor-cache"));
            foreach (var (key, row) in new[] { ("stonehill", 177), ("darkhollow", 118) })
                File.WriteAllText(Path.Combine(root, "editor-cache", key + "-mobys.json"),
                    $"{{\"mobys\":[{{\"trueIndex\":{row},\"sourceByte36Hex\":\"0x09\",\"sourceByte37Hex\":\"0x00\"}},{{\"trueIndex\":3,\"sourceByte36Hex\":\"0x54\",\"sourceByte37Hex\":\"0x00\"}}]}}");
            File.WriteAllText(Path.Combine(root, "darkhollow-native-edits.json"), "{\"edits\":[{\"trueIndex\":118,\"removed\":true,\"added\":false,\"sourceByte36Hex\":\"0x09\",\"sourceByte37Hex\":\"0x00\"}]}");
            // A link saved for the game's own Peace Keepers before it was rebuilt (Create BIN would stop on it).
            var stale = PortalRouteDocument.Empty.WithDestination(new("peacekeepers", 51), 31, true);
            PortalRouteStore.Save(root, stale, PortalRouteStore.Capture(root));

            window = new MainWindow(); window.Show(); Pump();
            var art = catalog.FindByKey("artisans")!;
            Set(window, "_retailCatalog", catalog); Set(window, "_catalog", catalog); Set(window, "_currentLevel", art);
            Get<List<Moby>>(window, "_currentMobys").Clear();

            // The File menu and the Game Settings card both open it.
            var file = Find<Button>(window, "EditorFileMenu");
            Check(window.GetLogicalDescendants().OfType<Button>().Any(b => b.Name == "LinksOpenLevelLinks" && b.Content?.ToString() == "Level Links…"), "the Links tab has Level Links…");
            Check(window.GetLogicalDescendants().OfType<Button>().Any(b => b.Name == "OpenLevelLinks" && b.Content?.ToString() == "Open Level Links…"), "Portals & Return Home has Open Level Links…");

            // Busy: it says so and opens nothing.
            Set(window, "_createBinBusy", true);
            var refused = window.ShowLevelLinksAsync(); Pump();
            Check(refused.IsCompleted && window.LevelLinksWindowForTesting == null && Status(window).Contains("Level Links opens once"), "a busy editor does not open Level Links");
            Set(window, "_createBinBusy", false);

            var task = window.ShowLevelLinksAsync(11);
            Wait(() => window.LevelLinksWindowForTesting != null, "the Level Links window");
            var dialog = window.LevelLinksWindowForTesting!; Pump();
            Check(dialog.Name == "LevelLinksDialog" && dialog.Title == "Level Links" && dialog.CanResize, "the window is Level Links and can be resized");
            var scroll = Find<ScrollViewer>(dialog, "LevelLinksScroll");
            foreach (string pinned in new[] { "LevelLinksUndo", "LevelLinksClose", "LevelLinksStatus" })
                Check(!Find<Control>(dialog, pinned).GetLogicalAncestors().Contains(scroll), pinned + " stays at the window's foot");
            Check(Find<Border>(dialog, "LevelLinksFocus").Child is TextBlock { Text: { } focus } && focus.Contains("SUNNY GLEN (built in Stone Hill's place)") &&
                focus.Contains("Artisans' Stone Hill doorway"), "opened for a level: says how Spyro gets in and out");

            ComboBox Doorway(int id) => Find<ComboBox>(dialog, "LevelLinksDoorway" + id);
            ComboBox Return(int id) => Find<ComboBox>(dialog, "LevelLinksReturn" + id);
            string Problems() => string.Join("\n", Find<Border>(dialog, "LevelLinksProblems").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            string Picked(ComboBox box) => box.SelectedItem?.ToString() ?? "";

            var stone = Doorway(11);
            var labels = stone.ItemsSource!.Cast<object>().Select(o => o.ToString()!).ToList();
            Check(Picked(stone) == "SUNNY GLEN (built in Stone Hill's place)" && stone.IsEnabled, "a doorway shows the level built in its place by name");
            Check(labels.Contains("MY LEVEL (your new level)") && labels.Contains("Wizard Peak") && labels.Contains("Gnasty's Loot") && !labels.Any(l => l.Contains("homeworld")) && labels.Count == 30,
                "leads-to lists the 29 levels and the new level, no homeworlds");
            Check(!Doorway(21).IsEnabled && Picked(Doorway(21)) == "Dry Canyon" && !dialog.GetLogicalDescendants().OfType<ComboBox>().Any(c => c.Name == "LevelLinksDoorway23"),
                "a rebuilt homeworld shows its own doorways, which can't be sent elsewhere");
            string problems = Problems();
            Check(problems.Contains("Peace Keepers is rebuilt as STAR PLAINS") && problems.Contains("Create BIN stops"), "a link of a rebuilt homeworld is a stop, in plain words");
            Check(problems.Contains("Ice Cavern has no doorway leading to it") && problems.Contains("MY LEVEL has no doorway leading to it yet"), "levels with no way in are named");
            Check(problems.Contains("LEVEL TWO (in your world) has no doorway in FAR SHORES") && !problems.Contains("no doorway back"), "the world's missing doorway is named; no doorway back out is asked for");
            Check(problems.Contains("FAR SHORES has no balloonist, so Spyro could not fly back out of your world: Create BIN stops until it has one. Fix It For Me then stands one"),
                "a world reached by balloon without its balloonist is a stop, with its fix said");
            var worldCard = Find<Border>(dialog, "LevelLinksWorld");
            Check(Find<TextBlock>(dialog, "LevelLinksWorldAccess").Text == "Players reach FAR SHORES by balloon: Artisans' balloonist flies Spyro there whenever he asks, and FAR SHORES' balloonist NILS flies him back to Artisans (and on to STAR PLAINS once he has rescued 10 dragons).",
                "Your world says in one sentence how players get there and back (Peace Keepers by the name it was rebuilt with): " + Find<TextBlock>(dialog, "LevelLinksWorldAccess").Text);
            var artisansCard = Find<Border>(dialog, "LevelLinksHome10").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Check(artisansCard.Contains("Its balloonist flies Spyro to your world FAR SHORES (see Your world below).") && !artisansCard.Any(t => t.Contains("doorway into your world")) &&
                  !worldCard.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("doorway home") == true || t.Text?.Contains("walk in") == true),
                "Artisans' balloonist is named as the way there; no doorway into the world or home is mentioned");
            Check(problems.Contains("SUNNY GLEN has no Return Home vortex (the game's Stone Hill has one)") && problems.Contains("Dark Hollow has lost its Return Home vortex"),
                "missing Return Home vortexes come from the saved level and the object cache");
            Check(!problems.Contains("MY LEVEL has no Return Home vortex") && Find<Control>(dialog, "LevelLinksReturns").GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Return Home vortex: yes."),
                "a vortex placed in the new level is seen");
            Check(Find<Border>(dialog, "LevelLinksWorld").GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("FAR SHORES' doorways lead to: LEVEL ONE, LEVEL TWO (no doorway yet).") == true),
                "the world's doorways are listed");

            // The fix for the stale link: gone from the file, Create BIN no longer stops.
            var fix = dialog.GetLogicalDescendants().OfType<Button>().First(b => b.Content?.ToString() == "Forget Those Links");
            Click(fix);
            Check(!PortalRouteStore.Capture(root).Document.HasEdits && !Problems().Contains("STAR PLAINS"), "Forget Those Links takes the stale link out");

            // Stone Hill's doorway to the new level: in and back through it; SUNNY GLEN now has no way in.
            stone = Doorway(11); stone.SelectedItem = stone.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 65); Pump(3);
            var saved = PortalRouteStore.Capture(root).Document;
            PortalEndpoint stoneDoor = new("artisans", 38), townDoor = new("artisans", 39), toastyDoor = new("artisans", 40);
            Check(saved.DestinationFor(stoneDoor) == 65 && saved.ReturnFor(65) == stoneDoor && saved.Returns.Count == 1, "a doorway sent to a level brings Spyro back out of itself");
            Check(Picked(Doorway(11)) == "MY LEVEL (your new level)" && Picked(Return(65)) == "Automatic: the doorway that leads here (Artisans' Stone Hill doorway)",
                "the new level's Return Home is the doorway that leads there");
            Check(Problems().Contains("SUNNY GLEN has no doorway leading to it") && !Problems().Contains("MY LEVEL has no doorway"), "the map follows the change");
            Check(Find<Button>(dialog, "LevelLinksUndo").IsEnabled && Find<TextBlock>(dialog, "LevelLinksStatus").Text!.Contains("now leads to MY LEVEL"), "the change is said and can be undone");

            // Town Square's doorway to Toasty too: two doorways lead there; Return Home follows the last one sent.
            var town = Doorway(13); town.SelectedItem = town.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 14); Pump(3);
            saved = PortalRouteStore.Capture(root).Document;
            Check(saved.DestinationFor(townDoor) == 14 && saved.ReturnFor(14) == townDoor, "the second doorway to Toasty brings Spyro back out of itself");
            Check(Problems().Contains("Two doorways lead to Toasty") && Problems().Contains("Return Home goes back through the Town Square one"), "two doorways to one level are said");
            // Toasty's Return Home set to automatic: its own doorway, with nothing saved for it.
            var toasty = Return(14); toasty.SelectedIndex = 0; Pump(3);
            saved = PortalRouteStore.Capture(root).Document;
            Check(saved.ReturnFor(14) == toastyDoor && saved.Returns.All(r => r.LevelId != 14), "automatic is the level's own doorway when it still leads there");
            Check(Problems().Contains("Return Home goes back through the Toasty one"), "the window shows the new way back");
            // Undo Last Change: Toasty comes back out of the Town Square doorway again.
            Click(Find<Button>(dialog, "LevelLinksUndo"));
            Check(PortalRouteStore.Capture(root).Document.ReturnFor(14) == townDoor && Problems().Contains("Town Square one"), "Undo Last Change takes the last change back");

            // A level's Return Home set to a doorway that leads elsewhere: said as a problem.
            var wizard = Return(33); wizard.SelectedItem = wizard.ItemsSource!.Cast<object>().First(i => i.ToString()!.StartsWith("Artisans — Dark Hollow doorway")); Pump(3);
            Check(PortalRouteStore.Capture(root).Document.ReturnFor(33) == new PortalEndpoint("artisans", 41) &&
                Problems().Contains("Return Home from Wizard Peak brings Spyro back out of Artisans' Dark Hollow doorway, which leads to Dark Hollow"), "a way back through another level's doorway is said");
            wizard = Return(33); wizard.SelectedIndex = 0; Pump(3);
            Check(PortalRouteStore.Capture(root).Document.Returns.All(r => r.LevelId != 33), "automatic again saves nothing for Wizard Peak");

            // Town Square's doorway back to Town Square: nothing saved for it, and Toasty comes back out of its own doorway.
            town = Doorway(13); town.SelectedItem = town.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 13); Pump(3);
            saved = PortalRouteStore.Capture(root).Document;
            Check(saved.Destinations.All(d => d.Portal != townDoor) && saved.ReturnFor(14) == toastyDoor && saved.Returns.All(r => r.LevelId != 14 && r.LevelId != 13),
                "a doorway put back leads where the game's does, and the level it left comes back out of its own doorway");

            // A change made elsewhere since the window was drawn: refused, the other change kept.
            var outside = PortalRouteStore.Capture(root);
            PortalRouteStore.Save(root, outside.Document.WithDestination(new("artisans", 141), 33, true), outside);
            string kept = PortalRouteStore.Capture(root).Fingerprint;
            var dark = Doorway(12); dark.SelectedItem = dark.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 31); Pump(3);
            Check(PortalRouteStore.Capture(root).Fingerprint == kept && Find<TextBlock>(dialog, "LevelLinksStatus").Text!.StartsWith("Not changed"), "a change after the routes changed elsewhere is refused");
            Check(Picked(Doorway(15)) == "Wizard Peak" && Picked(Doorway(12)) == "Dark Hollow", "the window then shows the routes as they are");

            // Busy while open: nothing can be changed.
            Set(window, "_startBlankBusy", true);
            dark = Doorway(12); dark.SelectedItem = dark.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 31); Pump(3);
            Check(PortalRouteStore.Capture(root).Fingerprint == kept && Picked(Doorway(12)) == "Dark Hollow" && !Doorway(12).IsEnabled,
                "a busy editor changes nothing, and the list goes back to what is saved");
            Set(window, "_startBlankBusy", false);

            // Unreadable routes: the change is refused, then said, nothing offered, the file kept.
            File.WriteAllText(Path.Combine(root, PortalRouteStore.FileName), "broken");
            dark = Doorway(12); dark.SelectedItem = dark.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == 31); Pump(3);
            if (Environment.GetEnvironmentVariable("LEVEL_LINKS_DEBUG") == "1")
                Console.WriteLine("status: " + Find<TextBlock>(dialog, "LevelLinksStatus").Text + "\n texts: " + string.Join(" | ", dialog.GetLogicalDescendants().OfType<TextBlock>().Take(6).Select(t => t.Text)) +
                    "\n combos: " + dialog.GetLogicalDescendants().OfType<ComboBox>().Count());
            Check(dialog.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("The links could not be read") == true) &&
                !dialog.GetLogicalDescendants().OfType<ComboBox>().Any() && File.ReadAllText(Path.Combine(root, PortalRouteStore.FileName)) == "broken",
                "unreadable routes are said and left as they are");

            Click(Find<Button>(dialog, "LevelLinksClose"));
            Wait(() => task.IsCompleted, "the window to close");
            Check(window.LevelLinksWindowForTesting == null && !Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories).Any(), "closed, and no disc image was made");
            Console.WriteLine($"LEVEL_LINKS_UI_OK checks={checks} sourceOpened=false nativeOutputWritten=false fixtures={root}");
        }
        finally
        {
            window?.Close(); Pump(); Environment.CurrentDirectory = previous;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace); Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", release);
        }
    }

    /// <summary>The folder with the game's level list (the repository root: the current folder in the gate, or above the test).</summary>
    private static string FindCatalogFolder()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "spyro-level-catalog.json")) && Directory.Exists(Path.Combine(dir.FullName, "src")))
                    return dir.FullName;
        throw new InvalidOperationException("spyro-level-catalog.json was not found.");
    }

    /// <summary>Runs a save away from the UI thread (its continuation must not wait for the dispatcher this thread holds).</summary>
    private static void Off(Func<Task> save) => Task.Run(save).GetAwaiter().GetResult();
    private static string Status(MainWindow w) => Get<TextBlock>(w, "_statusText").Text ?? "";
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Wait(Func<bool> predicate, string what)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!predicate()) { Pump(); if (DateTime.UtcNow > until) throw new TimeoutException("Level Links: " + what); }
        Pump();
    }
    private static void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(3); }
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing control " + name);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
}
