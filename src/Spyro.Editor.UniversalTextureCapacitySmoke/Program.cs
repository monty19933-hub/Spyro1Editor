using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int NativeTextureIdCapacity = 128;
const int WadLba = 37;
const int DonorTextureId = 17;

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal)));
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "universal-terrain-texture-capacity");
string workingImagePath = Path.Combine(outputRoot, "universal-capacity-working.bin");
string jsonPath = Path.Combine(outputRoot, "universal-terrain-texture-capacity-census.json");
string markdownPath = Path.Combine(outputRoot, "universal-terrain-texture-capacity-census.md");

Directory.CreateDirectory(outputRoot);
if (!File.Exists(sourceImagePath))
    throw new FileNotFoundException("Missing retail source BIN.", sourceImagePath);
if (!File.Exists(sourceCuePath))
    throw new FileNotFoundException("Missing retail source CUE.", sourceCuePath);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] levels = LevelRealmCatalog
    .OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    .ToArray();
Assert(levels.Length == 35, $"Expected all 35 mapped retail levels, got {levels.Length}.");
LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");

List<PreparedLevel> prepared = [];
foreach (LevelDefinition level in levels)
{
    IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(sourceImagePath, level);
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
    Dictionary<int, int> faceUseCounts = LoadFaceTextureUseCounts(workspaceRoot, level.Key);
    NativeTexturePageOwnershipReport ownership = NativeTexturePageOwnershipScanner.Scan(sourceImagePath, level);
    NativeTerrainTextureRecordAppendResearchRequest request = new(
        sourceImagePath,
        sourceCuePath,
        level,
        donorLevel,
        DonorTextureId);
    bool appendBuilt = NativeTerrainTextureRecordAppendResearch.TryBuild(
        request,
        out NativeTerrainTextureRecordAppendResearchPlan? appendPlan,
        out string appendFailure);

    Assert(runtime.Complete,
        $"{level.DisplayName}: runtime-control scan did not close: {string.Join(" ", runtime.SafetyBlockers)}");
    Assert(ownership.AllConsumerClosureComplete,
        $"{level.DisplayName}: all-consumer page ownership did not close: {string.Join(" ", ownership.SafetyBlockers)}");
    Assert(slots.Count == runtime.TextureCount && slots.Count == ownership.Terrain.TextureCount,
        $"{level.DisplayName}: texture record counts disagree across table/runtime/ownership scans.");

    prepared.Add(new PreparedLevel(
        level,
        slots.Count,
        faceUseCounts,
        runtime,
        ownership,
        appendBuilt ? appendPlan : null,
        appendFailure));
    Console.WriteLine(
        $"Prepared {level.DisplayName}: records={slots.Count}; freeIds={NativeTextureIdCapacity - slots.Count}; " +
        $"stable={Enumerable.Range(0, slots.Count).Count(runtime.IsRuntimePersistentTarget)}; " +
        $"controlled={runtime.ControlledTextureIds.Count}; append={(appendBuilt ? "yes" : "blocked")}");
}

File.Delete(workingImagePath);
File.Copy(sourceImagePath, workingImagePath, true);
LocalDiscLayout workingLayout = DetectDiscLayout(workingImagePath);
await using (FileStream working = File.Open(
                 workingImagePath,
                 FileMode.Open,
                 FileAccess.ReadWrite,
                 FileShare.Read))
{
    foreach (PreparedLevel item in prepared.Where(item => item.AppendPlan != null))
    {
        NativeTerrainTextureRecordAppendResearchPlan plan = item.AppendPlan!;
        byte[] before = ReadDiscFileBytes(
            working,
            workingLayout,
            WadLba,
            plan.LevelDataWadOffset,
            plan.LevelDataByteLength);
        Assert(before.SequenceEqual(plan.SourceLevelData),
            $"{item.Level.DisplayName}: shared census copy did not match the structural append preimage.");
        WriteDiscFileBytes(
            working,
            workingLayout,
            WadLba,
            plan.LevelDataWadOffset,
            plan.OutputLevelData);
        byte[] after = ReadDiscFileBytes(
            working,
            workingLayout,
            WadLba,
            plan.LevelDataWadOffset,
            plan.LevelDataByteLength);
        Assert(after.SequenceEqual(plan.OutputLevelData),
            $"{item.Level.DisplayName}: appended table final-image readback failed.");
    }
    working.Flush(flushToDisk: true);
}

List<CapacityRow> rows = [];
try
{
    foreach (PreparedLevel item in prepared)
    {
        LevelDefinition level = item.Level;
        int[] referenced = item.FaceUseCounts.Keys
            .Where(textureId => textureId >= 0 && textureId < item.NativeRecordCount)
            .Distinct()
            .Order()
            .ToArray();
        int[] referencedStable = referenced
            .Where(item.Runtime.IsRuntimePersistentTarget)
            .ToArray();
        int[] unreferencedStable = Enumerable.Range(0, item.NativeRecordCount)
            .Where(textureId => !item.FaceUseCounts.ContainsKey(textureId))
            .Where(item.Runtime.IsRuntimePersistentTarget)
            .ToArray();
        int[] referencedControlled = referenced
            .Where(textureId => !item.Runtime.IsRuntimePersistentTarget(textureId))
            .ToArray();
        int[] unreferencedControlled = item.Runtime.ControlledTextureIds
            .Where(textureId => !item.FaceUseCounts.ContainsKey(textureId))
            .ToArray();
        List<string> blockers = [];
        bool appendedStructureReady = item.AppendPlan != null;
        bool? pageFit = appendedStructureReady ? false : null;
        bool existingMovableUnionRepackFits = false;
        int appendedTextureId = item.NativeRecordCount;
        int movableRecordCount = 0;
        int fixedRecordCount = item.Runtime.ControlledTextureIds.Count;
        int movableReleasedBytes = 0;
        int fixedProtectedBytes = item.Ownership.ProtectedByteCount;
        int analysisProvenPrivateZeroBytes = item.Ownership.ProvablyPrivateByteCount;
        int requiredRepackBytes = 0;
        int allocatedPixelBytes = 0;
        int allocatedPaletteBytes = 0;
        int remainingPageBytes = 0;
        bool logicalReadbackVerified = false;
        bool protectedStorageVerified = false;
        string pageFailure = "";

        if (!appendedStructureReady)
        {
            blockers.Add(item.AppendFailure);
        }

        string analysisImagePath = appendedStructureReady ? workingImagePath : sourceImagePath;
        IReadOnlyList<TerrainTextureSlot> analysisSlots =
            TerrainPatchExporter.InspectTextureSlots(analysisImagePath, level);
        NativeTerrainTextureRuntimeControlAudit analysisRuntime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(analysisImagePath, level);
        int expectedAnalysisCount = item.NativeRecordCount + (appendedStructureReady ? 1 : 0);
        if (analysisSlots.Count != expectedAnalysisCount ||
            !analysisRuntime.Complete ||
            (appendedStructureReady && !analysisRuntime.IsRuntimePersistentTarget(appendedTextureId)))
        {
            blockers.Add(
                $"Analysis table/runtime readback rejected the expected {expectedAnalysisCount}-record state: " +
                string.Join(" ", analysisRuntime.SafetyBlockers));
        }
        else
        {
            int[] movableTextureIds = Enumerable.Range(0, analysisSlots.Count)
                .Where(analysisRuntime.IsRuntimePersistentTarget)
                .ToArray();
            movableRecordCount = movableTextureIds.Length;
            fixedRecordCount = analysisSlots.Count - movableTextureIds.Length;
            bool proofBuilt = NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                analysisImagePath,
                level,
                movableTextureIds,
                out NativeTexturePageRelocationOwnershipProofResult? proof,
                out string proofFailure);
            if (!proofBuilt || proof == null)
            {
                pageFailure = proofFailure;
                blockers.Add($"Complete movable-union ownership proof failed: {proofFailure}");
            }
            else
            {
                movableReleasedBytes = proof.ReleasedTargetByteCount;
                fixedProtectedBytes = proof.Proof.OwnedRanges.Sum(range => range.Length);
                analysisProvenPrivateZeroBytes = proof.LevelOwnership.ProvablyPrivateByteCount;
                NativeTerrainTextureRelocationImport[] imports = movableTextureIds
                    .Select(textureId => appendedStructureReady && textureId == appendedTextureId
                        ? new NativeTerrainTextureRelocationImport(
                            appendedTextureId,
                            donorLevel.SourceWadEntry,
                            DonorTextureId,
                            "both",
                            PreserveTargetDescriptorMaterial: false)
                        : new NativeTerrainTextureRelocationImport(
                            textureId,
                            level.SourceWadEntry,
                            textureId,
                            "both",
                            PreserveTargetDescriptorMaterial: true))
                    .ToArray();
                NativeTerrainTextureRelocationAudit audit =
                    NativeTerrainTextureRelocationAllocator.Audit(
                        analysisImagePath,
                        level,
                        imports,
                        proof.Proof);
                requiredRepackBytes = audit.RequiredAllocationByteCount;
                bool allocationBuilt = NativeTerrainTextureRelocationAllocator.TryBuild(
                    analysisImagePath,
                    level,
                    imports,
                    proof.Proof,
                    out NativeTerrainTextureRelocationPlan? repackPlan,
                    out pageFailure);
                existingMovableUnionRepackFits = allocationBuilt;
                pageFit = appendedStructureReady ? allocationBuilt : null;
                if (allocationBuilt && repackPlan != null)
                {
                    allocatedPixelBytes = repackPlan.AllocatedPixelByteCount;
                    allocatedPaletteBytes = repackPlan.AllocatedPaletteByteCount;
                    remainingPageBytes = NativeTexturePageOwnershipScanner.AddressableTexturePageBytes -
                        fixedProtectedBytes - allocatedPixelBytes - allocatedPaletteBytes;
                    logicalReadbackVerified = repackPlan.LogicalReadbackVerified &&
                        repackPlan.ExactDonorIndexedPixelsVerified &&
                        repackPlan.ExactDonorPalettesVerified;
                    protectedStorageVerified = repackPlan.ProtectedStorageVerified;
                }
                else
                {
                    blockers.Add($"Complete movable-union page repack failed: {pageFailure}");
                }
            }
        }

        if (NativeTextureIdCapacity - item.NativeRecordCount <= 0)
            blockers.Add("No seven-bit terrain texture id remains for a private record.");
        if (!item.Runtime.Complete)
            blockers.Add("Runtime texture-control ownership is incomplete.");
        if (!item.Ownership.AllConsumerClosureComplete)
            blockers.Add("External texture-page consumer ownership is incomplete.");
        blockers.Add("A structurally appended record and page repack remain research-only until final-BIN and DuckStation runtime proof are recorded for this destination.");

        CapacityRow row = new(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            Realm: LevelRealmCatalog.TryGetPosition(level, out LevelRealmPosition position)
                ? position.Realm.DisplayName
                : "Unknown",
            LevelId: level.LevelId,
            WadEntry: level.SourceWadEntry,
            IsFlight: level.DisplayName.Contains("Flight", StringComparison.OrdinalIgnoreCase),
            NativeRecordCount: item.NativeRecordCount,
            FreeSevenBitTextureIds: Math.Max(0, NativeTextureIdCapacity - item.NativeRecordCount),
            FaceReferencedRecordCount: referenced.Length,
            FaceUnreferencedRecordCount: item.NativeRecordCount - referenced.Length,
            FaceReferencedStableRecordCount: referencedStable.Length,
            FaceUnreferencedStableRecordCount: unreferencedStable.Length,
            FaceReferencedControlledRecordCount: referencedControlled.Length,
            FaceUnreferencedControlledRecordCount: unreferencedControlled.Length,
            RuntimeControlledTextureIds: item.Runtime.ControlledTextureIds,
            AppendedTextureId: appendedStructureReady ? appendedTextureId : null,
            RecordTableAppendReady: appendedStructureReady,
            SourceZeroTailBytes: item.AppendPlan?.SourceZeroTailByteCount ?? ExtractZeroTailBytes(item.AppendFailure),
            OutputZeroTailBytes: item.AppendPlan?.OutputZeroTailByteCount ?? 0,
            MovableRecordCount: movableRecordCount,
            FixedRuntimeControlledRecordCount: fixedRecordCount,
            MovableTerrainExclusiveBytesReleased: movableReleasedBytes,
            FixedProtectedPageBytes: fixedProtectedBytes,
            AnalysisProvenPrivateZeroBytes: analysisProvenPrivateZeroBytes,
            RequiredRepackBytes: requiredRepackBytes,
            ByteCapacitySlackBeforeGeometry: appendedStructureReady
                ? NativeTexturePageOwnershipScanner.AddressableTexturePageBytes - fixedProtectedBytes - requiredRepackBytes
                : null,
            ByteCapacityNecessaryConditionMet: appendedStructureReady
                ? NativeTexturePageOwnershipScanner.AddressableTexturePageBytes - fixedProtectedBytes - requiredRepackBytes >= 0
                : null,
            AllocatedPixelBytes: allocatedPixelBytes,
            AllocatedPaletteBytes: allocatedPaletteBytes,
            RemainingPageBytesAfterPlan: remainingPageBytes,
            ConcreteDonorLevel: donorLevel.DisplayName,
            ConcreteDonorTextureId: DonorTextureId,
            ExistingMovableUnionRepackFits: existingMovableUnionRepackFits,
            CompleteMovableUnionPlusDonorFits: pageFit,
            LogicalReadbackVerified: logicalReadbackVerified,
            ProtectedStorageVerified: protectedStorageVerified,
            PageFailure: pageFailure,
            HardBlockers: blockers.Distinct(StringComparer.Ordinal).ToArray());
        rows.Add(row);
        Console.WriteLine(
            $"Census {level.DisplayName}: append={(row.RecordTableAppendReady ? "yes" : "no")}; " +
            $"movable/fixed={row.MovableRecordCount}/{row.FixedRuntimeControlledRecordCount}; " +
            $"pageFit={FormatFit(row.CompleteMovableUnionPlusDonorFits)}; " +
            $"required={row.RequiredRepackBytes:N0}; protected={row.FixedProtectedPageBytes:N0}");
    }
}
finally
{
    File.Delete(workingImagePath);
}

Assert(rows.Count == 35, $"Expected 35 census rows, got {rows.Count}.");
Assert(rows.All(row => row.NativeRecordCount is > 0 and <= NativeTextureIdCapacity),
    "A retail texture count is outside the seven-bit face field.");
Assert(rows.All(row => row.FreeSevenBitTextureIds == NativeTextureIdCapacity - row.NativeRecordCount),
    "A seven-bit free-id count is inconsistent.");
Assert(rows.All(row => row.FaceReferencedStableRecordCount + row.FaceUnreferencedStableRecordCount +
                       row.FaceReferencedControlledRecordCount + row.FaceUnreferencedControlledRecordCount ==
                       row.NativeRecordCount),
    "A stable/controlled and referenced/unreferenced census partition is incomplete.");
Assert(Sha256File(sourceImagePath).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) &&
       new FileInfo(sourceImagePath).Length == sourceLength,
    "The census modified the retail source BIN.");

CapacitySummary summary = new(
    TotalLevels: rows.Count,
    RecordTableAppendReadyLevels: rows.Count(row => row.RecordTableAppendReady),
    PageByteCapacityCandidateLevels: rows.Count(row => row.ByteCapacityNecessaryConditionMet == true),
    PageByteCapacityBlockedLevels: rows.Count(row => row.ByteCapacityNecessaryConditionMet == false),
    GeometryPackingUnprovenLevels: rows.Count(row =>
        row.ByteCapacityNecessaryConditionMet == true &&
        row.CompleteMovableUnionPlusDonorFits == false),
    CompleteMovableUnionPlusDonorFitLevels: rows.Count(row => row.CompleteMovableUnionPlusDonorFits == true),
    LevelsWithRuntimeControlledRecords: rows.Count(row => row.FixedRuntimeControlledRecordCount > 0),
    MinimumFreeSevenBitTextureIds: rows.Min(row => row.FreeSevenBitTextureIds),
    MaximumNativeRecordCount: rows.Max(row => row.NativeRecordCount),
    MinimumSourceZeroTailBytes: rows.Min(row => row.SourceZeroTailBytes),
    MaximumFixedProtectedPageBytes: rows.Max(row => row.FixedProtectedPageBytes));

JsonSerializerOptions jsonOptions = new()
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = true,
    Scope = "All 35 retail levels, including five flights.",
    Source = new
    {
        Path = sourceImagePath,
        Length = sourceLength,
        Sha256 = sourceSha256,
        Unchanged = true
    },
    CapacityModel = new
    {
        NativeTextureIdCapacity,
        RecordGrowthBytes = NativeTerrainTextureRecordAppendResearch.RecordGrowthBytes,
        AddressableTexturePageBytes = NativeTexturePageOwnershipScanner.AddressableTexturePageBytes,
        ConcreteNewDonor = $"{donorLevel.DisplayName} T{DonorTextureId}",
        Movable = "Runtime-persistent native terrain records plus the appended record.",
        Fixed = "Runtime-controlled terrain destinations, decoded external consumers, and unclassified source-nonzero bytes.",
        Fit = "The current exact allocator emitted a complete all-movable-record plus donor plan with logical pixel/palette and protected-storage readback.",
        ByteCapacityNecessaryCondition = "fixed protected bytes + alias-preserving repack bytes <= 0x80000; passing is necessary but does not prove descriptor-encodable geometry.",
        ImportantBoundary = "A byte total alone is not a fit. Native descriptor alignment, page halves, LQ palette rows, HQ CLUTs, aliases, and fixed ownership all apply."
    },
    Summary = summary,
    ReleaseSafeUiSemantics = new[]
    {
        "Same-level texture-id reuse and reuse of an already-loaded cross-level donor require no new page storage and remain available even at capacity.",
        "Only Selected Section may allocate one private record only after a source-fingerprint-bound structural append and page-repack plan both pass.",
        "Display independent counters for remaining seven-bit record IDs and proven page capacity; neither counter implies the other.",
        "Runtime-controlled texture IDs stay fixed and may not be silently consumed as private targets.",
        "Each additional distinct cross-level donor is one more atomic capacity request. Repeated use of the same loaded donor reuses its private record.",
        "When capacity is exhausted, keep Shared Record Replacement available with an exact affected-section count, but never call that selected-only painting.",
        "Undo/remove must release the donor record transactionally and rerun the same source-bound pack/readback proof.",
        "Research-positive profiles remain test-CUE-only until DuckStation evidence exists; shipping Create BIN must reject unproven destination fingerprints."
    },
    Levels = rows
}, jsonOptions));
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(rows, summary));

Console.WriteLine();
Console.WriteLine("Universal terrain texture capacity census passed its static invariants.");
Console.WriteLine(
    $"Table append: {summary.RecordTableAppendReadyLevels}/{summary.TotalLevels}; " +
    $"current exact page fit for {donorLevel.DisplayName} T{DonorTextureId}: " +
    $"{summary.CompleteMovableUnionPlusDonorFitLevels}/{summary.TotalLevels}.");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");

static string BuildMarkdown(IReadOnlyList<CapacityRow> rows, CapacitySummary summary)
{
    StringBuilder text = new();
    text.AppendLine("# Universal selected-face terrain texture capacity census");
    text.AppendLine();
    text.AppendLine("## Result");
    text.AppendLine();
    text.AppendLine($"- Native record-table append is structurally ready in **{summary.RecordTableAppendReadyLevels}/{summary.TotalLevels}** retail levels.");
    text.AppendLine($"- Of those, **{summary.PageByteCapacityCandidateLevels}** pass the raw protected+required byte necessary condition and **{summary.PageByteCapacityBlockedLevels}** exceed it under the current alias-preserving model.");
    text.AppendLine($"- The current exact movable-union page plan fits Gnasty's World T{DonorTextureId} in **{summary.CompleteMovableUnionPlusDonorFitLevels}/{summary.TotalLevels}** levels.");
    text.AppendLine($"- **{summary.GeometryPackingUnprovenLevels}** structurally-ready levels have enough aggregate bytes but still lack a descriptor-encodable geometry plan; these are packing-research gaps, not proven physical impossibilities.");
    text.AppendLine($"- The tightest seven-bit table still has **{summary.MinimumFreeSevenBitTextureIds}** free id(s); the largest retail table has **{summary.MaximumNativeRecordCount}** records.");
    text.AppendLine("- A record-table slot and PS1 texture-page storage are independent capacities. A level can have dozens of free 7-bit IDs while fixed page ownership still prevents one descriptor-encodable donor allocation.");
    text.AppendLine("- Same-level texture reuse, or reusing a cross-level donor already loaded into the destination, consumes no new record/page storage and should remain available at capacity.");
    text.AppendLine();
    text.AppendLine("This is a static source-bound census, not runtime promotion evidence. Every positive destination still requires final-BIN readback and DuckStation proof before normal Create BIN can expose universal selected-only painting.");
    text.AppendLine();
    text.AppendLine("## All levels");
    text.AppendLine();
    text.AppendLine("| Realm | Level | Flight | Records | Free IDs | Stable ref / unused | Controlled | Append | Movable / fixed | Fixed page bytes | Required repack | Byte slack | Donor fit | Blocker |");
    text.AppendLine("|---|---|---|---:|---:|---:|---:|---|---:|---:|---:|---:|---|---|");
    foreach (CapacityRow row in rows)
    {
        string blocker = row.CompleteMovableUnionPlusDonorFits switch
        {
            true => "runtime proof pending",
            false => ShortFailure(row.PageFailure.Length > 0
                ? row.PageFailure
                : row.HardBlockers.FirstOrDefault() ?? "unknown"),
            null => ShortFailure(row.HardBlockers.FirstOrDefault() ?? "record-table structure unavailable")
        };
        text.AppendLine(
            $"| {Escape(row.Realm)} | {Escape(row.LevelName)} | {(row.IsFlight ? "yes" : "no")} | " +
            $"{row.NativeRecordCount} | {row.FreeSevenBitTextureIds} | " +
            $"{row.FaceReferencedStableRecordCount} / {row.FaceUnreferencedStableRecordCount} | " +
            $"{row.FixedRuntimeControlledRecordCount} | {(row.RecordTableAppendReady ? "yes" : "blocked")} | " +
            $"{row.MovableRecordCount} / {row.FixedRuntimeControlledRecordCount} | " +
            $"{row.FixedProtectedPageBytes:N0} | {row.RequiredRepackBytes:N0} | " +
            $"{(row.ByteCapacitySlackBeforeGeometry.HasValue ? row.ByteCapacitySlackBeforeGeometry.Value.ToString("N0") : "n/a")} | " +
            $"{FormatFit(row.CompleteMovableUnionPlusDonorFits)} | {Escape(blocker)} |");
    }
    text.AppendLine();
    text.AppendLine("## Release-safe UI semantics");
    text.AppendLine();
    text.AppendLine("1. Show `record IDs remaining` and `page capacity proven` separately.");
    text.AppendLine("2. Let a selected face reuse any same-level or already-loaded donor record without allocating storage.");
    text.AppendLine("3. Allocate one private record per distinct new donor only after an atomic structural append plus global page plan passes.");
    text.AppendLine("4. Keep runtime-controlled destination records fixed; do not silently consume them.");
    text.AppendLine("5. At capacity, offer truthful shared-record replacement with the exact affected-section count, not a disabled mystery state.");
    text.AppendLine("6. Undo/removal must release and repack transactionally, preserving unrelated terrain and staged donors.");
    text.AppendLine("7. Keep static-positive profiles test-only until final-BIN and DuckStation evidence is recorded for the destination/source fingerprint.");
    return text.ToString();
}

static string ShortFailure(string value)
{
    const int maximumLength = 140;
    string collapsed = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    return collapsed.Length <= maximumLength ? collapsed : collapsed[..(maximumLength - 1)] + "…";
}

static string FormatFit(bool? value) => value switch
{
    true => "yes",
    false => "blocked",
    null => "not tested"
};

static int ExtractZeroTailBytes(string failure)
{
    const string marker = "has only ";
    int start = failure.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    if (start < 0)
        return 0;
    start += marker.Length;
    int end = failure.IndexOf(" verified zero tail bytes", start, StringComparison.OrdinalIgnoreCase);
    return end > start && int.TryParse(failure[start..end], out int value) ? value : 0;
}

static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current); directory != null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Spyro the Dragon (USA).bin")) &&
            Directory.Exists(Path.Combine(directory.FullName, "src", "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static Dictionary<int, int> LoadFaceTextureUseCounts(string workspaceRoot, string levelKey)
{
    string overlayPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        $"{levelKey}-runtime-scene-editor-overlay.json");
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
            counts[textureId] = counts.TryGetValue(textureId, out int count) ? count + 1 : 1;
        }
    }
    return counts;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static LocalDiscLayout DetectDiscLayout(string path)
{
    using FileStream stream = File.OpenRead(path);
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (stream.Length < offset + 2048)
            continue;
        byte[] buffer = new byte[2048];
        stream.Position = offset;
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            continue;
        if (buffer[0] == 1 && Encoding.ASCII.GetString(buffer, 1, 5) == "CD001")
            return new LocalDiscLayout(sectorSize, userOffset);
    }
    throw new InvalidOperationException("Could not find an ISO9660 primary volume descriptor.");
}

static byte[] ReadDiscFileBytes(
    FileStream stream,
    LocalDiscLayout layout,
    int fileLba,
    long fileOffset,
    int length)
{
    byte[] result = new byte[length];
    int remaining = length;
    int written = 0;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int sector = fileLba + (int)(absolute / 2048);
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        stream.Position = ((long)sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
        if (stream.Read(result, written, toRead) != toRead)
            throw new EndOfStreamException("Could not read the requested disc bytes.");
        written += toRead;
        remaining -= toRead;
        absolute += toRead;
    }
    return result;
}

static void WriteDiscFileBytes(
    FileStream stream,
    LocalDiscLayout layout,
    int fileLba,
    long fileOffset,
    byte[] bytes)
{
    int remaining = bytes.Length;
    int written = 0;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int sector = fileLba + (int)(absolute / 2048);
        int toWrite = Math.Min(2048 - sectorOffset, remaining);
        stream.Position = ((long)sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
        stream.Write(bytes, written, toWrite);
        written += toWrite;
        remaining -= toWrite;
        absolute += toWrite;
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record PreparedLevel(
    LevelDefinition Level,
    int NativeRecordCount,
    IReadOnlyDictionary<int, int> FaceUseCounts,
    NativeTerrainTextureRuntimeControlAudit Runtime,
    NativeTexturePageOwnershipReport Ownership,
    NativeTerrainTextureRecordAppendResearchPlan? AppendPlan,
    string AppendFailure);

internal sealed record LocalDiscLayout(int SectorSize, int UserOffset);

internal sealed record CapacitySummary(
    int TotalLevels,
    int RecordTableAppendReadyLevels,
    int PageByteCapacityCandidateLevels,
    int PageByteCapacityBlockedLevels,
    int GeometryPackingUnprovenLevels,
    int CompleteMovableUnionPlusDonorFitLevels,
    int LevelsWithRuntimeControlledRecords,
    int MinimumFreeSevenBitTextureIds,
    int MaximumNativeRecordCount,
    int MinimumSourceZeroTailBytes,
    int MaximumFixedProtectedPageBytes);

internal sealed record CapacityRow(
    string LevelKey,
    string LevelName,
    string Realm,
    int LevelId,
    int WadEntry,
    bool IsFlight,
    int NativeRecordCount,
    int FreeSevenBitTextureIds,
    int FaceReferencedRecordCount,
    int FaceUnreferencedRecordCount,
    int FaceReferencedStableRecordCount,
    int FaceUnreferencedStableRecordCount,
    int FaceReferencedControlledRecordCount,
    int FaceUnreferencedControlledRecordCount,
    IReadOnlyList<int> RuntimeControlledTextureIds,
    int? AppendedTextureId,
    bool RecordTableAppendReady,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int MovableRecordCount,
    int FixedRuntimeControlledRecordCount,
    int MovableTerrainExclusiveBytesReleased,
    int FixedProtectedPageBytes,
    int AnalysisProvenPrivateZeroBytes,
    int RequiredRepackBytes,
    int? ByteCapacitySlackBeforeGeometry,
    bool? ByteCapacityNecessaryConditionMet,
    int AllocatedPixelBytes,
    int AllocatedPaletteBytes,
    int RemainingPageBytesAfterPlan,
    string ConcreteDonorLevel,
    int ConcreteDonorTextureId,
    bool ExistingMovableUnionRepackFits,
    bool? CompleteMovableUnionPlusDonorFits,
    bool LogicalReadbackVerified,
    bool ProtectedStorageVerified,
    string PageFailure,
    IReadOnlyList<string> HardBlockers);
