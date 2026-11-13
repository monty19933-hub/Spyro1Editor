using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Persistence;

/// <summary><c>creature-colours</c>: Game Settings › Creatures' colours in a level of the project. CREATURE_LEVEL (a
/// level key or "world:70") is opened; the card must list the enemy kinds the level has; each is given CREATURE_COLOUR
/// (#C82424). Writes creature-colours.json (the level, its kinds and the colour) for the check of the created game
/// (<c>--creature-colours-check</c>).</summary>
internal static partial class NewLevelBuildCases
{
    private static void CreatureColoursStep(MainWindow window, string outDir)
    {
        string level = Environment.GetEnvironmentVariable("CREATURE_LEVEL") ?? "world:70";
        if (!SpyroColourSettings.TryParse(Environment.GetEnvironmentVariable("CREATURE_COLOUR") ?? "#C82424", out var colour)) throw new ArgumentException("CREATURE_COLOUR is #RRGGBB.");
        if (level.StartsWith("world:", StringComparison.Ordinal)) { var open = (Task)Invoke(window, "OpenWorldLevelAsync", int.Parse(level[6..]))!; Wait(() => open.IsCompleted, "open the world level", 120); WaitIdle(window); }
        else Load(window, level);
        SelectTab(window, "Look"); Pump(20);
        Invoke(window, "RefreshCreatureColours");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(120)) { Pump(20); if (window.CreatureKindsReadyForTesting.IsCompleted) { Pump(40); if (window.CreatureKindsReadyForTesting.IsCompleted) break; } }
        var kinds = window.CreatureKindsForTesting;
        string key = window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel!.Key;
        Console.WriteLine($"{key}: {kinds.Count} kinds: " + string.Join(", ", kinds.Select(k => $"{k.Name} ({k.Key})")));
        if (kinds.Count == 0) throw new InvalidOperationException("The card lists nothing for " + key + ".");
        Snapshot(window, Path.Combine(outDir, "colours-tab-before.png"));
        // CREATURE_GAME=<seed>: Randomize Every Level (the whole game), and nothing else.
        if (int.TryParse(Environment.GetEnvironmentVariable("CREATURE_GAME"), out int gameSeed))
        {
            var every = window.RandomizeEveryLevelForTestingAsync(gameSeed);
            Wait(() => every.IsCompleted, "colours for every level", 300); Pump(60);
            var all = CreatureColourSettings.Load(Workspace(window).RootPath);
            Console.WriteLine($"every level: {all.Count} levels, {all.Values.Sum(l => l.Count)} kinds; status: {Status(window)}");
            Console.WriteLine("stonehill: " + (all.TryGetValue("stonehill", out var sh) ? string.Join(", ", sh.Select(c => $"{c.Key} #{c.Value.R:X2}{c.Value.G:X2}{c.Value.B:X2}")) : "none"));
            if (all.Count < 25) throw new InvalidOperationException("Randomize Every Level reached only " + all.Count + " levels.");
            Snapshot(window, Path.Combine(outDir, "colours-tab.png"));
            Console.WriteLine("NEW_LEVEL_CREATURE_COLOURS_OK"); return;
        }
        // CREATURE_RANDOM=<seed>: Randomize All instead of one colour for everything.
        if (int.TryParse(Environment.GetEnvironmentVariable("CREATURE_RANDOM"), out int seed))
        {
            window.RandomizeCreatureColoursForTesting(seed); Pump(60);
            Console.WriteLine("randomized: " + string.Join(", ", window.CreatureColoursForTesting.Select(c => $"{c.Key} #{c.Value.R:X2}{c.Value.G:X2}{c.Value.B:X2}")));
        }
        else foreach (var kind in kinds) window.SetCreatureColourForTesting(kind.Key, colour);
        // DRAGON_COLOUR=#RRGGBB: every dragon statue of the level in that crystal colour, then the level is saved.
        if (SpyroColourSettings.TryParse(Environment.GetEnvironmentVariable("DRAGON_COLOUR") ?? "", out var crystal))
        {
            window.SetDragonStatueColoursForTesting(crystal); Pump(30);
            Console.WriteLine("dragons: " + Status(window));
            var save = (Task)Invoke(window, "SaveFromToolbarAsync")!;
            Wait(() => save.IsCompleted, "save the level", 120); Pump(30);
            Console.WriteLine("saved: " + Status(window));
        }
        Pump(120);
        Snapshot(window, Path.Combine(outDir, "colours-tab.png"));
        if (Environment.GetEnvironmentVariable("CREATURE_RANDOM") != null) { Console.WriteLine("NEW_LEVEL_CREATURE_COLOURS_OK"); return; }
        Console.WriteLine("note: " + window.CreatureColourNoteForTesting);
        Pump(60);
        var saved = CreatureColourSettings.Load(Workspace(window).RootPath);
        if (!saved.TryGetValue(key, out var mine) || mine.Count != kinds.Count || mine.Values.Any(c => c != colour)) throw new InvalidOperationException("The colours were not kept for " + key + ".");
        Snapshot(window, Path.Combine(outDir, "creature-colours.png"));
        File.WriteAllText(Path.Combine(outDir, "creature-colours.json"), JsonSerializer.Serialize(new { level = key, colour = SpyroColourSettings.Format(colour), kinds = kinds.Select(k => new { key = k.Key, name = k.Name }).ToArray() }));
        Console.WriteLine("NEW_LEVEL_CREATURE_COLOURS_OK");
    }
}
