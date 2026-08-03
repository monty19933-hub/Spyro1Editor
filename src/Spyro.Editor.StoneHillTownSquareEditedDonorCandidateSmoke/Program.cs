using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

const int EditedTrueIndex = 21;
const float EditedWorldXDelta = -128f;
const long ExpectedDonorRecordWadOffset = 0x136E8A8;
const long ExpectedTargetRecordWadOffset = 0xD640A8;
const long ExpectedDonorPatchWadOffset = 0x136E8B4;
const long ExpectedTargetPatchWadOffset = 0xD640B4;
const long ExpectedDonorRenderRadiusWadOffset = 0x136E8F8;
const long ExpectedTargetRenderRadiusWadOffset = 0xD640F8;
const long ExpectedDonorUpdateScheduleWadOffset = 0x136E8FA;
const long ExpectedTargetUpdateScheduleWadOffset = 0xD640FA;
const string ExpectedOutputImageSha256 =
    "ec3d8e354cf246d704860a6b26968a59cc7f77fe6409e08c299a777b7fc4df8e";
const string ExpectedRenderRadiusOutputImageSha256 =
    "a3db572356470e697c643e474728b5e75a73fa813fe9868143fb2ea6a4a98f36";
const long ExpectedRenderRadiusChangedPhysicalBytes = 46;
const string ExpectedUnconditionalUpdateOutputImageSha256 =
    "580af811c2f3e130f03fc1556da56d8310064a588eb57f819f5129c74ccc9329";
const long ExpectedUnconditionalUpdateChangedPhysicalBytes = 53;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
(string sourceImage, string sourceCue) = ResolveCleanSource(repositoryRoot, args);
LevelCatalog catalog = LevelCatalog.Load(repositoryRoot);
LevelDefinition townSquare = catalog.FindByKey("townsquare")
    ?? throw new InvalidOperationException("The level catalog is missing Town Square.");

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "edited-donor-candidate");
Directory.CreateDirectory(outputRoot);
string editPath = Path.Combine(outputRoot, "town-square-T21-x-only-native-edits.json");
string cachePath = Path.Combine(repositoryRoot, "editor-cache", "townsquare-mobys.json");
List<Moby> mobys = MobyLoader.LoadCached(cachePath).ToList();
Moby edited = mobys.Single(moby => moby.TrueIndex == EditedTrueIndex);
Require(
    edited.OriginalPosition == new Vector3f(7813.75f, 7189.75f, 512f),
    "Town Square T21's clean-cache position changed; the fixture must not silently retarget another object.");
byte[] donorRecordBefore = ReadMode2WadBytes(
    sourceImage,
    ExpectedDonorRecordWadOffset,
    MobyLoader.RuntimeRecordStride);
Require(
    donorRecordBefore[0x4A] == 0xFF &&
    donorRecordBefore[0x4B] == 0x00 &&
    donorRecordBefore[0x50] == 0x18 &&
    donorRecordBefore[0x51] == 0x00 &&
    donorRecordBefore[0x52] == 0x40 &&
    donorRecordBefore[0x53] == 0xFF &&
    edited.Flag4A == 0x40 &&
    edited.Flag4B == 0xFF,
    "Town Square T21's native byte fixture changed: +0x4A/+0x4B must be FF/00, while the legacy cache flag4A/flag4B names map to +0x52/+0x53 = 40/FF.");
edited.Position = edited.OriginalPosition with { X = edited.OriginalPosition.X + EditedWorldXDelta };
edited.Label = "Red Gem";
Require(
    await MobyEditStore.SaveAsync(editPath, mobys, townSquare.DisplayName) == 1,
    "The deterministic fixture did not save exactly one native Moby edit.");

string stagingPrefix = Path.Combine(outputRoot, "town-square-edited-donor-plan-only");
MobySourcePatchPlan editorObjectPlan = MobySourcePatchExporter.BuildPlan(
    sourceImage,
    sourceCue,
    stagingPrefix + ".bin",
    stagingPrefix + ".cue",
    townSquare,
    editPath);
Require(
    editorObjectPlan.LevelKey == "townsquare" &&
    editorObjectPlan.SourceRecordCount == townSquare.SourceRecordCount &&
    editorObjectPlan.PatchCount == 1 &&
    editorObjectPlan.TotalPatchedBytes == 4 &&
    editorObjectPlan.SkippedEdits.Count == 0 &&
    editorObjectPlan.Patches.Count == 1 &&
    editorObjectPlan.Patches.Single() is { } patch &&
    patch.Kind == "moby-position-x" &&
    ParseHexOffset(patch.WadRelativeOffset) == ExpectedDonorPatchWadOffset &&
    !editorObjectPlan.Patches.Any(candidate =>
        candidate.Kind == "moby-placement-sector" ||
        string.Equals(candidate.RecordOffset, "0x4A", StringComparison.OrdinalIgnoreCase)),
    "The genuine saved Town Square edit must produce exactly its requested X patch while preserving native +0x4A = FF. " +
    $"patchCount={editorObjectPlan.PatchCount}, bytes={editorObjectPlan.TotalPatchedBytes}, " +
    $"patches=[{string.Join(", ", editorObjectPlan.Patches.Select(candidate => $"{candidate.Kind}@{candidate.WadRelativeOffset}+{candidate.ByteLength}"))}], " +
    $"skips=[{string.Join("; ", editorObjectPlan.SkippedEdits)}].");
MobySourcePatch donorPatch = editorObjectPlan.Patches.Single(patch =>
        patch.Kind == "moby-position-x" &&
        ParseHexOffset(patch.WadRelativeOffset) == ExpectedDonorPatchWadOffset);
Require(
    donorPatch.Kind == "moby-position-x" &&
    donorPatch.LevelKey == "townsquare" &&
    donorPatch.TrueIndex == EditedTrueIndex &&
    ParseHexOffset(donorPatch.WadRelativeOffset) == ExpectedDonorPatchWadOffset &&
    donorPatch.RecordOffset == "0xC" &&
    donorPatch.ByteLength == 4 &&
    NormalizeHex(donorPatch.BeforeHexPreview) == "5CE80100" &&
    NormalizeHex(donorPatch.AfterHexPreview) == "5CE00100",
    "Town Square T21's X-only donor patch changed offset, kind, length, or exact bytes.");
MobySourceEditOutcome editorOutcome = editorObjectPlan.EditOutcomes?.Single()
    ?? throw new InvalidDataException("The genuine saved Town Square edit has no unique source outcome.");
Require(
    editorOutcome.PatchKinds.Count == 1 &&
    editorOutcome.PatchKinds.Single() == "moby-position-x",
    "The genuine saved edit outcome must identify only the requested T21 X patch.");
// Consume the genuine editor plan without filtering. Native +0x4A = FF is a
// retail sentinel and must remain byte-identical; the previously derived D5
// candidate is retained only as rejected historical diagnostic evidence.
MobySourcePatchPlan objectPlan = editorObjectPlan;

(string identityImage, string identityCue) = await EnsureExactIdentityBaseAsync(
    repositoryRoot,
    sourceImage,
    sourceCue,
    catalog);
string identityHashBefore = await HashFileAsync(identityImage);
string retailHashBefore = await HashFileAsync(sourceImage);
string outputPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-edited-T21-X-FF-visible-RUNTIME-CANDIDATE");
StoneHillTownSquareEditedDonorCandidateRequest request = new(
    identityImage,
    identityCue,
    sourceImage,
    objectPlan,
    outputPrefix + ".bin",
    outputPrefix + ".cue");

MobySourcePatch extraPatch = donorPatch with
{
    Label = "townsquare-T21-unsafe-extra-y",
    Kind = "moby-position-y",
    RecordOffset = "0x10",
    WadRelativeOffset = "0x136E8B8"
};
MobySourcePatchPlan extraPatchPlan = objectPlan with
{
    PatchCount = 2,
    TotalPatchedBytes = 8,
    Patches = [donorPatch, extraPatch]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        request with { MobyPatchPlan = extraPatchPlan }),
    "An extra Town Square source patch passed the edited-donor X-only gate.");
MobySourcePatchPlan outsideDonorPlan = objectPlan with
{
    Patches =
    [
        donorPatch with
        {
            WadRelativeOffset = "0x118E800"
        }
    ]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        request with { MobyPatchPlan = outsideDonorPlan }),
    "A patch outside the checked donor data/record offset passed the edited-donor X-only gate.");

StoneHillTownSquareEditedDonorCandidatePlan plan =
    await StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(request);
Require(
    plan.RecipeId == StoneHillTownSquareEditedDonorCandidateComposer.RecipeId &&
    plan.RecipeVersion == StoneHillTownSquareEditedDonorCandidateComposer.RecipeVersion &&
    plan.Evidence == NativeLevelReplacementEvidenceStatus.StaticBaselineOnly &&
    plan.EvidenceId == StoneHillTownSquareEditedDonorCandidateComposer.EvidenceId &&
    plan.Safety.Status == StoneHillTownSquareEditedDonorCandidateComposer.Status &&
    plan.Safety.RequiresDuckStationRuntimeProof &&
    plan.BaseImageSha256 == identityHashBefore &&
    plan.RetailSourceImageSha256 == retailHashBefore &&
    plan.PlacementPatch == null,
    "The edited-donor plan was not bound to the exact X-only recipe, identity control, retail source, and native placement sentinel.");
Require(
    plan.Patch.Kind == "moby-position-x" &&
    plan.Patch.TrueIndex == EditedTrueIndex &&
    plan.Patch.DonorWadOffset == ExpectedDonorPatchWadOffset &&
    plan.Patch.TargetWadOffset == ExpectedTargetPatchWadOffset &&
    plan.Patch.ByteLength == 4 &&
    plan.Patch.BeforeHex == "5CE80100" &&
    plan.Patch.AfterHex == "5CE00100" &&
    IsSha256(plan.Patch.BeforeSha256) &&
    IsSha256(plan.Patch.AfterSha256) &&
    plan.Patch.BeforeSha256 != plan.Patch.AfterSha256,
    "The checked donor-to-target rebase or exact T21 X byte contract changed.");

string expectedProofPath = outputPrefix + "-static-proof.json";
string expectedChecklistPath = outputPrefix + "-runtime-checklist.md";
await File.WriteAllTextAsync(expectedProofPath, "{\"status\":\"stale-interrupted-sidecar\"}");
await File.WriteAllTextAsync(expectedChecklistPath, "stale interrupted sidecar");
StoneHillTownSquareEditedDonorArtifactResult artifact =
    await StoneHillTownSquareEditedDonorArtifactWriter.ExportAsync(request);
StoneHillTownSquareEditedDonorCandidateResult result = artifact.Candidate;
await File.AppendAllTextAsync(
    artifact.RuntimeChecklistPath,
    """

    ## Corrected native-FF visibility focus

    - Begin with T21 outside its near-detail range and hold the camera on its area.
    - Approach slowly from far distance, then back away and repeat from a second camera angle.
    - Confirm T21 remains continuously visible with no blink, pop-out, or distance flicker.
    - Reconfirm the same camera approach after collection/reload where applicable.
    """);
Require(
    result.Plan.RecipeId == plan.RecipeId &&
    result.Plan.RecipeVersion == plan.RecipeVersion &&
    result.Plan.Patch == plan.Patch &&
    result.Plan.PlacementPatch == null,
    "The exported edited-donor candidate did not retain the checked plan recipe.");
Require(
    result.OutputImageSha256 == ExpectedOutputImageSha256 &&
    IsSha256(result.OutputImageSha256) &&
    result.ChangedLogicalWadBytes == 1 &&
    result.ChangedPhysicalImageBytes == 37 &&
    result.RebuiltRawSectorCount == 1 &&
    result.ExactLogicalDiffBoundaryVerified &&
    result.ExactPhysicalSectorBoundaryVerified &&
    result.OriginalTownSquareDonorPreserved &&
    result.DisplayIdentityExecutablePreserved &&
    result.BaseImagePreserved &&
    result.RetailSourcePreserved &&
    result.BinCuePublishCompleted &&
    !result.PlacementSectorReadbackVerified,
    "The final X-only candidate changed hash/boundary or omitted an exact diff, source, donor, SCUS, sentinel, or publication proof.");
byte[] identityTargetBefore = ReadMode2WadBytes(
    identityImage,
    ExpectedTargetRecordWadOffset,
    MobyLoader.RuntimeRecordStride);
byte[] outputTargetAfter = ReadMode2WadBytes(
    result.OutputImagePath,
    ExpectedTargetRecordWadOffset,
    MobyLoader.RuntimeRecordStride);
Require(
    identityTargetBefore[0x4A] == 0xFF &&
    identityTargetBefore[0x4B] == 0x00 &&
    outputTargetAfter[0x4A] == 0xFF &&
    outputTargetAfter[0x4B] == 0x00 &&
    outputTargetAfter[0x52] == 0x40 &&
    outputTargetAfter[0x53] == 0xFF,
    "The X-only candidate did not preserve T21 +0x4A/+0x4B = FF/00 and legacy flag4A/flag4B bytes +0x52/+0x53 = 40/FF.");
Require(
    artifact.StaticProofPath == expectedProofPath &&
    artifact.RuntimeChecklistPath == expectedChecklistPath &&
    File.Exists(result.OutputImagePath) &&
    File.Exists(result.OutputCuePath) &&
    File.Exists(artifact.StaticProofPath) &&
    File.Exists(artifact.RuntimeChecklistPath),
    "The edited-donor artifact writer did not publish the complete disposable CUE and evidence set.");
using (JsonDocument proof = JsonDocument.Parse(await File.ReadAllTextAsync(artifact.StaticProofPath)))
{
    Require(
        proof.RootElement.GetProperty("status").GetString() ==
            StoneHillTownSquareEditedDonorCandidateComposer.Status &&
        !proof.RootElement.GetProperty("runtimeClaim").GetBoolean() &&
        proof.RootElement.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        proof.RootElement.GetProperty("evidenceId").GetString() == plan.EvidenceId &&
        proof.RootElement.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256 &&
        proof.RootElement.GetProperty("changedLogicalWadBytes").GetInt64() == 1 &&
        proof.RootElement.GetProperty("changedPhysicalImageBytes").GetInt64() == 37 &&
        proof.RootElement.GetProperty("rebuiltRawSectorCount").GetInt32() == 1 &&
        !proof.RootElement.GetProperty("placementSectorReadbackVerified").GetBoolean() &&
        proof.RootElement.GetProperty("plan").GetProperty("placementPatch").ValueKind == JsonValueKind.Null,
        "The edited-donor static proof omitted its exact X-only boundary, preserved placement sentinel, or final hash.");
}
string checklist = await File.ReadAllTextAsync(artifact.RuntimeChecklistPath);
Require(
    checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) &&
    checklist.Contains("not runtime proof", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("T21", StringComparison.Ordinal) &&
    checklist.Contains("X position changed", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("visibility sentinel", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("remains unchanged", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("outside its near-detail range", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("second camera angle", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("distance flicker", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("original retail Town Square", StringComparison.Ordinal),
    "The edited-donor checklist omitted the exact candidate, actor, native-sentinel boundary, or donor-isolation checks.");
Require(
    await HashFileAsync(identityImage) == identityHashBefore &&
    await HashFileAsync(sourceImage) == retailHashBefore &&
    !Directory.EnumerateFiles(outputRoot, "*.tmp").Any() &&
    !Directory.EnumerateFiles(outputRoot, "*.bak").Any(),
    "The identity/retail inputs changed or temporary publication files remained after export.");

string determinismPrefix = Path.Combine(outputRoot, "determinism-recheck");
try
{
    StoneHillTownSquareEditedDonorCandidateResult deterministic =
        await StoneHillTownSquareEditedDonorCandidateComposer.ExportAsync(
            request with
            {
                OutputImagePath = determinismPrefix + ".bin",
                OutputCuePath = determinismPrefix + ".cue"
            });
    Require(
        deterministic.OutputImageSha256 == result.OutputImageSha256 &&
        deterministic.OutputImageSha256 == ExpectedOutputImageSha256 &&
        deterministic.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        deterministic.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes &&
        !deterministic.PlacementSectorReadbackVerified &&
        deterministic.Plan.PlacementPatch == null,
        "A second edited-donor export was not byte-deterministic.");
}
finally
{
    DeleteIfExists(determinismPrefix + ".bin");
    DeleteIfExists(determinismPrefix + ".cue");
}

MobySourcePatch renderRadiusPatch = donorPatch with
{
    Label = "townsquare-T21-render-radius-18-to-7f",
    Kind = "moby-render-radius",
    RecordOffset = "0x50",
    WadRelativeOffset = $"0x{ExpectedDonorRenderRadiusWadOffset:X}",
    ImageOffset = "research-rebased-at-compose-time",
    ByteLength = 1,
    BeforeHexPreview = "18",
    AfterHexPreview = "7F",
    Description = "Isolated native render-radius diagnostic: expand moved T21 from native loose-gem value 0x18 to the maximum safe positive value 0x7F; values 0x80-0xFF select the renderer's special negative-radius path. This is not a loose-gem promotion rule."
};
MobySourcePatchPlan renderRadiusPlan = objectPlan with
{
    PatchCount = 2,
    TotalPatchedBytes = 5,
    Patches = [donorPatch, renderRadiusPatch],
    EditOutcomes =
    [
        editorOutcome with
        {
            PatchKinds = ["moby-position-x", "moby-render-radius"]
        }
    ],
    Notes = objectPlan.Notes
        .Concat([
            "Focused far-flicker diagnostic: keep +0x4A = FF and +0x52 = 40; change only T21 X plus native render radius +0x50 from 0x18 to maximum safe positive 0x7F."
        ])
        .ToArray()
};
string renderRadiusPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-edited-T21-X-render-radius-7F-RUNTIME-CANDIDATE");
StoneHillTownSquareEditedDonorCandidateRequest renderRadiusRequest = request with
{
    MobyPatchPlan = renderRadiusPlan,
    OutputImagePath = renderRadiusPrefix + ".bin",
    OutputCuePath = renderRadiusPrefix + ".cue"
};
StoneHillTownSquareEditedDonorCandidatePlan renderRadiusCandidatePlan =
    await StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(renderRadiusRequest);
StoneHillTownSquareEditedDonorPatchSummary radiusSummary =
    renderRadiusCandidatePlan.RenderRadiusPatch
    ?? throw new InvalidDataException("The isolated render-radius plan has no render-radius summary.");
Require(
    renderRadiusCandidatePlan.RecipeId ==
        StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusRecipeId &&
    renderRadiusCandidatePlan.RecipeVersion ==
        StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusRecipeVersion &&
    renderRadiusCandidatePlan.EvidenceId ==
        StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusEvidenceId &&
    renderRadiusCandidatePlan.Safety.Status ==
        StoneHillTownSquareEditedDonorCandidateComposer.RenderRadiusStatus &&
    renderRadiusCandidatePlan.PlacementPatch == null &&
    radiusSummary.Kind == "moby-render-radius" &&
    radiusSummary.TrueIndex == EditedTrueIndex &&
    radiusSummary.DonorWadOffset == ExpectedDonorRenderRadiusWadOffset &&
    radiusSummary.TargetWadOffset == ExpectedTargetRenderRadiusWadOffset &&
    radiusSummary.ByteLength == 1 &&
    radiusSummary.BeforeHex == "18" &&
    radiusSummary.AfterHex == "7F",
    "The isolated render-radius plan changed recipe, relocation, bytes, or evidence boundary.");
MobySourcePatchPlan unsafeRadiusPlan = renderRadiusPlan with
{
    Patches = [donorPatch, renderRadiusPatch with { AfterHexPreview = "80" }]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        renderRadiusRequest with { MobyPatchPlan = unsafeRadiusPlan }),
    "An unsafe negative-path T21 render-radius value passed the isolated 18 -> 7F gate.");

StoneHillTownSquareEditedDonorArtifactResult renderRadiusArtifact =
    await StoneHillTownSquareEditedDonorArtifactWriter.ExportAsync(renderRadiusRequest);
StoneHillTownSquareEditedDonorCandidateResult renderRadiusResult = renderRadiusArtifact.Candidate;
Require(
    renderRadiusResult.Plan.RenderRadiusPatch == radiusSummary &&
    renderRadiusResult.Plan.PlacementPatch == null &&
    renderRadiusResult.OutputImageSha256 == ExpectedRenderRadiusOutputImageSha256 &&
    renderRadiusResult.ChangedLogicalWadBytes == 2 &&
    renderRadiusResult.ChangedPhysicalImageBytes == ExpectedRenderRadiusChangedPhysicalBytes &&
    renderRadiusResult.RebuiltRawSectorCount == 1 &&
    renderRadiusResult.ExactLogicalDiffBoundaryVerified &&
    renderRadiusResult.ExactPhysicalSectorBoundaryVerified &&
    renderRadiusResult.OriginalTownSquareDonorPreserved &&
    renderRadiusResult.DisplayIdentityExecutablePreserved &&
    renderRadiusResult.BaseImagePreserved &&
    renderRadiusResult.RetailSourcePreserved &&
    renderRadiusResult.BinCuePublishCompleted &&
    !renderRadiusResult.PlacementSectorReadbackVerified &&
    renderRadiusResult.RenderRadiusReadbackVerified &&
    IsSha256(renderRadiusResult.OutputImageSha256),
    "The isolated render-radius candidate omitted an exact diff, readback, donor, SCUS, source, or publication proof.");
byte[] radiusTargetAfter = ReadMode2WadBytes(
    renderRadiusResult.OutputImagePath,
    ExpectedTargetRecordWadOffset,
    MobyLoader.RuntimeRecordStride);
Require(
    radiusTargetAfter[0x4A] == 0xFF &&
    radiusTargetAfter[0x4B] == 0x00 &&
    radiusTargetAfter[0x50] == 0x7F &&
    radiusTargetAfter[0x51] == 0x00 &&
    radiusTargetAfter[0x52] == 0x40 &&
    radiusTargetAfter[0x53] == 0xFF,
    "The render-radius candidate changed a T21 culling/update byte outside checked +0x50 = 7F.");
string radiusChecklist = await File.ReadAllTextAsync(renderRadiusArtifact.RuntimeChecklistPath);
Require(
    radiusChecklist.Contains(renderRadiusResult.OutputImageSha256, StringComparison.Ordinal) &&
    radiusChecklist.Contains("render radius", StringComparison.OrdinalIgnoreCase) &&
    radiusChecklist.Contains("+0x50", StringComparison.OrdinalIgnoreCase) &&
    radiusChecklist.Contains("adjacent native T22", StringComparison.OrdinalIgnoreCase) &&
    radiusChecklist.Contains("collect T21 once", StringComparison.OrdinalIgnoreCase),
    "The isolated render-radius checklist omitted its exact far-distance discriminator.");
string radiusDeterminismPrefix = Path.Combine(outputRoot, "render-radius-determinism-recheck");
try
{
    StoneHillTownSquareEditedDonorCandidateResult radiusDeterministic =
        await StoneHillTownSquareEditedDonorCandidateComposer.ExportAsync(
            renderRadiusRequest with
            {
                OutputImagePath = radiusDeterminismPrefix + ".bin",
                OutputCuePath = radiusDeterminismPrefix + ".cue"
            });
    Require(
        radiusDeterministic.OutputImageSha256 == renderRadiusResult.OutputImageSha256 &&
        radiusDeterministic.OutputImageSha256 == ExpectedRenderRadiusOutputImageSha256 &&
        radiusDeterministic.ChangedLogicalWadBytes == 2 &&
        radiusDeterministic.ChangedPhysicalImageBytes ==
            renderRadiusResult.ChangedPhysicalImageBytes &&
        radiusDeterministic.RenderRadiusReadbackVerified,
        "A second render-radius export was not byte-deterministic.");
}
finally
{
    DeleteIfExists(radiusDeterminismPrefix + ".bin");
    DeleteIfExists(radiusDeterminismPrefix + ".cue");
}

MobySourcePatch updateSchedulePatch = donorPatch with
{
    Label = "townsquare-T21-update-schedule-40-to-ff",
    Kind = "moby-update-schedule",
    RecordOffset = "0x52",
    WadRelativeOffset = $"0x{ExpectedDonorUpdateScheduleWadOffset:X}",
    ImageOffset = "research-rebased-at-compose-time",
    ByteLength = 1,
    BeforeHexPreview = "40",
    AfterHexPreview = "FF",
    Description = "Combined far-flicker diagnostic: keep the maximum safe positive +0x50 render radius and select the queue builder's unconditional scheduling path with signed +0x52 = FF. This is not a loose-gem promotion rule."
};
MobySourcePatchPlan unconditionalUpdatePlan = renderRadiusPlan with
{
    PatchCount = 3,
    TotalPatchedBytes = 6,
    Patches = [donorPatch, renderRadiusPatch, updateSchedulePatch],
    EditOutcomes =
    [
        editorOutcome with
        {
            PatchKinds = ["moby-position-x", "moby-render-radius", "moby-update-schedule"]
        }
    ],
    Notes = renderRadiusPlan.Notes
        .Concat([
            "Combined far-flicker discriminator: retain T21 X move and +0x50 = 7F, then change only native +0x52 from 40 to FF for unconditional update scheduling."
        ])
        .ToArray()
};
string unconditionalUpdatePrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-edited-T21-X-render-radius-7F-update-FF-RUNTIME-CANDIDATE");
StoneHillTownSquareEditedDonorCandidateRequest unconditionalUpdateRequest = request with
{
    MobyPatchPlan = unconditionalUpdatePlan,
    OutputImagePath = unconditionalUpdatePrefix + ".bin",
    OutputCuePath = unconditionalUpdatePrefix + ".cue"
};
StoneHillTownSquareEditedDonorCandidatePlan unconditionalUpdateCandidatePlan =
    await StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(unconditionalUpdateRequest);
StoneHillTownSquareEditedDonorPatchSummary updateScheduleSummary =
    unconditionalUpdateCandidatePlan.UpdateSchedulePatch
    ?? throw new InvalidDataException("The combined diagnostic plan has no update-schedule summary.");
Require(
    unconditionalUpdateCandidatePlan.RecipeId ==
        StoneHillTownSquareEditedDonorCandidateComposer.UnconditionalUpdateRecipeId &&
    unconditionalUpdateCandidatePlan.RecipeVersion ==
        StoneHillTownSquareEditedDonorCandidateComposer.UnconditionalUpdateRecipeVersion &&
    unconditionalUpdateCandidatePlan.EvidenceId ==
        StoneHillTownSquareEditedDonorCandidateComposer.UnconditionalUpdateEvidenceId &&
    unconditionalUpdateCandidatePlan.Safety.Status ==
        StoneHillTownSquareEditedDonorCandidateComposer.UnconditionalUpdateStatus &&
    unconditionalUpdateCandidatePlan.PlacementPatch == null &&
    unconditionalUpdateCandidatePlan.RenderRadiusPatch == radiusSummary &&
    updateScheduleSummary.Kind == "moby-update-schedule" &&
    updateScheduleSummary.TrueIndex == EditedTrueIndex &&
    updateScheduleSummary.DonorWadOffset == ExpectedDonorUpdateScheduleWadOffset &&
    updateScheduleSummary.TargetWadOffset == ExpectedTargetUpdateScheduleWadOffset &&
    updateScheduleSummary.ByteLength == 1 &&
    updateScheduleSummary.BeforeHex == "40" &&
    updateScheduleSummary.AfterHex == "FF",
    "The combined update-scheduling plan changed recipe, relocation, bytes, or evidence boundary.");
MobySourcePatchPlan unsafeUpdateSchedulePlan = unconditionalUpdatePlan with
{
    Patches = [donorPatch, renderRadiusPatch, updateSchedulePatch with { AfterHexPreview = "7F" }]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        unconditionalUpdateRequest with { MobyPatchPlan = unsafeUpdateSchedulePlan }),
    "A non-FF T21 +0x52 value passed the exact unconditional-update gate.");

StoneHillTownSquareEditedDonorArtifactResult unconditionalUpdateArtifact =
    await StoneHillTownSquareEditedDonorArtifactWriter.ExportAsync(unconditionalUpdateRequest);
StoneHillTownSquareEditedDonorCandidateResult unconditionalUpdateResult =
    unconditionalUpdateArtifact.Candidate;
Require(
    unconditionalUpdateResult.Plan.RenderRadiusPatch == radiusSummary &&
    unconditionalUpdateResult.Plan.UpdateSchedulePatch == updateScheduleSummary &&
    unconditionalUpdateResult.Plan.PlacementPatch == null &&
    unconditionalUpdateResult.OutputImageSha256 ==
        ExpectedUnconditionalUpdateOutputImageSha256 &&
    unconditionalUpdateResult.ChangedLogicalWadBytes == 3 &&
    unconditionalUpdateResult.ChangedPhysicalImageBytes ==
        ExpectedUnconditionalUpdateChangedPhysicalBytes &&
    unconditionalUpdateResult.RebuiltRawSectorCount == 1 &&
    unconditionalUpdateResult.ExactLogicalDiffBoundaryVerified &&
    unconditionalUpdateResult.ExactPhysicalSectorBoundaryVerified &&
    unconditionalUpdateResult.OriginalTownSquareDonorPreserved &&
    unconditionalUpdateResult.DisplayIdentityExecutablePreserved &&
    unconditionalUpdateResult.BaseImagePreserved &&
    unconditionalUpdateResult.RetailSourcePreserved &&
    unconditionalUpdateResult.BinCuePublishCompleted &&
    !unconditionalUpdateResult.PlacementSectorReadbackVerified &&
    unconditionalUpdateResult.RenderRadiusReadbackVerified &&
    unconditionalUpdateResult.UpdateScheduleReadbackVerified &&
    IsSha256(unconditionalUpdateResult.OutputImageSha256),
    "The combined unconditional-update candidate omitted an exact diff, readback, donor, SCUS, source, or publication proof.");
byte[] unconditionalUpdateTargetAfter = ReadMode2WadBytes(
    unconditionalUpdateResult.OutputImagePath,
    ExpectedTargetRecordWadOffset,
    MobyLoader.RuntimeRecordStride);
Require(
    unconditionalUpdateTargetAfter[0x4A] == 0xFF &&
    unconditionalUpdateTargetAfter[0x4B] == 0x00 &&
    unconditionalUpdateTargetAfter[0x50] == 0x7F &&
    unconditionalUpdateTargetAfter[0x51] == 0x00 &&
    unconditionalUpdateTargetAfter[0x52] == 0xFF &&
    unconditionalUpdateTargetAfter[0x53] == 0xFF,
    "The combined candidate changed a T21 culling/update byte outside checked +0x50 = 7F and +0x52 = FF.");
string unconditionalUpdateChecklist =
    await File.ReadAllTextAsync(unconditionalUpdateArtifact.RuntimeChecklistPath);
Require(
    unconditionalUpdateChecklist.Contains(unconditionalUpdateResult.OutputImageSha256, StringComparison.Ordinal) &&
    unconditionalUpdateChecklist.Contains("+0x50 = 7F", StringComparison.OrdinalIgnoreCase) &&
    unconditionalUpdateChecklist.Contains("+0x52 = FF", StringComparison.OrdinalIgnoreCase) &&
    unconditionalUpdateChecklist.Contains("unconditional", StringComparison.OrdinalIgnoreCase) &&
    unconditionalUpdateChecklist.Contains("collect T21 once", StringComparison.OrdinalIgnoreCase),
    "The combined candidate checklist omitted its exact far-distance discriminator.");
string unconditionalUpdateDeterminismPrefix =
    Path.Combine(outputRoot, "unconditional-update-determinism-recheck");
try
{
    StoneHillTownSquareEditedDonorCandidateResult unconditionalUpdateDeterministic =
        await StoneHillTownSquareEditedDonorCandidateComposer.ExportAsync(
            unconditionalUpdateRequest with
            {
                OutputImagePath = unconditionalUpdateDeterminismPrefix + ".bin",
                OutputCuePath = unconditionalUpdateDeterminismPrefix + ".cue"
            });
    Require(
        unconditionalUpdateDeterministic.OutputImageSha256 ==
            unconditionalUpdateResult.OutputImageSha256 &&
        unconditionalUpdateDeterministic.OutputImageSha256 ==
            ExpectedUnconditionalUpdateOutputImageSha256 &&
        unconditionalUpdateDeterministic.ChangedLogicalWadBytes == 3 &&
        unconditionalUpdateDeterministic.ChangedPhysicalImageBytes ==
            ExpectedUnconditionalUpdateChangedPhysicalBytes &&
        unconditionalUpdateDeterministic.RenderRadiusReadbackVerified &&
        unconditionalUpdateDeterministic.UpdateScheduleReadbackVerified,
        "A second combined unconditional-update export was not byte-deterministic.");
}
finally
{
    DeleteIfExists(unconditionalUpdateDeterminismPrefix + ".bin");
    DeleteIfExists(unconditionalUpdateDeterminismPrefix + ".cue");
}

Console.WriteLine("PASS: deterministic Town Square T21 X-only edit was rebased while native +0x4A = FF remained unchanged.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"BIN SHA-256: {result.OutputImageSha256}");
Console.WriteLine($"Checklist: {artifact.RuntimeChecklistPath}");
Console.WriteLine($"Static proof: {artifact.StaticProofPath}");
Console.WriteLine($"Native edit: {editPath}");
Console.WriteLine("T21 X: 7813.75 -> 7685.75 (raw 125020 -> 122972); native +0x4A/+0x4B: FF/00 preserved.");
Console.WriteLine("Legacy cache flag4A/flag4B map to native +0x52/+0x53: 40/FF preserved.");
Console.WriteLine($"Donor WAD offset: 0x{ExpectedDonorPatchWadOffset:X}");
Console.WriteLine($"Expected Stone Hill-slot WAD offset: 0x{ExpectedTargetPatchWadOffset:X}");
Console.WriteLine("PASS: isolated T21 render-radius diagnostic was rebased with every adjacent culling/update byte preserved.");
Console.WriteLine($"Render-radius CUE: {renderRadiusResult.OutputCuePath}");
Console.WriteLine($"Render-radius BIN: {renderRadiusResult.OutputImagePath}");
Console.WriteLine($"Render-radius BIN SHA-256: {renderRadiusResult.OutputImageSha256}");
Console.WriteLine($"Render-radius checklist: {renderRadiusArtifact.RuntimeChecklistPath}");
Console.WriteLine($"Render-radius static proof: {renderRadiusArtifact.StaticProofPath}");
Console.WriteLine($"T21 +0x50: 18 -> 7F; donor WAD 0x{ExpectedDonorRenderRadiusWadOffset:X}; target WAD 0x{ExpectedTargetRenderRadiusWadOffset:X}.");
Console.WriteLine("PASS: combined T21 maximum render-radius and unconditional update-scheduling diagnostic preserved every other byte.");
Console.WriteLine($"Unconditional-update CUE: {unconditionalUpdateResult.OutputCuePath}");
Console.WriteLine($"Unconditional-update BIN: {unconditionalUpdateResult.OutputImagePath}");
Console.WriteLine($"Unconditional-update BIN SHA-256: {unconditionalUpdateResult.OutputImageSha256}");
Console.WriteLine($"Unconditional-update changed physical bytes: {unconditionalUpdateResult.ChangedPhysicalImageBytes}");
Console.WriteLine($"Unconditional-update checklist: {unconditionalUpdateArtifact.RuntimeChecklistPath}");
Console.WriteLine($"Unconditional-update static proof: {unconditionalUpdateArtifact.StaticProofPath}");
Console.WriteLine($"T21 +0x52: 40 -> FF; donor WAD 0x{ExpectedDonorUpdateScheduleWadOffset:X}; target WAD 0x{ExpectedTargetUpdateScheduleWadOffset:X}.");

static async Task<(string ImagePath, string CuePath)> EnsureExactIdentityBaseAsync(
    string repositoryRoot,
    string sourceImage,
    string sourceCue,
    LevelCatalog catalog)
{
    string replacementRoot = Path.Combine(
        repositoryRoot,
        "_local",
        "v5-stone-hill-level-replacement",
        "town-square-complete-pair-candidate");
    string replacementPrefix = Path.Combine(
        replacementRoot,
        "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
    string replacementImage = replacementPrefix + ".bin";
    string replacementCue = replacementPrefix + ".cue";
    if (!await HasHashAsync(
            replacementImage,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256) ||
        !File.Exists(replacementCue))
    {
        Directory.CreateDirectory(replacementRoot);
        NativeLevelReplacementManifest manifest = await NativeLevelReplacementStore.StartStoneHillAsync(
            replacementRoot,
            sourceImage,
            catalog);
        NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
            replacementRoot,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
            manifest,
            catalog);
        await StoneHillTownSquareReplacementCandidateComposer.ExportAsync(new(
            manifest,
            intent,
            catalog,
            sourceImage,
            sourceCue,
            replacementImage,
            replacementCue));
    }

    string identityRoot = Path.Combine(
        repositoryRoot,
        "_local",
        "v5-stone-hill-level-replacement",
        "town-square-display-identity-candidate");
    string identityPrefix = Path.Combine(
        identityRoot,
        "Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE");
    string identityImage = identityPrefix + ".bin";
    string identityCue = identityPrefix + ".cue";
    if (!await HasHashAsync(
            identityImage,
            NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256) ||
        !File.Exists(identityCue))
    {
        Directory.CreateDirectory(identityRoot);
        StoneHillTownSquareIdentityCandidateResult result =
            await StoneHillTownSquareIdentityCandidateComposer.ExportAsync(new(
                replacementImage,
                replacementCue,
                identityImage,
                identityCue));
        Require(
            result.OutputImageSha256 ==
                NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256,
            "The regenerated display-identity control did not match its exact runtime-proven hash.");
    }
    return (identityImage, identityCue);
}

static (string ImagePath, string CuePath) ResolveCleanSource(
    string repositoryRoot,
    string[] arguments)
{
    string? suppliedImage = arguments.ElementAtOrDefault(1);
    string? suppliedCue = arguments.ElementAtOrDefault(2);
    if (!string.IsNullOrWhiteSpace(suppliedImage) && !string.IsNullOrWhiteSpace(suppliedCue))
        return (Path.GetFullPath(suppliedImage), Path.GetFullPath(suppliedCue));

    string settingsPath = Path.Combine(repositoryRoot, "_local", "settings", "source-disc.json");
    if (!File.Exists(settingsPath))
        throw new FileNotFoundException(
            "No clean source BIN/CUE arguments or local source-disc settings were available.",
            settingsPath);
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    string imagePath = settings.RootElement.GetProperty("imagePath").GetString() ?? "";
    string cuePath = settings.RootElement.GetProperty("cuePath").GetString() ?? "";
    if (!File.Exists(imagePath) || !File.Exists(cuePath))
        throw new FileNotFoundException("The configured clean USA BIN/CUE is missing.");
    return (Path.GetFullPath(imagePath), Path.GetFullPath(cuePath));
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
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static long ParseHexOffset(string text)
{
    if (string.IsNullOrWhiteSpace(text) ||
        !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
        !long.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out long value))
        throw new InvalidDataException($"Invalid hexadecimal offset '{text}'.");
    return value;
}

static string NormalizeHex(string text) =>
    new((text ?? "").Where(Uri.IsHexDigit).ToArray());

static bool IsSha256(string value) =>
    value.Length == 64 && value.All(Uri.IsHexDigit);

static async Task<bool> HasHashAsync(string path, string expected)
{
    return File.Exists(path) &&
        string.Equals(await HashFileAsync(path), expected, StringComparison.OrdinalIgnoreCase);
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    byte[] hash = await SHA256.HashDataAsync(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static byte[] ReadMode2WadBytes(string imagePath, long wadOffset, int byteLength)
{
    const int WadLba = 37;
    const int RawSectorByteLength = 2352;
    const int UserSectorByteLength = 2048;
    const int UserSectorOffset = 24;
    byte[] result = new byte[byteLength];
    using FileStream stream = File.OpenRead(imagePath);
    int copied = 0;
    while (copied < byteLength)
    {
        long current = wadOffset + copied;
        long sector = WadLba + (current / UserSectorByteLength);
        int sectorOffset = (int)(current % UserSectorByteLength);
        int count = Math.Min(byteLength - copied, UserSectorByteLength - sectorOffset);
        stream.Position = (sector * RawSectorByteLength) + UserSectorOffset + sectorOffset;
        stream.ReadExactly(result.AsSpan(copied, count));
        copied += count;
    }
    return result;
}

static async Task ExpectFailureAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is InvalidOperationException or InvalidDataException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void DeleteIfExists(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
