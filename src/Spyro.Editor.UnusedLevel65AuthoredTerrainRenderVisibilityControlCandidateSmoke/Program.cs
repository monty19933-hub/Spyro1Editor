using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

const string BaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string ExecutableSha256 =
    "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
const string DisplayEvidenceId =
    "unused-level-65-town-square-display-name-focused-pass-duckstation-2026-08-08";
const string DisplayEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-display-name-focused-pass-2026-08-08.json";
const string RejectedEvidenceId =
    "unused-level-65-town-square-authored-terrain-add-copy-not-observed-duckstation-2026-08-08";
const string RejectedEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-add-copy-not-observed-2026-08-08.json";
const string RejectedProfileId =
    "unused-level-65-town-square-authored-terrain-add-copy-clean-usa-disposable-v1";
const string RejectedImageSha256 =
    "db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f";
const string CandidatePrefix =
    "Unused-Level-65-Town-Square-authored-terrain-render-visibility-control-RUNTIME-CANDIDATE";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string basePrefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";
Require(File.Exists(baseImage) && File.Exists(baseCue),
    "The exact focused-runtime-passed display-name base BIN/CUE is missing.");
Require(await HashFileAsync(baseImage) == BaseImageSha256,
    "The renderer control base is not the exact focused-runtime-passed display-name BIN.");

string displayEvidenceAbsolutePath = Path.Combine(repositoryRoot, DisplayEvidencePath);
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(displayEvidenceAbsolutePath)))
{
    JsonElement root = evidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() == DisplayEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        root.GetProperty("outputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        !root.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean(),
        "The renderer control lost its exact unpromoted display-name runtime-pass base.");
}

string rejectedEvidenceAbsolutePath = Path.Combine(repositoryRoot, RejectedEvidencePath);
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(rejectedEvidenceAbsolutePath)))
{
    JsonElement root = evidence.RootElement;
    JsonElement defect = root.GetProperty("staticSafetyDefect");
    Require(
        root.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() ==
            "runtime-rejected-visibility-discriminator-not-observed" &&
        root.GetProperty("profileId").GetString() == RejectedProfileId &&
        root.GetProperty("baseOutputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        defect.GetProperty("sourceSectorEndExclusive").GetString() == "0x6A3C4F8" &&
        defect.GetProperty("followingSectorWadOffset").GetString() == "0x6A3C4F8" &&
        defect.GetProperty("repackPatchEndExclusive").GetString() == "0x6A3C514" &&
        defect.GetProperty("overlapByteLength").GetInt32() == 28,
        "The renderer control lost the exact rejected-v1 evidence and 28-byte overlap boundary.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-authored-terrain-render-visibility-control");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(outputRoot, CandidatePrefix);
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";

await WriteTextAtomicallyAsync(staticProofPath, "{\"stale\":true}\n");
await WriteTextAtomicallyAsync(checklistPath, "STALE CHECKLIST\n");

UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateRequest request = new(
    baseImage,
    baseCue,
    outputImage,
    outputCue);
UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult first =
    await UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ExportAsync(request);
VerifyResult(first);
UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult repeat =
    await UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ExportAsync(request);
VerifyResult(repeat);
Require(
    first.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == repeat.OutputDataSha256 &&
    await HashFileAsync(baseImage) == BaseImageSha256,
    "The renderer-visibility control or its passed base was not deterministic.");

IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
    RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
VerifyLoadCodes(loadCodes);
RuntimeCandidateFinderReveal finderReveal =
    await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(repeat.OutputCuePath);

object proof = new
{
    format = "spyro-editor-static-proof",
    formatVersion = 1,
    status = "static-proven-runtime-pending",
    runtimeClaim = false,
    promotionAuthorized = false,
    generatedAtUtc = repeat.Plan.GeneratedAtUtc,
    profileId = repeat.Plan.ProfileId,
    baseProfileId = repeat.Plan.BaseProfileId,
    baseRuntimeEvidence = new
    {
        evidenceId = DisplayEvidenceId,
        evidencePath = DisplayEvidencePath,
        evidenceStatus = "focused-runtime-pass",
        profileId = UnusedLevel65DisplayNameCandidateExporter.ProfileId,
        outputImageSha256 = BaseImageSha256,
        outputExecutableSha256 = ExecutableSha256,
        automatedEmulatorCapture = false,
        promotionAuthorized = false
    },
    predecessorRejectedEvidence = new
    {
        evidenceId = RejectedEvidenceId,
        evidencePath = RejectedEvidencePath,
        evidenceStatus = "runtime-rejected-visibility-discriminator-not-observed",
        profileId = RejectedProfileId,
        outputImageSha256 = RejectedImageSha256,
        structuralOverlapBytes = 28,
        reusedAsBase = false,
        doNotLoadOrRetest = true,
        promotionAuthorized = false
    },
    baseImageSha256 = repeat.Plan.BaseImageSha256,
    outputImageSha256 = repeat.OutputImageSha256,
    outputId65DataSha256 = repeat.OutputDataSha256,
    preservedExecutableSha256 = repeat.Plan.BaseExecutableSha256,
    researchBinding = new
    {
        levelId = repeat.Plan.LevelId,
        continuousLevelIndex = repeat.Plan.ContinuousLevelIndex,
        catalogIntegrated = false,
        normalCreateBinIntegrated = false,
        overlayWadEntry = repeat.Plan.TargetOverlayWadEntry,
        dataWadEntry = repeat.Plan.TargetDataWadEntry,
        overlayWadOffset = $"0x{repeat.Plan.TargetOverlayWadOffset:X}",
        overlayByteLength = repeat.Plan.TargetOverlayByteLength,
        dataWadOffset = $"0x{repeat.Plan.TargetDataWadOffset:X}",
        dataByteLength = repeat.Plan.TargetDataByteLength
    },
    renderControl = new
    {
        runtimeKey = repeat.Plan.RuntimeKey,
        sourceSectorIndex = repeat.Plan.SourceSectorIndex,
        sourceSectorWadOffset = $"0x{repeat.Plan.SourceSectorWadOffset:X}",
        sourceFaceWadOffset = $"0x{repeat.Plan.SourceFaceWadOffset:X}",
        sourceTextureId = repeat.Plan.SourceTextureId,
        spawnPoint = repeat.Plan.SpawnPoint,
        originalFacePoints = repeat.Plan.OriginalFacePoints,
        authoredRidgeZ = UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.AuthoredRidgeZ,
        indirectlyAffectedFaceIndexes = repeat.Plan.IndirectlyAffectedFaceIndexes,
        repeat.Plan.VisualOnly,
        repeat.Plan.CollisionIntentionallyUnchanged,
        faceCountChanged = false,
        sectorSizeChanged = false,
        componentSizeChanged = false,
        lowPolyChanged = false
    },
    logicalPatches = repeat.Plan.Patches.Select(patch => new
    {
        patch.Kind,
        patch.RuntimeKey,
        patch.VertexIndex,
        wadOffset = $"0x{patch.WadOffset:X}",
        patch.BeforeHex,
        patch.AfterHex,
        patch.OriginalPoint,
        patch.AuthoredPoint
    }).ToArray(),
    diffBoundary = new
    {
        changedLogicalWadBytes = repeat.ChangedLogicalWadBytes,
        changedPhysicalImageBytes = repeat.ChangedPhysicalImageBytes,
        rebuiltRawSectorCount = repeat.RebuiltRawSectorCount,
        changedRawSectorCount = repeat.ChangedRawSectorCount,
        affectedRawSectorLbas = repeat.Plan.AffectedRawSectorLbas,
        repeat.ExactLogicalDiffBoundaryVerified,
        repeat.ExactPhysicalSectorBoundaryVerified,
        repeat.VertexReadbackVerified,
        repeat.RawSectorIntegrityVerified
    },
    preserved = new
    {
        repeat.TerrainCountsPreserved,
        repeat.TerrainFacePreserved,
        repeat.CollisionComponentsPreserved,
        repeat.RetailTownSquarePreserved,
        repeat.Id65OverlayPreserved,
        repeat.ExecutablePreserved,
        repeat.BaseCandidatePreserved,
        repeat.AtomicRenameCompleted
    },
    testHandoff = new
    {
        cuePath = finderReveal.CuePath,
        pairedBinPath = finderReveal.PairedBinPath,
        finderRevealHelperPath = finderReveal.HelperPath,
        terminalCommand = finderReveal.TerminalCommand,
        finderReveal.CuePairingVerified,
        finderReveal.HelperIsExecutable,
        loadCodes = loadCodes.Select(code => new
        {
            testName = code.TestName,
            levelId = code.LevelId,
            inputCode = code.InputCode
        }).ToArray()
    },
    runtimeChecklist = repeat.Plan.RuntimeChecklist,
    requiresDuckStationRuntimeProof = repeat.Plan.RequiresDuckStationRuntimeProof,
    excludedScopes = new[]
    {
        "This is a visual-only existing-face renderer control, not an Add Terrain pass.",
        "Collision, terrain counts, sector/component sizes, low-detail terrain, textures, Mobys, music, totals, portals, Return Home, saving, and persistence are unchanged.",
        "The rejected db9230 candidate is not reused and must not be loaded or retested.",
        "No normal editor, Create BIN, release, or update path references this candidate.",
        "Static/readback proof does not claim DuckStation runtime success or authorize promotion."
    }
};
JsonSerializerOptions jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
await WriteTextAtomicallyAsync(
    staticProofPath,
    JsonSerializer.Serialize(proof, jsonOptions) + "\n");

StringBuilder checklist = new();
checklist.AppendLine("# ID65 authored terrain renderer-visibility control — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{repeat.OutputImageSha256}`");
checklist.AppendLine($"- Base BIN SHA-256: `{repeat.Plan.BaseImageSha256}`");
checklist.AppendLine($"- Profile: `{repeat.Plan.ProfileId}`");
checklist.AppendLine("- Status: static/readback proven; focused DuckStation runtime proof pending; research-only and unpromoted.");
checklist.AppendLine();
checklist.AppendLine(
    "This control starts again from the clean passed display-name base. It changes only two " +
    "existing HP vertex words directly ahead of ID65's entry landing, creating a 512-unit-high " +
    "visual ridge. Collision and every terrain count/component size remain byte-identical. " +
    "This is not an Add Terrain pass.");
checklist.AppendLine();
checklist.AppendLine(
    $"**Do not load the rejected `{RejectedImageSha256}` candidate. It crossed 28 bytes into " +
    "the following scene-sector header and is retained only as evidence.**");
checklist.AppendLine();
RuntimeCandidateTestHandoff.AppendCandidateDiscSection(checklist, finderReveal);
RuntimeCandidateTestHandoff.AppendLoadCodeTable(checklist, loadCodes);
checklist.AppendLine("## Test");
checklist.AppendLine();
for (int index = 0; index < repeat.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {repeat.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine(
    "Please report whether the enormous ridge is visible immediately after ID65 entry, whether " +
    "its native texture stays stable while rotating the camera, whether it is absent at the same " +
    "entry landing in retail Town Square, and whether Gnasty's Loot and Sunny Flight still load. " +
    "Collision is deliberately unchanged and is not a pass/fail criterion. Keep every " +
    "DuckStation cheat and memory-card insertion disabled and avoid every prohibited action.");
await WriteTextAtomicallyAsync(checklistPath, checklist.ToString());

VerifyProofReadback(staticProofPath, repeat, finderReveal, loadCodes);
string writtenChecklist = await File.ReadAllTextAsync(checklistPath);
RuntimeCandidateTestHandoff.VerifyChecklistReadback(writtenChecklist, finderReveal, loadCodes);
Require(
    !writtenChecklist.Contains("STALE CHECKLIST", StringComparison.Ordinal) &&
    Contains(writtenChecklist, repeat.OutputImageSha256) &&
    Contains(writtenChecklist, "512-unit-high") &&
    Contains(writtenChecklist, "immediately after ID65 entry") &&
    Contains(writtenChecklist, "Collision is deliberately unchanged") &&
    Contains(writtenChecklist, "not an Add Terrain pass") &&
    Contains(writtenChecklist, "Do not load the rejected") &&
    Contains(writtenChecklist, "28 bytes") &&
    Contains(writtenChecklist, "every DuckStation cheat") &&
    Contains(writtenChecklist, "memory-card") &&
    Contains(writtenChecklist, "Retail Town Square") &&
    Contains(writtenChecklist, "Gnasty's Loot") &&
    Contains(writtenChecklist, "Sunny Flight"),
    "The generated renderer-control checklist lost its exact visual gate, safety warning, codes, or exclusions.");
RequireNoTransactionalDebris(outputRoot);

Console.WriteLine(
    "PASS: the ID65 existing-face renderer-visibility control is deterministic and statically " +
    "proven; focused DuckStation runtime proof remains pending and promotion unauthorized.");
Console.WriteLine($"CUE: {repeat.OutputCuePath}");
Console.WriteLine($"Reveal in Finder: {finderReveal.HelperPath}");
Console.WriteLine($"BIN: {repeat.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {staticProofPath}");
Console.WriteLine($"BIN SHA-256: {repeat.OutputImageSha256}");
Console.WriteLine($"ID65 data SHA-256: {repeat.OutputDataSha256}");
Console.WriteLine(
    $"Changed logical WAD bytes: {repeat.ChangedLogicalWadBytes}; " +
    $"changed physical bytes: {repeat.ChangedPhysicalImageBytes}; " +
    $"rebuilt/changed raw sectors: {repeat.RebuiltRawSectorCount}/{repeat.ChangedRawSectorCount}.");

static void VerifyResult(
    UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult result)
{
    Require(
        result.Plan.ProfileId ==
            UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ProfileId &&
        result.Plan.BaseProfileId == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        result.Plan.BaseImageSha256 == BaseImageSha256 &&
        result.OutputImageSha256 ==
            UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ExpectedOutputImageSha256 &&
        result.OutputDataSha256 ==
            UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ExpectedOutputDataSha256 &&
        result.Plan.BaseExecutableSha256 == ExecutableSha256 &&
        result.Plan.ExpectedOutputDataSha256 == result.OutputDataSha256,
        "The renderer control lost an exact profile or payload hash.");
    Require(
        result.Plan.LevelId == 65 &&
        result.Plan.ContinuousLevelIndex == 35 &&
        result.Plan.TargetOverlayWadEntry == 79 &&
        result.Plan.TargetDataWadEntry == 80 &&
        result.Plan.TargetOverlayWadOffset == 0x6927000 &&
        result.Plan.TargetDataWadOffset == 0x6936800 &&
        result.Plan.RuntimeKey == "213:37:hp" &&
        result.Plan.SourceSectorIndex == 213 &&
        result.Plan.SourceSectorWadOffset == 0x6A3E8B4 &&
        result.Plan.SourceFaceWadOffset == 0x6A3F50C &&
        result.Plan.SourceTextureId == 28 &&
        result.Plan.VisualOnly &&
        result.Plan.CollisionIntentionallyUnchanged &&
        result.Plan.RequiresDuckStationRuntimeProof,
        "The exact research-only row-80 render-control binding changed.");
    Require(
        result.Plan.Patches.Count == 2 &&
        result.Plan.Patches[0].VertexIndex == 39 &&
        result.Plan.Patches[0].WadOffset == 0x6A3EB60 &&
        result.Plan.Patches[0].BeforeHex == "20 D8 E7 29" &&
        result.Plan.Patches[0].AfterHex == "20 DA E7 29" &&
        result.Plan.Patches[1].VertexIndex == 48 &&
        result.Plan.Patches[1].WadOffset == 0x6A3EB84 &&
        result.Plan.Patches[1].BeforeHex == "20 D8 E7 39" &&
        result.Plan.Patches[1].AfterHex == "20 DA E7 39" &&
        result.Plan.Patches.All(patch =>
            patch.OriginalPoint.Z == 512 && patch.AuthoredPoint.Z == 1024) &&
        result.Plan.IndirectlyAffectedFaceIndexes.SequenceEqual(new[] { 25, 26, 29, 37, 38, 43 }),
        "The exact two-word Z1024 ridge recipe changed.");
    Require(
        result.Plan.AffectedRawSectorLbas.SequenceEqual(new[] { 54434 }) &&
        result.ChangedLogicalWadBytes == 2 &&
        result.ChangedPhysicalImageBytes == 44 &&
        result.RebuiltRawSectorCount == 1 &&
        result.ChangedRawSectorCount == 1 &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.VertexReadbackVerified &&
        result.RawSectorIntegrityVerified &&
        result.TerrainCountsPreserved &&
        result.TerrainFacePreserved &&
        result.CollisionComponentsPreserved &&
        result.RetailTownSquarePreserved &&
        result.Id65OverlayPreserved &&
        result.ExecutablePreserved &&
        result.BaseCandidatePreserved &&
        result.AtomicRenameCompleted,
        "The exact logical/raw-sector boundary, preservation, integrity, or transaction proof failed.");

    string checklist = string.Join('\n', result.Plan.RuntimeChecklist);
    Require(
        Contains(checklist, "Disable every DuckStation cheat") &&
        Contains(checklist, "memory-card insertion") &&
        Contains(checklist, "Left, then Down") &&
        Contains(checklist, "entry landing") &&
        Contains(checklist, "Z 1024") &&
        Contains(checklist, "visual-only") &&
        Contains(checklist, "Collision remains the original flat Z 512") &&
        Contains(checklist, "retail Town Square") &&
        Contains(checklist, "Gnasty's Loot") &&
        Contains(checklist, "Sunny Flight") &&
        Contains(checklist, "Face counts") &&
        Contains(checklist, "Mobys") &&
        Contains(checklist, "totals") &&
        Contains(checklist, "Return Home") &&
        Contains(checklist, "saving"),
        "The focused renderer-control checklist lost a required test or excluded scope.");
}

static void VerifyProofReadback(
    string proofPath,
    UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateResult result,
    RuntimeCandidateFinderReveal finderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(proofPath));
    JsonElement root = document.RootElement;
    JsonElement rejected = root.GetProperty("predecessorRejectedEvidence");
    JsonElement control = root.GetProperty("renderControl");
    JsonElement diff = root.GetProperty("diffBoundary");
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        !root.TryGetProperty("stale", out _) &&
        root.GetProperty("status").GetString() == "static-proven-runtime-pending" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainRenderVisibilityControlCandidateExporter.ProfileId &&
        root.GetProperty("baseImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() == result.OutputDataSha256 &&
        root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean(),
        "The static proof lost its pending/unpromoted status or exact hashes.");
    Require(
        rejected.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        rejected.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        rejected.GetProperty("structuralOverlapBytes").GetInt32() == 28 &&
        !rejected.GetProperty("reusedAsBase").GetBoolean() &&
        rejected.GetProperty("doNotLoadOrRetest").GetBoolean() &&
        control.GetProperty("runtimeKey").GetString() == "213:37:hp" &&
        control.GetProperty("authoredRidgeZ").GetInt32() == 1024 &&
        control.GetProperty("visualOnly").GetBoolean() &&
        control.GetProperty("collisionIntentionallyUnchanged").GetBoolean() &&
        !control.GetProperty("faceCountChanged").GetBoolean() &&
        !control.GetProperty("sectorSizeChanged").GetBoolean() &&
        !control.GetProperty("componentSizeChanged").GetBoolean(),
        "The proof lost the rejected-v1 boundary or exact visual-only recipe.");
    Require(
        root.GetProperty("logicalPatches").GetArrayLength() == 2 &&
        diff.GetProperty("changedLogicalWadBytes").GetInt64() == 2 &&
        diff.GetProperty("changedPhysicalImageBytes").GetInt64() == 44 &&
        diff.GetProperty("affectedRawSectorLbas").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 54434 }) &&
        diff.GetProperty("exactLogicalDiffBoundaryVerified").GetBoolean() &&
        diff.GetProperty("exactPhysicalSectorBoundaryVerified").GetBoolean() &&
        diff.GetProperty("vertexReadbackVerified").GetBoolean() &&
        diff.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        handoff.GetProperty("cuePath").GetString() == finderReveal.CuePath &&
        handoff.GetProperty("pairedBinPath").GetString() == finderReveal.PairedBinPath &&
        handoff.GetProperty("finderRevealHelperPath").GetString() == finderReveal.HelperPath &&
        handoff.GetProperty("terminalCommand").GetString() == finderReveal.TerminalCommand &&
        handoff.GetProperty("cuePairingVerified").GetBoolean() &&
        handoff.GetProperty("helperIsExecutable").GetBoolean() &&
        handoff.GetProperty("loadCodes").GetArrayLength() == loadCodes.Count,
        "The proof lost the two-byte/one-sector boundary or exact CUE/Finder/code handoff.");
}

static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
{
    (string Name, int Id, string Selection)[] expected =
    [
        ("ID65 candidate", 65, "Left, then Down"),
        ("Retail Town Square", 13, "Cross, then Triangle"),
        ("Gnasty's Loot", 64, "Left, then Right"),
        ("Sunny Flight", 15, "Cross, then Down")
    ];
    Require(loadCodes.Count == expected.Length, "The handoff must contain exactly four load codes.");
    for (int index = 0; index < expected.Length; index++)
    {
        RuntimeCandidateLoadCode actual = loadCodes[index];
        string exactInput =
            $"Select; then {TestLevelWarpPatch.ActivationSequence}; then {expected[index].Selection}";
        Require(
            actual.TestName == expected[index].Name &&
            actual.LevelId == expected[index].Id &&
            actual.TargetSelection == expected[index].Selection &&
            actual.InputCode == exactInput,
            $"The complete load code for {expected[index].Name} changed.");
    }
}

static bool Contains(string value, string expected) =>
    value.Contains(expected, StringComparison.OrdinalIgnoreCase);

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static async Task WriteTextAtomicallyAsync(string path, string content)
{
    string fullPath = Path.GetFullPath(path);
    string directory = Path.GetDirectoryName(fullPath) ??
        throw new InvalidOperationException("The sidecar output has no parent directory.");
    Directory.CreateDirectory(directory);
    string temporaryPath = Path.Combine(
        directory,
        $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    try
    {
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        await using FileStream stream = new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
        stream.Flush(flushToDisk: true);
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
    }
}

static void RequireNoTransactionalDebris(string directory)
{
    bool hasDebris = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Any(name =>
            name != null &&
            (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".work", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".tmp.", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".bak.", StringComparison.OrdinalIgnoreCase)));
    Require(!hasDebris, "The renderer-control exporter left temporary, backup, or work debris.");
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (Directory.Exists(Path.Combine(cursor.FullName, ".git")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src")))
        {
            return cursor.FullName;
        }
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
