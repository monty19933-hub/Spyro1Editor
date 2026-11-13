using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

/// <summary>How players reach the project's own world and come back (by balloon, like the game's own worlds), on a
/// project with a world. <c>world-access</c>: the editor's words for it, read and pictured without changing anything —
/// the world's homeworld's Doorways card, Level Links opened on the world's homeworld (its "Your world" part, the
/// problems, Artisans' doorways), the World's Balloon Route window (cancelled) and the World Entrance card of the
/// homeworld the world is entered from. <c>world-check</c>: the project and the game Create BIN made from it — the world's
/// name, the homeworld's doorways and balloonist, no doorway into the world in any of the game's homeworlds and none back
/// out of the world's homeworld — and homeworld.json for the emulator scripts.
/// WORLD_EXPECT_NAME: the world's name expected; WORLD_EXPECT_BALLOON_ONLY=1: reached by balloon only.</summary>
internal static partial class NewLevelBuildCases
{
    private static string AllTexts(Control c) =>
        string.Join("\n", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

    private static void WorldAccessStep(MainWindow window, string outDir)
    {
        string root = Workspace(window).RootPath;
        var world = NewWorldDocument.Read(root) ?? throw new InvalidOperationException("The project has no world.");
        bool balloonOnly = Environment.GetEnvironmentVariable("WORLD_EXPECT_BALLOON_ONLY") == "1";
        Console.WriteLine($"new-world.json: {world.Name}, entry {world.EntryHomeworldKey}, doorway {world.Doorway}, balloon-only {world.BalloonOnly}, place {world.Balloon}");

        // The world's homeworld and its Doorways card.
        var open = (Task)Invoke(window, "OpenWorldLevelAsync", NewWorldDocument.Homeworld)!;
        Wait(() => open.IsCompleted, "open the world's homeworld", 300);
        WaitIdle(window);
        SelectTab(window, "Terrain"); Pump(30);
        Find<Control>(window, "TerrainDoorwaysCard").BringIntoView(); Pump(20);
        string doorways = Find<TextBlock>(window, "DoorwaysNote").Text ?? "";
        Console.WriteLine("doorways card: " + doorways);
        Snapshot(window, Path.Combine(outDir, "01-world-homeworld-doorways-card.png"));
        if (balloonOnly && !doorways.Contains("by balloon") && !doorways.Contains("balloonist flies Spyro back")) throw new InvalidOperationException("The Doorways card does not say the world is reached by balloon.");

        // Level Links opened on the world's homeworld.
        var links = window.ShowLevelLinksAsync(NewWorldDocument.Homeworld);
        Wait(() => window.LevelLinksWindowForTesting != null, "the Level Links window", 60);
        var dialog = window.LevelLinksWindowForTesting!;
        Pump(30);
        var scroll = Find<ScrollViewer>(dialog, "LevelLinksScroll");
        string focus = AllTexts(Find<Border>(dialog, "LevelLinksFocus")), problems = AllTexts(Find<Border>(dialog, "LevelLinksProblems")),
            yours = AllTexts(Find<Border>(dialog, "LevelLinksWorld")), artisans = AllTexts(Find<Border>(dialog, "LevelLinksHome10"));
        Console.WriteLine("level links, focus: " + focus);
        Console.WriteLine("level links, problems:\n" + problems);
        Console.WriteLine("level links, Artisans: " + string.Join(" / ", artisans.Split('\n').Where(t => !t.EndsWith("→", StringComparison.Ordinal) && !t.StartsWith("Return Home vortex", StringComparison.Ordinal))));
        Console.WriteLine("level links, your world:\n" + yours);
        scroll.Offset = default; Pump(10);
        Snapshot(dialog, Path.Combine(outDir, "02-level-links-top.png"));
        foreach (var (name, n) in new[] { ("LevelLinksHome10", 3), ("LevelLinksWorld", 4) })
        {
            var part = Find<Control>(dialog, name);
            var at = part.TranslatePoint(new Point(0, 0), (Visual)scroll.Content!) ?? new Point(0, 0);
            scroll.Offset = new Vector(0, Math.Max(0, at.Y - 8)); Pump(10);
            Snapshot(dialog, Path.Combine(outDir, $"0{n}-level-links-{name[10..].ToLowerInvariant()}.png"));
        }
        string access = Find<TextBlock>(dialog, "LevelLinksWorldAccess").Text ?? "";
        if (Environment.GetEnvironmentVariable("WORLD_EXPECT_NAME") is { Length: > 0 } expectName && !yours.StartsWith("Your world: " + expectName, StringComparison.Ordinal))
            throw new InvalidOperationException("Level Links does not call the world " + expectName);
        if (balloonOnly && (!access.Contains(" by balloon") || problems.Contains("has no doorway back") || yours.Contains("doorway home leads")))
            throw new InvalidOperationException("Level Links does not say the world is reached by balloon only.");
        Click(Find<Button>(dialog, "LevelLinksClose"));
        Wait(() => window.LevelLinksWindowForTesting == null && links.IsCompleted, "the Level Links window to close", 60);

        // The World's Balloon Route window: read, pictured, cancelled.
        var route = (Task)Invoke(window, "BalloonRouteAsync")!;
        Window? routeDialog = null;
        Wait(() => (routeDialog = window.OwnedWindows.FirstOrDefault(w => w.Name == "BalloonRouteDialog")) != null || route.IsCompleted, "the World's Balloon Route window", 60);
        if (routeDialog == null) throw new InvalidOperationException("The World's Balloon Route window did not open: " + Status(window));
        Pump(20);
        Console.WriteLine("balloon route window:\n" + AllTexts(routeDialog));
        Snapshot(routeDialog, Path.Combine(outDir, "05-balloon-route.png"));
        Click(Find<Button>(routeDialog, "BalloonRouteCancel"));
        Wait(() => route.IsCompleted, "cancel", 60);
        if (NewWorldDocument.Read(root)!.Balloon != world.NamedAfter(window.FreshSceneForTesting?.Name).Balloon) throw new InvalidOperationException("Cancel changed the balloon route.");

        // The World Entrance card of the homeworld the world is entered from.
        Load(window, world.EntryHomeworldKey);
        SelectTab(window, "Terrain"); Pump(20);
        Invoke(window, "RefreshWorldEntranceCard"); Pump(10);
        var card = Find<Control>(window, "TerrainWorldEntranceCard");
        card.BringIntoView(); Pump(20);
        string note = Find<TextBlock>(window, "WorldEntranceNote").Text ?? "";
        string button = Find<Button>(window, "WorldEntrancePlace").Content?.ToString() ?? "";
        Console.WriteLine($"world entrance card: {note} [{button}]");
        Snapshot(window, Path.Combine(outDir, "06-world-entrance-card.png"));
        if (balloonOnly && (!note.Contains("No doorway is needed") || button is not ("Add a Doorway As Well…" or "Build It As Well")))
            throw new InvalidOperationException("The World Entrance card does not say no doorway is needed.");
        Console.WriteLine("NEW_LEVEL_WORLD_ACCESS_OK");
    }

    private static void WorldCheckStep(MainWindow window, string outDir)
    {
        string root = Workspace(window).RootPath;
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        var world = NewWorldDocument.Read(root) ?? throw new InvalidOperationException("The project has no world.");
        var home = SceneDocument.Read(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld)));
        bool balloonOnly = Environment.GetEnvironmentVariable("WORLD_EXPECT_BALLOON_ONLY") == "1";
        List<string> wrong = [];
        Console.WriteLine($"new-world.json: {world.Name}, entry {world.EntryHomeworldKey}, doorway {world.Doorway}, balloon-only {world.BalloonOnly}, place {world.Balloon}");
        Console.WriteLine($"world-entrance.json: {(File.Exists(WorldEntranceDocument.PathFor(root)) ? File.ReadAllText(WorldEntranceDocument.PathFor(root)).ReplaceLineEndings(" ") : "none")}");
        var balloonists = home.Objects.Where(o => WorldAccess.IsBalloonist(o.Value)).Select(o => o.Value).ToList();
        Console.WriteLine($"homeworld {home.Name}: start {home.Spawn} heading {home.Heading}; doorways to {string.Join(", ", (home.Portals ?? []).Select(p => p.DestinationLevelId))}; " +
            $"balloonist {string.Join("; ", balloonists.Select(b => $"{b.Label} at {b.Position} yaw {b.YawByte}"))}");
        var fixIt = Path.Combine(root, "backups", "fix-it");
        if (Directory.Exists(fixIt)) Console.WriteLine("backups/fix-it: " + string.Join(", ", Directory.GetFiles(fixIt).Select(Path.GetFileName)));
        if (Environment.GetEnvironmentVariable("WORLD_EXPECT_NAME") is { Length: > 0 } expectName && (world.Name != expectName || home.Name != expectName)) wrong.Add($"the world is called {world.Name} (homeworld {home.Name}), not {expectName}");
        if (balloonOnly)
        {
            if (!world.BalloonOnly) wrong.Add("the world is not balloon-only");
            if (WorldAccess.WaysBack(home).Count > 0) wrong.Add("the world's homeworld still has a doorway back out");
            if (balloonists.Count == 0) wrong.Add("the world's homeworld has no balloonist");
        }

        // The game Create BIN made.
        string bin = Path.Combine(root, "output", "Spyro Editor - Authored Level.bin");
        if (!File.Exists(bin)) throw new InvalidOperationException("No game was made: " + bin);
        foreach (string key in NewWorldDocument.HomeworldKeys)
        {
            var level = catalog.FindByKey(key)!;
            if (File.Exists(SceneDocumentStore.PathForTarget(root, key))) { Console.WriteLine($"game {level.DisplayName}: rebuilt in the project"); continue; }
            var to = NativeWorldEntrance.DoorwayDestinations(NativeLevelWindowDisc.ReadEntry(bin, level.SourceWadEntry));
            Console.WriteLine($"game {level.DisplayName}: doorways to {string.Join(", ", to)}");
            if (balloonOnly && to.Contains(NewWorldDocument.Homeworld)) wrong.Add($"{level.DisplayName} has a doorway into the world");
        }
        try
        {
            var to = NativeWorldEntrance.DoorwayDestinations(NativeLevelWindowDisc.ReadEntry(bin, NativeNewWorldExport.FirstEntry + 1));
            Console.WriteLine($"game {home.Name}: doorways to {string.Join(", ", to)}");
            if (balloonOnly && to.Any(d => d is >= 10 and <= 60)) wrong.Add($"{home.Name} has a doorway back out in the game");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { Console.WriteLine($"game {home.Name}: doorway table not read ({e.Message})"); }
        File.WriteAllText(Path.Combine(outDir, "homeworld.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            name = home.Name, spawn = new[] { home.Spawn.X, home.Spawn.Y, home.Spawn.Z }, heading = home.Heading,
            doorways = (home.Portals ?? []).Select(x => new { to = x.DestinationLevelId, at = new[] { x.Position.X, x.Position.Y, x.Position.Z }, heading = x.Heading, shape = x.Shape }).ToArray(),
            objects = home.Objects.Where(o => !o.Value.IsRemoved).Select(o => new { label = o.Value.Label, at = new[] { o.Value.Position.X, o.Value.Position.Y, o.Value.Position.Z } }).ToArray(),
            entrance = WorldEntranceDocument.Read(root) is { } placed ? new[] { placed.Position.X, placed.Position.Y, placed.Position.Z, placed.Heading } : null,
        }));
        if (wrong.Count > 0) throw new InvalidOperationException("World check: " + string.Join("; ", wrong));
        Console.WriteLine("NEW_LEVEL_WORLD_CHECK_OK");
    }
}
