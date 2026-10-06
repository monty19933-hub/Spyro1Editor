using System.Diagnostics;
using System.Reflection;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Survey of a level's terrain from the editor's point of view: how many tiles have collision under every
/// corner, and how much height room each tile's sector leaves for Raise and Lower. Read-only.
/// Usage: UiSmoke --terrain-survey &lt;project folder&gt; &lt;output folder&gt; [level key, default artisans]
/// </summary>
internal static class TerrainSurveyCases
{
    internal static void Run(string project, string outDir, string levelKey)
    {
        outDir = Path.GetFullPath(outDir);
        project = Path.GetFullPath(project);
        Directory.CreateDirectory(outDir);
        string workspace = Path.Combine(outDir, "project");
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        var clone = Process.Start(new ProcessStartInfo("cp", ["-Rc", project, workspace]) { RedirectStandardError = true });
        clone!.WaitForExit();
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DATA_ROOT", Path.Combine(outDir, "data-root"));
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR", Path.Combine(outDir, "logs"));
        Environment.CurrentDirectory = workspace;
        MainWindow window = new() { Width = 1440, Height = 900 };
        window.Show();
        try
        {
            WaitIdle(window);
            LevelDefinition level = Get<LevelCatalog>(window, "_retailCatalog").FindByKey(levelKey)!;
            if (window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel?.Key != levelKey)
            {
                var load = (Task)Invoke(window, "SelectLevelAsync", level, null)!;
                Wait(() => load.IsCompleted, "load " + level.DisplayName, 300);
                WaitIdle(window);
            }
            var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
            var keys = Get<HashSet<string>>(window, "_terrainCollisionTriangleKeys");
            var points = keys.SelectMany(k => k.Split('|')).ToHashSet(StringComparer.Ordinal);
            Console.WriteLine($"{level.DisplayName}: {geometry.Polygons.Count} faces, {keys.Count} collision triangles, {points.Count} collision points, {geometry.SourceSectors.Count} sectors");
            int flatSectors = geometry.SourceSectors.Count(s => s.HighPolyCoordinates is { UsesSpecialZScale: true });
            Console.WriteLine($"sectors with the fine-height band (bit 12): {flatSectors}");
            int full = 0, partial = 0, none = 0, lp = 0, removed = 0;
            int cannotLower = 0, cannotLower32 = 0, cannotRaise32 = 0, noSector = 0;
            var examples = new List<string>();
            foreach (var p in geometry.Polygons)
            {
                if (p.IsTerrainRemoved) { removed++; continue; }
                if (!string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase)) { lp++; continue; }
                int covered = 0;
                for (int i = 0; i < p.Points.Count && i < p.OriginalZValues.Length; i++)
                {
                    var op = p.OriginalPoints.Count == p.Points.Count ? p.OriginalPoints[i] : p.Points[i];
                    if (points.Contains($"{MathF.Round(op.X)},{MathF.Round(op.Y)},{MathF.Round(p.OriginalZValues[i])}")) covered++;
                }
                if (covered == p.Points.Count) full++; else if (covered > 0) partial++; else none++;
                var sector = geometry.SourceSectors.FirstOrDefault(s => s.SectorOffset == p.SectorOffset);
                if (sector?.HighPolyCoordinates is not { } c) { noSector++; continue; }
                float bandMin, bandMax;
                if (c.UsesSpecialZScale) { bandMin = c.DecodedOriginZ / 8f; bandMax = (c.DecodedOriginZ + 1023) / 8f; }
                else { bandMin = c.DecodedOriginZ; bandMax = c.DecodedOriginZ + 1023; }
                float minZ = p.OriginalZValues.Take(p.Points.Count).Min(), maxZ = p.OriginalZValues.Take(p.Points.Count).Max();
                if (minZ - bandMin < 1) cannotLower++;
                if (minZ - 32 < bandMin) cannotLower32++;
                if (maxZ + 32 > bandMax) cannotRaise32++;
                if (examples.Count < 6 && covered == p.Points.Count && p.TextureId >= 0 && (minZ - 32 < bandMin))
                    examples.Add($"{p.RuntimeKey} z {minZ}-{maxZ} band {bandMin}-{bandMax} fine={c.UsesSpecialZScale}");
            }
            Console.WriteLine($"HP tiles: {full} with collision under every corner, {partial} partly, {none} none; {lp} LP faces; {removed} removed; {noSector} without sector data");
            Console.WriteLine($"height room (original tiles): {cannotLower} tiles sit at the bottom of their sector's band, {cannotLower32} cannot be lowered by 32, {cannotRaise32} cannot be raised by 32");
            foreach (string e in examples) Console.WriteLine("  example cannot-lower tile: " + e);
            // Band statistics per sector.
            var bands = geometry.SourceSectors.Where(s => s.HighPolyCoordinates != null).Select(s =>
            {
                var c = s.HighPolyCoordinates!.Value;
                var tiles = geometry.Polygons.Where(p => p.SectorOffset == s.SectorOffset && string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) && !p.IsTerrainRemoved).ToList();
                float tMin = tiles.Count == 0 ? 0 : tiles.Min(p => p.OriginalZValues.Take(p.Points.Count).Min());
                float tMax = tiles.Count == 0 ? 0 : tiles.Max(p => p.OriginalZValues.Take(p.Points.Count).Max());
                float bandMin = c.UsesSpecialZScale ? c.DecodedOriginZ / 8f : c.DecodedOriginZ;
                float bandMax = c.UsesSpecialZScale ? (c.DecodedOriginZ + 1023) / 8f : c.DecodedOriginZ + 1023;
                return (s.SectorIndex, c.UsesSpecialZScale, bandMin, bandMax, tMin, tMax, tiles.Count);
            }).ToList();
            Console.WriteLine("sector  fine  band            tiles z        count");
            foreach (var b in bands.Take(80))
                Console.WriteLine($"{b.SectorIndex,5}  {(b.UsesSpecialZScale ? "yes" : "no "),4}  {b.bandMin,6}-{b.bandMax,-6}  {b.tMin,6}-{b.tMax,-6}  {b.Item7}");
            Console.WriteLine("TERRAIN_SURVEY_DONE");
        }
        finally { window.Close(); Pump(10); }
    }

    private static void WaitIdle(MainWindow window)
    {
        var quiet = Stopwatch.StartNew(); var total = Stopwatch.StartNew();
        while (total.Elapsed < TimeSpan.FromSeconds(300))
        {
            Dispatcher.UIThread.RunJobs(); Thread.Sleep(10);
            var snapshot = window.CaptureEditorShellSessionSnapshotForTesting();
            bool busy = Get<bool>(window, "_loadingLevel") || Get<bool>(window, "_handlingLevelSelection") ||
                        Get<int>(window, "_levelSelectionAsyncInFlight") > 0 || snapshot.CurrentLevel == null || snapshot.CurrentGeometry == null;
            if (busy) quiet.Restart(); else if (quiet.Elapsed > TimeSpan.FromSeconds(1.5)) return;
        }
        throw new TimeoutException("The editor did not become idle.");
    }
    private static object? Invoke(MainWindow w, string name, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic).First(x => x.Name == name && x.GetParameters().Length == args.Length);
        return m.Invoke(m.IsStatic ? null : w, args);
    }
    private static T Get<T>(MainWindow w, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static void Pump(int times = 1) { for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } }
    private static void Wait(Func<bool> predicate, string label, int seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate() && clock.Elapsed < TimeSpan.FromSeconds(seconds)) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        if (!predicate()) throw new InvalidOperationException("Terrain survey: " + label);
        Dispatcher.UIThread.RunJobs();
    }
}
