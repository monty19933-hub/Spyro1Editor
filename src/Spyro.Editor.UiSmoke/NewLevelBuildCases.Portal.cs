using SkiaSharp;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

internal static partial class NewLevelBuildCases
{
    // Town Square doorway (Artisans) -> new Stone Hill doorway. Its stonework faces -x (the open ground; +x is the
    // edge of the map). The Stone Hill portal is reached from the bowl, on its -n side, so the copy turns Town
    // Square's front onto -n: a 50 degree proper rotation (Create BIN refuses copies whose surfaces turn round).
    internal static readonly int[] DarkHollowDoorway =
        [617, 618, 622, 641, 644, 645, 651, 657, 619, 623, 650, 658, 640, 643, 646, 661, 620, 621, 647, 649, 639, 642, 648, 1652, 662, 1653, 637, 638, 656, 653];
    private static readonly (double X, double Y, double Z) DhBase = (9385.09, 3222.63, 564.13);
    private static readonly (double X, double Y) TsFront = (-4093 / 4096.0, 150 / 4096.0);
    private static readonly (double X, double Y) TsAlong = (4.69 / 127.96, 127.87 / 127.96);
    private static readonly (double X, double Y, double Z) ShBase = (6274.0, 3336.3, 419.4);
    private static readonly (double X, double Y) ShNormal = (2741 / 4096.0, 3044 / 4096.0);
    private static readonly (double X, double Y) ShAlong = (-3044 / 4096.0, 2741 / 4096.0);
    private const double WidthScale = 122.2 / 127.96;

    /// <summary>Where a Town Square doorway corner lands for the Stone Hill portal moved by (dx,dy,dz).</summary>
    internal static (double X, double Y, double Z) ArchPoint(double x, double y, double z, double dx, double dy, double dz)
    {
        double rx = x - DhBase.X, ry = y - DhBase.Y;
        double depth = rx * TsFront.X + ry * TsFront.Y, along = (rx * TsAlong.X + ry * TsAlong.Y) * WidthScale;
        return (ShBase.X + dx - depth * ShNormal.X + along * ShAlong.X,
                ShBase.Y + dy - depth * ShNormal.Y + along * ShAlong.Y,
                z - DhBase.Z + ShBase.Z + dz);
    }

    internal static (double dx, double dy, double dz) PortalOffset()
    {
        string[] v = (System.Environment.GetEnvironmentVariable("NEW_LEVEL_PORTAL_OFFSET") ?? "-420,-1080,-10").Split(',');
        return (double.Parse(v[0]), double.Parse(v[1]), double.Parse(v[2]));
    }

    private static void Plot(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        var snap = window.CaptureEditorShellSessionSnapshotForTesting();
        var g = snap.CurrentGeometry!;
        var (dx, dy, dz) = PortalOffset();
        float x0 = 4900, y0 = 1500, size = 2600, scale = 1100 / size;
        using var bitmap = new SKBitmap(1100, 1100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        SKPoint P(double x, double y) => new((float)((x - x0) * scale), (float)((y - y0) * scale));
        foreach (var p in g.Polygons.OrderBy(p => p.MaxZ))
        {
            using var path = new SKPath();
            path.MoveTo(P(p.Points[0].X, p.Points[0].Y));
            for (int i = 1; i < p.Points.Count; i++) path.LineTo(P(p.Points[i].X, p.Points[i].Y));
            path.Close();
            string surface = TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface);
            byte shade = (byte)Math.Clamp((p.AvgZ - 300) * 0.8, 30, 255);
            SKColor color = surface switch
            {
                "grass" => new SKColor(0, shade, 0),
                "water" => new SKColor(0, 0, shade),
                _ => new SKColor(shade, shade, shade)
            };
            using var fill = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path, fill);
        }
        using var text = new SKPaint { Color = SKColors.Yellow, TextSize = 11, IsAntialias = true };
        using var dot = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Fill };
        foreach (var m in snap.CurrentMobys.Where(m => !m.IsRemoved))
        {
            var at = P(m.Position.X, m.Position.Y);
            canvas.DrawCircle(at, 3, dot);
            canvas.DrawText($"{m.DisplayLabel} {m.Position.Z:0}", at.X + 4, at.Y, text);
        }
        using var arch = new SKPaint { Color = SKColors.Magenta, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        foreach (int i in DarkHollowDoorway)
        {
            var p = g.Polygons[i];
            using var path = new SKPath();
            for (int k = 0; k < p.Points.Count; k++)
            {
                var a = ArchPoint(p.Points[k].X, p.Points[k].Y, p.ZValues[k], dx, dy, dz);
                if (k == 0) path.MoveTo(P(a.X, a.Y)); else path.LineTo(P(a.X, a.Y));
            }
            path.Close();
            canvas.DrawPath(path, arch);
        }
        using var marker = new SKPaint { Color = SKColors.Cyan, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawCircle(P(6408 + dx, 3489 + dy), 6, marker);
        // grid every 256
        using var grid = new SKPaint { Color = new SKColor(255, 255, 255, 60), StrokeWidth = 1 };
        for (float v = 0; v < 12000; v += 256)
        {
            canvas.DrawLine(P(v, y0).X, 0, P(v, y0).X, 1100, grid);
            canvas.DrawLine(0, P(x0, v).Y, 1100, P(x0, v).Y, grid);
            canvas.DrawText($"{v}", P(v, y0).X + 2, 12, text);
            canvas.DrawText($"{v}", 2, P(x0, v).Y - 2, text);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(Path.Combine(outDir, "plot.png"), data.ToArray());
        // Ground under the proposed arch footprint and in front of it.
        foreach (var (label, depth, along) in new[] { ("front-200", -200.0, 0.0), ("front-80", -80.0, 0.0), ("plane", 0, 0), ("left pillar", 20, -110), ("right pillar", 20, 110), ("behind-100", 100, 0), ("behind-200", 200, 0) })
        {
            double wx = ShBase.X + dx + depth * ShNormal.X + along * ShAlong.X, wy = ShBase.Y + dy + depth * ShNormal.Y + along * ShAlong.Y;
            var args = new object?[] { (float)wx, (float)wy, 420f, 0f, -1, false };
            var m = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            bool ok = (bool)m.Invoke(window, args)!;
            Console.WriteLine($"{label,-13} ({wx:0},{wy:0}) ground {(ok ? ((float)args[3]!).ToString("0") : "none")}");
        }
        Console.WriteLine("NEW_LEVEL_PLOT_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static void PortalBuild(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var viewport = Get<EditorViewport>(window, "_viewport");
        var (dx, dy, dz) = PortalOffset();

        // 1. Remove the old Stone Hill doorway (stone and trim) with its collision.
        int removed = 0;
        foreach (int i in ArchFaces)
        {
            Invoke(window, "StageTerrainRemoval", i, g.Polygons[i], "Removed");
            if (g.Polygons[i].IsTerrainRemoved) removed++; else Console.WriteLine($"  NOT removed {i}: {Status(window)}");
        }
        Console.WriteLine($"removed {removed}/{ArchFaces.Length} tiles of the old doorway");

        // 2. Copy the Dark Hollow doorway, turned to face the way the Stone Hill portal faces, at the new spot.
        int copied = 0;
        foreach (int i in DarkHollowDoorway)
        {
            var p = g.Polygons[i];
            if (!(bool)Invoke(window, "CanStageTerrainAddCopy", p)!) { Console.WriteLine($"  cannot copy {i}"); continue; }
            var deltas = new Spyro.Editor.Core.Primitives.Vector2f[p.OriginalPoints.Count];
            for (int k = 0; k < deltas.Length; k++)
            {
                var a = ArchPoint(p.OriginalPoints[k].X, p.OriginalPoints[k].Y, p.OriginalZValues[k], dx, dy, dz);
                deltas[k] = new((float)Math.Round(a.X - p.OriginalPoints[k].X), (float)Math.Round(a.Y - p.OriginalPoints[k].Y));
            }
            Invoke(window, "ClearTerrainBrushUndoHistory");
            p.ApplyTerrainVertexXYDeltas(deltas);
            p.ApplyTerrainDeltaZ((float)Math.Round(ShBase.Z - DhBase.Z + dz));
            p.StageTerrainAddClone();
            copied++;
        }
        viewport.NotifyTerrainPresentationDataChanged();
        Invoke(window, "RefreshCurrentLevelDetails");
        Console.WriteLine($"copied {copied}/{DarkHollowDoorway.Length} tiles of the Dark Hollow doorway");

        // 3. Move the Stone Hill portal (travel path marker and everything linked to it).
        Moby marker = Mobys(window).Single(m => m.TrueIndex == 38);
        SetField(window, "_selectedMoby", marker);
        Invoke(window, "NudgeSelectedMoby", (float)dx, (float)dy, (float)dz);
        Console.WriteLine("portal: " + Status(window));
        foreach (int t in new[] { 38, 144, 157 })
        {
            var m = Mobys(window).Single(m => m.TrueIndex == t);
            Console.WriteLine($"  T{t} {m.DisplayLabel} now ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0})");
        }

        // 4. Is there ground where the old doorway stood?
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var (x, y) in new[] { (6274f, 3336f), (6230f, 3380f), (6320f, 3295f), (6340f, 3280f), (6215f, 3390f), (6300f, 3360f), (6250f, 3305f) })
        {
            var args = new object?[] { x, y, 420f, 0f, -1, false };
            bool ok = (bool)tryZ.Invoke(window, args)!;
            Console.WriteLine($"  ground at old doorway ({x},{y}): {(ok ? ((float)args[3]!).ToString("0") : "NONE")}");
        }
        Save(window);
        var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
        Wait(() => check.IsCompleted, "export check", 600);
        Console.WriteLine($"export check: {(check.Result ? "ready" : "NOT ready")} — {Status(window)}");
        viewport.SetViewMode(ViewportViewMode.Map); Pump(10);
        viewport.FocusTerrain(DarkHollowDoorway[0]); Pump(20);
        Snapshot(window, Path.Combine(outDir, "portal-map.png"));
        Console.WriteLine("NEW_LEVEL_PORTAL_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static void Footprint(MainWindow window)
    {
        Load(window, "artisans");
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var basePts = ArchFaces.SelectMany(i => Enumerable.Range(0, g.Polygons[i].OriginalPoints.Count)
                .Where(k => g.Polygons[i].OriginalZValues[k] <= 425)
                .Select(k => (X: MathF.Round(g.Polygons[i].OriginalPoints[k].X), Y: MathF.Round(g.Polygons[i].OriginalPoints[k].Y), Z: MathF.Round(g.Polygons[i].OriginalZValues[k]))))
            .Distinct().ToList();
        foreach (var cluster in basePts.GroupBy(p => p.X + p.Y < 6274 + 3336 ? "upper-left pillar" : "lower-right pillar"))
        {
            float cx = cluster.Average(p => p.X), cy = cluster.Average(p => p.Y);
            Console.WriteLine($"{cluster.Key} centre ({cx:0},{cy:0}): " + string.Join(" ", cluster.OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx)).Select(p => $"({p.X},{p.Y},{p.Z})")));
        }
        // Ground faces touching the doorway, with their corners.
        foreach (int i in Enumerable.Range(0, g.Polygons.Count).Where(i => !ArchFaces.Contains(i) && g.Polygons[i].OriginalPoints.Any(q => Math.Abs(q.X - 6274) < 160 && Math.Abs(q.Y - 3336) < 160) && g.Polygons[i].MaxZ < 430))
        {
            var p = g.Polygons[i];
            object r = Invoke(window, "GetTerrainAddCopyFaceReadiness", p)!;
            Console.WriteLine($"ground {i} {p.RuntimeKey} tex{p.TextureId} {TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface)} copy={Prop(r, "CanAddIndependentVisibleTerrain")} coll {Prop(r, "MatchedCollisionTriangles")}/{Prop(r, "TotalCollisionTriangles")}: " +
                string.Join(" ", Enumerable.Range(0, p.OriginalPoints.Count).Select(k => $"({p.OriginalPoints[k].X:0},{p.OriginalPoints[k].Y:0},{p.OriginalZValues[k]:0})")));
        }
        Console.WriteLine("NEW_LEVEL_FOOTPRINT_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    // The old pillars stood on no ground. Each footprint is a pentagon bordered by existing grass.
    private static readonly (float X, float Y, float Z)[][] PillarHoles =
    [
        [(6303, 3254, 420), (6343, 3217, 418), (6399, 3280, 420), (6359, 3316, 420), (6324, 3292, 419)],
        [(6229, 3377, 419), (6193, 3352, 420), (6153, 3389, 420), (6209, 3451, 419), (6249, 3415, 420)],
    ];
    private static readonly (int Quad, int Triangle)[] FillSources = [(3819, 3814), (3816, 3813)];

    private static double SignedArea(IReadOnlyList<(float X, float Y)> p) =>
        Enumerable.Range(0, p.Count).Sum(i => (double)p[i].X * p[(i + 1) % p.Count].Y - (double)p[(i + 1) % p.Count].X * p[i].Y) / 2;

    private static void FillHoles(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        Console.WriteLine($"old doorway tiles still removed after reload: {ArchFaces.Count(i => g.Polygons[i].IsTerrainRemoved)}/{ArchFaces.Length}; copies {DarkHollowDoorway.Count(i => g.Polygons[i].IsTerrainAddClone)}/{DarkHollowDoorway.Length}");
        for (int h = 0; h < PillarHoles.Length; h++)
        {
            var hole = PillarHoles[h];
            Place(FillSources[h].Quad, [hole[0], hole[1], hole[2], hole[3]]);
            Place(FillSources[h].Triangle, [hole[0], hole[3], hole[4]]);
        }
        void Place(int source, (float X, float Y, float Z)[] target)
        {
            var p = g.Polygons[source];
            if (p.OriginalPoints.Count != target.Length || !(bool)Invoke(window, "CanStageTerrainAddCopy", p)!)
                throw new InvalidOperationException($"fill source {source} unsuitable");
            bool sourceCcw = SignedArea(p.OriginalPoints.Select(q => (q.X, q.Y)).ToArray()) > 0;
            if (sourceCcw != SignedArea(target.Select(q => (q.X, q.Y)).ToArray()) > 0)
                target = target.Reverse().ToArray();
            Invoke(window, "ClearTerrainBrushUndoHistory");
            p.ApplyTerrainPointPositions(target.Select(q => new Spyro.Editor.Core.Primitives.Vector2f(q.X, q.Y)).ToArray());
            p.ApplyTerrainZValues(target.Select(q => q.Z).ToArray());
            p.StageTerrainAddClone();
            Console.WriteLine($"  grass {source} {p.RuntimeKey} -> " + string.Join(" ", target.Select(q => $"({q.X},{q.Y},{q.Z})")));
        }
        Get<EditorViewport>(window, "_viewport").NotifyTerrainPresentationDataChanged();
        Invoke(window, "RefreshCurrentLevelDetails");
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (var (x, y) in new[] { (6230f, 3380f), (6340f, 3280f), (6215f, 3390f), (6350f, 3260f), (6200f, 3410f) })
        {
            var args = new object?[] { x, y, 420f, 0f, -1, false };
            bool ok = (bool)tryZ.Invoke(window, args)!;
            Console.WriteLine($"  ground at old pillar ({x},{y}): {(ok ? ((float)args[3]!).ToString("0") : "NONE")}");
        }
        Save(window);
        var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
        Wait(() => check.IsCompleted, "export check", 600);
        Console.WriteLine($"export check: {(check.Result ? "ready" : "NOT ready")} — {Status(window)}");
        Console.WriteLine("NEW_LEVEL_FILL_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    private static void Export(MainWindow window)
    {
        Load(window, System.Environment.GetEnvironmentVariable("NEW_LEVEL_EXPORT_FROM") ?? "artisans");
        var command = (Task)Invoke(window, "CreateObjectTestBinAsync")!;
        bool confirmed = false;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!command.IsCompleted && clock.Elapsed < TimeSpan.FromMinutes(60))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(20);
            Window? scope = window.OwnedWindows.FirstOrDefault(w => w.Name == "ProjectExportScope");
            if (scope != null && !confirmed)
            {
                Console.WriteLine("scope dialog: " + string.Join(" | ", Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scope).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(30)));
                var start = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(scope).OfType<ComboBox>().FirstOrDefault(c => c.Name == "ExportStartLevel");
                if (start != null)
                {
                    Console.WriteLine("start level choices: " + string.Join(" | ", start.ItemsSource!.Cast<object>()));
                    // NEW_LEVEL_START (or NEW_LEVEL_EXPORT_START) names the level New Game starts in (default: the first, Artisans or its replacement).
                    string? wanted = System.Environment.GetEnvironmentVariable("NEW_LEVEL_START") ?? System.Environment.GetEnvironmentVariable("NEW_LEVEL_EXPORT_START");
                    var names = start.ItemsSource!.Cast<object>().Select(o => o?.ToString() ?? "").ToList();
                    int chosen = wanted == null ? 0 : names.FindIndex(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
                    if (chosen < 0) throw new InvalidOperationException($"NEW_LEVEL_START {wanted} is not one of: {string.Join(" | ", names)}");
                    start.SelectedIndex = chosen; Pump(5);
                    Console.WriteLine("New Game starts in: " + start.SelectedItem);
                }
                Click(Find<Button>(scope, "ExportCreate"));
                confirmed = true;
            }
            foreach (Window other in window.OwnedWindows.Where(w => w.Name != "ProjectExportScope").ToArray())
            {
                Console.WriteLine("dialog '" + other.Title + "': " + string.Join(" | ", Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(other).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Take(12)));
                Button? proceed = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(other).OfType<Button>()
                    .FirstOrDefault(b => (b.Content?.ToString() ?? "") is "Create Anyway" or "Continue" or "OK" or "Create BIN");
                if (proceed != null) { Console.WriteLine("  pressing " + proceed.Content); Click(proceed); Pump(20); }
                if (window.OwnedWindows.Contains(other)) other.Close();
            }
        }
        Pump(100);
        Console.WriteLine($"Create BIN finished={command.IsCompleted} faulted={command.IsFaulted} after {clock.Elapsed.TotalSeconds:0}s");
        Console.WriteLine("status: " + Status(window));
        Console.WriteLine("terrain problem: " + (window.CurrentTerrainExportProblemForTesting?.ToString() ?? "none"));
        string output = Path.Combine(System.Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE")!, "output");
        if (Directory.Exists(output))
            foreach (string f in Directory.GetFiles(output).OrderBy(File.GetLastWriteTimeUtc))
                Console.WriteLine($"  {Path.GetFileName(f)} {new FileInfo(f).Length} {File.GetLastWriteTime(f):HH:mm:ss}");
        Console.WriteLine("NEW_LEVEL_EXPORT_DONE");
    }
}


internal static partial class NewLevelBuildCases
{
    /// <summary>Re-places the copied Dark Hollow doorway tiles with the current transform.</summary>
    private static void PortalReorient(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var (dx, dy, dz) = PortalOffset();
        int moved = 0;
        foreach (int i in DarkHollowDoorway)
        {
            var p = g.Polygons[i];
            if (!p.IsTerrainAddClone) { Console.WriteLine($"  {i} is not a copy"); continue; }
            var deltas = new Spyro.Editor.Core.Primitives.Vector2f[p.OriginalPoints.Count];
            for (int k = 0; k < deltas.Length; k++)
            {
                var a = ArchPoint(p.OriginalPoints[k].X, p.OriginalPoints[k].Y, p.OriginalZValues[k], dx, dy, dz);
                deltas[k] = new((float)Math.Round(a.X - p.OriginalPoints[k].X), (float)Math.Round(a.Y - p.OriginalPoints[k].Y));
            }
            p.ApplyTerrainVertexXYDeltas(deltas);
            p.ApplyTerrainZValues(p.OriginalZValues.Select(z => z + (float)Math.Round(ShBase.Z - DhBase.Z + dz)).ToArray());
            moved++;
        }
        Get<EditorViewport>(window, "_viewport").NotifyTerrainPresentationDataChanged();
        Invoke(window, "RefreshCurrentLevelDetails");
        Console.WriteLine($"re-placed {moved} copied tiles");
        Save(window);
        var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
        Wait(() => check.IsCompleted, "export check", 600);
        Console.WriteLine($"export check: {(check.Result ? "ready" : "NOT ready")}");
        Console.WriteLine("NEW_LEVEL_REORIENT_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    // The Dark Hollow doorway faces +x. Turned 48 degrees onto +n it shows from behind the new Stone Hill portal,
    // back to back with the Town Square stonework that faces the bowl.
    internal static readonly int[] DarkHollowBackside =
        [1005, 1009, 1018, 402, 405, 450, 456, 462, 403, 406, 1006, 449, 1010, 1017, 457, 461, 404, 407, 1007, 451, 1016, 460, 1008, 1011, 458, 1012, 1014, 459, 1015];

    internal static (double X, double Y, double Z) BackPoint(double x, double y, double z, double dx, double dy, double dz)
    {
        double depth = x - 3652, along = (y - 4100.75) * (122.2 / 128.0);
        return (ShBase.X + dx + depth * ShNormal.X + along * ShAlong.X,
                ShBase.Y + dy + depth * ShNormal.Y + along * ShAlong.Y,
                z - 424.5 + ShBase.Z + dz);
    }

    private static void PortalBackside(MainWindow window)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var (dx, dy, dz) = PortalOffset();
        int copied = 0;
        foreach (int i in DarkHollowBackside)
        {
            var p = g.Polygons[i];
            if (p.IsTerrainAddClone || !(bool)Invoke(window, "CanStageTerrainAddCopy", p)!) { Console.WriteLine($"  cannot copy {i}"); continue; }
            var deltas = new Spyro.Editor.Core.Primitives.Vector2f[p.OriginalPoints.Count];
            for (int k = 0; k < deltas.Length; k++)
            {
                var a = BackPoint(p.OriginalPoints[k].X, p.OriginalPoints[k].Y, p.OriginalZValues[k], dx, dy, dz);
                deltas[k] = new((float)Math.Round(a.X - p.OriginalPoints[k].X), (float)Math.Round(a.Y - p.OriginalPoints[k].Y));
            }
            Invoke(window, "ClearTerrainBrushUndoHistory");
            p.ApplyTerrainVertexXYDeltas(deltas);
            p.ApplyTerrainDeltaZ((float)Math.Round(ShBase.Z - 424.5 + dz));
            p.StageTerrainAddClone();
            copied++;
        }
        Get<EditorViewport>(window, "_viewport").NotifyTerrainPresentationDataChanged();
        Invoke(window, "RefreshCurrentLevelDetails");
        Console.WriteLine($"copied {copied}/{DarkHollowBackside.Length} Dark Hollow tiles for the back of the doorway");
        Save(window);
        var check = (Task<bool>)Invoke(window, "RunBuildSafetyInspectorAsync", false)!;
        Wait(() => check.IsCompleted, "export check", 600);
        Console.WriteLine($"export check: {(check.Result ? "ready" : "NOT ready")}");
        Console.WriteLine("NEW_LEVEL_BACKSIDE_OK");
    }
}
