using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

/// <summary>Terrain › Tiles › Add beside ◀ ▲: from the meadow's westmost tile, ◀ clicked until the map's west edge
/// (each copy exactly against the last: the row has no gaps), the click past the edge refused with a message; ▲ once;
/// the saved scene read back.</summary>
internal static partial class NewLevelBuildCases
{
    private static void AddBeside(MainWindow window, string outDir)
    {
        var task = (Task)Invoke(window, "NewMainLevelAsync")!;
        Wait(() =>
        {
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask) Click(Find<Button>(ask, "ConfirmationConfirm"));
            return task.IsCompleted;
        }, "File > New Level", 600);
        var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
        float t = StarterLevel.Tile, o = StarterLevel.Origin;
        var start = geometry.Polygons.First(p => p.Points.Count == 4 && Math.Abs(p.Points.Min(v => v.X) - o) < 1 && Math.Abs(p.Points.Min(v => v.Y) - (o + 2 * t)) < 1 && p.ZValues.All(z => Math.Abs(z - StarterLevel.Floor) < 1));
        Set(window, "_selectedTerrain", start); Set(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(start));
        var west = Find<Button>(window, "AddTerrainWest");
        int clicks = (int)(o / t);
        for (int i = 0; i < clicks; i++) { Click(west); Pump(5); }
        var row = geometry.Polygons.Where(p => p.Points.Count == 4 && Math.Abs(p.Points.Min(v => v.Y) - (o + 2 * t)) < 1 && p.Points.Max(v => v.X) <= o + 0.5f).ToList();
        var xs = row.Select(p => p.Points.Min(v => v.X)).OrderBy(x => x).ToList();
        Console.WriteLine($"after {clicks} clicks west: {row.Count} new tiles from x {xs.First()} to {row.Max(p => p.Points.Max(v => v.X))}");
        if (row.Count != clicks || xs.First() != 0 || xs.Zip(xs.Skip(1)).Any(p => p.Second - p.First != t))
            throw new InvalidOperationException("The added tiles are not a gapless row against the edge.");
        Click(west); Pump(5);
        string refusal = Status(window);
        Console.WriteLine("one more click: " + refusal);
        if (!refusal.Contains("west edge", StringComparison.Ordinal)) throw new InvalidOperationException("Adding past the map's edge was not refused with a message.");
        var top = geometry.Polygons.First(p => p.Points.Count == 4 && Math.Abs(p.Points.Min(v => v.X) - o) < 1 && Math.Abs(p.Points.Max(v => v.Y) - (o + 12 * t)) < 1);
        Set(window, "_selectedTerrain", top); Set(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(top));
        Click(Find<Button>(window, "AddTerrainNorth")); Pump(5);
        var added = Get<TerrainPolygon>(window, "_selectedTerrain");
        Console.WriteLine($"north copy: y {added.Points.Min(v => v.Y)}..{added.Points.Max(v => v.Y)} (source ended at {top.Points.Max(v => v.Y)}); heights {string.Join(",", added.ZValues.Distinct())}");
        if (added.Points.Min(v => v.Y) != top.Points.Max(v => v.Y) || !added.ZValues.SequenceEqual(top.ZValues)) throw new InvalidOperationException("The north copy is not against the tile's north edge at its height.");
        var save = (Task<bool>)Invoke(window, "SaveFreshSceneAsync")!; Wait(() => save.IsCompleted, "save", 60);
        Snapshot(window, Path.Combine(outDir, "01-add-beside.png"));
        Console.WriteLine("NEW_LEVEL_ADD_BESIDE_OK");
    }
}
