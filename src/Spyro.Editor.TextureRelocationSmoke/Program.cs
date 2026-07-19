using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int CompleteDescriptorCount = 23;
const int LowDetailDescriptorCount = 2;
const int LeadingDescriptorCount = 1;
const int NormalDescriptorCount = 4;
const int CloseDescriptorCount = 16;
const int GnastyWorld22RequiredBytes = 19_456;
const int GnastyWorld22PixelBytes = 8_704;
const int CompletePaletteBytes = 10_752;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "native-terrain-texture-relocation");
Directory.CreateDirectory(outputRoot);
string sourceSha256Before = ComputeSha256(sourceImagePath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
LevelDefinition artisansLevel = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
const int donorTextureId = 22;
List<AuditRow> rows = [];
foreach (LevelDefinition level in LevelRealmCatalog.OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0)))
{
    IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(sourceImagePath, level);
    Dictionary<int, int> faceUseCounts = LoadFaceTextureUseCounts(workspaceRoot, level.Key);
    int[] unused = slots
        .Select(slot => slot.TextureId)
        .Where(textureId => !faceUseCounts.ContainsKey(textureId))
        .Order()
        .ToArray();
    bool hasUnusedRecord = unused.Length > 0;
    int targetTextureId = hasUnusedRecord
        ? unused[0]
        : ChooseLeastUsedTexture(slots.Count, faceUseCounts);
    if (string.Equals(level.Key, "artisans", StringComparison.OrdinalIgnoreCase) && slots.Count > 54)
        targetTextureId = 54; // Focused retail LQ/HQ relocation example supplied by the descriptor audit.

    NativeTerrainTextureRelocationImport import = new(
        TargetTextureId: targetTextureId,
        DonorWadEntry: donorLevel.SourceWadEntry,
        DonorTextureId: donorTextureId,
        DescriptorTier: "both");
    NativeTerrainTextureRelocationAudit audit = NativeTerrainTextureRelocationAllocator.Audit(
        sourceImagePath,
        level,
        [import]);

    AssertCompleteAuditShape(audit, level.DisplayName);
    Assert(audit.RequiredAllocationByteCount == GnastyWorld22RequiredBytes,
        $"{level.DisplayName}: all-tier Gnasty's World texture 22 allocation should require exactly {GnastyWorld22RequiredBytes:N0} bytes, got {audit.RequiredAllocationByteCount:N0}.");
    Assert(!audit.OwnershipProofComplete,
        $"{level.DisplayName}: real-disc relocation unexpectedly bypassed the all-consumer proof gate.");
    Assert(audit.SafetyBlockers.Count == 4,
        $"{level.DisplayName}: expected the four external ownership blockers, got {audit.SafetyBlockers.Count}.");
    Assert(!audit.SafetyBlockers.Any(blocker => blocker.Contains("TexLq", StringComparison.OrdinalIgnoreCase)),
        $"{level.DisplayName}: TexLq is implemented and must not remain a tier blocker.");
    Assert(audit.RuntimeAnimationControlAuditRequired,
        $"{level.DisplayName}: relocation audit failed to expose the separate runtime animation-control integration gate.");

    rows.Add(new AuditRow(
        LevelKey: level.Key,
        LevelName: level.DisplayName,
        Realm: LevelRealmCatalog.TryGetPosition(level, out LevelRealmPosition position) ? position.Realm.DisplayName : "Unknown",
        TargetWadEntry: level.SourceWadEntry,
        TextureRecordCount: slots.Count,
        UsedTextureRecordCount: faceUseCounts.Keys.Count(textureId => textureId >= 0 && textureId < slots.Count),
        HasUnusedTargetRecord: hasUnusedRecord,
        CandidateTargetTextureId: targetTextureId,
        CandidateIsUnused: hasUnusedRecord && unused.Contains(targetTextureId),
        DonorWadEntry: donorLevel.SourceWadEntry,
        DonorTextureId: donorTextureId,
        TexturePagesLength: audit.TexturePagesLength,
        TerrainOwnedByteCount: audit.TerrainOwnedByteCount,
        TerrainFreeByteCount: audit.TerrainFreeByteCount,
        FullyMappedDescriptorCount: audit.FullyMappedTerrainDescriptorCount,
        PartiallyMappedDescriptorCount: audit.PartiallyMappedTerrainDescriptorCount,
        FullyExternalDescriptorCount: audit.FullyExternalTerrainDescriptorCount,
        RequiredAllocationByteCount: audit.RequiredAllocationByteCount,
        RequestedLowDetailDescriptorCount: audit.RequestedLowDetailDescriptorCount,
        RequestedLeadingDescriptorCount: audit.RequestedLeadingDescriptorCount,
        RequestedNormalDescriptorCount: audit.RequestedNormalDescriptorCount,
        RequestedCloseDescriptorCount: audit.RequestedCloseDescriptorCount,
        RequestedClose16DescriptorCount: audit.RequestedClose16DescriptorCount,
        RequestedClose32DescriptorCount: audit.RequestedClose32DescriptorCount,
        RequestedRotatedHqDescriptorCount: audit.RequestedRotatedHqDescriptorCount,
        ProvisionalTerrainOnlyFit: audit.ProvisionalTerrainOnlyFit,
        OwnershipProofComplete: audit.OwnershipProofComplete,
        RuntimeAnimationControlAuditRequired: audit.RuntimeAnimationControlAuditRequired,
        SafetyBlockers: audit.SafetyBlockers,
        Notes: (hasUnusedRecord
            ? "Candidate is an unused decoded terrain record, but final export still requires shared-page ownership and animation-control proof."
            : "No unused decoded terrain record exists. Candidate is record-scoped feasibility only and would affect every face that already uses the record.") +
            " " + string.Join(" ", audit.Notes)));
}

Assert(rows.Count == 35, $"Expected all 35 cataloged levels, got {rows.Count}.");
AuditRow artisans = rows.Single(row => string.Equals(row.LevelKey, "artisans", StringComparison.OrdinalIgnoreCase));
Assert(artisans.CandidateTargetTextureId == 54 && artisans.DonorWadEntry == 70 && artisans.DonorTextureId == 22,
    "The explicit Gnasty's World texture 22 -> Artisans texture 54 audit did not use the expected records.");
Assert(artisans.ProvisionalTerrainOnlyFit,
    $"The complete Gnasty's World texture 22 record did not pass exact all-tier readback and protected-byte verification in Artisans. {artisans.Notes}");

NativeTexturePageExternalOwnershipProof incompleteProof = new(
    TargetWadEntry: artisans.TargetWadEntry,
    TexturePagesLength: artisans.TexturePagesLength,
    TexturePagesSha256: "NOT-A-SOURCE-HASH",
    CoveredScopes: NativeTexturePageConsumerScope.None,
    OwnedRanges: Array.Empty<NativeTexturePageOwnedRange>());
bool unsafeBuild = NativeTerrainTextureRelocationAllocator.TryBuild(
    sourceImagePath,
    artisans.TargetWadEntry,
    [new NativeTerrainTextureRelocationImport(artisans.CandidateTargetTextureId, 70, 22, "both")],
    incompleteProof,
    out NativeTerrainTextureRelocationPlan? unsafePlan,
    out string unsafeReason);
Assert(!unsafeBuild && unsafePlan == null,
    "Incomplete shared-page ownership proof unexpectedly emitted a real-disc relocation plan.");
Assert(unsafeReason.Contains("particle", StringComparison.OrdinalIgnoreCase) &&
       unsafeReason.Contains("actor", StringComparison.OrdinalIgnoreCase) &&
       unsafeReason.Contains("player", StringComparison.OrdinalIgnoreCase) &&
       unsafeReason.Contains("other runtime", StringComparison.OrdinalIgnoreCase) &&
       unsafeReason.Contains("SHA256", StringComparison.OrdinalIgnoreCase),
    "Proof-gate failure did not name every unresolved external-consumer scope and the source hash mismatch.");

NativeTerrainTextureRelocationAudit artisansAudit = NativeTerrainTextureRelocationAllocator.Audit(
    sourceImagePath,
    artisansLevel,
    [new NativeTerrainTextureRelocationImport(54, 70, 22, "both")]);
NativeTexturePageExternalOwnershipProof completeTestProof = BuildCompleteTestProof(artisansAudit);
bool built = NativeTerrainTextureRelocationAllocator.TryBuild(
    sourceImagePath,
    artisansLevel,
    [new NativeTerrainTextureRelocationImport(54, 70, 22, "both")],
    completeTestProof,
    out NativeTerrainTextureRelocationPlan? completePlan,
    out string completeFailure);
Assert(built && completePlan != null, $"Complete proof-gated all-tier plan failed: {completeFailure}");
AssertCompletePlan(completePlan!, artisansAudit, expectedClose16: 16, expectedClose32: 0, minimumRotated: 0);
Assert(completePlan!.AllocatedPixelByteCount == GnastyWorld22PixelBytes &&
       completePlan.AllocatedPaletteByteCount == CompletePaletteBytes,
    $"Gnasty's World texture 22 allocation split changed: expected {GnastyWorld22PixelBytes:N0} pixel and {CompletePaletteBytes:N0} palette bytes.");

NativeTerrainTextureRelocationImport artOnlyMaterialImport = new(
    54,
    70,
    22,
    "both",
    PreserveTargetDescriptorMaterial: true);
bool artOnlyMaterialBuilt = NativeTerrainTextureRelocationAllocator.TryBuild(
    sourceImagePath,
    artisansLevel,
    [artOnlyMaterialImport],
    completeTestProof,
    out NativeTerrainTextureRelocationPlan? artOnlyMaterialPlan,
    out string artOnlyMaterialFailure);
Assert(artOnlyMaterialBuilt && artOnlyMaterialPlan != null,
    $"Art-only target descriptor-material preservation plan failed: {artOnlyMaterialFailure}");
Assert(artOnlyMaterialPlan!.TargetDescriptorMaterialPolicyVerified &&
       artOnlyMaterialPlan.Imports.Single().PreserveTargetDescriptorMaterial,
    "Art-only byte-private relocation did not verify preservation of target ABR/material descriptor controls.");

string focusedCandidateRoot = Path.Combine(outputRoot, "artisans-dragon-metal-research-candidate");
Directory.CreateDirectory(focusedCandidateRoot);
string focusedCandidatePrefix = Path.Combine(focusedCandidateRoot, "SpyroEditor-Artisans-Dragon-Metal-Research");
NativeTexturePageOwnershipReport candidateOwnership =
    NativeTexturePageOwnershipScanner.Scan(sourceImagePath, artisansLevel);
Assert(candidateOwnership.AllConsumerClosureComplete &&
       candidateOwnership.ProvablyPrivatePixelAndClutSpace,
    "The focused candidate lost its complete all-consumer closure or source-bound private-storage proof.");
bool emittedOwnershipProof = NativeTexturePageOwnershipScanner.TryBuildExternalOwnershipProof(
    sourceImagePath,
    artisansLevel,
    out NativeTexturePageOwnershipReport proofReport,
    out NativeTexturePageExternalOwnershipProof? sourceBoundOwnershipProof,
    out string ownershipProofFailure);
Assert(emittedOwnershipProof && sourceBoundOwnershipProof != null &&
       proofReport.AllConsumerClosureComplete,
    $"The complete source-bound Artisans ownership proof was not emitted: {ownershipProofFailure}");
DeleteIfExists($"{focusedCandidatePrefix}.bin");
DeleteIfExists($"{focusedCandidatePrefix}.cue");
DeleteIfExists($"{focusedCandidatePrefix}.native-terrain-texture-relocation.json");
string conservativeCandidateBlock = "";
try
{
    _ = await NativeTerrainTextureRelocationComposer.ExportAsync(
        new NativeTerrainTextureRelocationExportRequest(
            SourceImagePath: sourceImagePath,
            SourceCuePath: Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue"),
            OutputPrefix: focusedCandidatePrefix,
            TargetLevel: artisansLevel,
            Imports: [new NativeTerrainTextureRelocationImport(54, 70, 22, "both")],
            OwnershipProof: sourceBoundOwnershipProof!,
            WriteImage: true));
}
catch (InvalidOperationException exception)
{
    conservativeCandidateBlock = exception.Message;
}
Assert(conservativeCandidateBlock.Contains("No byte-private", StringComparison.OrdinalIgnoreCase),
    "The Artisans byte-private relocation fallback unexpectedly allocated across decoded-consumer or source-nonzero storage instead of deferring to the proven target-owned in-place strategy.");
Assert(!File.Exists($"{focusedCandidatePrefix}.bin") && !File.Exists($"{focusedCandidatePrefix}.cue"),
    "A stale unsafe focused candidate survived the conservative ownership failure.");

foreach (string partialTier in new[] { "hqData", "hqDataClose" })
{
    bool auditRejected = false;
    try
    {
        _ = NativeTerrainTextureRelocationAllocator.Audit(
            sourceImagePath,
            artisansLevel,
            [new NativeTerrainTextureRelocationImport(54, 70, 22, partialTier)]);
    }
    catch (InvalidOperationException exception)
    {
        auditRejected = exception.Message.Contains("runtime-incomplete", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("TexLq", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("leading", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("normal", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("close", StringComparison.OrdinalIgnoreCase);
    }
    Assert(auditRejected, $"Partial audit tier {partialTier} was not rejected as runtime-incomplete.");

    bool partialBuilt = NativeTerrainTextureRelocationAllocator.TryBuild(
        sourceImagePath,
        artisansLevel,
        [new NativeTerrainTextureRelocationImport(54, 70, 22, partialTier)],
        completeTestProof,
        out NativeTerrainTextureRelocationPlan? partialPlan,
        out string partialFailure);
    Assert(!partialBuilt && partialPlan == null &&
           partialFailure.Contains("runtime-incomplete", StringComparison.OrdinalIgnoreCase),
        $"Partial build tier {partialTier} was not hard-blocked before plan emission.");
}

NativeTerrainTextureRelocationPlan close32Plan = BuildFocusedPlan(
    sourceImagePath,
    artisansLevel,
    targetTextureId: 55,
    donorWadEntry: artisansLevel.SourceWadEntry,
    donorTextureId: 0,
    out NativeTerrainTextureRelocationAudit close32Audit);
Assert(close32Audit.RequestedClose16DescriptorCount == 0 && close32Audit.RequestedClose32DescriptorCount == 16,
    "The retail close-HQ 32x32 fallback/alias form was not derived from descriptor extents.");
AssertCompletePlan(close32Plan, close32Audit, expectedClose16: 0, expectedClose32: 16, minimumRotated: 0);

NativeTerrainTextureRelocationPlan rotatedPlan = BuildFocusedPlan(
    sourceImagePath,
    artisansLevel,
    targetTextureId: 56,
    donorWadEntry: artisansLevel.SourceWadEntry,
    donorTextureId: 5,
    out NativeTerrainTextureRelocationAudit rotatedAudit);
Assert(rotatedAudit.RequestedRotatedHqDescriptorCount > 0,
    "The focused rotated retail texture did not expose any oriented HQ descriptors.");
AssertCompletePlan(
    rotatedPlan,
    rotatedAudit,
    expectedClose16: rotatedAudit.RequestedClose16DescriptorCount,
    expectedClose32: rotatedAudit.RequestedClose32DescriptorCount,
    minimumRotated: 1);

string sourceSha256After = ComputeSha256(sourceImagePath);
Assert(string.Equals(sourceSha256Before, sourceSha256After, StringComparison.Ordinal),
    "The source disc changed while building read-only relocation plans.");

string jsonPath = Path.Combine(outputRoot, "terrain-texture-relocation-readiness.json");
string markdownPath = Path.Combine(outputRoot, "terrain-texture-relocation-readiness.md");
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    SourceImageSha256 = sourceSha256After,
    Mapping = new
    {
        RecordLayout = "2 TexLq + 1 byte-identical leading HQ/sprite alias + 4 normal TexHq + 16 close TexHq = 23 descriptors",
        LqPixelFormat = "4bpp indexed, logical 32x32, packed 16 bytes x 32 rows",
        LqPalette = "16 distance-shading CLUT rows x 32 bytes at 0x400-byte row stride",
        LqAlias = "TexLq0, TexLq1, and leading HQ/sprite descriptor remain byte-identical and share one relocated allocation",
        HqPixelFormat = "8bpp indexed for both normal and close TexHq",
        HqPixelAddress = "packedOffset = y * 0x400 + (full8bppByteX - 0x400)",
        HqPaletteAddress = "packedOffset = clutY * 0x400 + (clutX - 512) * 2",
        NormalDescriptorShape = "4 x 32x32 pixels, 512-byte CLUT each",
        CloseDescriptorShape = "16 x side-by-side descriptors; side=max(abs(xmax-xmin),abs(ymax-ymin))+1; proven sides 16 and 32",
        GnastyWorld22AllocationBytes = GnastyWorld22RequiredBytes,
        RuntimeIntegrationGate = "Reject target texture IDs mutated by level texture-animation or scrolling tables"
    },
    FocusedPlans = new
    {
        GnastyWorld22ToArtisans54 = SummarizePlan(completePlan!),
        Close32Fallback = SummarizePlan(close32Plan),
        RotatedHq = SummarizePlan(rotatedPlan)
    },
    Rows = rows
}, new JsonSerializerOptions { WriteIndented = true }));
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(rows, artisans, completePlan!, close32Plan, rotatedPlan));

Console.WriteLine($"Native terrain texture relocation smoke passed for {rows.Count} levels.");
Console.WriteLine($"Complete Artisans plan: {completePlan!.RewrittenDescriptorCount} descriptors; {completePlan.AllocatedPixelByteCount:N0} pixel bytes; {completePlan.AllocatedPaletteByteCount:N0} palette bytes.");
Console.WriteLine($"Close forms proved: 16x16={completePlan.RelocatedClose16DescriptorCount}; 32x32={close32Plan.RelocatedClose32DescriptorCount}; rotated={rotatedPlan.RelocatedRotatedHqDescriptorCount}.");
Console.WriteLine("Focused Artisans relocation fallback: correctly withheld because its source-bound private holes cannot fit the complete donor; the proven target-owned in-place strategy handles this pair.");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");

static void AssertCompleteAuditShape(NativeTerrainTextureRelocationAudit audit, string label)
{
    Assert(audit.RequestedLowDetailDescriptorCount == LowDetailDescriptorCount &&
           audit.RequestedLeadingDescriptorCount == LeadingDescriptorCount &&
           audit.RequestedNormalDescriptorCount == NormalDescriptorCount &&
           audit.RequestedCloseDescriptorCount == CloseDescriptorCount,
        $"{label}: audit did not account for the complete 2+1+4+16 descriptor record.");
    Assert(audit.RequestedNormal32DescriptorCount == NormalDescriptorCount,
        $"{label}: normal HQ descriptors were not all decoded as 8-bpp 32x32.");
    Assert(audit.RequestedClose16DescriptorCount + audit.RequestedClose32DescriptorCount == CloseDescriptorCount,
        $"{label}: close HQ descriptors were not all classified into proven 8-bpp 16x16/32x32 forms.");
}

static NativeTexturePageExternalOwnershipProof BuildCompleteTestProof(NativeTerrainTextureRelocationAudit audit) => new(
    TargetWadEntry: audit.TargetWadEntry,
    TexturePagesLength: audit.TexturePagesLength,
    TexturePagesSha256: audit.TexturePagesSha256,
    CoveredScopes: NativeTexturePageConsumerScope.RequiredExternalConsumers,
    OwnedRanges: Array.Empty<NativeTexturePageOwnedRange>());

static NativeTerrainTextureRelocationPlan BuildFocusedPlan(
    string sourceImagePath,
    LevelDefinition targetLevel,
    int targetTextureId,
    int donorWadEntry,
    int donorTextureId,
    out NativeTerrainTextureRelocationAudit audit)
{
    NativeTerrainTextureRelocationImport import = new(targetTextureId, donorWadEntry, donorTextureId, "both");
    audit = NativeTerrainTextureRelocationAllocator.Audit(sourceImagePath, targetLevel, [import]);
    AssertCompleteAuditShape(audit, $"WAD {donorWadEntry} texture {donorTextureId}");
    Assert(audit.ProvisionalTerrainOnlyFit,
        $"Focused complete-record audit failed for WAD {donorWadEntry} texture {donorTextureId}: {string.Join(" ", audit.Notes)}");
    bool built = NativeTerrainTextureRelocationAllocator.TryBuild(
        sourceImagePath,
        targetLevel,
        [import],
        BuildCompleteTestProof(audit),
        out NativeTerrainTextureRelocationPlan? plan,
        out string failure);
    Assert(built && plan != null,
        $"Focused complete-record plan failed for WAD {donorWadEntry} texture {donorTextureId}: {failure}");
    return plan!;
}

static void AssertCompletePlan(
    NativeTerrainTextureRelocationPlan plan,
    NativeTerrainTextureRelocationAudit audit,
    int expectedClose16,
    int expectedClose32,
    int minimumRotated)
{
    Assert(plan.RewrittenDescriptorCount == CompleteDescriptorCount &&
           plan.RewrittenLowDetailDescriptorCount == LowDetailDescriptorCount &&
           plan.RewrittenLeadingDescriptorCount == LeadingDescriptorCount &&
           plan.RewrittenNormalDescriptorCount == NormalDescriptorCount &&
           plan.RewrittenCloseDescriptorCount == CloseDescriptorCount,
        "Plan was emitted without rewriting the complete 2+1+4+16 descriptor record.");
    Assert(plan.RelocatedNormal32DescriptorCount == NormalDescriptorCount &&
           plan.RelocatedClose16DescriptorCount == expectedClose16 &&
           plan.RelocatedClose32DescriptorCount == expectedClose32 &&
           plan.RelocatedRotatedHqDescriptorCount >= minimumRotated,
        "Plan shape/orientation accounting does not match its audited donor descriptors.");
    Assert(plan.AllocatedPixelByteCount + plan.AllocatedPaletteByteCount == audit.RequiredAllocationByteCount,
        "Allocated pixel/palette storage does not equal the audited exact requirement.");
    Assert(plan.ExactDonorIndexedPixelsVerified && plan.ExactDonorPalettesVerified &&
           plan.LowDetailAliasPreserved && plan.LogicalReadbackVerified && plan.ProtectedStorageVerified &&
           plan.TargetDescriptorMaterialPolicyVerified,
        "Plan did not prove exact all-tier donor readback, the retail LQ alias, protected storage, and target descriptor-material policy.");
    Assert(plan.VerifiedProtectedDescriptorCount == (plan.TargetTextureCount - 1) * CompleteDescriptorCount,
        "Plan did not verify every descriptor in every non-target texture record.");
    Assert(plan.RuntimeAnimationControlAuditRequired,
        "Plan failed to expose the separate runtime animation-control integration gate.");
    Assert(plan.Patches.Count > 0 && plan.Patches.All(patch =>
            patch.ByteLength > 0 && patch.Before.Length == patch.ByteLength &&
            patch.After.Length == patch.ByteLength && !patch.Before.SequenceEqual(patch.After)),
        "Plan contains an empty, malformed, or no-op patch.");
}

static object SummarizePlan(NativeTerrainTextureRelocationPlan plan) => new
{
    plan.TargetWadEntry,
    plan.RewrittenDescriptorCount,
    plan.RewrittenLowDetailDescriptorCount,
    plan.RewrittenLeadingDescriptorCount,
    plan.RewrittenNormalDescriptorCount,
    plan.RewrittenCloseDescriptorCount,
    plan.RelocatedClose16DescriptorCount,
    plan.RelocatedClose32DescriptorCount,
    plan.RelocatedRotatedHqDescriptorCount,
    plan.AllocatedPixelByteCount,
    plan.AllocatedPaletteByteCount,
    plan.VerifiedProtectedDescriptorCount,
    plan.ExactDonorIndexedPixelsVerified,
    plan.ExactDonorPalettesVerified,
    plan.LowDetailAliasPreserved,
    plan.ProtectedStorageVerified,
    plan.TargetDescriptorMaterialPolicyVerified,
    plan.RuntimeAnimationControlAuditRequired
};

static string ComputeSha256(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static void DeleteIfExists(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static Dictionary<int, int> LoadFaceTextureUseCounts(string workspaceRoot, string levelKey)
{
    string overlayPath = Path.Combine(workspaceRoot, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
    if (!File.Exists(overlayPath))
        return [];
    using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(overlayPath));
    Dictionary<int, int> counts = [];
    if (!document.RootElement.TryGetProperty("candidates", out JsonElement candidates) || candidates.ValueKind != JsonValueKind.Array)
        return counts;
    foreach (JsonElement candidate in candidates.EnumerateArray())
    {
        if (!candidate.TryGetProperty("polygons", out JsonElement polygons) || polygons.ValueKind != JsonValueKind.Array)
            continue;
        foreach (JsonElement polygon in polygons.EnumerateArray())
        {
            if (!polygon.TryGetProperty("textureId", out JsonElement textureElement) || !textureElement.TryGetInt32(out int textureId))
                continue;
            counts[textureId] = counts.TryGetValue(textureId, out int count) ? count + 1 : 1;
        }
    }
    return counts;
}

static int ChooseLeastUsedTexture(int textureCount, IReadOnlyDictionary<int, int> counts) =>
    Enumerable.Range(0, textureCount)
        .OrderBy(textureId => counts.TryGetValue(textureId, out int count) ? count : 0)
        .ThenBy(textureId => textureId)
        .First();

static string BuildMarkdown(
    IReadOnlyList<AuditRow> rows,
    AuditRow artisans,
    NativeTerrainTextureRelocationPlan completePlan,
    NativeTerrainTextureRelocationPlan close32Plan,
    NativeTerrainTextureRelocationPlan rotatedPlan)
{
    StringBuilder text = new();
    text.AppendLine("# Native terrain texture relocation readiness");
    text.AppendLine();
    text.AppendLine("The allocator now moves the complete native 23-descriptor record atomically: two 4-bpp TexLq rows, the byte-identical leading HQ/sprite alias, four 8-bpp 32x32 normal TexHq rows, and sixteen 8-bpp close TexHq rows. TexLq preserves all sixteen 16-color distance-palette rows. Normal and close HQ preserve exact indexed pixels and complete 512-byte CLUTs.");
    text.AppendLine();
    text.AppendLine("Close tile side is derived as `max(abs(xmax-xmin), abs(ymax-ymin)) + 1`. Retail 16x16 and 32x32 forms are supported; rotated descriptors use the same orientation matrices with `side - 1` negative-axis adjustment. Any other shape is blocked. Partial `hqData` and `hqDataClose` requests are rejected before a plan can be emitted.");
    text.AppendLine();
    text.AppendLine($"Gnasty's World texture 22 -> Artisans texture 54 rewrites {completePlan.RewrittenDescriptorCount} descriptors using {completePlan.AllocatedPixelByteCount:N0} pixel bytes and {completePlan.AllocatedPaletteByteCount:N0} palette bytes. Focused proof also covered {close32Plan.RelocatedClose32DescriptorCount} close 32x32 fallback descriptors and {rotatedPlan.RelocatedRotatedHqDescriptorCount} rotated HQ descriptors.");
    text.AppendLine();
    text.AppendLine("Real-disc export remains shared-page proof-gated. Integration must also reject target IDs controlled by runtime texture-animation or scrolling tables, because those loops overwrite descriptor records after load.");
    text.AppendLine();
    text.AppendLine($"Explicit example provisional fit: **{artisans.ProvisionalTerrainOnlyFit}**. Unused Artisans record: **{artisans.CandidateIsUnused}**. All-consumer proof ready without supplied proof: **{artisans.OwnershipProofComplete}**.");
    text.AppendLine();
    text.AppendLine("| Realm | Level | Records | Used | Candidate | Complete bytes | 16px close | 32px close | Terrain-only fit | Safe proof | Terrain free / 0x80000 |");
    text.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|");
    foreach (AuditRow row in rows)
    {
        text.AppendLine($"| {Escape(row.Realm)} | {Escape(row.LevelName)} | {row.TextureRecordCount} | {row.UsedTextureRecordCount} | {row.CandidateTargetTextureId} | {row.RequiredAllocationByteCount:N0} | {row.RequestedClose16DescriptorCount} | {row.RequestedClose32DescriptorCount} | {(row.ProvisionalTerrainOnlyFit ? "yes" : "no")} | {(row.OwnershipProofComplete ? "yes" : "blocked")} | {row.TerrainFreeByteCount:N0} |");
    }
    text.AppendLine();
    text.AppendLine("## Safety blockers without an external proof");
    text.AppendLine();
    foreach (string blocker in artisans.SafetyBlockers)
        text.AppendLine($"- {blocker}");
    return text.ToString();
}

static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record AuditRow(
    string LevelKey,
    string LevelName,
    string Realm,
    int TargetWadEntry,
    int TextureRecordCount,
    int UsedTextureRecordCount,
    bool HasUnusedTargetRecord,
    int CandidateTargetTextureId,
    bool CandidateIsUnused,
    int DonorWadEntry,
    int DonorTextureId,
    int TexturePagesLength,
    int TerrainOwnedByteCount,
    int TerrainFreeByteCount,
    int FullyMappedDescriptorCount,
    int PartiallyMappedDescriptorCount,
    int FullyExternalDescriptorCount,
    int RequiredAllocationByteCount,
    int RequestedLowDetailDescriptorCount,
    int RequestedLeadingDescriptorCount,
    int RequestedNormalDescriptorCount,
    int RequestedCloseDescriptorCount,
    int RequestedClose16DescriptorCount,
    int RequestedClose32DescriptorCount,
    int RequestedRotatedHqDescriptorCount,
    bool ProvisionalTerrainOnlyFit,
    bool OwnershipProofComplete,
    bool RuntimeAnimationControlAuditRequired,
    IReadOnlyList<string> SafetyBlockers,
    string Notes);
