using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int WadLba = 37;
const int RequestedDonorTextureId = 17;
const int MaterialTemplateTextureId = 0;

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal)));
string[] onlyLevelKeys = ParseOnlyLevelKeys(args);
string outputSuffix = ParseOutputSuffix(args);
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string wadAnalysisPath = Path.Combine(workspaceRoot, "spyro-wad-analysis.json");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "all-level-synthetic-global-texture-repack");
string reportBaseName = "all-level-synthetic-global-texture-repack-audit" +
    (outputSuffix.Length == 0 ? "" : $"-{outputSuffix}");
string jsonPath = Path.Combine(outputRoot, reportBaseName + ".json");
string markdownPath = Path.Combine(outputRoot, reportBaseName + ".md");

Directory.CreateDirectory(outputRoot);
if (!File.Exists(sourceImagePath))
    throw new FileNotFoundException("Missing retail source BIN.", sourceImagePath);
if (!File.Exists(wadAnalysisPath))
    throw new FileNotFoundException("Missing WAD analysis required by the append-capacity API.", wadAnalysisPath);

FileInfo sourceInfoBefore = new(sourceImagePath);
string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = FileLength(sourceImagePath);
DateTime sourceWriteTimeBefore = sourceInfoBefore.LastWriteTimeUtc;
DiscLayout discLayout = DetectDiscLayout(sourceImagePath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] allLevels = LevelRealmCatalog
    .OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    .ToArray();
Assert(allLevels.Length == 35, $"Expected all 35 mapped retail levels, got {allLevels.Length}.");
HashSet<string> onlySet = onlyLevelKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
LevelDefinition[] levels = onlySet.Count == 0
    ? allLevels
    : allLevels.Where(level => onlySet.Contains(LevelCatalog.NormalizeKey(level.Key))).ToArray();
if (onlySet.Count > 0)
{
    string[] missing = onlySet
        .Except(levels.Select(level => LevelCatalog.NormalizeKey(level.Key)), StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Assert(missing.Length == 0, $"Unknown --only level key(s): {string.Join(", ", missing)}.");
    Assert(levels.Length > 0, "The --only filter selected no levels.");
}

LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the catalog.");
IReadOnlyList<TerrainTextureSlot> donorSlots =
    TerrainPatchExporter.InspectTextureSlots(sourceImagePath, donorLevel);
int donorTextureId = ResolveDonorTextureId(donorSlots);
TerrainTextureSlot donorSlot = donorSlots.Single(slot => slot.TextureId == donorTextureId);
Assert(donorSlot.HasNormalDescriptors && donorSlot.HasCloseDescriptors,
    $"Gnasty's World T{donorTextureId} is not a complete native donor tier.");

List<AuditRow> rows = [];
foreach ((LevelDefinition level, int levelIndex) in levels.Select((item, index) => (item, index)))
{
    Stopwatch timer = Stopwatch.StartNew();
    int retailCount = -1;
    int tailId = -1;
    string capacityMode = "unresolved";
    NativeTerrainTextureRecordAppendCapacity? capacity = null;
    NativeTerrainTextureGlobalRepackSyntheticPlan? syntheticPlan = null;
    List<string> failedProofs = [];
    string failure = "";
    bool success = false;
    PatchProof patchProof = PatchProof.Empty;
    int independentlyProtectedBytes = -1;
    int independentlyReleasedBytes = -1;

    try
    {
        NativeTerrainTextureRecordAppendSourceBinding binding =
            NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, level);
        capacity = NativeTerrainTextureRecordAppendBuilder.InspectCapacity(
            sourceImagePath,
            level,
            wadAnalysisPath);
        retailCount = binding.ExpectedSourceTextureCount;
        tailId = retailCount;
        capacityMode = capacity.WouldRequireTargetEntryGrowth
            ? $"+0x{capacity.RequiredSectorAlignedEntryGrowthBytes:X}-sector-growth"
            : "fixed-tail";

        if (capacity.SourceTextureCount != retailCount)
            failedProofs.Add("capacity/source texture counts match");
        if (tailId > 0x7F)
            failedProofs.Add("assigned tail id fits the seven-bit terrain field");
        if (capacity.WouldRequireTargetEntryGrowth)
        {
            if (capacity.RequiredSectorAlignedEntryGrowthBytes != 0x800)
                failedProofs.Add("entry-growth destination requires exactly +0x800 bytes");
            if (!capacity.SectorRelocationPlanVerified)
                failedProofs.Add($"sector relocation plan verified: {capacity.SectorRelocationFailure}");
        }
        else if (!capacity.CanAppendInsideCurrentSubfile)
        {
            failedProofs.Add("fixed-tail destination can append inside its current level-data subfile");
        }

        NativeTerrainTextureRuntimeControlAudit retailRuntime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
        if (retailRuntime.Complete)
        {
            int[] retailMovableIds = Enumerable.Range(0, retailCount)
                .Except(retailRuntime.ControlledTextureIds)
                .ToArray();
            if (NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                    sourceImagePath,
                    level,
                    retailMovableIds,
                    out NativeTexturePageRelocationOwnershipProofResult? ownership,
                    out _) && ownership != null)
            {
                independentlyProtectedBytes = UnionLength(ownership.Proof.OwnedRanges);
                independentlyReleasedBytes = ownership.ReleasedTargetByteCount;
            }
        }

        NativeTerrainTextureGlobalRepackSyntheticRequest request = new(
            sourceImagePath,
            level,
            [],
            [new NativeTerrainTextureGlobalRepackSyntheticRecord(
                tailId,
                donorLevel.SourceWadEntry,
                donorTextureId,
                MaterialTemplateTextureId)],
            sourceImagePath);
        if (!NativeTerrainTextureGlobalRepackerResearch.TryBuild(
                request,
                out syntheticPlan,
                out string repackFailure) || syntheticPlan == null)
        {
            failure = repackFailure;
        }
        else
        {
            NativeTerrainTextureGlobalRepackResearchPlan proof = syntheticPlan.PackingProof;
            AddFailedProof(failedProofs, proof.ExactIndexedPixelReadbackVerified, "exact indexed-pixel readback");
            AddFailedProof(failedProofs, proof.ExactPaletteReadbackVerified, "exact palette readback");
            AddFailedProof(failedProofs, proof.PixelAliasRelationshipsPreserved, "pixel aliases preserved");
            AddFailedProof(failedProofs, proof.PaletteAliasRelationshipsPreserved, "palette aliases preserved");
            AddFailedProof(failedProofs, proof.LowDetailAliasPreserved, "LQ three-way alias preserved");
            AddFailedProof(failedProofs, proof.ProtectedStoragePreserved, "protected page storage preserved");
            AddFailedProof(failedProofs, proof.FixedDescriptorRowsPreserved, "fixed descriptor rows preserved");
            AddFailedProof(failedProofs, proof.TargetMaterialBitsPreserved, "target material bits preserved");
            AddFailedProof(failedProofs, syntheticPlan.SourceDescriptorOffsetsRemainUnmodified,
                "synthetic source descriptor offsets remain unmodified");
            AddFailedProof(failedProofs, syntheticPlan.SyntheticRowsModeledOnlyInMemory,
                "synthetic rows modeled only in memory");
            AddFailedProof(failedProofs, syntheticPlan.RequiresStructuralComposer,
                "structural composer requirement retained");
            AddFailedProof(failedProofs, syntheticPlan.RequiresDuckStationRuntimeProof,
                "DuckStation runtime-proof boundary retained");
            AddFailedProof(failedProofs, proof.RequiresDuckStationRuntimeProof,
                "packing proof runtime boundary retained");
            AddFailedProof(failedProofs, syntheticPlan.SourceTextureCount == retailCount,
                "synthetic source count matches retail");
            AddFailedProof(failedProofs, syntheticPlan.OutputTextureCount == retailCount + 1,
                "synthetic output count adds exactly one record");
            AddFailedProof(failedProofs,
                syntheticPlan.SyntheticRecords.Count == 1 &&
                syntheticPlan.SyntheticRecords[0].TargetTextureId == tailId,
                "one exact tail record returned");
            AddFailedProof(failedProofs,
                syntheticPlan.SyntheticRecords.Count == 1 &&
                syntheticPlan.SyntheticRecords[0].DonorWadEntry == donorLevel.SourceWadEntry &&
                syntheticPlan.SyntheticRecords[0].DonorTextureId == donorTextureId,
                "tail record retains donor identity");
            AddFailedProof(failedProofs,
                syntheticPlan.SyntheticRecords.Count == 1 &&
                syntheticPlan.SyntheticRecords[0].LowDetailRow.Length == 16 &&
                syntheticPlan.SyntheticRecords[0].HighDetailRow.Length == 168 &&
                Sha256Bytes(syntheticPlan.SyntheticRecords[0].LowDetailRow)
                    .Equals(syntheticPlan.SyntheticRecords[0].LowDetailRowSha256, StringComparison.OrdinalIgnoreCase) &&
                Sha256Bytes(syntheticPlan.SyntheticRecords[0].HighDetailRow)
                    .Equals(syntheticPlan.SyntheticRecords[0].HighDetailRowSha256, StringComparison.OrdinalIgnoreCase),
                "tail record row lengths and hashes verified");

            NativeTerrainTextureRelocationPatch[] returnedPatches = syntheticPlan.TexturePagePatches
                .Concat(syntheticPlan.OriginalDescriptorPatches)
                .OrderBy(patch => patch.WadOffset)
                .ToArray();
            patchProof = VerifyPatchPreimages(sourceImagePath, discLayout, returnedPatches);
            AddFailedProof(failedProofs, patchProof.ByteLengthsValid, "patch byte lengths valid");
            AddFailedProof(failedProofs, patchProof.NonOverlapping, "patches do not overlap");
            AddFailedProof(failedProofs, patchProof.ExactSourcePreimages, "every patch preimage matches retail BIN");
            AddFailedProof(failedProofs,
                proof.Patches.Count == returnedPatches.Length &&
                proof.Patches.OrderBy(patch => patch.WadOffset).Zip(returnedPatches).All(pair =>
                    PatchEqual(pair.First, pair.Second)),
                "packing proof exposes the exact source-applicable patch set");

            success = failedProofs.Count == 0;
            if (!success)
                failure = string.Join("; ", failedProofs);
        }
    }
    catch (Exception ex)
    {
        failure = $"{ex.GetType().Name}: {ex.Message}";
    }
    finally
    {
        timer.Stop();
    }

    NativeTerrainTextureGlobalRepackResearchPlan? packing = syntheticPlan?.PackingProof;
    ProofFlags flags = new(
        packing?.ExactIndexedPixelReadbackVerified ?? false,
        packing?.ExactPaletteReadbackVerified ?? false,
        packing?.PixelAliasRelationshipsPreserved ?? false,
        packing?.PaletteAliasRelationshipsPreserved ?? false,
        packing?.LowDetailAliasPreserved ?? false,
        packing?.ProtectedStoragePreserved ?? false,
        packing?.FixedDescriptorRowsPreserved ?? false,
        packing?.TargetMaterialBitsPreserved ?? false,
        syntheticPlan?.SourceDescriptorOffsetsRemainUnmodified ?? false,
        syntheticPlan?.SyntheticRowsModeledOnlyInMemory ?? false,
        patchProof.ExactSourcePreimages,
        patchProof.NonOverlapping,
        syntheticPlan?.RequiresStructuralComposer ?? false,
        syntheticPlan?.RequiresDuckStationRuntimeProof ?? false);
    AuditRow row = new(
        level.Key,
        level.DisplayName,
        level.LevelId,
        level.SourceWadEntry,
        level.DisplayName.Contains("Flight", StringComparison.OrdinalIgnoreCase),
        retailCount,
        tailId,
        donorLevel.Key,
        donorLevel.DisplayName,
        donorLevel.SourceWadEntry,
        donorTextureId,
        capacityMode,
        capacity?.VerifiedLevelDataZeroTailBytes ?? -1,
        capacity?.AdditionalBytesNeededForOneRecord ?? -1,
        capacity?.RequiredSectorAlignedEntryGrowthBytes ?? -1,
        capacity?.SectorRelocationPlanVerified ?? false,
        success,
        failure,
        failedProofs,
        packing?.MovableTextureIds.Count ?? 0,
        packing?.FixedRuntimeControlledTextureIds.Count ?? 0,
        packing?.ProtectedByteCount ?? independentlyProtectedBytes,
        packing?.ReleasedTerrainByteCount ?? independentlyReleasedBytes,
        packing?.AllocatedByteCount ?? -1,
        packing?.FreeByteCount ?? -1,
        syntheticPlan?.TexturePagePatches.Count ?? 0,
        syntheticPlan?.OriginalDescriptorPatches.Count ?? 0,
        (syntheticPlan?.TexturePagePatches.Count ?? 0) +
            (syntheticPlan?.OriginalDescriptorPatches.Count ?? 0),
        packing?.ChangedTexturePageByteCount ?? 0,
        packing?.ChangedDescriptorByteCount ?? 0,
        packing?.PackingStrategy ?? "",
        flags,
        patchProof.PatchPreimageSha256,
        patchProof.PatchOutputSha256,
        timer.ElapsedMilliseconds);
    rows.Add(row);
    Console.WriteLine(
        $"[{levelIndex + 1,2}/{levels.Length}] {level.DisplayName,-18} {capacityMode,-24} " +
        $"T{tailId,-3} {(success ? "PASS" : "FAIL")} " +
        $"free={row.FreeByteCount,7:N0} patches={row.TotalPatchCount,5:N0} {timer.Elapsed.TotalSeconds,6:N1}s" +
        (success ? "" : $" :: {failure}"));

    syntheticPlan = null;
    if ((levelIndex + 1) % 5 == 0)
        GC.Collect();
}

FileInfo sourceInfoAfter = new(sourceImagePath);
string sourceShaAfter = Sha256File(sourceImagePath);
long sourceLengthAfter = FileLength(sourceImagePath);
bool sourceUnchanged =
    sourceLengthBefore == sourceLengthAfter &&
    sourceWriteTimeBefore == sourceInfoAfter.LastWriteTimeUtc &&
    sourceShaBefore.Equals(sourceShaAfter, StringComparison.OrdinalIgnoreCase);
int succeeded = rows.Count(row => row.Success);
int failed = rows.Count - succeeded;
int fixedTail = rows.Count(row => row.CapacityMode == "fixed-tail");
int sectorGrowth = rows.Count(row => row.CapacityMode == "+0x800-sector-growth");

AuditSummary summary = new(
    rows.Count,
    succeeded,
    failed,
    fixedTail,
    sectorGrowth,
    rows.Count(row => row.IsFlight),
    sourceUnchanged,
    rows.Where(row => row.FreeByteCount >= 0).Select(row => row.FreeByteCount).DefaultIfEmpty(0).Min(),
    rows.Where(row => row.FreeByteCount >= 0).Select(row => row.FreeByteCount).DefaultIfEmpty(0).Max(),
    rows.Sum(row => row.ElapsedMilliseconds));

JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = failed == 0 && sourceUnchanged,
    Scope = onlySet.Count == 0
        ? "All 35 mapped retail levels, including five flights; one in-memory appended private terrain-texture record per level."
        : $"Focused filtered audit of {levels.Length} mapped retail level(s): {string.Join(", ", levels.Select(level => level.DisplayName))}.",
    Source = new
    {
        Path = sourceImagePath,
        LengthBefore = sourceLengthBefore,
        LengthAfter = sourceLengthAfter,
        Sha256Before = sourceShaBefore,
        Sha256After = sourceShaAfter,
        LastWriteTimeUtcBefore = sourceWriteTimeBefore,
        LastWriteTimeUtcAfter = sourceInfoAfter.LastWriteTimeUtc,
        Unchanged = sourceUnchanged
    },
    Donor = new
    {
        LevelKey = donorLevel.Key,
        LevelName = donorLevel.DisplayName,
        WadEntry = donorLevel.SourceWadEntry,
        TextureId = donorTextureId,
        RequestedTextureId = RequestedDonorTextureId,
        UsedRequestedTexture = donorTextureId == RequestedDonorTextureId
    },
    CapacityClassification = new
    {
        FixedTail = "Existing capacity API proves the 184-byte row fits the current level-data subfile's verified zero tail.",
        SectorGrowth = "Existing capacity API proves a +0x800 WAD-entry/ISO relocation plan for the destination."
    },
    Summary = summary,
    Levels = rows
}, jsonOptions));
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(rows, summary, donorLevel, donorTextureId, sourceShaBefore));

Console.WriteLine();
Console.WriteLine($"Synthetic global-repack audit: {succeeded}/{rows.Count} passed; {failed} failed.");
Console.WriteLine($"Capacity: {fixedTail} fixed-tail; {sectorGrowth} +0x800 sector-growth.");
Console.WriteLine($"Retail BIN unchanged: {sourceUnchanged} ({sourceShaBefore}).");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");
if (failed > 0)
{
    foreach (AuditRow failedRow in rows.Where(row => !row.Success))
        Console.Error.WriteLine($"FAIL {failedRow.LevelName}: {failedRow.Failure}");
}

Environment.ExitCode = failed == 0 && sourceUnchanged ? 0 : 1;

static int ResolveDonorTextureId(IReadOnlyList<TerrainTextureSlot> slots)
{
    TerrainTextureSlot? requested = slots.FirstOrDefault(slot =>
        slot.TextureId == RequestedDonorTextureId &&
        slot.HasNormalDescriptors &&
        slot.HasCloseDescriptors);
    return requested?.TextureId ?? slots
        .Where(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors)
        .OrderBy(slot => slot.TextureId)
        .Select(slot => slot.TextureId)
        .FirstOrDefault(-1);
}

static PatchProof VerifyPatchPreimages(
    string sourceImagePath,
    DiscLayout layout,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> patches)
{
    bool lengthsValid = patches.All(patch =>
        patch.ByteLength > 0 &&
        patch.Before.Length == patch.ByteLength &&
        patch.After.Length == patch.ByteLength &&
        !patch.Before.SequenceEqual(patch.After));
    NativeTerrainTextureRelocationPatch[] ordered = patches.OrderBy(patch => patch.WadOffset).ToArray();
    bool nonOverlapping = ordered.Zip(ordered.Skip(1)).All(pair =>
        pair.First.WadOffset + pair.First.ByteLength <= pair.Second.WadOffset);
    bool exactPreimages = lengthsValid;
    using IncrementalHash beforeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    using IncrementalHash afterHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    using FileStream source = File.OpenRead(sourceImagePath);
    foreach (NativeTerrainTextureRelocationPatch patch in ordered)
    {
        byte[] actual = ReadDiscFileBytes(source, layout, WadLba, patch.WadOffset, patch.ByteLength);
        exactPreimages &= actual.SequenceEqual(patch.Before);
        AppendPatchHash(beforeHash, patch.WadOffset, patch.Before);
        AppendPatchHash(afterHash, patch.WadOffset, patch.After);
    }
    return new PatchProof(
        lengthsValid,
        nonOverlapping,
        exactPreimages,
        Convert.ToHexString(beforeHash.GetHashAndReset()),
        Convert.ToHexString(afterHash.GetHashAndReset()));
}

static void AppendPatchHash(IncrementalHash hash, long offset, byte[] bytes)
{
    hash.AppendData(BitConverter.GetBytes(offset));
    hash.AppendData(BitConverter.GetBytes(bytes.Length));
    hash.AppendData(bytes);
}

static bool PatchEqual(
    NativeTerrainTextureRelocationPatch left,
    NativeTerrainTextureRelocationPatch right) =>
    left.WadOffset == right.WadOffset &&
    left.ByteLength == right.ByteLength &&
    left.Kind == right.Kind &&
    left.Before.SequenceEqual(right.Before) &&
    left.After.SequenceEqual(right.After);

static void AddFailedProof(List<string> failures, bool passed, string name)
{
    if (!passed)
        failures.Add(name);
}

static string BuildMarkdown(
    IReadOnlyList<AuditRow> rows,
    AuditSummary summary,
    LevelDefinition donor,
    int donorTextureId,
    string sourceSha256)
{
    StringBuilder text = new();
    text.AppendLine("# All-level synthetic global terrain-texture repack audit");
    text.AppendLine();
    text.AppendLine("## Result");
    text.AppendLine();
    text.AppendLine($"- **{summary.SuccessfulLevels}/{summary.TotalLevels}** levels passed the exact static synthetic-repack proof; **{summary.FailedLevels}** failed.");
    text.AppendLine($"- Capacity classification: **{summary.FixedTailLevels} fixed-tail** and **{summary.SectorGrowthLevels} +0x800 sector-growth** destinations.");
    text.AppendLine($"- The immutable donor was **{donor.DisplayName} T{donorTextureId}**.");
    text.AppendLine($"- The retail BIN remained byte-identical: **{summary.SourceImageUnchanged}** (`{sourceSha256}`).");
    text.AppendLine("- This is exact static packing/preimage proof only. It does not claim DuckStation runtime proof or shipping promotion.");
    text.AppendLine();
    text.AppendLine("## Per-level proof");
    text.AppendLine();
    text.AppendLine("| Level | Flight | Retail -> tail | Capacity | Result | Movable / fixed | Protected | Released | Allocated | Free | Page / row patches | Proof flags | Failure |");
    text.AppendLine("|---|---|---:|---|---|---:|---:|---:|---:|---:|---:|---|---|");
    foreach (AuditRow row in rows)
    {
        text.AppendLine(
            $"| {Escape(row.LevelName)} | {(row.IsFlight ? "yes" : "no")} | {row.RetailTextureCount} -> T{row.AssignedTailTextureId} | " +
            $"{Escape(row.CapacityMode)} | {(row.Success ? "PASS" : "FAIL")} | {row.MovableRecordCount} / {row.FixedRecordCount} | " +
            $"{FormatBytes(row.ProtectedByteCount)} | {FormatBytes(row.ReleasedByteCount)} | " +
            $"{FormatBytes(row.AllocatedByteCount)} | {FormatBytes(row.FreeByteCount)} | " +
            $"{row.TexturePagePatchCount:N0} / {row.OriginalDescriptorPatchCount:N0} | {FormatFlags(row.ProofFlags)} | {Escape(row.Failure)} |");
    }
    if (summary.FailedLevels > 0)
    {
        text.AppendLine();
        text.AppendLine("## Exact failures");
        text.AppendLine();
        foreach (AuditRow row in rows.Where(row => !row.Success))
            text.AppendLine($"- **{row.LevelName}:** {row.Failure}");
    }
    return text.ToString();
}

static string FormatFlags(ProofFlags flags) =>
    flags.ExactIndexedPixels && flags.ExactPalettes &&
    flags.PixelAliases && flags.PaletteAliases &&
    flags.LowDetailAlias && flags.ProtectedStorage &&
    flags.FixedRows && flags.MaterialBits &&
    flags.SourceOffsetsUntouched && flags.SyntheticRowsInMemory &&
    flags.PatchPreimages && flags.PatchesNonOverlapping &&
    flags.StructuralComposerRequired && flags.RuntimeProofRequired
        ? "all"
        : "incomplete";

static string FormatBytes(int value) => value >= 0 ? value.ToString("N0") : "n/a";

static int UnionLength(IReadOnlyList<NativeTexturePageOwnedRange> ranges)
{
    if (ranges.Count == 0)
        return 0;
    (long Start, long End)[] ordered = ranges
        .Select(range => (range.Offset, checked(range.Offset + range.Length)))
        .OrderBy(range => range.Offset)
        .ToArray();
    long total = 0;
    long start = ordered[0].Start;
    long end = ordered[0].End;
    foreach ((long nextStart, long nextEnd) in ordered.Skip(1))
    {
        if (nextStart <= end)
        {
            end = Math.Max(end, nextEnd);
            continue;
        }
        total = checked(total + end - start);
        start = nextStart;
        end = nextEnd;
    }
    total = checked(total + end - start);
    return checked((int)total);
}

static string Escape(string value) =>
    value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

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

static string[] ParseOnlyLevelKeys(IReadOnlyList<string> arguments)
{
    string? raw = arguments.FirstOrDefault(argument =>
        argument.StartsWith("--only=", StringComparison.OrdinalIgnoreCase));
    if (raw == null)
        return [];
    return raw[(raw.IndexOf('=') + 1)..]
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(LevelCatalog.NormalizeKey)
        .Where(key => key.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static string ParseOutputSuffix(IReadOnlyList<string> arguments)
{
    string? raw = arguments.FirstOrDefault(argument =>
        argument.StartsWith("--output-suffix=", StringComparison.OrdinalIgnoreCase));
    if (raw == null)
        return "";
    string requested = raw[(raw.IndexOf('=') + 1)..].Trim();
    string safe = new(requested
        .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
            ? character
            : '-')
        .ToArray());
    safe = safe.Trim('-');
    Assert(safe.Length > 0, "--output-suffix must contain at least one letter or digit.");
    return safe;
}

static DiscLayout DetectDiscLayout(string path)
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
            return new DiscLayout(sectorSize, userOffset);
    }
    throw new InvalidOperationException("Could not detect the retail disc layout.");
}

static byte[] ReadDiscFileBytes(
    FileStream stream,
    DiscLayout layout,
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
        int sector = checked(fileLba + (int)(absolute / 2048));
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        stream.Position = ((long)sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
        int read = stream.Read(result, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException($"Could not read WAD offset 0x{fileOffset:X} for patch proof.");
        written += read;
        remaining -= read;
        absolute += read;
    }
    return result;
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

static string Sha256Bytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record DiscLayout(int SectorSize, int UserOffset);

internal sealed record PatchProof(
    bool ByteLengthsValid,
    bool NonOverlapping,
    bool ExactSourcePreimages,
    string PatchPreimageSha256,
    string PatchOutputSha256)
{
    public static PatchProof Empty { get; } = new(false, false, false, "", "");
}

internal sealed record ProofFlags(
    bool ExactIndexedPixels,
    bool ExactPalettes,
    bool PixelAliases,
    bool PaletteAliases,
    bool LowDetailAlias,
    bool ProtectedStorage,
    bool FixedRows,
    bool MaterialBits,
    bool SourceOffsetsUntouched,
    bool SyntheticRowsInMemory,
    bool PatchPreimages,
    bool PatchesNonOverlapping,
    bool StructuralComposerRequired,
    bool RuntimeProofRequired);

internal sealed record AuditRow(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    bool IsFlight,
    int RetailTextureCount,
    int AssignedTailTextureId,
    string DonorLevelKey,
    string DonorLevelName,
    int DonorWadEntry,
    int DonorTextureId,
    string CapacityMode,
    int VerifiedZeroTailBytes,
    int AdditionalRecordBytesNeeded,
    int RequiredSectorGrowthBytes,
    bool SectorRelocationVerified,
    bool Success,
    string Failure,
    IReadOnlyList<string> FailedProofs,
    int MovableRecordCount,
    int FixedRecordCount,
    int ProtectedByteCount,
    int ReleasedByteCount,
    int AllocatedByteCount,
    int FreeByteCount,
    int TexturePagePatchCount,
    int OriginalDescriptorPatchCount,
    int TotalPatchCount,
    int ChangedTexturePageByteCount,
    int ChangedDescriptorByteCount,
    string PackingStrategy,
    ProofFlags ProofFlags,
    string PatchPreimageSha256,
    string PatchOutputSha256,
    long ElapsedMilliseconds);

internal sealed record AuditSummary(
    int TotalLevels,
    int SuccessfulLevels,
    int FailedLevels,
    int FixedTailLevels,
    int SectorGrowthLevels,
    int FlightLevels,
    bool SourceImageUnchanged,
    int MinimumFreeBytes,
    int MaximumFreeBytes,
    long TotalElapsedMilliseconds);
