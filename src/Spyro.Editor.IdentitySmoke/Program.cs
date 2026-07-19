using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

string? workspaceArg = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
string? outputOption = args
    .FirstOrDefault(arg => arg.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))?
    ["--output=".Length..];
bool allowUnresolved = args.Contains("--allow-unresolved", StringComparer.OrdinalIgnoreCase);
string? refreshSourceCachesOption = args
    .FirstOrDefault(arg => arg.StartsWith("--refresh-source-caches=", StringComparison.OrdinalIgnoreCase))?
    ["--refresh-source-caches=".Length..];
string? discOption = args
    .FirstOrDefault(arg => arg.StartsWith("--disc=", StringComparison.OrdinalIgnoreCase))?
    ["--disc=".Length..];

EditorWorkspace workspace = EditorWorkspace.Find(workspaceArg);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
if (catalog.Levels.Count == 0)
    throw new InvalidOperationException("The Spyro level catalogue is missing or empty.");

SourceBoundaryCorrection[] sourceBoundaryCorrections =
[
    new("sunnyflight", "Sunny Flight", "0x1871074", "0x18710C8", "0x1BE8C8", 0x54, 45, "The earlier candidate decoded one non-moby prefix/header row plus repeated 0x7F data. Advancing by 0x54 yields 45 native flight records matching the live T0-T44 rows, followed immediately by a zero record."),
    new("crystalflight", "Crystal Flight", "0x3584074", "0x35840C8", "0x19A8C8", 0x54, 35, "The earlier candidate decoded one 0x23 row plus thirty-five repeated 0x7F rows. The corrected offset yields 35 native flight records followed immediately by a zero record."),
    new("beastmakers", "Beast Makers", "0x3763B4C", "0x3763BA4", "0x1C73A4", 0x58, 195, "The earlier candidate included one non-moby prefix/header row. Advancing one record yields 195 native records followed immediately by a zero record."),
    new("terracevillage", "Terrace Village", "0x39D3934", "0x39D398C", "0x1D418C", 0x58, 164, "The earlier candidate included one non-moby prefix/header row. Advancing one record yields 164 native records followed immediately by a zero record.")
];
ValidateCatalogCorrections(catalog, sourceBoundaryCorrections);

if (!string.IsNullOrWhiteSpace(refreshSourceCachesOption))
{
    if (string.IsNullOrWhiteSpace(discOption) || !File.Exists(discOption))
        throw new FileNotFoundException("--refresh-source-caches requires an existing --disc=<BIN> path.", discOption);

    string[] levelKeys = refreshSourceCachesOption
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (string levelKey in levelKeys)
    {
        LevelDefinition level = catalog.Levels.SingleOrDefault(candidate => string.Equals(candidate.Key, levelKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unknown level key '{levelKey}'.");
        string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        SourceMobyCacheResult result = await SourceMobyCacheBuilder.BuildAsync(Path.GetFullPath(discOption), level, cachePath);
        Console.WriteLine($"Refreshed {level.DisplayName}: {result.MobyCount} source records -> {result.OutputPath}");
    }
}

List<IdentityAuditRow> rows = new();
List<string> missingCaches = new();
List<string> countMismatches = new();
List<string> sourceMetadataMismatches = new();
foreach (LevelDefinition level in catalog.Levels)
{
    string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
    if (!File.Exists(cachePath))
    {
        missingCaches.Add(level.Key);
        continue;
    }

    List<Moby> mobys = MobyLoader.LoadCached(cachePath).ToList();
    if (mobys.Count != level.SourceRecordCount)
        countMismatches.Add($"{level.Key}: cache={mobys.Count}, catalogue={level.SourceRecordCount}");
    ValidateSourceCacheMetadata(cachePath, level, sourceMetadataMismatches);

    MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
    foreach (Moby moby in mobys)
    {
        bool unresolved = IsUnresolved(moby);
        bool generic = !unresolved && IsGenericLabel(moby.DisplayLabel);
        bool evidenceBacked = HasDirectIdentityEvidence(moby);
        string category = unresolved
            ? "unresolved"
            : generic
                ? evidenceBacked ? "evidence-backed generic" : "inferred generic"
                : evidenceBacked ? "evidence-backed precise" : "inferred precise";
        rows.Add(new IdentityAuditRow(
            level.Key,
            level.DisplayName,
            moby.TrueIndex,
            Fingerprint(moby),
            moby.DisplayLabel,
            moby.CandidateKind,
            moby.Confidence,
            moby.Evidence,
            category));
    }
}

if (missingCaches.Count > 0)
    throw new InvalidOperationException($"Missing {missingCaches.Count}/{catalog.Levels.Count} moby caches: {string.Join(", ", missingCaches)}");

IdentityAuditTotals totals = BuildTotals(rows);
IdentityAuditTotals preChangeComparableTotals = new(
    Total: 4937,
    EvidenceBackedPrecise: 3157,
    InferredPrecise: 1336,
    EvidenceBackedGeneric: 14,
    InferredGeneric: 375,
    Unresolved: 55);
var perLevel = rows
    .GroupBy(row => new { row.LevelKey, row.LevelName })
    .OrderBy(group => group.Key.LevelName, StringComparer.OrdinalIgnoreCase)
    .Select(group => new
    {
        group.Key.LevelKey,
        group.Key.LevelName,
        Totals = BuildTotals(group)
    })
    .ToArray();
var genericLabels = rows
    .Where(row => row.Category.Contains("generic", StringComparison.OrdinalIgnoreCase))
    .GroupBy(row => row.Label, StringComparer.OrdinalIgnoreCase)
    .Select(group => new
    {
        label = group.Key,
        count = group.Count(),
        levels = group.Select(row => row.LevelName).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
        samples = group.Take(5).Select(row => $"{row.LevelName}:T{row.TrueIndex}").ToArray()
    })
    .OrderByDescending(group => group.count)
    .ThenBy(group => group.label, StringComparer.OrdinalIgnoreCase)
    .ToArray();
IdentityAuditRow[] unresolvedRows = rows
    .Where(row => row.Category == "unresolved")
    .OrderBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
    .ThenBy(row => row.TrueIndex)
    .ToArray();
var confidenceSummary = rows
    .GroupBy(row => string.IsNullOrWhiteSpace(row.Confidence) ? "(none)" : row.Confidence, StringComparer.OrdinalIgnoreCase)
    .Select(group => new { confidence = group.Key, count = group.Count() })
    .OrderByDescending(group => group.count)
    .ThenBy(group => group.confidence, StringComparer.OrdinalIgnoreCase)
    .ToArray();

string outputPrefix = string.IsNullOrWhiteSpace(outputOption)
    ? Path.Combine(workspace.RootPath, "_local", "smoke", "moby-name-coverage-audit")
    : Path.GetFullPath(outputOption);
string? outputDirectory = Path.GetDirectoryName(outputPrefix);
if (!string.IsNullOrWhiteSpace(outputDirectory))
    Directory.CreateDirectory(outputDirectory);
string jsonPath = $"{outputPrefix}.json";
string markdownPath = $"{outputPrefix}.md";

var report = new
{
    generatedAt = DateTimeOffset.Now,
    workspace = workspace.RootPath,
    cacheCoverage = new
    {
        loaded = catalog.Levels.Count - missingCaches.Count,
        expected = catalog.Levels.Count,
        sourceRecordCountsMatch = countMismatches.Count == 0,
        sourceRecordCountNotes = countMismatches,
        sourceMetadataMatchesCatalog = sourceMetadataMismatches.Count == 0,
        sourceMetadataNotes = sourceMetadataMismatches
    },
    preChangeComparableTotals,
    totals,
    perLevel,
    confidenceSummary,
    genericLabels,
    unresolvedRows,
    sourceBoundaryCorrections,
    rows
};
File.WriteAllText(
    jsonPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));

StringBuilder markdown = new();
markdown.AppendLine("# Moby Name Coverage Audit");
markdown.AppendLine();
markdown.AppendLine($"Generated from all {catalog.Levels.Count} portable caches and the current metadata/classifier path.");
markdown.AppendLine();
markdown.AppendLine("The audit separates naming precision from evidence strength. `Evidence-backed precise` means a specific object/role name supported by byte, source-signature, native-scene, user, or live evidence. `Inferred precise` is a specific name supported by roster/position inference. Generic safety labels and unresolved candidates stay separate.");
markdown.AppendLine();
markdown.AppendLine("## Totals");
markdown.AppendLine();
markdown.AppendLine($"- Mobys audited: {totals.Total}");
markdown.AppendLine($"- Cache/catalogue source-count differences: {countMismatches.Count} (expected for RAM-derived caches that include loader-created rows)");
markdown.AppendLine($"- Source-cache metadata mismatches: {sourceMetadataMismatches.Count}");
markdown.AppendLine($"- Evidence-backed precise: {totals.EvidenceBackedPrecise}");
markdown.AppendLine($"- Inferred precise: {totals.InferredPrecise}");
markdown.AppendLine($"- Evidence-backed generic: {totals.EvidenceBackedGeneric}");
markdown.AppendLine($"- Inferred generic: {totals.InferredGeneric}");
markdown.AppendLine($"- Unresolved: {totals.Unresolved}");
markdown.AppendLine();
markdown.AppendLine("## Before/after under the same rubric");
markdown.AppendLine();
markdown.AppendLine("The before row reconstructs the original 4,937-cache census with the final audit's stricter treatment of broad control-marker labels, so the category changes are directly comparable.");
markdown.AppendLine();
markdown.AppendLine("| Census | Total | Evidence precise | Inferred precise | Evidence generic | Inferred generic | Unresolved |");
markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
markdown.AppendLine($"| Before | {preChangeComparableTotals.Total} | {preChangeComparableTotals.EvidenceBackedPrecise} | {preChangeComparableTotals.InferredPrecise} | {preChangeComparableTotals.EvidenceBackedGeneric} | {preChangeComparableTotals.InferredGeneric} | {preChangeComparableTotals.Unresolved} |");
markdown.AppendLine($"| After | {totals.Total} | {totals.EvidenceBackedPrecise} | {totals.InferredPrecise} | {totals.EvidenceBackedGeneric} | {totals.InferredGeneric} | {totals.Unresolved} |");
markdown.AppendLine($"| Delta | {totals.Total - preChangeComparableTotals.Total:+#;-#;0} | {totals.EvidenceBackedPrecise - preChangeComparableTotals.EvidenceBackedPrecise:+#;-#;0} | {totals.InferredPrecise - preChangeComparableTotals.InferredPrecise:+#;-#;0} | {totals.EvidenceBackedGeneric - preChangeComparableTotals.EvidenceBackedGeneric:+#;-#;0} | {totals.InferredGeneric - preChangeComparableTotals.InferredGeneric:+#;-#;0} | {totals.Unresolved - preChangeComparableTotals.Unresolved:+#;-#;0} |");
markdown.AppendLine();
markdown.AppendLine("## Verified source-table corrections");
markdown.AppendLine();
markdown.AppendLine("| Level | Previous WAD offset | Corrected WAD offset | Relative offset | Delta | Records | Evidence |");
markdown.AppendLine("|---|---:|---:|---:|---:|---:|---|");
foreach (SourceBoundaryCorrection correction in sourceBoundaryCorrections)
{
    markdown.AppendLine($"| {Escape(correction.LevelName)} | `{correction.PreviousWadOffset}` | `{correction.CorrectedWadOffset}` | `{correction.CorrectedRelativeOffset}` | `0x{correction.DeltaBytes:X}` | {correction.RecordCount} | {Escape(correction.Evidence)} |");
}
markdown.AppendLine();
markdown.AppendLine("## Per-level coverage");
markdown.AppendLine();
markdown.AppendLine("| Level | Total | Evidence precise | Inferred precise | Evidence generic | Inferred generic | Unresolved |");
markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
foreach (var level in perLevel)
{
    markdown.AppendLine($"| {Escape(level.LevelName)} | {level.Totals.Total} | {level.Totals.EvidenceBackedPrecise} | {level.Totals.InferredPrecise} | {level.Totals.EvidenceBackedGeneric} | {level.Totals.InferredGeneric} | {level.Totals.Unresolved} |");
}

markdown.AppendLine();
markdown.AppendLine("## Unresolved rows");
markdown.AppendLine();
if (unresolvedRows.Length == 0)
{
    markdown.AppendLine("No rows retain an unknown, question-mark, placeholder, candidate, or needs-validation identity.");
}
else
{
    markdown.AppendLine("| Level | Moby | Fingerprint | Current label | Confidence | Evidence |");
    markdown.AppendLine("|---|---:|---|---|---|---|");
    foreach (IdentityAuditRow row in unresolvedRows)
        markdown.AppendLine($"| {Escape(row.LevelName)} | T{row.TrueIndex} | `{row.Fingerprint}` | {Escape(row.Label)} | {Escape(row.Confidence)} | {Escape(row.Evidence)} |");
}

markdown.AppendLine();
markdown.AppendLine("## Largest generic label families");
markdown.AppendLine();
markdown.AppendLine("| Count | Label | Levels | Samples |");
markdown.AppendLine("|---:|---|---|---|");
foreach (var group in genericLabels.Take(40))
    markdown.AppendLine($"| {group.count} | {Escape(group.label)} | {Escape(string.Join(", ", group.levels))} | {Escape(string.Join(", ", group.samples))} |");
File.WriteAllText(markdownPath, markdown.ToString());

if (!allowUnresolved)
{
    if (sourceMetadataMismatches.Count > 0)
        throw new InvalidOperationException($"The authoritative source caches have {sourceMetadataMismatches.Count} catalogue metadata mismatch(es). See {markdownPath}.");

    if (totals.Unresolved > 0)
        throw new InvalidOperationException($"The authoritative cache still has {totals.Unresolved} unresolved identity row(s). See {markdownPath}.");

    IdentityAuditRow[] nonCanonicalFingerprints = rows
        .Where(row =>
            !MobyIdentityFingerprint.TryParse(row.Fingerprint, out MobyIdentityFingerprintParts parts) ||
            !parts.HasFullNativeClass ||
            !row.Fingerprint.StartsWith("class=0x", StringComparison.OrdinalIgnoreCase))
        .Take(8)
        .ToArray();
    if (nonCanonicalFingerprints.Length > 0)
        throw new InvalidOperationException($"Identity audit emitted non-v2 fingerprints: {string.Join("; ", nonCanonicalFingerprints.Select(row => $"{row.LevelKey}:T{row.TrueIndex} {row.Fingerprint}"))}");

    IdentityAuditRow[] gnorcCoveC7 = rows
        .Where(row => row.LevelKey == "gnorccove" && row.Fingerprint.Contains("class=0x00C7", StringComparison.Ordinal))
        .ToArray();
    if (gnorcCoveC7.Length != 14 || gnorcCoveC7.Any(row => !row.Label.StartsWith("Spring chest", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException(
            $"Gnorc Cove's 14 native 0x00C7 family rows should all inherit the source-observed Spring chest identity; got {gnorcCoveC7.Length} row(s), {gnorcCoveC7.Count(row => row.Label.StartsWith("Spring chest", StringComparison.OrdinalIgnoreCase))} named Spring chest.");
    }

    RequireLabel(rows, "icecavern", 50, "Flame/charge chest");
    RequireLabel(rows, "icecavern", 186, "Life chest");
    RequireLabel(rows, "icecavern", 187, "Life chest");
    RequireOptionalLiveLabel(rows, "icecavern", 229, "Extra Life Chest butterfly");
    RequireOptionalLiveLabel(rows, "icecavern", 230, "Extra Life Chest butterfly");
    RequireLabel(rows, "metalhead", 0, "Torch");
    RequireLabel(rows, "metalhead", 21, "Strongarm");
    RequireLabel(rows, "metalhead", 30, "Armored Banana Boy");
    RequireLabel(rows, "metalhead", 34, "Metalhead boss");
    RequireLabel(rows, "metalhead", 37, "Power pole");
    RequireLabel(rows, "beastmakers", 49, "Swamp grass clump");
    RequireLabel(rows, "beastmakers", 56, "Beast Makers banner");
    RequireLabel(rows, "mistybog", 54, "Swamp grass clump");
    RequireLabel(rows, "mistybog", 67, "Beast Makers banner");
    RequireLabel(rows, "terracevillage", 35, "Swamp grass clump");
    RequireLabel(rows, "alpineridge", 141, "Broad alpine tree");
    RequireLabel(rows, "alpineridge", 153, "Broad alpine tree");
    RequireLabel(rows, "alpineridge", 148, "Small swaying alpine tree");
    RequireLabel(rows, "blowhard", 12, "Small swaying alpine tree");
    RequireLabel(rows, "blowhard", 15, "Large alpine tree");
    RequireLabel(rows, "blowhard", 17, "Slender alpine tree");
    RequireLabel(rows, "magiccrafters", 90, "Large alpine tree");
    RequireLabel(rows, "magiccrafters", 91, "Slender alpine tree");
    RequireLabel(rows, "artisans", 133, "Sunny Flight stepping stone");
    RequireLabel(rows, "artisans", 137, "Sunny Flight stepping stone");
    RequireLabel(rows, "artisans", 138, "Sunny Flight stepping-stone unlock controller");
    RequireLabel(rows, "artisans", 160, "Toasty portal gate controller");
    RequireLabel(rows, "artisans", 43, "Toasty portal particle emitter");
    RequireLabel(rows, "artisans", 44, "Toasty portal particle emitter");
    RequireLabel(rows, "artisans", 149, "Water bubble emitter");
    RequireLabel(rows, "artisans", 154, "Water bubble emitter");
    RequireLabel(rows, "magiccrafters", 113, "Water bubble emitter");
    RequireLabel(rows, "magiccrafters", 118, "Water bubble emitter");
    RequireLabel(rows, "alpineridge", 159, "Water bubble emitter");
    RequireLabel(rows, "alpineridge", 168, "Water bubble emitter");
    RequireLabel(rows, "artisans", 0, "Tower flag");
    RequireLabel(rows, "artisans", 96, "Tower flag");
    RequireLabel(rows, "stonehill", 38, "Tower flag");
    RequireLabel(rows, "stonehill", 147, "Tower flag");
    RequireLabel(rows, "dreamweavers", 31, "Clock Fool");
    RequireLabel(rows, "jacques", 21, "Clock Fool");
    RequireLabel(rows, "jacques", 38, "Clock Fool");
    RequireLabel(rows, "jacques", 33, "Jacques (boss)");
    RequireLabel(rows, "loftycastle", 18, "Balloognorc balloon");
    RequireLabel(rows, "loftycastle", 21, "Balloognorc balloon");
    RequireLabel(rows, "loftycastle", 79, "Balloognorc balloon");
    RequireLabel(rows, "beastmakers", 188, "Balloonist");
    RequireLabel(rows, "beastmakers", 69, "Crocodile");
    RequireLabel(rows, "beastmakers", 72, "Crocodile");
    RequireLabel(rows, "dreamweavers", 51, "Balloonist");
    RequireLabel(rows, "dreamweavers", 149, "Wall lantern");
    RequireLabel(rows, "dreamweavers", 150, "Wall lantern");
    RequireLabel(rows, "gnastysworld", 2, "Balloonist");
    RequireNativeClassIdentityFamily(
        rows,
        "toasty",
        0x013A,
        new Dictionary<int, string>
        {
            [11] = "Toasty disguise boss actor",
            [51] = "Toasty sheep-on-stilts phase actor"
        },
        "boss");
    RequireNativeClassIdentityFamily(
        rows,
        "toasty",
        0x000D,
        new Dictionary<int, string>
        {
            [54] = "Toasty sheep-form contained Blue gem (5)",
            [55] = "Toasty sheep-form contained Blue gem (5)"
        },
        "sheep-form contained boss reward");
    RequireNativeClassIdentityFamily(
        rows,
        "peacekeepers",
        0x01A1,
        new Dictionary<int, string>
        {
            [45] = "Cannon-breakable target rock",
            [102] = "Cannon-breakable target rock"
        },
        "cannon auto-target");
    RequireNativeClassIdentityFamily(
        rows,
        "mistybog",
        0x01E7,
        new Dictionary<int, string>
        {
            [87] = "Chicken cage",
            [88] = "Chicken cage"
        },
        "chicken cage scenery");
    RequireNativeClassIdentityFamily(
        rows,
        "mistybog",
        0x01E4,
        new Dictionary<int, string> { [211] = "Arrow-sign Fairy" },
        "sign-bearing Fairy");
    RequireNativeClassIdentityFamily(
        rows,
        "twilightharbor",
        0x00AB,
        new Dictionary<int, string>
        {
            [21] = "Drawbridge lever",
            [22] = "Drawbridge lever"
        },
        "drawbridge lever prop");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastygnorc",
        0x009F,
        new Dictionary<int, string> { [1] = "Key thief", [2] = "Key thief" },
        "key-carrying final-boss chase thief");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastygnorc",
        0x00B5,
        new Dictionary<int, string> { [100] = "Key-thief key", [101] = "Key-thief key" },
        "key collectible");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastygnorc",
        0x00B9,
        new Dictionary<int, string>
        {
            [102] = "First keyhole insertion target",
            [103] = "Second keyhole insertion target"
        },
        "key-lock insertion");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastysloot",
        0x00B5,
        new Dictionary<int, string>
        {
            [1] = "Thief key",
            [2] = "Thief key",
            [3] = "Thief key",
            [9] = "Thief key"
        },
        "key collectible");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastysloot",
        0x00B9,
        new Dictionary<int, string>
        {
            [7] = "Key-gate unlock pose marker",
            [8] = "Key-gate unlock pose marker",
            [10] = "Key-gate unlock pose marker",
            [11] = "Key-gate unlock pose marker"
        },
        "key-gate unlock position");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastysloot",
        0x00D9,
        new Dictionary<int, string> { [128] = "Gnasty's Loot 100% ending controller" },
        "14,000-gem ending");
    RequireNativeClassIdentityFamily(
        rows,
        "gnastysworld",
        0x00D0,
        new Dictionary<int, string> { [1] = "Gnorc Gnexus progression-gate controller" },
        "portal-gate environment-animation");
    RequireLabel(rows, "icecavern", 80, "Chargeable lamppost");
    RequireLabel(rows, "icecavern", 94, "Chargeable lamppost");
    RequireLabel(rows, "icecavern", 78, "Ice stalactite");
    RequireLabel(rows, "icecavern", 82, "Ice stalactite");
    RequireOptionalLiveLabel(rows, "drycanyon", 183, "Return Home letter R");
    RequireOptionalLiveLabel(rows, "drycanyon", 184, "Return Home letter E");
    RequireOptionalLiveLabel(rows, "drycanyon", 185, "Return Home letter T");
    RequireOptionalLiveLabel(rows, "drycanyon", 186, "Return Home letter U");
    RequireOptionalLiveLabel(rows, "drycanyon", 187, "Return Home letter R");
    RequireOptionalLiveLabel(rows, "drycanyon", 188, "Return Home letter N");
    RequireOptionalLiveLabel(rows, "drycanyon", 189, "Return Home letter H");
    RequireOptionalLiveLabel(rows, "drycanyon", 190, "Return Home letter O");
    RequireOptionalLiveLabel(rows, "drycanyon", 191, "Return Home letter M");
    RequireOptionalLiveLabel(rows, "drycanyon", 192, "Return Home letter E");
    RequireLabel(rows, "magiccrafters", 34, "Green Druid spell target");
    RequireLabel(rows, "magiccrafters", 41, "Green Druid spell target");
    RequireLabel(rows, "alpineridge", 48, "Green Druid spell target");
    RequireLabel(rows, "alpineridge", 135, "Dormant Green Druid spell target");
    RequireLabel(rows, "alpineridge", 136, "Dormant Green Druid spell target");
    RequireLabel(rows, "alpineridge", 137, "Green Druid spell target");
    RequireLabel(rows, "highcaves", 20, "Green Druid spell target");
    RequireLabel(rows, "highcaves", 23, "Green Druid spell target");
    RequireLabel(rows, "blowhard", 68, "Green Druid spell target");
    RequireLabel(rows, "blowhard", 71, "Green Druid spell target");
    RequireLabel(rows, "alpineridge", 11, "Green Druid");
    RequireLabel(rows, "alpineridge", 15, "Green Druid");
    RequireOptionalLiveLabel(rows, "stonehill", 196, "Dragon Egg");
    RequireOptionalLiveLabel(rows, "townsquare", 109, "Dragon Egg");
    RequireOptionalLiveLabel(rows, "drycanyon", 178, "Dragon Egg");
    RequireOptionalLiveLabel(rows, "magiccrafters", 136, "Dragon Egg");
    RequireLabel(rows, "gnorccove", 0, "Barrel supply hatch");
    RequireLabel(rows, "gnorccove", 220, "Barrel supply hatch");
    RequireLabel(rows, "gnorccove", 34, "Steel barrel shell");
    RequireLabel(rows, "gnorccove", 87, "Steel barrel shell");
    RequireLabel(rows, "hauntedtowers", 7, "Metal door");
    RequireLabel(rows, "hauntedtowers", 60, "Metal door");
    RequireLabel(rows, "hauntedtowers", 46, "Temporary Superflame Fairy");
    RequireLabel(rows, "hauntedtowers", 82, "Temporary Superflame Fairy");
    RequireLabel(rows, "hauntedtowers", 50, "Permanent Superflame Fairy");
    RequireLabel(rows, "highcaves", 9, "Rescue Fairy");
    RequireLabel(rows, "highcaves", 10, "Rescue Fairy");
    RequireLabel(rows, "highcaves", 11, "Rescue Fairy");
    RequireLabel(rows, "highcaves", 24, "Temporary Superflame Fairy");
    RequireLabel(rows, "doctorshemp", 72, "Gem spawner (Blue reward)");
    RequireLabel(rows, "doctorshemp", 73, "Gem spawner (Blue reward)");
    RequireLabel(rows, "doctorshemp", 74, "Gem spawner (Blue reward)");
    RequireLabel(rows, "doctorshemp", 75, "Gem spawner (Blue reward)");
    RequireLabel(rows, "toasty", 53, "Toasty phase-reward Blue gem marker");
    RequireLabel(rows, "doctorshemp", 70, "Doctor Shemp phase-reward Yellow gem marker");
    RequireLabel(rows, "doctorshemp", 71, "Doctor Shemp phase-reward Blue gem marker");
    RequireLabel(rows, "blowhard", 73, "Blowhard phase-reward Yellow gem marker");
    RequireLabel(rows, "blowhard", 74, "Blowhard phase-reward Yellow gem marker");
    RequireLabel(rows, "wizardpeak", 1, "Ice Gnorc (Blue reward)");
    RequireLabel(rows, "wizardpeak", 2, "Ice Gnorc (Blue reward)");
    RequireLabel(rows, "wizardpeak", 3, "Ice Gnorc (Yellow reward)");
    RequireLabel(rows, "wizardpeak", 4, "Ice Gnorc (Yellow reward)");
    RequireLabel(rows, "wizardpeak", 5, "Ice Gnorc (Yellow reward)");
    RequireLabel(rows, "wizardpeak", 20, "Ice Gnorc (Green reward)");
    RequireLabel(rows, "wizardpeak", 21, "Ice Gnorc (Green reward)");
    RequireLabel(rows, "wizardpeak", 22, "Ice Gnorc (Blue reward)");
    RequireLabel(rows, "wizardpeak", 23, "Ice Gnorc (Blue reward)");
    RequireLabel(rows, "wizardpeak", 130, "Ice Gnorc (Blue reward)");
    RequireLabel(rows, "sunnyflight", 37, "Flight challenge controller");
    RequireLabel(rows, "nightflight", 13, "Flight challenge controller");
    RequireLabel(rows, "crystalflight", 25, "Flight challenge controller");
    RequireLabel(rows, "wildflight", 22, "Flight challenge controller");
    RequireLabel(rows, "icyflight", 20, "Flight challenge controller");
    RequireOptionalLiveLabel(rows, "sunnyflight", 52, "Flight HUD timer digit");
    RequireOptionalLiveLabel(rows, "sunnyflight", 53, "Flight barrel HUD icon");
    RequireOptionalLiveLabel(rows, "sunnyflight", 60, "Flight barrel HUD icon");
    RequireLabel(rows, "crystalflight", 33, "Flight direction arrow sign");
    RequireLabel(rows, "crystalflight", 34, "Flight direction arrow sign");
    RequireLabel(rows, "nightflight", 32, "Flight direction arrow sign");
    RequireLabel(rows, "nightflight", 35, "Flight direction arrow sign");
    RequireLabel(rows, "clifftown", 148, "Lantern post");
    RequireLabel(rows, "terracevillage", 118, "Lantern post");
    RequireLabel(rows, "darkpassage", 199, "Lantern post");
    RequireLabel(rows, "gnastysloot", 91, "Lantern post");
    RequireLabel(rows, "beastmakers", 31, "Key Chest");
    RequireLabel(rows, "treetops", 165, "Key Chest");
    RequireLabel(rows, "metalhead", 133, "Key Chest");
    RequireLabel(rows, "loftycastle", 110, "Key Chest");
    RequireLabel(rows, "jacques", 49, "Key Chest");
    RequireLabel(rows, "gnorccove", 173, "Key Chest");
    RequireLabel(rows, "darkpassage", 134, "Armored / super flame chest linked record");
    RequireLabel(rows, "twilightharbor", 54, "Armored / super flame chest linked record");
    RequireLabel(rows, "crystalflight", 0, "Flight chest target");
    RequireLabel(rows, "crystalflight", 7, "Flight ring");
    RequireLabel(rows, "crystalflight", 15, "Flight arch");
    RequireLabel(rows, "crystalflight", 26, "Airplane");
}

Console.WriteLine($"Moby name coverage: caches={catalog.Levels.Count}/{catalog.Levels.Count}, mobys={totals.Total}, evidence-precise={totals.EvidenceBackedPrecise}, inferred-precise={totals.InferredPrecise}, evidence-generic={totals.EvidenceBackedGeneric}, inferred-generic={totals.InferredGeneric}, unresolved={totals.Unresolved}");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");
return 0;

static IdentityAuditTotals BuildTotals(IEnumerable<IdentityAuditRow> rows)
{
    IdentityAuditRow[] items = rows.ToArray();
    return new IdentityAuditTotals(
        items.Length,
        items.Count(row => row.Category == "evidence-backed precise"),
        items.Count(row => row.Category == "inferred precise"),
        items.Count(row => row.Category == "evidence-backed generic"),
        items.Count(row => row.Category == "inferred generic"),
        items.Count(row => row.Category == "unresolved"));
}

static bool IsUnresolved(Moby moby)
{
    string label = moby.DisplayLabel;
    string text = $"{label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
    return string.IsNullOrWhiteSpace(label)
        || label.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, Moby.FallbackLabel(moby.Type), StringComparison.Ordinal)
        || text.Contains('?')
        || text.Contains("unknown")
        || text.Contains("placeholder")
        || text.Contains(" candidate")
        || text.Contains("needs live validation")
        || text.Contains("needs per-level confirmation")
        || text.Contains("exact model")
        || text.Contains("until live-tested")
        || text.Contains("visible object not confirmed")
        || text.Contains("actor, container, or interactive object");
}

static bool IsGenericLabel(string label)
{
    string text = label.Trim().ToLowerInvariant();
    return text.Contains("system/helper")
        || text.StartsWith("class 0x", StringComparison.Ordinal)
        || text.Contains("passive control")
        || text.Contains("actor/container")
        || text.Contains("flag/scenery")
        || text.Contains("scenery/prop")
        || text.Contains("special object")
        || text.Contains("scene/route")
        || text.Contains("course marker")
        || text.Contains("control marker")
        || text.Contains("special control")
        || text.Contains("route marker")
        || text.Contains("route scenery")
        || text.Contains("structural scenery")
        || text.Contains("regional scenery")
        || text.EndsWith("scenery prop", StringComparison.Ordinal)
        || text.EndsWith("scenery", StringComparison.Ordinal)
        || text.EndsWith("prop/control marker", StringComparison.Ordinal)
        || text.EndsWith("chest link", StringComparison.Ordinal);
}

static bool HasDirectIdentityEvidence(Moby moby)
{
    string confidence = moby.Confidence.ToLowerInvariant();
    return confidence.Contains("byte-pattern")
        || confidence.Contains("native-scene")
        || confidence.Contains("user-override")
        || confidence.Contains("live-observed")
        || confidence.Contains("observed-fingerprint")
        || confidence.Contains("source-signature-observed")
        || confidence.Contains("native-class-observed")
        || confidence.Contains("native-class-linked")
        || confidence.Contains("validated");
}

static void ValidateSourceCacheMetadata(string cachePath, LevelDefinition level, List<string> mismatches)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(cachePath));
    JsonElement root = document.RootElement;
    string source = root.TryGetProperty("source", out JsonElement sourceElement)
        ? sourceElement.GetString() ?? ""
        : "";
    if (!string.Equals(source, "source-wad-moby-table", StringComparison.OrdinalIgnoreCase))
        return;

    string tableOffset = root.TryGetProperty("sourceTableWadOffset", out JsonElement offsetElement)
        ? offsetElement.GetString() ?? ""
        : "";
    int recordCount = root.TryGetProperty("sourceRecordCount", out JsonElement countElement) && countElement.TryGetInt32(out int parsedCount)
        ? parsedCount
        : -1;
    if (!string.Equals(tableOffset, level.SourceTableWadOffset, StringComparison.OrdinalIgnoreCase) || recordCount != level.SourceRecordCount)
    {
        mismatches.Add(
            $"{level.Key}: cache offset/count={tableOffset}/{recordCount}, catalogue={level.SourceTableWadOffset}/{level.SourceRecordCount}");
    }
}

static void ValidateCatalogCorrections(LevelCatalog catalog, IEnumerable<SourceBoundaryCorrection> corrections)
{
    foreach (SourceBoundaryCorrection correction in corrections)
    {
        LevelDefinition level = catalog.Levels.Single(candidate => string.Equals(candidate.Key, correction.LevelKey, StringComparison.OrdinalIgnoreCase));
        if (!string.Equals(level.SourceTableWadOffset, correction.CorrectedWadOffset, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(level.SourceTableRelativeOffset, correction.CorrectedRelativeOffset, StringComparison.OrdinalIgnoreCase) ||
            level.SourceRecordCount != correction.RecordCount)
        {
            throw new InvalidOperationException(
                $"{correction.LevelName}'s verified source boundary regressed: catalogue={level.SourceTableWadOffset}/{level.SourceTableRelativeOffset}/{level.SourceRecordCount}, expected={correction.CorrectedWadOffset}/{correction.CorrectedRelativeOffset}/{correction.RecordCount}.");
        }
    }
}

static void RequireLabel(IEnumerable<IdentityAuditRow> rows, string levelKey, int trueIndex, string expectedPrefix)
{
    IdentityAuditRow? row = rows.SingleOrDefault(candidate => candidate.LevelKey == levelKey && candidate.TrueIndex == trueIndex);
    if (row is null || !row.Label.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Expected {levelKey} T{trueIndex} to be named {expectedPrefix}; got '{row?.Label ?? "missing"}'.");
    }
}

static void RequireOptionalLiveLabel(IEnumerable<IdentityAuditRow> rows, string levelKey, int trueIndex, string expectedPrefix)
{
    IdentityAuditRow? row = rows.SingleOrDefault(candidate => candidate.LevelKey == levelKey && candidate.TrueIndex == trueIndex);
    if (row is not null && !row.Label.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Expected optional live {levelKey} T{trueIndex} to be named {expectedPrefix}; got '{row.Label}'.");
    }
}

static void RequireNativeClassIdentityFamily(
    IEnumerable<IdentityAuditRow> rows,
    string levelKey,
    int nativeClass,
    IReadOnlyDictionary<int, string> expectedLabels,
    string expectedKindFragment)
{
    IdentityAuditRow[] family = rows
        .Where(row =>
            row.LevelKey == levelKey &&
            MobyIdentityFingerprint.TryParse(row.Fingerprint, out MobyIdentityFingerprintParts parts) &&
            parts.HasFullNativeClass &&
            parts.NativeClass == nativeClass)
        .OrderBy(row => row.TrueIndex)
        .ToArray();
    int[] actualIndexes = family.Select(row => row.TrueIndex).ToArray();
    int[] expectedIndexes = expectedLabels.Keys.Order().ToArray();
    if (!actualIndexes.SequenceEqual(expectedIndexes))
    {
        throw new InvalidOperationException(
            $"Expected {levelKey} native class 0x{nativeClass:X4} family at T{string.Join(",T", expectedIndexes)}; got T{string.Join(",T", actualIndexes)}.");
    }

    IdentityAuditRow[] incorrect = family
        .Where(row =>
            !expectedLabels.TryGetValue(row.TrueIndex, out string? expectedLabel) ||
            !row.Label.Equals(expectedLabel, StringComparison.Ordinal) ||
            !row.CandidateKind.Contains(expectedKindFragment, StringComparison.OrdinalIgnoreCase) ||
            !row.Confidence.Contains("native-class-observed", StringComparison.OrdinalIgnoreCase) ||
            !row.Category.Equals("evidence-backed precise", StringComparison.Ordinal) ||
            row.Label.Contains("route", StringComparison.OrdinalIgnoreCase) ||
            row.Label.Contains("scene control", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (incorrect.Length > 0)
    {
        throw new InvalidOperationException(
            $"{levelKey} native class 0x{nativeClass:X4} final identity regression: " +
            string.Join("; ", incorrect.Select(row =>
                $"T{row.TrueIndex} {row.Label}/{row.CandidateKind}/{row.Confidence}/{row.Category}")));
    }
}

static string Fingerprint(Moby moby) =>
    MobyIdentityFingerprint.BuildV2(moby);

static string Escape(string value) => value
    .Replace("|", "\\|", StringComparison.Ordinal)
    .Replace("\r", " ", StringComparison.Ordinal)
    .Replace("\n", " ", StringComparison.Ordinal);

internal sealed record IdentityAuditRow(
    string LevelKey,
    string LevelName,
    int TrueIndex,
    string Fingerprint,
    string Label,
    string CandidateKind,
    string Confidence,
    string Evidence,
    string Category);

internal sealed record IdentityAuditTotals(
    int Total,
    int EvidenceBackedPrecise,
    int InferredPrecise,
    int EvidenceBackedGeneric,
    int InferredGeneric,
    int Unresolved);

internal sealed record SourceBoundaryCorrection(
    string LevelKey,
    string LevelName,
    string PreviousWadOffset,
    string CorrectedWadOffset,
    string CorrectedRelativeOffset,
    int DeltaBytes,
    int RecordCount,
    string Evidence);
