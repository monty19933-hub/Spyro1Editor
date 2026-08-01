using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int WadLba = 37;
const int TexturePagesSubfileIndex = 0;
const int ModelSubfileIndex = 1;
const int RowBytes = 1024;
const int VramRows = 512;
const int AddressableBytes = RowBytes * VramRows;
const int LqRecordBytes = 16;
const int HqRecordBytes = 168;
const int LqPaletteWidthBytes = 32;
const int LqPaletteRows = 16;
const int HqPaletteBytes = 512;
const int FullVramTextureByteX = 1024;

int[][] matrices =
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

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault());
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "all-donor-texture-feasibility");
string jsonPath = Path.Combine(outputRoot, "all-donor-texture-feasibility-audit.json");
string markdownPath = Path.Combine(outputRoot, "all-donor-texture-feasibility-audit.md");
Directory.CreateDirectory(outputRoot);

Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = new FileInfo(sourceImagePath).Length;
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(sourceImagePath);
DiscLayout layout = DetectDiscLayout(sourceImagePath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] levels = LevelRealmCatalog
    .OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    .ToArray();
Assert(levels.Length == 35, $"Expected all 35 mapped retail levels, got {levels.Length}.");

Console.WriteLine("Decoding every runtime-initialized retail terrain record...");
List<DonorRecordRow> donorRows = [];
List<SourceLevelOutlierRow> sourceLevelRows = [];
Dictionary<string, DecodedLevelData> decodedLevels = new(StringComparer.OrdinalIgnoreCase);
foreach ((LevelDefinition level, int levelIndex) in levels.Select((value, index) => (value, index)))
{
    AssetData asset = LoadAsset(sourceImagePath, layout, level);
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
    Assert(runtime.Complete,
        $"{level.DisplayName}: runtime-control scan failed: {runtime.SafetyBlockers.FirstOrDefault()}");
    NativeTerrainTextureInitialStateResult initialized =
        NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtime, asset.Model);
    Assert(initialized.Complete,
        $"{level.DisplayName}: load-state initialization failed: {initialized.SafetyBlockers.FirstOrDefault()}");
    TextureIndex textureIndex = Decode(initialized.InitializedTextureData);
    Assert(textureIndex.TextureCount == runtime.TextureCount,
        $"{level.DisplayName}: initialized table count does not match the runtime audit.");

    DonorRecordRow[] levelDonorRows = textureIndex.Records
        .Select(record => AnalyzeRecord(level, record, asset.TexturePageLength))
        .ToArray();
    donorRows.AddRange(levelDonorRows);
    sourceLevelRows.Add(AnalyzeSourceLevel(level, textureIndex, asset.TexturePageLength));
    decodedLevels[level.Key] = new DecodedLevelData(textureIndex, asset.TexturePages);
    Console.WriteLine(
        $"[{levelIndex + 1,2}/{levels.Length}] {level.DisplayName,-18} " +
        $"{textureIndex.TextureCount,3} records, max {donorRows.Where(row => row.LevelKey == level.Key).Max(row => row.UniquePhysicalBytes),6:N0} B");
}

DonorRecordRow[] sortedByFootprint = donorRows
    .OrderByDescending(row => row.UniquePhysicalBytes)
    .ThenBy(row => row.LevelName, StringComparer.Ordinal)
    .ThenBy(row => row.TextureId)
    .ToArray();
DonorRecordRow worstDonor = sortedByFootprint[0];
DonorRecordRow gnastysWorld17 = donorRows.SingleOrDefault(row =>
    LevelCatalog.NormalizeKey(row.LevelKey) == "gnastysworld" && row.TextureId == 17)
    ?? throw new InvalidDataException("Gnasty's World T17 was not decoded.");

Console.WriteLine();
Console.WriteLine("Computing exact anchor-aware destination slack (this is the slower ownership-closure pass)...");
ConcurrentBag<DestinationCapacityRow> destinationBag = [];
ParallelOptions parallel = new() { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) };
await Parallel.ForEachAsync(levels, parallel, (level, _) =>
{
    Stopwatch timer = Stopwatch.StartNew();
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
    if (!runtime.Complete)
        throw new InvalidOperationException(
            $"{level.DisplayName}: runtime-control scan failed: {runtime.SafetyBlockers.FirstOrDefault()}");
    int[] movableIds = Enumerable.Range(0, runtime.TextureCount)
        .Except(runtime.ControlledTextureIds)
        .ToArray();
    if (!NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
            sourceImagePath,
            level,
            movableIds,
            out NativeTexturePageRelocationOwnershipProofResult? proof,
            out string failure) || proof == null)
    {
        throw new InvalidOperationException($"{level.DisplayName}: {failure}");
    }

    int protectedBytes = UnionLength(proof.Proof.OwnedRanges);
    int releasedBytes = proof.ReleasedTargetByteCount;
    int slackBeforeDonor = checked(AddressableBytes - protectedBytes - releasedBytes);
    NativeTexturePageOwnershipReport levelOwnership = proof.LevelOwnership;
    int deadUnreferencedCandidateSlack = checked(AddressableBytes - levelOwnership.KnownOwnedByteCount);
    Assert(deadUnreferencedCandidateSlack ==
        checked(slackBeforeDonor + levelOwnership.UnknownNonZeroByteCount),
        $"{level.DisplayName}: known-owned candidate slack does not reconcile with the conservative mask.");
    int worstResidual = checked(slackBeforeDonor - worstDonor.UniquePhysicalBytes);
    int gnastysWorld17Residual = checked(slackBeforeDonor - gnastysWorld17.UniquePhysicalBytes);
    int impossibleDonorCount = donorRows.Count(row => row.UniquePhysicalBytes > slackBeforeDonor);
    int deadCandidateWorstResidual = checked(deadUnreferencedCandidateSlack - worstDonor.UniquePhysicalBytes);
    int deadCandidateImpossibleDonorCount = donorRows.Count(row =>
        row.UniquePhysicalBytes > deadUnreferencedCandidateSlack);
    TargetReplacementCapacity[] replacementTargets = proof.TargetIsolations
        .OrderBy(item => item.TargetTextureId)
        .Select(item => new TargetReplacementCapacity(
            item.TargetTextureId,
            item.TargetOwnedByteCount,
            item.TargetExclusiveByteCount,
            item.AnyDecodedConsumerOverlapByteCount))
        .ToArray();
    DecodedLevelData decoded = decodedLevels[level.Key];
    ExactContentDuplicatePotential duplicatePotential = AnalyzeDuplicatePotential(
        level,
        decoded.TextureIndex,
        decoded.TexturePages,
        movableIds,
        replacementTargets);
    timer.Stop();
    destinationBag.Add(new DestinationCapacityRow(
        level.Key,
        level.DisplayName,
        level.LevelId,
        level.SourceWadEntry,
        IsFlight(level),
        runtime.TextureCount,
        runtime.ControlledTextureIds.Count,
        movableIds.Length,
        protectedBytes,
        releasedBytes,
        slackBeforeDonor,
        levelOwnership.KnownOwnedByteCount,
        levelOwnership.UnknownNonZeroByteCount,
        levelOwnership.UnknownNonZeroRegionCount,
        levelOwnership.UnknownUnprovenZeroByteCount,
        levelOwnership.AllConsumerClosureComplete,
        deadUnreferencedCandidateSlack,
        deadCandidateWorstResidual,
        deadCandidateImpossibleDonorCount,
        worstDonor.UniquePhysicalBytes,
        worstResidual,
        gnastysWorld17.UniquePhysicalBytes,
        gnastysWorld17Residual,
        impossibleDonorCount,
        impossibleDonorCount == 0,
        replacementTargets,
        duplicatePotential,
        timer.ElapsedMilliseconds));
    Console.WriteLine(
        $"{level.DisplayName,-18} slack={slackBeforeDonor,7:N0} B " +
        $"worst residual={worstResidual,7:N0} B GW17 residual={gnastysWorld17Residual,7:N0} B " +
        $"{timer.Elapsed.TotalSeconds,5:N1}s");
    return ValueTask.CompletedTask;
});

DestinationCapacityRow[] destinations = LevelRealmCatalog.OrderLevels(
        levels.Where(level => destinationBag.Any(row => row.LevelKey == level.Key)))
    .Select(level => destinationBag.Single(row => row.LevelKey == level.Key))
    .ToArray();
Assert(destinations.Length == 35, "Destination proof did not return all 35 levels.");

int[] footprints = donorRows.Select(row => row.UniquePhysicalBytes).Order().ToArray();
DonorDistribution distribution = new(
    donorRows.Count,
    footprints[0],
    Percentile(footprints, 0.50),
    Percentile(footprints, 0.90),
    Percentile(footprints, 0.95),
    Percentile(footprints, 0.99),
    footprints[^1],
    donorRows.Average(row => row.UniquePhysicalBytes),
    donorRows.Count(row => row.CrossRoleAliasBytes > 0),
    donorRows.Sum(row => row.CrossRoleAliasBytes),
    donorRows.Count(row => row.WrappedHqPaletteDescriptorCount > 0),
    donorRows.Sum(row => row.WrappedHqPaletteDescriptorCount),
    donorRows.Count(row => !row.CompleteNativeGrammar),
    donorRows.Count(row => !row.AllDescriptorsReadable),
    donorRows.Count(row => row.PartialSameFormatPixelOverlapPairs > 0));

DestinationCapacityRow minimumSlackDestination = destinations
    .OrderBy(row => row.AnchorAwareSlackBeforeDonorBytes)
    .ThenBy(row => row.LevelName, StringComparer.Ordinal)
    .First();
long impossiblePairs = destinations.Sum(destination =>
    donorRows.LongCount(donor => donor.UniquePhysicalBytes > destination.AnchorAwareSlackBeforeDonorBytes));
bool everyPairFitsByBytes = impossiblePairs == 0;
long deadCandidateImpossiblePairs = destinations.Sum(destination =>
    donorRows.LongCount(donor => donor.UniquePhysicalBytes > destination.DeadUnreferencedCandidateSlackBytes));
DestinationCapacityRow tightestDeadCandidateDestination = destinations
    .OrderBy(row => row.DeadUnreferencedCandidateSlackBytes)
    .ThenBy(row => row.LevelName, StringComparer.Ordinal)
    .First();
DonorRecordRow[] largeDonors = donorRows
    .Where(row => row.UniquePhysicalBytes > minimumSlackDestination.AnchorAwareSlackBeforeDonorBytes)
    .OrderByDescending(row => row.UniquePhysicalBytes)
    .ThenBy(row => row.LevelName, StringComparer.Ordinal)
    .ThenBy(row => row.TextureId)
    .ToArray();
ImpossiblePairReplacementRow[] impossiblePairReplacements = destinations
    .SelectMany(destination => donorRows
        .Where(donor => donor.UniquePhysicalBytes > destination.AnchorAwareSlackBeforeDonorBytes)
        .Select(donor => BuildReplacementRow(destination, donor)))
    .OrderBy(row => row.DestinationName, StringComparer.Ordinal)
    .ThenByDescending(row => row.DonorBytes)
    .ThenBy(row => row.DonorLevelName, StringComparer.Ordinal)
    .ThenBy(row => row.DonorTextureId)
    .ToArray();
Assert(largeDonors.Length == 47,
    $"Expected the observed 47 donors above the tightest destination slack, got {largeDonors.Length}.");
Assert(impossiblePairReplacements.LongLength == impossiblePairs,
    "The replacement matrix count does not match the impossible append-pair count.");
string[] globalCaveats =
[
    "A positive residual proves physical byte capacity for one appended private record; it does not by itself prove a legal descriptor placement in fragmented storage.",
    "HQ CLUTs beginning after packed x=512 are valid retail linear spans but must be normalized into a target-encodable row during relocation.",
    "Pixel/palette cross-role aliases must move as one physical component. Their bytes are counted once here; an allocator must preserve the exact overlap rather than duplicate either role.",
    "Runtime-controlled source rows are analyzed after the game's native load-state animation/scrolling initialization, matching the donor state used by the global repacker.",
    "This audit covers one imported private record. Multiple simultaneous imports consume the union of every donor component and require a separate batch placement proof.",
    "Whole-record replacement estimates reclaim only bytes proven exclusive to that target record. They account for external and cross-record aliases, but replacement changes every face using that shared target id and is not selected-face independence.",
    "Exact-content duplicate potential is optimistic research data. Coalescing equal bytes changes non-alias relationships and requires copy-on-write plus runtime proof before it could be treated as safe capacity."
];
DeadUnreferencedStorageResearchBoundary deadStorageBoundary = new(
    false,
    deadCandidateImpossiblePairs,
    deadCandidateImpossiblePairs == 0,
    tightestDeadCandidateDestination.LevelKey,
    tightestDeadCandidateDestination.LevelName,
    tightestDeadCandidateDestination.DeadUnreferencedCandidateSlackBytes,
    tightestDeadCandidateDestination.DeadCandidateWorstDonorResidualBytes,
    [
        "AllConsumerClosureComplete currently closes decoded descriptor roots, but its other-runtime scope is partly a source-audit assertion rather than a machine-checked control/data-flow proof over every executable path.",
        "The loader uploads the complete 0x80000-byte source page. An unowned nonzero byte can still be read by an immediate tpage/CLUT constant, a dynamically computed descriptor, a conditional actor/effect path, or a LoadImage/MoveImage source rectangle absent from decoded tables.",
        "A no-hit search for shared texture globals and GPU copy calls does not prove that every indirect call, overlay relocation, script opcode, and computed coordinate is bounded to the enumerated roots.",
        "UnknownNonZero has no per-range provenance yet. Padding, duplicated art, stale asset residue, and hidden conditional art are observationally indistinguishable without producer/consumer evidence.",
        "Gameplay traces are finite evidence, not proof of every state. Death/reload, pause capture, portals, cutscenes, particles, damage states, HUD changes, and conditional resident actors can exercise otherwise dormant texture reads."
    ],
    [
        "Bind the proof to the supported retail BIN and overlay hashes and reject all stale fingerprints.",
        "Enumerate every GPU texture-coordinate producer and every LoadImage/StoreImage/MoveImage source rectangle in the executable plus all level overlays, including indirect calls and immediate/computed tpage/CLUT values.",
        "Prove every reachable source-bound value resolves to terrain records, resident/PETE/dragon face tables, particles, LevelSceneHeader Tiledefs, or a separately retained runtime scratch range.",
        "Classify every UnknownNonZero range by archive provenance and prove it is never read before overwrite; retain any range whose producer or lifetime is unresolved.",
        "Verify runtime animation, scrolling, framebuffer capture/restore, portal transitions, and scratch uploads cannot turn an initially unowned source byte into a later input.",
        "Keep the known-owned mask immutable, require exact readback/alias proofs, and promote only after an adversarial emulator test of the candidate mask passes."
    ],
    [
        "Create a research-only Gnorc Cove selected-private candidate using Dark Hollow T0 (22,528 bytes), because it is the largest donor and fails the conservative mask by 7,213 bytes but leaves 48,004 bytes under the known-owned candidate mask.",
        "Force the allocator to place at least the 7,213-byte deficit inside current UnknownNonZero ranges; report exact overwritten ranges and preimage hashes instead of merely accepting any easier zero-space placement.",
        "Create a second poison candidate that changes every proposed dead-unreferenced nonzero range to deterministic high-contrast/index-disruptive bytes while preserving every known-owned byte exactly.",
        "Cold boot and test entry, all nearby terrain at normal/close/low detail, Spyro/HUD, particles, enemies, damage/death, pause/resume, gem and key UI changes, portal/balloon transitions, reload, leave/re-enter, and persistence.",
        "Capture GPU command/VRAM sampling traces when possible and assert every source texture/CLUT or copy-source byte belongs to KnownOwned or to an explicitly classified runtime scratch range.",
        "Do not promote on visual success alone; require the static producer proof, exact final-BIN readback, trace evidence, and repeated DuckStation proof on a fresh boot."
    ]);

AuditDocument report = new(
    DateTimeOffset.UtcNow,
    new SourceProof(sourceImagePath, sourceLengthBefore, sourceShaBefore, layout.SectorSize, layout.UserOffset),
    new ScopeProof(35, donorRows.Count, 35L * donorRows.Count, "all 35 retail levels as both source and destination; one appended private record"),
    distribution,
    worstDonor,
    minimumSlackDestination,
    impossiblePairs,
    everyPairFitsByBytes,
    deadCandidateImpossiblePairs,
    tightestDeadCandidateDestination,
    deadStorageBoundary,
    largeDonors,
    impossiblePairReplacements,
    sortedByFootprint.Take(25).ToArray(),
    donorRows.OrderBy(row => row.LevelName, StringComparer.Ordinal).ThenBy(row => row.TextureId).ToArray(),
    sourceLevelRows.OrderBy(row => row.LevelName, StringComparer.Ordinal).ToArray(),
    destinations,
    globalCaveats,
    true,
    true);

await File.WriteAllTextAsync(
    jsonPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report));

Assert(new FileInfo(sourceImagePath).Length == sourceLengthBefore, "Source BIN length changed during read-only audit.");
Assert(File.GetLastWriteTimeUtc(sourceImagePath) == sourceWriteBefore, "Source BIN write time changed during read-only audit.");
Assert(string.Equals(Sha256File(sourceImagePath), sourceShaBefore, StringComparison.OrdinalIgnoreCase),
    "Source BIN hash changed during read-only audit.");

Console.WriteLine();
Console.WriteLine($"Donor records: {donorRows.Count:N0}");
Console.WriteLine(
    $"Footprint range: {distribution.MinimumBytes:N0}..{distribution.MaximumBytes:N0} B; " +
    $"worst {worstDonor.LevelName} T{worstDonor.TextureId}");
Console.WriteLine(
    $"Tightest destination: {minimumSlackDestination.LevelName}, " +
    $"{minimumSlackDestination.AnchorAwareSlackBeforeDonorBytes:N0} B before donor; " +
    $"{minimumSlackDestination.WorstDonorResidualBytes:N0} B after worst donor.");
Console.WriteLine($"Byte-capacity-impossible source/destination pairs: {impossiblePairs:N0}");
Console.WriteLine(
    $"Research known-owned mask: {deadCandidateImpossiblePairs:N0} impossible pair(s); " +
    $"tightest {tightestDeadCandidateDestination.LevelName} " +
    $"{tightestDeadCandidateDestination.DeadUnreferencedCandidateSlackBytes:N0} B slack.");
Console.WriteLine($"Markdown: {markdownPath}");
Console.WriteLine($"JSON: {jsonPath}");

DonorRecordRow AnalyzeRecord(LevelDefinition level, TextureRecord record, int texturePageLength)
{
    Descriptor[] descriptors = EnumerateDescriptors(record).ToArray();
    HashSet<int> pixelUnion = [];
    HashSet<int> paletteUnion = [];
    List<HashSet<int>> pixelsByDescriptor = [];
    int descriptorReferenceBytes = 0;
    int wrappedHqPalettes = 0;
    int maxHqPaletteX = -1;
    int sourcePaletteOutliers = 0;
    int unreadableDescriptors = 0;
    int invalidSides = 0;
    List<string> outliers = [];

    foreach (Descriptor descriptor in descriptors)
    {
        HashSet<int> pixels = EnumeratePixelOffsets(descriptor).ToHashSet();
        HashSet<int> palette = EnumeratePaletteOffsets(descriptor).ToHashSet();
        pixelsByDescriptor.Add(pixels);
        descriptorReferenceBytes = checked(descriptorReferenceBytes + pixels.Count + palette.Count);
        pixelUnion.UnionWith(pixels);
        paletteUnion.UnionWith(palette);
        if (pixels.Any(offset => offset < 0 || offset >= texturePageLength) ||
            palette.Any(offset => offset < 0 || offset >= texturePageLength))
        {
            unreadableDescriptors++;
        }
        if (descriptor.Format == TextureFormat.Hq8)
        {
            int x = checked((int)(descriptor.PaletteStart % RowBytes));
            maxHqPaletteX = Math.Max(maxHqPaletteX, x);
            if (x > 512)
                wrappedHqPalettes++;
            if (!IsSourcePaletteEncodable(descriptor))
                sourcePaletteOutliers++;
            if (descriptor.Side is not (16 or 32))
                invalidSides++;
        }
        else if (!IsSourcePaletteEncodable(descriptor) || descriptor.Side != 32)
        {
            sourcePaletteOutliers++;
            if (descriptor.Side != 32)
                invalidSides++;
        }
    }

    HashSet<int> physicalUnion = pixelUnion.ToHashSet();
    physicalUnion.UnionWith(paletteUnion);
    int crossRole = pixelUnion.Intersect(paletteUnion).Count();
    int partialSameFormatPixelOverlapPairs = 0;
    for (int first = 0; first < descriptors.Length; first++)
    {
        for (int second = first + 1; second < descriptors.Length; second++)
        {
            if (descriptors[first].Format != descriptors[second].Format)
                continue;
            HashSet<int> left = pixelsByDescriptor[first];
            HashSet<int> right = pixelsByDescriptor[second];
            if (left.Overlaps(right) && !left.IsSupersetOf(right) && !right.IsSupersetOf(left))
                partialSameFormatPixelOverlapPairs++;
        }
    }

    bool lqAlias = record.Low[0].Raw.SequenceEqual(record.Low[1].Raw) &&
        record.Low[0].Raw.SequenceEqual(record.Leading.Raw);
    bool completeGrammar = lqAlias &&
        record.Low.All(descriptor => descriptor.Format == TextureFormat.Lq4 && descriptor.Side == 32) &&
        record.Leading.Format == TextureFormat.Lq4 && record.Leading.Side == 32 &&
        record.Normal.Count == 4 && record.Normal.All(descriptor =>
            descriptor.Format == TextureFormat.Hq8 && descriptor.Side == 32) &&
        record.Close.Count == 16 && record.Close.All(descriptor =>
            descriptor.Format == TextureFormat.Hq8 && descriptor.Side is 16 or 32) &&
        invalidSides == 0;
    if (!completeGrammar)
        outliers.Add("outside-complete-native-grammar");
    if (unreadableDescriptors > 0)
        outliers.Add($"unreadable-descriptors:{unreadableDescriptors}");
    if (sourcePaletteOutliers > 0)
        outliers.Add($"source-palette-nonencodable:{sourcePaletteOutliers}");
    if (wrappedHqPalettes > 0)
        outliers.Add($"hq-linear-clut-wrap:{wrappedHqPalettes}");
    if (crossRole > 0)
        outliers.Add($"pixel-palette-cross-role-alias:{crossRole}B");
    if (partialSameFormatPixelOverlapPairs > 0)
        outliers.Add($"partial-same-format-pixel-overlap:{partialSameFormatPixelOverlapPairs}");

    return new DonorRecordRow(
        level.Key,
        level.DisplayName,
        level.LevelId,
        level.SourceWadEntry,
        IsFlight(level),
        record.TextureId,
        pixelUnion.Count,
        paletteUnion.Count,
        crossRole,
        physicalUnion.Count,
        descriptorReferenceBytes,
        descriptorReferenceBytes - physicalUnion.Count,
        lqAlias,
        completeGrammar,
        unreadableDescriptors == 0,
        wrappedHqPalettes,
        maxHqPaletteX,
        sourcePaletteOutliers,
        partialSameFormatPixelOverlapPairs,
        outliers);
}

SourceLevelOutlierRow AnalyzeSourceLevel(
    LevelDefinition level,
    TextureIndex textureIndex,
    int texturePageLength)
{
    HashSet<int> levelPixels = [];
    HashSet<int> levelPalettes = [];
    Dictionary<int, HashSet<int>> physicalByRecord = [];
    int wrappedHqDescriptors = 0;
    int sourcePaletteOutliers = 0;
    int unreadableDescriptors = 0;
    foreach (TextureRecord record in textureIndex.Records)
    {
        HashSet<int> physical = [];
        foreach (Descriptor descriptor in EnumerateDescriptors(record))
        {
            int[] pixels = EnumeratePixelOffsets(descriptor).ToArray();
            int[] palettes = EnumeratePaletteOffsets(descriptor).ToArray();
            levelPixels.UnionWith(pixels);
            levelPalettes.UnionWith(palettes);
            physical.UnionWith(pixels);
            physical.UnionWith(palettes);
            if (pixels.Any(offset => offset < 0 || offset >= texturePageLength) ||
                palettes.Any(offset => offset < 0 || offset >= texturePageLength))
            {
                unreadableDescriptors++;
            }
            if (!IsSourcePaletteEncodable(descriptor))
                sourcePaletteOutliers++;
            if (descriptor.Format == TextureFormat.Hq8 && descriptor.PaletteStart % RowBytes > 512)
                wrappedHqDescriptors++;
        }
        physicalByRecord[record.TextureId] = physical;
    }
    HashSet<int> crossRole = levelPixels.Intersect(levelPalettes).ToHashSet();
    int[] touchingRecords = physicalByRecord
        .Where(pair => pair.Value.Overlaps(crossRole))
        .Select(pair => pair.Key)
        .Order()
        .ToArray();
    List<string> outliers = [];
    if (crossRole.Count > 0)
        outliers.Add($"native-cross-record-pixel-palette-alias:{crossRole.Count}B");
    if (wrappedHqDescriptors > 0)
        outliers.Add($"hq-linear-clut-wrap:{wrappedHqDescriptors}");
    if (sourcePaletteOutliers > 0)
        outliers.Add($"source-palette-nonencodable:{sourcePaletteOutliers}");
    if (unreadableDescriptors > 0)
        outliers.Add($"unreadable-descriptors:{unreadableDescriptors}");
    return new SourceLevelOutlierRow(
        level.Key,
        level.DisplayName,
        level.SourceWadEntry,
        textureIndex.TextureCount,
        levelPixels.Count,
        levelPalettes.Count,
        crossRole.Count,
        touchingRecords,
        wrappedHqDescriptors,
        sourcePaletteOutliers,
        unreadableDescriptors,
        outliers);
}

ImpossiblePairReplacementRow BuildReplacementRow(
    DestinationCapacityRow destination,
    DonorRecordRow donor)
{
    WholeRecordReplacementAttempt[] attempts = destination.ReplacementTargets
        .Select(target =>
        {
            int residual = checked(
                destination.AnchorAwareSlackBeforeDonorBytes +
                target.TargetExclusiveBytes -
                donor.UniquePhysicalBytes);
            return new WholeRecordReplacementAttempt(
                target.TargetTextureId,
                target.TargetOwnedBytes,
                target.TargetExclusiveBytes,
                target.AnyDecodedConsumerOverlapBytes,
                residual,
                residual >= 0);
        })
        .OrderByDescending(item => item.ResidualBytes)
        .ThenBy(item => item.TargetTextureId)
        .ToArray();
    WholeRecordReplacementAttempt? best = attempts.FirstOrDefault();
    return new ImpossiblePairReplacementRow(
        destination.LevelKey,
        destination.LevelName,
        donor.LevelKey,
        donor.LevelName,
        donor.TextureId,
        donor.UniquePhysicalBytes,
        checked(destination.AnchorAwareSlackBeforeDonorBytes - donor.UniquePhysicalBytes),
        attempts.Count(item => item.FeasibleByPhysicalBytes),
        attempts.Any(item => item.FeasibleByPhysicalBytes),
        best?.TargetTextureId,
        best?.TargetExclusiveBytes ?? 0,
        best?.ResidualBytes ?? int.MinValue,
        attempts);
}

ExactContentDuplicatePotential AnalyzeDuplicatePotential(
    LevelDefinition level,
    TextureIndex textureIndex,
    byte[] texturePages,
    IReadOnlyList<int> movableTextureIds,
    IReadOnlyList<TargetReplacementCapacity> replacementTargets)
{
    HashSet<int> movable = movableTextureIds.ToHashSet();
    Dictionary<string, ComponentCandidate> componentsByPhysicalKey = new(StringComparer.Ordinal);
    Dictionary<int, string> recordHashes = [];
    HashSet<int> globalPixelOffsets = [];
    HashSet<int> globalPaletteOffsets = [];

    foreach (TextureRecord record in textureIndex.Records.Where(item => movable.Contains(item.TextureId)))
    {
        StringBuilder recordSignature = new();
        int ordinal = 0;
        foreach (Descriptor descriptor in EnumerateDescriptors(record))
        {
            int[] pixelOffsets = EnumeratePixelOffsets(descriptor).Distinct().Order().ToArray();
            int[] paletteOffsets = EnumeratePaletteOffsets(descriptor).Distinct().Order().ToArray();
            Assert(pixelOffsets.All(offset => offset >= 0 && offset < texturePages.Length),
                $"{level.DisplayName} T{record.TextureId}: a pixel component is unreadable.");
            Assert(paletteOffsets.All(offset => offset >= 0 && offset < texturePages.Length),
                $"{level.DisplayName} T{record.TextureId}: a palette component is unreadable.");
            globalPixelOffsets.UnionWith(pixelOffsets);
            globalPaletteOffsets.UnionWith(paletteOffsets);

            string pixelContentHash = HashLogicalPixels(texturePages, descriptor);
            string paletteContentHash = HashPalette(texturePages, descriptor);
            AddComponent(
                componentsByPhysicalKey,
                "pixel",
                descriptor,
                pixelOffsets,
                HashPhysicalStorage(texturePages, pixelOffsets),
                record.TextureId,
                ordinal);
            AddComponent(
                componentsByPhysicalKey,
                "palette",
                descriptor,
                paletteOffsets,
                HashPhysicalStorage(texturePages, paletteOffsets),
                record.TextureId,
                ordinal);
            recordSignature.Append((int)descriptor.Format).Append(':')
                .Append(descriptor.Side).Append(':')
                .Append(pixelContentHash).Append(':')
                .Append(paletteContentHash).Append('|');
            ordinal++;
        }
        recordHashes[record.TextureId] = HashText(recordSignature.ToString());
    }

    ComponentCandidate[] components = componentsByPhysicalKey.Values.ToArray();
    IGrouping<string, ComponentCandidate>[] duplicateGroups = components
        .GroupBy(component => component.ContentKey, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .ToArray();
    int pixelPotential = duplicateGroups
        .Where(group => group.First().Role == "pixel")
        .Sum(group => group.OrderByDescending(item => item.ByteCount).Skip(1).Sum(item => item.ByteCount));
    int palettePotential = duplicateGroups
        .Where(group => group.First().Role == "palette")
        .Sum(group => group.OrderByDescending(item => item.ByteCount).Skip(1).Sum(item => item.ByteCount));
    IReadOnlyDictionary<int, int> exclusiveByTextureId = replacementTargets
        .ToDictionary(item => item.TargetTextureId, item => item.TargetExclusiveBytes);
    ExactDuplicateRecordGroup[] recordDuplicateGroups = recordHashes
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group =>
        {
            int[] ids = group.Select(pair => pair.Key).Order().ToArray();
            int[] exclusive = ids.Select(id => exclusiveByTextureId.GetValueOrDefault(id)).ToArray();
            int reclaim = checked(exclusive.Sum() - exclusive.Min());
            return new ExactDuplicateRecordGroup(group.Key, ids, exclusive, reclaim);
        })
        .OrderBy(group => group.TextureIds[0])
        .ToArray();
    int crossRoleBytes = globalPixelOffsets.Intersect(globalPaletteOffsets).Count();
    int recordExclusiveReclaim = recordDuplicateGroups.Sum(group => group.OptimisticExclusiveReclaimBytes);
    return new ExactContentDuplicatePotential(
        level.Key,
        movable.Count,
        components.Count(component => component.Role == "pixel"),
        components.Count(component => component.Role == "palette"),
        duplicateGroups.Count(group => group.First().Role == "pixel"),
        duplicateGroups.Count(group => group.First().Role == "palette"),
        pixelPotential,
        palettePotential,
        checked(pixelPotential + palettePotential),
        crossRoleBytes,
        recordDuplicateGroups.Length,
        recordDuplicateGroups.Sum(group => group.TextureIds.Count - 1),
        recordExclusiveReclaim,
        Math.Max(checked(pixelPotential + palettePotential), recordExclusiveReclaim),
        recordDuplicateGroups,
        false,
        "Optimistic only: equal logical content at distinct physical addresses is not a native alias and cannot be coalesced without copy-on-write/runtime proof.");
}

void AddComponent(
    IDictionary<string, ComponentCandidate> components,
    string role,
    Descriptor descriptor,
    int[] offsets,
    string logicalContentHash,
    int textureId,
    int descriptorOrdinal)
{
    string physicalKey = $"{role}:{(int)descriptor.Format}:{HashOffsets(offsets)}";
    string contentKey = role == "palette"
        ? $"{role}:{(int)descriptor.Format}:{logicalContentHash}"
        : $"{role}:{(int)descriptor.Format}:{descriptor.Side}:{logicalContentHash}";
    if (components.TryGetValue(physicalKey, out ComponentCandidate? existing))
    {
        Assert(string.Equals(existing.ContentKey, contentKey, StringComparison.Ordinal),
            "One physical component produced two logical content hashes.");
        existing.Owners.Add((textureId, descriptorOrdinal));
        return;
    }
    components[physicalKey] = new ComponentCandidate(
        role,
        descriptor.Format,
        descriptor.Side,
        offsets.Length,
        physicalKey,
        contentKey,
        [(textureId, descriptorOrdinal)]);
}

string HashLogicalPixels(byte[] pages, Descriptor descriptor)
{
    byte[] logical = new byte[checked(descriptor.Side * descriptor.Side)];
    int ordinal = 0;
    for (int y = 0; y < descriptor.Side; y++)
    {
        for (int x = 0; x < descriptor.Side; x++)
            logical[ordinal++] = ReadIndex(pages, descriptor, x, y);
    }
    return Convert.ToHexString(SHA256.HashData(logical));
}

string HashPalette(byte[] pages, Descriptor descriptor)
{
    byte[] logical = EnumeratePaletteOffsets(descriptor)
        .Select(offset => pages[offset])
        .ToArray();
    return Convert.ToHexString(SHA256.HashData(logical));
}

byte ReadIndex(byte[] pages, Descriptor descriptor, int x, int y)
{
    if (descriptor.Format == TextureFormat.Lq4)
    {
        int sx = descriptor.PackedX + (x / 2);
        int sy = descriptor.Y + y;
        byte value = pages[checked((sy * RowBytes) + sx)];
        return (byte)((value >> ((x & 1) * 4)) & 0x0F);
    }
    int[] matrix = matrices[Math.Clamp(descriptor.Orientation, 0, 7)];
    int edge = descriptor.Side - 1;
    int startY = descriptor.Y + ((matrix[2] < 0 || matrix[3] < 0) ? edge : 0);
    int startX = descriptor.PackedX + ((matrix[0] < 0 || matrix[1] < 0) ? edge : 0);
    int sxHq = startX + (x * matrix[0]) + (y * matrix[1]);
    int syHq = startY + (x * matrix[2]) + (y * matrix[3]);
    return pages[checked((syHq * RowBytes) + sxHq)];
}

string HashOffsets(IReadOnlyList<int> offsets)
{
    byte[] bytes = new byte[checked(offsets.Count * sizeof(int))];
    for (int index = 0; index < offsets.Count; index++)
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * sizeof(int), sizeof(int)), offsets[index]);
    return Convert.ToHexString(SHA256.HashData(bytes));
}

string HashPhysicalStorage(byte[] pages, IReadOnlyList<int> offsets)
{
    byte[] bytes = offsets.Select(offset => pages[offset]).ToArray();
    return Convert.ToHexString(SHA256.HashData(bytes));
}

string HashText(string value) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

IEnumerable<Descriptor> EnumerateDescriptors(TextureRecord record) =>
    record.Low.Concat([record.Leading]).Concat(record.Normal).Concat(record.Close);

IEnumerable<int> EnumeratePixelOffsets(Descriptor descriptor)
{
    for (int y = 0; y < descriptor.Side; y++)
    {
        for (int x = 0; x < descriptor.Side; x++)
        {
            if (descriptor.Format == TextureFormat.Lq4)
            {
                int sx = descriptor.PackedX + (x / 2);
                int sy = descriptor.Y + y;
                yield return checked((sy * RowBytes) + sx);
                continue;
            }
            int[] matrix = matrices[Math.Clamp(descriptor.Orientation, 0, 7)];
            int edge = descriptor.Side - 1;
            int startY = descriptor.Y + ((matrix[2] < 0 || matrix[3] < 0) ? edge : 0);
            int startX = descriptor.PackedX + ((matrix[0] < 0 || matrix[1] < 0) ? edge : 0);
            int sxHq = startX + (x * matrix[0]) + (y * matrix[1]);
            int syHq = startY + (x * matrix[2]) + (y * matrix[3]);
            yield return checked((syHq * RowBytes) + sxHq);
        }
    }
}

IEnumerable<int> EnumeratePaletteOffsets(Descriptor descriptor)
{
    if (descriptor.Format == TextureFormat.Lq4)
    {
        for (int row = 0; row < LqPaletteRows; row++)
        {
            int start = checked((int)descriptor.PaletteStart + (row * RowBytes));
            for (int index = 0; index < LqPaletteWidthBytes; index++)
                yield return start + index;
        }
        yield break;
    }
    for (int index = 0; index < HqPaletteBytes; index++)
        yield return checked((int)descriptor.PaletteStart + index);
}

bool IsSourcePaletteEncodable(Descriptor descriptor)
{
    if (descriptor.PaletteStart < 0 || descriptor.PaletteStart >= AddressableBytes)
        return false;
    int x = checked((int)(descriptor.PaletteStart % RowBytes));
    int y = checked((int)(descriptor.PaletteStart / RowBytes));
    return descriptor.Format == TextureFormat.Hq8
        ? (x & 31) == 0 && x <= 992 && y is >= 0 and < VramRows &&
          descriptor.PaletteStart + HqPaletteBytes <= AddressableBytes
        : (x & 31) == 0 && x <= 992 && (y & 3) == 0 && y is >= 0 and <= 496;
}

AssetData LoadAsset(string sourcePath, DiscLayout discLayout, LevelDefinition level)
{
    using FileStream image = File.OpenRead(sourcePath);
    Subfile pages = GetSubfile(image, discLayout, level.SourceWadEntry, TexturePagesSubfileIndex);
    Subfile model = GetSubfile(image, discLayout, level.SourceWadEntry, ModelSubfileIndex);
    byte[] pageBytes = ReadDiscFileBytes(image, discLayout, WadLba, pages.WadOffset, checked((int)pages.Length));
    byte[] modelBytes = ReadDiscFileBytes(image, discLayout, WadLba, model.WadOffset, checked((int)model.Length));
    return new AssetData(pageBytes, modelBytes);
}

Subfile GetSubfile(FileStream image, DiscLayout discLayout, int wadEntry, int subfileIndex)
{
    byte[] wadHeader = ReadDiscFileBytes(image, discLayout, WadLba, 0, 4096);
    ArchiveEntry wad = ParseHeader(wadHeader, 200_000_000).Single(item => item.Index == wadEntry);
    byte[] assetHeader = ReadDiscFileBytes(image, discLayout, WadLba, wad.Offset, 4096);
    ArchiveEntry subfile = ParseHeader(assetHeader, wad.Length).Single(item => item.Index == subfileIndex);
    return new Subfile(wad.Offset + subfile.Offset, subfile.Length);
}

IReadOnlyList<ArchiveEntry> ParseHeader(byte[] bytes, long archiveLength)
{
    List<ArchiveEntry> result = [];
    long first = ReadUInt32(bytes, 0);
    if (first <= 0 || first > bytes.Length)
        first = bytes.Length;
    for (int offset = 0; offset <= Math.Min(bytes.Length, first) - 8; offset += 8)
    {
        long start = ReadUInt32(bytes, offset);
        long length = ReadUInt32(bytes, offset + 4);
        if (start > 0 && length > 0 && start + length <= archiveLength)
            result.Add(new ArchiveEntry(offset / 8, start, length));
    }
    return result;
}

TextureIndex Decode(byte[] model)
{
    Assert(model.Length >= 8, "Texture model is shorter than its native header.");
    int length = checked((int)ReadUInt32(model, 0));
    int count = checked((int)ReadUInt32(model, 4));
    int expected = checked(8 + (count * (LqRecordBytes + HqRecordBytes)));
    Assert(count is >= 1 and <= 128 && length == expected && length <= model.Length,
        $"Invalid native terrain texture table {length}/{count}.");
    int highStart = checked(8 + (count * LqRecordBytes));
    List<TextureRecord> records = [];
    for (int textureId = 0; textureId < count; textureId++)
    {
        int lowOffset = checked(8 + (textureId * LqRecordBytes));
        int highOffset = checked(highStart + (textureId * HqRecordBytes));
        Descriptor[] low = [DecodeLq(model, lowOffset, 0), DecodeLq(model, lowOffset + 8, 1)];
        Descriptor leading = DecodeLq(model, highOffset, 0);
        Descriptor[] normal = Enumerable.Range(0, 4)
            .Select(index => DecodeHq(model, highOffset + 8 + (index * 8), index)).ToArray();
        Descriptor[] close = Enumerable.Range(0, 16)
            .Select(index => DecodeHq(model, highOffset + 40 + (index * 8), index)).ToArray();
        records.Add(new TextureRecord(textureId, low, leading, normal, close));
    }
    return new TextureIndex(count, records);
}

Descriptor DecodeLq(byte[] model, int offset, int index)
{
    byte[] raw = model.AsSpan(offset, 8).ToArray();
    int region = raw[6];
    return new Descriptor(
        index,
        raw,
        TextureFormat.Lq4,
        32,
        checked((raw[3] * 4L * RowBytes) + (raw[2] * (long)LqPaletteWidthBytes) - FullVramTextureByteX),
        0,
        ((((region * 256) % 2048) + raw[0]) / 2),
        GetTextureY(region, raw[1]));
}

Descriptor DecodeHq(byte[] model, int offset, int index)
{
    byte[] raw = model.AsSpan(offset, 8).ToArray();
    int region = raw[6];
    int side = Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1;
    return new Descriptor(
        index,
        raw,
        TextureFormat.Hq8,
        side,
        DecodeClut(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2))),
        (raw[7] >> 4) & 7,
        GetTextureX(region, raw[0]) - FullVramTextureByteX,
        GetTextureY(region, raw[1]));
}

int GetTextureX(int region, int value) => ((region * 128) % 2048) + value;
int GetTextureY(int region, int value) => ((region & 0x10) != 0 ? 256 : 0) + value;
long DecodeClut(int code)
{
    int x = (code & 0x3F) * 16;
    int y = (code >> 6) & 0x1FF;
    return checked((y * (long)RowBytes) + ((x - 512) * 2L));
}

int Percentile(IReadOnlyList<int> sorted, double percentile)
{
    int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

int UnionLength(IEnumerable<NativeTexturePageOwnedRange> ranges)
{
    bool[] mask = new bool[AddressableBytes];
    foreach (NativeTexturePageOwnedRange range in ranges)
    {
        int start = checked((int)range.Offset);
        Assert(start >= 0 && start + range.Length <= mask.Length,
            $"Ownership range {range.Owner} is outside the addressable page.");
        Array.Fill(mask, true, start, range.Length);
    }
    return mask.Count(value => value);
}

string BuildMarkdown(AuditDocument report)
{
    StringBuilder text = new();
    text.AppendLine("# All-donor terrain-texture feasibility audit");
    text.AppendLine();
    text.AppendLine($"- Retail disc SHA-256: `{report.Source.Sha256}`");
    text.AppendLine($"- Scope: **{report.Scope.LevelCount} levels**, **{report.Scope.DonorRecordCount:N0} runtime-initialized donor records**, **{report.Scope.SourceDestinationPairCount:N0} one-record source/destination pairs**.");
    text.AppendLine($"- Exact donor physical-union range: **{report.Distribution.MinimumBytes:N0}..{report.Distribution.MaximumBytes:N0} bytes** (median {report.Distribution.P50Bytes:N0}; p95 {report.Distribution.P95Bytes:N0}).");
    text.AppendLine($"- Largest donor: **{report.WorstDonor.LevelName} T{report.WorstDonor.TextureId}**, {report.WorstDonor.UniquePhysicalBytes:N0} bytes.");
    text.AppendLine($"- Tightest destination: **{report.TightestDestination.LevelName}**, {report.TightestDestination.AnchorAwareSlackBeforeDonorBytes:N0} bytes before import and {report.TightestDestination.WorstDonorResidualBytes:N0} bytes after the largest donor.");
    text.AppendLine($"- Byte-capacity-impossible pairs: **{report.ByteCapacityImpossiblePairCount:N0}**. All one-record pairs fit by physical byte count: **{report.EveryOneRecordPairFitsByPhysicalBytes}**.");
    text.AppendLine($"- Research-only known-owned mask: **{report.DeadUnreferencedCandidateImpossiblePairCount:N0}** byte-capacity-impossible pairs; tightest is **{report.TightestDeadUnreferencedCandidateDestination.LevelName}** at {report.TightestDeadUnreferencedCandidateDestination.DeadUnreferencedCandidateSlackBytes:N0} bytes, with {report.TightestDeadUnreferencedCandidateDestination.DeadCandidateWorstDonorResidualBytes:N0} bytes after the largest donor. This mask is **not production-safe yet**.");
    text.AppendLine();
    text.AppendLine("## Destination capacity");
    text.AppendLine();
    text.AppendLine("| Destination | Records | Protected | Native movable union | Slack before donor | Residual after worst donor | Residual after GW T17 | Impossible donors |");
    text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
    foreach (DestinationCapacityRow row in report.Destinations.OrderBy(row => row.AnchorAwareSlackBeforeDonorBytes))
    {
        text.AppendLine($"| {row.LevelName} | {row.RetailTextureCount} | {row.ProtectedBytes:N0} | {row.ReleasedNativeTerrainBytes:N0} | {row.AnchorAwareSlackBeforeDonorBytes:N0} | {row.WorstDonorResidualBytes:N0} | {row.GnastysWorldT17ResidualBytes:N0} | {row.ByteCapacityImpossibleDonorCount} |");
    }
    text.AppendLine();
    text.AppendLine("## Research-only dead-unreferenced candidate mask");
    text.AppendLine();
    text.AppendLine("This alternate count treats every addressable byte not claimed by a closed decoded consumer as dead, even when its retail preimage is nonzero. It is a capacity experiment, not an allocator authorization.");
    text.AppendLine();
    text.AppendLine("| Destination | Known owned | Unknown nonzero | Unknown regions | Unowned zero | Candidate slack | Residual after worst donor | Impossible donors | Closure complete |");
    text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (DestinationCapacityRow row in report.Destinations.OrderBy(row => row.DeadUnreferencedCandidateSlackBytes))
    {
        text.AppendLine($"| {row.LevelName} | {row.KnownOwnedByteCount:N0} | {row.UnknownNonZeroByteCount:N0} | {row.UnknownNonZeroRegionCount:N0} | {row.UnknownUnprovenZeroByteCount:N0} | {row.DeadUnreferencedCandidateSlackBytes:N0} | {row.DeadCandidateWorstDonorResidualBytes:N0} | {row.DeadCandidateImpossibleDonorCount} | {row.AllConsumerClosureComplete} |");
    }
    text.AppendLine();
    text.AppendLine("### Why complete decoded-consumer closure is not yet a dead-byte proof");
    text.AppendLine();
    foreach (string blocker in report.DeadUnreferencedStorageBoundary.CurrentProofBlockers)
        text.AppendLine($"- {blocker}");
    text.AppendLine();
    text.AppendLine("### Conditions required before production use");
    text.AppendLine();
    foreach (string condition in report.DeadUnreferencedStorageBoundary.RequiredProofConditions)
        text.AppendLine($"- {condition}");
    text.AppendLine();
    text.AppendLine("### Candidate runtime test");
    text.AppendLine();
    foreach (string step in report.DeadUnreferencedStorageBoundary.CandidateRuntimeTest)
        text.AppendLine($"- {step}");
    text.AppendLine();
    text.AppendLine("## Largest donor records");
    text.AppendLine();
    text.AppendLine("| Donor | Physical union | Pixel union | Palette union | Cross-role overlap | Alias savings | Wrapped HQ CLUTs | Outliers |");
    text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
    foreach (DonorRecordRow row in report.LargestDonors)
    {
        text.AppendLine($"| {row.LevelName} T{row.TextureId} | {row.UniquePhysicalBytes:N0} | {row.UniquePixelBytes:N0} | {row.UniquePaletteBytes:N0} | {row.CrossRoleAliasBytes:N0} | {row.AliasSavingsBytes:N0} | {row.WrappedHqPaletteDescriptorCount} | {string.Join(", ", row.Outliers)} |");
    }
    text.AppendLine();
    text.AppendLine("## Donors that exceed the tightest selected-private capacity");
    text.AppendLine();
    text.AppendLine($"These **{report.LargeDonorsExceedingTightestSlack.Count}** donors exceed {report.TightestDestination.LevelName}'s {report.TightestDestination.AnchorAwareSlackBeforeDonorBytes:N0}-byte private-append slack.");
    text.AppendLine();
    text.AppendLine("| Donor | Bytes | Exceeds tightest slack by | Flight source |");
    text.AppendLine("|---|---:|---:|---:|");
    foreach (DonorRecordRow row in report.LargeDonorsExceedingTightestSlack)
        text.AppendLine($"| {row.LevelName} T{row.TextureId} | {row.UniquePhysicalBytes:N0} | {row.UniquePhysicalBytes - report.TightestDestination.AnchorAwareSlackBeforeDonorBytes:N0} | {row.IsFlight} |");
    text.AppendLine();
    text.AppendLine("## Selected-private failures versus whole-record replacement");
    text.AppendLine();
    text.AppendLine("Each row is one byte-capacity-impossible private append. Replacement tries every runtime-persistent target id and reclaims only that target's decoded-consumer-exclusive bytes.");
    text.AppendLine();
    text.AppendLine("| Destination | Donor | Donor bytes | Private residual | Replacement targets that fit | Best target | Best exclusive reclaim | Best residual |");
    text.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
    foreach (ImpossiblePairReplacementRow row in report.ImpossiblePairReplacementMatrix)
    {
        text.AppendLine($"| {row.DestinationName} | {row.DonorLevelName} T{row.DonorTextureId} | {row.DonorBytes:N0} | {row.PrivateAppendResidualBytes:N0} | {row.FeasibleReplacementTargetCount} | {(row.BestTargetTextureId.HasValue ? $"T{row.BestTargetTextureId}" : "none")} | {row.BestTargetExclusiveBytes:N0} | {row.BestReplacementResidualBytes:N0} |");
    }
    text.AppendLine();
    text.AppendLine("## Exact-content duplicate-storage potential (unsafe upper bound)");
    text.AppendLine();
    text.AppendLine("| Destination | Movable records | Pixel components | Palette components | Duplicate pixel groups | Duplicate palette groups | Component byte potential | Exact-record exclusive reclaim | Conservative combined upper bound | Native cross-role bytes | Exact duplicate record groups |");
    text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (DestinationCapacityRow row in report.Destinations.OrderBy(row => row.AnchorAwareSlackBeforeDonorBytes))
    {
        ExactContentDuplicatePotential duplicate = row.ExactContentDuplicatePotential;
        text.AppendLine($"| {row.LevelName} | {duplicate.MovableRecordCount} | {duplicate.UniquePhysicalPixelComponentCount} | {duplicate.UniquePhysicalPaletteComponentCount} | {duplicate.ExactContentDuplicatePixelGroupCount} | {duplicate.ExactContentDuplicatePaletteGroupCount} | {duplicate.OptimisticPotentialSavingsBytes:N0} | {duplicate.OptimisticExactRecordExclusiveReclaimBytes:N0} | {duplicate.ConservativeCombinedUpperBoundBytes:N0} | {duplicate.NativeCrossRoleAliasBytes:N0} | {duplicate.ExactDuplicateRecordGroupCount} |");
    }
    text.AppendLine();
    text.AppendLine("## Descriptor/alias outliers");
    text.AppendLine();
    text.AppendLine($"- Records with a retail HQ CLUT linear span crossing a packed-row boundary: **{report.Distribution.RecordsWithWrappedHqPalettes:N0}** ({report.Distribution.WrappedHqPaletteDescriptorCount:N0} descriptors).");
    text.AppendLine($"- Records with exact pixel/palette cross-role aliases: **{report.Distribution.RecordsWithCrossRoleAliases:N0}** ({report.Distribution.CrossRoleAliasByteCount:N0} overlapping bytes).");
    text.AppendLine($"- Records outside the complete 2-LQ + leading-alias + 4-normal + 16-close grammar: **{report.Distribution.RecordsOutsideCompleteNativeGrammar:N0}**.");
    text.AppendLine($"- Records with unreadable source descriptors: **{report.Distribution.RecordsWithUnreadableDescriptors:N0}**.");
    text.AppendLine($"- Records with partially overlapping same-format pixel rectangles: **{report.Distribution.RecordsWithPartialSameFormatPixelOverlaps:N0}**.");
    text.AppendLine();
    text.AppendLine("### Whole-level native alias/encodability outliers");
    text.AppendLine();
    text.AppendLine("| Level | Cross-role alias bytes | Records touching cross-role bytes | Wrapped HQ CLUT descriptors | Source palette errors | Unreadable descriptors |");
    text.AppendLine("|---|---:|---|---:|---:|---:|");
    foreach (SourceLevelOutlierRow row in report.SourceLevelOutliers.Where(row => row.Outliers.Count > 0))
    {
        text.AppendLine($"| {row.LevelName} | {row.NativeCrossRoleAliasBytes:N0} | {string.Join(", ", row.TextureIdsTouchingCrossRoleAliases.Select(id => $"T{id}"))} | {row.WrappedHqPaletteDescriptorCount} | {row.SourcePaletteEncodabilityOutlierCount} | {row.UnreadableDescriptorCount} |");
    }
    text.AppendLine();
    text.AppendLine("## What the capacity result proves");
    text.AppendLine();
    foreach (string caveat in report.Caveats)
        text.AppendLine($"- {caveat}");
    text.AppendLine();
    text.AppendLine("The audit is deliberately read-only. It neither changes the retail BIN nor claims runtime proof for a generated disc.");
    return text.ToString();
}

string ResolveWorkspaceRoot(string? supplied)
{
    string current = Path.GetFullPath(string.IsNullOrWhiteSpace(supplied)
        ? Directory.GetCurrentDirectory()
        : supplied);
    for (DirectoryInfo? directory = new(current); directory != null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "spyro-level-catalog.json")) &&
            File.Exists(Path.Combine(directory.FullName, "Spyro the Dragon (USA).bin")))
        {
            return directory.FullName;
        }
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor workspace root.");
}

bool IsFlight(LevelDefinition level) =>
    level.DisplayName.Contains("Flight", StringComparison.OrdinalIgnoreCase);

DiscLayout DetectDiscLayout(string path)
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

byte[] ReadDiscFileBytes(
    FileStream stream,
    DiscLayout discLayout,
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
        stream.Position = ((long)sector * discLayout.SectorSize) + discLayout.UserOffset + sectorOffset;
        int read = stream.Read(result, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException($"Could not read WAD offset 0x{fileOffset:X}.");
        written += read;
        remaining -= read;
        absolute += read;
    }
    return result;
}

uint ReadUInt32(byte[] bytes, int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

internal sealed record ArchiveEntry(int Index, long Offset, long Length);
internal sealed record Subfile(long WadOffset, long Length);
internal sealed record AssetData(byte[] TexturePages, byte[] Model)
{
    public int TexturePageLength => TexturePages.Length;
}
internal sealed record DecodedLevelData(TextureIndex TextureIndex, byte[] TexturePages);
internal sealed record DiscLayout(int SectorSize, int UserOffset);
internal sealed record TextureIndex(int TextureCount, IReadOnlyList<TextureRecord> Records);
internal sealed record TextureRecord(
    int TextureId,
    IReadOnlyList<Descriptor> Low,
    Descriptor Leading,
    IReadOnlyList<Descriptor> Normal,
    IReadOnlyList<Descriptor> Close);
internal sealed record Descriptor(
    int Index,
    byte[] Raw,
    TextureFormat Format,
    int Side,
    long PaletteStart,
    int Orientation,
    int PackedX,
    int Y);
internal enum TextureFormat { Lq4, Hq8 }

internal sealed record DonorRecordRow(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    bool IsFlight,
    int TextureId,
    int UniquePixelBytes,
    int UniquePaletteBytes,
    int CrossRoleAliasBytes,
    int UniquePhysicalBytes,
    int DescriptorReferenceBytes,
    int AliasSavingsBytes,
    bool LowDetailThreeWayAlias,
    bool CompleteNativeGrammar,
    bool AllDescriptorsReadable,
    int WrappedHqPaletteDescriptorCount,
    int MaximumHqPaletteStartX,
    int SourcePaletteEncodabilityOutlierCount,
    int PartialSameFormatPixelOverlapPairs,
    IReadOnlyList<string> Outliers);

internal sealed record DestinationCapacityRow(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    bool IsFlight,
    int RetailTextureCount,
    int RuntimeControlledTextureCount,
    int MovableTextureCount,
    int ProtectedBytes,
    int ReleasedNativeTerrainBytes,
    int AnchorAwareSlackBeforeDonorBytes,
    int KnownOwnedByteCount,
    int UnknownNonZeroByteCount,
    int UnknownNonZeroRegionCount,
    int UnknownUnprovenZeroByteCount,
    bool AllConsumerClosureComplete,
    int DeadUnreferencedCandidateSlackBytes,
    int DeadCandidateWorstDonorResidualBytes,
    int DeadCandidateImpossibleDonorCount,
    int WorstDonorBytes,
    int WorstDonorResidualBytes,
    int GnastysWorldT17Bytes,
    int GnastysWorldT17ResidualBytes,
    int ByteCapacityImpossibleDonorCount,
    bool EveryDonorFitsByPhysicalBytes,
    IReadOnlyList<TargetReplacementCapacity> ReplacementTargets,
    ExactContentDuplicatePotential ExactContentDuplicatePotential,
    long ElapsedMilliseconds);

internal sealed record TargetReplacementCapacity(
    int TargetTextureId,
    int TargetOwnedBytes,
    int TargetExclusiveBytes,
    int AnyDecodedConsumerOverlapBytes);

internal sealed record WholeRecordReplacementAttempt(
    int TargetTextureId,
    int TargetOwnedBytes,
    int TargetExclusiveBytes,
    int AnyDecodedConsumerOverlapBytes,
    int ResidualBytes,
    bool FeasibleByPhysicalBytes);

internal sealed record ImpossiblePairReplacementRow(
    string DestinationKey,
    string DestinationName,
    string DonorLevelKey,
    string DonorLevelName,
    int DonorTextureId,
    int DonorBytes,
    int PrivateAppendResidualBytes,
    int FeasibleReplacementTargetCount,
    bool AnyWholeRecordReplacementFitsByPhysicalBytes,
    int? BestTargetTextureId,
    int BestTargetExclusiveBytes,
    int BestReplacementResidualBytes,
    IReadOnlyList<WholeRecordReplacementAttempt> TargetAttempts);

internal sealed record ExactDuplicateRecordGroup(
    string LogicalRecordSha256,
    IReadOnlyList<int> TextureIds,
    IReadOnlyList<int> TargetExclusiveBytes,
    int OptimisticExclusiveReclaimBytes);

internal sealed record ExactContentDuplicatePotential(
    string LevelKey,
    int MovableRecordCount,
    int UniquePhysicalPixelComponentCount,
    int UniquePhysicalPaletteComponentCount,
    int ExactContentDuplicatePixelGroupCount,
    int ExactContentDuplicatePaletteGroupCount,
    int OptimisticPixelPotentialSavingsBytes,
    int OptimisticPalettePotentialSavingsBytes,
    int OptimisticPotentialSavingsBytes,
    int NativeCrossRoleAliasBytes,
    int ExactDuplicateRecordGroupCount,
    int RedundantExactRecordCount,
    int OptimisticExactRecordExclusiveReclaimBytes,
    int ConservativeCombinedUpperBoundBytes,
    IReadOnlyList<ExactDuplicateRecordGroup> ExactDuplicateRecordGroups,
    bool RuntimeSafeToCoalesce,
    string SafetyBoundary);

internal sealed class ComponentCandidate(
    string role,
    TextureFormat format,
    int side,
    int byteCount,
    string physicalKey,
    string contentKey,
    HashSet<(int TextureId, int DescriptorOrdinal)> owners)
{
    public string Role { get; } = role;
    public TextureFormat Format { get; } = format;
    public int Side { get; } = side;
    public int ByteCount { get; } = byteCount;
    public string PhysicalKey { get; } = physicalKey;
    public string ContentKey { get; } = contentKey;
    public HashSet<(int TextureId, int DescriptorOrdinal)> Owners { get; } = owners;
}

internal sealed record DonorDistribution(
    int RecordCount,
    int MinimumBytes,
    int P50Bytes,
    int P90Bytes,
    int P95Bytes,
    int P99Bytes,
    int MaximumBytes,
    double MeanBytes,
    int RecordsWithCrossRoleAliases,
    int CrossRoleAliasByteCount,
    int RecordsWithWrappedHqPalettes,
    int WrappedHqPaletteDescriptorCount,
    int RecordsOutsideCompleteNativeGrammar,
    int RecordsWithUnreadableDescriptors,
    int RecordsWithPartialSameFormatPixelOverlaps);

internal sealed record SourceLevelOutlierRow(
    string LevelKey,
    string LevelName,
    int WadEntry,
    int TextureCount,
    int UniquePixelBytes,
    int UniquePaletteBytes,
    int NativeCrossRoleAliasBytes,
    IReadOnlyList<int> TextureIdsTouchingCrossRoleAliases,
    int WrappedHqPaletteDescriptorCount,
    int SourcePaletteEncodabilityOutlierCount,
    int UnreadableDescriptorCount,
    IReadOnlyList<string> Outliers);

internal sealed record SourceProof(
    string ImagePath,
    long ByteLength,
    string Sha256,
    int SectorSize,
    int UserDataOffset);

internal sealed record DeadUnreferencedStorageResearchBoundary(
    bool ProductionSafe,
    long CandidateByteCapacityImpossiblePairCount,
    bool EveryOneRecordPairFitsCandidateMaskByPhysicalBytes,
    string TightestCandidateLevelKey,
    string TightestCandidateLevelName,
    int TightestCandidateSlackBytes,
    int TightestCandidateWorstDonorResidualBytes,
    IReadOnlyList<string> CurrentProofBlockers,
    IReadOnlyList<string> RequiredProofConditions,
    IReadOnlyList<string> CandidateRuntimeTest);

internal sealed record ScopeProof(
    int LevelCount,
    int DonorRecordCount,
    long SourceDestinationPairCount,
    string Description);

internal sealed record AuditDocument(
    DateTimeOffset GeneratedAtUtc,
    SourceProof Source,
    ScopeProof Scope,
    DonorDistribution Distribution,
    DonorRecordRow WorstDonor,
    DestinationCapacityRow TightestDestination,
    long ByteCapacityImpossiblePairCount,
    bool EveryOneRecordPairFitsByPhysicalBytes,
    long DeadUnreferencedCandidateImpossiblePairCount,
    DestinationCapacityRow TightestDeadUnreferencedCandidateDestination,
    DeadUnreferencedStorageResearchBoundary DeadUnreferencedStorageBoundary,
    IReadOnlyList<DonorRecordRow> LargeDonorsExceedingTightestSlack,
    IReadOnlyList<ImpossiblePairReplacementRow> ImpossiblePairReplacementMatrix,
    IReadOnlyList<DonorRecordRow> LargestDonors,
    IReadOnlyList<DonorRecordRow> DonorRecords,
    IReadOnlyList<SourceLevelOutlierRow> SourceLevelOutliers,
    IReadOnlyList<DestinationCapacityRow> Destinations,
    IReadOnlyList<string> Caveats,
    bool RuntimeInitialStateAppliedToDonors,
    bool SourceImageUnchanged);
