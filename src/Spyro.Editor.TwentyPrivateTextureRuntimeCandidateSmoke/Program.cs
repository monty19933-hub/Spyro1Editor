using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int RequestedPrivateRows = 20;
const int ExpectedSourceTextureCount = 32;

string workspaceRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string overlayPath = Path.Combine(
    workspaceRoot,
    "editor-cache",
    "gnastysworld-runtime-scene-editor-overlay.json");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "twenty-private-texture-runtime-candidate");
Directory.CreateDirectory(outputRoot);

string outputPrefix = Path.Combine(
    outputRoot,
    "Gnastys-World-20-private-face-swaps-RUNTIME-CANDIDATE-RESEARCH-ONLY");
string editsPath = Path.Combine(
    outputRoot,
    "Gnastys-World-20-private-face-swaps.terrain-edits.json");
string sourceSearchPath = Path.Combine(
    outputRoot,
    "Gnastys-World-retail-source-search.json");
string wadAnalysisPath = Path.Combine(
    outputRoot,
    "source-bound-wad-analysis.json");
string proofPath = outputPrefix + "-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";
string foreignOutputPrefix = Path.Combine(
    outputRoot,
    "Gnastys-World-20-foreign-private-face-swaps-RUNTIME-CANDIDATE-RESEARCH-ONLY");
string foreignProofPath = foreignOutputPrefix + "-proof.json";
string foreignChecklistPath = foreignOutputPrefix + "-runtime-checklist.md";

foreach (string stalePath in new[]
         {
             outputPrefix + ".bin",
             outputPrefix + ".cue",
             foreignOutputPrefix + ".bin",
             foreignOutputPrefix + ".cue",
             editsPath,
             sourceSearchPath,
             proofPath,
             checklistPath,
             foreignProofPath,
             foreignChecklistPath
         })
{
    if (File.Exists(stalePath))
        File.Delete(stalePath);
}

await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
        sourceImagePath,
        gnastysWorld);
Assert(
    binding.ExpectedSourceTextureCount == ExpectedSourceTextureCount,
    $"Gnasty's World retail texture count changed from {ExpectedSourceTextureCount} to {binding.ExpectedSourceTextureCount}.");

GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
(float entryX, float entryY) = LoadEditorEntryXY(
    Path.Combine(workspaceRoot, "editor-cache", "level-entry-poses.json"),
    gnastysWorld.Key);
TerrainSourceSearchResult sourceSearch =
    await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            gnastysWorld,
            geometry));
HashSet<string> exactSourceKeys = sourceSearch.Report.Results
    .Where(result => result.FullSectorHits.Count == 1)
    .Select(result => result.Edit)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

List<TerrainPolygon> selectedFaces = [];
for (int sourceTextureId = 0; sourceTextureId < RequestedPrivateRows; sourceTextureId++)
{
    TerrainPolygon[] candidates = geometry.Polygons
        .Where(face =>
            face.OriginalTextureId == sourceTextureId &&
            face.HasCompleteNativeHighPolyFacePayload &&
            face.SectorOffset >= 0 &&
            face.FaceOffset >= 0 &&
            exactSourceKeys.Contains(face.RuntimeKey))
        .OrderBy(face => PlanarDistanceSquared(face.Center.X, face.Center.Y, entryX, entryY))
        .ThenBy(face => face.SectorIndex)
        .ThenBy(face => face.FaceIndex)
        .ToArray();
    TerrainPolygon selected = candidates.FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"Gnasty's World has no exact source-bound HP face using retail T{sourceTextureId}.");
    selectedFaces.Add(selected);
}
Assert(
    selectedFaces.Count == RequestedPrivateRows &&
    selectedFaces.Select(face => face.RuntimeKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
        RequestedPrivateRows,
    "The runtime candidate did not select twenty unique exact source-bound terrain faces.");

for (int index = 0; index < selectedFaces.Count; index++)
{
    TerrainPolygon face = selectedFaces[index];
    int assignedTextureId = binding.ExpectedSourceTextureCount + index;
    Assert(
        face.OriginalTextureId == index,
        $"Selected face {face.RuntimeKey} does not retain the required T{index} retail material template.");
    face.ApplyTextureOverride(assignedTextureId);
}

int savedEditCount = await TerrainEditStore.SaveAsync(
    editsPath,
    selectedFaces,
    "Gnasty's World twenty exact private-face assignments; RUNTIME-CANDIDATE / RESEARCH-ONLY");
Assert(
    savedEditCount == RequestedPrivateRows,
    $"Terrain edit store saved {savedEditCount}/{RequestedPrivateRows} exact private-face assignments.");

TerrainPatchPlan ordinaryTerrainPlan = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    outputPrefix + "-not-written.bin",
    outputPrefix + "-not-written.cue",
    gnastysWorld,
    ramPath: "",
    sourceSearchPath,
    editsPath);
Assert(
    ordinaryTerrainPlan.NativeTextureRelocationBytePatchCount == 0 &&
    ordinaryTerrainPlan.CustomTextureBytePatchCount == 0,
    "The ordinary face plan unexpectedly selected a second texture-page writer.");
TerrainPatch[] facePatches = ordinaryTerrainPlan.Patches
    .Where(patch => patch.Kind.Equals(
        "texture-id-word3",
        StringComparison.OrdinalIgnoreCase))
    .OrderBy(patch => patch.RuntimeKey, StringComparer.OrdinalIgnoreCase)
    .ToArray();
Assert(
    ordinaryTerrainPlan.Patches.Count == RequestedPrivateRows &&
    facePatches.Length == RequestedPrivateRows &&
    facePatches.Select(patch => patch.RuntimeKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(selectedFaces.Select(face => face.RuntimeKey)),
    "The normal terrain planner did not emit exactly one native texture-id patch for every selected face.");
Assert(
    ordinaryTerrainPlan.SkippedEdits.All(item =>
        item.Equals(
            "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
            StringComparison.Ordinal)),
    $"The ordinary terrain planner reported an unexpected skip: {string.Join(" | ", ordinaryTerrainPlan.SkippedEdits)}");

IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches =
    NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
        ordinaryTerrainPlan);
Assert(
    ordinaryPatches.Count == RequestedPrivateRows &&
    ordinaryPatches.All(patch =>
        patch.Before.Length == sizeof(uint) &&
        patch.After.Length == sizeof(uint) &&
        patch.Kind.Equals("texture-id-word3", StringComparison.OrdinalIgnoreCase)),
    "The compiler did not preserve twenty isolated four-byte face texture-id patches.");

for (int index = 0; index < selectedFaces.Count; index++)
{
    TerrainPolygon face = selectedFaces[index];
    int assignedTextureId = binding.ExpectedSourceTextureCount + index;
    NativeTerrainTextureRecordExistingPatch patch = ordinaryPatches.Single(item =>
        item.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    uint beforeWord = BinaryPrimitives.ReadUInt32LittleEndian(patch.Before);
    uint afterWord = BinaryPrimitives.ReadUInt32LittleEndian(patch.After);
    Assert(
        (beforeWord & 0x7F) == face.OriginalTextureId &&
        (afterWord & 0x7F) == assignedTextureId &&
        (afterWord & 0xFFFFFF80u) == (beforeWord & 0xFFFFFF80u),
        $"Face {face.RuntimeKey} did not change only its seven-bit texture id from T{face.OriginalTextureId} to T{assignedTextureId}.");
}

NativeTerrainTextureRelocationEdit[] privateRows = Enumerable
    .Range(0, RequestedPrivateRows)
    .Select(index => BuildEdit(
        binding,
        gnastysWorld,
        donorTextureId: index,
        materialTemplateTextureId: selectedFaces[index].OriginalTextureId,
        targetTextureId: binding.ExpectedSourceTextureCount + index))
    .ToArray();
NativeTerrainTexturePrivateRecordBatchPlan plan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateRecordBatchRequest(
            sourceImagePath,
            sourceCuePath,
            outputPrefix,
            wadAnalysisPath,
            gnastysWorld,
            privateRows,
            ordinaryPatches));
Assert(
    plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
    plan.FixedTailPlan == null &&
    plan.SectorRelocationPlan != null &&
    plan.AppendedPrivateEdits.Count == RequestedPrivateRows &&
    plan.OrdinaryLevelDataPatches.Count == RequestedPrivateRows,
    "The face-bound twenty-row candidate did not select one exclusive sector-relocation writer.");

NativeTerrainTextureSectorPrivateRecordPlan sectorPlan =
    plan.SectorRelocationPlan!;
Assert(
    sectorPlan.SourceBindingVerified &&
    sectorPlan.GlobalPackingProofComplete &&
    sectorPlan.OriginalRowsInstalledExactly &&
    sectorPlan.SyntheticRowsInstalledExactly &&
    sectorPlan.OrdinaryPatchesIncludedAndRelocated &&
    sectorPlan.TexturePagePatchesIncludedAndRelocated &&
    sectorPlan.StructuralSectorGrowthVerified &&
    sectorPlan.RequiresDuckStationRuntimeProof &&
    sectorPlan.Structural.SectorGrowthBytes == 0x1000 &&
    sectorPlan.Structural.Append.OutputTextureCount ==
        binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    sectorPlan.OrdinaryRelocatedPatchProofs.Count == RequestedPrivateRows,
    "The sector composer omitted a source, packing, row, face, page, growth, or runtime-proof gate.");

bool normalAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows,
        out AppendedPrivateTerrainTexturePromotionProfile? promotionProfile,
        out string normalGateReason);
Assert(
    !normalAuthorized && promotionProfile == null,
    "The twenty-face research candidate was accidentally authorized for normal Create BIN.");

NativeTerrainTexturePrivateRecordBatchExportResult exported =
    await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(plan);
Assert(
    exported.SourceImagePreserved &&
    exported.ExactReadbackVerified &&
    exported.RuntimeTargetReadbackVerified &&
    exported.GlobalLogicalReadbackVerified &&
    exported.AtomicRenameCompleted &&
    File.Exists(exported.OutputImagePath) &&
    File.Exists(exported.OutputCuePath),
    "The face-bound candidate omitted source, structural, face, runtime-target, logical, or atomic-write proof.");

IReadOnlyList<TerrainTextureSlot> slots =
    TerrainPatchExporter.InspectTextureSlots(exported.OutputImagePath, gnastysWorld);
Assert(
    slots.Count == binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    slots.Skip(binding.ExpectedSourceTextureCount)
        .Select(slot => slot.TextureId)
        .SequenceEqual(Enumerable.Range(binding.ExpectedSourceTextureCount, RequestedPrivateRows)) &&
    slots.Skip(binding.ExpectedSourceTextureCount)
        .All(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors),
    "The final BIN did not decode twenty complete contiguous private texture rows.");

List<object> finalFaces = [];
foreach (TerrainPolygon face in selectedFaces)
{
    int assignedTextureId = binding.ExpectedSourceTextureCount + face.OriginalTextureId;
    NativeTerrainTextureRecordExistingPatch ordinary = ordinaryPatches.Single(item =>
        item.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    NativeTerrainTextureRecordRebasedPatchProof proof =
        sectorPlan.OrdinaryRelocatedPatchProofs.Single(item =>
            item.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    byte[] finalBytes = ReadWadBytes(
        exported.OutputImagePath,
        proof.OutputWadOffset,
        sizeof(uint));
    uint finalWord = BinaryPrimitives.ReadUInt32LittleEndian(finalBytes);
    uint expectedWord = BinaryPrimitives.ReadUInt32LittleEndian(ordinary.After);
    Assert(
        finalWord == expectedWord &&
        (finalWord & 0x7F) == assignedTextureId &&
        ReadWadBytes(sourceImagePath, ordinary.WadOffset, ordinary.Before.Length)
            .SequenceEqual(ordinary.Before),
        $"Final face {face.RuntimeKey} did not read back as exact private T{assignedTextureId}, or its retail preimage changed.");
    finalFaces.Add(new
    {
        face.RuntimeKey,
        RetailTextureId = face.OriginalTextureId,
        AssignedTextureId = assignedTextureId,
        CenterX = face.Center.X,
        CenterY = face.Center.Y,
        AverageZ = face.AvgZ,
        PlanarDistanceFromEntry = MathF.Sqrt(
            PlanarDistanceSquared(
                face.Center.X,
                face.Center.Y,
                entryX,
                entryY)),
        RetailWadOffset = $"0x{proof.SourceWadOffset:X}",
        RelocatedWadOffset = $"0x{proof.OutputWadOffset:X}",
        RetailWord = $"0x{BinaryPrimitives.ReadUInt32LittleEndian(ordinary.Before):X8}",
        FinalWord = $"0x{finalWord:X8}"
    });
}

NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows =
    sectorPlan.GlobalPacking.OriginalMovableRecords
        .Concat(sectorPlan.GlobalPacking.SyntheticRecords)
        .OrderBy(row => row.TargetTextureId)
        .ToArray();
bool explicitLogicalReadback =
    NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        exported.OutputImagePath,
        gnastysWorld,
        expectedRows,
        sourceImagePath,
        out string logicalFailure);
Assert(
    explicitLogicalReadback &&
    sectorPlan.GlobalPacking.SyntheticRecords.Count == RequestedPrivateRows,
    $"Explicit immutable-source logical readback failed: {logicalFailure}");
Assert(
    string.Equals(
        Sha256File(sourceImagePath),
        sourceSha256,
        StringComparison.OrdinalIgnoreCase) &&
    new FileInfo(sourceImagePath).Length == sourceLength,
    "The runtime-candidate smoke modified its immutable retail source.");

await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status = "RUNTIME-CANDIDATE / RESEARCH-ONLY / DuckStation proof pending",
            NormalEditorPromotion = false,
            Target = new
            {
                gnastysWorld.Key,
                gnastysWorld.DisplayName,
                gnastysWorld.SourceWadEntry,
                EntryEditorX = entryX,
                EntryEditorY = entryY
            },
            Source = new
            {
                Path = sourceImagePath,
                Sha256 = sourceSha256,
                binding.ExpectedSourceTextureCount,
                binding.ExpectedTextureComponentSha256,
                binding.ExpectedLevelDataSha256,
                Preserved = true
            },
            Output = new
            {
                Bin = exported.OutputImagePath,
                Cue = exported.OutputCuePath,
                exported.OutputImageSha256,
                FinalTextureCount = slots.Count
            },
            Structure = new
            {
                Writer = plan.WriterKind.ToString(),
                sectorPlan.Structural.SectorGrowthBytes,
                sectorPlan.Structural.OriginalExecutableLba,
                sectorPlan.Structural.RelocatedExecutableLba,
                sectorPlan.GlobalPacking.PackingProof.ProtectedByteCount,
                sectorPlan.GlobalPacking.PackingProof.AllocatedByteCount,
                sectorPlan.GlobalPacking.PackingProof.FreeByteCount
            },
            PrivateRows = privateRows.Select(edit => new
            {
                edit.TargetTextureId,
                edit.DonorTextureId,
                edit.MaterialTemplateTextureId,
                edit.PrivateRecordEditId
            }),
            Faces = finalFaces,
            Proof = new
            {
                exported.SourceImagePreserved,
                exported.ExactReadbackVerified,
                exported.RuntimeTargetReadbackVerified,
                exported.GlobalLogicalReadbackVerified,
                ExplicitImmutableSourceLogicalReadback = explicitLogicalReadback,
                exported.AtomicRenameCompleted,
                NormalReleaseAuthorized = normalAuthorized,
                NormalGateReason = normalGateReason,
                TerrainFaceAssignmentCount = finalFaces.Count,
                DuckStationRuntimeProven = false
            }
        },
        new JsonSerializerOptions { WriteIndented = true }));

await File.WriteAllTextAsync(
    checklistPath,
    $$"""
      # Gnasty's World 20-private-face runtime candidate

      **RUNTIME-CANDIDATE / RESEARCH-ONLY.** This build is not promoted in the
      normal editor. It clones retail Gnasty's World T0..T19 into private
      T32..T51 and points one exact native HP face at each new row.

      1. Cold boot `{{exported.OutputCuePath}}` in DuckStation.
      2. Enter Gnasty's World.
      3. Traverse the full homeworld and rotate the camera around terrain,
         portals, water, distant geometry, and the balloonist.
      4. Confirm there is no black screen, GTE assertion, texture corruption,
         missing/culling terrain, flicker, or nearby actor regression.
      5. Leave and re-enter Gnasty's World and repeat the visual sweep.

      The cloned private rows intentionally preserve the same retail appearance
      as their source T0..T19 rows. The visible success condition is therefore
      unchanged correct terrain with no corruption; the proof JSON records the
      exact twenty final native face IDs.
      """);

// A separate real cross-level stress candidate uses the same twenty exact
// retail face patches and target material templates, but fills every private
// row from a different foreign level. The normal-preview evidence is only a
// visual-distinctness guard; the native compiler independently validates and
// repacks each complete retail donor record.
(string LevelKey, int TextureId)[] foreignDonorSpecs =
[
    ("artisans", 19),
    ("stonehill", 38),
    ("darkhollow", 14),
    ("townsquare", 2),
    ("toasty", 69),
    ("peacekeepers", 29),
    ("drycanyon", 33),
    ("clifftown", 22),
    ("icecavern", 17),
    ("doctorshemp", 16),
    ("magiccrafters", 55),
    ("alpineridge", 10),
    ("highcaves", 78),
    ("wizardpeak", 1),
    ("blowhard", 14),
    ("beastmakers", 3),
    ("terracevillage", 4),
    ("mistybog", 3),
    ("treetops", 67),
    ("metalhead", 47)
];
Assert(
    foreignDonorSpecs.Length == RequestedPrivateRows &&
    foreignDonorSpecs.Select(spec => spec.LevelKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == RequestedPrivateRows &&
    foreignDonorSpecs.All(spec =>
        !spec.LevelKey.Equals(gnastysWorld.Key, StringComparison.OrdinalIgnoreCase)),
    "The foreign stress candidate requires twenty different non-destination donor levels.");

ForeignDonorPreviewEvidence[] foreignDonorEvidence = foreignDonorSpecs
    .Select(spec => InspectForeignDonorPreview(
        workspaceRoot,
        catalog,
        spec.LevelKey,
        spec.TextureId))
    .ToArray();
Assert(
    foreignDonorEvidence.Select(item => item.NormalizedRgbaSha256)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == RequestedPrivateRows,
    "The selected foreign donor previews are not twenty visually distinct retail textures.");

NativeTerrainTextureRelocationEdit[] foreignRows = foreignDonorEvidence
    .Select((evidence, index) => BuildEdit(
        binding,
        evidence.Level,
        donorTextureId: evidence.TextureId,
        materialTemplateTextureId: selectedFaces[index].OriginalTextureId,
        targetTextureId: binding.ExpectedSourceTextureCount + index))
    .ToArray();
NativeTerrainTexturePrivateRecordBatchPlan foreignPlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateRecordBatchRequest(
            sourceImagePath,
            sourceCuePath,
            foreignOutputPrefix,
            wadAnalysisPath,
            gnastysWorld,
            foreignRows,
            ordinaryPatches));
Assert(
    foreignPlan.WriterKind ==
        NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
    foreignPlan.FixedTailPlan == null &&
    foreignPlan.SectorRelocationPlan != null &&
    foreignPlan.AppendedPrivateEdits.Count == RequestedPrivateRows &&
    foreignPlan.OrdinaryLevelDataPatches.Count == RequestedPrivateRows,
    "The foreign twenty-row candidate did not retain one exclusive sector-relocation writer.");
NativeTerrainTextureSectorPrivateRecordPlan foreignSectorPlan =
    foreignPlan.SectorRelocationPlan!;
Assert(
    foreignSectorPlan.SourceBindingVerified &&
    foreignSectorPlan.GlobalPackingProofComplete &&
    foreignSectorPlan.OriginalRowsInstalledExactly &&
    foreignSectorPlan.SyntheticRowsInstalledExactly &&
    foreignSectorPlan.OrdinaryPatchesIncludedAndRelocated &&
    foreignSectorPlan.TexturePagePatchesIncludedAndRelocated &&
    foreignSectorPlan.StructuralSectorGrowthVerified &&
    foreignSectorPlan.RequiresDuckStationRuntimeProof &&
    foreignSectorPlan.Structural.SectorGrowthBytes == 0x1000 &&
    foreignSectorPlan.Structural.Append.OutputTextureCount ==
        binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    foreignSectorPlan.OrdinaryRelocatedPatchProofs.Count ==
        RequestedPrivateRows,
    "The foreign sector composer omitted a source, packing, row, face, page, growth, or runtime-proof gate.");

bool foreignNormalAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        foreignPlan.WriterKind,
        RequestedPrivateRows,
        out AppendedPrivateTerrainTexturePromotionProfile? foreignPromotionProfile,
        out string foreignNormalGateReason);
Assert(
    !foreignNormalAuthorized && foreignPromotionProfile == null,
    "The twenty-foreign-row research candidate was accidentally authorized for normal Create BIN.");

NativeTerrainTexturePrivateRecordBatchExportResult foreignExported =
    await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(foreignPlan);
Assert(
    foreignExported.SourceImagePreserved &&
    foreignExported.ExactReadbackVerified &&
    foreignExported.RuntimeTargetReadbackVerified &&
    foreignExported.GlobalLogicalReadbackVerified &&
    foreignExported.AtomicRenameCompleted &&
    File.Exists(foreignExported.OutputImagePath) &&
    File.Exists(foreignExported.OutputCuePath),
    "The foreign candidate omitted source, structural, face, runtime-target, logical, or atomic-write proof.");

IReadOnlyList<TerrainTextureSlot> foreignSlots =
    TerrainPatchExporter.InspectTextureSlots(
        foreignExported.OutputImagePath,
        gnastysWorld);
Assert(
    foreignSlots.Count ==
        binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    foreignSlots.Skip(binding.ExpectedSourceTextureCount)
        .Select(slot => slot.TextureId)
        .SequenceEqual(Enumerable.Range(
            binding.ExpectedSourceTextureCount,
            RequestedPrivateRows)) &&
    foreignSlots.Skip(binding.ExpectedSourceTextureCount)
        .All(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors),
    "The foreign final BIN did not decode twenty complete contiguous private texture rows.");

List<object> foreignFinalFaces = [];
foreach (TerrainPolygon face in selectedFaces)
{
    int assignedTextureId =
        binding.ExpectedSourceTextureCount + face.OriginalTextureId;
    NativeTerrainTextureRecordExistingPatch ordinary = ordinaryPatches.Single(
        item => item.RuntimeKey.Equals(
            face.RuntimeKey,
            StringComparison.OrdinalIgnoreCase));
    NativeTerrainTextureRecordRebasedPatchProof proof =
        foreignSectorPlan.OrdinaryRelocatedPatchProofs.Single(item =>
            item.RuntimeKey.Equals(
                face.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    byte[] finalBytes = ReadWadBytes(
        foreignExported.OutputImagePath,
        proof.OutputWadOffset,
        sizeof(uint));
    uint finalWord = BinaryPrimitives.ReadUInt32LittleEndian(finalBytes);
    uint expectedWord = BinaryPrimitives.ReadUInt32LittleEndian(ordinary.After);
    Assert(
        finalWord == expectedWord &&
        (finalWord & 0x7F) == assignedTextureId &&
        ReadWadBytes(
                sourceImagePath,
                ordinary.WadOffset,
                ordinary.Before.Length)
            .SequenceEqual(ordinary.Before),
        $"Foreign final face {face.RuntimeKey} did not read back as exact private T{assignedTextureId}, or its retail preimage changed.");
    ForeignDonorPreviewEvidence donor =
        foreignDonorEvidence[face.OriginalTextureId];
    foreignFinalFaces.Add(new
    {
        face.RuntimeKey,
        RetailTextureId = face.OriginalTextureId,
        AssignedTextureId = assignedTextureId,
        CenterX = face.Center.X,
        CenterY = face.Center.Y,
        AverageZ = face.AvgZ,
        PlanarDistanceFromEntry = MathF.Sqrt(
            PlanarDistanceSquared(
                face.Center.X,
                face.Center.Y,
                entryX,
                entryY)),
        DonorLevelKey = donor.Level.Key,
        DonorLevelName = donor.Level.DisplayName,
        DonorTextureId = donor.TextureId,
        donor.PreviewSha256,
        donor.NormalizedRgbaSha256,
        RetailWadOffset = $"0x{proof.SourceWadOffset:X}",
        RelocatedWadOffset = $"0x{proof.OutputWadOffset:X}",
        RetailWord =
            $"0x{BinaryPrimitives.ReadUInt32LittleEndian(ordinary.Before):X8}",
        FinalWord = $"0x{finalWord:X8}"
    });
}

NativeTerrainTextureGlobalRepackPackedRecord[] foreignExpectedRows =
    foreignSectorPlan.GlobalPacking.OriginalMovableRecords
        .Concat(foreignSectorPlan.GlobalPacking.SyntheticRecords)
        .OrderBy(row => row.TargetTextureId)
        .ToArray();
bool foreignExplicitLogicalReadback =
    NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        foreignExported.OutputImagePath,
        gnastysWorld,
        foreignExpectedRows,
        sourceImagePath,
        out string foreignLogicalFailure);
Assert(
    foreignExplicitLogicalReadback &&
    foreignSectorPlan.GlobalPacking.SyntheticRecords.Count ==
        RequestedPrivateRows,
    $"Foreign immutable-source logical readback failed: {foreignLogicalFailure}");
Assert(
    string.Equals(
        Sha256File(sourceImagePath),
        sourceSha256,
        StringComparison.OrdinalIgnoreCase) &&
    new FileInfo(sourceImagePath).Length == sourceLength,
    "The foreign runtime-candidate smoke modified its immutable retail source.");

await File.WriteAllTextAsync(
    foreignProofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status =
                "RUNTIME-CANDIDATE / RESEARCH-ONLY / DuckStation proof pending",
            Scope =
                "Twenty exact Gnasty's World HP faces, twenty appended private records, twenty distinct static face-referenced foreign retail donors from twenty levels.",
            NormalEditorPromotion = false,
            Target = new
            {
                gnastysWorld.Key,
                gnastysWorld.DisplayName,
                gnastysWorld.SourceWadEntry,
                EntryEditorX = entryX,
                EntryEditorY = entryY
            },
            Source = new
            {
                Path = sourceImagePath,
                Sha256 = sourceSha256,
                binding.ExpectedSourceTextureCount,
                binding.ExpectedTextureComponentSha256,
                binding.ExpectedLevelDataSha256,
                Preserved = true
            },
            Output = new
            {
                Bin = foreignExported.OutputImagePath,
                Cue = foreignExported.OutputCuePath,
                foreignExported.OutputImageSha256,
                FinalTextureCount = foreignSlots.Count
            },
            Structure = new
            {
                Writer = foreignPlan.WriterKind.ToString(),
                foreignSectorPlan.Structural.SectorGrowthBytes,
                foreignSectorPlan.Structural.OriginalExecutableLba,
                foreignSectorPlan.Structural.RelocatedExecutableLba,
                foreignSectorPlan.GlobalPacking.PackingProof.ProtectedByteCount,
                foreignSectorPlan.GlobalPacking.PackingProof.AllocatedByteCount,
                foreignSectorPlan.GlobalPacking.PackingProof.FreeByteCount,
                foreignSectorPlan.GlobalPacking.PackingProof.PackingStrategy
            },
            Donors = foreignDonorEvidence.Select((donor, index) => new
            {
                AssignedTextureId =
                    binding.ExpectedSourceTextureCount + index,
                donor.Level.Key,
                donor.Level.DisplayName,
                donor.Level.SourceWadEntry,
                donor.TextureId,
                donor.PreviewFile,
                donor.PreviewSha256,
                donor.NormalizedRgbaSha256,
                MaterialTemplateTextureId =
                    selectedFaces[index].OriginalTextureId
            }),
            Faces = foreignFinalFaces,
            Proof = new
            {
                foreignExported.SourceImagePreserved,
                foreignExported.ExactReadbackVerified,
                foreignExported.RuntimeTargetReadbackVerified,
                foreignExported.GlobalLogicalReadbackVerified,
                ExplicitImmutableSourceLogicalReadback =
                    foreignExplicitLogicalReadback,
                foreignExported.AtomicRenameCompleted,
                DistinctForeignLevelCount = foreignDonorEvidence
                    .Select(item => item.Level.Key)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                DistinctNormalizedRgbaPreviewCount = foreignDonorEvidence
                    .Select(item => item.NormalizedRgbaSha256)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                TerrainFaceAssignmentCount = foreignFinalFaces.Count,
                NormalReleaseAuthorized = foreignNormalAuthorized,
                NormalGateReason = foreignNormalGateReason,
                DuckStationRuntimeProven = false
            }
        },
        new JsonSerializerOptions { WriteIndented = true }));

await File.WriteAllTextAsync(
    foreignChecklistPath,
    $$"""
      # Gnasty's World 20-foreign-private-face stress candidate

      **RUNTIME-CANDIDATE / RESEARCH-ONLY.** This build is not promoted in the
      normal editor. Twenty exact native HP faces now reference T32..T51. Each
      private row imports a visually distinct, static, face-referenced retail
      texture from a different foreign level while preserving the face's
      original Gnasty's World material template.

      1. Cold boot `{{foreignExported.OutputCuePath}}` in DuckStation.
      2. Enter Gnasty's World.
      3. Traverse the full homeworld and rotate the camera around terrain,
         portals, water, distant geometry, and the balloonist.
      4. Confirm the changed faces render stable foreign art without a black
         screen, GTE assertion, texture corruption, culling holes, flicker, or
         nearby actor regression.
      5. Leave and re-enter Gnasty's World and repeat the visual sweep.

      The exact face-to-donor map and final native words are in
      `{{foreignProofPath}}`.
      """);

Console.WriteLine("Gnasty's World twenty private face runtime-candidate smoke: PASSED");
Console.WriteLine(
    $"- Exact faces: {selectedFaces.Count}; private ids T{binding.ExpectedSourceTextureCount}..T{binding.ExpectedSourceTextureCount + RequestedPrivateRows - 1}");
Console.WriteLine(
    $"- WAD growth: +0x{sectorPlan.Structural.SectorGrowthBytes:X}; executable LBA {sectorPlan.Structural.OriginalExecutableLba}->{sectorPlan.Structural.RelocatedExecutableLba}");
Console.WriteLine($"- TEST CUE: {exported.OutputCuePath}");
Console.WriteLine($"- Proof: {proofPath}");
Console.WriteLine($"- Checklist: {checklistPath}");
Console.WriteLine("- Normal editor authorization remains unchanged; DuckStation proof is pending.");
Console.WriteLine("Gnasty's World twenty FOREIGN private face runtime-candidate smoke: PASSED");
Console.WriteLine(
    $"- Distinct foreign levels/previews: {foreignDonorEvidence.Length}/{foreignDonorEvidence.Select(item => item.PreviewSha256).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
Console.WriteLine(
    $"- Remaining native texture-page bytes: {foreignSectorPlan.GlobalPacking.PackingProof.FreeByteCount:N0}");
Console.WriteLine($"- FOREIGN TEST CUE: {foreignExported.OutputCuePath}");
Console.WriteLine($"- Foreign proof: {foreignProofPath}");
Console.WriteLine($"- Foreign checklist: {foreignChecklistPath}");
Console.WriteLine("- Normal editor authorization remains unchanged; DuckStation proof is pending.");

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

static byte[] ReadWadBytes(string imagePath, long wadOffset, int length)
{
    (int sectorSize, int userOffset) = DetectLayout(imagePath);
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long currentWadOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = checked((int)(currentWadOffset % 2048));
        int sector = checked(37 + (int)(currentWadOffset / 2048));
        int count = Math.Min(length - written, 2048 - sectorOffset);
        stream.Position = ((long)sector * sectorSize) + userOffset + sectorOffset;
        stream.ReadExactly(result.AsSpan(written, count));
        written += count;
        currentWadOffset += count;
    }
    return result;
}

static (int SectorSize, int UserOffset) DetectLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    Span<byte> signature = stackalloc byte[6];
    foreach ((int sectorSize, int userOffset) in new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (offset + signature.Length > stream.Length)
            continue;
        stream.Position = offset;
        stream.ReadExactly(signature);
        if (signature[0] == 1 &&
            Encoding.ASCII.GetString(signature[1..]) == "CD001")
        {
            return (sectorSize, userOffset);
        }
    }
    throw new InvalidDataException(
        "Could not detect the runtime candidate image's ISO9660 sector layout.");
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static string Sha256NormalizedRgba(string path)
{
    Rgba32[] pixels = PngRgbaImage.ReadRgba(
        path,
        out int width,
        out int height);
    byte[] normalized = new byte[checked(8 + (pixels.Length * 4))];
    BinaryPrimitives.WriteInt32LittleEndian(normalized.AsSpan(0, 4), width);
    BinaryPrimitives.WriteInt32LittleEndian(normalized.AsSpan(4, 4), height);
    int offset = 8;
    foreach (Rgba32 pixel in pixels)
    {
        normalized[offset++] = pixel.R;
        normalized[offset++] = pixel.G;
        normalized[offset++] = pixel.B;
        normalized[offset++] = pixel.A;
    }
    return Convert.ToHexString(SHA256.HashData(normalized));
}

static ForeignDonorPreviewEvidence InspectForeignDonorPreview(
    string workspaceRoot,
    LevelCatalog catalog,
    string levelKey,
    int textureId)
{
    LevelDefinition level = catalog.FindByKey(levelKey)
        ?? throw new InvalidOperationException(
            $"Foreign donor level '{levelKey}' is missing from the level catalog.");
    string manifestPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        "terrain-textures",
        level.Key,
        "manifest.json");
    using JsonDocument document = JsonDocument.Parse(
        File.ReadAllText(manifestPath));
    JsonElement[] matches = document.RootElement
        .GetProperty("textures")
        .EnumerateArray()
        .Where(item =>
            item.GetProperty("textureId").GetInt32() == textureId &&
            item.GetProperty("previewTier").GetString() == "normal")
        .ToArray();
    Assert(
        matches.Length == 1,
        $"{level.DisplayName} T{textureId} has {matches.Length} normal preview records, expected one.");
    JsonElement match = matches[0];
    Assert(
        !match.GetProperty("runtimeControlled").GetBoolean() &&
        match.GetProperty("faceReferenced").GetBoolean(),
        $"{level.DisplayName} T{textureId} is not a static face-referenced retail donor.");
    string file = match.GetProperty("file").GetString() ?? "";
    string previewSha256 =
        match.GetProperty("fileSha256").GetString() ?? "";
    string previewPath = Path.Combine(
        Path.GetDirectoryName(manifestPath) ?? "",
        file);
    Assert(
        File.Exists(previewPath) &&
        Sha256File(previewPath).Equals(
            previewSha256,
            StringComparison.OrdinalIgnoreCase),
        $"{level.DisplayName} T{textureId} preview bytes do not match the portable-cache manifest.");
    return new ForeignDonorPreviewEvidence(
        level,
        textureId,
        previewPath,
        previewSha256,
        Sha256NormalizedRgba(previewPath));
}

static (float X, float Y) LoadEditorEntryXY(
    string entryPosePath,
    string levelKey)
{
    using JsonDocument document = JsonDocument.Parse(
        File.ReadAllText(entryPosePath));
    JsonElement[] matches = document.RootElement
        .GetProperty("levels")
        .EnumerateArray()
        .Where(item => string.Equals(
            item.GetProperty("levelKey").GetString(),
            levelKey,
            StringComparison.OrdinalIgnoreCase))
        .ToArray();
    Assert(
        matches.Length == 1,
        $"Entry-pose cache has {matches.Length} rows for '{levelKey}', expected one.");
    const float NativeToEditorScale = 1f / 16f;
    return (
        matches[0].GetProperty("rawX").GetInt32() * NativeToEditorScale,
        matches[0].GetProperty("rawY").GetInt32() * NativeToEditorScale);
}

static float PlanarDistanceSquared(
    float x,
    float y,
    float targetX,
    float targetY)
{
    float dx = x - targetX;
    float dy = y - targetY;
    return (dx * dx) + (dy * dy);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record ForeignDonorPreviewEvidence(
    LevelDefinition Level,
    int TextureId,
    string PreviewFile,
    string PreviewSha256,
    string NormalizedRgbaSha256);
