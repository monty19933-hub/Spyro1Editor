using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.YearOfTheDragon;

/// <summary>A Spyro: Year of the Dragon level rebuilt in a Spyro the Dragon level slot, as the importer does it
/// (<see cref="YearOfTheDragonLevelImport"/>): YOTD_LEVEL names the level ("Sunny Villa", "lost fleet" or its index),
/// IMPORT_TARGET the retail level it replaces ("drycanyon"); the disc is YOTD_DISC or the project's configured Year of
/// the Dragon disc. The target is Start-Blanked, the imported terrain applied (its pictures go to the project's
/// custom-terrain-textures/&lt;level&gt;/) and saved; the import report is printed.</summary>
internal static partial class NewLevelBuildCases
{
    private static void YearOfTheDragonImport(MainWindow window)
    {
        string name = Environment.GetEnvironmentVariable("YOTD_LEVEL") ?? throw new InvalidOperationException("Set YOTD_LEVEL (a Year of the Dragon level name).");
        string key = Environment.GetEnvironmentVariable("IMPORT_TARGET") ?? throw new InvalidOperationException("Set IMPORT_TARGET (the Spyro the Dragon level to replace).");
        string disc = Environment.GetEnvironmentVariable("YOTD_DISC") is { Length: > 0 } configured ? configured
            : Spyro.Editor.Core.Music.YearOfTheDragonDiscLocator.Find(Workspace(window));
        if (disc.Length == 0) throw new InvalidOperationException("Set YOTD_DISC (or run the yotd-disc step first).");
        var level = YearOfTheDragonLevels.Find(name);

        StartBlank(window, key);
        var blank = window.FreshSceneForTesting ?? throw new InvalidOperationException($"{key} is not blank");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        var target = catalog.FindByKey(key)!;
        string usa = (string)Invoke(window, "ResolveTerrainTextureSourceImage", target)!;
        string pictures = Path.Combine(Workspace(window).RootPath, "custom-terrain-textures", YearOfTheDragonLevelImport.Slug(level.Name));
        Console.WriteLine($"importing {level.Name} (WAD entry {level.WadEntry}) into {target.DisplayName}; pictures in {pictures}");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var import = Task.Run(() => YearOfTheDragonLevelImport.ImportAsync(disc, level, usa, catalog, blank.TargetBinding,
            new() { PictureFolder = pictures }, new Progress<string>(m => Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,4:0}s] {m}"))));
        Wait(() => import.IsCompleted, "import " + level.Name, 1200);
        if (import.IsFaulted) throw import.Exception!.InnerException!;
        var result = import.Result;
        foreach (string line in result.Report.Lines()) Console.WriteLine("  " + line);

        ApplyScene(window, result.ApplyTo(blank));
        var saved = SceneDocument.Read(SceneDocument.PathForTarget(Workspace(window).RootPath, key));
        if (saved.Surfaces.Count != result.Scene.Surfaces.Count || saved.FarMeshes?.Count != result.Scene.FarMeshes?.Count ||
            saved.Surfaces.Count(s => s.Kind != null) != result.Scene.Surfaces.Count(s => s.Kind != null) ||
            saved.Surfaces.Count(s => s.TwoSided) != result.Scene.Surfaces.Count(s => s.TwoSided) ||
            saved.Surfaces.Count(s => s.FrontReversed) != result.Scene.Surfaces.Count(s => s.FrontReversed))
            throw new InvalidOperationException("The saved scene lost faces, ground kinds, sides or distant meshes of the import.");
        Console.WriteLine($"saved {target.DisplayName} as {saved.Name}: {saved.Surfaces.Count} faces, {saved.FarMeshes?.Count ?? 0} distant meshes, " +
            $"start ({saved.Spawn.X:0},{saved.Spawn.Y:0},{saved.Spawn.Z:0}) heading {saved.Heading}, {result.Materials.Count} pictures");
        Console.WriteLine("NEW_LEVEL_YOTD_IMPORT_OK");
    }

    private static void RunImportCommand(MainWindow window, string key, string method, string label)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var task = (Task)Invoke(window, method)!;
        string last = "";
        Wait(() =>
        {
            string now = Status(window);
            if (now != last) { Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,4:0}s] {now}"); last = now; }
            return task.IsCompleted;
        }, label, 1800);
        if (task.IsFaulted) throw task.Exception!.InnerException!;
        var scene = window.FreshSceneForTesting ?? throw new InvalidOperationException("No built level after the command: " + Status(window));
        var saved = SceneDocument.Read(SceneDocument.PathForTarget(Workspace(window).RootPath, key));
        if (saved.Surfaces.Count != scene.Surfaces.Count || saved.Surfaces.Count < 300)
            throw new InvalidOperationException($"The command did not save the imported level ({saved.Name}, {saved.Surfaces.Count} faces): {Status(window)}");
        var line = Get<Avalonia.Controls.TextBlock>(window, "_completionText");
        Wait(() => line.IsVisible, "the 100% line", 180);
        Console.WriteLine($"saved {key} as {saved.Name}: {saved.Surfaces.Count} faces, {saved.Objects.Count} objects; {line.Text!.Replace("\n", " | ")}");
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && !m.IsGemLike))
            Console.WriteLine($"  {m.DisplayLabel,-30} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte}{(m.UniversalPathNodes is { } r ? $" route {r.Count} points" : "")}");
        Console.WriteLine($"  gems: {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved && m.IsGemLike)}; treasure {Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue)}");
        Console.WriteLine("status: " + Status(window));
    }

    /// <summary>The same through the editor's command, File ▾ > New Level from Spyro: Year of the Dragon… (the level
    /// dialog answered with YOTD_LEVEL; the disc is the project's configured one): IMPORT_TARGET is opened and replaced.</summary>
    private static void YearOfTheDragonMenu(MainWindow window)
    {
        string name = Environment.GetEnvironmentVariable("YOTD_LEVEL") ?? throw new InvalidOperationException("Set YOTD_LEVEL (a Year of the Dragon level name).");
        string key = Environment.GetEnvironmentVariable("IMPORT_TARGET") ?? throw new InvalidOperationException("Set IMPORT_TARGET (the Spyro the Dragon level to replace).");
        var level = YearOfTheDragonLevels.Find(name);
        Load(window, key);
        bool fill = Environment.GetEnvironmentVariable("YOTD_FILL") != "0";
        window.OtherGameLevelChoiceForTesting = () => new(level, true, true, fill);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var task = (Task)Invoke(window, "NewLevelFromYearOfTheDragonAsync")!;
        string last = "";
        Wait(() =>
        {
            string now = Status(window);
            if (now != last) { Console.WriteLine($"  [{clock.Elapsed.TotalSeconds,4:0}s] {now}"); last = now; }
            return task.IsCompleted;
        }, "New Level from Year of the Dragon", 1200);
        if (task.IsFaulted) throw task.Exception!.InnerException!;
        var scene = window.FreshSceneForTesting ?? throw new InvalidOperationException("No built level after the command: " + Status(window));
        var saved = SceneDocument.Read(SceneDocument.PathForTarget(Workspace(window).RootPath, key));
        if (saved.Name != level.SpyroName || saved.Surfaces.Count != scene.Surfaces.Count || saved.Surfaces.Count < 500 || saved.FarMeshes is not { Count: > 0 })
            throw new InvalidOperationException($"The command did not save the imported level ({saved.Name}, {saved.Surfaces.Count} faces).");
        var line = Get<Avalonia.Controls.TextBlock>(window, "_completionText");
        Wait(() => line.IsVisible, "the 100% line", 180);
        Console.WriteLine($"saved {key} as {saved.Name}: {saved.Surfaces.Count} faces, {saved.Objects.Count} objects, gem spots {saved.GemSpots?.Count ?? 0}; {line.Text!.Replace("\n", " | ")}");
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && !m.IsGemLike))
            Console.WriteLine($"  {m.DisplayLabel,-30} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0}) yaw {m.YawByte}{(m.UniversalPathNodes is { } r ? $" route {r.Count} points" : "")}");
        Console.WriteLine($"  gems: {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved && m.IsGemLike)}; treasure {Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue)}");
        Console.WriteLine("status: " + Status(window));
        Console.WriteLine("NEW_LEVEL_YOTD_MENU_OK");
    }
}
