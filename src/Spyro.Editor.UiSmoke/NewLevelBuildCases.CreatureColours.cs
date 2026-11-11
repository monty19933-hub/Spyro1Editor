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
        SelectTab(window, "Game Settings"); Pump(20);
        Invoke(window, "RefreshCreatureColours");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(120)) { Pump(20); if (window.CreatureKindsReadyForTesting.IsCompleted) { Pump(40); if (window.CreatureKindsReadyForTesting.IsCompleted) break; } }
        var kinds = window.CreatureKindsForTesting;
        string key = window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel!.Key;
        Console.WriteLine($"{key}: {kinds.Count} kinds of enemy: " + string.Join(", ", kinds.Select(k => $"{k.Name} ({k.Key})")));
        Console.WriteLine("note: " + window.CreatureColourNoteForTesting);
        if (kinds.Count == 0) throw new InvalidOperationException("The card lists no enemies for " + key + ".");
        foreach (var kind in kinds) window.SetCreatureColourForTesting(kind.Key, colour);
        Pump(60);
        var saved = CreatureColourSettings.Load(Workspace(window).RootPath);
        if (!saved.TryGetValue(key, out var mine) || mine.Count != kinds.Count || mine.Values.Any(c => c != colour)) throw new InvalidOperationException("The colours were not kept for " + key + ".");
        Snapshot(window, Path.Combine(outDir, "creature-colours.png"));
        File.WriteAllText(Path.Combine(outDir, "creature-colours.json"), JsonSerializer.Serialize(new { level = key, colour = SpyroColourSettings.Format(colour), kinds = kinds.Select(k => new { key = k.Key, name = k.Name }).ToArray() }));
        Console.WriteLine("NEW_LEVEL_CREATURE_COLOURS_OK");
    }
}
