using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static partial class NewLevelBuildCases
{
    /// <summary><c>level-links</c>: File ▾ › Level Links… on the project, through the File menu's own item (or opened for a
    /// level with LEVEL_LINKS_FOCUS=&lt;level id&gt;, as Random Level does). Optional, first: LEVEL_LINKS_NEW_LEVEL=&lt;NAME&gt;
    /// makes File ▾ › New Level's meadow under that name, with a Return Home vortex at the far end of its floor (where Fill
    /// In puts one), and saves it. LEVEL_LINKS_OPEN: the level open behind the window (default artisans).
    /// LEVEL_LINKS_SEND="11:65,13:14": doorways (by the level they lead to in the game) sent to levels, each through its
    /// "leads to" list; LEVEL_LINKS_RETURN="14:auto" or "14:12": a level's Return Home set to automatic or to a doorway.
    /// Pictures of the whole window (top, doorways, Return Home, world) before and after; level-links.json holds the saved
    /// routes and the window's problems.</summary>
    private static void LevelLinksStep(MainWindow window, string outDir)
    {
        string root = System.Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE")!;
        if (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_NEW_LEVEL") is { Length: > 0 } newName)
        {
            var made = (Task)Invoke(window, "NewMainLevelAsync")!;
            Wait(() =>
            {
                if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask) Click(Find<Button>(ask, "ConfirmationConfirm"));
                return made.IsCompleted;
            }, "File > New Level", 600);
            made.GetAwaiter().GetResult();
            WaitIdle(window);
            SetField(window, "_freshSceneDocument", window.FreshSceneForTesting! with { Name = newName });
            var catalog = (Task<(string, UniversalSourceLibrary, IReadOnlyList<UniversalMobyCatalogEntry>)>)Invoke(window, "EnsureUniversalCatalogAsync")!;
            Wait(() => catalog.IsCompleted, "the Add Object library", 900);
            var (_, lib, entries) = catalog.GetAwaiter().GetResult();
            var scene = (SceneDocument)Invoke(window, "CaptureFreshScene")!;
            var plan = LevelFill.Make(scene, 0, [], true, 0);
            var spot = plan.Exit ?? throw new InvalidOperationException("Fill In found no place for Return Home in the meadow.");
            var (entry, variant) = entries.SelectMany(e => e.Variants.Select(v => (e, v))).First(p => p.v.ClassId == LevelCompletionCheck.ReturnHomeClass && p.v.RestrictionReason == null);
            var add = (Task<IReadOnlyList<Moby>>)Invoke(window, "AddUniversalMobyAtAsync", lib, entry, variant, spot.Position, spot.Yaw, null)!;
            Wait(() => add.IsCompleted, "adding Return Home", 300);
            var vortex = add.GetAwaiter().GetResult();
            Console.WriteLine($"new level {newName}: Add Object's \"{entry.Name}\" ({entry.Category} › {entry.SubCategory}; {variant.SourceLevelKey} row {variant.SourceRow}) at {vortex.FirstOrDefault()?.Position} yaw {spot.Yaw}");
            Console.WriteLine("Add Object entries found by searching \"return home\": " + string.Join(" | ", entries.Where(e => e.SearchText.Contains("return home")).Select(e => $"{e.Name} ({e.ClassHex})")));
            var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
            Wait(() => save.IsCompleted, "saving the new level", 300);
            if (!save.Result) throw new InvalidOperationException("The new level was not saved: " + Status(window));
            var savedScene = SceneDocument.Read(SceneDocumentStore.PathForTarget(root, "authored65"));
            var exit = savedScene.Objects.First(o => o.Value.ClassId == LevelCompletionCheck.ReturnHomeClass).Value;
            File.WriteAllText(Path.Combine(outDir, "new-level.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                name = savedScene.Name, spawn = new[] { savedScene.Spawn.X, savedScene.Spawn.Y, savedScene.Spawn.Z }, heading = savedScene.Heading,
                returnHome = new[] { exit.Position.X, exit.Position.Y, exit.Position.Z },
            }));
            Console.WriteLine("saved: " + Status(window));
        }
        Load(window, System.Environment.GetEnvironmentVariable("LEVEL_LINKS_OPEN") is { Length: > 0 } open ? open : "artisans");
        Task task;
        if (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_FOCUS") is { Length: > 0 } focus)
            task = window.ShowLevelLinksAsync(int.Parse(focus));
        else
        {
            // The File menu's own item.
            var item = Find<Button>(window, "LinksOpenLevelLinks");
            item.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, item));
            task = Task.CompletedTask;
        }
        Wait(() => window.LevelLinksWindowForTesting != null, "the Level Links window", 60);
        var dialog = window.LevelLinksWindowForTesting!;
        Pump(30);
        var scroll = Find<ScrollViewer>(dialog, "LevelLinksScroll");
        string Problems() => string.Join("\n", Find<Border>(dialog, "LevelLinksProblems").GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
        void Pictures(string tag)
        {
            Pump(10);
            Console.WriteLine($"window {dialog.Bounds.Width:0}x{dialog.Bounds.Height:0} (max {dialog.MaxHeight:0}), contents {scroll.Extent.Height:0} tall");
            scroll.Offset = default; Pump(10);
            Snapshot(dialog, Path.Combine(outDir, $"{tag}-1-top.png"));
            foreach (var (name, n) in new[] { ("LevelLinksHome10", 2), ("LevelLinksReturns", 3), ("LevelLinksWorld", 4) })
                if (dialog.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name) is { } part)
                {
                    var at = part.TranslatePoint(new Point(0, 0), (Visual)scroll.Content!) ?? new Point(0, 0);
                    scroll.Offset = new Vector(0, Math.Max(0, at.Y - 8)); Pump(10);
                    Snapshot(dialog, Path.Combine(outDir, $"{tag}-{n}-{name[10..].ToLowerInvariant()}.png"));
                }
            scroll.Offset = default; Pump(5);
        }
        Console.WriteLine("problems before:\n" + Problems());
        Pictures("01-before");
        // LEVEL_LINKS_FIX=1: the first problem's button (e.g. Forget Those Links) is pressed.
        if (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_FIX") == "1")
        {
            var fix = dialog.GetLogicalDescendants().OfType<Button>().First(b => b.Name?.StartsWith("LevelLinksFix", StringComparison.Ordinal) == true);
            Console.WriteLine("pressing " + fix.Content);
            Click(fix); Pump(20);
            Console.WriteLine("fix: " + Find<TextBlock>(dialog, "LevelLinksStatus").Text);
        }
        foreach (string send in (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_SEND") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (door, to) = (int.Parse(send.Split(':')[0]), int.Parse(send.Split(':')[1]));
            var box = Find<ComboBox>(dialog, "LevelLinksDoorway" + door);
            box.SelectedItem = box.ItemsSource!.Cast<LevelLinkPlace>().Single(p => p.LevelId == to); Pump(20);
            Console.WriteLine($"send {door} -> {to}: {Find<TextBlock>(dialog, "LevelLinksStatus").Text}");
        }
        foreach (string back in (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_RETURN") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (level, through) = (int.Parse(back.Split(':')[0]), back.Split(':')[1]);
            var box = Find<ComboBox>(dialog, "LevelLinksReturn" + level);
            if (through == "auto") box.SelectedIndex = 0;
            else
            {
                var portal = LevelLinks.OriginalDoorway(int.Parse(through))!;
                box.SelectedItem = box.ItemsSource!.Cast<object>().First(i => i.GetType().GetProperty("Endpoint")!.GetValue(i) is PortalEndpoint e && e == portal);
            }
            Pump(20);
            Console.WriteLine($"return {level} -> {through}: {Find<TextBlock>(dialog, "LevelLinksStatus").Text}");
        }
        var routes = PortalRouteStore.Capture(root).Document;
        string after = Problems();
        if (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_SEND") is { Length: > 0 } || System.Environment.GetEnvironmentVariable("LEVEL_LINKS_RETURN") is { Length: > 0 })
        {
            Console.WriteLine("problems after:\n" + after);
            Pictures("02-after");
        }
        File.WriteAllText(Path.Combine(outDir, "level-links.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            routes.Destinations, routes.Returns, problems = after.Split('\n'),
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("routes: " + string.Join(", ", routes.Destinations.Select(d => $"{d.Portal.HomeworldKey}:{d.Portal.PathTrueIndex}->{d.DestinationLevelId}")) +
            " | returns: " + string.Join(", ", routes.Returns.Select(r => $"{r.LevelId}->{r.Portal.HomeworldKey}:{r.Portal.PathTrueIndex}")));
        // LEVEL_LINKS_OPEN_BUTTON=<homeworld id>: that homeworld's Open button closes the window and opens it in the editor.
        if (System.Environment.GetEnvironmentVariable("LEVEL_LINKS_OPEN_BUTTON") is { Length: > 0 } openHome)
        {
            Click(Find<Button>(dialog, "LevelLinksOpen" + openHome));
            Wait(() => window.LevelLinksWindowForTesting == null && task.IsCompleted, "the window to close", 60);
            Wait(() => window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.LevelId == int.Parse(openHome), "the homeworld to open", 300);
            WaitIdle(window);
            Console.WriteLine($"Open button: now editing {window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.DisplayName} — {Status(window)}");
        }
        else
        {
            Click(Find<Button>(dialog, "LevelLinksClose"));
            Wait(() => window.LevelLinksWindowForTesting == null && task.IsCompleted, "the window to close", 60);
        }
        Console.WriteLine("NEW_LEVEL_LEVEL_LINKS_OK");
    }
}
