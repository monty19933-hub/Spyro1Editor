using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Sky;

/// <summary>Import Sky with a 360° panorama PNG written here (sky blue, green ground, a red marker facing heading 0):
/// the real dialog (preview built, Detail changed, Use This Sky), files from skybox makers (JPEG, OBJ, FBX), a level you
/// build taking it and saving it in the scene, a game level taking it as a .sky file in the project through Apply Sky,
/// and a rebuilt level keeping it in its scene.</summary>
internal static partial class NewLevelBuildCases
{
    private static void SkyImportStep(MainWindow window, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string png = Path.Combine(outDir, "marker-panorama.png");
        const int W = 2048, H = 1024;
        var pixels = new Rgba32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                double u = (x + 0.5) / W, v = (y + 0.5) / H;
                Rgba32 c = v < 0.5 ? new((byte)(60 + 280 * v), (byte)(120 + 200 * v), 230, 255) : new(40, 140, 50, 255);
                if (Math.Abs(u - 0.5) < 0.03 && Math.Abs(v - 0.5) < 0.06) c = new(230, 20, 20, 255);
                pixels[y * W + x] = c;
            }
        Task.Run(() => TerrainTexturePngWriter.WriteRgbaAsync(png, W, H, pixels)).GetAwaiter().GetResult(); // off the UI thread

        // The real dialog: its preview is built, Detail changes it, Use This Sky gives that many triangles.
        var import = (Task<SkyBuildResult?>)Invoke(window, "ImportSkyFromFileAsync", png)!;
        Wait(() => window.OwnedWindows.Any(w => w.Name == "SkyImportDialog"), "Import Sky dialog", 120);
        var dialog = window.OwnedWindows.Single(w => w.Name == "SkyImportDialog");
        Wait(() => Find<Button>(dialog, "SkyImportUse").IsEnabled, "sky preview", 120);
        Console.WriteLine("dialog: " + Find<TextBlock>(dialog, "SkyImportSummary").Text);
        Find<Slider>(dialog, "SkyImportDetail").Value = 1500; Pump(5);
        Wait(() => Find<TextBlock>(dialog, "SkyImportSummary").Text!.StartsWith("1,500 triangles") && Find<Button>(dialog, "SkyImportUse").IsEnabled, "preview at 1,500 triangles", 120);
        Snapshot(dialog, Path.Combine(outDir, "01-import-sky-dialog.png"));
        Click(Find<Button>(dialog, "SkyImportUse"));
        Wait(() => import.IsCompleted, "sky built", 120);
        var fromDialog = import.Result ?? throw new InvalidOperationException("The dialog gave no sky.");
        Console.WriteLine($"dialog sky: {fromDialog.Triangles} triangles, {fromDialog.Parts} parts, {fromDialog.Block.Length} bytes");
        if (fromDialog.Triangles != 1500) throw new InvalidOperationException("Use This Sky did not keep the chosen detail.");

        // Files as skybox makers give them (SKY_JPG, SKY_OBJ, SKY_FBX: the same marker panorama, as a JPEG and on a
        // textured sphere): each makes a sky with blue above, green below, and the yellow marker a quarter turn left of
        // the red one (so no reader mirrors it). Models may face another way, so only the markers' spacing is checked.
        // SKY_OUTSIDE_OBJ: a sphere made to be seen from outside (its picture reads backwards from inside), imported
        // with Flip left and right.
        foreach (string variable in new[] { "SKY_JPG", "SKY_OBJ", "SKY_FBX", "SKY_OUTSIDE_OBJ" })
        {
            if (Environment.GetEnvironmentVariable(variable) is not { Length: > 0 } file) continue;
            bool flip = variable == "SKY_OUTSIDE_OBJ";
            window.SkyImportChoiceForTesting = () => new SkyBuildOptions(3000, 0, flip);
            var made = (Task<SkyBuildResult?>)Invoke(window, "ImportSkyFromFileAsync", file)!;
            Wait(() => made.IsCompleted, "import " + Path.GetFileName(file), 300);
            window.SkyImportChoiceForTesting = null;
            var result = made.Result ?? throw new InvalidOperationException($"{Path.GetFileName(file)}: {Status(window)}");
            var picture = NativeSkyPreview.Render(result.Block, 512);
            double Mean(int y0, int y1, Func<Rgba32, int> channel) =>
                Enumerable.Range(y0, y1 - y0).SelectMany(y => Enumerable.Range(0, 512).Select(x => channel(picture.Pixels[y * 512 + x]))).Average();
            double MarkerU(int r, int g, int b)
            {
                // Circular mean of the marker's columns on the horizon band.
                var hits = Enumerable.Range(112, 32).SelectMany(y => Enumerable.Range(0, 512).Where(x =>
                {
                    var p = picture.Pixels[y * 512 + x];
                    return Math.Abs(p.R - r) + Math.Abs(p.G - g) + Math.Abs(p.B - b) < 90;
                })).ToList();
                if (hits.Count < 20) throw new InvalidOperationException($"{Path.GetFileName(file)}: a marker ({r},{g},{b}) is missing.");
                double sx = hits.Sum(x => Math.Cos(2 * Math.PI * x / 512)), sy = hits.Sum(x => Math.Sin(2 * Math.PI * x / 512));
                double a = Math.Atan2(sy, sx) / (2 * Math.PI);
                return a < 0 ? a + 1 : a;
            }
            double red = MarkerU(230, 20, 20), yellow = MarkerU(240, 220, 20), apart = ((red - yellow) % 1 + 1) % 1;
            double topBlue = Mean(0, 20, p => p.B), bottomGreen = Mean(236, 256, p => p.G) - Mean(236, 256, p => p.R);
            Console.WriteLine($"{Path.GetFileName(file)}: {result.Triangles} triangles; red at u {red:0.000}, yellow at u {yellow:0.000} (apart {apart:0.000}); top blue {topBlue:0}, ground green {bottomGreen:0}");
            Task.Run(() => TerrainTexturePngWriter.WriteRgbaAsync(Path.Combine(outDir, $"sky-from-{variable[4..].ToLowerInvariant().Replace('_', '-')}.png"), 512, 256, picture.Pixels)).GetAwaiter().GetResult();
            if (Math.Abs(apart - 0.25) > 0.03 || topBlue < 200 || bottomGreen < 60)
                throw new InvalidOperationException($"{Path.GetFileName(file)} does not give the expected sky (mirrored, or upside down).");
            if (variable == "SKY_JPG" && Math.Abs(red - 0.5) > 0.02) throw new InvalidOperationException("The JPEG panorama's middle does not face heading 0.");
        }

        // A level you build keeps the sky in its scene.
        var created = (Task)Invoke(window, "NewMainLevelAsync")!;
        Wait(() =>
        {
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask) Click(Find<Button>(ask, "ConfirmationConfirm"));
            return created.IsCompleted;
        }, "File > New Level", 600);
        window.CustomSkyFilePickerOverrideForTesting = () => Task.FromResult<string?>(png);
        window.SkyImportChoiceForTesting = () => new SkyBuildOptions(2000);
        try
        {
            var panel = Get<StackPanel>(window, "_freshSettingsPanel");
            var fresh = (Task)Invoke(window, "ImportFreshSkyAsync")!;
            Wait(() => fresh.IsCompleted, "Import a sky in a built level", 120);
            var scene = window.FreshSceneForTesting!;
            Console.WriteLine($"built level: {Status(window)}; sky colour {scene.SkyColor}");
            if (scene.CustomSky == null || scene.SkySourceWadEntry != null || NativeSkyPreview.Triangles(scene.CustomSky).Count != 2000)
                throw new InvalidOperationException("The built level did not take the imported sky.");
            var picker = Find<ComboBox>(Get<StackPanel>(window, "_freshSettingsPanel"), "FreshMainSkySource");
            if (picker.SelectedItem as string != "Your imported sky") throw new InvalidOperationException("The skybox list does not show the imported sky.");
            Save(window);
            var saved = SceneDocument.Read(SceneDocumentStore.PathForTarget(Workspace(window).RootPath, "authored65"));
            if (saved.CustomSky is not { } kept || !kept.SequenceEqual(scene.CustomSky)) throw new InvalidOperationException("The imported sky was not saved with the level.");
            Snapshot(window, Path.Combine(outDir, "02-built-level-sky.png"));

            // A game level (SKY_RETAIL, Stone Hill unless named): the sky becomes a .sky file in the project, used by
            // Apply Sky. A rebuilt level (SKY_REBUILT, Artisans in the test project) keeps it in its scene instead.
            string retail = Environment.GetEnvironmentVariable("SKY_RETAIL") ?? "stonehill";
            Load(window, retail);
            if (window.FreshSceneForTesting != null) throw new InvalidOperationException($"{retail} is rebuilt in this project; name a game level in SKY_RETAIL.");
            var choose = (Task)Invoke(window, "ChooseCustomSkyAsync")!;
            Wait(() => choose.IsCompleted, "Choose Sky File in " + retail, 120);
            string relative = Get<TextBox>(window, "_skyboxImportPathBox").Text ?? "";
            string full = Path.Combine(Workspace(window).RootPath, relative);
            Console.WriteLine($"{retail}: {relative}; {Status(window)}");
            if (!relative.EndsWith(".sky", StringComparison.Ordinal) || !File.Exists(full) ||
                !Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(File.ReadAllBytes(full)) || NativeSkyPreview.Triangles(File.ReadAllBytes(full)).Count != 2000)
                throw new InvalidOperationException($"{retail} did not get the converted sky as a .sky file.");
            var apply = (Task<bool>)Invoke(window, "SaveSkyboxPlanAsync")!;
            Wait(() => apply.IsCompleted, "Apply Sky", 300);
            Console.WriteLine($"apply sky: {apply.Result}; {Status(window)}");
            if (!apply.Result) throw new InvalidOperationException("Apply Sky refused the converted sky.");

            if (Environment.GetEnvironmentVariable("SKY_REBUILT") is { Length: > 0 } rebuilt)
            {
                Load(window, rebuilt);
                if (window.FreshSceneForTesting == null) throw new InvalidOperationException($"{rebuilt} is not rebuilt in this project.");
                var redirect = (Task)Invoke(window, "ChooseCustomSkyAsync")!;
                Wait(() => redirect.IsCompleted, "Choose Sky File in " + rebuilt, 120);
                Console.WriteLine($"{rebuilt} (rebuilt): {Status(window)}");
                if (window.FreshSceneForTesting!.CustomSky == null) throw new InvalidOperationException("The rebuilt level did not keep the sky in its scene.");
                Save(window);
            }
        }
        finally
        {
            window.CustomSkyFilePickerOverrideForTesting = null;
            window.SkyImportChoiceForTesting = null;
        }
        Console.WriteLine("NEW_LEVEL_SKY_IMPORT_OK");
    }
}
