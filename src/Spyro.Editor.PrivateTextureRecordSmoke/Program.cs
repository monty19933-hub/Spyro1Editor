using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int DonorTextureId = 17;
const int ExpectedSourceTextureCount = 68;
const int ExpectedAppendedTextureId = 68;
const int ExpectedRecordGrowthBytes = 184;
const int SharedEyeTextureId = 55;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "private-terrain-texture-record");
Directory.CreateDirectory(outputRoot);
foreach (string stale in Directory.EnumerateFiles(outputRoot))
    File.Delete(stale);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");

NativeTerrainTextureRecordAppendResearchRequest request = new(
    sourceImagePath,
    sourceCuePath,
    artisans,
    gnastysWorld,
    DonorTextureId);
bool built = NativeTerrainTextureRecordAppendResearch.TryBuild(
    request,
    out NativeTerrainTextureRecordAppendResearchPlan? candidatePlan,
    out string buildFailure);
Assert(built && candidatePlan != null, $"Private-record structural proof failed: {buildFailure}");
NativeTerrainTextureRecordAppendResearchPlan plan = candidatePlan!;
Assert(plan.SourceTextureCount == ExpectedSourceTextureCount &&
       plan.OutputTextureCount == ExpectedSourceTextureCount + 1 &&
       plan.AppendedTextureId == ExpectedAppendedTextureId &&
       plan.GrowthByteCount == ExpectedRecordGrowthBytes,
    "Artisans did not produce the expected retail 68->69 table with appended T68.");
Assert(plan.ExistingLowDetailRowsPreserved &&
       plan.ExistingHighDetailRowsPreserved &&
       plan.DonorRowsCopiedExactly &&
       plan.SuffixPreservedExactly &&
       plan.OutputZeroTailVerified &&
       plan.OutputComponentChainReparsed &&
       plan.AppendedTargetRuntimePersistent,
    "The structural plan omitted a table, suffix, tail, component-chain, or runtime-target invariant.");
Assert(plan.Components.Count >= 7 &&
       plan.Components.All(component =>
           component.OutputOffset == component.SourceOffset + ExpectedRecordGrowthBytes),
    "A later native level-data component did not shift by exactly one record.");

string structuralPrefix = Path.Combine(outputRoot, "Artisans-Append-T68-STRUCTURAL-DO-NOT-RUN");
NativeTerrainTextureRecordAppendResearchOutput structural =
    await NativeTerrainTextureRecordAppendResearch.WriteCandidateAsync(request, plan, structuralPrefix);
Assert(structural.ExactReadbackVerified && File.Exists(structural.OutputImagePath),
    "Structural research candidate write/readback failed.");
IReadOnlyList<TerrainTextureSlot> decoded = TerrainPatchExporter.InspectTextureSlots(
    structural.OutputImagePath,
    artisans);
Assert(decoded.Count == ExpectedSourceTextureCount + 1 && decoded[^1].TextureId == ExpectedAppendedTextureId,
    "Normal texture-table inspection did not decode appended T68.");
NativeTerrainTextureRuntimeControlAudit runtime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(structural.OutputImagePath, artisans);
Assert(runtime.Complete && runtime.TextureCount == ExpectedSourceTextureCount + 1 &&
       runtime.IsRuntimePersistentTarget(ExpectedAppendedTextureId),
    "Expanded T68 failed the normal runtime-control audit.");

NativeTerrainTextureRelocationImport privateImport = new(
    ExpectedAppendedTextureId,
    gnastysWorld.SourceWadEntry,
    DonorTextureId,
    "both");
bool privateProofBuilt = NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
    structural.OutputImagePath,
    artisans,
    [ExpectedAppendedTextureId],
    out NativeTexturePageRelocationOwnershipProofResult? privateProof,
    out string privateProofFailure);
Assert(privateProofBuilt && privateProof != null,
    $"All-consumer ownership proof for appended T68 failed before allocation: {privateProofFailure}");
NativeTerrainTextureRelocationAudit privateAudit = NativeTerrainTextureRelocationAllocator.Audit(
    structural.OutputImagePath,
    artisans,
    [privateImport],
    privateProof!.Proof);
bool privateAllocationBuilt = NativeTerrainTextureRelocationAllocator.TryBuild(
    structural.OutputImagePath,
    artisans,
    [privateImport],
    privateProof.Proof,
    out NativeTerrainTextureRelocationPlan? privateAllocation,
    out string privateAllocationFailure);
Assert(!privateAllocationBuilt && privateAllocation == null &&
       privateAllocationFailure.Contains("No byte-private, descriptor-encodable 32-byte x 32-row normal HQ pixel rectangle remains", StringComparison.Ordinal),
    $"The expected atomic page-storage block was absent or changed: {privateAllocationFailure}");

NativeTerrainTextureRelocationImport preserveSharedEye = new(
    SharedEyeTextureId,
    artisans.SourceWadEntry,
    SharedEyeTextureId,
    "both",
    PreserveTargetDescriptorMaterial: true);
bool jointProofBuilt = NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
    structural.OutputImagePath,
    artisans,
    [SharedEyeTextureId, ExpectedAppendedTextureId],
    out NativeTexturePageRelocationOwnershipProofResult? jointProof,
    out string jointProofFailure);
Assert(jointProofBuilt && jointProof != null,
    $"Joint T55/T68 ownership proof failed before allocation: {jointProofFailure}");
bool jointAllocationBuilt = NativeTerrainTextureRelocationAllocator.TryBuild(
    structural.OutputImagePath,
    artisans,
    [preserveSharedEye, privateImport],
    jointProof!.Proof,
    out NativeTerrainTextureRelocationPlan? jointAllocation,
    out string jointAllocationFailure);
Assert(!jointAllocationBuilt && jointAllocation == null &&
       jointAllocationFailure.Contains("No byte-private, descriptor-encodable 32-byte x 32-row normal HQ pixel rectangle remains", StringComparison.Ordinal),
    $"Relocating the unchanged shared eye record unexpectedly bypassed or changed the atomic storage block: {jointAllocationFailure}");

Assert(Sha256File(sourceImagePath).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) &&
       new FileInfo(sourceImagePath).Length == sourceLength,
    "Research proof modified the retail source BIN.");

string reportPath = Path.Combine(outputRoot, "artisans-private-texture-record-static-proof.json");
string markdownPath = Path.Combine(outputRoot, "artisans-private-texture-record-static-proof.md");
var report = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = true,
    Result = "structure-proven-page-storage-blocked",
    Target = new
    {
        Level = artisans.DisplayName,
        artisans.SourceWadEntry,
        OldTextureCount = plan.SourceTextureCount,
        NewTextureCount = plan.OutputTextureCount,
        AppendedTextureId = plan.AppendedTextureId
    },
    Donor = new
    {
        Level = gnastysWorld.DisplayName,
        gnastysWorld.SourceWadEntry,
        TextureId = DonorTextureId
    },
    Structure = new
    {
        plan.GrowthByteCount,
        plan.SourceTextureComponentByteLength,
        plan.OutputTextureComponentByteLength,
        plan.SourceUsedByteLength,
        plan.OutputUsedByteLength,
        plan.SourceZeroTailByteCount,
        plan.OutputZeroTailByteCount,
        plan.PreservedSuffixSha256,
        plan.ExistingLowDetailRowsPreserved,
        plan.ExistingHighDetailRowsPreserved,
        plan.DonorRowsCopiedExactly,
        plan.SuffixPreservedExactly,
        plan.OutputZeroTailVerified,
        plan.OutputComponentChainReparsed,
        Components = plan.Components
    },
    PageStorage = new
    {
        privateAudit.TexturePagesLength,
        privateAudit.TerrainOwnedByteCount,
        privateAudit.TerrainFreeByteCount,
        privateAudit.RequiredAllocationByteCount,
        privateAudit.ProvisionalTerrainOnlyFit,
        PrivateT68Failure = privateAllocationFailure,
        PreserveT55AndAddT68Failure = jointAllocationFailure,
        Atomic = privateAllocation == null && jointAllocation == null
    },
    NextRequiredStep = new
    {
        Name = "global alias-preserving texture-page repacker",
        Requirements = new[]
        {
            "Repack the complete terrain storage union around fixed external-consumer ranges, not one target record at a time.",
            "Preserve every existing logical indexed pixel, CLUT, LQ alias, descriptor material bit, and runtime animation/scroll target while leaving every non-terrain consumer byte untouched.",
            "Produce at least one descriptor-encodable byte-private 32x32 rectangle plus required CLUT/palette storage.",
            "If fixed external ranges make that impossible, separately decode and prove any external descriptor relocation before moving one byte.",
            "Pass full source-bound protected-storage and final logical readback, then DuckStation runtime proof before UI promotion."
        }
    },
    StructuralArtifact = new
    {
        BinPath = structural.OutputImagePath,
        CuePath = structural.OutputCuePath,
        Warning = "Static structural artifact only. T68 donor page storage was not allocated; do not use as a gameplay candidate."
    },
    Source = new
    {
        Path = sourceImagePath,
        Length = sourceLength,
        Sha256 = sourceSha256,
        Unchanged = true
    },
    RuntimeStatus = "DuckStation proof cannot begin until the page repacker produces a complete non-blocked art allocation."
};
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
await File.WriteAllTextAsync(markdownPath, $"""
    # Artisans Private Terrain Texture Record Research

    Status: **STRUCTURE PROVEN; PAGE STORAGE BLOCKED ATOMICALLY**

    - Texture table: {plan.SourceTextureCount}->{plan.OutputTextureCount}; appended id T{plan.AppendedTextureId}.
    - Exact structural growth: {plan.GrowthByteCount} bytes (16-byte LQ + 168-byte HQ).
    - Later native components: {plan.Components.Count} reparsed at old offset +{plan.GrowthByteCount}; every component payload remained byte-identical.
    - Verified zero tail: {plan.SourceZeroTailByteCount:N0}->{plan.OutputZeroTailByteCount:N0} bytes.
    - Normal table and runtime-control scanners both accept the expanded 69-record layout; T68 is not animation/scroll controlled.
    - Gnasty's World T{DonorTextureId} allocation was rejected before any page patch: `{privateAllocationFailure}`
    - Preserving/repacking the existing shared Artisans T{SharedEyeTextureId} alongside T68 was also rejected atomically: `{jointAllocationFailure}`

    The table itself is therefore not the blocker. Artisans has {privateAudit.TerrainFreeByteCount:N0} terrain-unclaimed bytes, but fixed external ownership and native descriptor geometry leave no legal byte-private 32x32 rectangle. Making selected-section painting generally available requires a global alias-preserving repacker that repacks the complete terrain union around fixed external ranges and proves every terrain/runtime-control consumer, not another unsafe per-record fallback. External bytes must remain untouched unless their descriptor roots are separately decoded and proven.

    DuckStation proof is still required after that repacker produces a complete candidate. The retained structural BIN/CUE is **not** a gameplay candidate because T68 art storage was intentionally not written.
    """);

Console.WriteLine("Artisans private texture record research passed its expected boundary checks.");
Console.WriteLine($"Structure: {plan.SourceTextureCount}->{plan.OutputTextureCount}, +{plan.GrowthByteCount} bytes, suffix and zero tail exact.");
Console.WriteLine($"Page allocation: BLOCKED ATOMICALLY — {privateAllocationFailure}");
Console.WriteLine($"Report: {reportPath}");
Console.WriteLine("Next required implementation: global alias-preserving texture-page repacker; then DuckStation runtime proof.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}
