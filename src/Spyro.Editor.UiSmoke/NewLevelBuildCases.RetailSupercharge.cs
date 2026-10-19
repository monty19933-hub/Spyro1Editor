using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

/// <summary>Tile Type… "Supercharge ramp" in one of the game's own levels (RETAIL_SUPERCHARGE_LEVEL, default Artisans):
/// the longest straight downhill run of walkable tiles (one collision triangle per drawn triangle, solid ground, 6-30°)
/// is given supercharge ground the way a user does it (the dialog offers it; the tiles change and are saved, and the
/// status says Create BIN adds Spyro's supercharge animations). A level without room for them (RETAIL_SUPERCHARGE_TIGHT,
/// default Dark Passage; "none" skips) refuses the kind in plain words and saves nothing. Writes retail-supercharge.json
/// (the run's tiles, its top and its heading) for the emulator, which charges down it.</summary>
internal static partial class NewLevelBuildCases
{
    private static void RetailSupercharge(MainWindow window, string outDir)
    {
        string levelKey = System.Environment.GetEnvironmentVariable("RETAIL_SUPERCHARGE_LEVEL") ?? "artisans";
        Load(window, levelKey);
        SelectTab(window, "Terrain"); Pump(20);
        if (window.FreshSceneForTesting != null) throw new InvalidOperationException($"{levelKey} is a rebuilt level in this project; use an original one.");
        var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
        var catalog = (NativeTerrainSurfaceLevelCatalog)Invoke(window, "TileTypeCatalog", new object?[] { null })!;
        var supercharge = new NativeTerrainSurfaceSignature(4, 0, 0);
        bool own = catalog.Source.SpecialSurfaces.Any(r => r.Type == 4);
        Console.WriteLine($"{catalog.Level.DisplayName}: own kinds [{string.Join(", ", catalog.Source.SpecialSurfaces.Select(r => $"{r.Type},{r.Param1},{r.Param2}"))}], " +
            $"{catalog.Source.FlagPromotionCapacity.VerifiedZeroTailBytes} spare bytes, supercharge {(own ? "its own" : "copied in")}");

        var objects = window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Where(m => !m.IsRemoved).ToList();
        bool Clear(TerrainPolygon p) => objects.All(m =>
            Math.Abs(m.Position.X - p.Points.Average(v => v.X)) > 160 || Math.Abs(m.Position.Y - p.Points.Average(v => v.Y)) > 160 ||
            Math.Abs(m.Position.Z - p.ZValues.Average()) > 300);
        // Downhill direction (degrees, 0 = +x) and slope of a tile's plane.
        static (double Heading, double Slope) Fall(TerrainPolygon p)
        {
            var a = (X: (double)p.OriginalPoints[0].X, Y: (double)p.OriginalPoints[0].Y, Z: (double)p.OriginalZValues[0]);
            var b = (X: (double)p.OriginalPoints[1].X, Y: (double)p.OriginalPoints[1].Y, Z: (double)p.OriginalZValues[1]);
            var c = (X: (double)p.OriginalPoints[2].X, Y: (double)p.OriginalPoints[2].Y, Z: (double)p.OriginalZValues[2]);
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            if (nz < 0) { nx = -nx; ny = -ny; nz = -nz; }
            double flat = Math.Sqrt(nx * nx + ny * ny);
            return (Math.Atan2(ny, nx) * 180 / Math.PI, Math.Atan2(flat, nz) * 180 / Math.PI);
        }
        string Corner(TerrainPolygon p, int i) => $"{MathF.Round(p.OriginalPoints[i].X)},{MathF.Round(p.OriginalPoints[i].Y)},{MathF.Round(p.OriginalZValues[i])}";
        List<TerrainPolygon> walkable = geometry.Polygons
            .Where(p => !p.IsTerrainRemoved && !p.IsTerrainEdited && p.Points.Count is 3 or 4 && Clear(p) &&
                        string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                        catalog.FindFace(p.RuntimeKey) is { HasExactTriangleMapping: true, NativeTriangles.Count: > 0, Signature.IsOrdinary: true } &&
                        Fall(p).Slope is >= 6 and <= 30)
            .ToList();
        Dictionary<string, List<TerrainPolygon>> byEdge = new(StringComparer.Ordinal);
        foreach (var p in walkable)
            for (int i = 0; i < p.OriginalPoints.Count; i++)
            {
                string a = Corner(p, i), b = Corner(p, (i + 1) % p.OriginalPoints.Count);
                string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
                (byEdge.TryGetValue(key, out var sharing) ? sharing : byEdge[key] = []).Add(p);
            }
        IEnumerable<TerrainPolygon> Neighbours(TerrainPolygon p) => Enumerable.Range(0, p.OriginalPoints.Count).SelectMany(i =>
        {
            string a = Corner(p, i), b = Corner(p, (i + 1) % p.OriginalPoints.Count);
            return byEdge[string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a];
        }).Where(q => q != p).Distinct();
        static double Turn(double a, double b) => Math.Abs((a - b + 540) % 360 - 180);
        // The longest straight run: from each tile, step to the lower neighbour that keeps the heading (within 35° of the
        // run's first tile), and keep the run that falls furthest.
        List<TerrainPolygon> best = [];
        double bestDrop = 0;
        foreach (var top in walkable)
        {
            double heading = Fall(top).Heading;
            List<TerrainPolygon> run = [top];
            while (run.Count < 12)
            {
                var here = run[^1];
                var next = Neighbours(here).Where(q => !run.Contains(q) && q.ZValues.Average() < here.ZValues.Average() - 4 &&
                        Turn(Fall(q).Heading, heading) < 35 &&
                        Turn(Math.Atan2(q.Points.Average(v => v.Y) - here.Points.Average(v => v.Y), q.Points.Average(v => v.X) - here.Points.Average(v => v.X)) * 180 / Math.PI, heading) < 50)
                    .OrderBy(q => q.ZValues.Average()).FirstOrDefault();
                if (next == null) break;
                run.Add(next);
            }
            double drop = run[0].OriginalZValues.Take(run[0].OriginalPoints.Count).Max() - run[^1].OriginalZValues.Take(run[^1].OriginalPoints.Count).Min();
            if (drop > bestDrop) { bestDrop = drop; best = run; }
        }
        if (best.Count < 2 || bestDrop < 150) throw new InvalidOperationException($"No downhill run of walkable tiles falls 150 in {levelKey} (best {bestDrop:0}).");
        double runHeading = Fall(best[0]).Heading;
        // Widen the run by the tiles beside it that fall the same way, so steering does not leave the supercharge ground.
        List<TerrainPolygon> tiles = [.. best];
        foreach (var p in best)
            foreach (var q in Neighbours(p))
                if (!tiles.Contains(q) && Turn(Fall(q).Heading, runHeading) < 35)
                    tiles.Add(q);
        Console.WriteLine($"run of {best.Count} tiles falling {bestDrop:0} heading {runHeading:0}°, {tiles.Count} tiles with the sides:");
        foreach (var p in best)
            Console.WriteLine($"  {p.RuntimeKey} at {p.Points.Average(v => v.X):0},{p.Points.Average(v => v.Y):0} z {p.ZValues.Average():0} slope {Fall(p).Slope:0}° heading {Fall(p).Heading:0}°");

        // The dialog offers supercharge in this level.
        SetField(window, "_selectedTerrain", best[0]);
        SetField(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(best[0]));
        Invoke(window, "RefreshReleaseTerrainPanelTexts"); Pump(5);
        Click(Find<Button>(window, "TerrainTileType"));
        Wait(() => window.OwnedWindows.Any(w => w.Name == "TileTypeDialog"), "Tile Type dialog", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "TileTypeDialog");
        var list = Find<ListBox>(dialog, "TileTypeList");
        var offered = list.Items.OfType<ListBoxItem>().Select(i => i.Content?.ToString() ?? "").ToList();
        Console.WriteLine("Tile Type… offers: " + string.Join(" | ", offered));
        if (!offered.Contains("Supercharge ramp")) throw new InvalidOperationException("Tile Type… does not offer supercharge in " + levelKey);
        list.SelectedItem = list.Items.OfType<ListBoxItem>().First(i => i.Content?.ToString() == "Supercharge ramp"); Pump(5);
        Snapshot(dialog, Path.Combine(outDir, "supercharge-tile-type-dialog.png"));
        Get<TextBlock>(window, "_statusText").Text = "";
        Click(Find<Button>(dialog, "TileTypeApply"));
        Wait(() => !window.OwnedWindows.Any(w => w.Name == "TileTypeDialog"), "Tile Type dialog closed", 60);
        Wait(() => Status(window).StartsWith("Changed", StringComparison.Ordinal) || Status(window).StartsWith("No tile", StringComparison.Ordinal) ||
            Status(window).StartsWith("Could not", StringComparison.Ordinal), "Tile Type applied", 120);
        Console.WriteLine("Tile Type… supercharge on the top tile: " + Status(window));
        // The rest of the run in one go, as a ⌘-click selection would.
        var apply = (Task)Invoke(window, "ApplyTileTypeAsync", tiles.Skip(1).ToList(), supercharge, catalog)!;
        Wait(() => apply.IsCompleted, "supercharge on the run", 300);
        Console.WriteLine("Tile Type… supercharge on the run: " + Status(window));
        var changed = tiles.Where(t => t.SurfaceBehaviorEdit is { SurfaceType: 4 }).ToList();
        if (!best.All(t => t.SurfaceBehaviorEdit is { SurfaceType: 4 }))
            throw new InvalidOperationException("Not every tile of the run took supercharge: " + Status(window));
        if (!own && !Status(window).Contains("Create BIN also gives " + catalog.Level.DisplayName + " Spyro's supercharge animations", StringComparison.Ordinal))
            throw new InvalidOperationException("The status does not say Create BIN adds Spyro's supercharge animations: " + Status(window));
        Save(window);

        var spec = new
        {
            level = levelKey,
            levelHadSupercharge = own,
            heading = runHeading,
            drop = bestDrop,
            top = new { x = best[0].Points.Average(v => v.X), y = best[0].Points.Average(v => v.Y), z = best[0].OriginalZValues.Take(best[0].OriginalPoints.Count).Max() },
            bottom = new { x = best[^1].Points.Average(v => v.X), y = best[^1].Points.Average(v => v.Y), z = best[^1].OriginalZValues.Take(best[^1].OriginalPoints.Count).Min() },
            run = best.Select(t => new { tile = t.RuntimeKey, x = t.Points.Average(v => v.X), y = t.Points.Average(v => v.Y), z = t.ZValues.Average(), slope = Fall(t).Slope }),
            tiles = changed.Select(t => t.RuntimeKey)
        };
        File.WriteAllText(Path.Combine(outDir, "retail-supercharge.json"), JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true }));

        // A level without room for the animations refuses the kind at once and saves nothing.
        string tightKey = System.Environment.GetEnvironmentVariable("RETAIL_SUPERCHARGE_TIGHT") ?? "darkpassage";
        if (tightKey != "none")
        {
            Load(window, tightKey); Pump(20);
            var tightGeometry = Get<GeometryCandidate>(window, "_currentGeometry");
            var tightCatalog = (NativeTerrainSurfaceLevelCatalog)Invoke(window, "TileTypeCatalog", new object?[] { null })!;
            TerrainPolygon tile = tightGeometry.Polygons.First(p => !p.IsTerrainRemoved && p.Points.Count is 3 or 4 &&
                string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                tightCatalog.FindFace(p.RuntimeKey) is { HasExactTriangleMapping: true, NativeTriangles.Count: > 0, Signature.IsOrdinary: true });
            var refuse = (Task)Invoke(window, "ApplyTileTypeAsync", new List<TerrainPolygon> { tile }, supercharge, tightCatalog)!;
            Wait(() => refuse.IsCompleted, "supercharge in " + tightKey, 300);
            Console.WriteLine($"Tile Type… supercharge in {tightCatalog.Level.DisplayName}: {Status(window)}");
            if (!Status(window).StartsWith("No tile was changed: a supercharge ramp needs Spyro's supercharge animations", StringComparison.Ordinal) ||
                !Status(window).Contains("bytes short", StringComparison.Ordinal) || tile.HasSurfaceBehaviorEdit)
                throw new InvalidOperationException($"{tightCatalog.Level.DisplayName} did not refuse supercharge plainly: " + Status(window));
            string tightEdits = Path.Combine(Workspace(window).RootPath, $"{tightKey}-terrain-edits.json");
            if (File.Exists(tightEdits) && File.ReadAllText(tightEdits).Contains("\"nativeSurfaceType\": 4", StringComparison.Ordinal))
                throw new InvalidOperationException($"{tightCatalog.Level.DisplayName}'s refused supercharge tile was saved.");
        }
        Console.WriteLine($"NEW_LEVEL_RETAIL_SUPERCHARGE_OK tiles={changed.Count} run={best.Count} drop={bestDrop:0}");
    }
}
