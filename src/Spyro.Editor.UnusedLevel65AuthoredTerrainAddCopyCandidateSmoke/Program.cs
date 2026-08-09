using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

const string ExpectedBaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string ExpectedOutputImageSha256 =
    "db9230bbe1b8b5b15b52299267b3453cb60ec7494248295f323a40f597d7391f";
const string ExpectedOutputDataSha256 =
    "9e4d30371b9aeffed1071883848a67e1c80f6df313826bc7f4e5db2e926696b9";
const string ExpectedExecutableSha256 =
    "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
const string ExpectedBaseOverlaySha256 =
    "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
const string ExpectedBaseDataSha256 =
    "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
const string DisplayNameEvidenceId =
    "unused-level-65-town-square-display-name-focused-pass-duckstation-2026-08-08";
const string SupersededDisplayNameEvidenceId =
    "unused-level-65-town-square-display-name-focused-partial-duckstation-2026-08-08";
const string PhysicalCloneEvidenceId =
    "unused-level-65-town-square-physical-clone-focused-pass-duckstation-2026-08-08";
const string PhysicalCloneProfileId =
    "unused-level-65-town-square-physical-clone-clean-usa-disposable-v3";
const string PhysicalCloneImageSha256 =
    "f585e45ff1d795f8b2de64f20e1ed2953bfc7f43adf1c0b47b03e849e4865e48";
const string DisplayNameEvidenceRelativePath =
    "docs/runtime-evidence/unused-level-65-town-square-display-name-focused-pass-2026-08-08.json";
const string DisplayNameCueRelativePath =
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.cue";
const string DisplayNameChecklistRelativePath =
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE-runtime-checklist.md";
const string DisplayNameStaticProofRelativePath =
    "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE-static-proof.json";
const string ExpectedCandidatePrefix =
    "Unused-Level-65-Town-Square-first-authored-collidable-terrain-add-copy-RUNTIME-CANDIDATE";
const long ExpectedChangedLogicalWadBytes = 108415;
const long ExpectedChangedPhysicalImageBytes = 125762;

(string Kind, long Offset, int Length, string BeforeSha256, string AfterSha256)[] expectedPatches =
[
    (
        "terrain-vertex-count-hp",
        0x6A3C04C,
        1,
        "a318c24216defe206feeb73ef5be00033fa9c4a74d0b967f6532a26ca5906d3b",
        "cdb4ee2aea69cc6a83331bbe96dc2caa9a299d21329efb0336fc02a82e1839a8"),
    (
        "terrain-face-count-hp",
        0x6A3C04E,
        1,
        "36a9e7f1c95b82ffb99743e0c5c4ce95d83c9a430aac59f84ef3cbfab6145068",
        "bb7208bc9b5d7c04f1236a82a0093a5e33f40423d5ba8d4266f7092c3ba43b62"),
    (
        "terrain-sector-repack-hp-add-copy",
        0x6A3C180,
        916,
        "ef9ce6afbff7a4df0a8d359004819957287b10a37a9847b6163a495dfd5d11fa",
        "70b28ec2405d4a47b4896123f4f85090f3007940984f0c76c318053b860a73d9"),
    (
        "collision-index-block-tree",
        0x6A40DE0,
        27090,
        "624f40b710336edcd00f5a938357e933db551e8f858af9ea38ba31e5de215b2d",
        "7fc5ac1f4d97a99703930b2895ede36ddc0873e8b360048adc8511ca80a47a93"),
    (
        "collision-index-blocks",
        0x6A47840,
        95232,
        "c924883afbda930a756f4a5c29620c339bc1489cf4a5d3712ad0ec09527d4d66",
        "cf5f75571a18db3d75d9035b6e86cbb2e8f5d28563a0b5ecaaf3350d5ee05b4c"),
    (
        "collision-triangle-add-copy",
        0x6A5F1C4,
        12,
        "f085f169d288c2aa8c30b8db595c0f34b2e7094d622f4f71238c02b4dc50bb30",
        "eb0df448fd5ebcf0db54810c7836c75c7dd98023e7e164f4181b816b28a553f3")
];
int[] expectedRawSectors = [54429, .. Enumerable.Range(54438, 62)];

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string baseRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name");
string basePrefix = Path.Combine(
    baseRoot,
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";
Require(File.Exists(baseImage) && File.Exists(baseCue),
    "The exact focused-runtime-passed ID65 display-name BIN/CUE is missing.");
Require(await HashFileAsync(baseImage) == ExpectedBaseImageSha256,
    "The ID65 terrain candidate base is not the exact focused-runtime-passed display-name BIN.");

string evidencePath = Path.Combine(repositoryRoot, DisplayNameEvidenceRelativePath);
Require(File.Exists(evidencePath), "The focused ID65 display-name runtime evidence is missing.");
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath)))
{
    JsonElement root = evidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() == DisplayNameEvidenceId &&
        root.GetProperty("evidenceType").GetString() ==
            "interactive-user-report-with-read-only-runtime-inspection" &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        !root.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        root.GetProperty("supersedesEvidenceId").GetString() == SupersededDisplayNameEvidenceId &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        root.GetProperty("baseProfileId").GetString() == PhysicalCloneProfileId &&
        root.GetProperty("baseRuntimeEvidenceId").GetString() == PhysicalCloneEvidenceId &&
        root.GetProperty("baseOutputImageSha256").GetString() == PhysicalCloneImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == ExpectedBaseImageSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExpectedExecutableSha256 &&
        NormalizeRelativePath(root.GetProperty("outputCue").GetString() ?? "") ==
            DisplayNameCueRelativePath &&
        NormalizeRelativePath(root.GetProperty("runtimeChecklistArtifact").GetString() ?? "") ==
            DisplayNameChecklistRelativePath &&
        NormalizeRelativePath(root.GetProperty("staticProofArtifact").GetString() ?? "") ==
            DisplayNameStaticProofRelativePath &&
        NormalizeRelativePath(Path.GetRelativePath(repositoryRoot, baseCue)) ==
            DisplayNameCueRelativePath &&
        File.Exists(Path.Combine(repositoryRoot, DisplayNameChecklistRelativePath)) &&
        File.Exists(Path.Combine(repositoryRoot, DisplayNameStaticProofRelativePath)),
        "The display-name runtime evidence no longer binds the exact unpromoted base profile and artifacts.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-authored-terrain-add-copy");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(outputRoot, ExpectedCandidatePrefix);
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";

// A stale sidecar must never survive a successful regeneration.
await WriteTextAtomicallyAsync(staticProofPath, "{\"stale\":true}\n");
await WriteTextAtomicallyAsync(checklistPath, "STALE CHECKLIST\n");

UnusedLevel65AuthoredTerrainAddCopyCandidateRequest request = new(
    baseImage,
    baseCue,
    outputImage,
    outputCue);
UnusedLevel65AuthoredTerrainAddCopyCandidateResult first =
    await UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ExportAsync(request);
VerifyResult(first, expectedPatches, expectedRawSectors);
UnusedLevel65AuthoredTerrainAddCopyCandidateResult repeat =
    await UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ExportAsync(request);
VerifyResult(repeat, expectedPatches, expectedRawSectors);
Require(first.OutputImageSha256 == repeat.OutputImageSha256 &&
        first.OutputDataSha256 == repeat.OutputDataSha256 &&
        await HashFileAsync(baseImage) == ExpectedBaseImageSha256,
    "The authored ID65 terrain candidate or its focused-pass base was not deterministic.");

IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
    RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
VerifyLoadCodes(loadCodes);
RuntimeCandidateFinderReveal finderReveal =
    await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(repeat.OutputCuePath);

object[] proofPatches = repeat.Plan.TerrainPatchPlan.Patches
    .Select(patch =>
    {
        byte[] before = Convert.FromHexString(NormalizeHex(patch.BeforeHexPreview));
        byte[] after = Convert.FromHexString(NormalizeHex(patch.AfterHexPreview));
        return (object)new
        {
            kind = patch.Kind,
            wadOffset = patch.WadRelativeOffset,
            byteLength = patch.ByteLength,
            beforeSha256 = Hash(before),
            afterSha256 = Hash(after)
        };
    })
    .ToArray();

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
        evidenceId = DisplayNameEvidenceId,
        evidencePath = DisplayNameEvidenceRelativePath,
        evidenceType = "interactive-user-report-with-read-only-runtime-inspection",
        evidenceStatus = "focused-runtime-pass",
        automatedEmulatorCapture = false,
        promotionAuthorized = false,
        supersedesEvidenceId = SupersededDisplayNameEvidenceId,
        profileId = UnusedLevel65DisplayNameCandidateExporter.ProfileId,
        baseProfileId = PhysicalCloneProfileId,
        baseRuntimeEvidenceId = PhysicalCloneEvidenceId,
        baseOutputImageSha256 = PhysicalCloneImageSha256,
        outputImageSha256 = ExpectedBaseImageSha256,
        outputExecutableSha256 = ExpectedExecutableSha256,
        outputCue = DisplayNameCueRelativePath,
        runtimeChecklistArtifact = DisplayNameChecklistRelativePath,
        staticProofArtifact = DisplayNameStaticProofRelativePath
    },
    baseImageSha256 = repeat.Plan.BaseImageSha256,
    outputImageSha256 = repeat.OutputImageSha256,
    outputId65DataSha256 = repeat.OutputDataSha256,
    preservedExecutableSha256 = repeat.Plan.BaseExecutableSha256,
    researchBinding = new
    {
        levelId = repeat.Plan.LevelId,
        continuousLevelIndex = repeat.Plan.ContinuousLevelIndex,
        levelKey = repeat.Plan.ResearchLevelKey,
        catalogIntegrated = false,
        normalCreateBinIntegrated = false,
        overlayWadEntry = repeat.Plan.TargetOverlayWadEntry,
        dataWadEntry = repeat.Plan.TargetDataWadEntry,
        overlayWadOffset = $"0x{repeat.Plan.TargetOverlayWadOffset:X}",
        overlayByteLength = repeat.Plan.TargetOverlayByteLength,
        dataWadOffset = $"0x{repeat.Plan.TargetDataWadOffset:X}",
        dataByteLength = repeat.Plan.TargetDataByteLength,
        townSquareToId65Rebase = $"0x{repeat.Plan.TownSquareToId65Rebase:X}",
        objectSourceTableEnabled = false,
        futureMobyTableWadOffset = $"0x{repeat.Plan.FutureMobyTableWadOffset:X}",
        futureMobyRecordCount = repeat.Plan.FutureMobyRecordCount
    },
    sourceDerivedScene = new
    {
        sceneWadOffset = $"0x{repeat.Plan.SourceSceneWadOffset:X}",
        sectorCount = repeat.Plan.SourceSceneSectorCount,
        highPolyFaceCount = repeat.Plan.SourceSceneHighPolyFaceCount,
        lowPolyFaceCount = repeat.Plan.SourceSceneLowPolyFaceCount,
        sourceSearchSectorCount = repeat.Plan.SourceSearchSectorCount,
        sourceSearchMatchedSectorCount = repeat.Plan.SourceSearchMatchedSectorCount
    },
    authoredFace = new
    {
        runtimeKey = repeat.Plan.Face.RuntimeKey,
        textureId = repeat.Plan.Face.TextureId,
        sourceSectorIndex = repeat.Plan.Face.SourceSectorIndex,
        sourceSectorWadOffset = $"0x{repeat.Plan.Face.SourceSectorWadOffset:X}",
        sourceFaceWadOffset = $"0x{repeat.Plan.Face.SourceFaceWadOffset:X}",
        originalVertices = repeat.Plan.Face.OriginalVertices.Select(VertexProof).ToArray(),
        authoredVertices = repeat.Plan.Face.AuthoredVertices.Select(VertexProof).ToArray(),
        deltaX = repeat.Plan.Face.DeltaX,
        deltaY = repeat.Plan.Face.DeltaY,
        deltaZ = repeat.Plan.Face.DeltaZ,
        clonedNativeFaceTailHex = "2B 2B 2C 2D 00 00 02 01 1B 00 A0 01 10 C4 10 00",
        independentHighPolyVertices = true,
        playableCollisionPlanned = true,
        lowPolyCompanionAuthored = false,
        sideWallsAuthored = false
    },
    logicalPatches = proofPatches,
    diffBoundary = new
    {
        changedLogicalWadBytes = repeat.ChangedLogicalWadBytes,
        changedPhysicalImageBytes = repeat.ChangedPhysicalImageBytes,
        rebuiltRawSectorCount = repeat.RebuiltRawSectorCount,
        changedRawSectorCount = repeat.ChangedRawSectorCount,
        affectedRawSectorLbas = repeat.Plan.AffectedRawSectorLbas,
        repeat.ExactLogicalDiffBoundaryVerified,
        repeat.ExactPhysicalSectorBoundaryVerified,
        repeat.TerrainPatchReadbackVerified,
        repeat.RawSectorIntegrityVerified
    },
    preserved = new
    {
        retailTownSquareOverlaySha256 = repeat.Plan.BaseOverlaySha256,
        retailTownSquareDataSha256 = repeat.Plan.BaseDataSha256,
        id65OverlaySha256 = repeat.Plan.BaseOverlaySha256,
        executableSha256 = repeat.Plan.BaseExecutableSha256,
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
        cuePairingVerified = finderReveal.CuePairingVerified,
        helperIsExecutable = finderReveal.HelperIsExecutable,
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
        "No retail catalog, normal Create BIN, editor workspace, release, or update path references this research-only candidate.",
        "No low-poly far-LOD terrain, vertical side walls, texture import, surface behavior, or Moby edit is authored.",
        "Music, totals, portals, Return Home, Exit Level, saving, memory-card ownership, and persistence remain unchanged and excluded.",
        "This static/readback proof does not claim DuckStation runtime success or authorize promotion."
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
checklist.AppendLine("# ID65 first authored collidable terrain — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{repeat.OutputImageSha256}`");
checklist.AppendLine($"- Base BIN SHA-256: `{repeat.Plan.BaseImageSha256}`");
checklist.AppendLine($"- Profile: `{repeat.Plan.ProfileId}`");
checklist.AppendLine("- Status: static/readback proven; focused DuckStation runtime proof pending; research-only and unpromoted.");
checklist.AppendLine();
checklist.AppendLine(
    "This disc adds exactly one raised, textured, collidable HP triangle to ID65's physically " +
    "independent Town Square data. The original triangle remains. Retail Town Square and every " +
    "retail level remain unchanged; no ID65 candidate or binding is integrated into the normal " +
    "editor, Create BIN, or release path.");
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
    "Please report: whether the original flat triangle and exactly one raised copy are visible; " +
    "whether Spyro can walk, stand, jump, and charge across the copy and its edges; whether it " +
    "flickers, corrupts, or disappears only at far distance; whether nearby ID65 gameplay stays " +
    "stable; whether the copy is absent from retail Town Square; and whether Gnasty's Loot and " +
    "Sunny Flight load normally. Keep every DuckStation cheat and memory-card insertion disabled " +
    "and avoid every prohibited action in the checklist.");
await WriteTextAtomicallyAsync(checklistPath, checklist.ToString());

VerifyProofReadback(
    staticProofPath,
    repeat,
    finderReveal,
    loadCodes,
    expectedPatches,
    expectedRawSectors);
string writtenChecklist = await File.ReadAllTextAsync(checklistPath);
RuntimeCandidateTestHandoff.VerifyChecklistReadback(
    writtenChecklist,
    finderReveal,
    loadCodes);
Require(
    !writtenChecklist.Contains("STALE CHECKLIST", StringComparison.Ordinal) &&
    writtenChecklist.Contains(ExpectedOutputImageSha256, StringComparison.Ordinal) &&
    Contains(writtenChecklist, "static/readback proven") &&
    Contains(writtenChecklist, "focused DuckStation runtime proof pending") &&
    Contains(writtenChecklist, "exactly one raised copy") &&
    Contains(writtenChecklist, "walk, stand, jump, and charge") &&
    Contains(writtenChecklist, "far distance") &&
    Contains(writtenChecklist, "Retail Town Square") &&
    Contains(writtenChecklist, "Gnasty's Loot") &&
    Contains(writtenChecklist, "Sunny Flight") &&
    Contains(writtenChecklist, "every DuckStation cheat") &&
    Contains(writtenChecklist, "memory-card") &&
    Contains(writtenChecklist, "Moby") &&
    Contains(writtenChecklist, "textures") &&
    Contains(writtenChecklist, "surface behavior") &&
    Contains(writtenChecklist, "totals") &&
    Contains(writtenChecklist, "alternate music") &&
    Contains(writtenChecklist, "portals") &&
    Contains(writtenChecklist, "Return Home") &&
    Contains(writtenChecklist, "saving") &&
    Contains(writtenChecklist, "persistence"),
    "The generated checklist omitted the exact terrain test, comparison, handoff, or excluded scope.");
RequireNoTransactionalDebris(outputRoot);

Console.WriteLine(
    "PASS: first authored ID65 terrain candidate is statically proven, deterministic, " +
    "and ready for focused DuckStation testing; promotion remains unauthorized.");
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
    UnusedLevel65AuthoredTerrainAddCopyCandidateResult result,
    IReadOnlyList<(string Kind, long Offset, int Length, string BeforeSha256, string AfterSha256)> expectedPatches,
    IReadOnlyList<int> expectedRawSectors)
{
    Require(
        result.Plan.ProfileId == UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ProfileId &&
        result.Plan.BaseProfileId == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        result.Plan.BaseImageSha256 == ExpectedBaseImageSha256 &&
        result.OutputImageSha256 == ExpectedOutputImageSha256 &&
        result.OutputDataSha256 == ExpectedOutputDataSha256 &&
        result.Plan.BaseExecutableSha256 == ExpectedExecutableSha256 &&
        result.Plan.BaseOverlaySha256 == ExpectedBaseOverlaySha256 &&
        result.Plan.BaseDataSha256 == ExpectedBaseDataSha256 &&
        result.Plan.RequiresDuckStationRuntimeProof,
        "The terrain candidate lost its exact profile, hashes, or runtime-proof requirement.");
    Require(
        result.Plan.LevelId == 65 &&
        result.Plan.ContinuousLevelIndex == 35 &&
        result.Plan.ResearchLevelKey == UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ResearchLevelKey &&
        result.Plan.TargetOverlayWadEntry == 79 &&
        result.Plan.TargetDataWadEntry == 80 &&
        result.Plan.TargetOverlayWadOffset == 0x6927000 &&
        result.Plan.TargetOverlayByteLength == 0xF800 &&
        result.Plan.TargetDataWadOffset == 0x6936800 &&
        result.Plan.TargetDataByteLength == 0x2E2000 &&
        result.Plan.RetailTownSquareOverlayWadOffset == 0x118E800 &&
        result.Plan.RetailTownSquareDataWadOffset == 0x119E000 &&
        result.Plan.TownSquareToId65Rebase == 0x5798800 &&
        result.Plan.FutureMobyTableWadOffset == 0x6B06970 &&
        result.Plan.FutureMobyRecordCount == 107,
        "The research-only ID65 row binding or protected retail layout changed.");
    Require(
        result.Plan.SourceSceneWadOffset == 0x6A182E0 &&
        result.Plan.SourceSceneSectorCount == 216 &&
        result.Plan.SourceSceneHighPolyFaceCount > 0 &&
        result.Plan.SourceSceneLowPolyFaceCount > 0 &&
        result.Plan.SourceSearchSectorCount > 0 &&
        result.Plan.SourceSearchMatchedSectorCount > 0,
        "The entry-80 source-derived scene/search binding changed.");
    Require(
        result.Plan.Face.RuntimeKey == "201:0:hp" &&
        result.Plan.Face.TextureId == 27 &&
        result.Plan.Face.SourceSectorIndex == 201 &&
        result.Plan.Face.SourceSectorWadOffset == 0x6A3C038 &&
        result.Plan.Face.SourceFaceWadOffset == 0x6A3C2F8 &&
        result.Plan.Face.DeltaX == 64f &&
        result.Plan.Face.DeltaY == 24f &&
        result.Plan.Face.DeltaZ.SequenceEqual(new[] { 10f, 13f, 16f }) &&
        VerticesEqual(result.Plan.Face.OriginalVertices,
        [
            new(8552f, 8172f, 736f),
            new(8561f, 8210f, 736f),
            new(8476f, 8114f, 736f)
        ]) &&
        VerticesEqual(result.Plan.Face.AuthoredVertices,
        [
            new(8616f, 8196f, 746f),
            new(8625f, 8234f, 749f),
            new(8540f, 8138f, 752f)
        ]),
        "The exact source face or authored displacement changed.");
    Require(
        result.Plan.TerrainPatchPlan.Patches.Count == expectedPatches.Count &&
        result.Plan.TerrainPatchPlan.PatchCount == expectedPatches.Count &&
        result.Plan.TerrainPatchPlan.TotalPatchedBytes == expectedPatches.Sum(patch => patch.Length) &&
        result.Plan.TerrainPatchPlan.SkippedEdits.Count == 0 &&
        result.Plan.TerrainPatchPlan.CustomTextureImportCount == 0 &&
        result.Plan.TerrainPatchPlan.NativeTextureRelocationCount == 0 &&
        result.Plan.TerrainPatchPlan.TerrainSideWalls.Count == 0,
        "The exact six-patch terrain/collision plan changed or gained an excluded edit.");
    for (int index = 0; index < expectedPatches.Count; index++)
    {
        TerrainPatch actual = result.Plan.TerrainPatchPlan.Patches[index];
        (string kind, long offset, int length, string beforeSha256, string afterSha256) = expectedPatches[index];
        byte[] before = Convert.FromHexString(NormalizeHex(actual.BeforeHexPreview));
        byte[] after = Convert.FromHexString(NormalizeHex(actual.AfterHexPreview));
        Require(
            actual.Kind == kind &&
            ParseHexLong(actual.WadRelativeOffset) == offset &&
            actual.ByteLength == length &&
            before.Length == length &&
            after.Length == length &&
            Hash(before) == beforeSha256 &&
            Hash(after) == afterSha256,
            $"Terrain patch {index} drifted from its exact kind/range/readback hashes.");
    }
    Require(
        result.Plan.AffectedRawSectorLbas.SequenceEqual(expectedRawSectors) &&
        result.ChangedLogicalWadBytes == ExpectedChangedLogicalWadBytes &&
        result.ChangedPhysicalImageBytes == ExpectedChangedPhysicalImageBytes &&
        result.RebuiltRawSectorCount == expectedRawSectors.Count &&
        result.ChangedRawSectorCount == expectedRawSectors.Count &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.TerrainPatchReadbackVerified &&
        result.RawSectorIntegrityVerified &&
        result.RetailTownSquarePreserved &&
        result.Id65OverlayPreserved &&
        result.ExecutablePreserved &&
        result.BaseCandidatePreserved &&
        result.AtomicRenameCompleted,
        "The exact logical/raw-sector boundary, integrity, preservation, or atomic publication proof failed.");

    string runtimeChecklist = string.Join('\n', result.Plan.RuntimeChecklist);
    Require(
        Contains(runtimeChecklist, "Disable every DuckStation cheat") &&
        Contains(runtimeChecklist, "memory-card insertion") &&
        Contains(runtimeChecklist, "Left, then Down") &&
        Contains(runtimeChecklist, "line of three loose red gems") &&
        Contains(runtimeChecklist, "nearby bull") &&
        Contains(runtimeChecklist, "original flat triangle remains") &&
        Contains(runtimeChecklist, "exactly one displaced textured copy") &&
        Contains(runtimeChecklist, "Walk, stand, jump, and charge") &&
        Contains(runtimeChecklist, "both visible faces are solid") &&
        Contains(runtimeChecklist, "no extra or misaligned invisible collision") &&
        Contains(runtimeChecklist, "retail Town Square") &&
        Contains(runtimeChecklist, "Gnasty's Loot") &&
        Contains(runtimeChecklist, "Sunny Flight") &&
        Contains(runtimeChecklist, "far camera distance") &&
        Contains(runtimeChecklist, "HP geometry only") &&
        Contains(runtimeChecklist, "Do not rescue dragons") &&
        Contains(runtimeChecklist, "Moby") &&
        Contains(runtimeChecklist, "textures") &&
        Contains(runtimeChecklist, "surface behavior") &&
        Contains(runtimeChecklist, "totals") &&
        Contains(runtimeChecklist, "alternate music") &&
        Contains(runtimeChecklist, "portals") &&
        Contains(runtimeChecklist, "Return Home") &&
        Contains(runtimeChecklist, "saving") &&
        Contains(runtimeChecklist, "persistence"),
        "The focused runtime checklist lost a required test or prohibited scope.");
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
    Require(loadCodes.Count == expected.Length, "The terrain handoff must contain exactly four load codes.");
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

static void VerifyProofReadback(
    string proofPath,
    UnusedLevel65AuthoredTerrainAddCopyCandidateResult result,
    RuntimeCandidateFinderReveal finderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
    IReadOnlyList<(string Kind, long Offset, int Length, string BeforeSha256, string AfterSha256)> expectedPatches,
    IReadOnlyList<int> expectedRawSectors)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(proofPath));
    JsonElement root = document.RootElement;
    JsonElement baseEvidence = root.GetProperty("baseRuntimeEvidence");
    JsonElement binding = root.GetProperty("researchBinding");
    JsonElement face = root.GetProperty("authoredFace");
    JsonElement diff = root.GetProperty("diffBoundary");
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        !root.TryGetProperty("stale", out _) &&
        root.GetProperty("status").GetString() == "static-proven-runtime-pending" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ProfileId &&
        root.GetProperty("baseProfileId").GetString() ==
            UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        root.GetProperty("baseImageSha256").GetString() == ExpectedBaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == ExpectedOutputImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() == ExpectedOutputDataSha256 &&
        root.GetProperty("preservedExecutableSha256").GetString() == ExpectedExecutableSha256 &&
        root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean(),
        "The generated proof lost its pending-runtime status or exact hashes/profile boundary.");
    Require(
        baseEvidence.GetProperty("evidenceId").GetString() == DisplayNameEvidenceId &&
        baseEvidence.GetProperty("evidencePath").GetString() == DisplayNameEvidenceRelativePath &&
        baseEvidence.GetProperty("evidenceType").GetString() ==
            "interactive-user-report-with-read-only-runtime-inspection" &&
        baseEvidence.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        !baseEvidence.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !baseEvidence.GetProperty("promotionAuthorized").GetBoolean() &&
        baseEvidence.GetProperty("supersedesEvidenceId").GetString() ==
            SupersededDisplayNameEvidenceId &&
        baseEvidence.GetProperty("profileId").GetString() ==
            UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        baseEvidence.GetProperty("baseProfileId").GetString() == PhysicalCloneProfileId &&
        baseEvidence.GetProperty("baseRuntimeEvidenceId").GetString() == PhysicalCloneEvidenceId &&
        baseEvidence.GetProperty("baseOutputImageSha256").GetString() == PhysicalCloneImageSha256 &&
        baseEvidence.GetProperty("outputImageSha256").GetString() == ExpectedBaseImageSha256 &&
        baseEvidence.GetProperty("outputExecutableSha256").GetString() == ExpectedExecutableSha256 &&
        baseEvidence.GetProperty("outputCue").GetString() == DisplayNameCueRelativePath &&
        baseEvidence.GetProperty("runtimeChecklistArtifact").GetString() ==
            DisplayNameChecklistRelativePath &&
        baseEvidence.GetProperty("staticProofArtifact").GetString() ==
            DisplayNameStaticProofRelativePath,
        "The generated proof lost its exact focused-pass base-evidence binding.");
    Require(
        binding.GetProperty("levelId").GetInt32() == 65 &&
        binding.GetProperty("continuousLevelIndex").GetInt32() == 35 &&
        binding.GetProperty("levelKey").GetString() ==
            UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ResearchLevelKey &&
        !binding.GetProperty("catalogIntegrated").GetBoolean() &&
        !binding.GetProperty("normalCreateBinIntegrated").GetBoolean() &&
        binding.GetProperty("overlayWadEntry").GetInt32() == 79 &&
        binding.GetProperty("dataWadEntry").GetInt32() == 80 &&
        !binding.GetProperty("objectSourceTableEnabled").GetBoolean(),
        "The proof no longer identifies an isolated terrain-only ID65 row-80 binding.");
    Require(
        face.GetProperty("runtimeKey").GetString() == "201:0:hp" &&
        face.GetProperty("textureId").GetInt32() == 27 &&
        face.GetProperty("sourceSectorIndex").GetInt32() == 201 &&
        face.GetProperty("clonedNativeFaceTailHex").GetString() ==
            "2B 2B 2C 2D 00 00 02 01 1B 00 A0 01 10 C4 10 00" &&
        face.GetProperty("independentHighPolyVertices").GetBoolean() &&
        face.GetProperty("playableCollisionPlanned").GetBoolean() &&
        !face.GetProperty("lowPolyCompanionAuthored").GetBoolean() &&
        !face.GetProperty("sideWallsAuthored").GetBoolean(),
        "The proof lost the exact HP face/collision recipe or its explicit limitations.");
    Require(
        root.GetProperty("logicalPatches").GetArrayLength() == expectedPatches.Count &&
        diff.GetProperty("changedLogicalWadBytes").GetInt64() == ExpectedChangedLogicalWadBytes &&
        diff.GetProperty("changedPhysicalImageBytes").GetInt64() == ExpectedChangedPhysicalImageBytes &&
        diff.GetProperty("rebuiltRawSectorCount").GetInt32() == expectedRawSectors.Count &&
        diff.GetProperty("changedRawSectorCount").GetInt32() == expectedRawSectors.Count &&
        diff.GetProperty("affectedRawSectorLbas").EnumerateArray().Select(value => value.GetInt32())
            .SequenceEqual(expectedRawSectors) &&
        diff.GetProperty("exactLogicalDiffBoundaryVerified").GetBoolean() &&
        diff.GetProperty("exactPhysicalSectorBoundaryVerified").GetBoolean() &&
        diff.GetProperty("terrainPatchReadbackVerified").GetBoolean() &&
        diff.GetProperty("rawSectorIntegrityVerified").GetBoolean(),
        "The proof lost the exact six-patch or 63-sector diff boundary.");
    for (int index = 0; index < expectedPatches.Count; index++)
    {
        JsonElement patch = root.GetProperty("logicalPatches")[index];
        (string kind, long offset, int length, string beforeSha256, string afterSha256) = expectedPatches[index];
        Require(
            patch.GetProperty("kind").GetString() == kind &&
            ParseHexLong(patch.GetProperty("wadOffset").GetString() ?? "") == offset &&
            patch.GetProperty("byteLength").GetInt32() == length &&
            patch.GetProperty("beforeSha256").GetString() == beforeSha256 &&
            patch.GetProperty("afterSha256").GetString() == afterSha256,
            $"The generated proof patch {index} drifted.");
    }
    Require(
        handoff.GetProperty("cuePath").GetString() == finderReveal.CuePath &&
        handoff.GetProperty("pairedBinPath").GetString() == finderReveal.PairedBinPath &&
        handoff.GetProperty("finderRevealHelperPath").GetString() == finderReveal.HelperPath &&
        handoff.GetProperty("terminalCommand").GetString() == finderReveal.TerminalCommand &&
        handoff.GetProperty("cuePairingVerified").GetBoolean() &&
        handoff.GetProperty("helperIsExecutable").GetBoolean() &&
        handoff.GetProperty("loadCodes").GetArrayLength() == loadCodes.Count &&
        result.OutputImageSha256 == ExpectedOutputImageSha256,
        "The proof lost the exact CUE/Finder/load-code handoff.");
}

static object VertexProof(Vector3f vertex) => new { vertex.X, vertex.Y, vertex.Z };

static bool VerticesEqual(
    IReadOnlyList<Vector3f> actual,
    IReadOnlyList<Vector3f> expected) =>
    actual.Count == expected.Count &&
    actual.Zip(expected).All(pair =>
        Math.Abs(pair.First.X - pair.Second.X) < 0.001f &&
        Math.Abs(pair.First.Y - pair.Second.Y) < 0.001f &&
        Math.Abs(pair.First.Z - pair.Second.Z) < 0.001f);

static string NormalizeHex(string value) =>
    new(value.Where(char.IsAsciiHexDigit).Select(char.ToUpperInvariant).ToArray());

static long ParseHexLong(string value)
{
    string normalized = value.Trim();
    if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        normalized = normalized[2..];
    return Convert.ToInt64(normalized, 16);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static string NormalizeRelativePath(string value) => value.Replace('\\', '/');

static bool Contains(string value, string expected) =>
    value.Contains(expected, StringComparison.OrdinalIgnoreCase);

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
        await using (FileStream stream = new(
                         temporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         FileOptions.Asynchronous))
        {
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
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
    Require(!hasDebris, "The authored terrain exporter left temporary, backup, or work debris behind.");
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (File.Exists(Path.Combine(cursor.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src", "Spyro.Editor.Core")))
        {
            return cursor.FullName;
        }
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
