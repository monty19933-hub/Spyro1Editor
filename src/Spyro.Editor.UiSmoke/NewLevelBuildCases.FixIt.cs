using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

internal static partial class NewLevelBuildCases
{
    /// <summary><c>thief-path</c>: in Stone Hill (the game's own level) one point of the egg thief's run is lifted, as
    /// dragging it did before 2.3 (point 6 lies 195 units below the floor in the game; the drag put it on the floor), and
    /// the level saved. Until 2.3 Create BIN refused such a run ("Saved changes could not be included: Stone Hill: Egg
    /// thief …"); since 2.3 the height of a point is not judged (the game does not use it) and the run is exported.
    /// THIEF_PATH_RAISE: how far the point is lifted, in the game's units × 16 (default 3124, as in the user's project).</summary>
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
