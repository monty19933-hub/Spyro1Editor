using Spyro.Editor.App.Views;

/// <summary>The 3D view draws each level's real sky: an untouched game level (SKY_VIEW_RETAIL, Toasty unless named) its
/// original, a game level with a sky edit (SKY_VIEW_EDITED, Stone Hill) that sky, a level you build its own (after
/// sky-import) and a rebuilt level (SKY_VIEW_REBUILT, Artisans). A picture of each.</summary>
internal static partial class NewLevelBuildCases
{
    private static void SkyViewStep(MainWindow window, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var viewport = Get<EditorViewport>(window, "_viewport");
        void Show(string label, Action open)
        {
            open();
            Wait(() => window.ViewportSkyTaskForTesting is { IsCompleted: true }, "sky for " + label, 120);
            Pump(10);
            Console.WriteLine($"{label}: sky shown {viewport.HasSkyPanoramaForTesting}; {Status(window)}");
            if (!viewport.HasSkyPanoramaForTesting) throw new InvalidOperationException($"{label} shows no sky in the 3D view.");
            Snapshot(window, Path.Combine(outDir, $"sky-view-{label.Replace(' ', '-').ToLowerInvariant()}.png"));
        }
        Show("original " + (Environment.GetEnvironmentVariable("SKY_VIEW_RETAIL") ?? "toasty"), () => Load(window, Environment.GetEnvironmentVariable("SKY_VIEW_RETAIL") ?? "toasty"));
        Show("edited " + (Environment.GetEnvironmentVariable("SKY_VIEW_EDITED") ?? "stonehill"), () => Load(window, Environment.GetEnvironmentVariable("SKY_VIEW_EDITED") ?? "stonehill"));
        Show("rebuilt " + (Environment.GetEnvironmentVariable("SKY_VIEW_REBUILT") ?? "artisans"), () => Load(window, Environment.GetEnvironmentVariable("SKY_VIEW_REBUILT") ?? "artisans"));
        Show("your level", () =>
        {
            var open = (Task)Invoke(window, "OpenMainFreshLevelAsync")!;
            Wait(() => open.IsCompleted, "Open Created Level", 120);
        });
        // Facing heading 0 (+X) level with the horizon, the middle of the view is the imported panorama's middle: its red
        // marker (sky-import's picture); facing the other way it is not.
        viewport.SetFlyGameCameraForTesting(3000, 3000, 900, 0, 0); Snapshot(window, Path.Combine(outDir, "sky-view-facing-east.png"));
        var east = viewport.SkyBackgroundPixelForTesting(0.5, 0.5);
        viewport.SetFlyGameCameraForTesting(3000, 3000, 900, Math.PI, 0); Snapshot(window, Path.Combine(outDir, "sky-view-facing-west.png"));
        var west = viewport.SkyBackgroundPixelForTesting(0.5, 0.5);
        Console.WriteLine($"facing east: {east}; facing west: {west}");
        if (east is not { R: > 180, G: < 80, B: < 80 } || west is { R: > 180, G: < 80 }) throw new InvalidOperationException("The 3D view's sky does not face the way the game's does.");
        Console.WriteLine("NEW_LEVEL_SKY_VIEW_OK");
    }
}
