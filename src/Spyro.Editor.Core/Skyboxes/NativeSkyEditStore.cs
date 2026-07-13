using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Skyboxes;

public sealed record NativeSkyEditPlan(
    int Version,
    DateTimeOffset SavedAt,
    string LevelKey,
    string LevelName,
    string Mode,
    string PalettePreset,
    string CustomPaletteHex,
    string DonorLevelKey,
    string ImportedSkyPath,
    string ImportedSkySha256)
{
    public const string PaletteMode = "palette";
    public const string SwapMode = "same-disc-swap";
    public const string OriginalPresetMode = "original-preset";
    public const string ImportMode = "custom-sky-import";

    public bool IsPalette => string.Equals(Mode, PaletteMode, StringComparison.OrdinalIgnoreCase);
    public bool IsSwap => string.Equals(Mode, SwapMode, StringComparison.OrdinalIgnoreCase);
    public bool IsOriginalPreset => string.Equals(Mode, OriginalPresetMode, StringComparison.OrdinalIgnoreCase);
    public bool IsImport => string.Equals(Mode, ImportMode, StringComparison.OrdinalIgnoreCase);
    public NativeEnvironmentGradePlan EnvironmentGrade { get; init; } = NativeEnvironmentGradePlan.Disabled;
}

public static class NativeSkyEditStore
{
    public static string PlanPath(string workspacePath, string levelKey) =>
        Path.Combine(workspacePath, $"{LevelCatalog.NormalizeKey(levelKey)}-skybox-edit-plan.json");

    public static async Task SaveAsync(string workspacePath, NativeSkyEditPlan plan, CancellationToken cancellationToken = default)
    {
        string path = PlanPath(workspacePath, plan.LevelKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await using FileStream output = File.Create(path);
        await JsonSerializer.SerializeAsync(output, plan, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }, cancellationToken);
    }

    public static NativeSkyEditPlan? Load(string workspacePath, LevelDefinition level)
    {
        string path = PlanPath(workspacePath, level.Key);
        if (!File.Exists(path))
            return null;

        try
        {
            using FileStream input = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(input);
            JsonElement root = document.RootElement;
            string savedLevelKey = JsonValue.GetString(root, "levelKey", level.Key);
            if (!string.Equals(LevelCatalog.NormalizeKey(savedLevelKey), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase))
                return null;

            string mode = JsonValue.GetString(root, "mode");
            string preset = JsonValue.GetString(root, "palettePreset", JsonValue.GetString(root, "preset", "night"));
            if (string.IsNullOrWhiteSpace(mode))
                mode = NativeSkyEditPlan.PaletteMode;
            if (mode is not (NativeSkyEditPlan.PaletteMode or NativeSkyEditPlan.SwapMode or NativeSkyEditPlan.OriginalPresetMode or NativeSkyEditPlan.ImportMode))
                return null;

            NativeEnvironmentGradePlan environmentGrade = ReadEnvironmentGrade(root, JsonValue.GetString(root, "donorLevelKey"));
            return new NativeSkyEditPlan(
                Version: JsonValue.GetInt32(root, "version", 1),
                SavedAt: ReadDate(root, "savedAt") ?? ReadDate(root, "generatedAt") ?? File.GetLastWriteTimeUtc(path),
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                Mode: mode,
                PalettePreset: SkyboxPresetCatalog.NormalizeNativePresetId(preset),
                CustomPaletteHex: JsonValue.GetString(root, "customPaletteHex"),
                DonorLevelKey: JsonValue.GetString(root, "donorLevelKey"),
                ImportedSkyPath: JsonValue.GetString(root, "importedSkyPath"),
                ImportedSkySha256: JsonValue.GetString(root, "importedSkySha256"))
            {
                EnvironmentGrade = environmentGrade
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static NativeEnvironmentGradePlan ReadEnvironmentGrade(JsonElement root, string skyDonorLevelKey)
    {
        if (!root.TryGetProperty("environmentGrade", out JsonElement grade) || grade.ValueKind != JsonValueKind.Object)
            return NativeEnvironmentGradePlan.Disabled;

        return new NativeEnvironmentGradePlan(
            Version: JsonValue.GetInt32(grade, "version", 1),
            Enabled: JsonValue.GetBoolean(grade, "enabled"),
            Mode: JsonValue.GetString(grade, "mode", NativeEnvironmentGradePlan.MatchSkySourceMode),
            DonorLevelKey: JsonValue.GetString(grade, "donorLevelKey", skyDonorLevelKey),
            StrengthPercent: JsonValue.GetInt32(grade, "strengthPercent", 100),
            BrightnessPercent: JsonValue.GetInt32(grade, "brightnessPercent", 100),
            SaturationPercent: JsonValue.GetInt32(grade, "saturationPercent", 100),
            TintHex: JsonValue.GetString(grade, "tintHex"),
            TintStrengthPercent: JsonValue.GetInt32(grade, "tintStrengthPercent", 0),
            GradeSceneColors: JsonValue.GetBoolean(grade, "gradeSceneColors", true),
            GradeTexturePalettes: JsonValue.GetBoolean(grade, "gradeTexturePalettes", true),
            GradeActors: JsonValue.GetBoolean(grade, "gradeActors"),
            GradeChests: JsonValue.GetBoolean(grade, "gradeChests"),
            GradeScenery: JsonValue.GetBoolean(grade, "gradeScenery"),
            GradeDragons: JsonValue.GetBoolean(grade, "gradeDragons"))
            .Normalize(skyDonorLevelKey);
    }

    public static bool Delete(string workspacePath, string levelKey)
    {
        string path = PlanPath(workspacePath, levelKey);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private static DateTimeOffset? ReadDate(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset parsed) ? parsed : null;
    }
}
