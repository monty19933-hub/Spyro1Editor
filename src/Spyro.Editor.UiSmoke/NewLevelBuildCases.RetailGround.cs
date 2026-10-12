using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

/// <summary>The Ground card in one of the game's own levels (RETAIL_GROUND_LEVEL, default Artisans), as a user runs it:
/// a selected tile made lava with the Lava button, a second lava tile and a water tile and a grass tile clicked in the
/// view while painting, a goo tile taken back with Undo Terrain, Esc, Save, and the saved edits read back (texture
/// paint, kinds, the lava record copied in from another level). Tile Type… must offer every kind and give a tile ooze,
/// which the level never had. Writes
/// ground-tiles.json (the painted tiles, for the export check and the emulator) and pictures to the out folder.</summary>
internal static partial class NewLevelBuildCases
{
    private sealed record GroundTile(string Ground, string RuntimeKey, int OriginalTextureId, float X, float Y, float Z,
        float[][] Corners, int[] Kind, string[][] TriangleKeys);

    private static void RetailGround(MainWindow window, string outDir)
    {
        string levelKey = System.Environment.GetEnvironmentVariable("RETAIL_GROUND_LEVEL") ?? "artisans";
        Load(window, levelKey);
        SelectTab(window, "Terrain"); Pump(20);
        if (window.FreshSceneForTesting != null) throw new InvalidOperationException($"{levelKey} is a rebuilt level in this project; use an original one.");
        var card = Find<Border>(window, "TerrainGroundCard");
        Invoke(window, "RefreshReleaseTerrainPanelTexts"); Pump(10);
        Wait(() => card.IsVisible, "the Ground card in an original level", 30);
        Console.WriteLine("Ground card shown in " + levelKey + ": " + Find<TextBlock>(window, "GroundNote").Text);
        Pump(100);
        Snapshot(window, Path.Combine(outDir, "01-original-level-ground-card.png"));

        var geometry = Get<GeometryCandidate>(window, "_currentGeometry");
        var catalog = (NativeTerrainSurfaceLevelCatalog)Invoke(window, "TileTypeCatalog", new object?[] { null })!;
        Console.WriteLine($"{catalog.Level.DisplayName}: own kinds [{string.Join(", ", catalog.Source.SpecialSurfaces.Select(r => $"{r.Type},{r.Param1},{r.Param2}"))}], " +
            $"{catalog.Source.FlagPromotionCapacity.VerifiedZeroTailBytes} spare bytes");

        // Tiles: flat high-detail quads Spyro walks on (one collision triangle per drawn triangle, solid ground), near
        // the level's start and well apart, so the emulator can drop Spyro on each.
        var start = StartOf(levelKey);
        // No object on or next to the tile (a dragon's pedestal, a tree: Spyro dropped there would meet it first).
        var objects = window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Where(m => !m.IsRemoved).ToList();
        bool Clear(TerrainPolygon p) => objects.All(m =>
            Math.Abs(m.Position.X - p.Points.Average(v => v.X)) > 220 || Math.Abs(m.Position.Y - p.Points.Average(v => v.Y)) > 220 ||
            Math.Abs(m.Position.Z - p.ZValues.Average()) > 300);
        List<TerrainPolygon> candidates = geometry.Polygons
            .Where(p => !p.IsTerrainRemoved && !p.IsTerrainEdited && p.Points.Count == 4 && p.TextureId >= 0 && Clear(p) &&
                        string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                        p.ZValues.Max() - p.ZValues.Min() < 3 && Span(p) >= 120 &&
                        catalog.FindFace(p.RuntimeKey) is { HasExactTriangleMapping: true, NativeTriangles.Count: > 0, Signature.IsOrdinary: true })
            .OrderBy(p => Dist(p, start.X, start.Y))
            .ToList();
        List<TerrainPolygon> chosen = [];
        foreach (TerrainPolygon p in candidates)
        {
            if (chosen.All(c => Dist(p, c.Points.Average(v => v.X), c.Points.Average(v => v.Y)) > 400))
                chosen.Add(p);
            if (chosen.Count == 6) break;
        }
        if (chosen.Count < 6) throw new InvalidOperationException($"Only {chosen.Count} flat walkable tiles were found.");
        TerrainPolygon lavaTile = chosen[0], lava2Tile = chosen[1], waterTile = chosen[2], grassTile = chosen.Skip(3).First(p => p.TextureId != GroundTypes.Grass.TextureId), gooTile = chosen[4];
        if (grassTile == gooTile) gooTile = chosen[3];
        TerrainPolygon oozeTile = chosen.Skip(3).First(p => p != grassTile && p != gooTile);
        foreach (var (g, t) in new[] { ("lava", lavaTile), ("lava2", lava2Tile), ("water", waterTile), ("grass", grassTile), ("goo", gooTile) })
            Console.WriteLine($"  {g} tile {t.RuntimeKey} texture {t.TextureId} at {t.Points.Average(v => v.X):0},{t.Points.Average(v => v.Y):0} z {t.ZValues.Average():0}");

        List<string> scopeQuestions = [];
        window.GroundScopeAnswerForTesting = (count, canSelected) =>
        {
            scopeQuestions.Add($"{count} tiles share it; one-tile slot {(canSelected ? "free" : "not free")}");
            return canSelected ? MainWindow.TerrainTexturePaintScopeChoice.SelectedSection : MainWindow.TerrainTexturePaintScopeChoice.AllLinkedSections;
        };

        // 1. A selected tile and the Lava button.
        SetField(window, "_selectedTerrain", lavaTile);
        SetField(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(lavaTile));
        Click(Find<Button>(window, "GroundLava"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Lava" && !Get<bool>(window, "_groundPaintBusy"), "painting lava", 600);
        Pump(20);
        Console.WriteLine("Lava button: " + Status(window));

        // 2. Tiles clicked in the view while painting.
        PaintClick(window, lava2Tile, "lava again");
        Click(Find<Button>(window, "GroundWater"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Water", "painting water", 60);
        PaintClick(window, waterTile, "water");
        Click(Find<Button>(window, "GroundGrass"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Grass", "painting grass", 60);
        PaintClick(window, grassTile, "grass");
        // A tile painted again: water first, then goo over it (its earlier paint comes off first).
        Click(Find<Button>(window, "GroundWater"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Water", "painting water again", 60);
        PaintClick(window, gooTile, "water on the goo tile");
        Click(Find<Button>(window, "GroundGoo"));
        Wait(() => window.ActiveGroundForTesting?.Name == "Goo", "painting goo", 60);
        PaintClick(window, gooTile, "goo over water");
        Pump(40);
        Snapshot(window, Path.Combine(outDir, "02-painting.png"));
        Invoke(window, "StopGroundPainting", true); Pump(10);
        if (window.ActiveGroundForTesting != null) throw new InvalidOperationException("Stop Painting left a ground chosen.");
        Console.WriteLine("scope questions: " + (scopeQuestions.Count == 0 ? "none" : string.Join("; ", scopeQuestions)));

        void Expect(TerrainPolygon t, GroundType g, string label)
        {
            var kind = g.Kind is { } k ? new NativeTerrainSurfaceSignature(k.Type, k.Param1, k.Param2) : NativeTerrainSurfaceSignature.Ordinary;
            var staged = t.SurfaceBehaviorEdit;
            var now = staged == null ? catalog.FindFace(t.RuntimeKey)?.Signature : new NativeTerrainSurfaceSignature(staged.SurfaceType, staged.Param1, staged.Param2);
            bool looks = (bool)Invoke(window, "GroundTextureShown", t, g)!;
            Console.WriteLine($"  {label}: texture {t.OriginalTextureId}->{t.TextureId} shows {g.Name}={looks}, kind {now?.Label} (donor {staged?.DescriptorDonorLevelKey}#{staged?.DescriptorDonorIndex})");
            if (!looks) throw new InvalidOperationException($"The {label} tile does not show the {g.Name} texture.");
            if (now != kind) throw new InvalidOperationException($"The {label} tile is {now?.Label}, not {kind.Label}.");
        }
        Expect(lavaTile, GroundTypes.Lava, "lava");
        Expect(lava2Tile, GroundTypes.Lava, "second lava");
        Expect(waterTile, GroundTypes.Water, "water");
        Expect(grassTile, GroundTypes.Grass, "grass");
        Expect(gooTile, GroundTypes.Goo, "goo");
        bool levelHasLava = catalog.Source.SpecialSurfaces.Any(r => r.Type == 0 && r.Param1 == 0 && r.Param2 == 1);
        if (!levelHasLava && !(lavaTile.SurfaceBehaviorEdit?.ImportsDescriptor ?? false))
            throw new InvalidOperationException("The lava tile does not name the record to copy in.");

        // 3. Undo Terrain puts the goo tile back.
        SetField(window, "_selectedTerrain", gooTile);
        SetField(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(gooTile));
        var undo = (Task)Invoke(window, "UndoSelectedTerrainAsync")!;
        Wait(() => undo.IsCompleted, "Undo Terrain", 300);
        Console.WriteLine("Undo Terrain on the goo tile: " + Status(window));
        if (gooTile.TextureId != gooTile.OriginalTextureId || gooTile.HasSurfaceBehaviorEdit || gooTile.HasTextureEdit)
            throw new InvalidOperationException("Undo Terrain did not put the goo tile back.");

        // 4. Tile Type… offers every kind in an original level, and gives a tile one the level never had (ooze).
        var choices = ((System.Collections.IEnumerable)Invoke(window, "RetailTileTypeChoices", catalog)!).Cast<NativeTerrainSurfaceSignature>().ToList();
        Console.WriteLine("Tile Type… choices: " + string.Join(" | ", choices.Select(c => MainWindow.PlainTileTypeName(c))));
        foreach (var kind in NativeTerrainSurfaceKindImport.ImportableKinds)
            if (!choices.Contains(kind)) throw new InvalidOperationException($"Tile Type… does not offer {kind.Label}.");
        SetField(window, "_selectedTerrain", oozeTile);
        SetField(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(oozeTile));
        Invoke(window, "RefreshReleaseTerrainPanelTexts"); Pump(5);
        var tileTypeButton = Find<Button>(window, "TerrainTileType");
        Click(tileTypeButton);
        Wait(() => window.OwnedWindows.Any(w => w.Name == "TileTypeDialog"), "Tile Type dialog", 60);
        var dialog = window.OwnedWindows.First(w => w.Name == "TileTypeDialog");
        var list = Find<ListBox>(dialog, "TileTypeList");
        list.SelectedItem = list.Items.OfType<ListBoxItem>().First(i => (i.Content?.ToString() ?? "").StartsWith("Ooze (Spyro sinks", StringComparison.Ordinal)); Pump(5);
        Snapshot(dialog, Path.Combine(outDir, "04-tile-type-dialog.png"));
        Click(Find<Button>(dialog, "TileTypeApply"));
        Wait(() => !window.OwnedWindows.Any(w => w.Name == "TileTypeDialog") && Status(window).StartsWith("Changed one tile", StringComparison.Ordinal), "Tile Type applied", 120);
        Console.WriteLine("Tile Type… ooze: " + Status(window));
        if (oozeTile.SurfaceBehaviorEdit is not { SurfaceType: 0, Param2: 2, ImportsDescriptor: true })
            throw new InvalidOperationException("Tile Type… did not give the tile ooze copied in from another level.");

        Save(window);
        var viewport = Get<EditorViewport>(window, "_viewport");
        foreach (var (g, t) in new[] { ("lava", lavaTile), ("water", waterTile), ("grass", grassTile) })
        {
            viewport.FocusTerrain(geometry.Polygons.IndexOf(t)); Pump(60);
            Snapshot(window, Path.Combine(outDir, $"03-map-{g}-tile.png"));
        }
        viewport.SetViewMode(ViewportViewMode.Fly3D); Pump(10);

        // 5. The saved files.
        string root = Workspace(window).RootPath;
        using var edits = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, $"{levelKey}-terrain-edits.json")));
        JsonElement Saved(TerrainPolygon t) => edits.RootElement.GetProperty("edits").EnumerateArray().First(e => e.GetProperty("runtimeKey").GetString() == t.RuntimeKey);
        foreach (var (t, g) in new[] { (lavaTile, GroundTypes.Lava), (lava2Tile, GroundTypes.Lava), (waterTile, GroundTypes.Water), (oozeTile, GroundTypes.Goo) })
        {
            var e = Saved(t);
            string donor = e.TryGetProperty("nativeSurfaceDescriptorDonorLevelKey", out var d) ? d.GetString() ?? "" : "";
            Console.WriteLine($"  saved {g.Name} {t.RuntimeKey}: kind {e.GetProperty("nativeSurfaceType")},{e.GetProperty("nativeSurfaceParam1")},{e.GetProperty("nativeSurfaceParam2")}" +
                $"{(e.TryGetProperty("textureIdEdited", out var te) ? $" texture {e.GetProperty("textureIdOriginal")}->{te}" : " (look unchanged)")}{(donor == "" ? "" : $", record from {donor}")}");
            if (e.GetProperty("nativeSurfaceParam2").GetInt32() != g.Kind!.Param2 || e.GetProperty("nativeSurfaceType").GetInt32() != 0)
                throw new InvalidOperationException($"The saved {g.Name} tile has the wrong kind.");
        }
        var grassSaved = Saved(grassTile);
        Console.WriteLine($"  saved grass {grassTile.RuntimeKey}: texture {grassSaved.GetProperty("textureIdOriginal")}->{grassSaved.GetProperty("textureIdEdited")}, kind edit {grassSaved.TryGetProperty("nativeSurfaceBehaviorEdit", out _)}");
        if (edits.RootElement.GetProperty("edits").EnumerateArray().Any(e => e.GetProperty("runtimeKey").GetString() == gooTile.RuntimeKey && e.TryGetProperty("nativeSurfaceBehaviorEdit", out _)))
            throw new InvalidOperationException("The undone goo tile was saved with a kind.");
        string relocations = Path.Combine(root, $"{levelKey}-native-terrain-texture-relocations.json");
        if (File.Exists(relocations))
            foreach (var r in JsonDocument.Parse(File.ReadAllText(relocations)).RootElement.GetProperty("relocations").EnumerateArray())
                Console.WriteLine($"  texture slot T{r.GetProperty("targetTextureId")} <- {r.GetProperty("donorLevelKey")} {r.GetProperty("donorTextureId")}");

        GroundTile Spec(string g, TerrainPolygon t, GroundType ground)
        {
            var kind = ground.Kind is { } k ? new[] { k.Type, k.Param1, k.Param2 } : new[] { -1, 0, 0 };
            string[][] keys = NativeTerrainSurfaceCatalogBuilder.VisualTriangulations(t.OriginalPoints.Count)
                .Select(tri => tri.Select(c => NativeTerrainSurfaceCatalogBuilder.VisualTriangleKey(t, c[0], c[1], c[2])).ToArray()).ToArray();
            return new(g, t.RuntimeKey, t.OriginalTextureId, t.Points.Average(v => v.X), t.Points.Average(v => v.Y), t.ZValues.Average(),
                t.OriginalPoints.Select((v, i) => new[] { v.X, v.Y, t.OriginalZValues[i] }).ToArray(), kind, keys);
        }
        var spec = new
        {
            level = levelKey,
            levelHadLava = levelHasLava,
            specialSurfacesBefore = catalog.Source.SpecialSurfaces.Count,
            tiles = new[] { Spec("lava", lavaTile, GroundTypes.Lava), Spec("lava", lava2Tile, GroundTypes.Lava), Spec("water", waterTile, GroundTypes.Water), Spec("grass", grassTile, GroundTypes.Grass),
                Spec("ooze (Tile Type)", oozeTile, GroundTypes.Goo), Spec("undone-goo", gooTile, GroundTypes.Goo) }
        };
        File.WriteAllText(Path.Combine(outDir, "ground-tiles.json"), JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true }));
        window.GroundScopeAnswerForTesting = null;
        Console.WriteLine("NEW_LEVEL_RETAIL_GROUND_OK");
    }

    private static void PaintClick(MainWindow window, TerrainPolygon tile, string label)
    {
        var painted = (Task<bool>)Invoke(window, "PaintGroundFromViewportAsync", tile)!;
        Wait(() => painted.IsCompleted, "paint click " + label, 600);
        if (!painted.Result) throw new InvalidOperationException("The paint click was not taken by the Ground card.");
        Pump(10);
        Console.WriteLine($"click ({label}): {Status(window)}");
    }

    private static float Span(TerrainPolygon p) =>
        Math.Min(p.Points.Max(v => v.X) - p.Points.Min(v => v.X), p.Points.Max(v => v.Y) - p.Points.Min(v => v.Y));

    private static double Dist(TerrainPolygon p, double x, double y) =>
        Math.Sqrt(Math.Pow(p.Points.Average(v => v.X) - x, 2) + Math.Pow(p.Points.Average(v => v.Y) - y, 2));

    /// <summary>Where New Game puts Spyro (editor units), from the project's entry-pose cache.</summary>
    private static (double X, double Y) StartOf(string levelKey)
    {
        string path = Path.Combine(System.Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE")!, "editor-cache", "level-entry-poses.json");
        if (File.Exists(path))
            foreach (var l in JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("levels").EnumerateArray())
                if (l.GetProperty("levelKey").GetString() == levelKey)
                    return (l.GetProperty("rawX").GetInt32() / 16.0, l.GetProperty("rawY").GetInt32() / 16.0);
        return (0, 0);
    }
}
