using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Editing;

public enum NativeTerrainTextureRelocationApplyMode
{
    ArtAndNativeSurface,
    ArtOnlyPreserveTarget
}

/// <summary>
/// Persists native cross-level terrain texture relocation intent separately from
/// imported-image texture edits. This store does not make relocation export-safe;
/// the exporter must still require runtime-control and texture-page ownership proof.
/// </summary>
public static class NativeTerrainTextureRelocationEditStore
{
    public const string CompleteDescriptorTier = "both";
    public const string ArtAndNativeSurfaceMode = "art-and-native-surface";
    public const string ArtOnlyPreserveTargetMode = "art-only-preserve-target";
    public const string NativeTextureRecordProvenancePrefix = "native-texture-record";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ManifestPath(string workspaceRoot, string destinationLevelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        string normalizedLevelKey = LevelCatalog.NormalizeKey(destinationLevelKey);
        if (string.IsNullOrWhiteSpace(normalizedLevelKey))
            throw new ArgumentException("Destination level key is required.", nameof(destinationLevelKey));

        return Path.Combine(workspaceRoot, $"{normalizedLevelKey}-native-terrain-texture-relocations.json");
    }

    public static IReadOnlyList<NativeTerrainTextureRelocationEdit> Load(
        string workspaceRoot,
        string destinationLevelKey)
    {
        string normalizedLevelKey = LevelCatalog.NormalizeKey(destinationLevelKey);
        return LoadManifest(ManifestPath(workspaceRoot, normalizedLevelKey), normalizedLevelKey);
    }

    public static IReadOnlyList<NativeTerrainTextureRelocationEdit> LoadManifest(
        string path,
        string expectedDestinationLevelKey = "")
    {
        if (!File.Exists(path))
            return Array.Empty<NativeTerrainTextureRelocationEdit>();

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Array.Empty<NativeTerrainTextureRelocationEdit>();
            int manifestVersion = JsonValue.GetInt32(root, "version", 1);
            if (manifestVersion is not (1 or 2))
                return Array.Empty<NativeTerrainTextureRelocationEdit>();

            string expectedKey = LevelCatalog.NormalizeKey(expectedDestinationLevelKey);
            string savedKey = LevelCatalog.NormalizeKey(JsonValue.GetString(root, "destinationLevelKey"));
            if (!string.IsNullOrWhiteSpace(expectedKey) &&
                !string.Equals(expectedKey, savedKey, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<NativeTerrainTextureRelocationEdit>();
            }

            if (!root.TryGetProperty("relocations", out JsonElement relocations) ||
                relocations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<NativeTerrainTextureRelocationEdit>();
            }

            Dictionary<int, NativeTerrainTextureRelocationEdit> byTargetTextureId = new();
            foreach (JsonElement relocation in relocations.EnumerateArray())
            {
                if (relocation.ValueKind != JsonValueKind.Object ||
                    !TryReadAndNormalize(relocation, manifestVersion, out NativeTerrainTextureRelocationEdit edit))
                {
                    continue;
                }

                // A malformed manifest can contain duplicate targets. Last valid
                // entry wins deterministically, matching AddOrReplace semantics.
                byTargetTextureId[edit.TargetTextureId] = edit;
            }

            return Order(byTargetTextureId.Values);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Array.Empty<NativeTerrainTextureRelocationEdit>();
        }
    }

    public static async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>> AddOrReplaceAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        int targetTextureId,
        string donorLevelKey,
        string donorLevelName,
        int donorWadEntry,
        int donorTextureId,
        string donorRuntimeKey,
        string previewImagePath = "",
        string previewImageName = "",
        CancellationToken cancellationToken = default)
    {
        return await AddOrReplaceCoreAsync(
            workspaceRoot,
            destinationLevelKey,
            destinationLevelName,
            targetTextureId,
            donorLevelKey,
            donorLevelName,
            donorWadEntry,
            donorTextureId,
            donorRuntimeKey,
            NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface,
            previewImagePath,
            previewImageName,
            cancellationToken);
    }

    public static async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>> AddOrReplaceArtOnlyAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        int targetTextureId,
        string donorLevelKey,
        string donorLevelName,
        int donorWadEntry,
        int donorTextureId,
        string previewImagePath = "",
        string previewImageName = "",
        CancellationToken cancellationToken = default)
    {
        string provenance = BuildTextureRecordProvenanceKey(donorLevelKey, donorTextureId);
        return await AddOrReplaceCoreAsync(
            workspaceRoot,
            destinationLevelKey,
            destinationLevelName,
            targetTextureId,
            donorLevelKey,
            donorLevelName,
            donorWadEntry,
            donorTextureId,
            provenance,
            NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
            previewImagePath,
            previewImageName,
            cancellationToken);
    }

    public static string BuildTextureRecordProvenanceKey(string donorLevelKey, int donorTextureId)
    {
        string normalizedLevelKey = LevelCatalog.NormalizeKey(donorLevelKey);
        if (string.IsNullOrWhiteSpace(normalizedLevelKey))
            throw new ArgumentException("Donor level key is required.", nameof(donorLevelKey));
        if (donorTextureId < 0)
            throw new ArgumentOutOfRangeException(nameof(donorTextureId), "Donor texture ID must be 0 or greater.");
        return $"{NativeTextureRecordProvenancePrefix}:{normalizedLevelKey}:{donorTextureId}";
    }

    private static async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>> AddOrReplaceCoreAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        int targetTextureId,
        string donorLevelKey,
        string donorLevelName,
        int donorWadEntry,
        int donorTextureId,
        string donorRuntimeKey,
        NativeTerrainTextureRelocationApplyMode applyMode,
        string previewImagePath,
        string previewImageName,
        CancellationToken cancellationToken)
    {
        Dictionary<int, NativeTerrainTextureRelocationEdit> edits = Load(workspaceRoot, destinationLevelKey)
            .ToDictionary(edit => edit.TargetTextureId);
        edits.TryGetValue(targetTextureId, out NativeTerrainTextureRelocationEdit? existing);

        NativeTerrainTextureRelocationEdit candidate = new(
            TargetTextureId: targetTextureId,
            DonorLevelKey: donorLevelKey,
            DonorLevelName: donorLevelName,
            DonorWadEntry: donorWadEntry,
            DonorTextureId: donorTextureId,
            DonorRuntimeKey: donorRuntimeKey,
            DescriptorTier: CompleteDescriptorTier,
            PreviewImagePath: previewImagePath,
            PreviewImageName: previewImageName,
            CreatedAt: existing?.CreatedAt ?? DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ApplyMode: applyMode);
        edits[targetTextureId] = NormalizeForSave(candidate, nameof(candidate));

        IReadOnlyList<NativeTerrainTextureRelocationEdit> ordered = Order(edits.Values);
        await SaveManifestAsync(
            ManifestPath(workspaceRoot, destinationLevelKey),
            destinationLevelKey,
            destinationLevelName,
            ordered,
            cancellationToken);
        return ordered;
    }

    public static async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>> RemoveAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        int targetTextureId,
        CancellationToken cancellationToken = default)
    {
        if (targetTextureId < 0)
            throw new ArgumentOutOfRangeException(nameof(targetTextureId), "Target texture ID must be 0 or greater.");

        Dictionary<int, NativeTerrainTextureRelocationEdit> edits = Load(workspaceRoot, destinationLevelKey)
            .ToDictionary(edit => edit.TargetTextureId);
        if (!edits.Remove(targetTextureId))
            return Order(edits.Values);

        IReadOnlyList<NativeTerrainTextureRelocationEdit> ordered = Order(edits.Values);
        string manifestPath = ManifestPath(workspaceRoot, destinationLevelKey);
        if (ordered.Count == 0)
        {
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
            return ordered;
        }

        await SaveManifestAsync(
            manifestPath,
            destinationLevelKey,
            destinationLevelName,
            ordered,
            cancellationToken);
        return ordered;
    }

    public static async Task SaveManifestAsync(
        string path,
        string destinationLevelKey,
        string destinationLevelName,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> edits,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(edits);
        string normalizedDestinationKey = LevelCatalog.NormalizeKey(destinationLevelKey);
        if (string.IsNullOrWhiteSpace(normalizedDestinationKey))
            throw new ArgumentException("Destination level key is required.", nameof(destinationLevelKey));
        if (string.IsNullOrWhiteSpace(destinationLevelName))
            throw new ArgumentException("Destination level name is required.", nameof(destinationLevelName));

        Dictionary<int, NativeTerrainTextureRelocationEdit> byTargetTextureId = new();
        for (int index = 0; index < edits.Count; index++)
        {
            NativeTerrainTextureRelocationEdit normalized = NormalizeForSave(edits[index], $"{nameof(edits)}[{index}]");
            byTargetTextureId[normalized.TargetTextureId] = normalized;
        }

        IReadOnlyList<NativeTerrainTextureRelocationEdit> ordered = Order(byTargetTextureId.Values);
        var root = new
        {
            version = 2,
            generatedBy = "Spyro.Editor.Core",
            destinationLevelKey = normalizedDestinationKey,
            destinationLevelName = destinationLevelName.Trim(),
            purpose = "Native cross-level terrain texture relocation intent. Export remains proof-gated.",
            relocationCount = ordered.Count,
            relocations = ordered.Select(edit => new
            {
                targetTextureId = edit.TargetTextureId,
                donorLevelKey = edit.DonorLevelKey,
                donorLevelName = edit.DonorLevelName,
                donorWadEntry = edit.DonorWadEntry,
                donorTextureId = edit.DonorTextureId,
                donorRuntimeKey = edit.DonorRuntimeKey,
                applyMode = ApplyModeName(edit.ApplyMode),
                descriptorTier = edit.DescriptorTier,
                previewImagePath = edit.PreviewImagePath,
                previewImageName = edit.PreviewImageName,
                createdAt = edit.CreatedAt
            }).ToArray()
        };

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, root, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool TryReadAndNormalize(
        JsonElement relocation,
        int manifestVersion,
        out NativeTerrainTextureRelocationEdit edit)
    {
        edit = null!;
        if (!TryReadApplyMode(relocation, manifestVersion, out NativeTerrainTextureRelocationApplyMode applyMode))
            return false;
        NativeTerrainTextureRelocationEdit candidate = new(
            TargetTextureId: JsonValue.GetInt32(relocation, "targetTextureId", -1),
            DonorLevelKey: JsonValue.GetString(relocation, "donorLevelKey"),
            DonorLevelName: JsonValue.GetString(relocation, "donorLevelName"),
            DonorWadEntry: JsonValue.GetInt32(relocation, "donorWadEntry", -1),
            DonorTextureId: JsonValue.GetInt32(relocation, "donorTextureId", -1),
            DonorRuntimeKey: JsonValue.GetString(relocation, "donorRuntimeKey"),
            DescriptorTier: JsonValue.GetString(relocation, "descriptorTier"),
            PreviewImagePath: JsonValue.GetString(relocation, "previewImagePath"),
            PreviewImageName: JsonValue.GetString(relocation, "previewImageName"),
            CreatedAt: JsonValue.GetString(relocation, "createdAt"),
            ApplyMode: applyMode);

        if (!TryNormalize(candidate, out NativeTerrainTextureRelocationEdit normalized))
            return false;

        edit = normalized;
        return true;
    }

    private static NativeTerrainTextureRelocationEdit NormalizeForSave(
        NativeTerrainTextureRelocationEdit edit,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!TryNormalize(edit, out NativeTerrainTextureRelocationEdit normalized))
        {
            throw new ArgumentException(
                "A relocation edit requires non-negative target/donor IDs, valid donor provenance/apply mode, descriptor tier 'both', and a valid createdAt timestamp.",
                parameterName);
        }

        return normalized;
    }

    private static bool TryNormalize(
        NativeTerrainTextureRelocationEdit edit,
        out NativeTerrainTextureRelocationEdit normalized)
    {
        normalized = null!;
        string donorLevelKey = LevelCatalog.NormalizeKey(edit.DonorLevelKey);
        string donorLevelName = (edit.DonorLevelName ?? "").Trim();
        string donorRuntimeKey = (edit.DonorRuntimeKey ?? "").Trim();
        if (edit.TargetTextureId < 0 ||
            edit.DonorWadEntry < 0 ||
            edit.DonorTextureId < 0 ||
            string.IsNullOrWhiteSpace(donorLevelKey) ||
            string.IsNullOrWhiteSpace(donorLevelName) ||
            string.IsNullOrWhiteSpace(donorRuntimeKey) ||
            !Enum.IsDefined(edit.ApplyMode) ||
            !string.Equals(edit.DescriptorTier, CompleteDescriptorTier, StringComparison.OrdinalIgnoreCase) ||
            !DateTimeOffset.TryParse(
                edit.CreatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset createdAt))
        {
            return false;
        }

        if (edit.ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget &&
            !string.Equals(
                donorRuntimeKey,
                BuildTextureRecordProvenanceKey(donorLevelKey, edit.DonorTextureId),
                StringComparison.Ordinal))
        {
            return false;
        }

        string previewPath = (edit.PreviewImagePath ?? "").Trim();
        string previewName = (edit.PreviewImageName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(previewPath) && string.IsNullOrWhiteSpace(previewName))
            previewName = Path.GetFileName(previewPath);

        normalized = edit with
        {
            DonorLevelKey = donorLevelKey,
            DonorLevelName = donorLevelName,
            DonorRuntimeKey = donorRuntimeKey,
            DescriptorTier = CompleteDescriptorTier,
            PreviewImagePath = previewPath,
            PreviewImageName = previewName,
            CreatedAt = createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };
        return true;
    }

    private static bool TryReadApplyMode(
        JsonElement relocation,
        int manifestVersion,
        out NativeTerrainTextureRelocationApplyMode applyMode)
    {
        applyMode = NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface;
        if (!relocation.TryGetProperty("applyMode", out JsonElement element))
            return manifestVersion == 1;
        if (element.ValueKind != JsonValueKind.String)
            return false;

        string value = (element.GetString() ?? "").Trim();
        if (string.Equals(value, ArtAndNativeSurfaceMode, StringComparison.OrdinalIgnoreCase))
        {
            applyMode = NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface;
            return true;
        }
        if (string.Equals(value, ArtOnlyPreserveTargetMode, StringComparison.OrdinalIgnoreCase))
        {
            applyMode = NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget;
            return true;
        }
        return false;
    }

    private static string ApplyModeName(NativeTerrainTextureRelocationApplyMode applyMode) =>
        applyMode switch
        {
            NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface => ArtAndNativeSurfaceMode,
            NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget => ArtOnlyPreserveTargetMode,
            _ => throw new ArgumentOutOfRangeException(nameof(applyMode), applyMode, "Unsupported native terrain texture relocation apply mode.")
        };

    private static IReadOnlyList<NativeTerrainTextureRelocationEdit> Order(
        IEnumerable<NativeTerrainTextureRelocationEdit> edits)
    {
        return edits
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
    }
}

public sealed record NativeTerrainTextureRelocationEdit(
    int TargetTextureId,
    string DonorLevelKey,
    string DonorLevelName,
    int DonorWadEntry,
    int DonorTextureId,
    string DonorRuntimeKey,
    string DescriptorTier,
    string PreviewImagePath,
    string PreviewImageName,
    string CreatedAt,
    NativeTerrainTextureRelocationApplyMode ApplyMode = NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface)
{
    public bool PreservesTargetNativeSurface =>
        ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget;

    // Historical field name retained for project compatibility. In art-only
    // mode this is a canonical native texture-record provenance key, never a
    // fabricated terrain-face runtime key.
    public string DonorProvenanceKey => DonorRuntimeKey;
}
