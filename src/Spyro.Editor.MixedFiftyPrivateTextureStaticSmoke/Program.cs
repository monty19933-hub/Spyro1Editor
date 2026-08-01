using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int FaceUnreferencedAnimationSourceRows = 4;
const int AppendedPrivateRows = 46;
const int TotalResearchTargets =
    FaceUnreferencedAnimationSourceRows + AppendedPrivateRows;
const int ExpectedSectorGrowth = 0x2000;

bool exportFinalBins = args.Any(argument =>
    argument.Equals("--export", StringComparison.OrdinalIgnoreCase));
bool visibleRuntimeCandidate = args.Any(argument =>
    argument.Equals(
        "--visible-runtime-candidate",
        StringComparison.OrdinalIgnoreCase));
string? rootArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
string workspaceRoot = ResolveWorkspaceRoot(rootArgument);
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "mixed-fifty-private-texture-static");
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
string proofPath = Path.Combine(outputRoot, "mixed-fifty-private-texture-proof.json");
if (visibleRuntimeCandidate)
{
    proofPath = Path.Combine(
        outputRoot,
        "mixed-fifty-private-texture-visible-runtime-candidate-proof.json");
}

Directory.CreateDirectory(outputRoot);
Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
Assert(File.Exists(sourceCuePath), $"Missing retail source CUE: {sourceCuePath}");
await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

string sourcePhysicalPath = ResolvePhysicalPath(sourceImagePath);
string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePhysicalPath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
TargetSpec[] targetSpecs =
[
    new(
        "wizardpeak",
        "wizardpeak-runtime-scene-editor-overlay.json",
        ExpectedSourceTextureCount: 82,
        ExpectedFaceUnreferencedAnimationSourceIds: [73, 75, 79, 81]),
    new(
        "treetops",
        "treetops-runtime-scene-editor-overlay.json",
        ExpectedSourceTextureCount: 81,
        ExpectedFaceUnreferencedAnimationSourceIds: [3, 5, 9, 11])
];

List<TargetProof> targetProofs = [];
foreach (TargetSpec spec in targetSpecs)
{
    LevelDefinition level = catalog.FindByKey(spec.LevelKey)
        ?? throw new InvalidOperationException(
            $"Level '{spec.LevelKey}' is missing from the level catalog.");
    string overlayPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        spec.OverlayName);
    Assert(File.Exists(overlayPath), $"Missing geometry overlay: {overlayPath}");

    NativeTerrainTextureRecordAppendSourceBinding binding =
        NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
            sourceImagePath,
            level);
    Assert(
        binding.ExpectedSourceTextureCount == spec.ExpectedSourceTextureCount,
        $"{level.DisplayName} retail texture count changed from " +
        $"{spec.ExpectedSourceTextureCount} to {binding.ExpectedSourceTextureCount}.");
    Assert(
        binding.ExpectedSourceTextureCount + AppendedPrivateRows <=
            NativeTerrainTextureRecordAppendBuilder.MaximumTextureRecordCount,
        $"{level.DisplayName} cannot address {AppendedPrivateRows} appended rows in the native seven-bit field.");

    GeometryCandidate geometry =
        GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    HashSet<int> referencedIds = geometry.Polygons
        .Select(face => face.OriginalTextureId)
        .Where(textureId =>
            textureId >= 0 &&
            textureId < binding.ExpectedSourceTextureCount)
        .ToHashSet();
    int[] unreferencedIds = Enumerable.Range(
            0,
            binding.ExpectedSourceTextureCount)
        .Where(textureId => !referencedIds.Contains(textureId))
        .ToArray();
    Assert(
        unreferencedIds.SequenceEqual(
            spec.ExpectedFaceUnreferencedAnimationSourceIds),
        $"{level.DisplayName} face-unreferenced animation-source rows changed; expected " +
        $"{FormatIds(spec.ExpectedFaceUnreferencedAnimationSourceIds)}, found {FormatIds(unreferencedIds)}.");

    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(
            sourceImagePath,
            level);
    Assert(
        runtime.Complete,
        $"{level.DisplayName} runtime-control audit is incomplete: " +
        $"{string.Join(" | ", runtime.SafetyBlockers)}");
    Assert(
        unreferencedIds.All(runtime.IsRuntimePersistentTarget),
        $"{level.DisplayName} has an unreferenced row controlled by native runtime animation or scrolling.");
    Assert(
        unreferencedIds.All(runtime.AnimationSourceTextureIds.Contains),
        $"{level.DisplayName} face-unreferenced rows are no longer the expected animation-source diagnostic rows.");

    NativeTerrainTextureRecordAppendCapacity normalCapacity =
        NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
            sourceImagePath,
            level,
            AppendedPrivateRows,
            wadAnalysisPath);
    NativeTerrainTextureRecordAppendCapacity staticCapacity =
        NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
            sourceImagePath,
            level,
            AppendedPrivateRows,
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
            wadAnalysisPath);
    Assert(
        !normalCapacity.HasExecutableWriter &&
        normalCapacity.RequiredSectorAlignedEntryGrowthBytes ==
            ExpectedSectorGrowth,
        $"{level.DisplayName} unexpectedly authorized its 46-row append under the normal structural ceiling.");
    Assert(
        staticCapacity.HasExecutableWriter &&
        staticCapacity.SectorRelocationPlanVerified &&
        staticCapacity.RequiredSectorAlignedEntryGrowthBytes ==
            ExpectedSectorGrowth &&
        staticCapacity.StaticResearchOnly,
        $"{level.DisplayName} did not prove exact +0x{ExpectedSectorGrowth:X} static-research growth.");

    int[] stableDonors = Enumerable.Range(
            0,
            binding.ExpectedSourceTextureCount)
        .Where(runtime.IsRuntimePersistentTarget)
        .Where(textureId =>
            !runtime.AnimationSourceTextureIds.Contains(textureId))
        .Except(unreferencedIds)
        .Take(FaceUnreferencedAnimationSourceRows)
        .ToArray();
    Assert(
        stableDonors.Length == FaceUnreferencedAnimationSourceRows,
        $"{level.DisplayName} does not expose four stable referenced donor rows.");

    string outputPrefix = Path.Combine(
        outputRoot,
        visibleRuntimeCandidate
            ? $"{level.Key}-4-native-plus-46-appended-50-visible-faces-RUNTIME-CANDIDATE"
            : $"{level.Key}-4-native-plus-46-appended-STATIC-ONLY");
    string editsPath = outputPrefix + ".terrain-edits.json";
    string sourceSearchPath = outputPrefix + "-source-search.json";
    foreach (string outputPath in new[]
             {
                 outputPrefix + ".bin",
                 outputPrefix + ".cue",
                 editsPath,
                 sourceSearchPath
             })
    {
        if (exportFinalBins && File.Exists(outputPath))
            File.Delete(outputPath);
    }

    int[] appendedMaterialTemplates =
        Enumerable.Range(0, AppendedPrivateRows).ToArray();
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches = [];
    int faceAssignmentCount = 0;
    if (visibleRuntimeCandidate)
    {
        TerrainSourceSearchResult sourceSearch =
            await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
                new SourceDerivedTerrainSourceSearchRequest(
                    sourceImagePath,
                    sourceSearchPath,
                    level,
                    geometry));
        HashSet<string> exactSourceKeys = sourceSearch.Report.Results
            .Where(result => result.FullSectorHits.Count == 1)
            .Select(result => result.Edit)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        TerrainPolygon[] exactFaces = geometry.Polygons
            .Where(face =>
                face.OriginalTextureId >= 0 &&
                face.OriginalTextureId < binding.ExpectedSourceTextureCount &&
                face.HasCompleteNativeHighPolyFacePayload &&
                face.SectorOffset >= 0 &&
                face.FaceOffset >= 0 &&
                exactSourceKeys.Contains(face.RuntimeKey))
            .OrderBy(face => face.OriginalTextureId)
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .ToArray();
        TerrainPolygon[] appendedFaces = exactFaces
            .GroupBy(face => face.OriginalTextureId)
            .OrderBy(group => group.Key)
            .Select(group => group.First())
            .Take(AppendedPrivateRows)
            .ToArray();
        HashSet<string> appendedFaceKeys = appendedFaces
            .Select(face => face.RuntimeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        TerrainPolygon[] existingFaces = exactFaces
            .Where(face => !appendedFaceKeys.Contains(face.RuntimeKey))
            .Take(FaceUnreferencedAnimationSourceRows)
            .ToArray();
        Assert(
            appendedFaces.Length == AppendedPrivateRows &&
            appendedFaces.Select(face => face.OriginalTextureId).Distinct().Count() ==
                AppendedPrivateRows &&
            existingFaces.Length == FaceUnreferencedAnimationSourceRows,
            $"{level.DisplayName} does not expose 46 distinct-material plus four additional exact source-bound HP faces.");

        for (int index = 0; index < existingFaces.Length; index++)
            existingFaces[index].ApplyTextureOverride(unreferencedIds[index]);
        for (int index = 0; index < appendedFaces.Length; index++)
        {
            appendedFaces[index].ApplyTextureOverride(
                binding.ExpectedSourceTextureCount + index);
        }
        appendedMaterialTemplates = appendedFaces
            .Select(face => face.OriginalTextureId)
            .ToArray();
        TerrainPolygon[] editedFaces =
            existingFaces.Concat(appendedFaces).ToArray();
        int savedFaceCount = await TerrainEditStore.SaveAsync(
            editsPath,
            editedFaces,
            $"{level.DisplayName} 4-native + 46-appended visible face runtime candidate");
        Assert(
            savedFaceCount == TotalResearchTargets,
            $"{level.DisplayName} saved {savedFaceCount}/{TotalResearchTargets} historical research face assignments.");

        TerrainPatchPlan ordinaryTerrainPlan = TerrainPatchExporter.BuildPlan(
            sourceImagePath,
            sourceCuePath,
            outputPrefix + "-not-written.bin",
            outputPrefix + "-not-written.cue",
            level,
            ramPath: "",
            sourceSearchPath,
            editsPath);
        TerrainPatch[] facePatches = ordinaryTerrainPlan.Patches
            .Where(patch => patch.Kind.Equals(
                "texture-id-word3",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert(
            ordinaryTerrainPlan.Patches.Count == TotalResearchTargets &&
            facePatches.Length == TotalResearchTargets &&
            facePatches.Select(patch => patch.RuntimeKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(editedFaces.Select(face => face.RuntimeKey)),
            $"{level.DisplayName} did not emit exactly fifty isolated native face texture-ID patches.");
        Assert(
            ordinaryTerrainPlan.SkippedEdits.All(item =>
                item.Equals(
                    "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
                    StringComparison.Ordinal)),
            $"{level.DisplayName} terrain planner reported an unexpected skip: {string.Join(" | ", ordinaryTerrainPlan.SkippedEdits)}");
        ordinaryPatches =
            NativeTerrainTexturePrivateRecordBatchCompiler
                .ConvertOrdinaryTerrainPlan(ordinaryTerrainPlan);
        Assert(
            ordinaryPatches.Count == TotalResearchTargets &&
            ordinaryPatches.All(patch =>
                patch.Before.Length == sizeof(uint) &&
                patch.After.Length == sizeof(uint) &&
                patch.Kind.Equals(
                    "texture-id-word3",
                    StringComparison.OrdinalIgnoreCase)),
            $"{level.DisplayName} did not preserve fifty exact four-byte face patches.");
        faceAssignmentCount = ordinaryPatches.Count;
    }

    NativeTerrainTextureRelocationEdit[] existingEdits =
        unreferencedIds.Select((targetTextureId, index) =>
            BuildExistingEdit(
                level,
                stableDonors[index],
                targetTextureId))
        .ToArray();
    NativeTerrainTextureRelocationEdit[] appendedEdits =
        Enumerable.Range(0, AppendedPrivateRows)
            .Select(index => BuildAppendedEdit(
                binding,
                level,
                // Use one already-resident art payload across distinct native
                // material contracts. This proves row/identity capacity while
                // preserving the page's exact alias relationships; it does
                // not claim that 46 arbitrary unique art payloads fit.
                donorTextureId: stableDonors[0],
                materialTemplateTextureId: appendedMaterialTemplates[index],
                targetTextureId:
                    binding.ExpectedSourceTextureCount + index))
            .ToArray();
    Assert(
        appendedEdits.Select(edit =>
                (edit.DonorWadEntry,
                 edit.DonorTextureId,
                 edit.MaterialTemplateTextureId,
                 edit.ApplyMode))
            .Distinct()
            .Count() == AppendedPrivateRows,
        $"{level.DisplayName} appended row contracts are not unique.");
    NativeTerrainTextureRelocationEdit[] allEdits =
        existingEdits.Concat(appendedEdits).ToArray();

    NativeTerrainTexturePrivateRecordBatchRequest request = new(
        sourceImagePath,
        sourceCuePath,
        outputPrefix,
        wadAnalysisPath,
        level,
        allEdits,
        OrdinaryLevelDataPatches: ordinaryPatches,
        StructuralGrowthPolicy:
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch);
    bool planned =
        NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
            request,
            out NativeTerrainTexturePrivateRecordBatchPlan? plan,
            out string planFailure);
    Assert(
        planned && plan != null,
        $"{level.DisplayName} mixed allocator failed: {planFailure}");
    NativeTerrainTexturePrivateRecordBatchPlan builtPlan =
        plan ?? throw new InvalidOperationException(
            $"{level.DisplayName} returned no mixed allocator plan.");

    NativeTerrainTextureSectorPrivateRecordPlan sectorPlan =
        builtPlan.SectorRelocationPlan
        ?? throw new InvalidOperationException(
            $"{level.DisplayName} did not select the structural sector writer.");
    Assert(
        builtPlan.WriterKind ==
            NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
        builtPlan.FixedTailPlan == null &&
        builtPlan.StaticResearchOnly &&
        builtPlan.ExistingRecordEdits.Count ==
            FaceUnreferencedAnimationSourceRows &&
        builtPlan.ExistingRecordOverrides.Count ==
            FaceUnreferencedAnimationSourceRows &&
        builtPlan.AppendedPrivateEdits.Count == AppendedPrivateRows &&
        builtPlan.SyntheticRecords.Count == AppendedPrivateRows &&
        builtPlan.OrdinaryLevelDataPatches.Count == faceAssignmentCount,
        $"{level.DisplayName} did not retain the exact 4-native + 46-appended compiler contract.");
    Assert(
        sectorPlan.SourceBindingVerified &&
        sectorPlan.GlobalPackingProofComplete &&
        sectorPlan.OriginalRowsInstalledExactly &&
        sectorPlan.SyntheticRowsInstalledExactly &&
        sectorPlan.OrdinaryPatchesIncludedAndRelocated &&
        sectorPlan.TexturePagePatchesIncludedAndRelocated &&
        sectorPlan.StructuralSectorGrowthVerified &&
        sectorPlan.Structural.SectorGrowthBytes ==
            ExpectedSectorGrowth &&
        sectorPlan.Structural.RelocatedExecutableLba ==
            sectorPlan.Structural.OriginalExecutableLba +
                (ExpectedSectorGrowth / 0x800) &&
        sectorPlan.Structural.Append.OutputTextureCount ==
            binding.ExpectedSourceTextureCount + AppendedPrivateRows &&
        sectorPlan.StaticResearchOnly,
        $"{level.DisplayName} omitted a packing, row-installation, patch-rebase, or structural proof.");
    AssertGlobalProof(sectorPlan, level.DisplayName);

    foreach (NativeTerrainTextureRelocationEdit existing in existingEdits)
    {
        NativeTerrainTextureGlobalRepackPackedRecord packed =
            sectorPlan.GlobalPacking.OriginalMovableRecords.Single(row =>
                row.TargetTextureId == existing.TargetTextureId);
        Assert(
            packed.DonorWadEntry == existing.DonorWadEntry &&
            packed.DonorTextureId == existing.DonorTextureId,
            $"{level.DisplayName} existing native T{existing.TargetTextureId} lost its donor contract.");
    }
    Assert(
        sectorPlan.GlobalPacking.SyntheticRecords
            .Select(row => row.TargetTextureId)
            .SequenceEqual(Enumerable.Range(
                binding.ExpectedSourceTextureCount,
                AppendedPrivateRows)),
        $"{level.DisplayName} synthetic rows are not the exact contiguous retail tail.");

    bool normalAuthorized =
        AppendedPrivateTerrainTexturePromotionProfileRegistry
            .TryAuthorizeNormalRelease(
                binding,
                builtPlan.WriterKind,
                AppendedPrivateRows,
                out AppendedPrivateTerrainTexturePromotionProfile? promotionProfile,
                out string normalGateReason);
    Assert(
        !normalAuthorized && promotionProfile == null,
        $"{level.DisplayName} static-research mixed plan accidentally entered normal Create BIN.");

    NativeTerrainTexturePrivateRecordBatchExportResult? export = null;
    bool explicitLogicalReadback = false;
    string explicitLogicalFailure = "";
    if (exportFinalBins)
    {
        export =
            await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(
                builtPlan);
        Assert(
            export.SourceImagePreserved &&
            export.ExactReadbackVerified &&
            export.RuntimeTargetReadbackVerified &&
            export.GlobalLogicalReadbackVerified &&
            export.AtomicRenameCompleted &&
            File.Exists(export.OutputImagePath) &&
            File.Exists(export.OutputCuePath),
            $"{level.DisplayName} final BIN omitted a source, structural, runtime-target, logical, or atomic-write proof.");

        NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows =
            sectorPlan.GlobalPacking.OriginalMovableRecords
                .Concat(sectorPlan.GlobalPacking.SyntheticRecords)
                .OrderBy(row => row.TargetTextureId)
                .ToArray();
        explicitLogicalReadback =
            NativeTerrainTextureGlobalRepackerResearch
                .TryVerifyCandidateLogicalReadback(
                    export.OutputImagePath,
                    level,
                    expectedRows,
                    sourceImagePath,
                    out explicitLogicalFailure);
        Assert(
            explicitLogicalReadback,
            $"{level.DisplayName} immutable-source logical readback failed: {explicitLogicalFailure}");
    }

    targetProofs.Add(new TargetProof(
        level.Key,
        level.DisplayName,
        level.SourceWadEntry,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        unreferencedIds,
        stableDonors,
        AppendedPrivateRows,
        TotalResearchTargets,
        faceAssignmentCount,
        binding.ExpectedSourceTextureCount + AppendedPrivateRows,
        builtPlan.WriterKind.ToString(),
        builtPlan.StructuralGrowthPolicy.ToString(),
        sectorPlan.Structural.SectorGrowthBytes,
        sectorPlan.Structural.OriginalExecutableLba,
        sectorPlan.Structural.RelocatedExecutableLba,
        sectorPlan.GlobalPacking.PackingProof.ProtectedByteCount,
        sectorPlan.GlobalPacking.PackingProof.AllocatedByteCount,
        sectorPlan.GlobalPacking.PackingProof.FreeByteCount,
        sectorPlan.GlobalPacking.PackingProof.PackingStrategy,
        normalAuthorized,
        normalGateReason,
        export?.OutputImagePath ?? "",
        export?.OutputCuePath ?? "",
        export?.OutputImageSha256 ?? "",
        export?.ExactReadbackVerified ?? false,
        export?.GlobalLogicalReadbackVerified ?? false,
        explicitLogicalReadback,
        exportFinalBins
            ? explicitLogicalFailure
            : "Final-BIN export intentionally skipped; run with --export."));
}

Assert(
    FileLength(sourceImagePath) == sourceLength &&
    File.GetLastWriteTimeUtc(sourcePhysicalPath) == sourceWriteTime &&
    string.Equals(
        Sha256File(sourceImagePath),
        sourceSha256,
        StringComparison.OrdinalIgnoreCase),
    "The mixed fifty-row smoke changed its immutable retail source.");

await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status = visibleRuntimeCandidate
                ? exportFinalBins
                    ? "VISIBLE 50-FACE RUNTIME CANDIDATE; final-BIN readback passed; DuckStation pending"
                    : "VISIBLE 50-FACE RUNTIME CANDIDATE plan; final BIN not written"
                : exportFinalBins
                    ? "STATIC-ONLY final-BIN readback; runtime-unverified; not promoted"
                    : "STATIC-ONLY plan proof; final BIN not written; runtime-unverified; not promoted",
            FaceUnreferencedAnimationSourceRows,
            AppendedPrivateRows,
            TotalResearchTargets,
            Source = new
            {
                Path = sourceImagePath,
                PhysicalPath = sourcePhysicalPath,
                Sha256 = sourceSha256,
                Length = sourceLength,
                Preserved = true
            },
            ExportRequested = exportFinalBins,
            Targets = targetProofs,
            Conclusions = new[]
            {
                "The compiler accepts face-unreferenced animation-source ExistingNative overrides and appended-private rows in one atomic historical research plan.",
                "Wizard Peak and Tree Tops each expose four face-unreferenced animation-source diagnostic records in the inspected retail source. Those rows do not count as safe normal-editor private capacity.",
                "The historical 4 + 46 plans address fifty research targets without exceeding the seven-bit texture ID field; only the appended tail counts toward ordinary private-slot capacity. The appended rows deliberately alias one already-resident art payload across unique material contracts, so this is not proof that 46 arbitrary unique art payloads fit the fixed page.",
                visibleRuntimeCandidate
                    ? "This candidate assigns four animation-source diagnostic rows plus all 46 appended rows to fifty exact source-bound HP terrain faces. It is an unsafe historical diagnostic, not a normal editor-capacity candidate. Final readback is static evidence only until DuckStation confirms visible near/far rendering and traversal."
                    : "This proof does not assign those records to fifty terrain faces and does not constitute DuckStation runtime evidence.",
                "Normal Beta/Create BIN authorization remains false."
            }
        },
        new JsonSerializerOptions { WriteIndented = true }) +
    Environment.NewLine);

Console.WriteLine(visibleRuntimeCandidate
    ? "Mixed fifty private terrain-texture visible runtime-candidate smoke passed."
    : "Mixed fifty private terrain-texture static smoke passed.");
foreach (TargetProof proof in targetProofs)
{
    Console.WriteLine(
        $"- {proof.LevelName}: face-unreferenced animation sources " +
        $"{FormatIds(proof.FaceUnreferencedAnimationSourceIds)} + " +
        $"{proof.AppendedRows} appended = {proof.TotalResearchTargets} historical research targets; " +
        $"+0x{proof.SectorGrowthBytes:X}; final table {proof.FinalTextureCount}.");
}
Console.WriteLine($"- Static proof: {proofPath}");
Console.WriteLine(exportFinalBins
    ? visibleRuntimeCandidate
        ? "- Fifty exact face assignments plus structural and immutable-source logical readback passed; DuckStation remains pending."
        : "- Final-BIN structural and immutable-source logical readback passed; DuckStation remains unproven."
    : "- No large BIN was written. Re-run with --export only after reviewing this plan proof.");
Console.WriteLine("- Normal Create BIN promotion remains intentionally false.");

static NativeTerrainTextureRelocationEdit BuildExistingEdit(
    LevelDefinition donor,
    int donorTextureId,
    int targetTextureId) =>
    new(
        targetTextureId,
        donor.Key,
        donor.DisplayName,
        donor.SourceWadEntry,
        donorTextureId,
        NativeTerrainTextureRelocationEditStore
            .BuildTextureRecordProvenanceKey(
                donor.Key,
                donorTextureId),
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-26T20:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.ExistingNative);

static NativeTerrainTextureRelocationEdit BuildAppendedEdit(
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
        NativeTerrainTextureRelocationEditStore
            .BuildTextureRecordProvenanceKey(
                donor.Key,
                donorTextureId),
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-26T20:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        binding.TargetWadEntry,
        binding.SourceImageSha256,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        materialTemplateTextureId,
        NativeTerrainTextureRelocationEditStore
            .BuildAppendedPrivateRecordId(targetTextureId));

static void AssertGlobalProof(
    NativeTerrainTextureSectorPrivateRecordPlan sectorPlan,
    string levelName)
{
    NativeTerrainTextureGlobalRepackResearchPlan proof =
        sectorPlan.GlobalPacking.PackingProof;
    Assert(
        proof.ExactIndexedPixelReadbackVerified &&
        proof.ExactPaletteReadbackVerified &&
        proof.PixelAliasRelationshipsPreserved &&
        proof.PaletteAliasRelationshipsPreserved &&
        proof.LowDetailAliasPreserved &&
        proof.ProtectedStoragePreserved &&
        proof.FixedDescriptorRowsPreserved &&
        proof.TargetMaterialBitsPreserved,
        $"{levelName} global packer omitted an indexed-pixel, palette, alias, protected-storage, fixed-row, or material proof.");
}

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(
        candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(
                directory.FullName,
                "Spyro the Dragon (USA).bin")) &&
            Directory.Exists(Path.Combine(
                directory.FullName,
                "src",
                "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static string ResolvePhysicalPath(string path)
{
    FileInfo info = new(Path.GetFullPath(path));
    return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
        ?? info.FullName;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long FileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static string FormatIds(IEnumerable<int> ids) =>
    string.Join(", ", ids.Select(textureId => $"T{textureId}"));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record TargetSpec(
    string LevelKey,
    string OverlayName,
    int ExpectedSourceTextureCount,
    int[] ExpectedFaceUnreferencedAnimationSourceIds);

internal sealed record TargetProof(
    string LevelKey,
    string LevelName,
    int WadEntry,
    int NativeTextureCount,
    string ExpectedTextureComponentSha256,
    string ExpectedLevelDataSha256,
    int[] FaceUnreferencedAnimationSourceIds,
    int[] ExistingDonorTextureIds,
    int AppendedRows,
    int TotalResearchTargets,
    int FaceAssignmentCount,
    int FinalTextureCount,
    string WriterKind,
    string StructuralGrowthPolicy,
    int SectorGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    int ProtectedByteCount,
    int AllocatedByteCount,
    int FreeByteCount,
    string PackingStrategy,
    bool NormalReleaseAuthorized,
    string NormalGateReason,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool ExactFinalBinReadbackVerified,
    bool GlobalLogicalReadbackVerified,
    bool ExplicitImmutableSourceLogicalReadbackVerified,
    string FinalBinReadbackNote);
