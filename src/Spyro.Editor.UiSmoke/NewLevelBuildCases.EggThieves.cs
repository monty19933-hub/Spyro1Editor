using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Egg thieves in the game's own levels: one added to a level that has none (<c>thief-add</c>), and the run of
/// one of the game's own thieves changed (<c>thief-run</c>, <c>thief-drag</c>). Each writes thief.json for the emulator
/// (where he stands, the points of his run, the level).</summary>
internal static partial class NewLevelBuildCases
{
    private static bool RunThiefSteps(MainWindow window, string outDir, string step)
    {
        switch (step)
        {
            case "thief-add": ThiefAddStep(window, outDir); return true;
            case "thief-run": ThiefRunStep(window, outDir); return true;
            case "thief-drag": ThiefDragStep(window, outDir); return true;
            default: return false;
        }
    }

    private static Vector3f? ParsePoint(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        float[] v = [.. text.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture))];
        return new Vector3f(v[0], v[1], v[2]);
    }

    private static void SelectObject(MainWindow window, Moby? moby)
    {
        SetField(window, "_selectedMoby", moby);
        Invoke(window, "RefreshActionAvailability"); Pump(10);
    }

    private static NativeMobyPath SelectedRun(MainWindow window) =>
        (NativeMobyPath?)Invoke(window, "SelectedNativeMobyPath") ?? throw new InvalidOperationException("The selected object has no run.");

    private static bool Floor(MainWindow window, float x, float y, float z, out float floor)
    {
        object?[] args = [x, y, z, 0f, -1, false];
        var method = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        bool hit = (bool)method.Invoke(window, args)!;
        floor = (float)args[3]!;
        return hit;
    }

    private static void WriteThief(MainWindow window, string outDir, Moby thief, NativeMobyPath path, object? more = null)
    {
        var level = window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel!;
        File.WriteAllText(Path.Combine(outDir, "thief.json"), JsonSerializer.Serialize(new
        {
            level = level.Key, levelId = level.LevelId,
            at = new[] { thief.Position.X, thief.Position.Y, thief.Position.Z },
            run = path.Nodes.Select(n => new[] { n.Position.X, n.Position.Y, n.Position.Z }).ToArray(),
            original = path.Nodes.Select(n => new[] { n.OriginalPosition.X, n.OriginalPosition.Y, n.OriginalPosition.Z }).ToArray(),
            added = thief.IsAdded, row = thief.TrueIndex, more,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary><c>thief-add</c>: THIEF_LEVEL (a level of the game with no egg thief) is opened, an egg thief is taken
    /// from Add Object (THIEF_SOURCE, default stonehill) and set down on open ground (THIEF_AT "x,y,z", or the nearest
    /// open floor to where Spyro arrives), his run laid out with New Loop Here, and the level saved. THIEF_COUNT (1)
    /// thieves are added.</summary>
    private static void ThiefAddStep(MainWindow window, string outDir)
    {
        string key = Environment.GetEnvironmentVariable("THIEF_LEVEL") ?? throw new InvalidOperationException("Set THIEF_LEVEL.");
        string source = Environment.GetEnvironmentVariable("THIEF_SOURCE") is { Length: > 0 } s ? s : "stonehill";
        int count = int.TryParse(Environment.GetEnvironmentVariable("THIEF_COUNT"), out int n) ? n : 1;
        Load(window, key);
        SelectTab(window, "Objects");
        var had = Mobys(window).Where(m => !m.IsRemoved && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase)).ToList();
        Console.WriteLine($"{key}: {Mobys(window).Count(m => !m.IsRemoved)} objects, {had.Count} egg thieves of its own");
        var (browser, entries) = OpenBrowserReporting(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var pick = entries.SelectMany(e => e.Variants.Select(v => (Entry: e, Variant: v)))
            .FirstOrDefault(p => p.Variant.ClassId == LevelCompletionCheck.EggThiefClass && p.Variant.SourceLevelKey == source && p.Variant.Extractable);
        if (pick.Entry == null) throw new InvalidOperationException($"Add Object offers no egg thief from {source}.");
        Console.WriteLine($"Add Object: {pick.Entry.Name} ({pick.Entry.Category}/{pick.Entry.SubCategory}) from {source} row {pick.Variant.SourceRow}");
        var pose = (PortableLevelEntryPose?)Invoke(window, "TryLoadLevelEntryPose", key);
        Vector3f start = ParsePoint(Environment.GetEnvironmentVariable("THIEF_AT")) ?? (pose != null ? new Vector3f((float)pose.X, (float)pose.Y, (float)pose.Z) : Mobys(window).First(m => !m.IsRemoved).Position);
        List<object> report = [];
        Moby? first = null; NativeMobyPath? firstRun = null;
        for (int k = 0; k < count; k++)
        {
            // Open floor: of the places round the start, the one with room for the widest round run (level floor all round).
            float Room(float x, float y, float z)
            {
                foreach (float radius in new[] { 420f, 340f, 270f, 210f, 160f })
                    if (Enumerable.Range(0, 16).All(i => Floor(window, x + radius * MathF.Cos(i * MathF.PI / 8), y + radius * MathF.Sin(i * MathF.PI / 8), z + 40, out float f) && MathF.Abs(f - z) <= 40)) return radius;
                return 0;
            }
            var places = new[] { 0f, 300f, 600f, 900f, 1200f, 1600f, 2000f, 2600f }.SelectMany(d => Enumerable.Range(0, d == 0 ? 1 : 12).Select(i => (D: d, A: i * MathF.PI / 6)))
                .Select(q => (X: start.X + q.D * MathF.Cos(q.A), Y: start.Y + q.D * MathF.Sin(q.A), q.D))
                .Select(q => Floor(window, q.X, q.Y, start.Z, out float f) && MathF.Abs(f - start.Z) <= (float.TryParse(Environment.GetEnvironmentVariable("THIEF_RISE"), out float rise) ? rise : 200) ? (q.X, q.Y, Z: f, q.D, Room: Room(q.X, q.Y, f)) : (q.X, q.Y, Z: 0f, q.D, Room: 0f))
                .Where(q => q.Room > 0 && !Mobys(window).Any(m => !m.IsRemoved && m.IsAdded && MathF.Abs(m.Position.X - q.X) + MathF.Abs(m.Position.Y - q.Y) < 2.2f * q.Room && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(q => q.Room).ThenBy(q => q.D).ToList();
            Console.WriteLine($"    {places.Count} open places near ({start.X:0},{start.Y:0},{start.Z:0}); the widest: {string.Join("; ", places.Take(4).Select(q => $"({q.X:0},{q.Y:0},{q.Z:0}) room {q.Room:0}"))}");
            Moby? thief = null; NativeMobyPath? run = null; string said = "";
            foreach (var place in places.Take(6))
            {
                var task = (Task<IReadOnlyList<Moby>>)Invoke(window, "AddUniversalMobyAtAsync", lib, pick.Entry, pick.Variant, new Vector3f(place.X, place.Y, place.Z), 0, null)!;
                Wait(() => task.IsCompleted, "add the thief", 180);
                var added = task.GetAwaiter().GetResult();
                if (added.Count == 0) throw new InvalidOperationException("Add Object did not add the egg thief: " + Status(window));
                thief = added[0];
                SelectObject(window, thief);
                run = SelectedRun(window);
                window.ReshapeSelectedRouteForTesting("loop"); Pump(5);
                said = Status(window);
                if (said.Contains("new run round", StringComparison.Ordinal)) break;
                // No room for a run here: take him away again and try the next place.
                Invoke(window, "RemoveSelectedMoby"); Pump(5);
                thief = null;
            }
            if (thief == null || run == null) throw new InvalidOperationException("No open floor for an egg thief's run was found near " + start + ": " + said);
            run = SelectedRun(window);
            var card = Get<Border>(window, "_routeToolsCard"); var edit = Get<Button>(window, "_objectNativeMovementButton");
            Console.WriteLine($"thief {k + 1}: {thief.DisplayLabel} at ({thief.Position.X:0},{thief.Position.Y:0},{thief.Position.Z:0}); {said}");
            Console.WriteLine($"    run tools shown {card.IsVisible}; button \"{edit.Content}\" shown {edit.IsVisible}; note: {Get<TextBlock>(window, "_routeToolsNote").Text}");
            if (!card.IsVisible || !edit.IsVisible) throw new InvalidOperationException("The added thief's run tools are not shown.");
            if (thief.UniversalPathNodes == null) throw new InvalidOperationException("The run was not kept on the thief.");
            report.Add(new { at = new[] { thief.Position.X, thief.Position.Y, thief.Position.Z }, run = run.Nodes.Select(q => new[] { q.Position.X, q.Position.Y, q.Position.Z }).ToArray() });
            first ??= thief; firstRun ??= run;
        }
        Snapshot(window, Path.Combine(outDir, "thief-added.png"));
        Save(window);
        Console.WriteLine("saved: " + Status(window));
        WriteThief(window, outDir, first!, firstRun!, report);
        Console.WriteLine("NEW_LEVEL_THIEF_ADD_OK");
    }

    /// <summary><c>thief-run</c>: THIEF_LEVEL's own egg thief (THIEF_ROW, default the first) is selected and points of
    /// his run moved along the ground the way the editor's own tools move them (THIEF_MOVES "node:dx:dy;…" in game
    /// units, default: every second point 60 units towards the run's middle); the level is saved.</summary>
    private static void ThiefRunStep(MainWindow window, string outDir)
    {
        string key = Environment.GetEnvironmentVariable("THIEF_LEVEL") ?? "stonehill";
        Load(window, key);
        SelectTab(window, "Objects");
        var thieves = Mobys(window).Where(m => !m.IsRemoved && !m.IsAdded && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase)).ToList();
        if (thieves.Count == 0) throw new InvalidOperationException($"{key} has no egg thief of its own.");
        Moby thief = int.TryParse(Environment.GetEnvironmentVariable("THIEF_ROW"), out int row) ? thieves.First(m => m.TrueIndex == row) : thieves[0];
        SelectObject(window, thief);
        var path = SelectedRun(window);
        var edit = Get<Button>(window, "_objectNativeMovementButton");
        Console.WriteLine($"{key}: {thief.DisplayLabel} (row {thief.TrueIndex}) at ({thief.Position.X:0},{thief.Position.Y:0},{thief.Position.Z:0}); run of {path.Nodes.Count} points; button \"{edit.Content}\" shown {edit.IsVisible}");
        if (!edit.IsVisible) throw new InvalidOperationException("The game's own thief has no Edit button for his run.");
        float cx = path.Nodes.Average(q => q.Position.X), cy = path.Nodes.Average(q => q.Position.Y);
        List<(int Node, float Dx, float Dy)> moves = [];
        if (Environment.GetEnvironmentVariable("THIEF_MOVES") is { Length: > 0 } text)
            foreach (string part in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split(':');
                moves.Add((int.Parse(f[0]) - 1, float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture)));
            }
        else
            for (int i = 1; i < path.Nodes.Count; i += 2)
            {
                var p = path.Nodes[i].Position; float l = MathF.Max(1, MathF.Sqrt((cx - p.X) * (cx - p.X) + (cy - p.Y) * (cy - p.Y)));
                moves.Add((i, (cx - p.X) / l * 60, (cy - p.Y) / l * 60));
            }
        int moved = 0;
        foreach (var (node, dx, dy) in moves)
        {
            var before = path.Nodes[node].Position;
            string said = window.MoveRunPointForTesting(path, node, dx, dy);
            var after = path.Nodes[node].Position;
            Console.WriteLine($"    point {node + 1}: ({before.X:0},{before.Y:0},{before.Z:0}) -> ({after.X:0},{after.Y:0},{after.Z:0}) — {said}");
            if (after != before) moved++;
        }
        if (moved == 0) throw new InvalidOperationException("No point of the run could be moved.");
        Console.WriteLine($"check: {window.DescribeRunForTesting(path)}");
        Snapshot(window, Path.Combine(outDir, "thief-run.png"));
        Save(window);
        Console.WriteLine("saved: " + Status(window));
        WriteThief(window, outDir, thief, path);
        Console.WriteLine($"NEW_LEVEL_THIEF_RUN_OK moved={moved}");
    }

    /// <summary><c>thief-drag</c>: the drag the first report came from: point THIEF_NODE (default 6) of Stone Hill's thief
    /// dragged in the 3D view by THIEF_DRAG "dx,dy" (default a small pull). The point must land on the floor the thief
    /// runs on, and the run must stay one he can take; the level is saved. THIEF_EXPECT_REFUSED=1: the drag goes where he
    /// can't run, and the editor must keep the point and say why.</summary>
    private static void ThiefDragStep(MainWindow window, string outDir)
    {
        string key = Environment.GetEnvironmentVariable("THIEF_LEVEL") ?? "stonehill";
        Load(window, key);
        SelectTab(window, "Objects");
        var thief = Mobys(window).First(m => !m.IsRemoved && !m.IsAdded && m.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase));
        SelectObject(window, thief);
        var path = SelectedRun(window);
        int node = (int.TryParse(Environment.GetEnvironmentVariable("THIEF_NODE"), out int q) ? q : 6) - 1;
        float[] drag = [.. (Environment.GetEnvironmentVariable("THIEF_DRAG") ?? "24,18").Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture))];
        var before = path.Nodes[node].Position;
        // The viewport's own event, as a drag raises it (several small moves).
        for (int stepIndex = 0; stepIndex < 6; stepIndex++)
        {
            Invoke(window, "MoveNativePathNodeFromViewport", new ViewportNativePathNodeMoveRequestedEventArgs(path, path.Nodes[node], drag[0] / 6, drag[1] / 6));
            Pump(2);
        }
        var after = path.Nodes[node].Position;
        Console.WriteLine($"{key} {thief.DisplayLabel}: point {node + 1} dragged from ({before.X:0.#},{before.Y:0.#},{before.Z:0.#}) to ({after.X:0.#},{after.Y:0.#},{after.Z:0.#}) — {Status(window)}");
        Console.WriteLine($"check: {window.DescribeRunForTesting(path)}");
        string check = window.DescribeRunForTesting(path);
        if (Environment.GetEnvironmentVariable("THIEF_EXPECT_REFUSED") == "1")
        {
            if (after.X != before.X || after.Y != before.Y || !Status(window).Contains("can't go there", StringComparison.Ordinal))
                throw new InvalidOperationException("A point the thief could not run to was taken: " + Status(window));
            Console.WriteLine("NEW_LEVEL_THIEF_DRAG_REFUSED_OK");
            return;
        }
        if (after.X == before.X && after.Y == before.Y) throw new InvalidOperationException("The point did not move: " + Status(window));
        if (check != "ok") throw new InvalidOperationException("The dragged run is not one the thief can take: " + check);
        if (Floor(window, after.X, after.Y, after.Z - 32, out float floor) && MathF.Abs(after.Z - 32 - floor) > 1) throw new InvalidOperationException("The dragged point is not on the floor.");
        Save(window);
        Console.WriteLine("saved: " + Status(window));
        WriteThief(window, outDir, thief, path);
        Console.WriteLine("NEW_LEVEL_THIEF_DRAG_OK");
    }
}
