using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary><c>reward-chests</c>: the teal and rainbow spring chests of the Add Object list
/// (<see cref="UniversalHighValueRewards"/>). REWARD_CHEST_LEVEL (a level key, default stonehill, or "created" for the
/// created level) is opened. The list must offer "Spring chest (Teal reward)" and "Spring chest (Rainbow reward)" under
/// their stable ids, and each must pass its fit check for this level in the browser. One of each, then an ordinary spring
/// chest, is added in a row from REWARD_CHEST_AT ("x,y,z" in editor units; default a little in front of Spyro's start),
/// REWARD_CHEST_STEP apart along x (default 94). The objects must give 50 and 100, the level's treasure must follow, and
/// the level must save and come back the same when opened again. Writes reward-chests.json (the level, its id, where
/// each chest stands in game units and what it gives) for the emulator script chestrun.py.</summary>
internal static partial class NewLevelBuildCases
{
    private static void RewardChestsStep(MainWindow window, string outDir)
    {
        string level = Environment.GetEnvironmentVariable("REWARD_CHEST_LEVEL") ?? "stonehill";
        bool created = level == "created";
        void Open()
        {
            if (created) { var open = (Task)Invoke(window, "OpenMainFreshLevelAsync")!; Wait(() => open.IsCompleted, "open the created level", 120); WaitIdle(window); }
            else Load(window, level);
        }
        Open();
        SelectTab(window, "Objects");
        var (browser, shown) = OpenBrowserReporting(window);
        var chests = UniversalHighValueRewards.SpringChests.Select(c => (c.Id, c.Name, c.Reward,
            Entry: shown.SingleOrDefault(e => e.Id == c.Id) ?? throw new InvalidOperationException($"The Add Object list has no {c.Name} ({c.Id})."))).ToList();
        foreach (var c in chests)
        {
            if (c.Entry.Name != c.Name || c.Entry.Category != "Interactive" || c.Entry.SubCategory != "Chests & Containers")
                throw new InvalidOperationException($"{c.Id} is listed as {c.Entry.Name} under {c.Entry.Category}/{c.Entry.SubCategory}.");
            if (c.Entry.Variants.Count == 0 || c.Entry.Variants.Any(v => v.RewardByte != c.Reward || !UniversalHighValueRewards.Supports(v.ClassId)))
                throw new InvalidOperationException($"{c.Name}'s source versions are not spring chests giving 0x{c.Reward:X2}.");
        }
        Console.WriteLine($"listed: {string.Join(", ", chests.Select(c => $"{c.Name} [{c.Id}] {c.Entry.Variants.Count} versions from {c.Entry.SourceLevels.Count} levels"))}");
        var plain = shown.First(e => e.ClassId == 0x149 && e.Id == null);
        // The browser's own fit check, as the user sees it.
        ListBox list = Find<ListBox>(browser, "UniversalMobyList");
        TextBlock fit = Find<TextBlock>(browser, "UniversalMobyFitText");
        ComboBox versions = Find<ComboBox>(browser, "UniversalMobyVariant");
        List<(string Name, byte? Reward, UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)> toAdd = [];
        foreach (var (name, reward, entry) in chests.Select(c => (c.Name, (byte?)c.Reward, c.Entry)).Append((plain.Name, null, plain)))
        {
            list.SelectedItem = entry; Pump(5);
            if (!ReferenceEquals(list.SelectedItem, entry)) throw new InvalidOperationException($"{name} is not in the list shown.");
            Wait(() => (fit.Text ?? "").StartsWith("✓", StringComparison.Ordinal) || (fit.Text ?? "").Contains("won't fit", StringComparison.OrdinalIgnoreCase)
                || (fit.Text ?? "").Contains("can't", StringComparison.OrdinalIgnoreCase) || (fit.Text ?? "").Contains("cannot", StringComparison.OrdinalIgnoreCase), "fit check of " + name, 600);
            var variant = versions.SelectedItem as UniversalMobyVariant ?? entry.Default;
            Console.WriteLine($"  {name}: {fit.Text} (version {variant.SourceLevelKey} row {variant.SourceRow})");
            if (!(fit.Text ?? "").StartsWith("✓", StringComparison.Ordinal)) throw new InvalidOperationException($"{name} does not fit: {fit.Text}");
            if (reward != null && variant.RewardByte != reward) throw new InvalidOperationException($"{name}'s chosen version does not give 0x{reward:X2}.");
            if (reward != null) Snapshot(browser, Path.Combine(outDir, $"reward-chests-browser-{(reward == UniversalHighValueRewards.TealReward ? "teal" : "rainbow")}.png"));
            toAdd.Add((name, reward, entry, variant));
        }
        browser.Close(); Pump(10);

        var lib = Library(window);
        int Treasure() => Mobys(window).Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
        int before = Treasure();
        Vector3f start;
        if (Environment.GetEnvironmentVariable("REWARD_CHEST_AT") is { Length: > 0 } at)
        { float[] v = at.Split(',').Select(float.Parse).ToArray(); start = new(v[0], v[1], v[2]); }
        else if (created) { var spawn = window.FreshSceneForTesting!.Spawn; start = new(spawn.X - 94, spawn.Y + 110, spawn.Z); }
        else start = new(8182, 7658, 1310);
        float step = float.Parse(Environment.GetEnvironmentVariable("REWARD_CHEST_STEP") ?? "94");
        List<(Moby Moby, byte? Reward, int Gives)> placed = [];
        for (int n = 0; n < toAdd.Count; n++)
        {
            var (name, reward, entry, variant) = toAdd[n];
            var add = (Task<IReadOnlyList<Moby>>)Invoke(window, "AddUniversalMobyAtAsync", lib, entry, variant, start with { X = start.X + step * n }, 64, null)!;
            Wait(() => add.IsCompleted, "add " + name, 300);
            if (add.IsFaulted) throw add.Exception!;
            if (add.Result.Count == 0) throw new InvalidOperationException($"{name} was not added: {Status(window)}");
            Moby m = add.Result[0];
            int gives = reward is byte r ? UniversalHighValueRewards.ValueFor(r) : m.TreasureValue;
            Console.WriteLine($"  added {m.DisplayLabel} at ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}): gives 0x{m.Flag4B:X2}, treasure {m.TreasureValue}; {Status(window)}");
            if (reward is byte want && (m.Label != name || m.Flag4B != want || m.TreasureValue != gives || !m.GivesHighValueGem || m.RewardGem != GemValue.Unknown))
                throw new InvalidOperationException($"{name} was added as {m.Label} giving 0x{m.Flag4B:X2} worth {m.TreasureValue}.");
            placed.Add((m, reward, gives));
        }
        int after = Treasure();
        Console.WriteLine($"treasure {before} -> {after}");
        if (after - before != placed.Sum(p => p.Gives)) throw new InvalidOperationException("The level's treasure did not count the chests.");
        // Neither the reward card nor the Edit window's five-gem choice may take a teal or rainbow chest's gem away.
        foreach (var p in placed.Where(p => p.Reward != null))
            if ((bool)Invoke(window, "CanChooseReward", p.Moby)! || (bool)Invoke(window, "HasEditableGemValue", p.Moby)!)
                throw new InvalidOperationException($"{p.Moby.Label} offers the five-gem reward choice.");
        if (created)
        {
            var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
            Wait(() => save.IsCompleted, "Save Level", 120);
            if (!save.GetAwaiter().GetResult()) throw new InvalidOperationException("The level did not save: " + Status(window));
        }
        else
        {
            Save(window);
            // Chests from the universal library are not tagged with the older imported-bundle profiles (Stone Hill's is
            // "blocked": Create BIN's safety check refused every spring chest there when no level was built).
            string saved = File.ReadAllText(Path.Combine(Workspace(window).RootPath, $"{level}-native-edits.json"));
            if (System.Text.RegularExpressions.Regex.IsMatch(saved, "\"specialChestBundleId\":\\s*\"") || placed.Any(p => !string.IsNullOrEmpty(p.Moby.SpecialChestBundleId)))
                throw new InvalidOperationException("A spring chest from the object library was saved with a special-chest bundle tag.");
        }
        Snapshot(window, Path.Combine(outDir, "reward-chests-level.png"));
        // Opened again from what was saved.
        Load(window, level == "artisans" ? "stonehill" : "artisans");
        Open();
        foreach (var p in placed)
        {
            var again = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && MathF.Abs(m.Position.X - p.Moby.Position.X) + MathF.Abs(m.Position.Y - p.Moby.Position.Y) + MathF.Abs(m.Position.Z - p.Moby.Position.Z) < 1).ToList();
            if (again.Count != 1 || again[0].Flag4B != p.Moby.Flag4B || again[0].TreasureValue != p.Gives || again[0].Label != p.Moby.Label)
                throw new InvalidOperationException($"{p.Moby.Label} did not come back as saved ({again.Count} found).");
        }
        if (Treasure() != after) throw new InvalidOperationException($"The level's treasure is {Treasure()} after opening it again, not {after}.");
        Console.WriteLine($"reopened: {placed.Count} chests as saved, treasure {Treasure()}");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        string key = created ? window.FreshSceneForTesting!.TargetKey : level;
        int levelId = catalog.FindByKey(key)?.LevelId ?? (key == "authored65" ? 65 : throw new InvalidOperationException("No level id for " + key));
        File.WriteAllText(Path.Combine(outDir, "reward-chests.json"), JsonSerializer.Serialize(new
        {
            levels = new[] { new { level = key, levelId, treasure = after, chests = placed.Select(p => new {
                x = (int)MathF.Round(p.Moby.Position.X * 16), y = (int)MathF.Round(p.Moby.Position.Y * 16), z = (int)MathF.Round(p.Moby.Position.Z * 16),
                reward = p.Moby.Flag4B, gives = p.Gives, label = p.Moby.Label }) } }
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NEW_LEVEL_REWARD_CHESTS_OK");
    }
}
