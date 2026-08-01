using System.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(
    workspaceRoot,
    "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(
    workspaceRoot,
    "Spyro the Dragon (USA).cue");
string wadAnalysisPath = Path.Combine(
    workspaceRoot,
    "spyro-wad-analysis.json");
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException(
        "Artisans is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException(
        "Gnasty's World is missing from the level catalog.");
NativeTerrainTexturePrivateRecordStagingSourceProof stagingSourceProof =
    NativeTerrainTexturePrivateRecordStagingSourceProof.Capture(
        sourceImagePath,
        artisans);
NativeTerrainTextureRecordAppendSourceBinding binding =
    stagingSourceProof.SourceBinding;

NativeTerrainTextureRelocationEdit first = BuildEdit(
    binding,
    gnastysWorld,
    donorTextureId: 17,
    materialTemplateTextureId: 55,
    targetTextureId: binding.ExpectedSourceTextureCount);
NativeTerrainTextureRelocationEdit second = BuildEdit(
    binding,
    gnastysWorld,
    donorTextureId: 18,
    materialTemplateTextureId: 54,
    targetTextureId: binding.ExpectedSourceTextureCount + 1);
NativeTerrainTexturePrivateRecordBatchRequest baseRequest = new(
    sourceImagePath,
    sourceCuePath,
    Path.Combine(
        workspaceRoot,
        "_local",
        "research",
        "source-analysis-session-cache",
        "not-exported"),
    wadAnalysisPath,
    artisans,
    SavedEdits: [first],
    OrdinaryLevelDataPatches: []);

NativeTerrainTextureSourceAnalysisSessionCache.ClearForTesting();
Stopwatch firstTimer = Stopwatch.StartNew();
Assert(
    NativeTerrainTexturePrivateRecordBatchCompiler.TryBuildStagingPreflight(
        baseRequest,
        stagingSourceProof,
        out NativeTerrainTexturePrivateRecordStagingPreflightResult? oneRow,
        out string firstFailure) &&
    oneRow != null,
    $"The first exact source-bound private-row plan failed: {firstFailure}");
firstTimer.Stop();

NativeTerrainTextureSourceAnalysisCacheStats afterFirst =
    NativeTerrainTextureSourceAnalysisSessionCache.SnapshotForTesting();
Assert(
    afterFirst.RuntimeAuditBuildCount == 1 &&
    afterFirst.RuntimeAuditHitCount == 1 &&
    afterFirst.OwnershipProofBuildCount == 1 &&
    afterFirst.OwnershipProofHitCount == 0,
    "The first plan did not collapse its duplicate runtime audit to one source-bound build.");

Stopwatch secondTimer = Stopwatch.StartNew();
Assert(
    NativeTerrainTexturePrivateRecordBatchCompiler.TryBuildStagingPreflight(
        baseRequest with { SavedEdits = [first, second] },
        stagingSourceProof,
        out NativeTerrainTexturePrivateRecordStagingPreflightResult? twoRows,
        out string secondFailure) &&
    twoRows != null,
    $"The second unique private-row plan failed: {secondFailure}");
secondTimer.Stop();

NativeTerrainTextureSourceAnalysisCacheStats afterSecond =
    NativeTerrainTextureSourceAnalysisSessionCache.SnapshotForTesting();
Assert(
    afterSecond.RuntimeAuditBuildCount == 1 &&
    afterSecond.RuntimeAuditHitCount == 3 &&
    afterSecond.OwnershipProofBuildCount == 1 &&
    afterSecond.OwnershipProofHitCount == 1 &&
    afterSecond.RuntimeAuditEntryCount == 1 &&
    afterSecond.OwnershipProofEntryCount == 1,
    "The second unique plan did not reuse the exact source-bound runtime and ownership analyses.");
Assert(
    oneRow!.SourceBinding == binding &&
    oneRow.AppendedPrivateEditCount == 1 &&
    twoRows!.SourceBinding == binding &&
    twoRows.AppendedPrivateEditCount == 2,
    "The source-analysis cache changed the private-row plan identities.");

NativeTerrainTextureRecordAppendSourceBinding changedFingerprint =
    binding with
    {
        ExpectedLevelDataSha256 =
            binding.ExpectedLevelDataSha256[..63] +
            (binding.ExpectedLevelDataSha256[^1] == '0' ? "1" : "0")
    };
_ = NativeTerrainTextureSourceAnalysisSessionCache.InspectRuntime(
    sourceImagePath,
    artisans,
    changedFingerprint,
    out bool changedFingerprintHit);
NativeTerrainTextureSourceAnalysisCacheStats afterChangedFingerprint =
    NativeTerrainTextureSourceAnalysisSessionCache.SnapshotForTesting();
Assert(
    !changedFingerprintHit &&
    afterChangedFingerprint.RuntimeAuditBuildCount == 2,
    "A changed exact level-data fingerprint reused the previous session audit.");

string currentPagesSha256 = new('A', 64);
string changedPagesSha256 = new('B', 64);
Assert(
    NativeTerrainTextureGlobalRepackerResearch
        .TryValidateOwnershipTexturePagesFingerprint(
            currentPagesSha256,
            currentPagesSha256.ToLowerInvariant(),
            out string matchingPagesFailure) &&
    string.IsNullOrEmpty(matchingPagesFailure),
    "The ownership-page fingerprint check rejected the same exact SHA-256.");
Assert(
    !NativeTerrainTextureGlobalRepackerResearch
        .TryValidateOwnershipTexturePagesFingerprint(
            currentPagesSha256,
            changedPagesSha256,
            out string changedPagesFailure) &&
    changedPagesFailure.Contains(
        "changed after",
        StringComparison.OrdinalIgnoreCase),
    "A same-size/current-source texture-page hash change was not rejected.");

Console.WriteLine("Source-analysis session cache smoke passed.");
Console.WriteLine(
    $"- cold one-row staging preflight: {firstTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine(
    $"- unique two-row staging preflight with source analysis cached: {secondTimer.Elapsed.TotalSeconds:F2}s");
Console.WriteLine(
    "- exact full-BIN, texture-component, and level-data fingerprints key every cached analysis");
Console.WriteLine(
    "- current target texture-page SHA must still match the cached ownership proof exactly");
Console.WriteLine(
    "- final candidate runtime inspection and final-BIN readback remain uncached");

static NativeTerrainTextureRelocationEdit BuildEdit(
    NativeTerrainTextureRecordAppendSourceBinding binding,
    LevelDefinition donor,
    int donorTextureId,
    int materialTemplateTextureId,
    int targetTextureId) =>
    new(
        targetTextureId,
        donor.Key,
        donor.DisplayName,
        donor.SourceWadEntry,
        donorTextureId,
        NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
            donor.Key,
            donorTextureId),
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-26T00:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        binding.TargetWadEntry,
        binding.SourceImageSha256,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        materialTemplateTextureId,
        NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(
            targetTextureId));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
