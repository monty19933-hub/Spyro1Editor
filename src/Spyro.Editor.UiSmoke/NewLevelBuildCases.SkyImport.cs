using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Sky;

/// <summary>Import Sky with a 360° panorama PNG written here (sky blue, green ground, a red marker facing heading 0):
/// the real dialog (preview built, Detail changed, Use This Sky), a level you build taking it and saving it in the
/// scene, and Artisans taking it as a .sky file in the project through Apply Sky.</summary>
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

        // A level you build keeps the sky in its scene.
        var created = (Task)Invoke(window, "NewMainLevelAsync")!;
        Wait(() =>
        {
            if (window.OwnedWindows.FirstOrDefault(w => w.Name == "NewLevelReplaceDialog") is { } ask) Click(Find<Button>(ask, "ConfirmationConfirm"));
            return created.IsCompleted;
        }, "File > New Level", 600);
        window.CustomSkyFilePickerOverrideForTesting = () => Task.FromResult<string?>(png);
        window.SkyImportChoiceForTesting = () => (2000, 0);
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

            // Artisans: the sky becomes a .sky file in the project, used by Apply Sky.
            Load(window, "artisans");
            var choose = (Task)Invoke(window, "ChooseCustomSkyAsync")!;
            Wait(() => choose.IsCompleted, "Choose Sky File in Artisans", 120);
            string relative = Get<TextBox>(window, "_skyboxImportPathBox").Text ?? "";
            string full = Path.Combine(Workspace(window).RootPath, relative);
            Console.WriteLine($"artisans: {relative}; {Status(window)}");
            if (!relative.EndsWith(".sky", StringComparison.Ordinal) || !File.Exists(full) ||
                !Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(File.ReadAllBytes(full)) || NativeSkyPreview.Triangles(File.ReadAllBytes(full)).Count != 2000)
                throw new InvalidOperationException("Artisans did not get the converted sky as a .sky file.");
            var apply = (Task<bool>)Invoke(window, "SaveSkyboxPlanAsync")!;
            Wait(() => apply.IsCompleted, "Apply Sky", 300);
            Console.WriteLine($"apply sky: {apply.Result}; {Status(window)}");
            if (!apply.Result) throw new InvalidOperationException("Apply Sky refused the converted sky.");
        }
        finally
        {
            window.CustomSkyFilePickerOverrideForTesting = null;
            window.SkyImportChoiceForTesting = null;
        }
        Console.WriteLine("NEW_LEVEL_SKY_IMPORT_OK");
    }
}
