using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Editing;

public enum NativeTerrainTextureRelocationApplyMode
{
    ArtAndNativeSurface,
    ArtOnlyPreserveTarget
}

public enum NativeTerrainTextureTargetRecordKind
{
    ExistingNative,
    AppendedPrivate
}

public sealed record NativeTerrainTextureAppendedPrivateAllocation(
    IReadOnlyList<NativeTerrainTextureRelocationEdit> Edits,
    int AssignedTextureId,
    bool ReusedExistingRecord,
    bool AddedNewRecord);

public sealed record NativeTerrainTextureAppendedPrivateCompaction(
    IReadOnlyList<NativeTerrainTextureRelocationEdit> Edits,
    IReadOnlyDictionary<int, int> TextureIdRemap,
    int SourceTextureRecordCount,
    int AppendedRecordCount,
    int DuplicateContractCount,
    bool Changed);

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
    public const string ExistingNativeTargetRecordKind = "existing-native";
    public const string AppendedPrivateTargetRecordKind = "appended-private";

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
            if (manifestVersion is not (1 or 2 or 3))
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
                if (manifestVersion == 3)
                {
                    throw new InvalidDataException(
                        "Version-3 native terrain texture manifest is missing its relocations array.");
                }
                return Array.Empty<NativeTerrainTextureRelocationEdit>();
            }

            Dictionary<int, NativeTerrainTextureRelocationEdit> byTargetTextureId = new();
            int rowIndex = 0;
            foreach (JsonElement relocation in relocations.EnumerateArray())
            {
                if (relocation.ValueKind != JsonValueKind.Object ||
                    !TryReadAndNormalize(relocation, manifestVersion, out NativeTerrainTextureRelocationEdit edit))
                {
                    if (manifestVersion == 3)
                    {
                        throw new InvalidDataException(
                            $"Version-3 native terrain texture manifest row {rowIndex} is malformed; no relocation row was loaded.");
                    }
                    rowIndex++;
                    continue;
                }

                if (manifestVersion == 3 && byTargetTextureId.ContainsKey(edit.TargetTextureId))
                {
                    throw new InvalidDataException(
                        $"Version-3 native terrain texture manifest contains duplicate target T{edit.TargetTextureId}; no relocation row was loaded.");
                }
                // Legacy manifests predate strict transactional loading. Keep
                // their deterministic last-valid-row migration behavior.
                byTargetTextureId[edit.TargetTextureId] = edit;
                rowIndex++;
            }

            return Order(byTargetTextureId.Values);
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or JsonException ||
            (ex is IOException && ex is not InvalidDataException))
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

    public static async Task<IReadOnlyList<NativeTerrainTextureRelocationEdit>> AddOrReplaceAppendedPrivateArtOnlyAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        int targetTextureId,
        string donorLevelKey,
        string donorLevelName,
        int donorWadEntry,
        int donorTextureId,
        int targetWadEntry,
        string sourceImageSha256,
        int sourceTextureRecordCount,
        string sourceTextureComponentSha256,
        string sourceLevelDataSha256,
        int materialTemplateTextureId,
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
            cancellationToken,
            NativeTerrainTextureTargetRecordKind.AppendedPrivate,
            targetWadEntry,
            sourceImageSha256,
            sourceTextureRecordCount,
            sourceTextureComponentSha256,
            sourceLevelDataSha256,
            materialTemplateTextureId,
            BuildAppendedPrivateRecordId(targetTextureId));
    }

    /// <summary>
    /// Reuses one already-staged private record when the native donor/material
    /// contract is identical; otherwise allocates the next contiguous retail-
    /// tail id. This is the only safe allocator for newly staged appended
    /// records because gaps would make the native table id disagree with the
    /// face's seven-bit texture id.
    /// </summary>
    public static async Task<NativeTerrainTextureAppendedPrivateAllocation> AddOrReuseAppendedPrivateArtOnlyAsync(
        string workspaceRoot,
        string destinationLevelKey,
        string destinationLevelName,
        string donorLevelKey,
        string donorLevelName,
        int donorWadEntry,
        int donorTextureId,
        int targetWadEntry,
        string sourceImageSha256,
        int sourceTextureRecordCount,
        string sourceTextureComponentSha256,
        string sourceLevelDataSha256,
        int materialTemplateTextureId,
        string previewImagePath = "",
        string previewImageName = "",
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NativeTerrainTextureRelocationEdit> current =
            Load(workspaceRoot, destinationLevelKey);
        NativeTerrainTextureAppendedPrivateCompaction normalized =
            CompactAppendedPrivateRecords(current);
        if (normalized.Changed)
        {
            throw new InvalidOperationException(
                "The saved appended-private texture table needs face-ID compaction before another record can be allocated. Compact the manifest and its terrain-face assignments in one transaction first.");
        }

        NativeTerrainTextureRelocationEdit? reusable = current.FirstOrDefault(edit =>
            edit.UsesAppendedPrivateRecord &&
            edit.DonorWadEntry == donorWadEntry &&
            edit.DonorTextureId == donorTextureId &&
            edit.MaterialTemplateTextureId == materialTemplateTextureId &&
            edit.TargetWadEntry == targetWadEntry &&
            edit.SourceTextureRecordCount == sourceTextureRecordCount &&
            string.Equals(edit.SourceImageSha256, NormalizeSha256(sourceImageSha256), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edit.SourceTextureComponentSha256, NormalizeSha256(sourceTextureComponentSha256), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edit.SourceLevelDataSha256, NormalizeSha256(sourceLevelDataSha256), StringComparison.OrdinalIgnoreCase));
        if (reusable != null)
        {
            return new NativeTerrainTextureAppendedPrivateAllocation(
                current,
                reusable.TargetTextureId,
                ReusedExistingRecord: true,
                AddedNewRecord: false);
        }

        NativeTerrainTextureRelocationEdit[] appended = current
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
        if (appended.Any(edit =>
                edit.SourceTextureRecordCount != sourceTextureRecordCount ||
                edit.TargetWadEntry != targetWadEntry ||
                !string.Equals(edit.SourceImageSha256, NormalizeSha256(sourceImageSha256), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(edit.SourceTextureComponentSha256, NormalizeSha256(sourceTextureComponentSha256), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(edit.SourceLevelDataSha256, NormalizeSha256(sourceLevelDataSha256), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Existing private texture records are bound to a different disc, WAD entry, texture table, or level-data preimage.");
        }

        int assignedTextureId = checked(sourceTextureRecordCount + appended.Length);
        if (assignedTextureId > 127)
            throw new InvalidOperationException("No seven-bit native terrain texture id remains for another private record.");
        IReadOnlyList<NativeTerrainTextureRelocationEdit> saved =
            await AddOrReplaceAppendedPrivateArtOnlyAsync(
                workspaceRoot,
                destinationLevelKey,
                destinationLevelName,
                assignedTextureId,
                donorLevelKey,
                donorLevelName,
                donorWadEntry,
                donorTextureId,
                targetWadEntry,
                sourceImageSha256,
                sourceTextureRecordCount,
                sourceTextureComponentSha256,
                sourceLevelDataSha256,
                materialTemplateTextureId,
                previewImagePath,
                previewImageName,
                cancellationToken);
        return new NativeTerrainTextureAppendedPrivateAllocation(
            saved,
            assignedTextureId,
            ReusedExistingRecord: false,
            AddedNewRecord: true);
    }

    /// <summary>
    /// Produces the only legal appended table order after undo/removal. Exact
    /// donor/material contracts collapse to one row and all remaining rows are
    /// reassigned contiguously from the retail texture count. The caller must
    /// apply <see cref="NativeTerrainTextureAppendedPrivateCompaction.TextureIdRemap"/>
    /// to every face before saving this manifest; this method deliberately does
    /// not mutate a project by itself.
    /// </summary>
    public static NativeTerrainTextureAppendedPrivateCompaction CompactAppendedPrivateRecords(
        IReadOnlyList<NativeTerrainTextureRelocationEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        NativeTerrainTextureRelocationEdit[] existing = edits
            .Where(edit => !edit.UsesAppendedPrivateRecord)
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
        NativeTerrainTextureRelocationEdit[] appended = edits
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .OrderBy(edit => edit.TargetTextureId)
            .ThenBy(edit => edit.CreatedAt, StringComparer.Ordinal)
            .ToArray();
        if (appended.Length == 0)
        {
            return new NativeTerrainTextureAppendedPrivateCompaction(
                existing,
                new Dictionary<int, int>(),
                -1,
                0,
                0,
                Changed: false);
        }

        int sourceCount = appended[0].SourceTextureRecordCount;
        int targetWadEntry = appended[0].TargetWadEntry;
        string sourceImageSha256 = appended[0].SourceImageSha256;
        string componentSha256 = appended[0].SourceTextureComponentSha256;
        string levelDataSha256 = appended[0].SourceLevelDataSha256;
        if (appended.Any(edit =>
                edit.SourceTextureRecordCount != sourceCount ||
                edit.TargetWadEntry != targetWadEntry ||
                !string.Equals(edit.SourceImageSha256, sourceImageSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(edit.SourceTextureComponentSha256, componentSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(edit.SourceLevelDataSha256, levelDataSha256, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "Appended private texture records do not share one source-disc/table/level-data binding and cannot be compacted safely.");
        }

        Dictionary<AppendedPrivateContract, NativeTerrainTextureRelocationEdit> canonical = [];
        List<NativeTerrainTextureRelocationEdit> orderedCanonical = [];
        Dictionary<int, AppendedPrivateContract> contractByOldId = [];
        foreach (NativeTerrainTextureRelocationEdit edit in appended)
        {
            AppendedPrivateContract contract = new(
                edit.DonorWadEntry,
                edit.DonorTextureId,
                edit.MaterialTemplateTextureId,
                edit.ApplyMode);
            contractByOldId[edit.TargetTextureId] = contract;
            if (canonical.TryAdd(contract, edit))
                orderedCanonical.Add(edit);
        }

        if (sourceCount + orderedCanonical.Count > 128)
            throw new InvalidOperationException("Compacted private textures would exceed the native seven-bit texture-id table.");

        Dictionary<AppendedPrivateContract, int> newIdByContract = [];
        List<NativeTerrainTextureRelocationEdit> compacted = [.. existing];
        for (int index = 0; index < orderedCanonical.Count; index++)
        {
            NativeTerrainTextureRelocationEdit edit = orderedCanonical[index];
            int targetId = sourceCount + index;
            AppendedPrivateContract contract = new(
                edit.DonorWadEntry,
                edit.DonorTextureId,
                edit.MaterialTemplateTextureId,
                edit.ApplyMode);
            newIdByContract[contract] = targetId;
            compacted.Add(edit with
            {
                TargetTextureId = targetId,
                PrivateRecordEditId = BuildAppendedPrivateRecordId(targetId)
            });
        }

        Dictionary<int, int> remap = contractByOldId.ToDictionary(
            pair => pair.Key,
            pair => newIdByContract[pair.Value]);
        IReadOnlyList<NativeTerrainTextureRelocationEdit> ordered = Order(compacted);
        bool changed = remap.Any(pair => pair.Key != pair.Value) ||
            ordered.Count != edits.Count ||
            !ordered.SequenceEqual(Order(edits));
        return new NativeTerrainTextureAppendedPrivateCompaction(
            ordered,
            remap,
            sourceCount,
            orderedCanonical.Count,
            appended.Length - orderedCanonical.Count,
            changed);
    }

    public static string BuildAppendedPrivateRecordId(int targetTextureId)
    {
        if (targetTextureId is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(targetTextureId), "A terrain texture ID must fit the native 7-bit face field.");
        return $"appended-private-terrain-texture:T{targetTextureId:000}";
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
        CancellationToken cancellationToken,
        NativeTerrainTextureTargetRecordKind targetRecordKind = NativeTerrainTextureTargetRecordKind.ExistingNative,
        int targetWadEntry = -1,
        string sourceImageSha256 = "",
        int sourceTextureRecordCount = -1,
        string sourceTextureComponentSha256 = "",
        string sourceLevelDataSha256 = "",
        int materialTemplateTextureId = -1,
        string privateRecordEditId = "")
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
            ApplyMode: applyMode,
            TargetRecordKind: targetRecordKind,
            TargetWadEntry: targetWadEntry,
            SourceImageSha256: sourceImageSha256,
            SourceTextureRecordCount: sourceTextureRecordCount,
            SourceTextureComponentSha256: sourceTextureComponentSha256,
            SourceLevelDataSha256: sourceLevelDataSha256,
            MaterialTemplateTextureId: materialTemplateTextureId,
            PrivateRecordEditId: privateRecordEditId);
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
            version = 3,
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
                targetRecordKind = TargetRecordKindName(edit.TargetRecordKind),
                targetWadEntry = edit.TargetWadEntry,
                sourceImageSha256 = edit.SourceImageSha256,
                sourceTextureRecordCount = edit.SourceTextureRecordCount,
                sourceTextureComponentSha256 = edit.SourceTextureComponentSha256,
                sourceLevelDataSha256 = edit.SourceLevelDataSha256,
                materialTemplateTextureId = edit.MaterialTemplateTextureId,
                privateRecordEditId = edit.PrivateRecordEditId,
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
        if (!TryReadTargetRecordKind(relocation, manifestVersion, out NativeTerrainTextureTargetRecordKind targetRecordKind))
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
            ApplyMode: applyMode,
            TargetRecordKind: targetRecordKind,
            TargetWadEntry: JsonValue.GetInt32(relocation, "targetWadEntry", -1),
            SourceImageSha256: JsonValue.GetString(relocation, "sourceImageSha256"),
            SourceTextureRecordCount: JsonValue.GetInt32(relocation, "sourceTextureRecordCount", -1),
            SourceTextureComponentSha256: JsonValue.GetString(relocation, "sourceTextureComponentSha256"),
            SourceLevelDataSha256: JsonValue.GetString(relocation, "sourceLevelDataSha256"),
            MaterialTemplateTextureId: JsonValue.GetInt32(relocation, "materialTemplateTextureId", -1),
            PrivateRecordEditId: JsonValue.GetString(relocation, "privateRecordEditId"));

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
        if (edit.TargetTextureId is < 0 or > 127 ||
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

        string sourceImageSha256 = NormalizeSha256(edit.SourceImageSha256);
        string sourceTextureComponentSha256 = NormalizeSha256(edit.SourceTextureComponentSha256);
        string sourceLevelDataSha256 = NormalizeSha256(edit.SourceLevelDataSha256);
        string privateRecordEditId = (edit.PrivateRecordEditId ?? "").Trim();
        if (edit.TargetRecordKind == NativeTerrainTextureTargetRecordKind.AppendedPrivate)
        {
            if (edit.ApplyMode != NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget ||
                edit.TargetWadEntry < 0 ||
                sourceImageSha256.Length != 64 ||
                edit.SourceTextureRecordCount is < 1 or > 127 ||
                edit.TargetTextureId < edit.SourceTextureRecordCount ||
                edit.MaterialTemplateTextureId < 0 ||
                edit.MaterialTemplateTextureId >= edit.SourceTextureRecordCount ||
                sourceTextureComponentSha256.Length != 64 ||
                sourceLevelDataSha256.Length != 64 ||
                !string.Equals(
                    privateRecordEditId,
                    BuildAppendedPrivateRecordId(edit.TargetTextureId),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        else if (edit.TargetWadEntry != -1 ||
                 sourceImageSha256.Length != 0 ||
                 edit.SourceTextureRecordCount != -1 ||
                 edit.MaterialTemplateTextureId != -1 ||
                 sourceTextureComponentSha256.Length != 0 ||
                 sourceLevelDataSha256.Length != 0 ||
                 privateRecordEditId.Length != 0)
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
            CreatedAt = createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            SourceImageSha256 = sourceImageSha256,
            SourceTextureComponentSha256 = sourceTextureComponentSha256,
            SourceLevelDataSha256 = sourceLevelDataSha256,
            PrivateRecordEditId = privateRecordEditId
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

    private static bool TryReadTargetRecordKind(
        JsonElement relocation,
        int manifestVersion,
        out NativeTerrainTextureTargetRecordKind targetRecordKind)
    {
        targetRecordKind = NativeTerrainTextureTargetRecordKind.ExistingNative;
        if (!relocation.TryGetProperty("targetRecordKind", out JsonElement element))
            return manifestVersion < 3;
        if (element.ValueKind != JsonValueKind.String)
            return false;

        string value = (element.GetString() ?? "").Trim();
        if (string.Equals(value, ExistingNativeTargetRecordKind, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(value, AppendedPrivateTargetRecordKind, StringComparison.OrdinalIgnoreCase))
        {
            targetRecordKind = NativeTerrainTextureTargetRecordKind.AppendedPrivate;
            return true;
        }
        return false;
    }

    private static string TargetRecordKindName(NativeTerrainTextureTargetRecordKind targetRecordKind) =>
        targetRecordKind switch
        {
            NativeTerrainTextureTargetRecordKind.ExistingNative => ExistingNativeTargetRecordKind,
            NativeTerrainTextureTargetRecordKind.AppendedPrivate => AppendedPrivateTargetRecordKind,
            _ => throw new ArgumentOutOfRangeException(nameof(targetRecordKind), targetRecordKind, "Unsupported native terrain texture target-record kind.")
        };

    private static string NormalizeSha256(string value)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized
            : "";
    }

    private static IReadOnlyList<NativeTerrainTextureRelocationEdit> Order(
        IEnumerable<NativeTerrainTextureRelocationEdit> edits)
    {
        return edits
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
    }

    private sealed record AppendedPrivateContract(
        int DonorWadEntry,
        int DonorTextureId,
        int MaterialTemplateTextureId,
        NativeTerrainTextureRelocationApplyMode ApplyMode);
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
    NativeTerrainTextureRelocationApplyMode ApplyMode = NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface,
    NativeTerrainTextureTargetRecordKind TargetRecordKind = NativeTerrainTextureTargetRecordKind.ExistingNative,
    int TargetWadEntry = -1,
    string SourceImageSha256 = "",
    int SourceTextureRecordCount = -1,
    string SourceTextureComponentSha256 = "",
    string SourceLevelDataSha256 = "",
    int MaterialTemplateTextureId = -1,
    string PrivateRecordEditId = "")
{
    public bool PreservesTargetNativeSurface =>
        ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget;

    // Historical field name retained for project compatibility. In art-only
    // mode this is a canonical native texture-record provenance key, never a
    // fabricated terrain-face runtime key.
    public string DonorProvenanceKey => DonorRuntimeKey;

    public bool UsesAppendedPrivateRecord =>
        TargetRecordKind == NativeTerrainTextureTargetRecordKind.AppendedPrivate;
}
