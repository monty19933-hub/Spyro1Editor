using Spyro.Editor.Core;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Skyboxes;
using Spyro.Editor.Core.Workspace;
using System.Security.Cryptography;

string? workspaceArgument = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
EditorWorkspace workspace = EditorWorkspace.Find(workspaceArgument);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
LevelDefinition target = catalog.FindByKey("stonehill")
    ?? throw new InvalidOperationException("Stone Hill is missing from the level catalog.");
LevelDefinition donor = catalog.FindByKey("crystalflight")
    ?? throw new InvalidOperationException("Crystal Flight is missing from the level catalog.");

string sourceImage = Option("--source=") ?? Path.Combine(
    workspace.RootPath,
    "output",
    "_combined-build",
    "Spyro Editor - Stone Hill - 03-Stone-Hill-terrain.bin");
string sourceCue = Option("--cue=") ?? Path.ChangeExtension(sourceImage, ".cue");
string wadAnalysis = Option("--analysis=") ?? Path.Combine(workspace.RootPath, "spyro-wad-analysis.json");
string outputDirectory = Option("--output=") ?? Path.Combine(
    workspace.RootPath,
    "output",
    "environment-composite-fix-test");

RequireFile(sourceImage, "post-texture source BIN");
RequireFile(sourceCue, "post-texture source CUE");
RequireFile(wadAnalysis, "WAD analysis");
Directory.CreateDirectory(outputDirectory);

NativeEnvironmentGradePlan grade = NativeEnvironmentGradePlan.MatchSkySource(donor.Key) with
{
    GradeActors = true,
    GradeChests = true,
    GradeScenery = true,
    GradeDragons = true
};
NativeEnvironmentGradeMatch before = NativeEnvironmentGradeExporter.AnalyzeMatch(
    sourceImage,
    wadAnalysis,
    target,
    donor,
    grade);

string gradePrefix = Path.Combine(outputDirectory, "01-Stone-Hill-Crystal-Flight-composite-grade");
NativeEnvironmentGradePatchResult gradeResult = await NativeEnvironmentGradeExporter.ExportBatchAsync(
    new NativeEnvironmentGradeBatchPatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: sourceCue,
        WadAnalysisPath: wadAnalysis,
        OutputPrefix: gradePrefix,
        Catalog: catalog,
        Edits: [new NativeEnvironmentGradeBatchEdit(target, grade)],
        WriteImage: true));

NativeEnvironmentGradeMatch after = NativeEnvironmentGradeExporter.AnalyzeMatch(
    gradeResult.OutputImagePath,
    wadAnalysis,
    target,
    donor,
    grade);
if (!gradeResult.WroteImage || gradeResult.Plan.SceneColorPatchCount <= 0 || gradeResult.Plan.TexturePalettePatchCount != 0)
    throw new InvalidOperationException("The composite grade must patch native scene colors while preserving every texture-page byte.");
if (gradeResult.Plan.Patches.Any(patch => patch.ByteLength <= 0 || patch.ChangedByteCount <= 0))
    throw new InvalidOperationException("The composite grade emitted an empty or unchanged patch.");
double beforeComposite = CompositeMeanLuminance(before.TargetSceneColors, before.TargetTextureUsage);
double afterComposite = CompositeMeanLuminance(after.TargetSceneColors, after.TargetTextureUsage);
double donorComposite = CompositeMeanLuminance(before.DonorSceneColors, before.DonorTextureUsage);
if (Math.Abs(afterComposite - donorComposite) >= Math.Abs(beforeComposite - donorComposite))
    throw new InvalidOperationException(
        $"Composite environment output did not move toward the donor ({beforeComposite:F4} -> {afterComposite:F4}; donor {donorComposite:F4}).");

NativeSkyEditPlan skyEdit = new(
    Version: 3,
    SavedAt: DateTimeOffset.UtcNow,
    LevelKey: target.Key,
    LevelName: target.DisplayName,
    Mode: NativeSkyEditPlan.SwapMode,
    PalettePreset: "",
    CustomPaletteHex: "",
    DonorLevelKey: donor.Key,
    ImportedSkyPath: "",
    ImportedSkySha256: "")
{
    EnvironmentGrade = grade
};
string skyPrefix = Path.Combine(outputDirectory, "Spyro Editor - Stone Hill - Crystal Flight Environment Fix Test");
NativeSkyPatchResult skyResult = await NativeSkyPatchExporter.ExportBatchAsync(
    new NativeSkyBatchPatchRequest(
        SourceImagePath: gradeResult.OutputImagePath,
        SourceCuePath: gradeResult.OutputCuePath,
        WadAnalysisPath: wadAnalysis,
        WorkspacePath: workspace.RootPath,
        OutputPrefix: skyPrefix,
        Catalog: catalog,
        Edits: [new NativeSkyBatchEdit(target, skyEdit)],
        WriteImage: true,
        // Crystal Flight is a flight-stage cyclorama being exercised in a
        // freely roaming ground level. Keep this exact pair research-only
        // until the generated CUE passes the DuckStation camera-path check.
        AllowUnprovenLinkedPortalExpansion: true));

if (!skyResult.WroteImage || !File.Exists(skyResult.OutputImagePath) || !File.Exists(skyResult.OutputCuePath))
    throw new InvalidOperationException("The combined environment regression did not produce its final BIN/CUE.");

string beforeTextureDirectory = Path.Combine(outputDirectory, "texture-preview-before");
string afterTextureDirectory = Path.Combine(outputDirectory, "texture-preview-after");
Directory.CreateDirectory(beforeTextureDirectory);
Directory.CreateDirectory(afterTextureDirectory);
int exportedTexturePairs = 0;
for (int textureId = 0; textureId < Math.Max(64, before.TargetTextureIdCount); textureId++)
{
    string beforePath = Path.Combine(beforeTextureDirectory, $"texture-{textureId:D3}.png");
    string afterPath = Path.Combine(afterTextureDirectory, $"texture-{textureId:D3}.png");
    TerrainTextureImageExport? beforeExport = await TerrainPatchExporter.TryExportTerrainTextureImageAsync(
        sourceImage,
        target,
        textureId,
        beforePath);
    TerrainTextureImageExport? afterExport = await TerrainPatchExporter.TryExportTerrainTextureImageAsync(
        gradeResult.OutputImagePath,
        target,
        textureId,
        afterPath);
    if ((beforeExport == null) != (afterExport == null))
        throw new InvalidOperationException($"Texture {textureId} was not readable on both sides of the grade.");
    if (beforeExport != null)
    {
        string beforeHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(beforePath)));
        string afterHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(afterPath)));
        if (!string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
            throw new InvalidOperationException($"Texture {textureId} changed during composite environment matching ({beforeHash} -> {afterHash}).");
        exportedTexturePairs++;
    }
}

Console.WriteLine("Stone Hill + mixed donor terrain + Crystal Flight environment regression passed.");
Console.WriteLine($"Scene median:  {before.TargetSceneColors.MedianLuminance:F4} -> {after.TargetSceneColors.MedianLuminance:F4} (donor {before.DonorSceneColors.MedianLuminance:F4})");
Console.WriteLine($"Texture median: {before.TargetTextureColors.MedianLuminance:F4} -> {after.TargetTextureColors.MedianLuminance:F4} (donor {before.DonorTextureColors.MedianLuminance:F4})");
Console.WriteLine($"Composite mean: {beforeComposite:F4} -> {afterComposite:F4} (donor {donorComposite:F4})");
Console.WriteLine($"Scene RGB scale: {before.SceneTransform.RedScale:F3}/{before.SceneTransform.GreenScale:F3}/{before.SceneTransform.BlueScale:F3}");
Console.WriteLine($"Texture RGB scale: {before.TextureTransform.RedScale:F3}/{before.TextureTransform.GreenScale:F3}/{before.TextureTransform.BlueScale:F3}");
Console.WriteLine($"Sky visibility patch: {(skyResult.Plan.SkyOcclusionBypassApplied ? "enabled" : "not required")}");
Console.WriteLine($"Texture preview pairs: {exportedTexturePairs} ({beforeTextureDirectory} -> {afterTextureDirectory})");
Console.WriteLine($"Final CUE: {skyResult.OutputCuePath}");
Console.WriteLine($"Grade plan: {gradeResult.OutputPlanPath}");
Console.WriteLine($"Sky plan: {skyResult.OutputPlanPath}");

return;

string? Option(string prefix) => args
    .FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
    [prefix.Length..];

static void RequireFile(string path, string label)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"The {label} was not found.", path);
}

static double CompositeMeanLuminance(
    NativeEnvironmentColorStatistics scene,
    NativeEnvironmentTextureUsageStatistics texture)
{
    double red = scene.MeanRed * texture.MeanRed;
    double green = scene.MeanGreen * texture.MeanGreen;
    double blue = scene.MeanBlue * texture.MeanBlue;
    return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
}
