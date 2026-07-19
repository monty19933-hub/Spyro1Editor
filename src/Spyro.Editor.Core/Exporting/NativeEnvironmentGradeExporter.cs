using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Skyboxes;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeEnvironmentGradeBatchEdit(LevelDefinition Level, NativeEnvironmentGradePlan Grade);

public sealed record NativeEnvironmentGradeMatch(
    string TargetLevelKey,
    string TargetLevelName,
    string DonorLevelKey,
    string DonorLevelName,
    int TargetSectorCount,
    int DonorSectorCount,
    int TargetSceneColorTableCount,
    int DonorSceneColorTableCount,
    int TargetTextureIdCount,
    int DonorTextureIdCount,
    int TargetTexturePaletteCount,
    int DonorTexturePaletteCount,
    int TargetTextureRuntimeVariantCount,
    int DonorTextureRuntimeVariantCount,
    int TargetActorMobyCount,
    int TargetChestMobyCount,
    int TargetSceneryMobyCount,
    int TargetDragonMobyCount,
    string MobyMaterialColorHex,
    NativeEnvironmentColorStatistics TargetSceneColors,
    NativeEnvironmentColorStatistics DonorSceneColors,
    NativeEnvironmentColorStatistics TargetLowDetailSceneColors,
    NativeEnvironmentColorStatistics DonorLowDetailSceneColors,
    NativeEnvironmentColorStatistics TargetHighDetailSceneColors,
    NativeEnvironmentColorStatistics DonorHighDetailSceneColors,
    NativeEnvironmentColorStatistics TargetTextureColors,
    NativeEnvironmentColorStatistics DonorTextureColors,
    NativeEnvironmentTextureUsageStatistics TargetTextureUsage,
    NativeEnvironmentTextureUsageStatistics DonorTextureUsage,
    NativeEnvironmentColorTransform SceneTransform,
    NativeEnvironmentColorTransform LowDetailSceneTransform,
    NativeEnvironmentColorTransform HighDetailSceneTransform,
    NativeEnvironmentColorTransform TextureTransform);

public sealed record NativeEnvironmentTextureUsageStatistics(
    int DescriptorCount,
    int VisibleTexelCount,
    double MeanRed,
    double MeanGreen,
    double MeanBlue,
    double GreenDominantPercent);

public sealed record NativeEnvironmentGradePatch(
    string Label,
    string Kind,
    string LevelKey,
    string LevelName,
    string DonorLevelKey,
    string DonorLevelName,
    string WadOffset,
    string ImageOffset,
    int ByteLength,
    int ColorCount,
    int ChangedByteCount,
    string BeforeSha256,
    string AfterSha256,
    string BeforeHexPreview,
    string AfterHexPreview);

public sealed record NativeEnvironmentGradePatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string WadAnalysisPath,
    int EditedLevelCount,
    int PatchCount,
    int SceneColorPatchCount,
    int TexturePalettePatchCount,
    int MobyMaterialRowPatchCount,
    int MobyRuntimePatchCount,
    int TotalColorCount,
    int TotalWrittenBytes,
    int TotalChangedBytes,
    IReadOnlyList<NativeEnvironmentGradeMatch> Matches,
    IReadOnlyList<NativeEnvironmentGradePatch> Patches,
    IReadOnlyList<string> SafetyNotes);

public sealed record NativeEnvironmentGradeBatchPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string WadAnalysisPath,
    string OutputPrefix,
    LevelCatalog Catalog,
    IReadOnlyList<NativeEnvironmentGradeBatchEdit> Edits,
    bool WriteImage);

public sealed record NativeEnvironmentGradePatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    NativeEnvironmentGradePatchPlan Plan,
    bool WroteImage);

public static class NativeEnvironmentGradeExporter
{
    private const int TexturePagesSubfileIndex = 0;
    private const int ModelSubfileIndex = 1;
    private const int SceneryModelSubfileIndex = 2;
    private const int MobyRecordStride = 0x58;
    private const int MobyMaterialOffset = 0x4F;
    private const int TexturePaletteByteLength = 512;
    private const int TexturePalette16ColorByteLength = 32;
    private const int PackedTexturePageRowByteLength = 1024;
    private const int PackedTexturePageVramXOrigin = 512;
    private const uint ExeDestination = 0x80010000;
    private const uint MobyGradeHookAddress = 0x80012230;
    private const uint OriginalGameLoopTarget = 0x8003385C;
    private const uint MobyGradePayloadAddress = 0x8007314C;
    private const int MobyGradePayloadBytes = 0x400;
    private const int MobyGradeLevelTableOffset = 0x80;
    private const int MaxLevelId = 64;
    private const uint CurrentLevelIdAddress = 0x8007596C;
    private const uint NeutralMobyMaterialAddress = 0x8006E3D8;
    private const uint NeutralMobyMaterialWord = 0x00808080;
    private static readonly int[][] TextureDescriptorMatrices =
    [
        [ 1,  0,  0,  1],
        [ 0,  1,  1,  0],
        [-1,  0,  0, -1],
        [ 0, -1,  1,  0],
        [ 0,  1,  1,  0],
        [-1,  0,  0,  1],
        [ 0, -1, -1,  0],
        [ 1,  0,  0, -1]
    ];
    private static readonly int[] DarkHollowGpuObserved4BitPaletteOffsets =
    [
        0x6C0C0, 0x6C140, 0x6C560, 0x6C8C0, 0x6C960, 0x6CCC0, 0x6D0C0,
        0x6E8E0, 0x6ED60, 0x6F160,
        0x70160, 0x70560, 0x70940, 0x70960, 0x70D40, 0x70D60, 0x71160, 0x71560, 0x72CE0,
        0x740A0, 0x74160, 0x74560, 0x74180, 0x74580, 0x74960, 0x74980, 0x74D60, 0x74D80,
        0x75160, 0x75180, 0x75560, 0x75580, 0x75960, 0x75D60,
        0x760E0, 0x76160, 0x764E0, 0x76560, 0x76960, 0x76D60,
        0x77160, 0x77560, 0x77960,
        0x79D80, 0x7A180, 0x7A580, 0x7A980, 0x7AD80, 0x7B180,
        0x7C080, 0x7C140, 0x7C440, 0x7C460, 0x7C480, 0x7C0A0, 0x7C4A0, 0x7C880, 0x7C8A0,
        0x7CC40, 0x7CC60, 0x7CC80, 0x7CCA0, 0x7CD40,
        0x7D040, 0x7D060, 0x7D080, 0x7D0A0, 0x7D440, 0x7D460, 0x7D480, 0x7D4A0,
        0x7D880, 0x7D8A0, 0x7DC80, 0x7DCA0,
        0x7E080, 0x7E0A0, 0x7E440, 0x7E480, 0x7E4A0, 0x7E880, 0x7E8A0, 0x7EC40, 0x7EC80, 0x7ECA0, 0x7ED40,
        0x7F040, 0x7F080, 0x7F0A0, 0x7F140, 0x7F440
    ];
    private static readonly HashSet<int> DarkHollowGpuObserved8BitPaletteOffsets =
    [
        0x2400, 0x2800, 0x2C00, 0x3000, 0x3400, 0x4000, 0x5800,
        0x9400, 0x9C00, 0xA400, 0xA800, 0xB800, 0xC800
    ];

    // Additional live CLUT rows observed across Dark Hollow's outdoor, midrange, and indoor GPU captures.
    private static readonly int[] DarkHollowGpuObservedAdditional4BitPaletteOffsets =
    [
        0x68040, 0x687A0, 0x69440, 0x6B7A0, 0x6BBA0, 0x6BC20, 0x6BFA0, 0x6C0A0,
        0x6C120, 0x6C160, 0x6C4A0, 0x6C520, 0x6C540, 0x6C8A0, 0x6C920, 0x6C940,
        0x6CCE0, 0x6CD20, 0x6CD40, 0x6D120, 0x6D140, 0x6D520, 0x6D920, 0x6D940,
        0x6DD20, 0x6DD40, 0x6E120, 0x6E140, 0x6E520, 0x6E540, 0x6E920, 0x6ED20,
        0x6ED40, 0x6F120, 0x6F140, 0x6F520, 0x6F540, 0x6F8E0, 0x6F920, 0x6F940,
        0x6FCC0, 0x6FD20, 0x6FD40, 0x700A0, 0x70100, 0x70820, 0x70D20, 0x71120,
        0x71140, 0x71400, 0x71420, 0x71C20, 0x71D60, 0x720C0, 0x72140, 0x72520,
        0x72560, 0x72960, 0x730A0, 0x73160, 0x734A0, 0x73560, 0x738A0, 0x738C0,
        0x73CA0, 0x73D60, 0x74040, 0x74060, 0x74120, 0x74140, 0x744C0, 0x74520,
        0x74920, 0x74D20, 0x75120, 0x75140, 0x754A0, 0x75520, 0x75540, 0x758A0,
        0x75920, 0x75940, 0x75C00, 0x75C20, 0x75CA0, 0x75D20, 0x75D40, 0x75D80,
        0x76000, 0x76020, 0x76040, 0x76060, 0x760A0, 0x76120, 0x76140, 0x76180,
        0x764A0, 0x764C0, 0x76520, 0x768A0, 0x76920, 0x76940, 0x76CC0, 0x76D20,
        0x76D40, 0x77120, 0x77140, 0x77180, 0x774C0, 0x77520, 0x77540, 0x778E0,
        0x77920, 0x77980, 0x77CA0, 0x77CC0, 0x77CE0, 0x77D60, 0x78000, 0x78080,
        0x78120, 0x78140, 0x78520, 0x78540, 0x787E0, 0x78940, 0x78BE0, 0x78D20,
        0x78D40, 0x78FE0, 0x79120, 0x79140, 0x793E0, 0x79480, 0x797E0, 0x79880,
        0x79920, 0x79940, 0x79BE0, 0x79D20, 0x79D40, 0x79FE0, 0x7A080, 0x7A140,
        0x7A3E0, 0x7A480, 0x7A520, 0x7A540, 0x7A7E0, 0x7A880, 0x7A920, 0x7ABE0,
        0x7AC80, 0x7AD20, 0x7AD40, 0x7AFE0, 0x7B080, 0x7B120, 0x7B140, 0x7B3E0,
        0x7B480, 0x7B540, 0x7B7E0, 0x7B860, 0x7B880, 0x7B920, 0x7B940, 0x7B980,
        0x7BBE0, 0x7BC80, 0x7BD40, 0x7BD80, 0x7C040, 0x7C060, 0x7C0C0, 0x7C0E0,
        0x7C100, 0x7C120, 0x7C520, 0x7C920, 0x7CD00, 0x7CD20, 0x7D100, 0x7D120,
        0x7D4E0, 0x7D500, 0x7D520, 0x7D800, 0x7D820, 0x7D8C0, 0x7D900, 0x7D920,
        0x7DC40, 0x7DC60, 0x7DCC0, 0x7DCE0, 0x7DD00, 0x7DD20, 0x7E000, 0x7E040,
        0x7E060, 0x7E0C0, 0x7E0E0, 0x7E100, 0x7E120, 0x7E400, 0x7E500, 0x7E520,
        0x7E8C0, 0x7E8E0, 0x7E900, 0x7E920, 0x7EC00, 0x7ECC0, 0x7ECE0, 0x7ED00,
        0x7ED20, 0x7F000, 0x7F0C0, 0x7F0E0, 0x7F100, 0x7F400, 0x7F460, 0x7F480,
        0x7F4C0, 0x7F4E0, 0x7F500, 0x7F880, 0x7F8A0, 0x7F8C0, 0x7F8E0, 0x7F900,
        0x7FC00, 0x7FC40, 0x7FC80, 0x7FCA0, 0x7FCC0, 0x7FCE0, 0x7FD00, 0x7FD20,
        0x778C0, 0x6FCE0, 0x73CC0, 0x6E960, 0x7FC60, 0x6E560, 0x69C40, 0x7F520,
        0x7F4A0, 0x79980, 0x6F8C0, 0x6F4C0, 0x6ECC0, 0x73960, 0x6E8C0, 0x77580,
        0x7E540, 0x7F920, 0x7F120, 0x76CA0, 0x71960, 0x75980,
    ];

    private static readonly HashSet<int> DarkHollowProtectedPlayerPaletteOffsets =
    [
        0x687A0, 0x6B7A0, 0x6BBA0, 0x6BFA0,
        0x787E0, 0x78BE0, 0x78FE0, 0x793E0, 0x797E0, 0x79BE0, 0x79FE0,
        0x7A3E0, 0x7A7E0, 0x7ABE0, 0x7AFE0, 0x7B3E0, 0x7B7E0
    ];

    private static readonly IReadOnlyDictionary<int, string> DarkHollowCloseTreePaletteSha256 =
        new Dictionary<int, string>
        {
            [0x7C460] = "3EF169D671DD5F3D84BFEAF2F5FD1388D0E17EF4048D986FE84979AE5F3494A7",
            [0x7CC60] = "D9E70917444CAA9F244E54807A02F8A1A8269077813908CA6EE8E0F95120B291",
            [0x7D060] = "C5CDABB0610F79ABCA0AA864833CA68C9582365DE727168A4AD820E8EEF5A360",
            [0x7D460] = "E18CEAC716D0D8B868DCE9D952B6F6942E9CCCF4038649E133E519B5F8BADA33",
            [0x7E440] = "5AE1DEFB401F8307BE30FF58EE84A76450A3CD0C3C883B8203436C8147E86B99",
            [0x7EC40] = "97178BB30CD90B42916A9DD8849BB207DC55CB37E0994BABD5CDB1695FF8F1FF",
            [0x7F040] = "5FB93A92D570CCC907B76C1829995586A63BA4A28C97A7D0EB8936F0E75AABDB",
            [0x7F440] = "144F2F14015E653DE9024F1A4AF2E5BB255427D65A2B352BAE0D8A3721F1EE69"
        };

    // GPU captures tie these untextured color tables to Dark Hollow's three far-tree model variants.
    private static readonly DarkHollowSceneryColorTableSpec[] DarkHollowFarTreeColorTableSpecs =
    [
        new(0x129FC, 44, "tree-variant-1", "3D2CE20BF4A01D78B944D9DB5EC43C092049E2FC209CF91004C500B6FC94F431"),
        new(0x16624, 52, "tree-variant-2", "F817FBC3BDB7F3B61A87247AFCE3D799F20878815C154466B5FDCA6C85C400BF"),
        new(0x16D9C, 41, "tree-variant-3", "FAC5789C3CD68EB7DC6552468392113709B75BD073C6C1B17B722C129880FB78")
    ];

    public static async Task<NativeEnvironmentGradePatchResult> ExportBatchAsync(
        NativeEnvironmentGradeBatchPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        (NativeEnvironmentGradePatchPlan plan, IReadOnlyList<GradePayload> payloads) = BuildPlanAndPayloads(request);
        string outputPlanPath = $"{request.OutputPrefix}.environment-grade-patch-plan.json";
        ValidateOutputPaths(request, plan, outputPlanPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        string temporarySuffix = $".{Guid.NewGuid():N}.tmp";
        string temporaryPlanPath = outputPlanPath + temporarySuffix;
        string temporaryImagePath = plan.OutputImagePath + temporarySuffix;
        string temporaryCuePath = plan.OutputCuePath + temporarySuffix;
        bool wroteImage = request.WriteImage && payloads.Count > 0;
        try
        {
            await using (FileStream output = File.Create(temporaryPlanPath))
            {
                await JsonSerializer.SerializeAsync(output, plan, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            if (wroteImage)
            {
                File.Copy(request.SourceImagePath, temporaryImagePath, true);
                DiscLayout layout = DiscImage.DetectLayout(temporaryImagePath);
                await using (FileStream image = File.Open(temporaryImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    NativeAssetCatalog copiedAssets = LoadAssetCatalog(request.WadAnalysisPath);
                    ValidateAssetCatalogAgainstSource(image, layout, copiedAssets);
                    foreach (GradePayload payload in payloads)
                    {
                        byte[] actualBefore = DiscImage.ReadFileBytes(
                            image,
                            layout,
                            payload.FileLba,
                            payload.FileOffset,
                            payload.Before.Length);
                        if (!actualBefore.AsSpan().SequenceEqual(payload.Before))
                        {
                            throw new InvalidOperationException(
                                $"Environment-grade source bytes changed before '{payload.Label}' could be written. Rebuild the plan from the selected clean source image.");
                        }
                        DiscImage.WriteFileBytes(image, layout, payload.FileLba, payload.FileOffset, payload.After);
                    }
                    await image.FlushAsync(cancellationToken);
                }
                string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(plan.OutputImagePath));
                await File.WriteAllTextAsync(temporaryCuePath, cueText, Encoding.ASCII, cancellationToken);
            }
            List<(string TemporaryPath, string FinalPath)> stagedOutputs =
            [
                (temporaryPlanPath, outputPlanPath)
            ];
            if (wroteImage)
            {
                stagedOutputs.Insert(0, (temporaryCuePath, plan.OutputCuePath));
                stagedOutputs.Insert(0, (temporaryImagePath, plan.OutputImagePath));
            }
            PublishStagedOutputs(stagedOutputs, temporarySuffix);
        }
        finally
        {
            DeleteTemporaryOutput(temporaryPlanPath);
            DeleteTemporaryOutput(temporaryImagePath);
            DeleteTemporaryOutput(temporaryCuePath);
        }

        return new NativeEnvironmentGradePatchResult(
            plan.OutputImagePath,
            plan.OutputCuePath,
            outputPlanPath,
            plan,
            wroteImage);
    }

    private static void DeleteTemporaryOutput(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void PublishStagedOutputs(
        IReadOnlyList<(string TemporaryPath, string FinalPath)> outputs,
        string temporarySuffix)
    {
        List<(string FinalPath, string BackupPath)> backups = [];
        List<string> published = [];
        try
        {
            foreach ((_, string finalPath) in outputs)
            {
                if (!File.Exists(finalPath))
                    continue;
                string backupPath = finalPath + temporarySuffix + ".bak";
                File.Move(finalPath, backupPath);
                backups.Add((finalPath, backupPath));
            }
            foreach ((string temporaryPath, string finalPath) in outputs)
            {
                File.Move(temporaryPath, finalPath);
                published.Add(finalPath);
            }
        }
        catch (Exception publishException)
        {
            List<Exception> rollbackExceptions = [];
            foreach (string finalPath in published.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);
                }
                catch (Exception rollbackException)
                {
                    rollbackExceptions.Add(rollbackException);
                }
            }
            foreach ((string finalPath, string backupPath) in backups.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(backupPath))
                        File.Move(backupPath, finalPath, true);
                }
                catch (Exception rollbackException)
                {
                    rollbackExceptions.Add(rollbackException);
                }
            }
            if (rollbackExceptions.Count > 0)
            {
                throw new AggregateException(
                    "Environment-grade output publication failed and one or more previous artifacts could not be restored. Retained .bak files are recovery copies.",
                    [publishException, .. rollbackExceptions]);
            }
            throw;
        }

        foreach ((_, string backupPath) in backups)
            DeleteTemporaryOutput(backupPath);
    }

    private static void ValidateOutputPaths(
        NativeEnvironmentGradeBatchPatchRequest request,
        NativeEnvironmentGradePatchPlan plan,
        string outputPlanPath)
    {
        (string Label, string Path)[] inputs =
        [
            ("source BIN", request.SourceImagePath),
            ("source CUE", request.SourceCuePath),
            ("WAD analysis", request.WadAnalysisPath)
        ];
        (string Label, string Path)[] outputs =
        [
            ("output BIN", plan.OutputImagePath),
            ("output CUE", plan.OutputCuePath),
            ("output plan", outputPlanPath)
        ];
        foreach ((string inputLabel, string inputPath) in inputs)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                continue;
            foreach ((string outputLabel, string outputPath) in outputs)
            {
                if (!PathsEqual(inputPath, outputPath))
                    continue;
                throw new InvalidOperationException(
                    $"The environment-grade {outputLabel} cannot replace the selected {inputLabel}. Choose a different output prefix.");
            }
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }

    public static NativeEnvironmentGradePatchPlan BuildPlan(NativeEnvironmentGradeBatchPatchRequest request) =>
        BuildPlanAndPayloads(request).Plan;

    public static NativeEnvironmentGradeMatch AnalyzeMatch(
        string sourceImagePath,
        string wadAnalysisPath,
        LevelDefinition target,
        LevelDefinition donor,
        NativeEnvironmentGradePlan grade)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        ValidateGradeScope(target, grade);
        NativeAssetCatalog assets = LoadAssetCatalog(wadAnalysisPath);
        DiscLayout discLayout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        ValidateAssetCatalogAgainstSource(image, discLayout, assets);
        LevelColorData targetData = ReadLevelColorData(image, discLayout, assets, target);
        LevelColorData donorData = ReadLevelColorData(image, discLayout, assets, donor);
        return BuildMatch(target, donor, targetData, donorData, grade.Normalize(donor.Key));
    }

    private static (NativeEnvironmentGradePatchPlan Plan, IReadOnlyList<GradePayload> Payloads) BuildPlanAndPayloads(
        NativeEnvironmentGradeBatchPatchRequest request)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", request.WadAnalysisPath);

        NativeEnvironmentGradeBatchEdit[] enabledEdits = request.Edits
            .Where(edit => edit.Grade.Enabled)
            .DistinctBy(edit => LevelCatalog.NormalizeKey(edit.Level.Key), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (enabledEdits.Length == 0)
            throw new InvalidOperationException("No enabled environment grades were provided.");

        NativeAssetCatalog assets = LoadAssetCatalog(request.WadAnalysisPath);
        DiscLayout discLayout = DiscImage.DetectLayout(request.SourceImagePath);
        using FileStream image = File.Open(request.SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        ValidateAssetCatalogAgainstSource(image, discLayout, assets);
        List<NativeEnvironmentGradeMatch> matches = [];
        List<NativeEnvironmentGradePatch> patches = [];
        List<GradePayload> payloads = [];
        GradeWriteRangeTracker writtenRanges = new();
        Dictionary<int, LevelColorData> colorDataByWadEntry = new();
        Dictionary<int, uint> mobyMaterialByLevelId = new();

        LevelColorData GetColorData(LevelDefinition level)
        {
            if (!colorDataByWadEntry.TryGetValue(level.SourceWadEntry, out LevelColorData? data))
            {
                data = ReadLevelColorData(image, discLayout, assets, level);
                colorDataByWadEntry[level.SourceWadEntry] = data;
            }
            return data;
        }

        foreach (NativeEnvironmentGradeBatchEdit edit in enabledEdits)
        {
            NativeEnvironmentGradePlan grade = edit.Grade.Normalize();
            ValidateGradeScope(edit.Level, grade);
            LevelDefinition donor = request.Catalog.FindByKey(grade.DonorLevelKey)
                ?? throw new InvalidOperationException($"{edit.Level.DisplayName} does not have a valid environment-grade donor.");
            if (string.Equals(LevelCatalog.NormalizeKey(donor.Key), LevelCatalog.NormalizeKey(edit.Level.Key), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The environment-grade donor must be a different level.");

            LevelColorData targetData = GetColorData(edit.Level);
            LevelColorData donorData = GetColorData(donor);
            if (grade.GradeTexturePalettes)
                ValidateDarkHollowCloseTreePaletteSources(edit.Level, targetData.TexturePalettes);
            NativeEnvironmentGradeMatch match = BuildMatch(edit.Level, donor, targetData, donorData, grade);
            matches.Add(match);

            if (grade.GradeSceneColors)
            {
                foreach (SceneColorTable table in targetData.SceneColorTables)
                {
                    byte[] before = table.Bytes;
                    byte[] after = TransformSceneColorTable(
                        before,
                        table,
                        match.SceneTransform,
                        smoothTerrain: true);
                    AddPatch(
                        edit.Level,
                        donor,
                        assets.WadLba,
                        targetData.ModelSubfile.WadOffset + table.Offset,
                        table.Detail == "lp" ? "environment-scene-lp-colors" : "environment-scene-hp-colors",
                        $"sector-{table.SectorIndex}-{table.Detail}",
                        table.ColorCount * table.RgbOffsets.Count,
                        before,
                        after,
                        discLayout,
                        writtenRanges,
                        patches,
                        payloads);
                }

                foreach (SceneryColorTable table in targetData.SceneryColorTables)
                {
                    byte[] before = table.Bytes;
                    string beforeHash = Hash(before);
                    if (!string.Equals(beforeHash, table.ExpectedBeforeSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Dark Hollow's {table.Label} scenery color table does not match the validated USA source bytes. " +
                            $"Expected {table.ExpectedBeforeSha256}, got {beforeHash}.");
                    }
                    byte[] after = TransformSceneryColorTable(before, table, match.SceneTransform);
                    AddPatch(
                        edit.Level,
                        donor,
                        assets.WadLba,
                        targetData.SceneryModelSubfile!.WadOffset + table.Offset,
                        "environment-scenery-lod-colors",
                        table.Label,
                        table.ColorCount,
                        before,
                        after,
                        discLayout,
                        writtenRanges,
                        patches,
                        payloads);
                }
            }

            if (grade.GradeTexturePalettes)
            {
                AddTexturePalettePatches(
                    edit.Level,
                    donor,
                    assets.WadLba,
                    targetData,
                    match.TextureTransform,
                    discLayout,
                    writtenRanges,
                    patches,
                    payloads);
            }

            if (grade.GradeAnyMobys)
            {
                if (edit.Level.LevelId is < 0 or > MaxLevelId)
                    throw new InvalidOperationException($"{edit.Level.DisplayName}'s level id cannot use the guarded Moby material table.");
                if (!ColorRgba.TryParseHex(match.MobyMaterialColorHex, out ColorRgba material))
                    throw new InvalidOperationException($"{edit.Level.DisplayName}'s Moby environment material is invalid.");
                mobyMaterialByLevelId[edit.Level.LevelId] = EncodeRgbWord(material);
            }
        }

        if (mobyMaterialByLevelId.Count > 0)
        {
            AddMobyRuntimePatches(
                image,
                discLayout,
                mobyMaterialByLevelId,
                writtenRanges,
                patches,
                payloads);
        }

        string outputImagePath = $"{request.OutputPrefix}.bin";
        string outputCuePath = $"{request.OutputPrefix}.cue";
        NativeEnvironmentGradePatchPlan plan = new(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: request.SourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            WadAnalysisPath: request.WadAnalysisPath,
            EditedLevelCount: matches.Count,
            PatchCount: patches.Count,
            SceneColorPatchCount: patches.Count(patch => patch.Kind.StartsWith("environment-scene-", StringComparison.OrdinalIgnoreCase)),
            TexturePalettePatchCount: patches.Count(patch => patch.Kind == "environment-terrain-texture-palette"),
            MobyMaterialRowPatchCount: patches.Count(patch => patch.Kind == "environment-moby-material-route"),
            MobyRuntimePatchCount: patches.Count(patch => patch.Kind.StartsWith("environment-moby-runtime-", StringComparison.OrdinalIgnoreCase)),
            TotalColorCount: patches.Sum(patch => patch.ColorCount),
            TotalWrittenBytes: patches.Sum(patch => patch.ByteLength),
            TotalChangedBytes: patches.Sum(patch => patch.ChangedByteCount),
            Matches: matches,
            Patches: patches,
            SafetyNotes:
            [
                "The grade transforms the target level's own color tables; donor geometry, texture indexes, actor models, and behavior packages are not copied.",
                "Low-detail and high-detail scene colors use one identical transform and smoothing curve so runtime terrain-sector LOD swaps cannot introduce a new hue or brightness seam.",
                "Large donor hue or saturation shifts use one luminance-preserving affine harmonization across every low-detail and high-detail sector, reducing source-sector color breaks without flattening native shading.",
                "Drastic shifts fully normalize both scene and texture chroma because PS1 texture and vertex colors multiply at render time; the target level's native luminance, shading, and texture detail remain intact.",
                "Advanced tint strength is divided across scene and texture layers so their multiplicative runtime result matches the requested tint instead of applying it twice; native object lighting keeps its independently selected material grade.",
                "Both contiguous four-byte high-detail color banks are transformed; their command bytes remain unchanged.",
                "Landscape palettes use the same shadow-protected tonal curve to avoid exposing large terrain triangles after aggressive dark grades.",
                "Both low-detail and high-detail scene color tables are patched only when every native color-command byte validates as 0x00 or 0x30.",
                "Texture grading covers only packed PS1 CLUT sources referenced by the decoded scene and exact Dark Hollow LOD rows proven by GPU captures; the legacy palette-code-times-32 address is never written because it can point into texture pixels.",
                "Four-bit landscape CLUTs write exactly 32 bytes and eight-bit CLUTs write exactly 512 bytes, preventing palette transforms from spilling into neighboring texture data.",
                "Dark Hollow inferred 512-byte rows are reduced to 32 bytes when another decoded palette starts inside the range; only GPU-proven 8-bit rows retain 512-byte writes.",
                "GPU-captured Spyro player palettes are explicitly excluded from Dark Hollow environment grading.",
                "Dark Hollow's GPU-proven close-tree palettes and three far/untextured tree color tables receive the same grade, preventing green/gray color changes across scenery LOD distance swaps.",
                "Object lighting changes the game's existing neutral material-0 entry per level; no object row is rerouted and the invalid reserved-material path is never used.",
                "Native material-1 and material-2 objects keep their original special lighting, preserving gems and other emissive or reflective objects.",
                "Every ungraded level id explicitly restores neutral material 0x00808080 so an environment grade cannot leak across a portal transition.",
                "Transparent PS1 palette entries and semi-transparency bits are preserved.",
                "Every patch is fixed-size, so WAD layout and executable locations remain unchanged before any separate oversized-sky relocation step."
            ]);
        return (plan, payloads);
    }

    private static void ValidateGradeScope(LevelDefinition target, NativeEnvironmentGradePlan grade)
    {
        NativeEnvironmentGradePlan normalized = grade.Normalize();
        if (!normalized.Enabled ||
            !string.Equals(LevelCatalog.NormalizeKey(target.Key), "darkhollow", StringComparison.OrdinalIgnoreCase) ||
            normalized.GradeSceneColors == normalized.GradeTexturePalettes)
        {
            return;
        }

        throw new InvalidOperationException(
            "Dark Hollow's Landscape lighting and Landscape texture palettes must be enabled together. " +
            "Its close-tree palettes and far/untextured RGB0 tables are one atomic LOD grade.");
    }

    private static NativeEnvironmentGradeMatch BuildMatch(
        LevelDefinition target,
        LevelDefinition donor,
        LevelColorData targetData,
        LevelColorData donorData,
        NativeEnvironmentGradePlan grade)
    {
        NativeEnvironmentColorStatistics targetScene = NativeEnvironmentColorStatistics.FromColors(targetData.SceneColors);
        NativeEnvironmentColorStatistics donorScene = NativeEnvironmentColorStatistics.FromColors(donorData.SceneColors);
        NativeEnvironmentColorStatistics targetLowDetailScene = SceneColorStatistics(targetData, "lp");
        NativeEnvironmentColorStatistics donorLowDetailScene = SceneColorStatistics(donorData, "lp");
        NativeEnvironmentColorStatistics targetHighDetailScene = SceneColorStatistics(targetData, "hp");
        NativeEnvironmentColorStatistics donorHighDetailScene = SceneColorStatistics(donorData, "hp");
        NativeEnvironmentColorStatistics targetTextures = NativeEnvironmentColorStatistics.FromColors(targetData.TextureColors);
        NativeEnvironmentColorStatistics donorTextures = NativeEnvironmentColorStatistics.FromColors(donorData.TextureColors);
        NativeEnvironmentColorTransform sceneTransform = NativeEnvironmentColorTransform.Build(targetScene, donorScene, grade);
        NativeEnvironmentColorTransform textureTransform = targetTextures.SampleCount > 0 && donorTextures.SampleCount > 0
            ? NativeEnvironmentColorTransform.Build(targetTextures, donorTextures, grade)
            : sceneTransform;
        if (grade.GradeSceneColors && grade.GradeTexturePalettes && sceneTransform.HarmonizationPercent >= 20)
        {
            int sharedHarmonizationFloor = sceneTransform.HarmonizationPercent >= 70
                ? 100
                : sceneTransform.HarmonizationPercent;
            sceneTransform = sceneTransform with
            {
                HarmonizationPercent = Math.Max(
                    sceneTransform.HarmonizationPercent,
                    sharedHarmonizationFloor)
            };
            textureTransform = textureTransform with
            {
                HarmonizationPercent = Math.Max(
                    textureTransform.HarmonizationPercent,
                    sharedHarmonizationFloor)
            };
        }
        NativeEnvironmentColorTransform mobyTransform = sceneTransform;
        if (grade.GradeSceneColors && grade.GradeTexturePalettes && grade.TintStrengthPercent > 0)
        {
            int perLayerTint = CompoundLayerTintStrength(grade.TintStrengthPercent, 2);
            sceneTransform = sceneTransform with { TintStrengthPercent = perLayerTint };
            textureTransform = textureTransform with { TintStrengthPercent = perLayerTint };
        }
        ColorRgba mobyMaterial = mobyTransform.Apply(ColorRgba.FromRgb(128, 128, 128));
        return new NativeEnvironmentGradeMatch(
            TargetLevelKey: target.Key,
            TargetLevelName: target.DisplayName,
            DonorLevelKey: donor.Key,
            DonorLevelName: donor.DisplayName,
            TargetSectorCount: targetData.Sectors.Count,
            DonorSectorCount: donorData.Sectors.Count,
            TargetSceneColorTableCount: targetData.SceneColorTables.Count,
            DonorSceneColorTableCount: donorData.SceneColorTables.Count,
            TargetTextureIdCount: targetData.TextureIds.Count,
            DonorTextureIdCount: donorData.TextureIds.Count,
            TargetTexturePaletteCount: targetData.TexturePalettes.Count,
            DonorTexturePaletteCount: donorData.TexturePalettes.Count,
            TargetTextureRuntimeVariantCount: targetData.TexturePalettes.Count(table => table.IsRuntimeVariant),
            DonorTextureRuntimeVariantCount: donorData.TexturePalettes.Count(table => table.IsRuntimeVariant),
            TargetActorMobyCount: targetData.MobyMaterialRows.Count(row => row.Kind == MobyVisualKind.Actor && row.MaterialId == 0),
            TargetChestMobyCount: targetData.MobyMaterialRows.Count(row => row.Kind == MobyVisualKind.Chest && row.MaterialId == 0),
            TargetSceneryMobyCount: targetData.MobyMaterialRows.Count(row => row.Kind == MobyVisualKind.Scenery && row.MaterialId == 0),
            TargetDragonMobyCount: targetData.MobyMaterialRows.Count(row => row.Kind == MobyVisualKind.Dragon && row.MaterialId == 0),
            MobyMaterialColorHex: $"#{mobyMaterial.R:X2}{mobyMaterial.G:X2}{mobyMaterial.B:X2}",
            TargetSceneColors: targetScene,
            DonorSceneColors: donorScene,
            TargetLowDetailSceneColors: targetLowDetailScene,
            DonorLowDetailSceneColors: donorLowDetailScene,
            TargetHighDetailSceneColors: targetHighDetailScene,
            DonorHighDetailSceneColors: donorHighDetailScene,
            TargetTextureColors: targetTextures,
            DonorTextureColors: donorTextures,
            TargetTextureUsage: targetData.TextureUsage,
            DonorTextureUsage: donorData.TextureUsage,
            SceneTransform: sceneTransform,
            LowDetailSceneTransform: sceneTransform,
            HighDetailSceneTransform: sceneTransform,
            TextureTransform: textureTransform);
    }

    private static int CompoundLayerTintStrength(int totalTintPercent, int layerCount)
    {
        double total = Math.Clamp(totalTintPercent, 0, 100) / 100.0;
        int count = Math.Max(1, layerCount);
        return (int)Math.Round((1.0 - Math.Pow(1.0 - total, 1.0 / count)) * 100.0);
    }

    private static NativeEnvironmentColorStatistics SceneColorStatistics(LevelColorData data, string detail) =>
        NativeEnvironmentColorStatistics.FromColors(
            data.SceneColorTables
                .Where(table => table.Detail == detail)
                .SelectMany(table => ReadSceneColors(table.Bytes, table)));

    private static LevelColorData ReadLevelColorData(
        FileStream image,
        DiscLayout discLayout,
        NativeAssetCatalog assets,
        LevelDefinition level)
    {
        if (!assets.Levels.TryGetValue(level.SourceWadEntry, out LevelAssetLayout? asset))
            throw new InvalidOperationException($"{level.DisplayName} is missing native texture/model subfiles.");
        if (string.Equals(LevelCatalog.NormalizeKey(level.Key), "darkhollow", StringComparison.OrdinalIgnoreCase) &&
            asset.SceneryModels == null)
        {
            throw new InvalidOperationException(
                "Dark Hollow environment grading requires scenery subfile 2 from an analysis built for the selected source image. Rebuild the WAD analysis before saving or exporting this grade.");
        }
        byte[] modelBytes = DiscImage.ReadFileBytes(image, discLayout, assets.WadLba, asset.Model.WadOffset, asset.Model.Size);
        byte[] textureBytes = DiscImage.ReadFileBytes(image, discLayout, assets.WadLba, asset.TexturePages.WadOffset, asset.TexturePages.Size);
        byte[]? sceneryModelBytes = asset.SceneryModels == null
            ? null
            : DiscImage.ReadFileBytes(image, discLayout, assets.WadLba, asset.SceneryModels.WadOffset, asset.SceneryModels.Size);
        IReadOnlyList<SceneSector> sectors = FindBestSceneChain(modelBytes);
        if (sectors.Count == 0)
            throw new InvalidOperationException($"Could not decode {level.DisplayName}'s landscape scene sectors.");

        List<SceneColorTable> colorTables = [];
        HashSet<int> textureIds = [];
        foreach (SceneSector sector in sectors)
        {
            AddSceneColorTables(modelBytes, sector, colorTables);
            AddSceneTextureIds(modelBytes, sector, textureIds);
        }
        if (colorTables.Count == 0)
            throw new InvalidOperationException($"Could not decode {level.DisplayName}'s landscape color tables.");

        IReadOnlyList<TexturePaletteTable> texturePalettes = ReadSceneTexturePalettes(modelBytes, textureBytes, textureIds, level.Key);
        IReadOnlyList<SceneryColorTable> sceneryColorTables = sceneryModelBytes == null
            ? Array.Empty<SceneryColorTable>()
            : ReadDarkHollowFarTreeColorTables(sceneryModelBytes, level.Key);
        NativeEnvironmentTextureUsageStatistics textureUsage = ReadSceneTextureUsage(modelBytes, textureBytes, textureIds);
        IReadOnlyList<MobyMaterialRow> mobyMaterialRows = ReadMobyMaterialRows(image, discLayout, assets.WadLba, level);
        ColorRgba[] sceneColors = colorTables
            .SelectMany(table => ReadSceneColors(table.Bytes, table))
            .ToArray();
        ColorRgba[] textureColors = texturePalettes
            .SelectMany(table => ReadTextureColors(table.Bytes))
            .ToArray();
        return new LevelColorData(
            asset.Model,
            asset.TexturePages,
            asset.SceneryModels,
            sectors,
            colorTables,
            sceneryColorTables,
            textureIds,
            texturePalettes,
            mobyMaterialRows,
            sceneColors,
            textureColors,
            textureUsage);
    }

    private static IReadOnlyList<MobyMaterialRow> ReadMobyMaterialRows(
        FileStream image,
        DiscLayout discLayout,
        int wadLba,
        LevelDefinition level)
    {
        if (!level.HasSourceTable || !TryParseLong(level.SourceTableWadOffset, out long tableWadOffset))
            return Array.Empty<MobyMaterialRow>();

        byte[] countBytes = DiscImage.ReadFileBytes(image, discLayout, wadLba, tableWadOffset - 4, 4);
        int count = BinaryPrimitives.ReadInt32LittleEndian(countBytes);
        if (count is <= 0 or > 1024)
            count = level.SourceRecordCount;
        if (count <= 0)
            return Array.Empty<MobyMaterialRow>();

        List<MobyMaterialRow> rows = [];
        for (int trueIndex = 0; trueIndex < count; trueIndex++)
        {
            long rowWadOffset = tableWadOffset + ((long)trueIndex * MobyRecordStride);
            byte[] record = DiscImage.ReadFileBytes(image, discLayout, wadLba, rowWadOffset, MobyRecordStride);
            int type = record[0x50];
            int state = record[0x51];
            int yawByte = Moby.TryMatrixToYawByte(
                BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(0x20, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(record.AsSpan(0x24, 2)),
                out int decodedYaw)
                ? decodedYaw
                : -1;
            Vector3f position = new(
                BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x0C, 4)) / 16f,
                BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x10, 4)) / 16f,
                BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x14, 4)) / 16f);
            Moby moby = new()
            {
                Index = trueIndex,
                TrueIndex = trueIndex,
                Position = position,
                OriginalPosition = position,
                Type = type,
                OriginalType = type,
                State = state,
                OriginalState = state,
                YawByte = yawByte,
                OriginalYawByte = yawByte,
                SourceByte36 = record[0x36],
                OriginalSourceByte36 = record[0x36],
                SourceByte37 = record[0x37],
                OriginalSourceByte37 = record[0x37],
                SourceByte4F = record[MobyMaterialOffset],
                OriginalSourceByte4F = record[MobyMaterialOffset],
                Flag4A = record[0x52],
                OriginalFlag4A = record[0x52],
                Flag4B = record[0x53],
                OriginalFlag4B = record[0x53],
                Color = Moby.ColorForType(type),
                Label = $"Type 0x{type:X2}",
                OriginalLabel = $"Type 0x{type:X2}"
            };
            MobyIdentityClassifier.Apply(level.Key, [moby]);
            rows.Add(new MobyMaterialRow(
                TrueIndex: trueIndex,
                WadOffset: rowWadOffset,
                Kind: moby.VisualKind,
                Label: moby.DisplayLabel,
                MaterialId: record[MobyMaterialOffset]));
        }

        return rows;
    }

    private static IReadOnlyList<SceneSector> FindBestSceneChain(byte[] bytes)
    {
        Dictionary<int, SceneSector> valid = new();
        for (int offset = 0; offset <= bytes.Length - 28; offset += 4)
        {
            SceneSector? sector = TryReadSceneSector(bytes, offset);
            if (sector != null)
                valid[offset] = sector;
        }

        List<SceneSector> best = [];
        foreach (int start in valid.Keys.Order())
        {
            List<SceneSector> chain = [];
            int offset = start;
            int guard = 0;
            while (valid.TryGetValue(offset, out SceneSector? sector) && guard++ < 4096)
            {
                chain.Add(sector with { SectorIndex = chain.Count });
                offset += sector.SizeBytes;
            }
            if (!chain.Any(sector => HasValidatedColorTable(bytes, sector)))
                continue;
            if (chain.Count > best.Count || chain.Count == best.Count && chain.Sum(sector => sector.NumHpFaces) > best.Sum(sector => sector.NumHpFaces))
                best = chain;
        }
        return best;
    }

    private static SceneSector? TryReadSceneSector(byte[] bytes, int offset)
    {
        int numLpVertices = bytes[offset + 16];
        int numLpColours = bytes[offset + 17];
        int numLpFaces = bytes[offset + 18];
        int numHpVertices = bytes[offset + 20];
        int numHpColours = bytes[offset + 21];
        int numHpFaces = bytes[offset + 22];
        int sizeWords = 7 + numLpVertices + numLpColours + (numLpFaces * 2) + numHpVertices + (numHpColours * 2) + (numHpFaces * 4);
        int sizeBytes = sizeWords * 4;
        if (sizeBytes < 28 || sizeBytes > 0x40000 || offset + sizeBytes > bytes.Length)
            return null;
        if (numLpVertices + numHpVertices == 0 || numLpFaces + numHpFaces == 0)
            return null;
        if (numLpVertices + numHpVertices > 512 || numLpFaces + numHpFaces > 512)
            return null;

        SceneSector sector = new(
            Offset: offset,
            CentreRadiusAndFlags: ReadUInt16(bytes, offset + 4),
            XyPos: ReadUInt32(bytes, offset + 8),
            ZPos: ReadUInt32(bytes, offset + 12),
            SizeBytes: sizeBytes,
            NumLpVertices: numLpVertices,
            NumLpColours: numLpColours,
            NumLpFaces: numLpFaces,
            NumHpVertices: numHpVertices,
            NumHpColours: numHpColours,
            NumHpFaces: numHpFaces,
            SectorIndex: -1);
        Vector3f sample = ConvertSceneVertex(ReadUInt32(bytes, offset + 28), sector);
        return sample.X is >= 0 and <= 32768 && sample.Y is >= 0 and <= 32768 && sample.Z is >= 0 and <= 32768
            ? sector
            : null;
    }

    private static void AddSceneColorTables(byte[] modelBytes, SceneSector sector, List<SceneColorTable> result)
    {
        int dataStart = sector.Offset + 28;
        int lpStart = dataStart + (sector.NumLpVertices * 4);
        TryAddColorTable(modelBytes, sector.SectorIndex, "lp", lpStart, sector.NumLpColours, 4, [0], result);

        int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
        int hpColorStart = dataStart + ((hpVertexStartWords + sector.NumHpVertices) * 4);
        TryAddColorTable(
            modelBytes,
            sector.SectorIndex,
            "hp",
            hpColorStart,
            sector.NumHpColours * NativeTerrainHpColorLayout.TableCount,
            NativeTerrainHpColorLayout.ColorBytes,
            [0],
            result);
    }

    private static bool HasValidatedColorTable(byte[] modelBytes, SceneSector sector)
    {
        int dataStart = sector.Offset + 28;
        int lpStart = dataStart + (sector.NumLpVertices * 4);
        if (IsValidatedColorTable(modelBytes, lpStart, sector.NumLpColours, 4, [0]))
            return true;
        int hpVertexStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2);
        int hpColorStart = dataStart + ((hpVertexStartWords + sector.NumHpVertices) * 4);
        return IsValidatedColorTable(
            modelBytes,
            hpColorStart,
            sector.NumHpColours * NativeTerrainHpColorLayout.TableCount,
            NativeTerrainHpColorLayout.ColorBytes,
            [0]);
    }

    private static void TryAddColorTable(
        byte[] modelBytes,
        int sectorIndex,
        string detail,
        int offset,
        int count,
        int entryBytes,
        IReadOnlyList<int> rgbOffsets,
        List<SceneColorTable> result)
    {
        int byteLength = count * entryBytes;
        if (!IsValidatedColorTable(modelBytes, offset, count, entryBytes, rgbOffsets))
            return;
        result.Add(new SceneColorTable(
            SectorIndex: sectorIndex,
            Detail: detail,
            Offset: offset,
            EntryBytes: entryBytes,
            RgbOffsets: rgbOffsets,
            ColorCount: count,
            Bytes: modelBytes.AsSpan(offset, byteLength).ToArray()));
    }

    private static bool IsValidatedColorTable(
        byte[] modelBytes,
        int offset,
        int count,
        int entryBytes,
        IReadOnlyList<int> rgbOffsets)
    {
        int byteLength = count * entryBytes;
        if (count <= 0 || rgbOffsets.Count == 0 || offset < 0 || offset + byteLength > modelBytes.Length)
            return false;
        for (int index = 0; index < count; index++)
        {
            foreach (int rgbOffset in rgbOffsets)
            {
                int colorOffset = offset + (index * entryBytes) + rgbOffset;
                if (rgbOffset < 0 || rgbOffset + 4 > entryBytes ||
                    colorOffset + 4 > modelBytes.Length ||
                    modelBytes[colorOffset + 3] is not (0x00 or 0x30))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void AddSceneTextureIds(byte[] modelBytes, SceneSector sector, HashSet<int> result)
    {
        int dataStart = sector.Offset + 28;
        int hpFaceStartWords = sector.NumLpVertices + sector.NumLpColours + (sector.NumLpFaces * 2) + sector.NumHpVertices + (sector.NumHpColours * 2);
        for (int face = 0; face < sector.NumHpFaces; face++)
        {
            int faceOffset = dataStart + ((hpFaceStartWords + (face * 4)) * 4);
            if (faceOffset + 16 > modelBytes.Length)
                continue;
            result.Add((int)(ReadUInt32(modelBytes, faceOffset + 8) & 0x7F));
        }
    }

    private static IReadOnlyList<TexturePaletteTable> ReadSceneTexturePalettes(
        byte[] modelBytes,
        byte[] textureBytes,
        IReadOnlySet<int> textureIds,
        string levelKey)
    {
        int textureListSize = checked((int)ReadUInt32(modelBytes, 0));
        int textureCount = checked((int)ReadUInt32(modelBytes, 4));
        if (textureListSize <= 8 || textureCount <= 0 || textureListSize > modelBytes.Length || (textureListSize - 8) % textureCount != 0)
            return Array.Empty<TexturePaletteTable>();
        int recordBytes = (textureListSize - 8) / textureCount;
        if (recordBytes < 184)
            return Array.Empty<TexturePaletteTable>();

        Dictionary<int, TexturePaletteCandidate> paletteOffsets = [];
        void AddPaletteCandidate(int offset, int byteLength, bool isRuntimeVariant)
        {
            if (offset < 0 || byteLength <= 0 || offset + byteLength > textureBytes.Length)
                return;
            if (!paletteOffsets.TryGetValue(offset, out TexturePaletteCandidate? existing))
            {
                paletteOffsets[offset] = new TexturePaletteCandidate(byteLength, isRuntimeVariant);
                return;
            }
            paletteOffsets[offset] = new TexturePaletteCandidate(
                Math.Max(existing.ByteLength, byteLength),
                existing.IsRuntimeVariant && isRuntimeVariant);
        }

        foreach (int textureId in textureIds.Where(id => id >= 0 && id < textureCount))
        {
            int recordOffset = 8 + (textureId * recordBytes);
            foreach (TextureDescriptor descriptor in DecodeTextureDescriptors(modelBytes, recordOffset))
            {
                if (CanUseTextureDescriptor(descriptor, textureBytes.Length))
                {
                    int packedSourceOffset = DecodePackedPaletteByteStart(descriptor);
                    AddPaletteCandidate(
                        packedSourceOffset,
                        descriptor.PaletteByteLength,
                        isRuntimeVariant: true);
                }
            }
        }

        foreach (TexturePaletteCandidateRange observed in EnumerateGpuObservedPackedPaletteRanges(levelKey))
        {
            if (observed.Offset >= 0 && observed.Offset + observed.ByteLength <= textureBytes.Length)
                paletteOffsets[observed.Offset] = new TexturePaletteCandidate(observed.ByteLength, IsRuntimeVariant: true);
        }
        NormalizeDarkHollowPaletteCandidateLengths(levelKey, paletteOffsets);
        RemoveDarkHollowProtectedPaletteCandidates(levelKey, paletteOffsets);

        return paletteOffsets
            .OrderBy(pair => pair.Key)
            .Select(pair =>
            {
                int offset = pair.Key;
                byte[] bytes = textureBytes.AsSpan(offset, pair.Value.ByteLength).ToArray();
                return new TexturePaletteTable(
                    Offset: offset,
                    NonZeroColorCount: Enumerable.Range(0, bytes.Length / 2).Count(index => (ReadUInt16(bytes, index * 2) & 0x7FFF) != 0),
                    IsRuntimeVariant: pair.Value.IsRuntimeVariant,
                    Bytes: bytes);
            })
            .Where(table => table.NonZeroColorCount > 0)
            .ToArray();
    }

    private static NativeEnvironmentTextureUsageStatistics ReadSceneTextureUsage(
        byte[] modelBytes,
        byte[] textureBytes,
        IReadOnlySet<int> textureIds)
    {
        int textureListSize = checked((int)ReadUInt32(modelBytes, 0));
        int textureCount = checked((int)ReadUInt32(modelBytes, 4));
        if (textureListSize <= 8 || textureCount <= 0 || textureListSize > modelBytes.Length || (textureListSize - 8) % textureCount != 0)
            return new NativeEnvironmentTextureUsageStatistics(0, 0, 0, 0, 0, 0);
        int recordBytes = (textureListSize - 8) / textureCount;
        if (recordBytes < 184)
            return new NativeEnvironmentTextureUsageStatistics(0, 0, 0, 0, 0, 0);

        int descriptorCount = 0;
        int visibleTexelCount = 0;
        int greenDominantCount = 0;
        long redTotal = 0;
        long greenTotal = 0;
        long blueTotal = 0;
        foreach (int textureId in textureIds.Where(id => id >= 0 && id < textureCount))
        {
            int recordOffset = 8 + (textureId * recordBytes);
            foreach (TextureDescriptor descriptor in DecodeTextureDescriptors(modelBytes, recordOffset))
            {
                if (!CanUseTextureDescriptor(descriptor, textureBytes.Length))
                    continue;

                int paletteByteStart = DecodePackedPaletteByteStart(descriptor);
                if (paletteByteStart < 0)
                    continue;

                {
                    descriptorCount++;
                    int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
                    int startX = descriptor.VramXMin + (matrix[0] < 0 || matrix[1] < 0 ? 31 : 0);
                    int startY = descriptor.VramYMin + (matrix[2] < 0 || matrix[3] < 0 ? 31 : 0);
                    for (int y = 0; y < 32; y++)
                    {
                        for (int x = 0; x < 32; x++)
                        {
                            int sourceX = startX + (x * matrix[0]) + (y * matrix[1]);
                            int sourceY = startY + (x * matrix[2]) + (y * matrix[3]);
                            int texelOffset = checked((sourceY * 2048) + sourceX);
                            int colorIndex = descriptor.PaletteByteLength == TexturePalette16ColorByteLength
                                ? textureBytes[texelOffset] & 0x0F
                                : textureBytes[texelOffset];
                            int paletteOffset = paletteByteStart + (colorIndex * 2);
                            ushort word = ReadUInt16(textureBytes, paletteOffset);
                            if ((word & 0x7FFF) == 0)
                                continue;

                            ColorRgba color = DecodePsx555(word);
                            visibleTexelCount++;
                            redTotal += color.R;
                            greenTotal += color.G;
                            blueTotal += color.B;
                            if (color.G * 100 > color.R * 112 && color.G * 100 > color.B * 112)
                                greenDominantCount++;
                        }
                    }
                }
            }
        }

        if (visibleTexelCount == 0)
            return new NativeEnvironmentTextureUsageStatistics(descriptorCount, 0, 0, 0, 0, 0);
        return new NativeEnvironmentTextureUsageStatistics(
            DescriptorCount: descriptorCount,
            VisibleTexelCount: visibleTexelCount,
            MeanRed: redTotal / (255.0 * visibleTexelCount),
            MeanGreen: greenTotal / (255.0 * visibleTexelCount),
            MeanBlue: blueTotal / (255.0 * visibleTexelCount),
            GreenDominantPercent: greenDominantCount * 100.0 / visibleTexelCount);
    }

    private static IEnumerable<TextureDescriptor> DecodeTextureDescriptors(byte[] bytes, int recordOffset)
    {
        for (int index = 0; index < 4; index++)
            yield return DecodeTextureDescriptor(bytes, recordOffset + 24 + (index * 8), TexturePaletteByteLength);
        for (int index = 0; index < 16; index++)
            yield return DecodeTextureDescriptor(bytes, recordOffset + 56 + (index * 8), TexturePalette16ColorByteLength);
    }

    private static TextureDescriptor DecodeTextureDescriptor(byte[] bytes, int offset, int paletteByteLength)
    {
        int xmin = bytes[offset];
        int ymin = bytes[offset + 1];
        int palette = ReadUInt16(bytes, offset + 2);
        int xmax = bytes[offset + 4];
        int ymax = bytes[offset + 5];
        int region = bytes[offset + 6];
        int unknown = bytes[offset + 7];
        return new TextureDescriptor(
            PaletteCode: palette,
            PaletteByteStart: palette * 32,
            PaletteByteLength: paletteByteLength,
            Orientation: (unknown >> 4) & 7,
            VramXMin: ((region * 128) % 2048) + xmin,
            VramYMin: ((region & 0x1F) / 16 * 256) + ymin,
            VramXMax: ((region * 128) % 2048) + xmax,
            VramYMax: ((region & 0x1F) / 16 * 256) + ymax);
    }

    private static int DecodePackedPaletteByteStart(TextureDescriptor descriptor)
    {
        int clutX = (descriptor.PaletteCode & 0x3F) * 16;
        int clutY = (descriptor.PaletteCode >> 6) & 0x1FF;
        int rowByteOffset = (clutX - PackedTexturePageVramXOrigin) * 2;
        if (rowByteOffset < 0 || rowByteOffset + descriptor.PaletteByteLength > PackedTexturePageRowByteLength)
            return -1;
        return checked((clutY * PackedTexturePageRowByteLength) + rowByteOffset);
    }

    private static IEnumerable<TexturePaletteCandidateRange> EnumerateGpuObservedPackedPaletteRanges(string levelKey)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(levelKey), "darkhollow", StringComparison.OrdinalIgnoreCase))
            yield break;

        foreach (int offset in DarkHollowGpuObserved8BitPaletteOffsets)
            yield return new TexturePaletteCandidateRange(offset, TexturePaletteByteLength);
        foreach (int offset in DarkHollowGpuObserved4BitPaletteOffsets
            .Concat(DarkHollowGpuObservedAdditional4BitPaletteOffsets)
            .Distinct())
            yield return new TexturePaletteCandidateRange(offset, TexturePalette16ColorByteLength);
    }

    private static void NormalizeDarkHollowPaletteCandidateLengths(
        string levelKey,
        Dictionary<int, TexturePaletteCandidate> candidates)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(levelKey), "darkhollow", StringComparison.OrdinalIgnoreCase))
            return;

        int[] starts = candidates.Keys.Order().ToArray();
        foreach (int start in starts)
        {
            TexturePaletteCandidate candidate = candidates[start];
            if (candidate.ByteLength != TexturePaletteByteLength ||
                DarkHollowGpuObserved8BitPaletteOffsets.Contains(start))
            {
                continue;
            }

            if (starts.Any(other => other > start && other < start + TexturePaletteByteLength))
                candidates[start] = candidate with { ByteLength = TexturePalette16ColorByteLength };
        }
    }

    private static void RemoveDarkHollowProtectedPaletteCandidates(
        string levelKey,
        Dictionary<int, TexturePaletteCandidate> candidates)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(levelKey), "darkhollow", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (int offset in DarkHollowProtectedPlayerPaletteOffsets)
            candidates.Remove(offset);
    }

    private static void ValidateDarkHollowCloseTreePaletteSource(
        LevelDefinition level,
        TexturePaletteTable palette)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "darkhollow", StringComparison.OrdinalIgnoreCase) ||
            !DarkHollowCloseTreePaletteSha256.TryGetValue(palette.Offset, out string? expectedHash))
        {
            return;
        }

        string actualHash = Hash(palette.Bytes);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Dark Hollow's close-tree palette at 0x{palette.Offset:X} does not match the validated USA source bytes. " +
                $"Expected {expectedHash}, got {actualHash}.");
        }
    }

    private static void ValidateDarkHollowCloseTreePaletteSources(
        LevelDefinition level,
        IReadOnlyList<TexturePaletteTable> palettes)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "darkhollow", StringComparison.OrdinalIgnoreCase))
            return;

        foreach ((int offset, string expectedHash) in DarkHollowCloseTreePaletteSha256)
        {
            TexturePaletteTable? palette = palettes.SingleOrDefault(candidate => candidate.Offset == offset);
            if (palette == null)
            {
                throw new InvalidOperationException(
                    $"Dark Hollow's close-tree palette at 0x{offset:X} is missing from the validated USA source layout. " +
                    "Rebuild the WAD analysis from a clean USA source image.");
            }
            string actualHash = Hash(palette.Bytes);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Dark Hollow's close-tree palette at 0x{offset:X} does not match the validated USA source bytes. " +
                    $"Expected {expectedHash}, got {actualHash}.");
            }
        }
    }

    private static IReadOnlyList<SceneryColorTable> ReadDarkHollowFarTreeColorTables(
        byte[] sceneryModelBytes,
        string levelKey)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(levelKey), "darkhollow", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<SceneryColorTable>();

        List<SceneryColorTable> result = [];
        foreach (DarkHollowSceneryColorTableSpec spec in DarkHollowFarTreeColorTableSpecs)
        {
            int byteLength = checked(spec.ColorCount * 4);
            if (spec.Offset < 0 || spec.Offset + byteLength > sceneryModelBytes.Length)
                throw new InvalidOperationException($"Dark Hollow's {spec.Label} scenery color table is outside subfile 2.");

            byte[] bytes = sceneryModelBytes.AsSpan(spec.Offset, byteLength).ToArray();
            if (Enumerable.Range(0, spec.ColorCount).Any(index => bytes[(index * 4) + 3] != 0x00))
                throw new InvalidOperationException($"Dark Hollow's {spec.Label} scenery color table no longer has the validated RGB0 layout.");

            result.Add(new SceneryColorTable(
                spec.Offset,
                spec.ColorCount,
                spec.Label,
                spec.ExpectedBeforeSha256,
                bytes));
        }
        return result;
    }

    private static bool CanUseTextureDescriptor(TextureDescriptor descriptor, int texturePagesSize)
    {
        int packedPaletteStart = DecodePackedPaletteByteStart(descriptor);
        if (packedPaletteStart < 0 || packedPaletteStart + descriptor.PaletteByteLength > texturePagesSize)
            return false;
        int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
        int startX = descriptor.VramXMin + (matrix[0] < 0 || matrix[1] < 0 ? 31 : 0);
        int startY = descriptor.VramYMin + (matrix[2] < 0 || matrix[3] < 0 ? 31 : 0);
        foreach ((int x, int y) in new[] { (0, 0), (31, 0), (0, 31), (31, 31) })
        {
            int sx = startX + (x * matrix[0]) + (y * matrix[1]);
            int sy = startY + (x * matrix[2]) + (y * matrix[3]);
            long relative = (sy * 2048L) + sx;
            if (sx < 0 || sx >= 2048 || sy < 0 || sy >= 512 || relative < 0 || relative >= texturePagesSize)
                return false;
        }
        return true;
    }

    private static IEnumerable<ColorRgba> ReadSceneColors(byte[] bytes, SceneColorTable table)
    {
        for (int index = 0; index < table.ColorCount; index++)
        {
            foreach (int rgbOffset in table.RgbOffsets)
            {
                int offset = (index * table.EntryBytes) + rgbOffset;
                yield return ColorRgba.FromRgb(bytes[offset], bytes[offset + 1], bytes[offset + 2]);
            }
        }
    }

    private static IEnumerable<ColorRgba> ReadTextureColors(byte[] bytes)
    {
        for (int index = 0; index < bytes.Length / 2; index++)
        {
            ushort word = ReadUInt16(bytes, index * 2);
            if ((word & 0x7FFF) == 0)
                continue;
            yield return DecodePsx555(word);
        }
    }

    private static byte[] TransformSceneColorTable(
        byte[] before,
        SceneColorTable table,
        NativeEnvironmentColorTransform transform,
        bool smoothTerrain)
    {
        byte[] after = before.ToArray();
        for (int index = 0; index < table.ColorCount; index++)
        {
            foreach (int rgbOffset in table.RgbOffsets)
            {
                int offset = (index * table.EntryBytes) + rgbOffset;
                ColorRgba source = ColorRgba.FromRgb(after[offset], after[offset + 1], after[offset + 2]);
                ColorRgba graded = smoothTerrain
                    ? transform.ApplyTerrainSmoothing(source)
                    : transform.Apply(source);
                after[offset] = graded.R;
                after[offset + 1] = graded.G;
                after[offset + 2] = graded.B;
            }
        }
        return after;
    }

    private static byte[] TransformTexturePalette(
        byte[] before,
        NativeEnvironmentColorTransform transform,
        bool smoothTerrain)
    {
        byte[] after = before.ToArray();
        for (int index = 0; index < before.Length / 2; index++)
        {
            int offset = index * 2;
            ushort word = ReadUInt16(before, offset);
            if ((word & 0x7FFF) == 0)
                continue;
            ColorRgba source = DecodePsx555(word);
            ColorRgba graded = smoothTerrain
                ? transform.ApplyTerrainSmoothing(source)
                : transform.Apply(source);
            ushort replacement = EncodePsx555(graded, (word & 0x8000) != 0);
            BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(offset, 2), replacement);
        }
        return after;
    }

    private static byte[] TransformSceneryColorTable(
        byte[] before,
        SceneryColorTable table,
        NativeEnvironmentColorTransform transform)
    {
        byte[] after = before.ToArray();
        for (int index = 0; index < table.ColorCount; index++)
        {
            int offset = index * 4;
            ColorRgba source = ColorRgba.FromRgb(after[offset], after[offset + 1], after[offset + 2]);
            ColorRgba graded = transform.ApplyTerrainSmoothing(source);
            after[offset] = graded.R;
            after[offset + 1] = graded.G;
            after[offset + 2] = graded.B;
        }
        return after;
    }

    private static ColorRgba DecodePsx555(ushort word)
    {
        static byte Expand(int value) => (byte)((value << 3) | (value >> 2));
        return ColorRgba.FromRgb(Expand(word & 31), Expand((word >> 5) & 31), Expand((word >> 10) & 31));
    }

    private static ushort EncodePsx555(ColorRgba color, bool semiTransparent)
    {
        int r = Math.Clamp((int)Math.Round(color.R * 31.0 / 255.0), 0, 31);
        int g = Math.Clamp((int)Math.Round(color.G * 31.0 / 255.0), 0, 31);
        int b = Math.Clamp((int)Math.Round(color.B * 31.0 / 255.0), 0, 31);
        return (ushort)((semiTransparent ? 0x8000 : 0) | (b << 10) | (g << 5) | r);
    }

    private static void AddMobyRuntimePatches(
        FileStream image,
        DiscLayout discLayout,
        IReadOnlyDictionary<int, uint> materialByLevelId,
        GradeWriteRangeTracker writtenRanges,
        List<NativeEnvironmentGradePatch> patches,
        List<GradePayload> payloads)
    {
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, discLayout, IsExecutableName);
        long hookFileOffset = ExeFileOffset(MobyGradeHookAddress);
        long payloadFileOffset = ExeFileOffset(MobyGradePayloadAddress);
        long neutralMaterialFileOffset = ExeFileOffset(NeutralMobyMaterialAddress);
        if (hookFileOffset < 0x800 ||
            payloadFileOffset < 0x800 ||
            payloadFileOffset + MobyGradePayloadBytes > executable.Size ||
            neutralMaterialFileOffset < 0x800 ||
            neutralMaterialFileOffset + 4 > executable.Size)
        {
            throw new InvalidOperationException("The guarded Moby environment hook is outside the executable body.");
        }

        byte[] neutralMaterial = DiscImage.ReadFileBytes(image, discLayout, executable.Lba, neutralMaterialFileOffset, 4);
        uint neutralMaterialWord = BinaryPrimitives.ReadUInt32LittleEndian(neutralMaterial);
        if (neutralMaterialWord != NeutralMobyMaterialWord)
        {
            throw new InvalidOperationException(
                $"Enemy/object lighting expected native material 0x{NeutralMobyMaterialWord:X8} at 0x{NeutralMobyMaterialAddress:X8}, got 0x{neutralMaterialWord:X8}.");
        }

        byte[] beforeHook = DiscImage.ReadFileBytes(image, discLayout, executable.Lba, hookFileOffset, 4);
        uint hookWord = BinaryPrimitives.ReadUInt32LittleEndian(beforeHook);
        if (hookWord != NewJalWord(OriginalGameLoopTarget))
        {
            throw new InvalidOperationException(
                $"Enemy/object palette matching needs the guarded executable cave, but another runtime helper already owns the game-loop hook (0x{hookWord:X8}). " +
                "Turn off enemy/object matching for this BIN or remove the experimental Spring Chest helper.");
        }

        byte[] beforePayload = DiscImage.ReadFileBytes(image, discLayout, executable.Lba, payloadFileOffset, MobyGradePayloadBytes);
        if (beforePayload.Any(value => value != 0))
            throw new InvalidOperationException($"The guarded Moby environment payload cave at 0x{MobyGradePayloadAddress:X8} is not blank.");

        byte[] afterHook = BitConverter.GetBytes(NewJalWord(MobyGradePayloadAddress));
        byte[] afterPayload = BuildMobyGradePayload(materialByLevelId);
        AddRawPatch(
            executable.Lba,
            hookFileOffset,
            "environment-moby-runtime-hook",
            "shared-moby-grade-hook",
            "Shared Moby environment grade",
            beforeHook,
            afterHook,
            discLayout,
            writtenRanges,
            patches,
            payloads,
            "Install the level-aware native object-lighting update and preserve the original game-loop call.");
        AddRawPatch(
            executable.Lba,
            payloadFileOffset,
            "environment-moby-runtime-payload",
            "shared-moby-grade-payload",
            "Shared Moby environment grade",
            beforePayload,
            afterPayload,
            discLayout,
            writtenRanges,
            patches,
            payloads,
            $"Update the valid neutral material-0 entry from a {MaxLevelId + 1}-entry level-id color table after the original game loop; no object row is rerouted.");
    }

    private static byte[] BuildMobyGradePayload(IReadOnlyDictionary<int, uint> materialByLevelId)
    {
        MobyGradeMipsEmitter asm = new(MobyGradePayloadAddress);
        asm.Addiu("sp", "sp", -0x10);
        asm.Sw("ra", 0, "sp");
        asm.Jal(OriginalGameLoopTarget);
        asm.Nop();
        asm.Li("t0", CurrentLevelIdAddress);
        asm.Lw("t1", 0, "t0");
        asm.Sltiu("t2", "t1", MaxLevelId + 1);
        asm.Beq("t2", "zero", "done");
        asm.Nop();
        asm.Sll("t1", "t1", 2);
        asm.Li("t0", MobyGradePayloadAddress + MobyGradeLevelTableOffset);
        asm.Addu("t0", "t0", "t1");
        asm.Lw("t1", 0, "t0");
        asm.Li("t0", NeutralMobyMaterialAddress);
        asm.Sw("t1", 0, "t0");
        asm.L("done");
        asm.Lw("ra", 0, "sp");
        asm.Addiu("sp", "sp", 0x10);
        asm.Jr("ra");
        asm.Nop();

        byte[] code = asm.ToBytes();
        if (code.Length > MobyGradeLevelTableOffset)
            throw new InvalidOperationException("The guarded Moby environment payload overlaps its level color table.");
        byte[] payload = new byte[MobyGradePayloadBytes];
        Array.Copy(code, payload, code.Length);
        for (int levelId = 0; levelId <= MaxLevelId; levelId++)
        {
            uint material = materialByLevelId.TryGetValue(levelId, out uint value) ? value : NeutralMobyMaterialWord;
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(MobyGradeLevelTableOffset + (levelId * 4), 4),
                material);
        }
        return payload;
    }

    private static void AddRawPatch(
        int fileLba,
        long fileOffset,
        string kind,
        string label,
        string levelName,
        byte[] before,
        byte[] after,
        DiscLayout discLayout,
        GradeWriteRangeTracker writtenRanges,
        List<NativeEnvironmentGradePatch> patches,
        List<GradePayload> payloads,
        string description)
    {
        ValidateFixedSizePatch(before, after, label);
        if (before.AsSpan().SequenceEqual(after))
            return;
        writtenRanges.Reserve(fileLba, fileOffset, after.Length, label);

        patches.Add(new NativeEnvironmentGradePatch(
            Label: $"{label}: {description}",
            Kind: kind,
            LevelKey: "shared-runtime",
            LevelName: levelName,
            DonorLevelKey: "",
            DonorLevelName: "",
            WadOffset: $"exe:0x{ExeDestination + fileOffset - 0x800:X8}",
            ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(discLayout, fileLba, fileOffset):X}",
            ByteLength: after.Length,
            ColorCount: 0,
            ChangedByteCount: CountChangedBytes(before, after),
            BeforeSha256: Hash(before),
            AfterSha256: Hash(after),
            BeforeHexPreview: HexPreview(before),
            AfterHexPreview: HexPreview(after)));
        payloads.Add(new GradePayload(fileLba, fileOffset, before, after, label));
    }

    private static void AddTexturePalettePatches(
        LevelDefinition level,
        LevelDefinition donor,
        int wadLba,
        LevelColorData targetData,
        NativeEnvironmentColorTransform transform,
        DiscLayout discLayout,
        GradeWriteRangeTracker writtenRanges,
        List<NativeEnvironmentGradePatch> patches,
        List<GradePayload> payloads)
    {
        List<PaletteTransformRange> claimed = [];
        foreach (TexturePaletteTable palette in targetData.TexturePalettes.OrderBy(item => item.Offset))
        {
            ValidateDarkHollowCloseTreePaletteSource(level, palette);
            byte[] before = palette.Bytes;
            byte[] after = TransformTexturePalette(before, transform, smoothTerrain: true);
            int start = palette.Offset;
            int end = checked(start + before.Length);
            string sourceLabel = $"palette-0x{palette.Offset:X}";

            foreach (PaletteTransformRange existing in claimed)
            {
                int overlapStart = Math.Max(start, existing.Start);
                int overlapEnd = Math.Min(end, existing.End);
                if (overlapStart >= overlapEnd)
                    continue;

                int overlapLength = overlapEnd - overlapStart;
                ReadOnlySpan<byte> currentBefore = before.AsSpan(overlapStart - start, overlapLength);
                ReadOnlySpan<byte> currentAfter = after.AsSpan(overlapStart - start, overlapLength);
                ReadOnlySpan<byte> existingBefore = existing.Before.AsSpan(overlapStart - existing.Start, overlapLength);
                ReadOnlySpan<byte> existingAfter = existing.After.AsSpan(overlapStart - existing.Start, overlapLength);
                if (!currentBefore.SequenceEqual(existingBefore) || !currentAfter.SequenceEqual(existingAfter))
                {
                    throw new InvalidOperationException(
                        $"Overlapping environment palette candidates '{sourceLabel}' and '{existing.Label}' do not produce identical source/graded bytes.");
                }
            }

            List<PaletteInterval> uncovered = [new(start, end)];
            foreach (PaletteTransformRange existing in claimed)
                uncovered = SubtractPaletteInterval(uncovered, existing.Start, existing.End);

            foreach (PaletteInterval interval in uncovered)
            {
                int relativeOffset = interval.Start - start;
                int byteLength = interval.End - interval.Start;
                byte[] segmentBefore = before.AsSpan(relativeOffset, byteLength).ToArray();
                byte[] segmentAfter = after.AsSpan(relativeOffset, byteLength).ToArray();
                string suffix = interval.Start == start && interval.End == end
                    ? sourceLabel
                    : $"{sourceLabel}-range-0x{interval.Start:X}";
                int nonZeroColorCount = Enumerable.Range(0, segmentBefore.Length / 2)
                    .Count(index => (ReadUInt16(segmentBefore, index * 2) & 0x7FFF) != 0);
                AddPatch(
                    level,
                    donor,
                    wadLba,
                    targetData.TexturePagesSubfile.WadOffset + interval.Start,
                    "environment-terrain-texture-palette",
                    suffix,
                    nonZeroColorCount,
                    segmentBefore,
                    segmentAfter,
                    discLayout,
                    writtenRanges,
                    patches,
                    payloads);
            }

            claimed.Add(new PaletteTransformRange(start, end, before, after, sourceLabel));
        }
    }

    private static List<PaletteInterval> SubtractPaletteInterval(
        IReadOnlyList<PaletteInterval> source,
        int removeStart,
        int removeEnd)
    {
        List<PaletteInterval> result = [];
        foreach (PaletteInterval interval in source)
        {
            if (removeEnd <= interval.Start || removeStart >= interval.End)
            {
                result.Add(interval);
                continue;
            }
            if (removeStart > interval.Start)
                result.Add(new PaletteInterval(interval.Start, Math.Min(removeStart, interval.End)));
            if (removeEnd < interval.End)
                result.Add(new PaletteInterval(Math.Max(removeEnd, interval.Start), interval.End));
        }
        return result;
    }

    private static void AddPatch(
        LevelDefinition level,
        LevelDefinition donor,
        int wadLba,
        long wadOffset,
        string kind,
        string suffix,
        int colorCount,
        byte[] before,
        byte[] after,
        DiscLayout discLayout,
        GradeWriteRangeTracker writtenRanges,
        List<NativeEnvironmentGradePatch> patches,
        List<GradePayload> payloads)
    {
        string label = $"{level.Key}-{kind}-{suffix}";
        ValidateFixedSizePatch(before, after, label);
        if (before.AsSpan().SequenceEqual(after))
            return;
        writtenRanges.Reserve(wadLba, wadOffset, after.Length, label);
        int changedBytes = CountChangedBytes(before, after);
        patches.Add(new NativeEnvironmentGradePatch(
            Label: label,
            Kind: kind,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            DonorLevelKey: donor.Key,
            DonorLevelName: donor.DisplayName,
            WadOffset: $"0x{wadOffset:X}",
            ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(discLayout, wadLba, wadOffset):X}",
            ByteLength: after.Length,
            ColorCount: colorCount,
            ChangedByteCount: changedBytes,
            BeforeSha256: Hash(before),
            AfterSha256: Hash(after),
            BeforeHexPreview: HexPreview(before),
            AfterHexPreview: HexPreview(after)));
        payloads.Add(new GradePayload(wadLba, wadOffset, before, after, label));
    }

    private static void ValidateFixedSizePatch(byte[] before, byte[] after, string label)
    {
        if (before.Length <= 0 || before.Length != after.Length)
        {
            throw new InvalidOperationException(
                $"Environment-grade patch '{label}' must preserve one non-empty fixed-size source range ({before.Length} before byte(s), {after.Length} after byte(s)).");
        }
    }

    private static NativeAssetCatalog LoadAssetCatalog(string wadAnalysisPath)
    {
        if (!File.Exists(wadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", wadAnalysisPath);
        using FileStream stream = File.OpenRead(wadAnalysisPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        JsonElement wad = root.GetProperty("wad");
        int wadLba = JsonValue.GetInt32(wad, "lba", 37);
        int wadSize = JsonValue.GetInt32(wad, "size", -1);
        if (wadSize <= 0)
            throw new InvalidOperationException("The WAD analysis does not contain a valid WAD.WAD size.");
        Dictionary<int, LevelAssetLayout> levels = new();
        foreach (JsonElement entry in root.GetProperty("entries").EnumerateArray())
        {
            int entryIndex = JsonValue.GetInt32(entry, "index", -1);
            long entryOffset = JsonValue.GetInt64(entry, "offset", -1);
            int entrySize = JsonValue.GetInt32(entry, "size", -1);
            if (entryIndex < 0 || entryOffset < 0 || entrySize <= 0 || !entry.TryGetProperty("level", out JsonElement level) ||
                level.ValueKind != JsonValueKind.Object ||
                !level.TryGetProperty("subfiles", out JsonElement subfiles) ||
                subfiles.ValueKind != JsonValueKind.Array)
                continue;
            NativeSubfile? textures = null;
            NativeSubfile? model = null;
            NativeSubfile? sceneryModels = null;
            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                int index = JsonValue.GetInt32(subfile, "index", -1);
                long relativeOffset = JsonValue.GetInt64(subfile, "offset", -1);
                int size = JsonValue.GetInt32(subfile, "size", -1);
                if (relativeOffset < 0 || size <= 0)
                    continue;
                NativeSubfile value = new(index, relativeOffset, entryOffset + relativeOffset, size);
                if (index == TexturePagesSubfileIndex)
                    textures = value;
                else if (index == ModelSubfileIndex)
                    model = value;
                else if (index == SceneryModelSubfileIndex)
                    sceneryModels = value;
            }
            if (textures != null && model != null)
            {
                levels[entryIndex] = new LevelAssetLayout(
                    entryIndex,
                    entryOffset,
                    entrySize,
                    textures,
                    model,
                    sceneryModels);
            }
        }
        return new NativeAssetCatalog(wadLba, wadSize, levels);
    }

    private static void ValidateAssetCatalogAgainstSource(
        FileStream image,
        DiscLayout discLayout,
        NativeAssetCatalog assets)
    {
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, discLayout, IsWadName);
        if (wad.Lba != assets.WadLba || wad.Size != assets.WadSize)
        {
            throw new InvalidOperationException(
                $"The WAD analysis belongs to a different disc layout: selected WAD.WAD is LBA {wad.Lba}, {wad.Size} bytes; " +
                $"analysis expects LBA {assets.WadLba}, {assets.WadSize} bytes. Rebuild the analysis from the selected source image.");
        }

        foreach (LevelAssetLayout level in assets.Levels.Values)
        {
            long entryHeaderOffset = checked(level.EntryIndex * 8L);
            ValidateContainedRange(
                entryHeaderOffset,
                8,
                assets.WadSize,
                $"WAD entry {level.EntryIndex} header");
            byte[] entryHeader = DiscImage.ReadFileBytes(
                image,
                discLayout,
                assets.WadLba,
                entryHeaderOffset,
                8);
            long actualEntryOffset = BinaryPrimitives.ReadUInt32LittleEndian(entryHeader.AsSpan(0, 4));
            int actualEntrySize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entryHeader.AsSpan(4, 4)));
            if (actualEntryOffset != level.EntryOffset || actualEntrySize != level.EntrySize)
            {
                throw new InvalidOperationException(
                    $"The WAD analysis entry {level.EntryIndex} is stale for the selected source image: " +
                    $"disc has offset 0x{actualEntryOffset:X}, size 0x{actualEntrySize:X}; " +
                    $"analysis expects offset 0x{level.EntryOffset:X}, size 0x{level.EntrySize:X}.");
            }
            ValidateContainedRange(
                level.EntryOffset,
                level.EntrySize,
                assets.WadSize,
                $"WAD entry {level.EntryIndex}");

            foreach (NativeSubfile subfile in EnumerateAssetSubfiles(level))
            {
                long relativeHeaderOffset = checked(subfile.Index * 8L);
                ValidateContainedRange(
                    relativeHeaderOffset,
                    8,
                    level.EntrySize,
                    $"WAD entry {level.EntryIndex} subfile {subfile.Index} header");
                ValidateContainedRange(
                    subfile.RelativeOffset,
                    subfile.Size,
                    level.EntrySize,
                    $"WAD entry {level.EntryIndex} subfile {subfile.Index}");
                byte[] subfileHeader = DiscImage.ReadFileBytes(
                    image,
                    discLayout,
                    assets.WadLba,
                    checked(level.EntryOffset + relativeHeaderOffset),
                    8);
                long actualRelativeOffset = BinaryPrimitives.ReadUInt32LittleEndian(subfileHeader.AsSpan(0, 4));
                int actualSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(subfileHeader.AsSpan(4, 4)));
                if (actualRelativeOffset != subfile.RelativeOffset || actualSize != subfile.Size)
                {
                    throw new InvalidOperationException(
                        $"The WAD analysis entry {level.EntryIndex} subfile {subfile.Index} is stale for the selected source image: " +
                        $"disc has relative offset 0x{actualRelativeOffset:X}, size 0x{actualSize:X}; " +
                        $"analysis expects relative offset 0x{subfile.RelativeOffset:X}, size 0x{subfile.Size:X}.");
                }
            }
        }
    }

    private static void ValidateContainedRange(
        long start,
        long length,
        long containerLength,
        string label)
    {
        if (start < 0 || length <= 0 || containerLength <= 0)
            throw new InvalidOperationException($"{label} has an invalid source-bound range.");
        long end;
        try
        {
            end = checked(start + length);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException($"{label} overflows its source-bound range.", exception);
        }
        if (end > containerLength)
        {
            throw new InvalidOperationException(
                $"{label} extends past its validated container: [0x{start:X},0x{end:X}) of 0x{containerLength:X} bytes.");
        }
    }

    private static IEnumerable<NativeSubfile> EnumerateAssetSubfiles(LevelAssetLayout level)
    {
        yield return level.TexturePages;
        yield return level.Model;
        if (level.SceneryModels != null)
            yield return level.SceneryModels;
    }

    private static Vector3f ConvertSceneVertex(uint word, SceneSector sector)
    {
        int sectorX = (int)((sector.XyPos >> 16) & 0xFFFF);
        int sectorY = (int)(sector.XyPos & 0xFFFF);
        int sectorZ = (int)((sector.ZPos >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((sector.CentreRadiusAndFlags >> 12) & 1) == 1)
            z >>= 3;
        return new Vector3f(x, y, z);
    }

    private static int CountChangedBytes(byte[] before, byte[] after)
    {
        int count = Math.Abs(before.Length - after.Length);
        for (int index = 0; index < Math.Min(before.Length, after.Length); index++)
        {
            if (before[index] != after[index])
                count++;
        }
        return count;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string HexPreview(byte[] bytes) => Convert.ToHexString(bytes.AsSpan(0, Math.Min(32, bytes.Length)));
    private static ushort ReadUInt16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static uint EncodeRgbWord(ColorRgba color) => (uint)(color.R | (color.G << 8) | (color.B << 16));
    private static uint NewJalWord(uint address) => (3u << 26) | ((address >> 2) & 0x03FFFFFFu);
    private static long ExeFileOffset(uint runtimeAddress) => 0x800 + ((long)runtimeAddress - ExeDestination);

    private static bool IsExecutableName(string name)
    {
        string upper = (name ?? "").ToUpperInvariant();
        return upper.StartsWith("SCUS", StringComparison.Ordinal) ||
            upper.StartsWith("SCES", StringComparison.Ordinal) ||
            upper.StartsWith("SCPS", StringComparison.Ordinal) ||
            upper.StartsWith("SLUS", StringComparison.Ordinal) ||
            upper.StartsWith("SLES", StringComparison.Ordinal) ||
            upper.StartsWith("SLPS", StringComparison.Ordinal);
    }

    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseLong(string text, out long value)
    {
        string trimmed = (text ?? "").Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private sealed record GradePayload(
        int FileLba,
        long FileOffset,
        byte[] Before,
        byte[] After,
        string Label);
    private sealed record NativeSubfile(int Index, long RelativeOffset, long WadOffset, int Size);
    private sealed record LevelAssetLayout(
        int EntryIndex,
        long EntryOffset,
        int EntrySize,
        NativeSubfile TexturePages,
        NativeSubfile Model,
        NativeSubfile? SceneryModels);
    private sealed record NativeAssetCatalog(
        int WadLba,
        int WadSize,
        IReadOnlyDictionary<int, LevelAssetLayout> Levels);
    private sealed record SceneSector(
        int Offset,
        int CentreRadiusAndFlags,
        uint XyPos,
        uint ZPos,
        int SizeBytes,
        int NumLpVertices,
        int NumLpColours,
        int NumLpFaces,
        int NumHpVertices,
        int NumHpColours,
        int NumHpFaces,
        int SectorIndex);
    private sealed record SceneColorTable(
        int SectorIndex,
        string Detail,
        int Offset,
        int EntryBytes,
        IReadOnlyList<int> RgbOffsets,
        int ColorCount,
        byte[] Bytes);
    private sealed record TexturePaletteCandidate(int ByteLength, bool IsRuntimeVariant);
    private sealed record TexturePaletteCandidateRange(int Offset, int ByteLength);
    private sealed record TexturePaletteTable(int Offset, int NonZeroColorCount, bool IsRuntimeVariant, byte[] Bytes);
    private sealed record PaletteInterval(int Start, int End);
    private sealed record PaletteTransformRange(int Start, int End, byte[] Before, byte[] After, string Label);
    private sealed record DarkHollowSceneryColorTableSpec(
        int Offset,
        int ColorCount,
        string Label,
        string ExpectedBeforeSha256);
    private sealed record SceneryColorTable(
        int Offset,
        int ColorCount,
        string Label,
        string ExpectedBeforeSha256,
        byte[] Bytes);
    private sealed record MobyMaterialRow(int TrueIndex, long WadOffset, MobyVisualKind Kind, string Label, byte MaterialId);
    private sealed record TextureDescriptor(
        int PaletteCode,
        int PaletteByteStart,
        int PaletteByteLength,
        int Orientation,
        int VramXMin,
        int VramYMin,
        int VramXMax,
        int VramYMax);
    private sealed record LevelColorData(
        NativeSubfile ModelSubfile,
        NativeSubfile TexturePagesSubfile,
        NativeSubfile? SceneryModelSubfile,
        IReadOnlyList<SceneSector> Sectors,
        IReadOnlyList<SceneColorTable> SceneColorTables,
        IReadOnlyList<SceneryColorTable> SceneryColorTables,
        IReadOnlySet<int> TextureIds,
        IReadOnlyList<TexturePaletteTable> TexturePalettes,
        IReadOnlyList<MobyMaterialRow> MobyMaterialRows,
        IReadOnlyList<ColorRgba> SceneColors,
        IReadOnlyList<ColorRgba> TextureColors,
        NativeEnvironmentTextureUsageStatistics TextureUsage);

    private sealed class GradeWriteRangeTracker
    {
        private readonly List<GradeWriteRange> ranges = [];

        public void Reserve(int fileLba, long start, int length, string label)
        {
            if (start < 0 || length <= 0)
                throw new InvalidOperationException($"Environment-grade patch '{label}' has an invalid target range.");
            long end = checked(start + length);
            GradeWriteRange? overlap = ranges.FirstOrDefault(existing =>
                existing.FileLba == fileLba &&
                start < existing.End &&
                existing.Start < end);
            if (overlap != null)
            {
                throw new InvalidOperationException(
                    $"Environment-grade patch '{label}' [0x{start:X}, 0x{end:X}) overlaps " +
                    $"'{overlap.Label}' [0x{overlap.Start:X}, 0x{overlap.End:X}) in file LBA {fileLba}.");
            }
            ranges.Add(new GradeWriteRange(fileLba, start, end, label));
        }

        private sealed record GradeWriteRange(int FileLba, long Start, long End, string Label);
    }

    private sealed class MobyGradeMipsEmitter
    {
        private static readonly IReadOnlyDictionary<string, int> Registers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = 0,
            ["at"] = 1,
            ["v0"] = 2,
            ["v1"] = 3,
            ["a0"] = 4,
            ["a1"] = 5,
            ["a2"] = 6,
            ["a3"] = 7,
            ["t0"] = 8,
            ["t1"] = 9,
            ["t2"] = 10,
            ["t3"] = 11,
            ["t4"] = 12,
            ["t5"] = 13,
            ["t6"] = 14,
            ["t7"] = 15,
            ["t8"] = 24,
            ["t9"] = 25,
            ["sp"] = 29,
            ["ra"] = 31
        };

        private readonly uint baseAddress;
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<BranchFixup> fixups = [];

        public MobyGradeMipsEmitter(uint baseAddress)
        {
            this.baseAddress = baseAddress;
        }

        public void L(string name) => labels[name] = words.Count;
        public void Nop() => words.Add(0);
        public void Addiu(string rt, string rs, int immediate) => EmitIType(9, rs, rt, immediate);
        public void Lui(string rt, int immediate) => EmitIType(15, "zero", rt, immediate);
        public void Ori(string rt, string rs, long immediate) => EmitIType(13, rs, rt, immediate);
        public void Sltiu(string rt, string rs, int immediate) => EmitIType(11, rs, rt, immediate);
        public void Lw(string rt, int offset, string rs)
        {
            EmitIType(35, rs, rt, offset);
            Nop();
        }
        public void Sw(string rt, int offset, string rs) => EmitIType(43, rs, rt, offset);
        public void Sll(string rd, string rt, int shift) => EmitRType("zero", rt, rd, shift, 0);
        public void Addu(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 33);
        public void Jr(string rs) => EmitRType(rs, "zero", "zero", 0, 8);
        public void Beq(string rs, string rt, string label)
        {
            fixups.Add(new BranchFixup(words.Count, 4, rs, rt, label));
            words.Add(0);
        }
        public void Jal(uint address) => words.Add(NewJalWord(address));

        public void Li(string rt, uint value)
        {
            int high = (int)((value >> 16) & 0xFFFF);
            int low = (int)(value & 0xFFFF);
            if (high == 0)
            {
                Ori(rt, "zero", low);
                return;
            }
            Lui(rt, high);
            if (low != 0)
                Ori(rt, rt, low);
        }

        public byte[] ToBytes()
        {
            foreach (BranchFixup fixup in fixups)
            {
                if (!labels.TryGetValue(fixup.Label, out int targetIndex))
                    throw new InvalidOperationException($"Missing MIPS payload label '{fixup.Label}'.");
                int relative = targetIndex - (fixup.WordIndex + 1);
                if (relative is < short.MinValue or > short.MaxValue)
                    throw new InvalidOperationException($"MIPS payload branch to '{fixup.Label}' is out of range.");
                words[fixup.WordIndex] = IType(
                    fixup.Op,
                    Registers[fixup.Rs],
                    Registers[fixup.Rt],
                    relative);
            }

            byte[] bytes = new byte[words.Count * 4];
            for (int index = 0; index < words.Count; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4, 4), words[index]);
            return bytes;
        }

        private void EmitIType(int op, string rs, string rt, long immediate) =>
            words.Add(IType(op, Registers[rs], Registers[rt], immediate));

        private void EmitRType(string rs, string rt, string rd, int shift, int function) =>
            words.Add((uint)((Registers[rs] << 21) | (Registers[rt] << 16) | (Registers[rd] << 11) | (shift << 6) | function));

        private static uint IType(int op, int rs, int rt, long immediate) =>
            (uint)((op << 26) | (rs << 21) | (rt << 16) | ((int)immediate & 0xFFFF));

        private sealed record BranchFixup(int WordIndex, int Op, string Rs, string Rt, string Label);
    }
}
