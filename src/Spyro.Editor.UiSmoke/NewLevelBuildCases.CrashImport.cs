using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

/// <summary>A Crash Bandicoot: Warped level rebuilt in a Spyro the Dragon level slot, as the importer does it
/// (<see cref="CrashLevelImport"/>): CRASH_LEVEL names the Crash level ("Toad Village", "gee wiz" or its file
/// "S0/S000000B.NSF"), IMPORT_TARGET the retail level it replaces ("icecavern"); the disc is CRASH_DISC or the project's
/// configured Crash disc (crash-disc step / "Choose Crash disc…"). Optional: CRASH_ZONES "first:last" (indices along the
/// level's path), CRASH_SCALE (editor units per Crash unit, default crate scale 0.115), CRASH_MARGIN (backdrop kept, Crash
/// units), CRASH_TURN (degrees), CRASH_WALLS=0 (no invisible walls). The target is Start-Blanked, the imported terrain
/// applied (its pictures go to the project's custom-terrain-textures/&lt;level&gt;/) and saved; the report is printed.</summary>
internal static partial class NewLevelBuildCases
{
    private static void CrashImport(MainWindow window, string outDir)
    {
        string name = Environment.GetEnvironmentVariable("CRASH_LEVEL") ?? throw new InvalidOperationException("Set CRASH_LEVEL (a Crash Bandicoot: Warped level name).");
        string key = Environment.GetEnvironmentVariable("IMPORT_TARGET") ?? throw new InvalidOperationException("Set IMPORT_TARGET (the Spyro the Dragon level to replace).");
        string disc = Environment.GetEnvironmentVariable("CRASH_DISC") is { Length: > 0 } configured ? configured : CrashWarpedDiscLocator.Find(Workspace(window));
        if (disc.Length == 0) throw new InvalidOperationException("Set CRASH_DISC (or run the crash-disc step first).");
        var level = CrashLevels.Find(name);
        int? first = null, last = null;
        if (Environment.GetEnvironmentVariable("CRASH_ZONES") is { Length: > 0 } zones)
        {
            var parts = zones.Split(':');
            first = int.Parse(parts[0]); last = int.Parse(parts[^1]);
        }
        static double? Number(string variable) => double.TryParse(Environment.GetEnvironmentVariable(variable), System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : null;

        StartBlank(window, key);
        var blank = window.FreshSceneForTesting ?? throw new InvalidOperationException($"{key} is not blank");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        var target = catalog.FindByKey(key)!;
        string usa = (string)Invoke(window, "ResolveTerrainTextureSourceImage", target)!;
        string pictures = Path.Combine(Workspace(window).RootPath, "custom-terrain-textures", CrashLevelImport.Slug(level.Name));
        var options = new CrashLevelImportOptions
        {
            PictureFolder = pictures, FirstZone = first, LastZone = last,
            Scale = Number("CRASH_SCALE") ?? CrashLevelImportOptions.CrateScale,
            BackdropMargin = Number("CRASH_MARGIN") is double m ? (int)m : null,
            Turn = Number("CRASH_TURN"),
            InvisibleWalls = Environment.GetEnvironmentVariable("CRASH_WALLS") != "0",
        };
        Console.WriteLine($"importing {level.Name} ({level.File}) into {target.DisplayName}; pictures in {pictures}");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var import = Task.Run(() => CrashLevelImport.ImportAsync(disc, level, usa, catalog, blank.TargetBinding, options,
            new Progress<string>(msg => Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,4:0}s] {msg}"))));
        Wait(() => import.IsCompleted, "import " + level.Name, 1200);
        if (import.IsFaulted) throw import.Exception!.InnerException!;
        var result = import.Result;
        foreach (string line in result.Report.Lines()) Console.WriteLine("  " + line);
        // Crash's way through the level, for walking it in the emulator.
        File.WriteAllText(Path.Combine(outDir, "crash-route.json"),
            System.Text.Json.JsonSerializer.Serialize(result.Report.Route.Select(p => new[] { p.X, p.Y, p.Z })));

        ApplyScene(window, result.ApplyTo(blank));
        var saved = SceneDocument.Read(SceneDocument.PathForTarget(Workspace(window).RootPath, key));
        if (saved.Surfaces.Count != result.Scene.Surfaces.Count || saved.CollisionFaces?.Count != result.Scene.CollisionFaces?.Count ||
            saved.Surfaces.Count(s => s.NoCollision) != result.Scene.Surfaces.Count(s => s.NoCollision) ||
            saved.Surfaces.Count(s => s.TwoSided) != result.Scene.Surfaces.Count(s => s.TwoSided) ||
            saved.Surfaces.Count(s => s.FrontReversed) != result.Scene.Surfaces.Count(s => s.FrontReversed) ||
            saved.Surfaces.Select(s => s.Sector).Distinct().Count() != result.Scene.Surfaces.Select(s => s.Sector).Distinct().Count())
            throw new InvalidOperationException("The saved scene lost faces, sides, sectors, collision-free faces or invisible walls of the import.");
        Console.WriteLine($"saved {target.DisplayName} as {saved.Name}: {saved.Surfaces.Count} faces, {saved.CollisionFaces?.Count ?? 0} invisible faces, " +
            $"start ({saved.Spawn.X:0},{saved.Spawn.Y:0},{saved.Spawn.Z:0}) heading {saved.Heading}, {result.Materials.Count} pictures");
        Console.WriteLine("NEW_LEVEL_CRASH_IMPORT_OK");
    }
}
