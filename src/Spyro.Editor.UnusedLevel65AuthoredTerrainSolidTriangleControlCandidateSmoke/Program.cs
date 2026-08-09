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
const string RendererEvidenceId =
    "unused-level-65-town-square-authored-terrain-render-visibility-control-focused-pass-duckstation-2026-08-08";
const string RendererEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-render-visibility-control-focused-pass-2026-08-08.json";
const string RendererProfileId =
    "unused-level-65-town-square-authored-terrain-render-visibility-control-clean-usa-disposable-v2";
const string RendererImageSha256 =
    "01a170b19303eaab77fb00fbc8cfabf5425a640348c84aa09f8aa4eb5b57e6b1";
const string RendererDataSha256 =
    "c20e5cee4abd551c2536eb23fba9d2a8868193c9991af9ccfe7883429c9b4a57";
const string RejectedEvidenceId =
    "unused-level-65-town-square-authored-terrain-add-copy-not-observed-duckstation-2026-08-08";
const string RejectedEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-add-copy-not-observed-2026-08-08.json";
const string RejectedProfileId =
    "unused-level-65-town-square-authored-terrain-add-copy-clean-usa-disposable-v1";
const string RejectedImageSha256 =
    "db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f";
const string CandidatePrefix =
    "Unused-Level-65-Town-Square-authored-terrain-solid-existing-triangle-control-RUNTIME-CANDIDATE";

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
    "The solid-triangle control base is not the exact focused-runtime-passed display-name BIN.");

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
        "The solid-triangle control lost its exact unpromoted display-name runtime-pass base.");
}

string rendererEvidenceAbsolutePath = Path.Combine(repositoryRoot, RendererEvidencePath);
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(rendererEvidenceAbsolutePath)))
{
    JsonElement root = evidence.RootElement;
    JsonElement observation = root.GetProperty("focusedRuntimeObservation");
    Require(
        root.GetProperty("evidenceId").GetString() == RendererEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        root.GetProperty("profileId").GetString() == RendererProfileId &&
        root.GetProperty("baseOutputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == RendererImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() == RendererDataSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        observation.GetProperty("primaryEvidence").GetBoolean() &&
        observation.GetProperty("passedChecks").GetArrayLength() == 5 &&
        !root.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean(),
        "The solid-triangle control lost the exact focused renderer-pass predecessor evidence.");
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
        "The solid-triangle control lost the exact rejected-v1 evidence and 28-byte overlap boundary.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-authored-terrain-solid-existing-triangle-control");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(outputRoot, CandidatePrefix);
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";

await WriteTextAtomicallyAsync(staticProofPath, "{\"stale\":true}\n");
await WriteTextAtomicallyAsync(checklistPath, "STALE CHECKLIST\n");

UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateRequest request = new(
    baseImage,
    baseCue,
    outputImage,
    outputCue);
UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult first =
    await UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ExportAsync(request);
VerifyResult(first);
UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult repeat =
    await UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ExportAsync(request);
VerifyResult(repeat);
Require(
    first.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == repeat.OutputDataSha256 &&
    await HashFileAsync(baseImage) == BaseImageSha256,
    "The solid-triangle control or its passed base was not deterministic.");

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
    predecessorRendererRuntimeEvidence = new
    {
        evidenceId = RendererEvidenceId,
        evidencePath = RendererEvidencePath,
        evidenceStatus = "focused-runtime-pass",
        profileId = RendererProfileId,
        baseOutputImageSha256 = BaseImageSha256,
        outputImageSha256 = RendererImageSha256,
        outputId65DataSha256 = RendererDataSha256,
        focusedDiscriminatorPassed = true,
        reusedAsBase = false,
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
    solidTriangleControl = new
    {
        runtimeKey = repeat.Plan.RuntimeKey,
        sourceSectorIndex = repeat.Plan.SourceSectorIndex,
        sourceSectorWadOffset = $"0x{repeat.Plan.SourceSectorWadOffset:X}",
        sourceFaceWadOffset = $"0x{repeat.Plan.SourceFaceWadOffset:X}",
        sourceTextureId = repeat.Plan.SourceTextureId,
        spawnPoint = repeat.Plan.SpawnPoint,
        originalFacePoints = repeat.Plan.OriginalFacePoints,
        authoredPeakZ = UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.AuthoredPeakZ,
        authoredVertexIndex = UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.AuthoredVertexIndex,
        sourceFaceVertexIndexes = repeat.Plan.SourceFaceVertexIndexes,
        affectedFaceIndexes = repeat.Plan.AffectedFaceIndexes,
        uniqueVertexFaceReferenceCount = 1,
        collisionTriangleIndex = repeat.Plan.CollisionTriangleIndex,
        collisionTriangleWadOffset = $"0x{repeat.Plan.CollisionTriangleWadOffset:X}",
        collisionAssignmentWadOffset = $"0x{repeat.Plan.CollisionAssignmentWadOffset:X}",
        collisionAssignmentValue = 0,
        collisionLookupReferenceCount = repeat.Plan.CollisionLookupReferenceCount,
        collisionLookupWadOffsets = repeat.Plan.CollisionLookupWadOffsets.Select(offset => $"0x{offset:X}").ToArray(),
        collisionLookupCells = new[] { "X31/Y24/Z2", "X31/Y25/Z2" },
        collisionHeaderSha256 = repeat.Plan.CollisionHeaderSha256,
        collisionBlockTreeSha256 = repeat.Plan.CollisionBlockTreeSha256,
        collisionBlocksSha256 = repeat.Plan.CollisionBlocksSha256,
        collisionAssignmentsSha256 = repeat.Plan.CollisionAssignmentsSha256,
        collisionFlagsSha256 = repeat.Plan.CollisionFlagsSha256,
        selectionRationale = new
        {
            previousV2RidgeIntentionallyAbsent = true,
            previousV2HeightDelta = 512,
            nativeCollisionUnsignedZDeltaMaximum = 255,
            previousV2SharedVisualFaceCount = 6,
            selectedVertexFaceReferenceCount = 1,
            selectedHeightDelta = 64
        },
        repeat.Plan.CollisionPatched,
        repeat.Plan.CountsAndComponentSizesUnchanged,
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
        patch.CollisionTriangleIndex,
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
        rawSectorDiffs = repeat.RawSectorDiffs,
        repeat.ExactLogicalDiffBoundaryVerified,
        repeat.ExactPhysicalSectorBoundaryVerified,
        repeat.VertexReadbackVerified,
        repeat.RawSectorIntegrityVerified
    },
    preserved = new
    {
        repeat.TerrainCountsPreserved,
        repeat.TerrainFacePreserved,
        repeat.UniqueVertexIsolationVerified,
        repeat.CollisionStructurePreserved,
        repeat.CollisionTriangleReadbackVerified,
        repeat.CollisionLookupPreserved,
        repeat.CollisionAssignmentPreserved,
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
        "This is a visual-plus-collision existing-face coherence control, not an Add Terrain pass.",
        "One existing HP vertex and one exact source-derived native collision triangle change; all counts, sector/component sizes, collision tree/index/assignments, LP terrain, textures, Mobys, music, totals, portals, Return Home, saving, and persistence remain unchanged.",
        "The rejected db9230 candidate is not reused and must not be loaded or retested.",
        "The passed v2 renderer control is evidence only and is not reused as this candidate's base.",
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
checklist.AppendLine("# ID65 authored terrain solid-triangle control — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{repeat.OutputImageSha256}`");
checklist.AppendLine($"- Base BIN SHA-256: `{repeat.Plan.BaseImageSha256}`");
checklist.AppendLine($"- Profile: `{repeat.Plan.ProfileId}`");
checklist.AppendLine("- Status: static/readback proven; focused DuckStation runtime proof pending; research-only and unpromoted.");
checklist.AppendLine();
checklist.AppendLine(
    "This control starts again from the clean passed display-name base and is justified by the " +
    "focused v2 renderer pass. It raises exactly one HP vertex used only by face 213:64 from " +
    "Z 512 to Z 576 and applies the same +64 height to exact source-derived native collision " +
    "triangle 13853. Counts, component sizes, collision tree/index/assignments, and LP terrain " +
    "remain unchanged. The previous enormous v2 ridge is intentionally absent: its +512 height " +
    "cannot fit the native unsigned +255 collision Z delta and its vertices are shared. Look only " +
    "for the new small grass slope about 77 degrees right. This is not an Add Terrain pass.");
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
    "Please report whether the small grass slope is visible at the exact right-side landmark, " +
    "whether walking, charging, jumping, and landing follow that visible incline without the " +
    "old flat plane remaining underneath, whether its texture/camera stay stable, whether it is " +
    "absent in retail Town Square, and whether Gnasty's Loot and Sunny Flight still load. Keep every " +
    "DuckStation cheat and memory-card insertion disabled and avoid every prohibited action.");
await WriteTextAtomicallyAsync(checklistPath, checklist.ToString());

VerifyProofReadback(staticProofPath, repeat, finderReveal, loadCodes);
string writtenChecklist = await File.ReadAllTextAsync(checklistPath);
RuntimeCandidateTestHandoff.VerifyChecklistReadback(writtenChecklist, finderReveal, loadCodes);
Require(
    !writtenChecklist.Contains("STALE CHECKLIST", StringComparison.Ordinal) &&
    Contains(writtenChecklist, repeat.OutputImageSha256) &&
    Contains(writtenChecklist, "Z 512 to Z 576") &&
    Contains(writtenChecklist, "triangle 13853") &&
    Contains(writtenChecklist, "77 degrees right") &&
    Contains(writtenChecklist, "walking, charging, jumping, and landing") &&
    Contains(writtenChecklist, "old flat plane") &&
    Contains(writtenChecklist, "not an Add Terrain pass") &&
    Contains(writtenChecklist, "Do not load the rejected") &&
    Contains(writtenChecklist, "28 bytes") &&
    Contains(writtenChecklist, "every DuckStation cheat") &&
    Contains(writtenChecklist, "memory-card") &&
    Contains(writtenChecklist, "Retail Town Square") &&
    Contains(writtenChecklist, "Gnasty's Loot") &&
    Contains(writtenChecklist, "Sunny Flight"),
    "The generated solid-triangle-control checklist lost its exact visual gate, safety warning, codes, or exclusions.");
RequireNoTransactionalDebris(outputRoot);

Console.WriteLine(
    "PASS: the ID65 existing-face solid-triangle control is deterministic and statically " +
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
    UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult result)
{
    Require(
        result.Plan.ProfileId ==
            UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ProfileId &&
        result.Plan.BaseProfileId == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        result.Plan.BaseImageSha256 == BaseImageSha256 &&
        result.OutputImageSha256 ==
            UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ExpectedOutputImageSha256 &&
        result.OutputDataSha256 ==
            UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ExpectedOutputDataSha256 &&
        result.Plan.BaseExecutableSha256 == ExecutableSha256 &&
        result.Plan.ExpectedOutputDataSha256 == result.OutputDataSha256,
        "The solid-triangle control lost an exact profile or payload hash.");
    Require(
        result.Plan.LevelId == 65 &&
        result.Plan.ContinuousLevelIndex == 35 &&
        result.Plan.TargetOverlayWadEntry == 79 &&
        result.Plan.TargetDataWadEntry == 80 &&
        result.Plan.TargetOverlayWadOffset == 0x6927000 &&
        result.Plan.TargetDataWadOffset == 0x6936800 &&
        result.Plan.RuntimeKey == "213:64:hp" &&
        result.Plan.SourceSectorIndex == 213 &&
        result.Plan.SourceSectorWadOffset == 0x6A3E8B4 &&
        result.Plan.SourceFaceWadOffset == 0x6A3F6BC &&
        result.Plan.SourceTextureId == 5 &&
        result.Plan.SourceFaceVertexIndexes.SequenceEqual(new[] { 93, 93, 95, 94 }) &&
        result.Plan.AffectedFaceIndexes.SequenceEqual(new[] { 64 }) &&
        result.Plan.CollisionTriangleIndex == 13853 &&
        result.Plan.CollisionTriangleWadOffset == 0x6A87B20 &&
        result.Plan.CollisionAssignmentWadOffset == 0x6A9C861 &&
        result.Plan.CollisionLookupReferenceCount == 2 &&
        result.Plan.CollisionLookupWadOffsets.SequenceEqual(new long[] { 0x6A51682, 0x6A5191E }) &&
        result.Plan.CollisionPatched &&
        result.Plan.CountsAndComponentSizesUnchanged &&
        result.Plan.RequiresDuckStationRuntimeProof,
        "The exact research-only row-80 solid-triangle binding changed.");
    Require(
        result.Plan.Patches.Count == 2 &&
        result.Plan.Patches[0].Kind == "visual-hp" &&
        result.Plan.Patches[0].VertexIndex == 95 &&
        result.Plan.Patches[0].CollisionTriangleIndex == -1 &&
        result.Plan.Patches[0].WadOffset == 0x6A3EC40 &&
        result.Plan.Patches[0].BeforeHex == "20 84 85 5C" &&
        result.Plan.Patches[0].AfterHex == "60 84 85 5C" &&
        result.Plan.Patches[1].Kind == "collision-triangle" &&
        result.Plan.Patches[1].VertexIndex == -1 &&
        result.Plan.Patches[1].CollisionTriangleIndex == 13853 &&
        result.Plan.Patches[1].WadOffset == 0x6A87B20 &&
        result.Plan.Patches[1].BeforeHex == "BB 1F 8B DE 0A D9 EA D1 00 02 00 00" &&
        result.Plan.Patches[1].AfterHex == "BB 1F 8B DE 0A D9 EA D1 00 02 40 00" &&
        result.Plan.Patches.All(patch =>
            patch.OriginalPoint == new UnusedLevel65AuthoredTerrainSolidTriangleControlPoint(8167, 6325, 512) &&
            patch.AuthoredPoint == new UnusedLevel65AuthoredTerrainSolidTriangleControlPoint(8167, 6325, 576)),
        "The exact one-vertex plus source-derived collision-triangle +64 recipe changed.");
    Require(
        result.Plan.CollisionHeaderSha256 == "73d2390f99633c263650569efc56788d5bf918ff05b02f39bf7b378e255c6e98" &&
        result.Plan.CollisionBlockTreeSha256 == "ad6ce785e5d49ff97c5fb79c967f2d0bcff2f2e2d40ef1137cdce0114e4ea6ed" &&
        result.Plan.CollisionBlocksSha256 == "c2e0d178d39cd8ce8439d44d64987c81e60c93e0eb50f43c4a7ce51a258250d5" &&
        result.Plan.CollisionAssignmentsSha256 == "d1e9140fc8b3ca6fe7edf3f080ee74a9acad3e2373a55de9cf7678103ee29b0d" &&
        result.Plan.CollisionFlagsSha256 == "ef48378248aa661b07d1fa37059177924aa5881ae8863bf778a4ab1917371f92" &&
        result.Plan.AffectedRawSectorLbas.SequenceEqual(new[] { 54434, 54580 }) &&
        result.ChangedLogicalWadBytes == 2 &&
        result.ChangedPhysicalImageBytes == 74 &&
        result.RebuiltRawSectorCount == 2 &&
        result.ChangedRawSectorCount == 2 &&
        result.RawSectorDiffs.SequenceEqual(new[]
        {
            new UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff(54434, 0, 1, 4, 0, 10, 22, 37),
            new UnusedLevel65AuthoredTerrainSolidTriangleControlRawSectorDiff(54580, 0, 1, 4, 0, 12, 20, 37)
        }) &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.VertexReadbackVerified &&
        result.RawSectorIntegrityVerified &&
        result.TerrainCountsPreserved &&
        result.TerrainFacePreserved &&
        result.UniqueVertexIsolationVerified &&
        result.CollisionStructurePreserved &&
        result.CollisionTriangleReadbackVerified &&
        result.CollisionLookupPreserved &&
        result.CollisionAssignmentPreserved &&
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
        Contains(checklist, "previous enormous v2") &&
        Contains(checklist, "unsigned +255") &&
        Contains(checklist, "shared by six") &&
        Contains(checklist, "77 degrees right") &&
        Contains(checklist, "Z 512 to Z 576") &&
        Contains(checklist, "triangle 13853") &&
        Contains(checklist, "Walk slowly up and down") &&
        Contains(checklist, "charge across it") &&
        Contains(checklist, "jump onto it") &&
        Contains(checklist, "old flat Z 512 plane") &&
        Contains(checklist, "not an Add Terrain") &&
        Contains(checklist, "retail Town Square") &&
        Contains(checklist, "Gnasty's Loot") &&
        Contains(checklist, "Sunny Flight") &&
        Contains(checklist, "Face/vertex/triangle counts") &&
        Contains(checklist, "collision tree/index/assignments") &&
        Contains(checklist, "Mobys") &&
        Contains(checklist, "totals") &&
        Contains(checklist, "Return Home") &&
        Contains(checklist, "saving"),
        "The focused solid-triangle-control checklist lost a required test or excluded scope.");
}

static void VerifyProofReadback(
    string proofPath,
    UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateResult result,
    RuntimeCandidateFinderReveal finderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(proofPath));
    JsonElement root = document.RootElement;
    JsonElement rejected = root.GetProperty("predecessorRejectedEvidence");
    JsonElement renderer = root.GetProperty("predecessorRendererRuntimeEvidence");
    JsonElement control = root.GetProperty("solidTriangleControl");
    JsonElement diff = root.GetProperty("diffBoundary");
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        !root.TryGetProperty("stale", out _) &&
        root.GetProperty("status").GetString() == "static-proven-runtime-pending" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ProfileId &&
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
        renderer.GetProperty("evidenceId").GetString() == RendererEvidenceId &&
        renderer.GetProperty("profileId").GetString() == RendererProfileId &&
        renderer.GetProperty("outputImageSha256").GetString() == RendererImageSha256 &&
        renderer.GetProperty("focusedDiscriminatorPassed").GetBoolean() &&
        !renderer.GetProperty("reusedAsBase").GetBoolean() &&
        control.GetProperty("runtimeKey").GetString() == "213:64:hp" &&
        control.GetProperty("authoredPeakZ").GetInt32() == 576 &&
        control.GetProperty("authoredVertexIndex").GetInt32() == 95 &&
        control.GetProperty("uniqueVertexFaceReferenceCount").GetInt32() == 1 &&
        control.GetProperty("collisionTriangleIndex").GetInt32() == 13853 &&
        control.GetProperty("collisionTriangleWadOffset").GetString() == "0x6A87B20" &&
        control.GetProperty("collisionAssignmentWadOffset").GetString() == "0x6A9C861" &&
        control.GetProperty("collisionAssignmentValue").GetInt32() == 0 &&
        control.GetProperty("collisionLookupReferenceCount").GetInt32() == 2 &&
        control.GetProperty("collisionLookupWadOffsets").EnumerateArray()
            .Select(value => value.GetString()).SequenceEqual(new[] { "0x6A51682", "0x6A5191E" }) &&
        control.GetProperty("selectionRationale").GetProperty("previousV2RidgeIntentionallyAbsent").GetBoolean() &&
        control.GetProperty("selectionRationale").GetProperty("nativeCollisionUnsignedZDeltaMaximum").GetInt32() == 255 &&
        control.GetProperty("selectionRationale").GetProperty("selectedVertexFaceReferenceCount").GetInt32() == 1 &&
        control.GetProperty("collisionPatched").GetBoolean() &&
        control.GetProperty("countsAndComponentSizesUnchanged").GetBoolean() &&
        !control.GetProperty("faceCountChanged").GetBoolean() &&
        !control.GetProperty("sectorSizeChanged").GetBoolean() &&
        !control.GetProperty("componentSizeChanged").GetBoolean(),
        "The proof lost the predecessor evidence or exact solid-triangle visual/collision recipe.");
    Require(
        root.GetProperty("logicalPatches").GetArrayLength() == 2 &&
        diff.GetProperty("changedLogicalWadBytes").GetInt64() == 2 &&
        diff.GetProperty("changedPhysicalImageBytes").GetInt64() == 74 &&
        diff.GetProperty("rebuiltRawSectorCount").GetInt32() == 2 &&
        diff.GetProperty("changedRawSectorCount").GetInt32() == 2 &&
        diff.GetProperty("rawSectorDiffs").GetArrayLength() == 2 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("rawSectorLba").GetInt32() == 54434 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("headerChangedBytes").GetInt32() == 0 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("payloadChangedBytes").GetInt32() == 1 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("edcChangedBytes").GetInt32() == 4 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("reservedChangedBytes").GetInt32() == 0 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("eccPChangedBytes").GetInt32() == 10 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("eccQChangedBytes").GetInt32() == 22 &&
        diff.GetProperty("rawSectorDiffs")[0].GetProperty("totalChangedBytes").GetInt32() == 37 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("rawSectorLba").GetInt32() == 54580 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("headerChangedBytes").GetInt32() == 0 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("payloadChangedBytes").GetInt32() == 1 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("edcChangedBytes").GetInt32() == 4 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("reservedChangedBytes").GetInt32() == 0 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("eccPChangedBytes").GetInt32() == 12 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("eccQChangedBytes").GetInt32() == 20 &&
        diff.GetProperty("rawSectorDiffs")[1].GetProperty("totalChangedBytes").GetInt32() == 37 &&
        diff.GetProperty("affectedRawSectorLbas").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 54434, 54580 }) &&
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
        "The proof lost the two-byte/two-sector boundary or exact CUE/Finder/code handoff.");
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
    Require(!hasDebris, "The solid-triangle-control exporter left temporary, backup, or work debris.");
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
