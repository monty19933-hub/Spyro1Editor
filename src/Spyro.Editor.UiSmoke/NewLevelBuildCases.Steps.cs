using System.Reflection;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;

internal static partial class NewLevelBuildCases
{
    private static partial bool RunMoreSteps(MainWindow window, string outDir, string step)
    {
        switch (step)
        {
            case "textures": Textures(window, outDir); return true;
            case "portal-survey": PortalSurvey(window, outDir); return true;
            case "environment": EnvironmentStep(window, outDir); return true;
            case "settings": Settings(window, outDir); return true;
            case "look": Look(window, outDir); return true;
            case "arch-corners": ArchCorners(window); return true;
            case "portal-probe": PortalProbe(window); return true;
            case "lining": Lining(window); return true;
            case "portal-geom": PortalGeom(window); return true;
            case "plot": Plot(window, outDir); return true;
            case "portal-build": PortalBuild(window, outDir); return true;
            case "footprint": Footprint(window); return true;
            case "fill": FillHoles(window, outDir); return true;
            case "export": Export(window); return true;
            case "portal-reorient": PortalReorient(window, outDir); return true;
            case "portal-backside": PortalBackside(window); return true;
            case "objects": StonehillObjects(window); return true;
            case "fitmix": FitMix(window); return true;
            case "eggs-fix": EggsFix(window); return true;
            case "gems": Gems(window); return true;
            case "frost-probe": FrostProbe(window); return true;
            case "frost-probe2": FrostProbe2(window); return true;
            case "frost-update": FrostUpdate(window); return true;
            case "frost-snow": FrostSnowStep(window, outDir); return true;
            case "blank-trial": BlankTrial(window); return true;
            case "sky-build": SkyBuild(window); return true;
            case "sky-objects": SkyObjectsStep(window); return true;
            case "sky-update": SkyUpdateStep(window); return true;
            case "sky-update2": SkyUpdate2Step(window); return true;
            case "sky-update3": SkyUpdate3Step(window); return true;
            case "sky-picker": SkyPickerStep(window); return true;
            case "scene-sky": SceneSkyStep(window); return true;
            case "dialog-shots": DialogShots(window, outDir); return true;
            case "crash-disc": CrashDisc(window); return true;
            case "crash-textures": CrashTextures(window); return true;
            case "crash-objects": CrashObjects(window); return true;
            case "crash-enemies": CrashEnemies(window); return true;
            case "wumpa-build": WumpaBuild(window); return true;
            case "wumpa-objects": WumpaObjects(window); return true;
            case "wumpa-tiletype": WumpaTileType(window); return true;
            case "music": MusicStep(window, outDir); return true;
            case "yotd-disc": YearOfTheDragonDisc(window); return true;
            case "lostfleet": LostFleetBuild(window); return true;
            case "lostfleet-objects": LostFleetObjects(window); return true;
            case "lostfleet-sheep": LostFleetSheep(window); return true;
            case "add-objects": AddObjects(window); return true;
            case "lostfleet-fix": LostFleetFix(window); return true;
            case "lostfleet-holes": LostFleetHoles(window); return true;
            case "lostfleet-creatures": LostFleetCreatures(window); return true;
            case "retile": Retile(window); return true;
            case "edit-objects": EditObjects(window); return true;
            case "solidify": Solidify(window); return true;
            case "completion-check": CompletionCheck(window); return true;
            case "cloudspire-cannon": CloudspireCannon(window); return true;
            case "yotd-import": YearOfTheDragonImport(window); return true;
            case "yotd-menu": YearOfTheDragonMenu(window); return true;
            case "crash-menu": CrashMenu(window); return true;
            case "crash-import": CrashImport(window, outDir); return true;
            default: return false;
        }
    }

    /// <summary>Stone Hill texture → (donor level, donor texture). Every tile using the Stone Hill texture changes.</summary>
    private static readonly (int Target, string Donor, int DonorTexture)[] TextureMap =
    [
        (40, "icecavern", 10), (4, "icecavern", 34), (1, "icecavern", 2), (6, "icecavern", 3), (3, "highcaves", 27),
        (12, "icecavern", 4), (7, "highcaves", 36), (11, "highcaves", 37), (30, "highcaves", 31),
        (10, "icecavern", 30), (19, "icecavern", 30), (9, "icecavern", 30), (21, "icecavern", 27), (24, "icecavern", 27),
        (25, "icecavern", 27), (2, "icecavern", 28), (5, "icecavern", 38), (15, "icecavern", 38),
        (8, "icecavern", 17), (16, "icecavern", 18), (26, "icecavern", 37), (27, "icecavern", 18),
        (13, "highcaves", 13), (14, "highcaves", 86), (20, "highcaves", 21), (22, "highcaves", 71), (23, "highcaves", 77),
        (28, "icecavern", 47), (29, "icecavern", 24), (31, "icecavern", 6), (32, "icecavern", 5), (17, "icecavern", 38), (18, "icecavern", 38),
    ];

    private static void Textures(MainWindow window, string outDir)
    {
        Load(window, "stonehill");
        SelectTab(window, "Terrain");
        var geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        System.Collections.IEnumerable relocations() => (System.Collections.IEnumerable)typeof(MainWindow).GetField("_nativeTerrainTextureRelocations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        int done = 0;
        foreach (var (target, donor, donorTexture) in TextureMap)
        {
            bool already = relocations().Cast<object>().Any(r => (int)r.GetType().GetProperty("TargetTextureId")!.GetValue(r)! == target);
            if (already) { Console.WriteLine($"texture {target}: already replaced"); done++; continue; }
            TerrainPolygon? face = geometry.Polygons.FirstOrDefault(p => !p.IsTerrainRemoved && p.TextureId == target &&
                string.Equals(p.Detail, "hp", StringComparison.OrdinalIgnoreCase) && !p.HasTextureEdit && !p.HasTextureVisualEdit && !p.HasSurfaceBehaviorEdit);
            if (face == null) { Console.WriteLine($"texture {target}: no clean face"); continue; }
            SetField(window, "_selectedTerrain", face);
            SetField(window, "_selectedTerrainIndex", geometry.Polygons.IndexOf(face));
            SetField(window, "_selectedTerrainPointIndex", -1);
            var build = typeof(MainWindow).GetMethod("BuildCrossLevelTerrainLookChoices", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var choices = ((System.Collections.IEnumerable)build.Invoke(window, [face.Surface, null, donor, false, true])!).Cast<object>().ToArray();
            object? choice = choices.FirstOrDefault(c => (int)Prop(c, "TextureId")! == donorTexture);
            if (choice == null) { Console.WriteLine($"texture {target}: {donor} {donorTexture} not offered ({choices.Length} choices)"); continue; }
            if (!(bool)Prop(choice, "CanApplyAtomically")!) { Console.WriteLine($"texture {target}: {donor} {donorTexture} blocked: {Prop(choice, "AtomicBlockReason")}"); continue; }
            var apply = (Task)Invoke(window, "ApplySelectedTerrainCrossLevelLookAsync", choice, false)!;
            Wait(() => apply.IsCompleted, "apply texture " + target, 600);
            if (apply.IsFaulted) throw apply.Exception!;
            bool ok = relocations().Cast<object>().Any(r => (int)r.GetType().GetProperty("TargetTextureId")!.GetValue(r)! == target);
            Console.WriteLine($"texture {target} <- {donor} {donorTexture}: {(ok ? "replaced" : "NOT replaced")} — {Status(window)}");
            if (ok) done++;
            WaitIdle(window);
            geometry = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        }
        Save(window);
        Snapshot(window, Path.Combine(outDir, "textures.png"));
        Console.WriteLine($"NEW_LEVEL_TEXTURES_OK replaced={done}/{TextureMap.Length}");
    }

    private static void PortalSurvey(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        var snap = window.CaptureEditorShellSessionSnapshotForTesting();
        var g = snap.CurrentGeometry!;
        Moby trig = Mobys(window).Single(m => m.TrueIndex == 38);
        Moby sign = Mobys(window).Single(m => m.TrueIndex == 144);
        Moby sound = Mobys(window).Single(m => m.TrueIndex == 157);
        Console.WriteLine($"trigger {trig.Position} sign {sign.Position} sound {sound.Position}");
        // Faces within 700 units of the doorway, by height above the trigger.
        var near = g.Polygons.Select((p, i) => (p, i)).Where(t => !t.p.IsTerrainRemoved &&
            t.p.Points.Average(q => q.X) is var cx && t.p.Points.Average(q => q.Y) is var cy &&
            Math.Abs(cx - sign.Position.X) < 700 && Math.Abs(cy - sign.Position.Y) < 700).ToList();
        Console.WriteLine($"{near.Count} faces near the doorway");
        foreach (var (p, i) in near.OrderBy(t => t.p.ZValues.Average()))
            Console.WriteLine($"  {i} {p.RuntimeKey} tex {p.TextureId} {TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface)} centre {p.Points.Average(q => q.X):0},{p.Points.Average(q => q.Y):0} z {p.ZValues.Min():0}..{p.ZValues.Max():0}");
        File.WriteAllLines(Path.Combine(outDir, "artisans-faces.tsv"), g.Polygons.Select((p, i) =>
            $"{i}\t{p.RuntimeKey}\t{p.TextureId}\t{TerrainMaterialClassifier.NormalizeSurfaceName(p.Surface)}\t{p.Points.Average(q => q.X):0}\t{p.Points.Average(q => q.Y):0}\t{p.ZValues.Min():0}\t{p.ZValues.Max():0}\t{p.Points.Count}"));
        var viewport = Get<EditorViewport>(window, "_viewport");
        viewport.SetViewMode(ViewportViewMode.Map); Pump(20);
        viewport.FocusMobys([trig, sign, sound]); Pump(20);
        Snapshot(window, Path.Combine(outDir, "artisans-doorway-map.png"));
        viewport.SetViewMode(ViewportViewMode.Map); Pump(10);
        viewport.ResetView(); Pump(20);
        Snapshot(window, Path.Combine(outDir, "artisans-map.png"));
        Console.WriteLine("NEW_LEVEL_PORTAL_SURVEY_OK");
    }

    private static void EnvironmentStep(MainWindow window, string outDir)
    {
        Load(window, "stonehill");
        SelectTab(window, "Environment");
        var mode = Get<Avalonia.Controls.ComboBox>(window, "_skyboxModeBox");
        mode.SelectedItem = mode.Items.Cast<object>().First(o => o.ToString()!.Contains("another level", StringComparison.Ordinal)); Pump(20);
        var donor = Get<Avalonia.Controls.ComboBox>(window, "_skyboxDonorBox");
        string donorKey = System.Environment.GetEnvironmentVariable("NEW_LEVEL_SKY_DONOR") ?? "icecavern";
        donor.SelectedItem = donor.Items.Cast<Spyro.Editor.Core.Levels.LevelDefinition>().First(l => l.Key == donorKey); Pump(20);
        var match = (Task)Invoke(window, "MatchLevelTerrainPaletteAsync")!;
        Wait(() => match.IsCompleted, "match terrain palette", 900);
        Console.WriteLine("match: " + Status(window));
        Console.WriteLine("details: " + Get<Avalonia.Controls.TextBlock>(window, "_skyboxEnvironmentDetails").Text);
        Snapshot(window, Path.Combine(outDir, "environment-preview.png"));
        var save = (Task<bool>)Invoke(window, "SaveSkyboxPlanAsync")!;
        Wait(() => save.IsCompleted, "apply sky", 900);
        Console.WriteLine("apply sky: " + save.Result + " — " + Status(window));
        Snapshot(window, Path.Combine(outDir, "environment.png"));
        Console.WriteLine("NEW_LEVEL_ENVIRONMENT_OK");
    }

    // Frostbite round 3: snowy trees, set the way a user would (Environment tab, tick the box, Apply Sky).
    private static void FrostSnowStep(MainWindow window, string outDir)
    {
        Load(window, "stonehill");
        SelectTab(window, "Environment");
        var snow = Get<Avalonia.Controls.CheckBox>(window, "_skyboxEnvironmentSnowBox");
        Console.WriteLine($"snow box enabled {snow.IsEnabled}, checked {snow.IsChecked}");
        snow.IsChecked = true; Pump(10);
        var save = (Task<bool>)Invoke(window, "SaveSkyboxPlanAsync")!;
        Wait(() => save.IsCompleted, "apply sky", 900);
        Console.WriteLine("apply sky: " + save.Result + " - " + Status(window));
        Snapshot(window, Path.Combine(outDir, "environment-snow.png"));
        Console.WriteLine("NEW_LEVEL_FROSTSNOW_OK");
    }

    /// <summary>NEW_LEVEL_MUSIC_LEVEL gets NEW_LEVEL_MUSIC (a picker entry's start), chosen and saved the way a user would.</summary>
    private static void MusicStep(MainWindow window, string outDir)
    {
        Load(window, System.Environment.GetEnvironmentVariable("NEW_LEVEL_MUSIC_LEVEL") ?? "darkhollow");
        SelectTab(window, "Game Settings");
        string track = System.Environment.GetEnvironmentVariable("NEW_LEVEL_MUSIC") ?? "Toad Village";
        var music = Get<Avalonia.Controls.ComboBox>(window, "_levelMusicTrackBox");
        Console.WriteLine("music choices: " + music.Items.Count + ", last: " + music.Items.Cast<object>().Last());
        music.SelectedItem = music.Items.Cast<object>().First(o => o.ToString()!.StartsWith(track, StringComparison.Ordinal)); Pump(10);
        Console.WriteLine("details: " + Get<Avalonia.Controls.TextBlock>(window, "_levelMusicDetails").Text?.Replace('\n', ' '));
        var save = (Task)Invoke(window, "SaveLevelMusicPlanAsync")!;
        Wait(() => save.IsCompleted, "save music", 300);
        Console.WriteLine("save music: " + Status(window));
        Snapshot(window, Path.Combine(outDir, "music.png"));
        Console.WriteLine("NEW_LEVEL_MUSIC_OK");
    }

    private static void Settings(MainWindow window, string outDir)
    {
        Load(window, System.Environment.GetEnvironmentVariable("NEW_LEVEL_SETTINGS_LEVEL") ?? "stonehill");
        SelectTab(window, "Game Settings");
        string name = System.Environment.GetEnvironmentVariable("NEW_LEVEL_NAME") ?? "FROSTBITE FIELDS";
        var target = Get<Avalonia.Controls.ComboBox>(window, "_levelTextTargetBox");
        Console.WriteLine("name targets: " + string.Join(" | ", target.Items.Cast<object>()));
        string targetName = System.Environment.GetEnvironmentVariable("NEW_LEVEL_TEXT_TARGET") ?? "Stone Hill";
        var stoneTarget = target.Items.Cast<object>().First(o => o.ToString()!.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        target.SelectedItem = stoneTarget; Pump(20);
        Console.WriteLine("name target: " + stoneTarget + " — " + Get<Avalonia.Controls.TextBlock>(window, "_levelTextDetails").Text?.Replace('\n', ' '));
        Get<Avalonia.Controls.TextBox>(window, "_levelTextReplacementBox").Text = name; Pump(10);
        var saveName = (Task)Invoke(window, "SaveLevelTextPlanAsync")!;
        Wait(() => saveName.IsCompleted, "apply name", 300);
        Console.WriteLine("apply name: " + Status(window));
        string track = System.Environment.GetEnvironmentVariable("NEW_LEVEL_MUSIC") ?? "Icy Flight";
        if (track == "keep") { Snapshot(window, Path.Combine(outDir, "settings.png")); Console.WriteLine("NEW_LEVEL_SETTINGS_OK"); return; }
        var music = Get<Avalonia.Controls.ComboBox>(window, "_levelMusicTrackBox");
        music.SelectedItem = music.Items.Cast<object>().First(o => o.ToString()!.StartsWith(track, StringComparison.Ordinal)); Pump(10);
        var saveMusic = (Task)Invoke(window, "SaveLevelMusicPlanAsync")!;
        Wait(() => saveMusic.IsCompleted, "apply music", 300);
        Console.WriteLine("apply music: " + Status(window));
        Snapshot(window, Path.Combine(outDir, "settings.png"));
        Console.WriteLine("NEW_LEVEL_SETTINGS_OK");
    }

    /// <summary>NEW_LEVEL_LOOK_LEVEL (default artisans) and NEW_LEVEL_LOOK="x,y;x,y": map and camera views at each point.</summary>
    private static void Look(MainWindow window, string outDir)
    {
        Load(window, System.Environment.GetEnvironmentVariable("NEW_LEVEL_LOOK_LEVEL") ?? "artisans");
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var viewport = Get<EditorViewport>(window, "_viewport");
        int n = 0;
        foreach (string pt in (System.Environment.GetEnvironmentVariable("NEW_LEVEL_LOOK") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            float[] xy = pt.Split(',').Select(float.Parse).ToArray();
            int idx = Enumerable.Range(0, g.Polygons.Count).OrderBy(i => Math.Abs(g.Polygons[i].Points.Average(q => q.X) - xy[0]) + Math.Abs(g.Polygons[i].Points.Average(q => q.Y) - xy[1])).First();
            viewport.SetViewMode(ViewportViewMode.Map); Pump(10);
            viewport.FocusTerrain(idx); Pump(20);
            Snapshot(window, Path.Combine(outDir, $"look{n}-map.png"));
            viewport.SetViewMode(ViewportViewMode.Fly3D); Pump(10);
            viewport.FocusTerrain(idx); Pump(20);
            Snapshot(window, Path.Combine(outDir, $"look{n}-camera.png"));
            n++;
        }
        Console.WriteLine("NEW_LEVEL_LOOK_OK");
    }

    internal static readonly int[] ArchFaces =
    [194, 199, 185, 187, 190, 208, 211, 214, 220, 233, 202, 229, 186, 188, 189, 209, 212, 213, 195, 198, 201, 221, 228, 232,
     184, 210, 193, 215, 218, 219, 200, 227, 230, 231, 216, 217, 196, 222, 197, 223, 224, 225, 226];

    private static void ArchCorners(MainWindow window)
    {
        Load(window, "artisans");
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        var arch = ArchFaces.Select(i => g.Polygons[i]).ToHashSet();
        string Key(TerrainPolygon p, int i) => $"{MathF.Round(p.OriginalPoints[i].X)},{MathF.Round(p.OriginalPoints[i].Y)},{MathF.Round(p.OriginalZValues[i])}";
        var archCorners = arch.SelectMany(p => Enumerable.Range(0, p.Points.Count).Select(i => Key(p, i))).ToHashSet();
        var shared = g.Polygons.Where(p => !arch.Contains(p)).SelectMany(p => Enumerable.Range(0, p.Points.Count).Select(i => (p, k: Key(p, i)))).Where(t => archCorners.Contains(t.k)).ToList();
        Console.WriteLine($"arch faces {arch.Count}, corners {archCorners.Count}; other faces sharing a corner: {shared.Select(t => t.p).Distinct().Count()} ({string.Join(", ", shared.Select(t => t.p.RuntimeKey + " tex" + t.p.TextureId).Distinct().Take(20))})");
        Console.WriteLine("sectors: " + string.Join(", ", arch.Select(p => p.SectorIndex).Distinct()));
        Console.WriteLine("NEW_LEVEL_ARCH_OK");
    }

    private static void PortalProbe(MainWindow window)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        int[] stone = [3, 4, 5, 6, 10, 17];
        var viewport = Get<EditorViewport>(window, "_viewport");
        void Report(string label, IEnumerable<int> faces)
        {
            int ready = 0, removable = 0, n = 0;
            var bySector = new Dictionary<int, (int avail, int need)>();
            foreach (int i in faces)
            {
                n++;
                var p = g.Polygons[i];
                object r = Invoke(window, "GetTerrainAddCopyFaceReadiness", p)!;
                bool can = (bool)Prop(r, "CanAddIndependentVisibleTerrain")!;
                int avail = (int)Prop(r, "AvailableSlackBytes")!, need = (int)Prop(r, "RequiredSlackBytes")!;
                if (can) ready++; else Console.WriteLine($"   not copyable: {i} {p.RuntimeKey} tex{p.TextureId} z{p.MinZ:0}-{p.MaxZ:0} coll {Prop(r, "MatchedCollisionTriangles")}/{Prop(r, "TotalCollisionTriangles")}");
                if ((bool)Invoke(window, "CanStageTerrainRemoval", p)!) removable++;
                var cur = bySector.GetValueOrDefault(p.SectorOffset);
                bySector[p.SectorOffset] = (avail, cur.need + need);
            }
            Console.WriteLine($"{label}: {n} faces, add-copy ready {ready}, removable {removable}; sectors: " +
                string.Join("; ", bySector.Select(kv => $"0x{kv.Key:X} slack {kv.Value.avail} need-all {kv.Value.need}")));
        }
        IEnumerable<int> Cluster(float x0, float x1, float y0, float y1) => Enumerable.Range(0, g.Polygons.Count).Where(i =>
        {
            var p = g.Polygons[i];
            return stone.Contains(p.TextureId) && p.MaxZ > 440 && (p.Bounds.Left + p.Bounds.Width / 2) > x0 && (p.Bounds.Left + p.Bounds.Width / 2) < x1 && (p.Bounds.Top + p.Bounds.Height / 2) > y0 && (p.Bounds.Top + p.Bounds.Height / 2) < y1;
        });
        Report("stone hill tower", ArchFaces);
        Report("town square doorway", Cluster(8900, 9700, 2800, 3700));
        Report("dark hollow doorway", Cluster(3400, 4100, 3700, 4500));
        foreach (var grp in g.Polygons.Select((p, i) => (p, i)).Where(t => (t.p.Bounds.Left + t.p.Bounds.Width / 2) > 5500 && (t.p.Bounds.Left + t.p.Bounds.Width / 2) < 6300 && (t.p.Bounds.Top + t.p.Bounds.Height / 2) > 1900 && (t.p.Bounds.Top + t.p.Bounds.Height / 2) < 2600).GroupBy(t => t.p.SectorOffset))
            Console.WriteLine($"new spot sector 0x{grp.Key:X}: {grp.Count()} faces");
        Console.WriteLine($"sectors in level: {g.SourceSectors.Count}");
        Console.WriteLine("NEW_LEVEL_PROBE_OK");
    }

    private static void Lining(MainWindow window)
    {
        Load(window, "artisans");
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        object tuple = Invoke(window, "TerrainCollisionTriangles")!;
        var tris = (System.Collections.IList)tuple.GetType().GetField("Item1")!.GetValue(tuple)!;
        var pts = new List<(int tri, float x, float y, float z)>();
        for (int t = 0; t < tris.Count; t++)
            foreach (object pt in (Array)tris[t]!)
                pts.Add((t, Convert.ToSingle(Prop(pt, "X")), Convert.ToSingle(Prop(pt, "Y")), Convert.ToSingle(Prop(pt, "Z"))));
        Console.WriteLine($"collision triangles: {tris.Count}");
        foreach (int i in new[] { 185, 208, 186, 209, 184, 210, 402, 403, 404, 405, 406, 407 })
        {
            var p = g.Polygons[i];
            float cx = p.Points.Average(q => q.X), cy = p.Points.Average(q => q.Y), cz = p.ZValues.Average();
            var near = pts.Where(q => Math.Abs(q.x - cx) < 40 && Math.Abs(q.y - cy) < 40 && Math.Abs(q.z - cz) < 80).Select(q => q.tri).Distinct().ToList();
            var touching = pts.Where(q => Enumerable.Range(0, p.Points.Count).Any(k => Math.Abs(q.x - p.Points[k].X) < 3 && Math.Abs(q.y - p.Points[k].Y) < 3 && Math.Abs(q.z - p.ZValues[k]) < 3)).Select(q => q.tri).Distinct().ToList();
            var f = Enumerable.Range(0, p.Points.Count).Select(k => ((double)p.Points[k].X, (double)p.Points[k].Y, (double)p.ZValues[k])).ToArray();
            var c3 = (f.Average(q => q.Item1), f.Average(q => q.Item2), f.Average(q => q.Item3));
            for (int t = 0; t < tris.Count; t++)
            {
                var tp = ((Array)tris[t]!).Cast<object>().Select(o => ((double)Convert.ToSingle(Prop(o, "X")), (double)Convert.ToSingle(Prop(o, "Y")), (double)Convert.ToSingle(Prop(o, "Z")))).ToArray();
                double d = Spyro.Editor.Core.Scene.TerrainSceneryFaceRule.DistanceToTriangle(c3, tp[0], tp[1], tp[2]);
                if (d < 6) Console.WriteLine($"   tri {t} dist {d:0.00}: {string.Join(" ", tp.Select(q => $"({q.Item1},{q.Item2},{q.Item3})"))}");
            }
            Console.WriteLine($"face {i} {p.RuntimeKey} corners {string.Join(" ", Enumerable.Range(0, p.Points.Count).Select(k => $"({p.Points[k].X:0},{p.Points[k].Y:0},{p.ZValues[k]:0})"))}: collision tris within 40 of centre {near.Count}, sharing a corner {touching.Count}");
        }
        Console.WriteLine("NEW_LEVEL_LINING_OK");
    }

    private static void PortalGeom(MainWindow window)
    {
        Load(window, "artisans");
        var level = window.CaptureEditorShellSessionSnapshotForTesting().CurrentLevel!;
        string image = (string)Invoke(window, "ResolveTerrainTextureSourceImage", level)!;
        var data = Spyro.Editor.Core.Exporting.PortalSourceDataLocator.Locate(image, level);
        foreach (var portal in data.Portals)
        {
            Console.WriteLine($"portal {portal.Index} -> level {portal.DestinationLevelId} path T{portal.SourcePathMobyTrueIndex} normal ({portal.NormalX},{portal.NormalY},{portal.NormalZ}) centre ({portal.CenterX},{portal.CenterY},{portal.CenterZ}) points {string.Join(" ", portal.Points.Select(q => $"({q.X},{q.Y},{q.Z})"))}");
            foreach (var t in data.TriggerTriangles.Where(t => t.PortalIndex == portal.Index))
                Console.WriteLine($"   trigger tri {t.TriangleIndex}: {Prop(t, "P1")} {Prop(t, "P2")} {Prop(t, "P3")}");
        }
        ComboBox box = Get<ComboBox>(window, "_portalControlBox");
        foreach (object row in box.Items)
        {
            var members = (IReadOnlyList<Moby>)Prop(row, "Members")!;
            Console.WriteLine($"row {row}: " + string.Join("; ", members.Select(m => $"T{m.TrueIndex} {m.DisplayLabel} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte}")));
        }
        Console.WriteLine("NEW_LEVEL_GEOM_OK");
    }

    internal static object? Prop(object o, string name) => o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(o);
    internal static void SelectTab(MainWindow window, string header)
    {
        var tabs = Find<Avalonia.Controls.TabControl>(window, "EditorWorkspaceToolTabs");
        tabs.SelectedItem = tabs.Items.OfType<Avalonia.Controls.TabItem>().First(t => (t.Header as Avalonia.Controls.TextBlock)?.Text == header); Pump(20);
    }
}
