using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary><c>world-objects</c>: Objects › Random objects in levels whose place in the game has no Return Home vortex,
/// egg thief or locked chest of its own: the levels of the project's new world and the project's new level (2.6).
/// WORLD_OBJECTS_LEVELS names them, comma separated, each "id[:level type[:enemy level]]" (70-74 the world's levels,
/// 65 the project's new level; default "71"), on a project that has them. For each: the level is opened and Randomize
/// pressed twice (RANDOM_TREASURE gems, default 300). After each press the level must hold exactly one Return Home
/// vortex (none in the world's homeworld, which is left by balloon), one egg thief with a run, and its locked chest
/// with the chest's own key (one linked group, the key far from the chest, floating; the chest moves without its key
/// and is removed with it); the line under the level's treasure and the note must say so, Create BIN must take the
/// arrangement, and the saved level must hold it. world-objects.json (for the emulator scripts) gets where everything
/// is. WORLD_OBJECTS_KEEP=1: one press only (the arrangement the emulator is to be shown).</summary>
internal static partial class NewLevelBuildCases
{
    private static void WorldObjectsStep(MainWindow window, string outDir)
    {
        string root = Workspace(window).RootPath;
        int treasure = int.Parse(Environment.GetEnvironmentVariable("RANDOM_TREASURE") ?? "300");
        bool once = Environment.GetEnvironmentVariable("WORLD_OBJECTS_KEEP") == "1";
        long seed0 = long.Parse(Environment.GetEnvironmentVariable("WORLD_OBJECTS_SEED") ?? "2601");
        List<object> report = [];
        int checks = 0;
        void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException(what + " | status: " + Status(window)); checks++; }
        List<Moby> Live() => [.. Get<List<Moby>>(window, "_currentMobys").Where(m => !m.IsRemoved)];
        static double[] At(Vector3f p) => [Math.Round(p.X, 1), Math.Round(p.Y, 1), Math.Round(p.Z, 1)];
        static double Far(Vector3f a, Vector3f b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        foreach (string spec in (Environment.GetEnvironmentVariable("WORLD_OBJECTS_LEVELS") ?? "71").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = spec.Split(':');
            int id = int.Parse(parts[0]);
            string type = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : "stonehill", enemies = parts.Length > 2 ? parts[2] : "";
            var open = id == 65 ? (Task)Invoke(window, "OpenMainFreshLevelAsync")! : (Task)Invoke(window, "OpenWorldLevelAsync", id)!;
            Wait(() => open.IsCompleted, $"open level {id}", 300);
            WaitIdle(window);
            var scene = window.FreshSceneForTesting ?? throw new InvalidOperationException($"Level {id} did not open: " + Status(window));
            Check(id == 65 ? scene.TargetBinding == null && scene.WorldLevelId == null : scene.WorldLevelId == id, $"level {id} is open");
            bool home = id == NewWorldDocument.Homeworld;
            Console.WriteLine($"== {id} {scene.Name}: {scene.Surfaces.Count} faces, {Live().Count} objects, level type {type}{(enemies.Length > 0 ? ", enemies of " + enemies : "")}");
            string? firstShape = null;
            for (int press = 1; press <= (once ? 1 : 2); press++)
            {
                var choice = new MainWindow.RandomObjectsChoice(type, enemies, 0, MainWindow.RandomObjectsEnemies(LevelDifficulty.Medium, treasure), treasure, 0, Seed: seed0 + 10 * id + press);
                var task = (Task)Invoke(window, "RandomizeObjectsAsync", choice)!;
                Wait(() => task.IsCompleted, "Random objects", 1800);
                task.GetAwaiter().GetResult();
                string status = Status(window);
                var placed = window.LastRandomObjects;
                Console.WriteLine($"press {press}: {status}");
                Console.WriteLine("    " + string.Join(", ", placed.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Value} {p.Key}")));
                Check(window.LastRandomObjectsRefusal == null, "Create BIN would refuse what Random objects placed: " + window.LastRandomObjectsRefusal);
                var live = Live();
                // The way out.
                var exits = live.Where(m => m.ClassId == LevelCompletionCheck.ReturnHomeClass).ToList();
                var pilots = live.Where(m => m.SourceByte37 == 0 && m.SourceByte36 is >= UniversalBalloonistRows.BalloonistClass and <= UniversalBalloonistRows.BalloonistClass + 5).ToList();
                if (home) Check(exits.Count == 0 && pilots.Count == 1, $"the world's homeworld is left by its balloonist ({pilots.Count}) and has no vortex ({exits.Count})");
                else Check(exits.Count == 1 && placed.GetValueOrDefault("Return Home vortex") == 1, $"one Return Home vortex ({exits.Count} in the level, {placed.GetValueOrDefault("Return Home vortex")} placed)");
                // One egg thief, with a run.
                var thieves = live.Where(m => m.ClassId == LevelCompletionCheck.EggThiefClass).ToList();
                Check(thieves.Count == 1 && placed.GetValueOrDefault("egg thief") == 1, $"one egg thief ({thieves.Count} in the level, {placed.GetValueOrDefault("egg thief")} placed)");
                var run = thieves[0].UniversalPathNodes;
                Check(run is { Count: > 1 }, "the egg thief has a run fitted to this level");
                Check(!status.Contains("egg thieves belong", StringComparison.OrdinalIgnoreCase), "the note no longer says egg thieves cannot be here");
                // The locked chest and its own key.
                var chests = live.Where(m => m.IsAdded && m.ClassId == UniversalLockedChestRows.ChestClass).ToList();
                var keys = live.Where(m => m.IsAdded && m.ClassId == UniversalLockedChestRows.KeyClass).ToList();
                Check(chests.Count == keys.Count && chests.Count <= 1, $"a locked chest comes with one key ({chests.Count} chests, {keys.Count} keys)");
                Moby? chest = chests.FirstOrDefault(), key = keys.FirstOrDefault();
                if (chest != null && key != null)
                {
                    Check(placed.GetValueOrDefault("locked chest") == 1 && placed.GetValueOrDefault("key") == 1, "the note counts the locked chest and its key");
                    Check(!string.IsNullOrWhiteSpace(chest.UniversalLinkGroupId) && chest.UniversalLinkGroupId == key.UniversalLinkGroupId, "chest and key are one linked group");
                    Check(string.Equals(chest.CrossLevelSourceLevelKey, key.CrossLevelSourceLevelKey, StringComparison.OrdinalIgnoreCase), "the key is the chest's own level's key");
                    Check(Far(chest.Position, key.Position) >= 400, $"the key lies away from its chest ({Far(chest.Position, key.Position):0} units)");
                    object?[] floor = [key.Position.X, key.Position.Y, key.Position.Z, 0f, -1, false];
                    bool found = (bool)typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, floor)!;
                    float lift = found ? key.Position.Z - (float)floor[3]! : float.NaN;
                    Check(found && lift is > 14 and < 26, $"the key floats above the floor as the game's keys do ({lift:0.0})");
                    var moves = ((IEnumerable<Moby>)Invoke(window, "GetLinkedMoveMobys", chest)!).ToList();
                    var keyMoves = ((IEnumerable<Moby>)Invoke(window, "GetLinkedMoveMobys", key)!).ToList();
                    Check(!moves.Contains(key) && !keyMoves.Contains(chest), "the chest and its key are moved each on its own");
                    var group = ((IEnumerable<Moby>)Invoke(window, "GetUniversalLinkGroupMembers", chest)!).ToList();
                    Check(group.Contains(key) && group.Contains(chest), "the chest and its key are removed (and copied) together");
                    // Where Add Object first sets a Key Chest's key down: a few steps from the chest, floating over floor.
                    var beside = (Vector3f)Invoke(window, "LockedChestKeyPlace", chest.Position)!;
                    object?[] under = [beside.X, beside.Y, beside.Z, 0f, -1, false];
                    bool ground = (bool)typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, under)!;
                    Check(Far(beside, chest.Position) is > 90 and < 260 && ground && beside.Z - (float)under[3]! is > 14 and < 26, $"a Key Chest added by hand gets its key beside it ({Far(beside, chest.Position):0} units off, {beside.Z - (float)under[3]!:0.0} above the floor)");
                }
                else Console.WriteLine("    (no locked chest this time: " + (status.Contains("locked chest") ? "see the note" : "the level had no place or rows for one") + ")");
                // The line under the level's treasure.
                Invoke(window, "RefreshCompletionLine");
                string line = Get<TextBlock>(window, "_completionText").Text ?? "";
                Console.WriteLine("    completion line: " + line.ReplaceLineEndings(" / "));
                if (!home) Check(line.Contains("Return Home vortex: yes") && line.Contains("egg thief 1"), "the completion line counts the vortex and the egg thief: " + line);
                string shape = string.Join(";", live.OrderBy(m => m.Position.X).ThenBy(m => m.Position.Y).Select(m => $"{m.Position.X:0},{m.Position.Y:0}"));
                if (press == 1) firstShape = shape; else Check(shape != firstShape, "Randomize makes another arrangement");
                Snapshot(window, Path.Combine(outDir, $"world-objects-{id}-{press}.png"));
                if (press == (once ? 1 : 2))
                {
                    var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
                    Wait(() => save.IsCompleted, "Save Level", 120);
                    Check(save.GetAwaiter().GetResult(), "the level saves");
                    string keyName = window.FreshSceneForTesting?.TargetKey ?? "";
                    var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(root, keyName));
                    var objects = saved.Objects.Select(o => o.Value).Where(m => !m.IsRemoved).ToList();
                    Check(objects.Count(m => m.ClassId == LevelCompletionCheck.EggThiefClass) == 1 && objects.Count(m => m.ClassId == LevelCompletionCheck.ReturnHomeClass) == (home ? 0 : 1) &&
                        objects.Count(m => m.ClassId == UniversalLockedChestRows.KeyClass) == keys.Count, "the saved level holds the vortex, the egg thief and the key");
                    Console.WriteLine($"    saved {keyName}: {objects.Count} objects");
                    var spawn = saved.Spawn;
                    report.Add(new
                    {
                        id, name = saved.Name, type, spawn = At(spawn), heading = saved.Heading, treasure = objects.Sum(m => m.TreasureValue),
                        vortex = exits.Select(m => At(m.Position)).FirstOrDefault(), balloonist = pilots.Select(m => At(m.Position)).FirstOrDefault(),
                        thief = At(thieves[0].Position), thiefFrom = thieves[0].CrossLevelSourceLevelKey,
                        thiefRun = run!.Select(n => new[] { Math.Round(n[0] / 16.0, 1), Math.Round(n[1] / 16.0, 1), Math.Round(n[2] / 16.0, 1) }).ToArray(),
                        chest = chest == null ? null : At(chest.Position), key = key == null ? null : At(key.Position),
                        keyFromChest = chest == null || key == null ? 0 : Math.Round(Far(chest.Position, key.Position)),
                        chestGives = chest?.TreasureValue ?? 0,
                    });
                }
            }
        }
        File.WriteAllText(Path.Combine(outDir, "world-objects.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"NEW_LEVEL_WORLD_OBJECTS_OK checks={checks}");
    }
}
