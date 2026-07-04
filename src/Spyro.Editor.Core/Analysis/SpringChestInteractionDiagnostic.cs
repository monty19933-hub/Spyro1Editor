using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class SpringChestInteractionDiagnostic
{
    private const int WadLba = 37;
    private const int RecordStride = 0x58;
    private const int RuntimeMobyPointerOffset = 0x75828;
    private const int DynamicMobyPointerOffset = 0x7573C;

    private static readonly (string Name, string Before, string After, string LevelKey, int[] TrueIndices)[] CapturePairs =
    [
        ("Peace Keepers native T70/T71 flame or charge", "peacekeepers-t70t71-before.bin", "peacekeepers-t70t71-after-hit.bin", "peacekeepers", [67, 68, 69, 70, 71, 72, 73, 74, 92, 93, 94]),
        ("Stone Hill imported Spring Chest flame or charge", "stonehill-imported-t195t196-before.bin", "stonehill-imported-t195t196-after-hit.bin", "stonehill", [194, 195, 196, 197, 198])
    ];

    public static SpringChestInteractionDiagnosticReport Build(EditorWorkspace workspace, LevelCatalog catalog, string sourceImagePath)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);

        List<SpringChestNativeCluster> nativeClusters = new();
        foreach (string levelKey in new[] { "peacekeepers", "townsquare", "drycanyon" })
        {
            LevelDefinition? level = catalog.FindByKey(levelKey);
            if (level?.HasSourceTable != true)
                continue;

            IReadOnlyList<SpringChestSourceRecord> records = ReadSourceRecords(stream, layout, level);
            nativeClusters.AddRange(BuildNativeClusters(level, records));
        }

        string objectsDir = Path.Combine(workspace.RootPath, "_local", "objects");
        string latestResultPath = FindLatestStoneHillSpringCandidateResultPath(objectsDir);
        SpringChestCandidateFinding latestFinding = ReadLatestCandidateFinding(latestResultPath);

        string captureDir = Path.Combine(workspace.RootPath, "_local", "smoke", "spring-chest-interaction-captures");
        List<SpringChestRamPairDiagnostic> ramPairs = [];
        foreach ((string name, string before, string after, string levelKey, int[] trueIndices) in CapturePairs)
        {
            ramPairs.Add(BuildRamPairDiagnostic(captureDir, name, before, after, levelKey, trueIndices));
            for (int eventIndex = 1; eventIndex <= 4; eventIndex++)
            {
                string eventName = EventCaptureName(after, eventIndex);
                if (File.Exists(Path.Combine(captureDir, eventName)))
                    ramPairs.Add(BuildRamPairDiagnostic(captureDir, $"{name} event {eventIndex}", before, eventName, levelKey, trueIndices));
            }
        }
        SpringChestCaptureFreshness captureFreshness = BuildCaptureFreshness(captureDir, latestResultPath);
        SpringChestRamNativeVsImportDiagnostic nativeVsImport = BuildNativeVsImportDiagnostic(captureDir);
        IReadOnlyList<SpringChestNeighborPattern> neighborPatterns = BuildNeighborPatterns(nativeClusters);

        string conclusion = latestFinding.BootedLevel && latestFinding.ObjectAppeared && latestFinding.BehaviorCorrect == false
            ? "The current Stone Hill import is a visible actor-package success but an interaction failure. The strongest next proof is the pre-hit RAM delta between a real Peace Keepers Spring Chest and the imported Stone Hill Spring Chest."
            : "The current Stone Hill import still needs a clean in-game result before interaction diagnostics can narrow the missing dependency.";

        return new SpringChestInteractionDiagnosticReport(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: sourceImagePath,
            LatestCandidate: latestFinding,
            NativeClusters: nativeClusters,
            NeighborPatterns: neighborPatterns,
            RamPairDiagnostics: ramPairs,
            CaptureFreshness: captureFreshness,
            NativeVsImport: nativeVsImport,
            CaptureDirectory: captureDir,
            Conclusion: conclusion,
            NextCapturePlan:
            [
                "Use Launch Spring Chest Auto Diagnostic.command to watch DuckStation and save these RAM files automatically.",
                "In Peace Keepers, stand at the native T70/T71 spring chest pair, capture RAM before hitting it, then capture RAM immediately after a flame or charge attempt that makes the chest react.",
                "In Stone Hill using the latest imported T195/T196/T197 candidate, capture RAM before flame/charge, then flame or charge the imported chest twice so the helper can save first-hit and second-hit event snapshots.",
                "The auto diagnostic now watches the active pickup list plus the hidden reward row T197, because Sparx appears to be chasing that row while it is parked."
            ]);
    }

    public static async Task WriteAsync(SpringChestInteractionDiagnosticReport report, string jsonPath, string markdownPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(report.CaptureDirectory);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), cancellationToken);
    }

    private static IReadOnlyList<SpringChestSourceRecord> ReadSourceRecords(FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        List<SpringChestSourceRecord> records = new(level.SourceRecordCount);
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            records.Add(ReadRecord(level, trueIndex, record));
        }

        return records;
    }

    private static SpringChestSourceRecord ReadRecord(LevelDefinition level, int trueIndex, byte[] record)
    {
        int actorId = record[0x36] | (record[0x37] << 8);
        int type = record[0x50];
        int flag4A = record[0x52];
        int flag4B = record[0x53];
        string family = actorId switch
        {
            0x0149 => "spring-chest",
            0x00C2 or 0x00C3 => "normal-chest",
            0x00AE => "key-chest",
            0x00AD => "key",
            _ when type == 0x18 && record[0x52] == 0x40 => "standalone-gem",
            _ when type == 0x00 && flag4A == 0xFF && flag4B is >= 0x53 and <= 0x57 => "contained-gem-or-helper",
            _ => "other"
        };

        return new SpringChestSourceRecord(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            TrueIndex: trueIndex,
            SpecialOffsetHex: HexOffset(BitConverter.ToUInt32(record, 0)),
            X: BitConverter.ToInt32(record, 0x0C) / 16f,
            Y: BitConverter.ToInt32(record, 0x10) / 16f,
            Z: BitConverter.ToInt32(record, 0x14) / 16f,
            TypeHex: HexByte(type),
            StateHex: HexByte(record[0x51]),
            ActorIdHex: HexWord(actorId),
            SourceByte36Hex: HexByte(record[0x36]),
            SourceByte37Hex: HexByte(record[0x37]),
            SourceByte4FHex: HexByte(record[0x4F]),
            Flag4AHex: HexByte(flag4A),
            Flag4BHex: HexByte(flag4B),
            Family: family);
    }

    private static IEnumerable<SpringChestNativeCluster> BuildNativeClusters(LevelDefinition level, IReadOnlyList<SpringChestSourceRecord> records)
    {
        foreach (SpringChestSourceRecord spring in records.Where(record => record.Family == "spring-chest"))
        {
            IReadOnlyList<SpringChestSourceRecord> nearby = records
                .Where(record => record.TrueIndex != spring.TrueIndex)
                .Select(record => (Record: record, Distance: Distance2d(spring, record), Dz: Math.Abs(spring.Z - record.Z)))
                .Where(item => item.Distance <= 650 && item.Dz <= 260)
                .OrderBy(item => item.Distance)
                .ThenBy(item => Math.Abs(item.Record.TrueIndex - spring.TrueIndex))
                .Take(10)
                .Select(item => item.Record)
                .ToArray();

            IReadOnlyList<SpringChestSourceRecord> indexNeighbors = records
                .Where(record => record.TrueIndex >= spring.TrueIndex - 3 && record.TrueIndex <= spring.TrueIndex + 3 && record.TrueIndex != spring.TrueIndex)
                .OrderBy(record => record.TrueIndex)
                .ToArray();

            yield return new SpringChestNativeCluster(
                LevelKey: level.Key,
                LevelName: level.DisplayName,
                SpringChest: spring,
                NearbyRecords: nearby,
                IndexNeighbors: indexNeighbors,
                Read: BuildClusterRead(spring, nearby, indexNeighbors));
        }
    }

    private static string BuildClusterRead(SpringChestSourceRecord spring, IReadOnlyList<SpringChestSourceRecord> nearby, IReadOnlyList<SpringChestSourceRecord> indexNeighbors)
    {
        bool hasNearbyGem = nearby.Any(record => record.Family.Contains("gem", StringComparison.OrdinalIgnoreCase));
        bool hasNearbyChest = nearby.Any(record => record.Family.Contains("chest", StringComparison.OrdinalIgnoreCase) && record.Family != "spring-chest");
        bool hasAdjacentSpring = indexNeighbors.Any(record => record.Family == "spring-chest");
        List<string> parts = [];
        if (hasAdjacentSpring)
            parts.Add("adjacent spring pair");
        if (hasNearbyGem)
            parts.Add("nearby gem rows");
        if (hasNearbyChest)
            parts.Add("nearby non-spring chest rows");
        return parts.Count == 0
            ? "isolated visible spring row in source table"
            : string.Join(", ", parts);
    }

    private static IReadOnlyList<SpringChestNeighborPattern> BuildNeighborPatterns(IReadOnlyList<SpringChestNativeCluster> clusters)
    {
        Dictionary<(string ActorIdHex, string Family), SpringChestNeighborPatternBuilder> builders = [];
        foreach (SpringChestNativeCluster cluster in clusters)
        {
            foreach (SpringChestSourceRecord record in cluster.NearbyRecords)
            {
                if (record.TrueIndex == cluster.SpringChest.TrueIndex)
                    continue;

                (string ActorIdHex, string Family) key = (record.ActorIdHex, record.Family);
                if (!builders.TryGetValue(key, out SpringChestNeighborPatternBuilder? builder))
                {
                    builder = new SpringChestNeighborPatternBuilder(record.ActorIdHex, record.Family);
                    builders.Add(key, builder);
                }

                builder.Add(cluster, record);
            }
        }

        return builders.Values
            .Select(builder => builder.Build())
            .OrderByDescending(pattern => pattern.SpringChestMentions)
            .ThenBy(pattern => pattern.ActorIdHex, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SpringChestCandidateFinding ReadLatestCandidateFinding(string resultPath)
    {
        if (!File.Exists(resultPath))
            return new SpringChestCandidateFinding(resultPath, "missing", false, false, null, "");

        using FileStream stream = File.OpenRead(resultPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement nestedResult)
            ? nestedResult
            : document.RootElement;
        return new SpringChestCandidateFinding(
            ResultPath: resultPath,
            Status: JsonValue.GetString(result, "status"),
            BootedLevel: JsonValue.GetBoolean(result, "bootedLevel"),
            ObjectAppeared: JsonValue.GetBoolean(result, "objectAppeared"),
            BehaviorCorrect: result.TryGetProperty("behaviorCorrect", out JsonElement behavior) && behavior.ValueKind is JsonValueKind.True or JsonValueKind.False ? behavior.GetBoolean() : null,
            Notes: JsonValue.GetString(result, "notes", JsonValue.GetString(result, "userResult", JsonValue.GetString(result, "likelyMeaning"))));
    }

    private static string FindLatestStoneHillSpringCandidateResultPath(string objectsDir)
    {
        if (!Directory.Exists(objectsDir))
            return Path.Combine(objectsDir, "stonehill-spring-chest.candidate-result.json");

        FileInfo? latest = new DirectoryInfo(objectsDir)
            .EnumerateFiles("*.candidate-result.json", SearchOption.TopDirectoryOnly)
            .Where(file =>
                file.Name.Contains("stonehill", StringComparison.OrdinalIgnoreCase) &&
                (file.Name.Contains("spring-chest", StringComparison.OrdinalIgnoreCase) ||
                 file.Name.Contains("springchest", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest?.FullName ?? Path.Combine(objectsDir, "stonehill-spring-chest.candidate-result.json");
    }

    private static SpringChestRamPairDiagnostic BuildRamPairDiagnostic(string captureDir, string name, string beforeName, string afterName, string levelKey, int[] trueIndices)
    {
        string beforePath = Path.Combine(captureDir, beforeName);
        string afterPath = Path.Combine(captureDir, afterName);
        if (!File.Exists(beforePath) || !File.Exists(afterPath))
        {
            return new SpringChestRamPairDiagnostic(
                Name: name,
                LevelKey: levelKey,
                BeforePath: beforePath,
                AfterPath: afterPath,
                Status: "missing-captures",
                Summary: $"Need {beforeName} and {afterName}.",
                TargetDiffs: []);
        }

        byte[] before = File.ReadAllBytes(beforePath);
        byte[] after = File.ReadAllBytes(afterPath);
        if (!TryGetRuntimeTable(before, out int beforeTable, out int beforeCount) ||
            !TryGetRuntimeTable(after, out int afterTable, out int afterCount))
        {
            return new SpringChestRamPairDiagnostic(name, levelKey, beforePath, afterPath, "invalid-ram", "One or both files do not look like Spyro live RAM.", []);
        }

        List<SpringChestRecordDiff> diffs = [];
        foreach (int trueIndex in trueIndices)
        {
            if (trueIndex >= beforeCount || trueIndex >= afterCount)
            {
                diffs.Add(new SpringChestRecordDiff(trueIndex, "missing-runtime-row", []));
                continue;
            }

            byte[] beforeRecord = before.AsSpan(beforeTable + (trueIndex * RecordStride), RecordStride).ToArray();
            byte[] afterRecord = after.AsSpan(afterTable + (trueIndex * RecordStride), RecordStride).ToArray();
            List<string> changed = [];
            for (int offset = 0; offset < RecordStride; offset++)
            {
                if (beforeRecord[offset] != afterRecord[offset])
                    changed.Add($"+0x{offset:X2}: 0x{beforeRecord[offset]:X2}->0x{afterRecord[offset]:X2}");
            }

            diffs.Add(new SpringChestRecordDiff(trueIndex, changed.Count == 0 ? "unchanged" : "changed", changed.Take(24).ToArray()));
        }

        string summary = $"runtime counts {beforeCount}->{afterCount}; changed target rows={diffs.Count(diff => diff.Status == "changed")}";
        return new SpringChestRamPairDiagnostic(name, levelKey, beforePath, afterPath, "compared", summary, diffs);
    }

    private static SpringChestRamNativeVsImportDiagnostic BuildNativeVsImportDiagnostic(string captureDir)
    {
        string nativeBeforePath = Path.Combine(captureDir, "peacekeepers-t70t71-before.bin");
        string importBeforePath = Path.Combine(captureDir, "stonehill-imported-t195t196-before.bin");
        if (!File.Exists(nativeBeforePath) || !File.Exists(importBeforePath))
        {
            return new SpringChestRamNativeVsImportDiagnostic(
                NativeBeforePath: nativeBeforePath,
                ImportBeforePath: importBeforePath,
                Status: "missing-captures",
                Summary: "Need native Peace Keepers and imported Stone Hill before-hit captures.",
                RowComparisons: []);
        }

        byte[] native = File.ReadAllBytes(nativeBeforePath);
        byte[] imported = File.ReadAllBytes(importBeforePath);
        if (!TryGetRuntimeTable(native, out int nativeTable, out int nativeCount) ||
            !TryGetRuntimeTable(imported, out int importTable, out int importCount))
        {
            return new SpringChestRamNativeVsImportDiagnostic(
                nativeBeforePath,
                importBeforePath,
                "invalid-ram",
                "One or both before-hit captures do not look like Spyro live RAM.",
                []);
        }

        (int NativeIndex, int ImportIndex, string Label)[] pairs =
        [
            (70, 196, "native T70 visible shell -> imported visible shell"),
            (71, 196, "native T71 visible shell -> imported visible shell"),
            (70, 195, "native T70 visible shell -> imported local controller"),
            (71, 195, "native T71 visible shell -> imported local controller")
        ];
        List<SpringChestRecordCompareDiff> comparisons = [];
        foreach ((int nativeIndex, int importIndex, string label) in pairs)
        {
            if (nativeIndex >= nativeCount || importIndex >= importCount)
            {
                comparisons.Add(new SpringChestRecordCompareDiff(
                    NativeTrueIndex: nativeIndex,
                    ImportTrueIndex: importIndex,
                    Status: "missing-runtime-row",
                    Summary: $"{label}; runtime counts native={nativeCount}, imported={importCount}",
                    Differences: []));
                continue;
            }

            byte[] nativeRecord = native.AsSpan(nativeTable + (nativeIndex * RecordStride), RecordStride).ToArray();
            byte[] importRecord = imported.AsSpan(importTable + (importIndex * RecordStride), RecordStride).ToArray();
            List<string> differences = [];
            differences.Add($"role: {label}");
            differences.Add($"native key bytes: {FormatKeyBytes(nativeRecord)}");
            differences.Add($"import key bytes: {FormatKeyBytes(importRecord)}");
            string lifecycleRead = BuildLifecycleRead(importRecord);
            if (!string.IsNullOrWhiteSpace(lifecycleRead))
                differences.Add(lifecycleRead);
            for (int offset = 0; offset < RecordStride; offset++)
            {
                if (nativeRecord[offset] == importRecord[offset])
                    continue;

                differences.Add($"+0x{offset:X2} {FieldName(offset)}: native 0x{nativeRecord[offset]:X2}, imported 0x{importRecord[offset]:X2}");
            }

            comparisons.Add(new SpringChestRecordCompareDiff(
                NativeTrueIndex: nativeIndex,
                ImportTrueIndex: importIndex,
                Status: differences.Count == 0 ? "matches-native" : "differs",
                Summary: $"{Math.Max(0, differences.Count - 3)} byte/field note(s)",
                Differences: differences.Take(40).ToArray()));
        }

        AddDynamicSpawnComparison(captureDir, comparisons);

        int totalDifferences = comparisons.Sum(comparison => comparison.Differences.Count);
        string summary = $"runtime counts native={nativeCount}, imported={importCount}; visible pre-hit and after-hit row differences shown={totalDifferences}";
        return new SpringChestRamNativeVsImportDiagnostic(nativeBeforePath, importBeforePath, "compared", summary, comparisons);
    }

    private static void AddDynamicSpawnComparison(string captureDir, List<SpringChestRecordCompareDiff> comparisons)
    {
        string nativeAfterPath = Path.Combine(captureDir, "peacekeepers-t70t71-after-hit.bin");
        string importAfterPath = Path.Combine(captureDir, "stonehill-imported-t195t196-after-hit.bin");
        string importEvent2Path = Path.Combine(captureDir, "stonehill-imported-t195t196-after-hit-event-2.bin");
        if (!TryReadRuntimeRecord(nativeAfterPath, 136, out byte[]? nativeSpawn, out int nativeAfterCount) ||
            !TryReadRuntimeRecord(importAfterPath, 200, out byte[]? importFirstAfterSpawn, out int importFirstAfterCount) ||
            !TryReadRuntimeRecord(importEvent2Path, 200, out byte[]? importEvent2Spawn, out int importEvent2Count))
        {
            comparisons.Add(new SpringChestRecordCompareDiff(
                NativeTrueIndex: 136,
                ImportTrueIndex: 200,
                Status: "missing-after-hit-captures",
                Summary: "need native after-hit T136 and imported after-hit/event T200 captures",
                Differences:
                [
                    $"native after-hit capture: {Path.GetFileName(nativeAfterPath)} exists={File.Exists(nativeAfterPath)}",
                    $"import after-hit capture: {Path.GetFileName(importAfterPath)} exists={File.Exists(importAfterPath)}",
                    $"import event-2 capture: {Path.GetFileName(importEvent2Path)} exists={File.Exists(importEvent2Path)}"
                ]));
            return;
        }

        List<string> differences =
        [
            $"native after-hit T136 count={nativeAfterCount}: {FormatKeyBytes(nativeSpawn!)}; {FormatImportantWords(nativeSpawn!)}",
            $"import after-hit T200 count={importFirstAfterCount}: {FormatKeyBytes(importFirstAfterSpawn!)}; {FormatImportantWords(importFirstAfterSpawn!)}",
            $"import event-2 T200 count={importEvent2Count}: {FormatKeyBytes(importEvent2Spawn!)}; {FormatImportantWords(importEvent2Spawn!)}"
        ];

        if (TryReadRuntimeRecord(importEvent2Path, 201, out byte[]? importEvent2SecondSpawn, out _))
            differences.Add($"import event-2 T201 extra row: {FormatKeyBytes(importEvent2SecondSpawn!)}; {FormatImportantWords(importEvent2SecondSpawn!)}");

        int nativeActor = ActorId(nativeSpawn!);
        int nativeType = nativeSpawn![0x50];
        int firstImportActor = ActorId(importFirstAfterSpawn!);
        int firstImportType = importFirstAfterSpawn![0x50];
        int event2ImportActor = ActorId(importEvent2Spawn!);
        int event2ImportType = importEvent2Spawn![0x50];
        if (firstImportActor != nativeActor || firstImportType != nativeType || event2ImportActor != nativeActor || event2ImportType != nativeType)
        {
            differences.Insert(0, $"finding: native spring hit creates actor/type {HexWord(nativeActor)}/{HexByte(nativeType)}, but the Stone Hill import creates {HexWord(firstImportActor)}/{HexByte(firstImportType)} then {HexWord(event2ImportActor)}/{HexByte(event2ImportType)}. This points at the helper writing the wrong reward/effect row, which matches Sparx chasing/crashing behavior.");
        }

        comparisons.Add(new SpringChestRecordCompareDiff(
            NativeTrueIndex: 136,
            ImportTrueIndex: 200,
            Status: "after-hit-dynamic-row",
            Summary: "native added reward/effect row vs imported helper-created row",
            Differences: differences));
    }

    private static SpringChestCaptureFreshness BuildCaptureFreshness(string captureDir, string latestResultPath)
    {
        DateTimeOffset? candidateWrittenAt = File.Exists(latestResultPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(latestResultPath), TimeSpan.Zero)
            : null;
        string[] captureNames =
        [
            "peacekeepers-t70t71-before.bin",
            "peacekeepers-t70t71-after-hit.bin",
            "stonehill-imported-t195t196-before.bin",
            "stonehill-imported-t195t196-after-hit.bin"
        ];

        List<SpringChestCaptureFileFreshness> files = [];
        foreach (string captureName in captureNames)
        {
            string path = Path.Combine(captureDir, captureName);
            DateTimeOffset? writtenAt = File.Exists(path)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
            bool isImportedStoneHillCapture = captureName.StartsWith("stonehill-imported-", StringComparison.OrdinalIgnoreCase);
            bool staleForCandidate = isImportedStoneHillCapture && candidateWrittenAt.HasValue && writtenAt.HasValue && writtenAt < candidateWrittenAt;
            files.Add(new SpringChestCaptureFileFreshness(path, writtenAt, staleForCandidate));
        }

        int staleCount = files.Count(file => file.StaleForLatestCandidate);
        int missingCount = files.Count(file => !file.WrittenAtUtc.HasValue);
        string status = missingCount > 0
            ? "missing-captures"
            : staleCount > 0 ? "stale-stonehill-captures" : "fresh-enough";
        string summary = candidateWrittenAt.HasValue
            ? $"latest candidate result written {candidateWrittenAt:yyyy-MM-dd HH:mm:ss} UTC; stale Stone Hill capture(s)={staleCount}"
            : "latest candidate result timestamp is unavailable.";

        return new SpringChestCaptureFreshness(candidateWrittenAt, status, summary, files);
    }

    private static string FormatDate(DateTimeOffset? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) : "missing";

    private static string FieldName(int offset) => offset switch
    {
        >= 0x00 and <= 0x03 => "special-data pointer",
        >= 0x08 and <= 0x0B => "model/runtime pointer",
        >= 0x0C and <= 0x17 => "position",
        0x25 => "runtime flag 25",
        0x1A => "hit/reaction flag",
        0x29 => "runtime flag 29",
        0x2D => "runtime flag 2D",
        0x34 or 0x35 => "variant/partner",
        0x36 or 0x37 => "actor id",
        0x40 or 0x41 => "runtime state",
        0x49 => "activation flag",
        0x4C or 0x4D => "interaction/collision",
        0x50 => "type",
        0x51 => "state",
        0x52 => "flag 4A",
        0x53 => "flag 4B/reward",
        0x55 => "runtime flag 55",
        _ => "unknown"
    };

    private static string FormatKeyBytes(byte[] record)
    {
        int actorId = ActorId(record);
        int variant = record[0x34] | (record[0x35] << 8);
        return $"actor={HexWord(actorId)}, type={HexByte(record[0x50])}, state={HexByte(record[0x51])}, f4A={HexByte(record[0x52])}, f4B={HexByte(record[0x53])}, variant={HexWord(variant)}, b3A={HexByte(record[0x3A])}, b49={HexByte(record[0x49])}, b4C={HexByte(record[0x4C])}, b4D={HexByte(record[0x4D])}, b55={HexByte(record[0x55])}";
    }

    private static string FormatImportantWords(byte[] record)
    {
        uint w34 = BitConverter.ToUInt32(record, 0x34);
        uint w48 = BitConverter.ToUInt32(record, 0x48);
        uint w4C = BitConverter.ToUInt32(record, 0x4C);
        uint w50 = BitConverter.ToUInt32(record, 0x50);
        uint w54 = BitConverter.ToUInt32(record, 0x54);
        return $"w34=0x{w34:X8}, w48=0x{w48:X8}, w4C=0x{w4C:X8}, w50=0x{w50:X8}, w54=0x{w54:X8}";
    }

    private static int ActorId(byte[] record) => record[0x36] | (record[0x37] << 8);

    private static bool TryReadRuntimeRecord(string path, int trueIndex, out byte[]? record, out int runtimeCount)
    {
        record = null;
        runtimeCount = 0;
        if (!File.Exists(path))
            return false;

        byte[] ram = File.ReadAllBytes(path);
        if (!TryGetRuntimeTable(ram, out int tableOffset, out runtimeCount))
            return false;

        int recordOffset = tableOffset + (trueIndex * RecordStride);
        if (recordOffset < 0 || recordOffset + RecordStride > ram.Length)
            return false;

        record = ram.AsSpan(recordOffset, RecordStride).ToArray();
        return true;
    }

    private static string BuildLifecycleRead(byte[] importedRecord)
    {
        if ((importedRecord[0x36] | (importedRecord[0x37] << 8)) != 0x0149)
            return "";

        bool looksPostHit = importedRecord[0x49] == 0x01 && importedRecord[0x4C] == 0x30 && importedRecord[0x4D] == 0x00 && importedRecord[0x55] == 0x20;
        bool looksNativePreHit = importedRecord[0x49] == 0x00 && importedRecord[0x4C] == 0x80 && importedRecord[0x4D] == 0x0C && importedRecord[0x55] == 0x10;
        if (looksPostHit)
            return "lifecycle read: imported shell is already in the post-hit-style byte shape before the player hits it; this matches the no-pop/top-color-change failures.";
        if (looksNativePreHit)
            return "lifecycle read: imported shell matches the native pre-hit shell byte shape.";
        return "lifecycle read: imported shell is neither the native pre-hit nor native post-hit byte shape.";
    }

    private static bool TryGetRuntimeTable(byte[] ram, out int tableOffset, out int runtimeCount)
    {
        tableOffset = 0;
        runtimeCount = 0;
        if (ram.Length < RuntimeMobyPointerOffset + 4)
            return false;

        uint pointer = BitConverter.ToUInt32(ram, RuntimeMobyPointerOffset);
        tableOffset = (int)(pointer & 0x001FFFFF);
        if (pointer < 0x80000000 || tableOffset < 0 || tableOffset + RecordStride > ram.Length)
            return false;

        if (ram.Length >= DynamicMobyPointerOffset + 4)
        {
            int dynamicOffset = (int)(BitConverter.ToUInt32(ram, DynamicMobyPointerOffset) & 0x001FFFFF);
            if (dynamicOffset > tableOffset && dynamicOffset <= ram.Length && ((dynamicOffset - tableOffset) % RecordStride) == 0)
            {
                runtimeCount = Math.Min(512, (dynamicOffset - tableOffset) / RecordStride);
                return true;
            }
        }

        runtimeCount = Math.Min(512, (ram.Length - tableOffset) / RecordStride);
        return true;
    }

    private static string BuildMarkdown(SpringChestInteractionDiagnosticReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Spring Chest Interaction Diagnostic");
        builder.AppendLine();
        builder.AppendLine(report.Conclusion);
        builder.AppendLine();
        builder.AppendLine("## Latest Stone Hill Candidate");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{report.LatestCandidate.Status}`");
        builder.AppendLine($"- Booted: {report.LatestCandidate.BootedLevel}");
        builder.AppendLine($"- Appeared: {report.LatestCandidate.ObjectAppeared}");
        builder.AppendLine($"- Behavior correct: {FormatNullableBool(report.LatestCandidate.BehaviorCorrect)}");
        builder.AppendLine($"- Notes: {report.LatestCandidate.Notes}");
        builder.AppendLine();
        builder.AppendLine("## Native Source Clusters");
        builder.AppendLine();
        builder.AppendLine("| Level | T | Reward | Special | XYZ | Read | Nearest records | Index neighbors |");
        builder.AppendLine("|---|---:|---|---|---|---|---|---|");
        foreach (SpringChestNativeCluster cluster in report.NativeClusters)
        {
            builder.AppendLine($"| {cluster.LevelName} | {cluster.SpringChest.TrueIndex} | `{cluster.SpringChest.Flag4BHex}` | `{cluster.SpringChest.SpecialOffsetHex}` | {cluster.SpringChest.X:0.##},{cluster.SpringChest.Y:0.##},{cluster.SpringChest.Z:0.##} | {cluster.Read} | {FormatRecords(cluster.NearbyRecords)} | {FormatRecords(cluster.IndexNeighbors)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Native Neighbor Pattern Ranking");
        builder.AppendLine();
        builder.AppendLine("This ranks rows that repeatedly appear near working native Spring Chests. Rows marked as contained gem/helper are stronger candidates for hidden reward/link behavior than ordinary loose gems.");
        builder.AppendLine();
        builder.AppendLine("| Actor | Family | Mentions | Levels | Example rows | Read |");
        builder.AppendLine("|---|---|---:|---|---|---|");
        foreach (SpringChestNeighborPattern pattern in report.NeighborPatterns.Take(16))
            builder.AppendLine($"| `{pattern.ActorIdHex}` | {pattern.Family} | {pattern.SpringChestMentions} | {string.Join(", ", pattern.Levels)} | {string.Join(", ", pattern.Examples)} | {pattern.Read} |");
        builder.AppendLine();

        builder.AppendLine("## RAM Pair Diagnostics");
        builder.AppendLine();
        builder.AppendLine($"Capture folder: `{report.CaptureDirectory}`");
        builder.AppendLine();
        builder.AppendLine("| Pair | Status | Summary |");
        builder.AppendLine("|---|---|---|");
        foreach (SpringChestRamPairDiagnostic pair in report.RamPairDiagnostics)
            builder.AppendLine($"| {pair.Name} | `{pair.Status}` | {pair.Summary} |");
        builder.AppendLine();
        foreach (SpringChestRamPairDiagnostic pair in report.RamPairDiagnostics.Where(pair => pair.TargetDiffs.Count > 0))
        {
            builder.AppendLine($"### {pair.Name}");
            builder.AppendLine();
            builder.AppendLine("| T | Status | Byte changes |");
            builder.AppendLine("|---:|---|---|");
            foreach (SpringChestRecordDiff diff in pair.TargetDiffs)
                builder.AppendLine($"| {diff.TrueIndex} | `{diff.Status}` | {string.Join("<br>", diff.Changes)} |");
            builder.AppendLine();
        }

        builder.AppendLine("## Capture Freshness");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{report.CaptureFreshness.Status}`");
        builder.AppendLine($"- Summary: {report.CaptureFreshness.Summary}");
        builder.AppendLine();
        builder.AppendLine("| Capture | Written | Stale for current Stone Hill candidate |");
        builder.AppendLine("|---|---|---|");
        foreach (SpringChestCaptureFileFreshness file in report.CaptureFreshness.Files)
            builder.AppendLine($"| `{Path.GetFileName(file.Path)}` | {FormatDate(file.WrittenAtUtc)} | {file.StaleForLatestCandidate} |");
        builder.AppendLine();

        builder.AppendLine("## Native vs Imported Pre-Hit RAM");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{report.NativeVsImport.Status}`");
        builder.AppendLine($"- Summary: {report.NativeVsImport.Summary}");
        builder.AppendLine($"- Native before: `{report.NativeVsImport.NativeBeforePath}`");
        builder.AppendLine($"- Imported before: `{report.NativeVsImport.ImportBeforePath}`");
        builder.AppendLine();
        if (report.NativeVsImport.RowComparisons.Count > 0)
        {
            builder.AppendLine("| Rows | Status | Summary | First byte differences |");
            builder.AppendLine("|---|---|---|---|");
            foreach (SpringChestRecordCompareDiff comparison in report.NativeVsImport.RowComparisons)
            {
                string changes = comparison.Differences.Count == 0 ? "-" : string.Join("<br>", comparison.Differences);
                builder.AppendLine($"| T{comparison.NativeTrueIndex} -> T{comparison.ImportTrueIndex} | `{comparison.Status}` | {comparison.Summary} | {changes} |");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Next Capture Plan");
        builder.AppendLine();
        foreach (string step in report.NextCapturePlan)
            builder.AppendLine($"- {step}");
        builder.AppendLine();
        builder.AppendLine("## Working Read");
        builder.AppendLine();
        builder.AppendLine("The strongest current read is that the imported actor is drawing but not registering a hit/break response. The diagnostic now needs a native before/after hit capture so we can stop guessing and compare exactly which runtime rows or counters change when a real Spring Chest arms, breaks, and creates its reward.");
        return builder.ToString();
    }

    private static string FormatRecords(IReadOnlyList<SpringChestSourceRecord> records)
    {
        if (records.Count == 0)
            return "-";
        return string.Join(", ", records.Take(6).Select(record => $"T{record.TrueIndex} {record.Family} {record.SourceByte36Hex}/{record.Flag4AHex}/{record.Flag4BHex}"));
    }

    private static string FormatNullableBool(bool? value) => value.HasValue ? value.Value.ToString() : "unknown";
    private static float Distance2d(SpringChestSourceRecord a, SpringChestSourceRecord b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static long ParseNumber(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string HexByte(int value) => $"0x{value & 0xFF:X2}";
    private static string HexWord(int value) => $"0x{value & 0xFFFF:X4}";
    private static string HexOffset(uint value) => $"0x{value:X}";
    private static string EventCaptureName(string afterName, int eventIndex)
    {
        string extension = Path.GetExtension(afterName);
        string stem = Path.GetFileNameWithoutExtension(afterName);
        return $"{stem}-event-{eventIndex}{extension}";
    }
}

public sealed record SpringChestInteractionDiagnosticReport(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    SpringChestCandidateFinding LatestCandidate,
    IReadOnlyList<SpringChestNativeCluster> NativeClusters,
    IReadOnlyList<SpringChestNeighborPattern> NeighborPatterns,
    IReadOnlyList<SpringChestRamPairDiagnostic> RamPairDiagnostics,
    SpringChestCaptureFreshness CaptureFreshness,
    SpringChestRamNativeVsImportDiagnostic NativeVsImport,
    string CaptureDirectory,
    string Conclusion,
    IReadOnlyList<string> NextCapturePlan);

public sealed record SpringChestCandidateFinding(
    string ResultPath,
    string Status,
    bool BootedLevel,
    bool ObjectAppeared,
    bool? BehaviorCorrect,
    string Notes);

public sealed record SpringChestNativeCluster(
    string LevelKey,
    string LevelName,
    SpringChestSourceRecord SpringChest,
    IReadOnlyList<SpringChestSourceRecord> NearbyRecords,
    IReadOnlyList<SpringChestSourceRecord> IndexNeighbors,
    string Read);

public sealed record SpringChestNeighborPattern(
    string ActorIdHex,
    string Family,
    int SpringChestMentions,
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> Examples,
    string Read);

public sealed record SpringChestSourceRecord(
    string LevelKey,
    string LevelName,
    int TrueIndex,
    string SpecialOffsetHex,
    float X,
    float Y,
    float Z,
    string TypeHex,
    string StateHex,
    string ActorIdHex,
    string SourceByte36Hex,
    string SourceByte37Hex,
    string SourceByte4FHex,
    string Flag4AHex,
    string Flag4BHex,
    string Family);

internal sealed class SpringChestNeighborPatternBuilder(string actorIdHex, string family)
{
    private readonly HashSet<string> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _examples = [];
    private int _mentions;

    public void Add(SpringChestNativeCluster cluster, SpringChestSourceRecord record)
    {
        _mentions++;
        _levels.Add(cluster.LevelName);
        if (_examples.Count < 6)
            _examples.Add($"{cluster.LevelName}:T{cluster.SpringChest.TrueIndex}->T{record.TrueIndex} {record.Flag4AHex}/{record.Flag4BHex}");
    }

    public SpringChestNeighborPattern Build()
    {
        string read = family switch
        {
            "contained-gem-or-helper" => "Likely hidden reward/link rows; better next candidate material than loose standalone gems.",
            "standalone-gem" => "Native clusters often have nearby loose-looking gem rows, but copying these directly made loose gems appear.",
            "spring-chest" => "Adjacent Spring Chest pair relationship; useful for pair imports but not sufficient alone so far.",
            "normal-chest" => "Nearby ordinary chest behavior; risky because Stone Hill already has its own 0x00C2 package.",
            _ when actorIdHex == "0x00D8" => "Peace Keepers-only nearby helper candidate; full package import crashed, so decode before importing again.",
            _ => "Nearby native context row; investigate only if stronger candidates fail."
        };

        return new SpringChestNeighborPattern(
            ActorIdHex: actorIdHex,
            Family: family,
            SpringChestMentions: _mentions,
            Levels: _levels.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Examples: _examples,
            Read: read);
    }
}

public sealed record SpringChestRamPairDiagnostic(
    string Name,
    string LevelKey,
    string BeforePath,
    string AfterPath,
    string Status,
    string Summary,
    IReadOnlyList<SpringChestRecordDiff> TargetDiffs);

public sealed record SpringChestRecordDiff(
    int TrueIndex,
    string Status,
    IReadOnlyList<string> Changes);

public sealed record SpringChestCaptureFreshness(
    DateTimeOffset? LatestCandidateWrittenAtUtc,
    string Status,
    string Summary,
    IReadOnlyList<SpringChestCaptureFileFreshness> Files);

public sealed record SpringChestCaptureFileFreshness(
    string Path,
    DateTimeOffset? WrittenAtUtc,
    bool StaleForLatestCandidate);

public sealed record SpringChestRamNativeVsImportDiagnostic(
    string NativeBeforePath,
    string ImportBeforePath,
    string Status,
    string Summary,
    IReadOnlyList<SpringChestRecordCompareDiff> RowComparisons);

public sealed record SpringChestRecordCompareDiff(
    int NativeTrueIndex,
    int ImportTrueIndex,
    string Status,
    string Summary,
    IReadOnlyList<string> Differences);
