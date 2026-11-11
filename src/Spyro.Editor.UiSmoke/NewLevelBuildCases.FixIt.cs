using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

internal static partial class NewLevelBuildCases
{
    /// <summary><c>thief-path</c>: in Stone Hill (the game's own level) one point of the egg thief's run is moved off its
    /// ground, the way dragging it does, and the level saved. Create BIN refuses such a run ("Saved changes could not be
    /// included: Stone Hill: Egg thief …"): a stop for Fix It For Me. THIEF_PATH_RAISE: how far the point is lifted, in
    /// the game's units × 16 (default 3124, as in the project this was first seen in).</summary>
    private static void ThiefPathStep(MainWindow window)
    {
        Load(window, "stonehill");
        var mobys = Get<List<Moby>>(window, "_currentMobys");
        var thief = mobys.FirstOrDefault(m => !m.IsRemoved && !m.IsAdded && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Stone Hill's egg thief was not found.");
        SetField(window, "_selectedMoby", thief);
        Invoke(window, "RefreshActionAvailability"); Pump(10);
        var path = (NativeMobyPath)Invoke(window, "SelectedNativeMobyPath")!;
        var node = path.Nodes[Math.Min(5, path.Nodes.Count - 1)];
        int raise = int.TryParse(System.Environment.GetEnvironmentVariable("THIEF_PATH_RAISE"), out int r) ? r : 3124;
        node.TranslateRaw(0, 0, raise);
        Console.WriteLine($"thief {thief.DisplayLabel} (T{thief.TrueIndex}): point {node.Index + 1} of {path.Nodes.Count} lifted by {raise} to {node.Position}");
        Save(window);
        string file = Path.Combine(Workspace(window).RootPath, "stonehill-native-moby-path-edits.json");
        string saved = File.Exists(file) ? File.ReadAllText(file) : "";
        if (!saved.Contains("\"editCount\": 1", StringComparison.Ordinal)) throw new InvalidOperationException("The run's change was not saved: " + Status(window));
        Console.WriteLine("saved: " + Status(window));
        Console.WriteLine("NEW_LEVEL_THIEF_PATH_OK");
    }
}
