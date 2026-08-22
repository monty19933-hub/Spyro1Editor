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
    "unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-not-observed-duckstation-2026-08-08";
const string RejectedEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-not-observed-2026-08-08.json";
const string RejectedProfileId =
    "unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-clean-usa-disposable-v3";
const string RejectedImageSha256 =
    "976a1910264c48fbc7cec8a9a1291f849bcad898fc511bae5d986a7af1c66214";
const string FocusedRuntimeEvidenceId =
    "unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-focused-pass-duckstation-2026-08-09";
const string FocusedRuntimeEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-focused-pass-2026-08-09.json";
const string FocusedRuntimeEvidenceRecordedDate = "2026-08-09";
const string CandidatePrefix =
    "Unused-Level-65-Town-Square-authored-terrain-solid-entry-ramp-control-RUNTIME-CANDIDATE";

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
    "The solid-entry-ramp base is not the exact focused-runtime-passed display-name BIN.");

VerifyDisplayEvidence(Path.Combine(repositoryRoot, DisplayEvidencePath));
VerifyRendererEvidence(Path.Combine(repositoryRoot, RendererEvidencePath));
VerifyRejectedEvidence(Path.Combine(repositoryRoot, RejectedEvidencePath));

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-authored-terrain-solid-entry-ramp-control");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(outputRoot, CandidatePrefix);
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";
string guidePath = outputPrefix + "-location-guide.svg";
string focusedRuntimeEvidenceFile = Path.Combine(repositoryRoot, FocusedRuntimeEvidencePath);
Require(File.Exists(focusedRuntimeEvidenceFile),
    "The focused runtime-pass evidence for the solid-entry-ramp control is missing.");
string expectedOutputCueArtifact = RepositoryRelativePath(repositoryRoot, outputCue);
string expectedChecklistArtifact = RepositoryRelativePath(repositoryRoot, checklistPath);
string expectedStaticProofArtifact = RepositoryRelativePath(repositoryRoot, staticProofPath);
string expectedLocationGuideArtifact = RepositoryRelativePath(repositoryRoot, guidePath);
VerifyFocusedRuntimeEvidence(
    focusedRuntimeEvidenceFile,
    expectedOutputCueArtifact,
    expectedChecklistArtifact,
    expectedStaticProofArtifact,
    expectedLocationGuideArtifact);

await WriteTextAtomicallyAsync(staticProofPath, "{\"stale\":true}\n");
await WriteTextAtomicallyAsync(checklistPath, "STALE CHECKLIST\n");
await WriteTextAtomicallyAsync(guidePath, "<svg><!-- STALE GUIDE --></svg>\n");

UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateRequest request = new(
    baseImage,
    baseCue,
    outputImage,
    outputCue);
UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult first =
    await UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExportAsync(request);
VerifyResult(first);

string firstImageHash = await HashFileAsync(outputImage);
string firstCueHash = await HashFileAsync(outputCue);
bool injectedFailureObserved = false;
try
{
    await UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExportAsync(new(
        baseImage,
        baseCue,
        outputImage,
        outputCue,
        stage =>
        {
            if (stage == "after-image-publication")
                throw new IOException("Injected rollback proof after image publication.");
        }));
}
catch (IOException ex) when (ex.Message.Contains("Injected rollback proof", StringComparison.Ordinal))
{
    injectedFailureObserved = true;
}
Require(injectedFailureObserved, "The transactional rollback fault was not injected at the required stage.");
Require(
    await HashFileAsync(outputImage) == firstImageHash &&
    await HashFileAsync(outputCue) == firstCueHash,
    "The injected publication fault did not restore the prior BIN/CUE pair exactly.");
RequireNoTransactionalDebris(outputRoot);

UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult repeat =
    await UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExportAsync(request);
VerifyResult(repeat);
Require(
    first.OutputImageSha256 == repeat.OutputImageSha256 &&
    first.OutputDataSha256 == repeat.OutputDataSha256 &&
    first.ChangedLogicalWadBytes == repeat.ChangedLogicalWadBytes &&
    first.ChangedPhysicalImageBytes == repeat.ChangedPhysicalImageBytes &&
    await HashFileAsync(baseImage) == BaseImageSha256,
    "The solid-entry-ramp candidate or its passed base was not deterministic.");

IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
    RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
VerifyLoadCodes(loadCodes);
RuntimeCandidateFinderReveal finderReveal =
    await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(repeat.OutputCuePath);
await WriteTextAtomicallyAsync(
    guidePath,
    BuildGuideSvg(
        loadCodes,
        repeat.OutputImageSha256,
        repeat.Plan.ProfileId,
        Path.GetFileName(repeat.OutputCuePath)));
string guideSha256 = await HashFileAsync(guidePath);

object proof = new
{
    format = "spyro-editor-static-proof",
    formatVersion = 1,
    status = "static-proven-external-focused-runtime-pass-recorded",
    runtimeClaim = false,
    promotionAuthorized = false,
    runtimeEvidence = new
    {
        evidenceId = FocusedRuntimeEvidenceId,
        evidencePath = FocusedRuntimeEvidencePath,
        evidenceStatus = "focused-runtime-pass",
        evidenceScope = "complete-edited-surface-solidity-only",
        automatedEmulatorCapture = false,
        promotionAuthorized = false,
        normalEditorIntegrationAuthorized = false,
        normalCreateBinIntegrationAuthorized = false,
        profileId = UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId,
        outputImageSha256 =
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputImageSha256,
        outputId65DataSha256 =
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputDataSha256,
        outputExecutableSha256 = ExecutableSha256,
        outputCue = expectedOutputCueArtifact,
        runtimeChecklistArtifact = expectedChecklistArtifact,
        staticProofArtifact = expectedStaticProofArtifact,
        locationGuideArtifact = expectedLocationGuideArtifact
    },
    generatedFromRuntimeEvidenceRecordedDate = FocusedRuntimeEvidenceRecordedDate,
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
        overlappingVisualRuntimeKey = "4:6:hp",
        shadowingCollisionTriangleIndexes = new[] { 1189, 1190 },
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
        dataWadOffset = $"0x{repeat.Plan.TargetDataWadOffset:X}"
    },
    solidEntryRampControl = new
    {
        runtimeKey = repeat.Plan.RuntimeKey,
        sourceSectorIndex = repeat.Plan.SourceSectorIndex,
        sourceSectorWadOffset = $"0x{repeat.Plan.SourceSectorWadOffset:X}",
        sourceFaceWadOffset = $"0x{repeat.Plan.SourceFaceWadOffset:X}",
        sourceTextureId = repeat.Plan.SourceTextureId,
        repeat.Plan.SpawnPoint,
        originalFacePoints = repeat.Plan.OriginalFacePoints,
        authoredFacePoints = repeat.Plan.AuthoredFacePoints,
        authoredRidgeZ = UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.AuthoredRidgeZ,
        authoredHeightDelta = UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.AuthoredHeightDelta,
        repeat.Plan.AuthoredVertexIndexes,
        repeat.Plan.AffectedFaceIndexes,
        repeat.Plan.CollisionTriangleIndexes,
        collisionBindings = repeat.Plan.CollisionBindings.Select(binding => new
        {
            binding.TriangleIndex,
            binding.SourcePoints,
            binding.AuthoredPoints,
            binding.CyclicWindingPreserved,
            blockBounds = new
            {
                binding.MinXBlock,
                binding.MaxXBlock,
                binding.MinYBlock,
                binding.MaxYBlock,
                binding.MinZBlock,
                binding.MaxZBlock
            },
            lookupWadOffsets = binding.LookupWadOffsets.Select(offset => $"0x{offset:X}").ToArray(),
            binding.Assignment,
            binding.Flags
        }).ToArray(),
        repeat.Plan.VisualFootprintIntersections,
        fullSceneSectorScanCount = 216,
        centralAscentDegrees = 36.87,
        centralDescentDegrees = 36.87,
        primaryRoute = "walk straight ahead through the center of faces 213:37 and 213:29",
        collisionIndexRebuildRequired = repeat.Plan.CollisionIndexRebuildRequired,
        repeat.Plan.CountsAndComponentSizesUnchanged,
        collisionStructureHashes = new
        {
            repeat.Plan.CollisionHeaderSha256,
            repeat.Plan.CollisionBlockTreeSha256,
            repeat.Plan.CollisionBlocksSha256,
            repeat.Plan.CollisionAssignmentsSha256,
            repeat.Plan.CollisionFlagsSha256
        }
    },
    logicalPatches = repeat.Plan.Patches.Select(patch => new
    {
        patch.Kind,
        patch.VertexIndex,
        patch.CollisionTriangleIndex,
        wadOffset = $"0x{patch.WadOffset:X}",
        patch.BeforeHex,
        patch.AfterHex
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
        repeat.TerrainFaceBindingsVerified,
        repeat.CollisionTriangleReadbackVerified,
        repeat.VisualFootprintExposureVerified,
        repeat.CollisionFootprintExposureVerified,
        repeat.RawSectorIntegrityVerified
    },
    preserved = new
    {
        repeat.CollisionStructurePreserved,
        repeat.CollisionAssignmentsPreserved,
        repeat.RetailTownSquarePreserved,
        repeat.Id65OverlayPreserved,
        repeat.ExecutablePreserved,
        repeat.BaseCandidatePreserved,
        repeat.AtomicRenameCompleted,
        injectedRollbackRestoredPriorPair = injectedFailureObserved
    },
    testHandoff = new
    {
        cuePath = finderReveal.CuePath,
        pairedBinPath = finderReveal.PairedBinPath,
        finderRevealHelperPath = finderReveal.HelperPath,
        guidePath,
        guideSha256,
        terminalCommand = finderReveal.TerminalCommand,
        finderReveal.CuePairingVerified,
        finderReveal.HelperIsExecutable,
        guideVerified = true,
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
        "This is an existing-face close-range visual-plus-collision coherence control, not an Add Terrain pass.",
        "LP/far-LOD terrain, counts, sector/component sizes, textures, Mobys, music, totals, portals, Return Home, saving, and persistence are unchanged.",
        "The retired v3 976a candidate is not reused and must not be loaded or retested.",
        "No normal editor, Create BIN, release, or update path references this candidate.",
        "The external evidence records only complete-edited-surface solidity and resolution of the prior one-sided-solidity failure; the other checklist items remain unverified.",
        "This static/readback artifact makes no independent runtime claim and does not authorize promotion."
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
checklist.AppendLine("# ID65 solid entry ridge — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{repeat.OutputImageSha256}`");
checklist.AppendLine($"- Base BIN SHA-256: `{repeat.Plan.BaseImageSha256}`");
checklist.AppendLine($"- Profile: `{repeat.Plan.ProfileId}`");
checklist.AppendLine($"- Location guide: `{guidePath}`");
checklist.AppendLine($"- Location-guide SHA-256: `{guideSha256}`");
checklist.AppendLine("- Status: exact external focused complete-edited-surface solidity pass recorded; static/readback proof remains static with `runtimeClaim: false`; research-only and unpromoted.");
checklist.AppendLine();
checklist.AppendLine(
    "This control starts from the clean passed display-name base. It raises the broad entry " +
    "ridge directly in front of Spyro by 96 units and rewrites every matching native collision " +
    "triangle. Counts, component sizes, collision lookup/index, and retail levels remain unchanged. " +
    "This is not yet Add Terrain.");
checklist.AppendLine();
checklist.AppendLine(
    "The linked external evidence records that the entire edited v4 surface was solid and that the " +
    "prior one-side-solid/other-side-not-solid failure mode was resolved. It does not mark reset, " +
    "comparison levels, separately itemized movement checks, or any other checklist item as passed.");
checklist.AppendLine();
checklist.AppendLine(
    $"**Do not load the retired v3 `{RejectedImageSha256}` candidate. Its target was hidden under " +
    "higher visual and collision geometry and is retained only as rejected evidence.**");
checklist.AppendLine();
RuntimeCandidateTestHandoff.AppendCandidateDiscSection(checklist, finderReveal);
RuntimeCandidateTestHandoff.AppendLoadCodeTable(checklist, loadCodes);
checklist.AppendLine("## Location guide");
checklist.AppendLine();
checklist.AppendLine($"Open `{guidePath}` before booting. At the ID65 landing, stop and walk straight ahead; do not turn or search near gems.");
checklist.AppendLine();
checklist.AppendLine("## Test");
checklist.AppendLine();
checklist.AppendLine("The original checklist remains below only for the runtime items not covered by the recorded focused solidity report:");
checklist.AppendLine();
for (int index = 0; index < repeat.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {repeat.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine(
    "Please report separately: whether the ridge is immediately visible only in ID65; whether " +
    "walking, charging, jumping, and landing follow its center surface in both directions; whether " +
    "the old flat plane remains underneath; whether reset reproduces it; and whether retail Town " +
    "Square, Gnasty's Loot, and Sunny Flight pass. Keep every cheat off, both card slots None, and " +
    "avoid every prohibited action.");
await WriteTextAtomicallyAsync(checklistPath, checklist.ToString());

VerifyProofReadback(
    staticProofPath,
    repeat,
    finderReveal,
    loadCodes,
    guidePath,
    guideSha256,
    expectedOutputCueArtifact,
    expectedChecklistArtifact,
    expectedStaticProofArtifact,
    expectedLocationGuideArtifact);
string writtenChecklist = await File.ReadAllTextAsync(checklistPath);
RuntimeCandidateTestHandoff.VerifyChecklistReadback(writtenChecklist, finderReveal, loadCodes);
string writtenGuide = await File.ReadAllTextAsync(guidePath);
Require(
    !writtenChecklist.Contains("STALE CHECKLIST", StringComparison.Ordinal) &&
    Contains(writtenChecklist, repeat.OutputImageSha256) &&
    Contains(writtenChecklist, "walk straight ahead") &&
    Contains(writtenChecklist, "96 units") &&
    Contains(writtenChecklist, "old flat plane") &&
    Contains(writtenChecklist, "not yet Add Terrain") &&
    Contains(writtenChecklist, "external focused complete-edited-surface solidity pass recorded") &&
    Contains(writtenChecklist, "runtimeClaim: false") &&
    Contains(writtenChecklist, "one-side-solid/other-side-not-solid failure mode was resolved") &&
    Contains(writtenChecklist, "does not mark reset") &&
    Contains(writtenChecklist, "items not covered by the recorded focused solidity report") &&
    Contains(writtenChecklist, "both card slots None") &&
    Contains(writtenChecklist, "Moon Jump") &&
    Contains(writtenChecklist, "Retail Town Square") &&
    Contains(writtenChecklist, "Gnasty's Loot") &&
    Contains(writtenChecklist, "Sunny Flight") &&
    writtenGuide.StartsWith("<svg", StringComparison.Ordinal) &&
    !writtenGuide.Contains("STALE GUIDE", StringComparison.Ordinal) &&
    Contains(writtenGuide, "ID65 SOLID ENTRY RIDGE") &&
    Contains(writtenGuide, "Do not turn or search for gems") &&
    Contains(writtenGuide, "Cheats OFF") &&
    loadCodes.All(code => Contains(writtenGuide, code.InputCode)),
    "The generated checklist or guide lost its exact route, controls, codes, or exclusions.");
RequireNoTransactionalDebris(outputRoot);

Console.WriteLine(
    "PASS: the ID65 solid-entry-ramp control is deterministic, exposed, collision-coherent, " +
    "and statically proven; its exact external focused complete-edited-surface solidity pass is " +
    "recorded while this static proof keeps runtimeClaim false and promotion unauthorized.");
Console.WriteLine($"CUE: {repeat.OutputCuePath}");
Console.WriteLine($"Reveal in Finder: {finderReveal.HelperPath}");
Console.WriteLine($"Guide: {guidePath}");
Console.WriteLine($"BIN: {repeat.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {staticProofPath}");
Console.WriteLine($"BIN SHA-256: {repeat.OutputImageSha256}");
Console.WriteLine($"ID65 data SHA-256: {repeat.OutputDataSha256}");
Console.WriteLine(
    $"Changed logical WAD bytes: {repeat.ChangedLogicalWadBytes}; " +
    $"changed physical bytes: {repeat.ChangedPhysicalImageBytes}; " +
    $"rebuilt/changed raw sectors: {repeat.RebuiltRawSectorCount}/{repeat.ChangedRawSectorCount}.");

static void VerifyDisplayEvidence(string path)
{
    using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
    JsonElement root = evidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() == DisplayEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        root.GetProperty("profileId").GetString() == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        root.GetProperty("outputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        !root.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean(),
        "The entry-ramp control lost its exact display-name runtime-pass base evidence.");
}

static void VerifyFocusedRuntimeEvidence(
    string path,
    string expectedOutputCueArtifact,
    string expectedChecklistArtifact,
    string expectedStaticProofArtifact,
    string expectedLocationGuideArtifact)
{
    using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
    JsonElement root = evidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() == FocusedRuntimeEvidenceId &&
        root.GetProperty("recordedDate").GetString() == FocusedRuntimeEvidenceRecordedDate &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        root.GetProperty("evidenceScope").GetString() == "complete-edited-surface-solidity-only" &&
        !root.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        !root.GetProperty("normalEditorIntegrationAuthorized").GetBoolean() &&
        !root.GetProperty("normalCreateBinIntegrationAuthorized").GetBoolean() &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId &&
        root.GetProperty("baseProfileId").GetString() ==
            UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        root.GetProperty("baseOutputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputDataSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        root.GetProperty("outputCue").GetString() == expectedOutputCueArtifact &&
        root.GetProperty("runtimeChecklistArtifact").GetString() == expectedChecklistArtifact &&
        root.GetProperty("staticProofArtifact").GetString() == expectedStaticProofArtifact &&
        root.GetProperty("locationGuideArtifact").GetString() == expectedLocationGuideArtifact,
        "The solid-entry-ramp runtime evidence no longer binds this exact unpromoted profile, output, executable, data, or artifact set.");
}

static void VerifyRendererEvidence(string path)
{
    using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
    JsonElement root = evidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() == RendererEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        root.GetProperty("profileId").GetString() == RendererProfileId &&
        root.GetProperty("baseOutputImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == RendererImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() == RendererDataSha256 &&
        root.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        !root.GetProperty("promotionAuthorized").GetBoolean(),
        "The entry-ramp control lost its exact renderer-pass predecessor evidence.");
}

static void VerifyRejectedEvidence(string path)
{
    using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
    JsonElement root = evidence.RootElement;
    JsonElement defect = root.GetProperty("structuralDiscriminatorDefect");
    Require(
        root.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() == "runtime-rejected-visibility-discriminator-not-observed" &&
        root.GetProperty("profileId").GetString() == RejectedProfileId &&
        root.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        root.GetProperty("doNotLoadOrRetest").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        defect.GetProperty("overlappingVisualRuntimeKey").GetString() == "4:6:hp" &&
        defect.GetProperty("shadowingCollisionTriangleIndexes").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 1189, 1190 }),
        "The entry-ramp control lost the exact v3 rejection/no-retest boundary.");
}

static void VerifyResult(
    UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult result)
{
    Require(
        result.Plan.ProfileId == UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId &&
        result.Plan.BaseProfileId == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        result.Plan.BaseImageSha256 == BaseImageSha256 &&
        result.Plan.BaseExecutableSha256 == ExecutableSha256 &&
        result.OutputImageSha256 ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputImageSha256 &&
        result.OutputDataSha256 ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ExpectedOutputDataSha256 &&
        result.Plan.ExpectedOutputDataSha256 == result.OutputDataSha256,
        "The entry-ramp control lost an exact profile or payload hash.");
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
        result.Plan.VisualFootprintIntersections.SequenceEqual(new[] { "213:37" }) &&
        result.Plan.AuthoredVertexIndexes.SequenceEqual(new[] { 39, 48 }) &&
        result.Plan.AffectedFaceIndexes.SequenceEqual(new[] { 25, 26, 29, 37, 38, 43 }) &&
        result.Plan.CollisionTriangleIndexes.SequenceEqual(
            new[] { 1353, 1354, 1360, 1361, 1363, 1369, 1370, 1395, 1400, 1401 }) &&
        result.Plan.CountsAndComponentSizesUnchanged &&
        !result.Plan.CollisionIndexRebuildRequired &&
        result.Plan.RequiresDuckStationRuntimeProof,
        "The exact exposed row-80 entry-ramp binding changed.");
    Require(
        result.Plan.Patches.Count == 12 &&
        result.Plan.Patches.Count(patch => patch.Kind == "visual-hp") == 2 &&
        result.Plan.Patches.Count(patch => patch.Kind == "collision-triangle") == 10 &&
        result.Plan.Patches[0].WadOffset == 0x6A3EB60 &&
        result.Plan.Patches[0].BeforeHex == "20 D8 E7 29" &&
        result.Plan.Patches[0].AfterHex == "80 D8 E7 29" &&
        result.Plan.Patches[1].WadOffset == 0x6A3EB84 &&
        result.Plan.Patches[1].BeforeHex == "20 D8 E7 39" &&
        result.Plan.Patches[1].AfterHex == "80 D8 E7 39" &&
        result.Plan.Patches[2].CollisionTriangleIndex == 1353 &&
        result.Plan.Patches[^1].CollisionTriangleIndex == 1401,
        "The exact two-vertex/ten-triangle patch recipe changed.");
    (int Index, int MinX, int MaxX, int MinY, int MaxY, long[] Refs)[] expectedBindings =
    [
        (1353, 30, 30, 24, 25, [0x6A51672, 0x6A51908]),
        (1354, 30, 30, 24, 25, [0x6A51670, 0x6A51906]),
        (1360, 29, 30, 25, 25, [0x6A518E2, 0x6A51904]),
        (1361, 29, 30, 25, 25, [0x6A518E0, 0x6A51902]),
        (1363, 29, 30, 24, 25, [0x6A5166A, 0x6A518DC, 0x6A518FE]),
        (1369, 30, 30, 25, 25, [0x6A518F8]),
        (1370, 30, 30, 25, 25, [0x6A518F6]),
        (1395, 30, 31, 25, 25, [0x6A518EC, 0x6A51924]),
        (1400, 30, 31, 24, 25, [0x6A51660, 0x6A51694, 0x6A518EA, 0x6A51922]),
        (1401, 30, 31, 24, 25, [0x6A51692, 0x6A518E8, 0x6A51920])
    ];
    Require(result.Plan.CollisionBindings.Count == expectedBindings.Length,
        "The exact ten-row collision binding proof changed count.");
    for (int index = 0; index < expectedBindings.Length; index++)
    {
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding actual =
            result.Plan.CollisionBindings[index];
        var expected = expectedBindings[index];
        string[] transformedSource = actual.SourcePoints
            .Select(point => point switch
            {
                (7762, 6474, 512) => "7762,6474,608",
                (7890, 6474, 512) => "7890,6474,608",
                _ => $"{point.X},{point.Y},{point.Z}"
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] authored = actual.AuthoredPoints
            .Select(point => $"{point.X},{point.Y},{point.Z}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(
            actual.TriangleIndex == expected.Index &&
            actual.CyclicWindingPreserved &&
            transformedSource.SequenceEqual(authored) &&
            actual.MinXBlock == expected.MinX &&
            actual.MaxXBlock == expected.MaxX &&
            actual.MinYBlock == expected.MinY &&
            actual.MaxYBlock == expected.MaxY &&
            actual.MinZBlock == 2 &&
            actual.MaxZBlock == 2 &&
            actual.LookupWadOffsets.SequenceEqual(expected.Refs) &&
            actual.Assignment == 0 &&
            actual.Flags == 0,
            $"Collision binding {expected.Index} lost its exact point, winding, cell, lookup, assignment, or flag proof.");
    }
    Require(
        result.Plan.AffectedRawSectorLbas.SequenceEqual(new[] { 54434, 54507 }) &&
        result.ChangedLogicalWadBytes == 42 &&
        result.ChangedPhysicalImageBytes == 267 &&
        result.RebuiltRawSectorCount == 2 &&
        result.ChangedRawSectorCount == 2 &&
        result.RawSectorDiffs.Count == 2 &&
        result.RawSectorDiffs[0] == new UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff(
            54434, 0, 2, 4, 0, 12, 26, 44) &&
        result.RawSectorDiffs[1] == new UnusedLevel65AuthoredTerrainSolidEntryRampControlRawSectorDiff(
            54507, 0, 40, 4, 0, 88, 91, 223) &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.VertexReadbackVerified &&
        result.TerrainFaceBindingsVerified &&
        result.CollisionTriangleReadbackVerified &&
        result.VisualFootprintExposureVerified &&
        result.CollisionFootprintExposureVerified &&
        result.CollisionStructurePreserved &&
        result.CollisionAssignmentsPreserved &&
        result.RawSectorIntegrityVerified &&
        result.RetailTownSquarePreserved &&
        result.Id65OverlayPreserved &&
        result.ExecutablePreserved &&
        result.BaseCandidatePreserved &&
        result.AtomicRenameCompleted,
        "The logical/raw-sector boundary, exposure, collision, integrity, preservation, or transaction proof failed.");
    string checklist = string.Join('\n', result.Plan.RuntimeChecklist);
    Require(
        Contains(checklist, "Moon Jump") &&
        Contains(checklist, "Memory Card 1") &&
        Contains(checklist, "Left, then Down") &&
        Contains(checklist, "walk straight") &&
        Contains(checklist, "Z 608") &&
        Contains(checklist, "old flat Z 512") &&
        Contains(checklist, "retail Town Square") &&
        Contains(checklist, "Gnasty's Loot") &&
        Contains(checklist, "Sunny Flight") &&
        Contains(checklist, "LP terrain is unchanged") &&
        Contains(checklist, "not an Add Terrain pass"),
        "The focused entry-ramp checklist lost a required test or excluded scope.");
}

static void VerifyProofReadback(
    string proofPath,
    UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateResult result,
    RuntimeCandidateFinderReveal finderReveal,
    IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
    string guidePath,
    string guideSha256,
    string expectedOutputCueArtifact,
    string expectedChecklistArtifact,
    string expectedStaticProofArtifact,
    string expectedLocationGuideArtifact)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(proofPath));
    JsonElement root = document.RootElement;
    JsonElement runtimeEvidence = root.GetProperty("runtimeEvidence");
    JsonElement baseEvidence = root.GetProperty("baseRuntimeEvidence");
    JsonElement renderer = root.GetProperty("predecessorRendererRuntimeEvidence");
    JsonElement rejected = root.GetProperty("predecessorRejectedEvidence");
    JsonElement control = root.GetProperty("solidEntryRampControl");
    JsonElement diff = root.GetProperty("diffBoundary");
    JsonElement preserved = root.GetProperty("preserved");
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        !root.TryGetProperty("stale", out _) &&
        root.GetProperty("status").GetString() ==
            "static-proven-external-focused-runtime-pass-recorded" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("generatedFromRuntimeEvidenceRecordedDate").GetString() ==
            FocusedRuntimeEvidenceRecordedDate &&
        runtimeEvidence.GetProperty("evidenceId").GetString() == FocusedRuntimeEvidenceId &&
        runtimeEvidence.GetProperty("evidencePath").GetString() == FocusedRuntimeEvidencePath &&
        runtimeEvidence.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        runtimeEvidence.GetProperty("evidenceScope").GetString() ==
            "complete-edited-surface-solidity-only" &&
        !runtimeEvidence.GetProperty("automatedEmulatorCapture").GetBoolean() &&
        !runtimeEvidence.GetProperty("promotionAuthorized").GetBoolean() &&
        !runtimeEvidence.GetProperty("normalEditorIntegrationAuthorized").GetBoolean() &&
        !runtimeEvidence.GetProperty("normalCreateBinIntegrationAuthorized").GetBoolean() &&
        runtimeEvidence.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId &&
        runtimeEvidence.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        runtimeEvidence.GetProperty("outputId65DataSha256").GetString() == result.OutputDataSha256 &&
        runtimeEvidence.GetProperty("outputExecutableSha256").GetString() == ExecutableSha256 &&
        runtimeEvidence.GetProperty("outputCue").GetString() == expectedOutputCueArtifact &&
        runtimeEvidence.GetProperty("runtimeChecklistArtifact").GetString() == expectedChecklistArtifact &&
        runtimeEvidence.GetProperty("staticProofArtifact").GetString() == expectedStaticProofArtifact &&
        runtimeEvidence.GetProperty("locationGuideArtifact").GetString() == expectedLocationGuideArtifact &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId &&
        root.GetProperty("baseImageSha256").GetString() == BaseImageSha256 &&
        root.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        root.GetProperty("outputId65DataSha256").GetString() == result.OutputDataSha256 &&
        root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean(),
        "The static proof lost its external focused-pass binding, static runtimeClaim=false boundary, or exact hashes.");
    Require(
        baseEvidence.GetProperty("evidenceId").GetString() == DisplayEvidenceId &&
        baseEvidence.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        baseEvidence.GetProperty("outputImageSha256").GetString() == BaseImageSha256 &&
        !baseEvidence.GetProperty("promotionAuthorized").GetBoolean() &&
        renderer.GetProperty("evidenceId").GetString() == RendererEvidenceId &&
        renderer.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        renderer.GetProperty("outputImageSha256").GetString() == RendererImageSha256 &&
        !renderer.GetProperty("reusedAsBase").GetBoolean() &&
        !renderer.GetProperty("promotionAuthorized").GetBoolean() &&
        rejected.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        rejected.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        rejected.GetProperty("doNotLoadOrRetest").GetBoolean() &&
        control.GetProperty("runtimeKey").GetString() == "213:37:hp" &&
        control.GetProperty("authoredRidgeZ").GetInt32() == 608 &&
        control.GetProperty("visualFootprintIntersections").EnumerateArray()
            .Select(value => value.GetString()).SequenceEqual(new[] { "213:37" }) &&
        control.GetProperty("collisionTriangleIndexes").GetArrayLength() == 10 &&
        control.GetProperty("collisionBindings").GetArrayLength() == 10 &&
        control.GetProperty("fullSceneSectorScanCount").GetInt32() == 216 &&
        !control.GetProperty("collisionIndexRebuildRequired").GetBoolean() &&
        control.GetProperty("countsAndComponentSizesUnchanged").GetBoolean(),
        "The proof lost the v3 rejection boundary or exact exposed-ramp recipe.");
    Require(
        root.GetProperty("logicalPatches").GetArrayLength() == 12 &&
        diff.GetProperty("changedLogicalWadBytes").GetInt64() == 42 &&
        diff.GetProperty("changedPhysicalImageBytes").GetInt64() == 267 &&
        diff.GetProperty("affectedRawSectorLbas").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 54434, 54507 }) &&
        diff.GetProperty("rawSectorDiffs").GetArrayLength() == 2 &&
        SerializedRawSectorDiffMatches(
            diff.GetProperty("rawSectorDiffs")[0], 54434, 0, 2, 4, 0, 12, 26, 44) &&
        SerializedRawSectorDiffMatches(
            diff.GetProperty("rawSectorDiffs")[1], 54507, 0, 40, 4, 0, 88, 91, 223) &&
        diff.GetProperty("visualFootprintExposureVerified").GetBoolean() &&
        diff.GetProperty("collisionFootprintExposureVerified").GetBoolean() &&
        diff.GetProperty("rawSectorIntegrityVerified").GetBoolean() &&
        handoff.GetProperty("cuePath").GetString() == finderReveal.CuePath &&
        handoff.GetProperty("pairedBinPath").GetString() == finderReveal.PairedBinPath &&
        handoff.GetProperty("finderRevealHelperPath").GetString() == finderReveal.HelperPath &&
        handoff.GetProperty("guidePath").GetString() == guidePath &&
        handoff.GetProperty("guideSha256").GetString() == guideSha256 &&
        handoff.GetProperty("cuePairingVerified").GetBoolean() &&
        handoff.GetProperty("helperIsExecutable").GetBoolean() &&
        handoff.GetProperty("guideVerified").GetBoolean() &&
        handoff.GetProperty("loadCodes").GetArrayLength() == loadCodes.Count &&
        preserved.GetProperty("injectedRollbackRestoredPriorPair").GetBoolean(),
        "The proof lost the 42-byte/two-sector boundary or exact CUE/Finder/guide/code handoff.");
    JsonElement bindings = control.GetProperty("collisionBindings");
    for (int index = 0; index < result.Plan.CollisionBindings.Count; index++)
    {
        UnusedLevel65AuthoredTerrainSolidEntryRampControlCollisionBinding expected =
            result.Plan.CollisionBindings[index];
        JsonElement actual = bindings[index];
        JsonElement bounds = actual.GetProperty("blockBounds");
        Require(
            actual.GetProperty("triangleIndex").GetInt32() == expected.TriangleIndex &&
            actual.GetProperty("sourcePoints").GetArrayLength() == 3 &&
            actual.GetProperty("authoredPoints").GetArrayLength() == 3 &&
            actual.GetProperty("cyclicWindingPreserved").GetBoolean() &&
            bounds.GetProperty("minXBlock").GetInt32() == expected.MinXBlock &&
            bounds.GetProperty("maxXBlock").GetInt32() == expected.MaxXBlock &&
            bounds.GetProperty("minYBlock").GetInt32() == expected.MinYBlock &&
            bounds.GetProperty("maxYBlock").GetInt32() == expected.MaxYBlock &&
            bounds.GetProperty("minZBlock").GetInt32() == 2 &&
            bounds.GetProperty("maxZBlock").GetInt32() == 2 &&
            actual.GetProperty("lookupWadOffsets").EnumerateArray()
                .Select(value => Convert.ToInt64(value.GetString()![2..], 16))
                .SequenceEqual(expected.LookupWadOffsets) &&
            actual.GetProperty("assignment").GetInt32() == 0 &&
            actual.GetProperty("flags").GetInt32() == 0,
            $"The serialized proof for collision binding {expected.TriangleIndex} drifted.");
    }
    JsonElement structureHashes = control.GetProperty("collisionStructureHashes");
    Require(
        structureHashes.GetProperty("collisionHeaderSha256").GetString() == result.Plan.CollisionHeaderSha256 &&
        structureHashes.GetProperty("collisionBlockTreeSha256").GetString() == result.Plan.CollisionBlockTreeSha256 &&
        structureHashes.GetProperty("collisionBlocksSha256").GetString() == result.Plan.CollisionBlocksSha256 &&
        structureHashes.GetProperty("collisionAssignmentsSha256").GetString() == result.Plan.CollisionAssignmentsSha256 &&
        structureHashes.GetProperty("collisionFlagsSha256").GetString() == result.Plan.CollisionFlagsSha256,
        "The serialized collision structure hashes drifted.");
}

static bool SerializedRawSectorDiffMatches(
    JsonElement value,
    int lba,
    int header,
    int payload,
    int edc,
    int reserved,
    int eccP,
    int eccQ,
    int total) =>
    value.GetProperty("rawSectorLba").GetInt32() == lba &&
    value.GetProperty("headerChangedBytes").GetInt32() == header &&
    value.GetProperty("payloadChangedBytes").GetInt32() == payload &&
    value.GetProperty("edcChangedBytes").GetInt32() == edc &&
    value.GetProperty("reservedChangedBytes").GetInt32() == reserved &&
    value.GetProperty("eccPChangedBytes").GetInt32() == eccP &&
    value.GetProperty("eccQChangedBytes").GetInt32() == eccQ &&
    value.GetProperty("totalChangedBytes").GetInt32() == total;

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

static string BuildGuideSvg(
    IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
    string imageSha256,
    string profileId,
    string cueFileName)
{
    string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? value;
    string exactCodeMetadata = Escape(string.Join(
        " | ",
        loadCodes.Select(code => $"{code.TestName} (ID{code.LevelId}): {code.InputCode}")));
    string[] codeNames = loadCodes.Select(code => $"{Escape(code.TestName)} (ID{code.LevelId})").ToArray();
    string[] codeSelections = loadCodes.Select(code => Escape(code.TargetSelection.ToUpperInvariant())).ToArray();
    return $$"""
<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="1000" viewBox="0 0 1200 1000" role="img" aria-labelledby="title desc">
  <title id="title">ID65 Solid Entry Ridge Test Guide</title>
  <desc id="desc">Walk straight ahead from spawn over the authored solid entry ridge.</desc>
  <metadata id="exact-load-codes">{{exactCodeMetadata}}</metadata>
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop stop-color="#091322"/><stop offset="1" stop-color="#18304d"/></linearGradient>
    <linearGradient id="up" x1="0" y1="1" x2="0" y2="0"><stop stop-color="#ffd166"/><stop offset="1" stop-color="#f77f00"/></linearGradient>
    <linearGradient id="down" x1="0" y1="0" x2="0" y2="1"><stop stop-color="#f77f00"/><stop offset="1" stop-color="#55b7ff"/></linearGradient>
    <pattern id="hatch" width="12" height="12" patternUnits="userSpaceOnUse" patternTransform="rotate(35)"><rect width="12" height="12" fill="#315b78"/><line x1="0" y1="0" x2="0" y2="12" stroke="#6ba4c8" stroke-width="4"/></pattern>
    <marker id="arrow" markerWidth="12" markerHeight="12" refX="10" refY="6" orient="auto"><path d="M0,0 L12,6 L0,12 z" fill="#7ef9ff"/></marker>
  </defs>
  <rect width="1200" height="1000" rx="24" fill="url(#bg)"/>
  <text x="60" y="62" fill="#ffffff" font-family="-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif" font-size="34" font-weight="800">ID65 SOLID ENTRY RIDGE — WALK STRAIGHT AHEAD</text>
  <text x="60" y="99" fill="#ffcf5a" font-family="-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif" font-size="23" font-weight="700">Do not turn or search for gems.</text>
  <rect x="75" y="135" width="760" height="760" rx="20" fill="#0c1b2c" stroke="#4d7697" stroke-width="2"/>
  <text x="95" y="173" fill="#a9d9ff" font-family="monospace" font-size="18">+Y = FORWARD ↑     +X = RIGHT →</text>
  <polygon points="256.8,624.8 410.4,624.8 410.4,471.2 256.8,471.2" fill="url(#hatch)" opacity="0.8"/>
  <polygon points="564,624.8 717.6,624.8 717.6,471.2 564,471.2" fill="url(#hatch)" opacity="0.8"/>
  <polygon points="256.8,471.2 410.4,471.2 410.4,317.6 256.8,317.6" fill="url(#hatch)" opacity="0.65"/>
  <polygon points="564,471.2 717.6,471.2 717.6,317.6 564,317.6" fill="url(#hatch)" opacity="0.65"/>
  <rect x="410.4" y="471.2" width="153.6" height="153.6" fill="url(#up)" stroke="#ffe09a" stroke-width="3"/>
  <rect x="410.4" y="317.6" width="153.6" height="153.6" fill="url(#down)" stroke="#a8dcff" stroke-width="3"/>
  <line x1="410.4" y1="471.2" x2="564" y2="471.2" stroke="#ff334e" stroke-width="12"/>
  <circle cx="488.4" cy="701.6" r="18" fill="#7ef9ff" stroke="#ffffff" stroke-width="4"/>
  <line x1="488.4" y1="683" x2="488.4" y2="635" stroke="#7ef9ff" stroke-width="8" marker-end="url(#arrow)"/>
  <line x1="488.4" y1="665" x2="488.4" y2="335" stroke="#ffffff" stroke-width="3" stroke-dasharray="10 10" opacity="0.85"/>
  <text x="510" y="711" fill="#ffffff" font-family="sans-serif" font-size="20" font-weight="700">1 STOP AT SPAWN</text>
  <text x="585" y="615" fill="#ffd166" font-family="sans-serif" font-size="19" font-weight="700">2 WALK / CHARGE UP</text>
  <text x="585" y="477" fill="#ff6377" font-family="sans-serif" font-size="19" font-weight="700">3 CROSS CREST</text>
  <text x="585" y="360" fill="#81c9ff" font-family="sans-serif" font-size="19" font-weight="700">4 WALK / CHARGE DOWN</text>
  <text x="430" y="283" fill="#ffffff" font-family="sans-serif" font-size="19" font-weight="700">5 TURN AROUND AND REPEAT</text>
  <text x="95" y="648" fill="#d9ebf8" font-family="sans-serif" font-size="16">near edge: 64 units ahead · Z512</text>
  <text x="95" y="493" fill="#ff8fa0" font-family="sans-serif" font-size="16">solid crest: 192 units ahead · Z608</text>
  <text x="95" y="339" fill="#a8dcff" font-family="sans-serif" font-size="16">flat again: 320 units ahead · Z512</text>
  <rect x="855" y="135" width="310" height="760" rx="20" fill="#101e30" stroke="#4d7697" stroke-width="2"/>
  <text x="880" y="178" fill="#ffffff" font-family="sans-serif" font-size="25" font-weight="800">PASS / FAIL</text>
  <text x="880" y="220" fill="#69f0ae" font-family="sans-serif" font-size="17" font-weight="700">ID65: ridge visible + solid</text>
  <text x="880" y="249" fill="#69f0ae" font-family="sans-serif" font-size="17">Retail Town Square: flat</text>
  <text x="880" y="285" fill="#ff7b8c" font-family="sans-serif" font-size="16">FAIL: pass-through, old flat</text>
  <text x="880" y="308" fill="#ff7b8c" font-family="sans-serif" font-size="16">collision, snag, launch, or fall</text>
  <line x1="880" y1="335" x2="1140" y2="335" stroke="#38516d"/>
  <text x="880" y="370" fill="#ffffff" font-family="sans-serif" font-size="22" font-weight="800">FULL LOAD CODES</text>
  <text x="880" y="395" fill="#ffffff" font-family="sans-serif" font-size="13" font-weight="700">{{codeNames[0]}}</text>
  <text x="880" y="411" fill="#b9d9ef" font-family="monospace" font-size="10">SELECT → R1 → R2 → L1 → L2</text>
  <text x="880" y="425" fill="#b9d9ef" font-family="monospace" font-size="10">→ R1 → L1 → R2 → L2 → {{codeSelections[0]}}</text>
  <text x="880" y="451" fill="#ffffff" font-family="sans-serif" font-size="13" font-weight="700">{{codeNames[1]}}</text>
  <text x="880" y="467" fill="#b9d9ef" font-family="monospace" font-size="10">SELECT → R1 → R2 → L1 → L2</text>
  <text x="880" y="481" fill="#b9d9ef" font-family="monospace" font-size="10">→ R1 → L1 → R2 → L2 → {{codeSelections[1]}}</text>
  <text x="880" y="507" fill="#ffffff" font-family="sans-serif" font-size="13" font-weight="700">{{codeNames[2]}}</text>
  <text x="880" y="523" fill="#b9d9ef" font-family="monospace" font-size="10">SELECT → R1 → R2 → L1 → L2</text>
  <text x="880" y="537" fill="#b9d9ef" font-family="monospace" font-size="10">→ R1 → L1 → R2 → L2 → {{codeSelections[2]}}</text>
  <text x="880" y="563" fill="#ffffff" font-family="sans-serif" font-size="13" font-weight="700">{{codeNames[3]}}</text>
  <text x="880" y="579" fill="#b9d9ef" font-family="monospace" font-size="10">SELECT → R1 → R2 → L1 → L2</text>
  <text x="880" y="593" fill="#b9d9ef" font-family="monospace" font-size="10">→ R1 → L1 → R2 → L2 → {{codeSelections[3]}}</text>
  <text x="880" y="640" fill="#ffcf5a" font-family="sans-serif" font-size="17" font-weight="800">CENTER ROUTE IS PRIMARY</text>
  <text x="880" y="669" fill="#dcecff" font-family="sans-serif" font-size="14">Side tapers are steeper.</text>
  <text x="880" y="695" fill="#dcecff" font-family="sans-serif" font-size="14">Test them separately.</text>
  <text x="880" y="755" fill="#ffffff" font-family="sans-serif" font-size="17" font-weight="800">BOOT RULES</text>
  <text x="880" y="786" fill="#ffcf5a" font-family="sans-serif" font-size="14">Cheats OFF · Moon Jump OFF</text>
  <text x="880" y="812" fill="#ffcf5a" font-family="sans-serif" font-size="14">Card 1/2 NONE · cold boot CUE</text>
  <text x="880" y="838" fill="#ffcf5a" font-family="sans-serif" font-size="14">No save state · under 8 minutes</text>
  <text x="60" y="920" fill="#dcecff" font-family="sans-serif" font-size="17">Start at the landing. Walk the dashed center line forward and backward. Test walking, charge, jump, and landing.</text>
  <text x="60" y="945" fill="#9fc1d9" font-family="monospace" font-size="10">External focused solidity pass recorded · static guide · unpromoted · BIN SHA-256: {{imageSha256}}</text>
  <text x="60" y="965" fill="#9fc1d9" font-family="monospace" font-size="9">Profile: {{Escape(profileId)}}</text>
  <text x="60" y="984" fill="#9fc1d9" font-family="monospace" font-size="9">Use only with exact CUE: {{Escape(cueFileName)}}</text>
</svg>
""";
}

static bool Contains(string value, string expected) =>
    value.Contains(expected, StringComparison.OrdinalIgnoreCase);

static string RepositoryRelativePath(string repositoryRoot, string path) =>
    Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');

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
    string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    try
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        await using FileStream stream = new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
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
    string[] debris = Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Where(name =>
            name != null &&
            (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".work", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".tmp.", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".bak.", StringComparison.OrdinalIgnoreCase)))
        .Select(name => name!)
        .ToArray();
    Require(debris.Length == 0,
        $"The entry-ramp exporter left temporary, backup, or work debris: {string.Join(", ", debris)}.");
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
