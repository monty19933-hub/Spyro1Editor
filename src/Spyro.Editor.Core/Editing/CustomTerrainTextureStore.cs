using System.Text.Json;

namespace Spyro.Editor.Core.Editing;

public static class CustomTerrainTextureStore
{
    public static string ManifestPath(string workspaceRoot, string levelKey)
    {
        return Path.Combine(workspaceRoot, $"{levelKey}-custom-terrain-textures.json");
    }

    public static IReadOnlyList<CustomTerrainTextureImport> Load(string workspaceRoot, string levelKey)
    {
        return LoadManifest(ManifestPath(workspaceRoot, levelKey));
    }

    public static IReadOnlyList<CustomTerrainTextureImport> LoadManifest(string path)
    {
        if (!File.Exists(path))
            return Array.Empty<CustomTerrainTextureImport>();

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("textures", out JsonElement textures) || textures.ValueKind != JsonValueKind.Array)
                return Array.Empty<CustomTerrainTextureImport>();

            List<CustomTerrainTextureImport> result = new();
            foreach (JsonElement texture in textures.EnumerateArray())
            {
                int textureId = JsonValue.GetInt32(texture, "textureId", -1);
                string sourceImagePath = JsonValue.GetString(texture, "sourceImagePath");
                if (textureId < 0 || string.IsNullOrWhiteSpace(sourceImagePath))
                    continue;

                result.Add(new CustomTerrainTextureImport(
                    TextureId: textureId,
                    SourceImagePath: sourceImagePath,
                    SourceImageName: JsonValue.GetString(texture, "sourceImageName", Path.GetFileName(sourceImagePath)),
                    DescriptorTier: NormalizeTier(JsonValue.GetString(texture, "descriptorTier", "hqData")),
                    TileSize: JsonValue.GetInt32(texture, "tileSize", 64),
                    SourceKind: JsonValue.GetString(texture, "sourceKind", "imported-image"),
                    PaletteName: JsonValue.GetString(texture, "paletteName"),
                    PaletteLowHex: JsonValue.GetString(texture, "paletteLowHex"),
                    PaletteHighHex: JsonValue.GetString(texture, "paletteHighHex"),
                    PaletteHexColors: ReadPaletteHexColors(texture),
                    CreatedAt: JsonValue.GetString(texture, "createdAt"),
                    SourceLevelKey: JsonValue.GetString(texture, "sourceLevelKey"),
                    SourceTextureId: JsonValue.GetInt32(texture, "sourceTextureId", -1),
                    SourceRuntimeKey: JsonValue.GetString(texture, "sourceRuntimeKey"),
                    SourceWadEntry: JsonValue.GetInt32(texture, "sourceWadEntry", -1)));
            }

            return result
                .OrderBy(texture => texture.TextureId)
                .ToArray();
        }
        catch
        {
            return Array.Empty<CustomTerrainTextureImport>();
        }
    }

    public static async Task<IReadOnlyList<CustomTerrainTextureImport>> AddOrReplaceAsync(
        string workspaceRoot,
        string levelKey,
        string levelName,
        int textureId,
        string sourceImagePath,
        string sourceImageName,
        string descriptorTier = "hqData",
        int tileSize = 64,
        string sourceKind = "imported-image",
        string paletteName = "",
        string paletteLowHex = "",
        string paletteHighHex = "",
        IReadOnlyList<string>? paletteHexColors = null,
        string sourceLevelKey = "",
        int sourceTextureId = -1,
        string sourceRuntimeKey = "",
        int sourceWadEntry = -1,
        CancellationToken cancellationToken = default)
    {
        if (textureId < 0)
            throw new ArgumentOutOfRangeException(nameof(textureId), "Texture ID must be 0 or greater.");
        if (string.IsNullOrWhiteSpace(sourceImagePath))
            throw new ArgumentException("Source image path is required.", nameof(sourceImagePath));

        string normalizedTier = NormalizeTier(descriptorTier);
        Dictionary<string, CustomTerrainTextureImport> imports = Load(workspaceRoot, levelKey)
            .ToDictionary(TextureKey, StringComparer.OrdinalIgnoreCase);
        imports.Remove($"{textureId}:both");
        if (normalizedTier == "both")
        {
            imports.Remove($"{textureId}:hqData");
            imports.Remove($"{textureId}:hqDataClose");
        }

        imports[$"{textureId}:{normalizedTier}"] = new CustomTerrainTextureImport(
            TextureId: textureId,
            SourceImagePath: sourceImagePath,
            SourceImageName: string.IsNullOrWhiteSpace(sourceImageName) ? Path.GetFileName(sourceImagePath) : sourceImageName,
            DescriptorTier: normalizedTier,
            TileSize: tileSize > 0 ? tileSize : 64,
            SourceKind: string.IsNullOrWhiteSpace(sourceKind) ? "imported-image" : sourceKind,
            PaletteName: paletteName ?? "",
            PaletteLowHex: paletteLowHex ?? "",
            PaletteHighHex: paletteHighHex ?? "",
            PaletteHexColors: NormalizePaletteHexColors(paletteHexColors, paletteLowHex ?? "", paletteHighHex ?? ""),
            CreatedAt: DateTime.UtcNow.ToString("o"),
            SourceLevelKey: sourceLevelKey ?? "",
            SourceTextureId: sourceTextureId,
            SourceRuntimeKey: sourceRuntimeKey ?? "",
            SourceWadEntry: sourceWadEntry);

        IReadOnlyList<CustomTerrainTextureImport> ordered = imports.Values
            .OrderBy(texture => texture.TextureId)
            .ToArray();
        await SaveAsync(workspaceRoot, levelKey, levelName, ordered, cancellationToken);
        return ordered;
    }

    public static async Task SaveManifestAsync(
        string path,
        string levelKey,
        string levelName,
        IReadOnlyList<CustomTerrainTextureImport> imports,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var root = new
        {
            generatedBy = "Spyro.Editor.Core",
            levelName,
            levelKey,
            purpose = $"{levelName} custom terrain texture imports for the texture-page patch exporter.",
            updatedUtc = DateTime.UtcNow.ToString("o"),
            textureCount = imports.Count,
            textures = imports.Select(texture => new
            {
                textureId = texture.TextureId,
                sourceImagePath = texture.SourceImagePath,
                sourceImageName = texture.SourceImageName,
                descriptorTier = texture.DescriptorTier,
                tileSize = texture.TileSize,
                sourceKind = texture.SourceKind,
                paletteName = texture.PaletteName,
                paletteLowHex = texture.PaletteLowHex,
                paletteHighHex = texture.PaletteHighHex,
                paletteHexColors = texture.PaletteHexColors,
                sourceLevelKey = texture.SourceLevelKey,
                sourceTextureId = texture.SourceTextureId,
                sourceRuntimeKey = texture.SourceRuntimeKey,
                sourceWadEntry = texture.SourceWadEntry,
                createdAt = texture.CreatedAt
            }).ToArray()
        };

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private static async Task SaveAsync(
        string workspaceRoot,
        string levelKey,
        string levelName,
        IReadOnlyList<CustomTerrainTextureImport> imports,
        CancellationToken cancellationToken)
    {
        await SaveManifestAsync(ManifestPath(workspaceRoot, levelKey), levelKey, levelName, imports, cancellationToken);
    }

    private static string NormalizeTier(string tier)
    {
        if (string.Equals(tier, "both", StringComparison.OrdinalIgnoreCase))
            return "both";

        return string.Equals(tier, "hqDataClose", StringComparison.OrdinalIgnoreCase)
            ? "hqDataClose"
            : "hqData";
    }

    private static string TextureKey(CustomTerrainTextureImport texture)
    {
        return $"{texture.TextureId}:{NormalizeTier(texture.DescriptorTier)}";
    }

    private static IReadOnlyList<string> ReadPaletteHexColors(JsonElement texture)
    {
        if (!texture.TryGetProperty("paletteHexColors", out JsonElement colors) || colors.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return colors
            .EnumerateArray()
            .Where(color => color.ValueKind == JsonValueKind.String)
            .Select(color => color.GetString() ?? "")
            .Select(NormalizeHexText)
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizePaletteHexColors(IReadOnlyList<string>? colors, string lowHex, string highHex)
    {
        List<string> normalized = new();
        if (colors != null)
        {
            normalized.AddRange(colors
                .Select(NormalizeHexText)
                .Where(color => !string.IsNullOrWhiteSpace(color)));
        }

        if (normalized.Count == 0)
        {
            normalized.Add(NormalizeHexText(lowHex));
            normalized.Add(NormalizeHexText(highHex));
        }

        return normalized
            .Where(color => !string.IsNullOrWhiteSpace(color))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeHexText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string text = value.Trim();
        if (!text.StartsWith("#", StringComparison.Ordinal))
            text = $"#{text}";
        if (text.Length != 7)
            return "";

        return text.ToUpperInvariant();
    }
}

public sealed record CustomTerrainTextureImport(
    int TextureId,
    string SourceImagePath,
    string SourceImageName,
    string DescriptorTier,
    int TileSize,
    string SourceKind = "imported-image",
    string PaletteName = "",
    string PaletteLowHex = "",
    string PaletteHighHex = "",
    IReadOnlyList<string>? PaletteHexColors = null,
    string CreatedAt = "",
    string SourceLevelKey = "",
    int SourceTextureId = -1,
    string SourceRuntimeKey = "",
    int SourceWadEntry = -1);
