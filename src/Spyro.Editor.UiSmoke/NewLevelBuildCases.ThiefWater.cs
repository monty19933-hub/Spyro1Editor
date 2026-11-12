using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

/// <summary><c>thief-water</c>: an egg thief's run is kept off water, lava and ooze. THIEF_LEVEL (default clifftown, which
/// has its own thief and a lake) must hold an egg thief (its own, or one added by <c>thief-add</c> and saved; Beast
/// Makers' open floor near the start is swamp, so thief-add finds no dry place for a run there). A point of his run is dragged onto the
/// nearest water tile: the editor must keep the point where it was and say it is water; New Loop Here must lay a run
/// with no point over water.</summary>
internal static partial class NewLevelBuildCases
{
    private static void ThiefWaterStep(MainWindow window, string outDir)
    {
        string key = Environment.GetEnvironmentVariable("THIEF_LEVEL") ?? "clifftown";
        Load(window, key);
        SelectTab(window, "Objects");
        // Added objects' runs are built once the object catalogue has been read (in the background after a level opens).
        var ready = (Task)Invoke(window, "EnsureUniversalCatalogAsync")!;
        Wait(() => ready.IsCompleted, "the object catalogue", 300); Pump(40);
        bool LiquidAt(float x, float y) => (bool)Invoke(window, "IsLiquidAt", x, y)!;
        Moby? thief = null; NativeMobyPath? path = null;
        foreach (var candidate in Mobys(window).Where(m => !m.IsRemoved && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.IsAdded))
        {
            SelectObject(window, candidate);
            if (Invoke(window, "SelectedNativeMobyPath") is NativeMobyPath run) { thief = candidate; path = run; break; }
            Console.WriteLine($"  {candidate.DisplayLabel} (row {candidate.TrueIndex}, added {candidate.IsAdded}) has no run");
        }
        if (thief == null || path == null) throw new InvalidOperationException($"{key} has no egg thief with a run: run thief-add first.");
        var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        bool Liquid(TerrainPolygon p) => (bool)Invoke(window, "IsLiquidTile", p)!;
        // Water tiles that are the floor at their middle (not water under a bridge or a ledge: a thief runs on that).
        var water = geometry.Polygons.Where(p => !p.IsTerrainRemoved && p.Points.Count >= 3 && Liquid(p) && LiquidAt(p.Points.Average(q => q.X), p.Points.Average(q => q.Y))).ToList();
        var hazards = (Spyro.Editor.Core.Scene.LevelHazards)Invoke(window, "CurrentHazards")!;
        Console.WriteLine($"{key}: {thief.DisplayLabel} (row {thief.TrueIndex}, added {thief.IsAdded}) at ({thief.Position.X:0},{thief.Position.Y:0},{thief.Position.Z:0}); run of {path.Nodes.Count} points; " +
            $"{water.Count} water, lava or ooze tiles; {hazards.LiquidTriangles} liquid collision triangles: {hazards.Describe()}");
        Console.WriteLine($"   level extent: x {geometry.Polygons.Min(p => p.Points.Min(q => q.X)):0}..{geometry.Polygons.Max(p => p.Points.Max(q => q.X)):0}, " +
            $"z {geometry.Polygons.Min(p => p.ZValues.Min()):0}..{geometry.Polygons.Max(p => p.ZValues.Max()):0}");
        if (water.Count == 0) throw new InvalidOperationException($"{key} has no water, lava or ooze tile the editor knows of.");
        // The water tile nearest any point of the run, and that point.
        var (node, tile, distance) = path.Nodes.SelectMany(n => water.Select(w =>
        {
            float cx = w.Points.Average(q => q.X), cy = w.Points.Average(q => q.Y);
            return (Node: n, Tile: w, D: MathF.Sqrt((cx - n.Position.X) * (cx - n.Position.X) + (cy - n.Position.Y) * (cy - n.Position.Y)));
        })).OrderBy(c => c.D).First();
        float tx = tile.Points.Average(q => q.X), ty = tile.Points.Average(q => q.Y);
        var before = node.Position;
        string said = window.MoveRunPointForTesting(path, node.Index, tx - before.X, ty - before.Y);
        var after = path.Nodes[node.Index].Position;
        Console.WriteLine($"point {node.Index + 1} at ({before.X:0},{before.Y:0}) dragged {distance:0} units onto water at ({tx:0},{ty:0}): {said}");
        if (after != before) throw new InvalidOperationException("A run point was moved onto water.");
        if (!said.Contains("water, lava or ooze", StringComparison.Ordinal)) throw new InvalidOperationException("The editor did not say it is water: " + said);
        // The run card warns while he stands in or runs through water.
        bool standsWet = LiquidAt(thief.Position.X, thief.Position.Y), runWet = path.Nodes.Any(n => LiquidAt(n.Position.X, n.Position.Y));
        string note = Get<TextBlock>(window, "_routeToolsNote").Text ?? "";
        Console.WriteLine($"stands in water {standsWet}, run through water {runWet}; card: {note}");
        if ((standsWet || runWet) != note.Contains("Spyro sinks there and can't catch him", StringComparison.Ordinal))
            throw new InvalidOperationException("The run card's water warning does not match where the thief is.");
        if (thief.IsAdded)
        {
            // New Loop Here either lays a run on dry ground or keeps the run and says why.
            for (int k = 0; k < 3; k++)
            {
                var old = SelectedRun(window).Nodes.Select(n => n.Position).ToList();
                window.ReshapeSelectedRouteForTesting("loop"); Pump(5);
                var now = SelectedRun(window);
                bool changed = !now.Nodes.Select(n => n.Position).SequenceEqual(old);
                bool over = now.Nodes.Any(n => LiquidAt(n.Position.X, n.Position.Y));
                Console.WriteLine($"New Loop Here {k + 1}: {Status(window)} — changed {changed}, a point over water {over}");
                if (changed && over) throw new InvalidOperationException("New Loop Here laid a run point over water.");
                if (!changed && !Status(window).Contains("water, lava or ooze", StringComparison.Ordinal) && !Status(window).Contains("new run round", StringComparison.Ordinal))
                    throw new InvalidOperationException("New Loop Here neither laid a run nor said why: " + Status(window));
            }
        }
        Snapshot(window, Path.Combine(outDir, "thief-water.png"));
        Console.WriteLine("NEW_LEVEL_THIEF_WATER_OK");
    }
}
