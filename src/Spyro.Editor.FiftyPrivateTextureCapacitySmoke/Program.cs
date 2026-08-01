using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int RequestedPrivateRows = 50;
Assert(
    NativeTerrainTextureRecordAppendBuilder.IsRequestedRecordCountAuthorized(
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch) &&
    !NativeTerrainTextureRecordAppendBuilder.IsRequestedRecordCountAuthorized(
        RequestedPrivateRows + 1,
        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch),
    "The core static-research policy must admit exactly 50 appended records and reject row 51.");

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal)));
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "fifty-private-texture-capacity");
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
string jsonPath = Path.Combine(outputRoot, "fifty-private-texture-capacity-census.json");
string markdownPath = Path.Combine(outputRoot, "fifty-private-texture-capacity-census.md");
string oneRowAuditPath = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "all-level-synthetic-global-texture-repack",
    "all-level-synthetic-global-texture-repack-audit.json");

Directory.CreateDirectory(outputRoot);
Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourceImagePath);
Dictionary<string, OneRowPackingResult> oneRowPacking =
    LoadOneRowPackingResults(oneRowAuditPath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] levels = LevelRealmCatalog
    .OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    .ToArray();
Assert(levels.Length == 35, $"Expected all 35 mapped retail levels, got {levels.Length}.");

List<CapacityRow> rows = [];
foreach (LevelDefinition level in levels)
{
    NativeTerrainTextureRecordAppendSourceBinding binding =
        NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
            sourceImagePath,
            level);
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(
            sourceImagePath,
            level);
    Assert(runtime.Complete && runtime.TextureCount == binding.ExpectedSourceTextureCount,
        $"{level.DisplayName}: runtime/table count proof did not close.");

    Dictionary<int, int> faceUseCounts =
        LoadFaceTextureUseCounts(workspaceRoot, level.Key);
    int editableTexturedFaceCount =
        LoadPrimaryCandidateTexturedFaceCount(workspaceRoot, level.Key);
    int[] unreferencedPersistentIds = Enumerable
        .Range(0, binding.ExpectedSourceTextureCount)
        .Where(textureId => !faceUseCounts.ContainsKey(textureId))
        .Where(runtime.IsRuntimePersistentTarget)
        // Animation frame sources are not overwritten, but the native scene
        // still reads them to initialize controlled destination rows. They
        // are diagnostic records, not free private-texture capacity.
        .Where(textureId =>
            !runtime.AnimationSourceTextureIds.Contains(textureId))
        .ToArray();
    int appendOnlyHeadroom =
        NativeTerrainTextureRecordAppendBuilder.MaximumTextureRecordCount -
        binding.ExpectedSourceTextureCount;
    bool directAppendIdFit = appendOnlyHeadroom >= RequestedPrivateRows;
    int optimisticMixedPrivateCapacity =
        appendOnlyHeadroom + unreferencedPersistentIds.Length;
    bool mixedIdFit = optimisticMixedPrivateCapacity >= RequestedPrivateRows;
    int appendedRowsRequiredForMixedFifty = Math.Max(
        0,
        RequestedPrivateRows - unreferencedPersistentIds.Length);

    NativeTerrainTextureRecordAppendCapacity? normalCapacity = null;
    NativeTerrainTextureRecordAppendCapacity? staticCapacity = null;
    NativeTerrainTextureRecordAppendCapacity? mixedStaticCapacity = null;
    string staticFailure = "";
    if (directAppendIdFit)
    {
        normalCapacity =
            NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
                sourceImagePath,
                level,
                RequestedPrivateRows,
                wadAnalysisPath);
        staticCapacity =
            NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
                sourceImagePath,
                level,
                RequestedPrivateRows,
                NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
                wadAnalysisPath);

        Assert(
            !normalCapacity.HasExecutableWriter &&
            normalCapacity.RequiredSectorAlignedEntryGrowthBytes >
                NativeTerrainTextureRecordAppendBuilder.MaximumCheckedSectorGrowthBytes,
            $"{level.DisplayName}: normal policy unexpectedly admitted fifty-row structural growth.");
        Assert(
            staticCapacity.HasExecutableWriter &&
            staticCapacity.ExecutableWriter == "SectorRelocation" &&
            staticCapacity.SectorRelocationPlanVerified &&
            staticCapacity.RequiredSectorAlignedEntryGrowthBytes is 0x2000 or 0x2800 &&
            staticCapacity.StaticResearchOnly,
            $"{level.DisplayName}: extended static policy did not prove exact +0x2000/+0x2800 growth.");
    }
    else
    {
        staticFailure =
            $"Retail T0..T{binding.ExpectedSourceTextureCount - 1} leaves " +
            $"{appendOnlyHeadroom} appended ID(s), below the requested {RequestedPrivateRows}.";
    }
    if (mixedIdFit)
    {
        mixedStaticCapacity =
            NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
                sourceImagePath,
                level,
                appendedRowsRequiredForMixedFifty,
                NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
                wadAnalysisPath);
        Assert(
            mixedStaticCapacity.HasExecutableWriter &&
            mixedStaticCapacity.SectorRelocationPlanVerified &&
            mixedStaticCapacity.StaticResearchOnly,
            $"{level.DisplayName}: current unused-native-first allocation did not prove the " +
            $"{appendedRowsRequiredForMixedFifty}-row structural append needed for fifty distinct records.");
    }

    oneRowPacking.TryGetValue(
        LevelCatalog.NormalizeKey(level.Key),
        out OneRowPackingResult? packing);
    rows.Add(new CapacityRow(
        LevelCatalog.NormalizeKey(level.Key),
        level.DisplayName,
        level.LevelId,
        level.SourceWadEntry,
        IsFlight(level),
        editableTexturedFaceCount,
        binding.ExpectedSourceTextureCount,
        appendOnlyHeadroom,
        unreferencedPersistentIds,
        optimisticMixedPrivateCapacity,
        directAppendIdFit,
        mixedIdFit,
        appendedRowsRequiredForMixedFifty,
        normalCapacity?.RequiredSectorAlignedEntryGrowthBytes,
        normalCapacity?.HasExecutableWriter,
        staticCapacity?.RequiredSectorAlignedEntryGrowthBytes,
        staticCapacity?.AvailableIsoGrowthBytes,
        staticCapacity?.SectorRelocationPlanVerified ?? false,
        staticCapacity?.HasExecutableWriter ?? false,
        mixedStaticCapacity?.RequiredSectorAlignedEntryGrowthBytes,
        mixedStaticCapacity?.SectorRelocationPlanVerified ?? false,
        mixedStaticCapacity?.HasExecutableWriter ?? false,
        packing?.Success,
        packing?.Failure ?? "",
        staticFailure,
        false));
    Console.WriteLine(
        $"{level.DisplayName,-18} retail={binding.ExpectedSourceTextureCount,3} " +
        $"append={appendOnlyHeadroom,3} unused={unreferencedPersistentIds.Length,2} " +
        $"direct50={(directAppendIdFit ? "yes" : "no "),-3} " +
        $"mixedAppend={appendedRowsRequiredForMixedFifty,2} " +
        $"mixedStatic={(mixedStaticCapacity?.HasExecutableWriter == true ? $"+0x{mixedStaticCapacity.RequiredSectorAlignedEntryGrowthBytes:X}" : "blocked")}");
}

int directAppendLevels = rows.Count(row => row.DirectAppendIdFit);
int mixedIdCandidateLevels = rows.Count(row => row.MixedIdFit);
CapacityRow[] hardIdBlocked = rows.Where(row => !row.MixedIdFit).ToArray();
CapacityRow[] directStaticFailures = rows
    .Where(row => row.DirectAppendIdFit && !row.StaticStructuralWriterVerified)
    .ToArray();
CapacityRow[] oneRowPackerFailures = rows
    .Where(row => row.OneRowGlobalPackingPassed == false)
    .ToArray();
int mixedStructuralAndOneRowPageLevels = rows.Count(row =>
    row.MixedStaticStructuralWriterVerified &&
    row.OneRowGlobalPackingPassed == true);

Assert(directAppendLevels == 29,
    $"Expected 29 direct fifty-row ID destinations, got {directAppendLevels}.");
Assert(mixedIdCandidateLevels == 29,
    $"Expected 29 destinations to reach fifty after excluding animation-source diagnostics from reclaimable rows, got {mixedIdCandidateLevels}.");
Assert(
    rows.All(row => row.EditableTexturedFaceCount >= RequestedPrivateRows),
    $"At least one level exposes fewer than {RequestedPrivateRows} source-backed textured faces: " +
    string.Join(
        ", ",
        rows.Where(row => row.EditableTexturedFaceCount < RequestedPrivateRows)
            .Select(row => $"{row.LevelName}={row.EditableTexturedFaceCount}")));
Assert(
    hardIdBlocked.Select(row => row.LevelKey).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(
            [
                "highcaves",
                "wizardpeak",
                "treetops",
                "loftycastle",
                "hauntedtowers",
                "gnorccove"
            ]),
    "The native seven-bit hard-blocked level set changed.");
Assert(directStaticFailures.Length == 0,
    $"Extended structural writer failed direct-ID destinations: {string.Join(", ", directStaticFailures.Select(row => row.LevelName))}");
Assert(
    oneRowPackerFailures.Select(row => row.LevelKey).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(["loftycastle", "jacques", "gnorccove"]),
    "The current exact one-row global-packer blocker set changed.");
Assert(
    mixedStructuralAndOneRowPageLevels == 28,
    $"Expected 28 destinations to pass both safe fifty-row structure and the current one-row page gate, got {mixedStructuralAndOneRowPageLevels}.");
Assert(
    FileLength(sourceImagePath) == sourceLength &&
    File.GetLastWriteTimeUtc(sourceImagePath) == sourceWriteTime &&
    string.Equals(Sha256File(sourceImagePath), sourceSha256, StringComparison.OrdinalIgnoreCase),
    "The read-only fifty-row capacity census changed its retail source.");

var report = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = true,
    Scope = "All 35 mapped retail levels, including five flights; static capacity only.",
    Source = new
    {
        Path = sourceImagePath,
        Sha256 = sourceSha256,
        Length = sourceLength,
        Preserved = true
    },
    RequestedPrivateRows,
    NativeFormat = new
    {
        TextureIdBits = 7,
        MaximumTextureRecords = 128,
        BytesPerTextureRecord =
            NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
        RequestedTableGrowthBytes =
            RequestedPrivateRows *
            NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
        NormalMaximumSectorGrowthBytes =
            NativeTerrainTextureRecordAppendBuilder.MaximumCheckedSectorGrowthBytes,
        StaticResearchMaximumSectorGrowthBytes =
            NativeTerrainTextureRecordAppendBuilder.MaximumStaticResearchFiftyRowSectorGrowthBytes
    },
    Summary = new
    {
        TotalLevels = rows.Count,
        LevelsWithAtLeastFiftyEditableTexturedFaces =
            rows.Count(row => row.EditableTexturedFaceCount >= RequestedPrivateRows),
        MinimumEditableTexturedFaceCount =
            rows.Min(row => row.EditableTexturedFaceCount),
        DirectAppendFiftyIdLevels = directAppendLevels,
        MixedUnusedRowCandidateLevels = mixedIdCandidateLevels - directAppendLevels,
        MixedUnusedRowStructuralProofLevels =
            rows.Count(row => row.MixedIdFit && row.MixedStaticStructuralWriterVerified),
        HardSevenBitBlockedLevels = hardIdBlocked.Length,
        StaticStructuralWriterPassedDirectIdLevels =
            rows.Count(row => row.DirectAppendIdFit && row.StaticStructuralWriterVerified),
        CurrentOneRowGlobalPackerPassedLevels =
            rows.Count(row => row.OneRowGlobalPackingPassed == true),
        CurrentOneRowGlobalPackerBlockedLevels = oneRowPackerFailures.Length,
        MixedFiftyStructureAndOneRowPageGateLevels =
            mixedStructuralAndOneRowPageLevels,
        DuckStationRuntimeProvenLevels = 0
    },
    Interpretation = new[]
    {
        "Twenty-nine levels can append fifty contiguous T-ids and now pass exact +0x2000/+0x2800 WAD/ISO/executable relocation planning under the explicit static-research policy.",
        "Every level exposes far more than fifty source-backed textured faces, and repeated face assignments can reuse one compatible donor/material row without consuming another record.",
        "Wizard Peak and Tree Tops have four face-unreferenced rows, but those rows feed native full-record animation destinations and are diagnostic dependencies rather than reclaimable private capacity. Their normal-editor append-only limits are therefore 46 and 47. The historical 4-native + 46-appended candidates remain useful diagnostics, not safe fifty-slot allocation proof.",
        "High Caves, Wizard Peak, Tree Tops, Lofty Castle, Haunted Towers, and Gnorc Cove cannot expose fifty simultaneously distinct native private records while preserving every used retail row and every animation-source dependency because the face field is seven bits.",
        "A successful structural row plan does not prove that fifty arbitrary distinct donor art payloads fit the fixed 0x80000 texture-page upload.",
        "The existing one-row global-packer audit still blocks Lofty Castle, Jacques, and Gnorc Cove.",
        "Twenty-eight levels pass both the safe fifty-row structural prerequisite and the current one-row page-packing gate. That intersection is still not a fifty-art-payload proof.",
        "No row in this report claims DuckStation or hardware runtime evidence."
    },
    Levels = rows
};
JsonSerializerOptions options = new() { WriteIndented = true };
await File.WriteAllTextAsync(
    jsonPath,
    JsonSerializer.Serialize(report, options) + Environment.NewLine);
await File.WriteAllTextAsync(
    markdownPath,
    BuildMarkdown(rows, sourceSha256));

Console.WriteLine();
Console.WriteLine("Fifty-private-texture capacity census passed.");
Console.WriteLine($"- Direct append ID + structural proof: {directAppendLevels}/35");
    Console.WriteLine($"- Safe mixed unused-row ID candidates: {mixedIdCandidateLevels}/35");
    Console.WriteLine($"- Safe mixed unused-row structural proofs: {rows.Count(row => row.MixedIdFit && row.MixedStaticStructuralWriterVerified)}/35");
Console.WriteLine($"- Mixed 50-row structure + current one-row page gate: {mixedStructuralAndOneRowPageLevels}/35");
Console.WriteLine($"- Levels with at least 50 editable textured faces: {rows.Count(row => row.EditableTexturedFaceCount >= RequestedPrivateRows)}/35 (minimum {rows.Min(row => row.EditableTexturedFaceCount):N0})");
Console.WriteLine($"- Native seven-bit hard blockers: {string.Join(", ", hardIdBlocked.Select(row => row.LevelName))}");
Console.WriteLine($"- JSON: {jsonPath}");
Console.WriteLine($"- Markdown: {markdownPath}");
Console.WriteLine("- DuckStation/runtime proof: intentionally not claimed.");

static string BuildMarkdown(
    IReadOnlyList<CapacityRow> rows,
    string sourceSha256)
{
    StringBuilder text = new();
    text.AppendLine("# Fifty private terrain-texture capacity census");
    text.AppendLine();
    text.AppendLine("**Static research only. No DuckStation or hardware proof is claimed.**");
    text.AppendLine();
    text.AppendLine($"- Retail source SHA-256: `{sourceSha256}`");
    text.AppendLine("- Native face texture ID width: **7 bits** (`T0..T127`).");
    text.AppendLine("- Fifty appended rows add **9,200 bytes** to a level texture table.");
    text.AppendLine($"- Direct fifty-row ID + structural growth proof: **{rows.Count(row => row.DirectAppendIdFit && row.StaticStructuralWriterVerified)}/35** levels.");
    text.AppendLine($"- ID-side capacity of fifty under the current allocator after excluding native animation-source dependencies: **{rows.Count(row => row.MixedIdFit)}/35** levels.");
    text.AppendLine($"- Current mixed allocation's required append count has structural growth proof: **{rows.Count(row => row.MixedIdFit && row.MixedStaticStructuralWriterVerified)}/35** levels.");
    text.AppendLine($"- Mixed fifty-row structure plus the current exact one-row page-packing gate overlap in **{rows.Count(row => row.MixedStaticStructuralWriterVerified && row.OneRowGlobalPackingPassed == true)}/35** levels; this is a prerequisite count, not a fifty-art-payload proof.");
    text.AppendLine($"- At least fifty source-backed textured faces are independently assignable in **{rows.Count(row => row.EditableTexturedFaceCount >= RequestedPrivateRows)}/35** levels; the smallest level still has **{rows.Min(row => row.EditableTexturedFaceCount):N0}** such faces.");
    text.AppendLine();
    text.AppendLine("| Level | Editable textured faces | Retail rows | Append IDs | Safe reclaimable rows | Direct 50 | Mixed 50 candidate | Mixed append rows | Mixed static growth | One-row page pack |");
    text.AppendLine("|---|---:|---:|---:|---:|:---:|:---:|---:|---:|:---:|");
    foreach (CapacityRow row in rows)
    {
        string mixedGrowth = row.MixedStaticSectorGrowthBytes.HasValue
            ? $"+0x{row.MixedStaticSectorGrowthBytes.Value:X}"
            : "blocked";
        string pack = row.OneRowGlobalPackingPassed switch
        {
            true => "PASS",
            false => "FAIL",
            null => "unknown"
        };
        text.AppendLine(
            $"| {row.LevelName} | {row.EditableTexturedFaceCount} | {row.RetailTextureCount} | {row.AppendOnlyIdHeadroom} | " +
            $"{row.UnreferencedPersistentTextureIds.Count} | {(row.DirectAppendIdFit ? "yes" : "no")} | " +
            $"{(row.MixedIdFit ? "yes" : "no")} | {row.AppendedRowsRequiredForMixedFifty} | {mixedGrowth} | {pack} |");
    }
    text.AppendLine();
    text.AppendLine("## Hard native-ID blockers");
    text.AppendLine();
    foreach (CapacityRow row in rows.Where(row => !row.MixedIdFit))
    {
        text.AppendLine(
            $"- **{row.LevelName}:** at most {row.OptimisticMixedPrivateCapacity} distinct private records " +
            "after counting only face-unreferenced, runtime-persistent rows that are not native animation sources.");
    }
    text.AppendLine();
    text.AppendLine("The structural result proves table growth, WAD/ISO relocation planning, executable LBA movement, source fingerprints, and native ID bounds. It does not prove live loading, VRAM behavior, LOD/culling, or that fifty arbitrary distinct art payloads fit simultaneously.");
    return text.ToString();
}

static Dictionary<int, int> LoadFaceTextureUseCounts(
    string workspaceRoot,
    string levelKey)
{
    string overlayPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        $"{LevelCatalog.NormalizeKey(levelKey)}-runtime-scene-editor-overlay.json");
    if (!File.Exists(overlayPath))
        throw new FileNotFoundException($"Missing cached terrain overlay for {levelKey}.", overlayPath);

    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(overlayPath));
    Dictionary<int, int> counts = [];
    if (!document.RootElement.TryGetProperty("candidates", out JsonElement candidates) ||
        candidates.ValueKind != JsonValueKind.Array)
    {
        return counts;
    }
    foreach (JsonElement candidate in candidates.EnumerateArray())
    {
        if (!candidate.TryGetProperty("polygons", out JsonElement polygons) ||
            polygons.ValueKind != JsonValueKind.Array)
        {
            continue;
        }
        foreach (JsonElement polygon in polygons.EnumerateArray())
        {
            if (!polygon.TryGetProperty("textureId", out JsonElement textureElement) ||
                !textureElement.TryGetInt32(out int textureId))
            {
                continue;
            }
            counts[textureId] = counts.TryGetValue(textureId, out int count)
                ? count + 1
                : 1;
        }
    }
    return counts;
}

static int LoadPrimaryCandidateTexturedFaceCount(
    string workspaceRoot,
    string levelKey)
{
    string overlayPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        $"{LevelCatalog.NormalizeKey(levelKey)}-runtime-scene-editor-overlay.json");
    if (!File.Exists(overlayPath))
        throw new FileNotFoundException($"Missing cached terrain overlay for {levelKey}.", overlayPath);

    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(overlayPath));
    if (!document.RootElement.TryGetProperty("candidates", out JsonElement candidates) ||
        candidates.ValueKind != JsonValueKind.Array ||
        candidates.GetArrayLength() == 0)
    {
        return 0;
    }
    JsonElement primary = candidates[0];
    if (!primary.TryGetProperty("polygons", out JsonElement polygons) ||
        polygons.ValueKind != JsonValueKind.Array)
    {
        return 0;
    }

    int count = 0;
    foreach (JsonElement polygon in polygons.EnumerateArray())
    {
        if (polygon.TryGetProperty("textureId", out JsonElement textureElement) &&
            textureElement.TryGetInt32(out int textureId) &&
            textureId >= 0)
        {
            count++;
        }
    }
    return count;
}

static Dictionary<string, OneRowPackingResult> LoadOneRowPackingResults(
    string path)
{
    if (!File.Exists(path))
        return new Dictionary<string, OneRowPackingResult>(StringComparer.OrdinalIgnoreCase);
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
    Dictionary<string, OneRowPackingResult> results =
        new(StringComparer.OrdinalIgnoreCase);
    foreach (JsonElement level in document.RootElement.GetProperty("Levels").EnumerateArray())
    {
        string key = LevelCatalog.NormalizeKey(
            level.GetProperty("LevelKey").GetString() ?? "");
        bool success = level.GetProperty("Success").GetBoolean();
        string failure = level.TryGetProperty("Failure", out JsonElement failureElement)
            ? failureElement.GetString() ?? ""
            : "";
        results[key] = new OneRowPackingResult(success, failure);
    }
    return results;
}

static bool IsFlight(LevelDefinition level) =>
    NativeSkyGeometrySafety.IsFlight(level.Key);

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Spyro the Dragon (USA).bin")) &&
            Directory.Exists(Path.Combine(directory.FullName, "src", "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long FileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record OneRowPackingResult(
    bool Success,
    string Failure);

internal sealed record CapacityRow(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    bool IsFlight,
    int EditableTexturedFaceCount,
    int RetailTextureCount,
    int AppendOnlyIdHeadroom,
    IReadOnlyList<int> UnreferencedPersistentTextureIds,
    int OptimisticMixedPrivateCapacity,
    bool DirectAppendIdFit,
    bool MixedIdFit,
    int AppendedRowsRequiredForMixedFifty,
    int? NormalSectorGrowthBytes,
    bool? NormalStructuralWriterVerified,
    int? StaticSectorGrowthBytes,
    int? AvailableIsoGrowthBytes,
    bool StaticSectorRelocationPlanVerified,
    bool StaticStructuralWriterVerified,
    int? MixedStaticSectorGrowthBytes,
    bool MixedStaticSectorRelocationPlanVerified,
    bool MixedStaticStructuralWriterVerified,
    bool? OneRowGlobalPackingPassed,
    string OneRowGlobalPackingFailure,
    string StaticFailure,
    bool DuckStationRuntimeProven);
