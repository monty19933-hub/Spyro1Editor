using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

/// <summary><c>rewards</c>: Objects › "What it gives". REWARD_LEVEL (a level key, "created" for the created level, or
/// "world:70") is opened; REWARD_COUNT (3) of its enemies that pay a gem are each selected and given REWARD_GEM (Purple)
/// through the card's own drop-down. The card must show for them and not for gems or creatures that pay nothing, the
/// object and the level's treasure must follow, and the level must save. Writes rewards.json (where each stands and
/// what it gives) for the emulator.</summary>
internal static partial class NewLevelBuildCases
{
    private static void RewardsStep(MainWindow window, string outDir)
    {
        string level = Environment.GetEnvironmentVariable("REWARD_LEVEL") ?? "stonehill";
        int count = int.Parse(Environment.GetEnvironmentVariable("REWARD_COUNT") ?? "3");
        var gem = GemValue.Known.First(g => g.Name.StartsWith(Environment.GetEnvironmentVariable("REWARD_GEM") ?? "Purple", StringComparison.OrdinalIgnoreCase));
        if (level == "created") { var open = (Task)Invoke(window, "OpenMainFreshLevelAsync")!; Wait(() => open.IsCompleted, "open the created level", 120); WaitIdle(window); }
        else if (level.StartsWith("world:", StringComparison.Ordinal)) { var open = (Task)Invoke(window, "OpenWorldLevelAsync", int.Parse(level[6..]))!; Wait(() => open.IsCompleted, "open the world level", 120); WaitIdle(window); }
        else Load(window, level);
        SelectTab(window, "Objects");
        var live = Mobys(window).Where(m => !m.IsRemoved).ToList();
        var card = Get<Border>(window, "_rewardCard");
        var box = Get<ComboBox>(window, "_rewardBox");
        void Select(Moby? m)
        {
            SetField(window, "_selectedMoby", m);
            Invoke(window, "ShowSelection", m == null ? ViewportSelectionChangedEventArgs.None : ViewportSelectionChangedEventArgs.ForMoby(m)); Pump(5);
        }
        // What pays a gem: enemies and chests. A loose gem and a creature that pays nothing have no choice.
        var payers = live.Where(m => !m.IsGemLike && !m.IsChestContent && GemValue.TryFromIdByte(m.Flag4B, out _)).ToList();
        var enemies = payers.Where(m => !m.IsChest && !m.IsLockedChestShell).ToList();
        Console.WriteLine($"{level}: {live.Count} objects, {payers.Count} pay a gem ({enemies.Count} of them not chests): " +
            string.Join(", ", enemies.GroupBy(m => m.DisplayLabel).Select(g => $"{g.Count()} {g.Key}").Take(12)));
        if (enemies.Count < count) throw new InvalidOperationException($"{level} has only {enemies.Count} enemies that pay a gem.");
        if (live.FirstOrDefault(m => m.IsVisibleGem) is { } looseGem) { Select(looseGem); if (card.IsVisible) throw new InvalidOperationException("The card shows for a loose gem."); }
        if (live.FirstOrDefault(m => !m.IsGemLike && m.Flag4B == 0xFF && !m.IsChest) is { } nothing) { Select(nothing); if (card.IsVisible) throw new InvalidOperationException("The card shows for an object that pays nothing."); }
        int Treasure() => Mobys(window).Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
        int before = Treasure();
        List<object> report = []; int expected = 0;
        // Different kinds first: one of each, then more of the same.
        var chosen = enemies.GroupBy(m => m.DisplayLabel).SelectMany(g => g.Select((m, i) => (m, i))).OrderBy(p => p.i).Select(p => p.m).Where(m => m.Flag4B != gem.IdByte).Take(count).ToList();
        foreach (var m in chosen)
        {
            Select(m);
            if (!card.IsVisible) throw new InvalidOperationException($"The card does not show for {m.DisplayLabel}.");
            int had = m.TreasureValue;
            var came = GemValue.Known[box.SelectedIndex];
            if (came.Value != had) throw new InvalidOperationException($"The card shows {came.Name} for {m.DisplayLabel}, which gives {had}.");
            box.SelectedIndex = GemValue.Known.ToList().FindIndex(g => g.IdByte == gem.IdByte); Pump(10);
            Console.WriteLine($"  {m.DisplayLabel} at ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}): {had} -> {m.TreasureValue}; {Status(window)}");
            if (m.Flag4B != gem.IdByte || m.TreasureValue != gem.Value) throw new InvalidOperationException("The object does not give the chosen gem.");
            if (m.IsAdded && m.UniversalRewardByte != gem.IdByte) throw new InvalidOperationException("An added object's reward is not kept for Create BIN.");
            if (!Status(window).Contains(gem.Name, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The status line does not say what it gives now.");
            expected += gem.Value - had;
            report.Add(new { label = m.DisplayLabel, x = m.Position.X, y = m.Position.Y, z = m.Position.Z, gives = gem.Value, gave = had, id = gem.IdByte, added = m.IsAdded, row = m.TrueIndex });
        }
        int after = Treasure();
        Console.WriteLine($"treasure {before} -> {after}");
        if (after - before != expected)
            throw new InvalidOperationException("The level's treasure did not follow the new rewards.");
        // Choosing the gem it has changes nothing.
        Select(chosen[0]);
        if ((bool)Invoke(window, "ApplyReward", chosen[0], gem)!) throw new InvalidOperationException("Choosing the same gem again counted as a change.");
        Snapshot(window, Path.Combine(outDir, "rewards-card.png"));
        if (window.FreshSceneForTesting != null)
        {
            var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!;
            Wait(() => save.IsCompleted, "Save Level", 120);
            if (!save.GetAwaiter().GetResult()) throw new InvalidOperationException("The level did not save: " + Status(window));
        }
        else Save(window);
        Console.WriteLine("saved: " + Status(window));
        File.WriteAllText(Path.Combine(outDir, "rewards.json"), JsonSerializer.Serialize(new { level, treasure = after, objects = report }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NEW_LEVEL_REWARDS_OK");
    }
}
