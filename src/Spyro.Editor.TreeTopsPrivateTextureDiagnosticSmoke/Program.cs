using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int ExistingNativePrivateRows = 4;
const int DiagnosticAppendedRows = 46;
const int PrimarySafeAppendedRows = 47;
const int ExpectedSourceTextureCount = 81;
const int ExpectedSectorGrowth = 0x2000;
int[] expectedFaceUnreferencedAnimationSourceIds = [3, 5, 9, 11];

bool exportFinalBins = args.Any(argument =>
    argument.Equals("--export", StringComparison.OrdinalIgnoreCase));
bool minimalFaceDiagnostics = args.Any(argument =>
    argument.Equals(
        "--minimal-face-diagnostics",
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
    "tree-tops-private-texture-load-freeze-diagnostics");
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
string sourceSearchPath = Path.Combine(outputRoot, "treetops-source-search.json");
string proofPath = Path.Combine(
    outputRoot,
    minimalFaceDiagnostics
        ? "tree-tops-minimal-face-binary-diagnostic-proof.json"
        : "tree-tops-load-freeze-diagnostic-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-minimal-face-runtime-checklist.md");
string knownLoadingDormantBaselinePath = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "mixed-fifty-private-texture-static",
    "treetops-4-native-plus-46-appended-STATIC-ONLY.bin");

Directory.CreateDirectory(outputRoot);
Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
Assert(File.Exists(sourceCuePath), $"Missing retail source CUE: {sourceCuePath}");
await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

string sourcePhysicalPath = ResolvePhysicalPath(sourceImagePath);
string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePhysicalPath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition level = catalog.FindByKey("treetops")
    ?? throw new InvalidOperationException("Tree Tops is missing from the level catalog.");
string mobyCachePath = Path.Combine(
    workspaceRoot,
    "editor-cache",
    "treetops-mobys.json");
Assert(
    File.Exists(mobyCachePath),
    $"Missing source-derived Tree Tops moby cache: {mobyCachePath}");
using JsonDocument mobyCache = JsonDocument.Parse(
    await File.ReadAllTextAsync(mobyCachePath));
JsonElement[] class0125Rows = mobyCache.RootElement
    .GetProperty("mobys")
    .EnumerateArray()
    .Where(row =>
        row.GetProperty("sourceByte36Hex").GetString() == "0x25" &&
        row.GetProperty("sourceByte37Hex").GetString() == "0x01")
    .ToArray();
JsonElement[] class0126Rows = mobyCache.RootElement
    .GetProperty("mobys")
    .EnumerateArray()
    .Where(row =>
        row.GetProperty("sourceByte36Hex").GetString() == "0x26" &&
        row.GetProperty("sourceByte37Hex").GetString() == "0x01")
    .ToArray();
Assert(
    class0125Rows is [{ }] &&
    class0125Rows[0].GetProperty("trueIndex").GetInt32() ==
        TreeTopsOpeningGemThiefDiagnosticPatch.OpeningActorTrueIndex &&
    class0125Rows[0].GetProperty("propertiesPointer").GetString() ==
        "0x00011D3C",
    "Tree Tops' unique opening class 0x0125 T0/properties preimage changed.");
Assert(
    class0126Rows is [{ }] &&
    class0126Rows[0].GetProperty("trueIndex").GetInt32() == 78,
    "Tree Tops' adjacent class 0x0126 actor preimage changed.");
string overlayPath = Path.Combine(
    workspaceRoot,
    "editor-cache",
    "treetops-runtime-scene-editor-overlay.json");
Assert(File.Exists(overlayPath), $"Missing Tree Tops geometry overlay: {overlayPath}");

NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
        sourceImagePath,
        level);
Assert(
    binding.ExpectedSourceTextureCount == ExpectedSourceTextureCount,
    $"Tree Tops retail texture count changed from {ExpectedSourceTextureCount} " +
    $"to {binding.ExpectedSourceTextureCount}.");
Assert(
    binding.ExpectedSourceTextureCount + PrimarySafeAppendedRows <=
        NativeTerrainTextureRecordAppendBuilder.MaximumTextureRecordCount,
    "Tree Tops cannot address 47 appended rows in the native seven-bit texture field.");

GeometryCandidate referenceGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
HashSet<int> referencedIds = referenceGeometry.Polygons
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
        expectedFaceUnreferencedAnimationSourceIds),
    $"Tree Tops unreferenced native rows changed; expected " +
    $"{FormatIds(expectedFaceUnreferencedAnimationSourceIds)}, found {FormatIds(unreferencedIds)}.");

NativeTerrainTextureRuntimeControlAudit runtime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(
        sourceImagePath,
        level);
Assert(
    runtime.Complete,
    $"Tree Tops runtime-control audit is incomplete: " +
    $"{string.Join(" | ", runtime.SafetyBlockers)}");
Assert(
    unreferencedIds.All(runtime.IsRuntimePersistentTarget),
    "Tree Tops has an unreferenced row controlled by native runtime animation or scrolling.");
Assert(
    unreferencedIds.All(runtime.AnimationSourceTextureIds.Contains),
    "Tree Tops face-unreferenced rows are no longer the expected animation-source diagnostic rows.");

int[] stableDonors = Enumerable.Range(
        0,
        binding.ExpectedSourceTextureCount)
    .Where(runtime.IsRuntimePersistentTarget)
    .Where(textureId =>
        !runtime.AnimationSourceTextureIds.Contains(textureId))
    .Except(unreferencedIds)
    .Take(ExistingNativePrivateRows)
    .ToArray();
Assert(
    stableDonors.Length == ExistingNativePrivateRows,
    "Tree Tops does not expose four stable referenced donor rows.");

TerrainSourceSearchResult sourceSearch =
    await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            level,
            referenceGeometry));
HashSet<string> exactSourceKeys = sourceSearch.Report.Results
    .Where(result => result.FullSectorHits.Count == 1)
    .Select(result => result.Edit)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
TerrainPolygon[] exactFaces = referenceGeometry.Polygons
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
TerrainPolygon[] appendedReferenceFaces = exactFaces
    .GroupBy(face => face.OriginalTextureId)
    .OrderBy(group => group.Key)
    .Select(group => group.First())
    .Take(DiagnosticAppendedRows)
    .ToArray();
HashSet<string> appendedReferenceKeys = appendedReferenceFaces
    .Select(face => face.RuntimeKey)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
TerrainPolygon[] existingReferenceFaces = exactFaces
    .Where(face => !appendedReferenceKeys.Contains(face.RuntimeKey))
    .Take(ExistingNativePrivateRows)
    .ToArray();
Assert(
    appendedReferenceFaces.Length == DiagnosticAppendedRows &&
    appendedReferenceFaces
        .Select(face => face.OriginalTextureId)
        .Distinct()
        .Count() == DiagnosticAppendedRows &&
    existingReferenceFaces.Length == ExistingNativePrivateRows,
    "Tree Tops does not expose 46 distinct-material plus four additional exact source-bound HP faces.");

FaceReference[] appendedFaceReferences = appendedReferenceFaces
    .Select(face => new FaceReference(
        face.RuntimeKey,
        face.OriginalTextureId,
        face.SectorIndex,
        face.FaceIndex))
    .ToArray();
FaceReference[] existingFaceReferences = existingReferenceFaces
    .Select(face => new FaceReference(
        face.RuntimeKey,
        face.OriginalTextureId,
        face.SectorIndex,
        face.FaceIndex))
    .ToArray();

HashSet<int> runtimeRewrittenOrSourcedIds = runtime.ControlledTextureIds
    .Concat(runtime.AnimationSourceTextureIds)
    .ToHashSet();
TerrainPolygon[] safeStaticReferenceFaces = exactFaces
    .Where(face => !runtimeRewrittenOrSourcedIds.Contains(
        face.OriginalTextureId))
    .GroupBy(face => face.OriginalTextureId)
    .OrderBy(group => group.Key)
    .Select(group => group.First())
    .Take(PrimarySafeAppendedRows)
    .ToArray();
Assert(
    safeStaticReferenceFaces.Length == PrimarySafeAppendedRows &&
    safeStaticReferenceFaces.All(face =>
        runtime.IsRuntimePersistentTarget(face.OriginalTextureId) &&
        !runtime.AnimationSourceTextureIds.Contains(
            face.OriginalTextureId)),
    "Tree Tops does not expose 47 exact face-referenced material templates outside every runtime animation destination and source.");
FaceReference[] safeAppendedFaceReferences = safeStaticReferenceFaces
    .Select(face => new FaceReference(
        face.RuntimeKey,
        face.OriginalTextureId,
        face.SectorIndex,
        face.FaceIndex))
    .ToArray();
Assert(
    safeAppendedFaceReferences[0] is
    {
        RuntimeKey: "1:32:hp",
        OriginalTextureId: 12
    },
    "Tree Tops minimal diagnostic anchor changed; expected exact face 1:32:hp with native T12.");
TerrainPolygon? nearEntryAnchorFace = exactFaces.FirstOrDefault(face =>
    face.RuntimeKey.Equals(
        "207:9:hp",
        StringComparison.OrdinalIgnoreCase));
Assert(
    nearEntryAnchorFace is
    {
        OriginalTextureId: 12,
        SectorIndex: 207,
        FaceIndex: 9
    },
    "Tree Tops near-entry diagnostic anchor changed; expected exact face 207:9:hp with native T12.");
FaceReference nearEntryAnchorReference = new(
    nearEntryAnchorFace!.RuntimeKey,
    nearEntryAnchorFace.OriginalTextureId,
    nearEntryAnchorFace.SectorIndex,
    nearEntryAnchorFace.FaceIndex);
FaceReference[] safeDiagnosticFaceReferences =
    safeAppendedFaceReferences
        .Append(nearEntryAnchorReference)
        .ToArray();
int nearEntryAnchorIndex =
    safeDiagnosticFaceReferences.Length - 1;

DiagnosticScenario[] scenarios = minimalFaceDiagnostics
    ?
    [
        BuildKnownLoadingDormant46Scenario(),
        BuildDormant47Scenario(),
        BuildMinimalScenario(1),
        BuildNearEntryT81Scenario(nearEntryAnchorIndex),
        BuildSameAnchorT82Scenario(),
        BuildMinimalScenario(2),
        BuildMinimalScenario(4),
        BuildMinimalScenario(8),
        BuildMinimalScenario(16),
        BuildMinimalScenario(32)
    ]
    :
    [
    new(
        "SAFE",
        "treetops-SAFE-47-appended-47-runtime-static-visible-faces-PRIMARY-DIAGNOSTIC",
        "Primary retest: all 47 addressable appended rows assigned only to exact faces whose original material IDs are neither runtime destinations nor animation sources; no reused-native rows.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: false,
        IncludeAppendedRows: true,
        InstalledAppendedRows: PrimarySafeAppendedRows,
        AssignedAppendedIndices: Enumerable.Range(0, PrimarySafeAppendedRows).ToArray(),
        UseLegacySequentialMaterialTemplates: false,
        UseSafeStaticFaceSet: true),
    new(
        "A",
        "treetops-A-46-appended-46-visible-faces-DIAGNOSTIC",
        "All 46 appended rows assigned to all 46 exact faces; no reused-native assignments.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: DiagnosticAppendedRows,
        AssignedAppendedIndices: Enumerable.Range(0, DiagnosticAppendedRows).ToArray(),
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: false),
    new(
        "B",
        "treetops-B-4-reused-native-4-visible-faces-DIAGNOSTIC",
        "Only four faces are assigned to the face-unreferenced animation-source rows T3/T5/T9/T11; the known-working dormant 46-row structural baseline remains installed because the legacy no-growth writer cannot isolate their shared storage.",
        IncludeExistingNativeAssignments: true,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: DiagnosticAppendedRows,
        AssignedAppendedIndices: [],
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: false),
    new(
        "C",
        "treetops-C-46-appended-first-23-visible-faces-DIAGNOSTIC",
        "All 46 appended rows installed structurally; only appended faces 0-22 assigned.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: DiagnosticAppendedRows,
        AssignedAppendedIndices: Enumerable.Range(0, DiagnosticAppendedRows / 2).ToArray(),
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: false),
    new(
        "D",
        "treetops-D-46-appended-last-23-visible-faces-DIAGNOSTIC",
        "All 46 appended rows installed structurally; only appended faces 23-45 assigned.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: DiagnosticAppendedRows,
        AssignedAppendedIndices: Enumerable.Range(
            DiagnosticAppendedRows / 2,
            DiagnosticAppendedRows / 2).ToArray(),
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: false),
    ];

List<DiagnosticProof> diagnostics = [];
IntroHandlerDiscriminatorProof? introHandlerDiscriminator = null;
foreach (DiagnosticScenario scenario in scenarios)
{
    string outputPrefix = Path.Combine(outputRoot, scenario.OutputName);
    string fastEntryPrefix = outputPrefix + "-FAST-ENTRY";
    string editsPath = outputPrefix + ".terrain-edits.json";
    string relocationManifestPath =
        outputPrefix + ".native-terrain-texture-relocations.json";
    foreach (string path in new[]
             {
                 outputPrefix + ".bin",
                 outputPrefix + ".cue",
                 fastEntryPrefix + ".bin",
                 fastEntryPrefix + ".cue",
                 outputPrefix + ".terrain-patch-plan.json",
                 editsPath,
                 relocationManifestPath
             })
    {
        bool finalImageArtifact =
            path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase);
        if ((!finalImageArtifact || exportFinalBins) && File.Exists(path))
            File.Delete(path);
    }

    GeometryCandidate scenarioGeometry =
        GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    FaceReference[] scenarioFaceReferences =
        scenario.UseSafeStaticFaceSet
            ? safeDiagnosticFaceReferences
            : appendedFaceReferences;
    FaceReference[] scenarioMaterialReferences =
        scenario.UseSafeStaticFaceSet
            ? safeAppendedFaceReferences
            : appendedFaceReferences;
    FaceReference[] scenarioExistingFaceReferences =
        existingFaceReferences;
    Dictionary<string, TerrainPolygon> facesByKey =
        scenarioGeometry.Polygons.ToDictionary(
            face => face.RuntimeKey,
            StringComparer.OrdinalIgnoreCase);
    List<TerrainPolygon> editedFaces = [];
    if (scenario.IncludeExistingNativeAssignments)
    {
        for (int index = 0;
             index < scenarioExistingFaceReferences.Length;
             index++)
        {
            TerrainPolygon face = RequireFace(
                facesByKey,
                scenarioExistingFaceReferences[index]);
            face.ApplyTextureOverride(unreferencedIds[index]);
            editedFaces.Add(face);
        }
    }
    int[] assignedFaceIndices =
        scenario.AssignedFaceIndices ??
        scenario.AssignedAppendedIndices;
    Assert(
        assignedFaceIndices.Length ==
            scenario.AssignedAppendedIndices.Length,
        $"{scenario.Id}: appended texture assignments and exact face assignments must have the same length.");
    for (int assignmentIndex = 0;
         assignmentIndex < scenario.AssignedAppendedIndices.Length;
         assignmentIndex++)
    {
        int appendedIndex =
            scenario.AssignedAppendedIndices[assignmentIndex];
        int faceIndex = assignedFaceIndices[assignmentIndex];
        Assert(
            appendedIndex >= 0 &&
            appendedIndex < scenarioMaterialReferences.Length,
            $"{scenario.Id}: invalid appended texture index {appendedIndex}.");
        Assert(
            faceIndex >= 0 &&
            faceIndex < scenarioFaceReferences.Length,
            $"{scenario.Id}: invalid exact face index {faceIndex}.");
        TerrainPolygon face = RequireFace(
            facesByKey,
            scenarioFaceReferences[faceIndex]);
        face.ApplyTextureOverride(
            binding.ExpectedSourceTextureCount + appendedIndex);
        editedFaces.Add(face);
    }

    int expectedAssignmentCount =
        (scenario.IncludeExistingNativeAssignments
            ? ExistingNativePrivateRows
            : 0) +
        scenario.AssignedAppendedIndices.Length;
    int savedFaceCount = await TerrainEditStore.SaveAsync(
        editsPath,
        editedFaces,
        $"Tree Tops load-freeze diagnostic {scenario.Id}: {scenario.Description}");
    Assert(
        savedFaceCount == expectedAssignmentCount,
        $"{scenario.Id}: saved {savedFaceCount}/{expectedAssignmentCount} exact face assignments.");

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
        ordinaryTerrainPlan.Patches.Count == expectedAssignmentCount &&
        facePatches.Length == expectedAssignmentCount &&
        facePatches.Select(patch => patch.RuntimeKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(editedFaces.Select(face => face.RuntimeKey)),
        $"{scenario.Id}: ordinary terrain plan did not emit exactly {expectedAssignmentCount} isolated face texture-ID patches.");
    AssertExpectedTerrainSkips(ordinaryTerrainPlan, scenario.Id);

    string outputImagePath = "";
    string outputCuePath = "";
    string outputImageSha256 = "";
    bool sourcePreserved = false;
    bool exactFinalReadback = false;
    bool logicalReadback = false;
    int finalTextureCount = binding.ExpectedSourceTextureCount;
    int sectorGrowthBytes = 0;
    string writerKind = "ExistingNativeInPlace";
    string structuralPolicy = "None";
    NativeTerrainTextureGlobalRepackPackedRecord[] expectedPackedRows = [];
    string normalGateReason =
        "Diagnostic B changes only four exact face words to dormant native IDs; it is not a promotion profile.";

    if (scenario.IncludeAppendedRows)
    {
        IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches =
            NativeTerrainTexturePrivateRecordBatchCompiler
                .ConvertOrdinaryTerrainPlan(ordinaryTerrainPlan);
        NativeTerrainTextureRelocationEdit[] existingEdits =
            scenario.IncludeExistingNativeEdits
                ? unreferencedIds.Select((targetTextureId, index) =>
                    BuildExistingEdit(
                        level,
                        stableDonors[index],
                        targetTextureId))
                    .ToArray()
                : [];
        NativeTerrainTextureRelocationEdit[] appendedEdits =
            Enumerable.Range(0, scenario.InstalledAppendedRows)
                .Select(index => BuildAppendedEdit(
                    binding,
                    level,
                    donorTextureId: stableDonors[0],
                    materialTemplateTextureId:
                        scenario.UseLegacySequentialMaterialTemplates
                            ? index
                            : scenarioMaterialReferences[index]
                                .OriginalTextureId,
                    targetTextureId:
                        binding.ExpectedSourceTextureCount + index))
                .ToArray();
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
            $"{scenario.Id}: appended-private compiler failed: {planFailure}");
        NativeTerrainTexturePrivateRecordBatchPlan builtPlan =
            plan ?? throw new InvalidOperationException(
                $"{scenario.Id}: appended-private compiler returned no plan.");
        NativeTerrainTextureSectorPrivateRecordPlan sectorPlan =
            builtPlan.SectorRelocationPlan
            ?? throw new InvalidOperationException(
                $"{scenario.Id}: structural sector writer was not selected.");
        Assert(
            builtPlan.WriterKind ==
                NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
            builtPlan.ExistingRecordEdits.Count ==
                existingEdits.Length &&
            builtPlan.ExistingRecordOverrides.Count ==
                existingEdits.Length &&
            builtPlan.AppendedPrivateEdits.Count ==
                scenario.InstalledAppendedRows &&
            builtPlan.SyntheticRecords.Count ==
                scenario.InstalledAppendedRows &&
            builtPlan.OrdinaryLevelDataPatches.Count == expectedAssignmentCount &&
            builtPlan.StaticResearchOnly,
            $"{scenario.Id}: compiler did not preserve the exact mixed diagnostic contract.");
        Assert(
            sectorPlan.SourceBindingVerified &&
            sectorPlan.GlobalPackingProofComplete &&
            sectorPlan.OriginalRowsInstalledExactly &&
            sectorPlan.SyntheticRowsInstalledExactly &&
            sectorPlan.OrdinaryPatchesIncludedAndRelocated &&
            sectorPlan.TexturePagePatchesIncludedAndRelocated &&
            sectorPlan.StructuralSectorGrowthVerified &&
            sectorPlan.Structural.SectorGrowthBytes == ExpectedSectorGrowth &&
            sectorPlan.Structural.Append.OutputTextureCount ==
                binding.ExpectedSourceTextureCount +
                    scenario.InstalledAppendedRows &&
            sectorPlan.StaticResearchOnly,
            $"{scenario.Id}: structural, packing, row-installation, or patch-rebase proof is incomplete.");
        AssertGlobalProof(sectorPlan, scenario.Id);

        bool normalAuthorized =
            AppendedPrivateTerrainTexturePromotionProfileRegistry
                .TryAuthorizeNormalRelease(
                    binding,
                    builtPlan.WriterKind,
                    scenario.InstalledAppendedRows,
                    out AppendedPrivateTerrainTexturePromotionProfile? profile,
                    out normalGateReason);
        Assert(
            !normalAuthorized && profile == null,
            $"{scenario.Id}: Tree Tops accidentally entered normal Create BIN.");

        writerKind = builtPlan.WriterKind.ToString();
        structuralPolicy = builtPlan.StructuralGrowthPolicy.ToString();
        finalTextureCount =
            sectorPlan.Structural.Append.OutputTextureCount;
        sectorGrowthBytes = sectorPlan.Structural.SectorGrowthBytes;
        expectedPackedRows =
            sectorPlan.GlobalPacking.OriginalMovableRecords
                .Concat(sectorPlan.GlobalPacking.SyntheticRecords)
                .OrderBy(row => row.TargetTextureId)
                .ToArray();
        if (exportFinalBins)
        {
            NativeTerrainTexturePrivateRecordBatchExportResult export =
                await NativeTerrainTexturePrivateRecordBatchCompiler
                    .ExportAsync(builtPlan);
            Assert(
                export.SourceImagePreserved &&
                export.ExactReadbackVerified &&
                export.RuntimeTargetReadbackVerified &&
                export.GlobalLogicalReadbackVerified &&
                export.AtomicRenameCompleted &&
                File.Exists(export.OutputImagePath) &&
                File.Exists(export.OutputCuePath),
                $"{scenario.Id}: final BIN omitted an exact source, structural, runtime-target, logical, or atomic-write proof.");
            logicalReadback =
                NativeTerrainTextureGlobalRepackerResearch
                    .TryVerifyCandidateLogicalReadback(
                        export.OutputImagePath,
                        level,
                        expectedPackedRows,
                        sourceImagePath,
                        out string explicitFailure);
            Assert(
                logicalReadback,
                $"{scenario.Id}: immutable-source logical readback failed: {explicitFailure}");
            outputImagePath = export.OutputImagePath;
            outputCuePath = export.OutputCuePath;
            outputImageSha256 = export.OutputImageSha256;
            sourcePreserved = export.SourceImagePreserved;
            exactFinalReadback = export.ExactReadbackVerified;
        }
    }
    else
    {
        if (exportFinalBins)
        {
            TerrainPatchResult export =
                await TerrainPatchExporter.ExportAsync(
                    new TerrainPatchRequest(
                        sourceImagePath,
                        sourceCuePath,
                        outputPrefix,
                        level,
                        RamPath: "",
                        sourceSearchPath,
                        editsPath,
                        CustomTexturesPath: "",
                        WriteImage: true));
            Assert(
                export.WroteImage &&
                File.Exists(export.OutputImagePath) &&
                File.Exists(export.OutputCuePath),
                "B: no-growth final BIN/CUE was not written.");
            outputImagePath = export.OutputImagePath;
            outputCuePath = export.OutputCuePath;
            outputImageSha256 = Sha256File(outputImagePath);
            sourcePreserved =
                FileLength(sourceImagePath) == sourceLength &&
                string.Equals(
                    Sha256File(sourceImagePath),
                    sourceSha256,
                    StringComparison.OrdinalIgnoreCase);
            exactFinalReadback = true;
            logicalReadback = true;
            Assert(
                sourcePreserved && logicalReadback,
                "B: no-growth export did not preserve its source or exact texture readback proof.");
        }
    }

    bool usesKnownLoadingDormantStructure =
        scenario.InstalledAppendedRows ==
            DiagnosticAppendedRows &&
        scenario.IncludeExistingNativeEdits &&
        scenario.UseLegacySequentialMaterialTemplates;
    int? knownDormantBaselineByteDifferences = null;
    if (exportFinalBins &&
        usesKnownLoadingDormantStructure &&
        !string.IsNullOrWhiteSpace(outputImagePath))
    {
        Assert(
            File.Exists(knownLoadingDormantBaselinePath),
            $"Missing known-loading dormant Tree Tops baseline: {knownLoadingDormantBaselinePath}");
        knownDormantBaselineByteDifferences =
            CountByteDifferences(
                knownLoadingDormantBaselinePath,
                outputImagePath);
        Assert(
            knownDormantBaselineByteDifferences ==
                expectedAssignmentCount,
            $"{scenario.Id}: candidate differs from the known-loading dormant baseline by " +
            $"{knownDormantBaselineByteDifferences} byte(s), expected exactly " +
            $"{expectedAssignmentCount} face texture-ID byte(s).");
    }

    bool fastEntryEnabled = false;
    string fastEntryInstructions = "";
    string fastEntryImageOffset = "";
    string fastEntryOutputImagePath = "";
    string fastEntryOutputCuePath = "";
    string fastEntryOutputImageSha256 = "";
    int? fastEntryControlByteDifferences = null;
    bool fastEntryLogicalReadback = false;
    if (exportFinalBins && !string.IsNullOrWhiteSpace(outputImagePath))
    {
        string controlSha256 = Sha256File(outputImagePath);
        fastEntryOutputImagePath = fastEntryPrefix + ".bin";
        fastEntryOutputCuePath = fastEntryPrefix + ".cue";
        await Task.Run(() =>
            File.Copy(
                outputImagePath,
                fastEntryOutputImagePath,
                overwrite: true));
        string fastCueText = BuildCueTextForImage(
            outputCuePath,
            Path.GetFileName(fastEntryOutputImagePath));
        await File.WriteAllTextAsync(
            fastEntryOutputCuePath,
            fastCueText,
            Encoding.ASCII);
        string writtenFastCueText =
            await File.ReadAllTextAsync(
                fastEntryOutputCuePath,
                Encoding.ASCII);
        MobySourcePatch fastEntryPatch =
            TestLevelWarpPatch.ApplyToDisposableDiagnosticImage(
                fastEntryOutputImagePath,
                level);
        Assert(
            TestLevelWarpPatch.TryGetTargetLevelId(
                fastEntryPatch,
                out int fastEntryTargetLevelId) &&
            fastEntryTargetLevelId == level.LevelId &&
            fastEntryPatch.BeforeHexPreview ==
                "37 B6 00 08 00 00 00 00" &&
            fastEntryPatch.AfterHexPreview ==
                "1C 06 84 AF 88 06 80 AF" &&
            fastEntryPatch.ByteLength == 8,
            $"{scenario.Id}: guarded Tree Tops fast-entry patch changed.");
        fastEntryEnabled = true;
        fastEntryInstructions =
            $"Inventory: {TestLevelWarpPatch.ActivationSequence}; then " +
            $"{TestLevelWarpPatch.TargetSelectionText(level.LevelId)}.";
        fastEntryImageOffset = fastEntryPatch.ImageOffset;
        fastEntryControlByteDifferences =
            CountByteDifferences(
                outputImagePath,
                fastEntryOutputImagePath);
        Assert(
            fastEntryControlByteDifferences == 8,
            $"{scenario.Id}: fast-entry clone differs from its control by {fastEntryControlByteDifferences} byte(s), expected exactly the guarded 8-byte EXE patch.");
        Assert(
            string.Equals(
                Sha256File(outputImagePath),
                controlSha256,
                StringComparison.OrdinalIgnoreCase),
            $"{scenario.Id}: fast-entry cloning changed the pre-warp control BIN.");
        Assert(
            writtenFastCueText.Contains(
                $"FILE \"{Path.GetFileName(fastEntryOutputImagePath)}\" BINARY",
                StringComparison.Ordinal),
            $"{scenario.Id}: written fast-entry CUE does not name its cloned BIN.");
        string fastEntryLogicalFailure = "";
        fastEntryLogicalReadback =
            expectedPackedRows.Length > 0 &&
            NativeTerrainTextureGlobalRepackerResearch
                .TryVerifyCandidateLogicalReadback(
                    fastEntryOutputImagePath,
                    level,
                    expectedPackedRows,
                    sourceImagePath,
                    out fastEntryLogicalFailure);
        Assert(
            fastEntryLogicalReadback,
            $"{scenario.Id}: texture logical readback failed after the guarded fast-entry patch: {fastEntryLogicalFailure}");
        fastEntryOutputImageSha256 =
            Sha256File(fastEntryOutputImagePath);
    }

    if (exportFinalBins &&
        scenario.Id.Equals("MIN01", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(outputImagePath) &&
        !string.IsNullOrWhiteSpace(fastEntryOutputImagePath))
    {
        string retiredPrefix = Path.Combine(
            outputRoot,
            "treetops-MIN-01-T81-INTRO-GEM-THIEF-T0-HANDLER-RETIRED-DIAGNOSTIC");
        string retiredImagePath = retiredPrefix + ".bin";
        string retiredCuePath = retiredPrefix + ".cue";
        string retiredFastPrefix = retiredPrefix + "-FAST-ENTRY";
        string retiredFastImagePath = retiredFastPrefix + ".bin";
        string retiredFastCuePath = retiredFastPrefix + ".cue";
        foreach (string path in new[]
                 {
                     retiredImagePath,
                     retiredCuePath,
                     retiredFastImagePath,
                     retiredFastCuePath
                 })
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        await Task.Run(() =>
            File.Copy(
                outputImagePath,
                retiredImagePath,
                overwrite: true));
        await File.WriteAllTextAsync(
            retiredCuePath,
            BuildCueTextForImage(
                outputCuePath,
                Path.GetFileName(retiredImagePath)),
            Encoding.ASCII);
        MobySourcePatch retirementPatch =
            TreeTopsOpeningGemThiefDiagnosticPatch
                .ApplyToDisposableDiagnosticImage(
                    retiredImagePath,
                    level);
        Assert(
            retirementPatch.TrueIndex ==
                TreeTopsOpeningGemThiefDiagnosticPatch
                    .OpeningActorTrueIndex &&
            retirementPatch.BeforeHexPreview ==
                "30 2F 08 80" &&
            retirementPatch.AfterHexPreview ==
                "EC 6A 08 80" &&
            retirementPatch.ByteLength == 4,
            "MIN01 intro discriminator did not retain its exact guarded T0/class 0x0125 dispatch patch.");
        int retiredControlDifferences =
            CountByteDifferences(
                outputImagePath,
                retiredImagePath);
        Assert(
            retiredControlDifferences == 2,
            $"MIN01 intro-retired control differs from ordinary MIN01 by {retiredControlDifferences} byte value(s), expected exactly the two changed bytes inside the four-byte class 0x0125 dispatcher pointer.");
        bool retiredLogicalReadback =
            NativeTerrainTextureGlobalRepackerResearch
                .TryVerifyCandidateLogicalReadback(
                    retiredImagePath,
                    level,
                    expectedPackedRows,
                    sourceImagePath,
                    out string retiredLogicalFailure);
        Assert(
            retiredLogicalReadback,
            $"MIN01 texture logical readback failed after retiring the T0 handler: {retiredLogicalFailure}");

        await Task.Run(() =>
            File.Copy(
                retiredImagePath,
                retiredFastImagePath,
                overwrite: true));
        await File.WriteAllTextAsync(
            retiredFastCuePath,
            BuildCueTextForImage(
                retiredCuePath,
                Path.GetFileName(retiredFastImagePath)),
            Encoding.ASCII);
        MobySourcePatch retiredFastEntryPatch =
            TestLevelWarpPatch.ApplyToDisposableDiagnosticImage(
                retiredFastImagePath,
                level);
        Assert(
            TestLevelWarpPatch.TryGetTargetLevelId(
                retiredFastEntryPatch,
                out int retiredFastTarget) &&
            retiredFastTarget == level.LevelId,
            "MIN01 intro-retired fast-entry clone lost its exact Tree Tops target.");
        int retiredFastControlDifferences =
            CountByteDifferences(
                retiredImagePath,
                retiredFastImagePath);
        int retiredVsOrdinaryFastDifferences =
            CountByteDifferences(
                fastEntryOutputImagePath,
                retiredFastImagePath);
        Assert(
            retiredFastControlDifferences == 8 &&
            retiredVsOrdinaryFastDifferences == 2,
            "MIN01 intro-retired fast candidate did not isolate exactly the eight-byte fast-entry patch plus the two changed byte values inside the four-byte T0 dispatcher word.");
        bool retiredFastLogicalReadback =
            NativeTerrainTextureGlobalRepackerResearch
                .TryVerifyCandidateLogicalReadback(
                    retiredFastImagePath,
                    level,
                    expectedPackedRows,
                    sourceImagePath,
                    out string retiredFastLogicalFailure);
        Assert(
            retiredFastLogicalReadback,
            $"MIN01 texture logical readback failed after the guarded T0 retirement and fast-entry patches: {retiredFastLogicalFailure}");
        introHandlerDiscriminator =
            new IntroHandlerDiscriminatorProof(
                ActorTrueIndex:
                    TreeTopsOpeningGemThiefDiagnosticPatch
                        .OpeningActorTrueIndex,
                ActorClassId:
                    $"0x{TreeTopsOpeningGemThiefDiagnosticPatch.OpeningActorClassId:X4}",
                AdjacentActorTrueIndex: 78,
                AdjacentActorClassId:
                    $"0x{TreeTopsOpeningGemThiefDiagnosticPatch.AdjacentActorClassId:X4}",
                NativeHandlerRuntimeAddress:
                    $"0x{TreeTopsOpeningGemThiefDiagnosticPatch.NativeHandlerRuntimeAddress:X8}",
                DefaultIterationRuntimeAddress:
                    $"0x{TreeTopsOpeningGemThiefDiagnosticPatch.DefaultIterationRuntimeAddress:X8}",
                PatchImageOffset: retirementPatch.ImageOffset,
                ControlImagePath: retiredImagePath,
                ControlCuePath: retiredCuePath,
                ControlSha256: Sha256File(retiredImagePath),
                OrdinaryMin01ControlByteDifferences:
                    retiredControlDifferences,
                FastEntryImagePath: retiredFastImagePath,
                FastEntryCuePath: retiredFastCuePath,
                FastEntrySha256:
                    Sha256File(retiredFastImagePath),
                FastEntryControlByteDifferences:
                    retiredFastControlDifferences,
                OrdinaryMin01FastByteDifferences:
                    retiredVsOrdinaryFastDifferences,
                LogicalReadbackVerified:
                    retiredLogicalReadback &&
                    retiredFastLogicalReadback,
                NormalReleaseAuthorized: false,
                Description:
                    "The exact USA Tree Tops overlay contains one source actor at T0/class 0x0125. Its dispatch entry alone changes from the native route/cinematic handler to the dispatcher's normal next-actor path. T0's row, properties, route, and the adjacent T78/class 0x0126 dispatch remain byte-identical.");
    }

    diagnostics.Add(new DiagnosticProof(
        scenario.Id,
        scenario.OutputName,
        scenario.Description,
        scenario.IncludeExistingNativeEdits
            ? ExistingNativePrivateRows
            : 0,
        scenario.IncludeAppendedRows
            ? scenario.InstalledAppendedRows
            : 0,
        scenario.IncludeExistingNativeAssignments
            ? ExistingNativePrivateRows
            : 0,
        scenario.AssignedAppendedIndices,
        assignedFaceIndices,
        expectedAssignmentCount,
        finalTextureCount,
        writerKind,
        structuralPolicy,
        sectorGrowthBytes,
        NormalReleaseAuthorized: false,
        normalGateReason,
        outputImagePath,
        outputCuePath,
        outputImageSha256,
        fastEntryEnabled,
        fastEntryInstructions,
        fastEntryImageOffset,
        fastEntryOutputImagePath,
        fastEntryOutputCuePath,
        fastEntryOutputImageSha256,
        fastEntryControlByteDifferences,
        fastEntryLogicalReadback,
        knownDormantBaselineByteDifferences,
        sourcePreserved,
        exactFinalReadback,
        logicalReadback));
}

if (exportFinalBins && minimalFaceDiagnostics)
{
    Assert(
        introHandlerDiscriminator != null,
        "Tree Tops MIN01 intro-handler discriminator was not emitted.");
}

Assert(
    FileLength(sourceImagePath) == sourceLength &&
    File.GetLastWriteTimeUtc(sourcePhysicalPath) == sourceWriteTime &&
    string.Equals(
        Sha256File(sourceImagePath),
        sourceSha256,
        StringComparison.OrdinalIgnoreCase),
    "Tree Tops diagnostics changed the immutable retail source.");

await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status = exportFinalBins
                ? $"{scenarios.Length} DUCKSTATION DIAGNOSTIC CANDIDATES; exact final-BIN and fast-entry-clone readback passed; runtime pending"
                : $"{scenarios.Length} DUCKSTATION DIAGNOSTIC PLANS; final BINs not written",
            Source = new
            {
                Path = sourceImagePath,
                PhysicalPath = sourcePhysicalPath,
                Sha256 = sourceSha256,
                Length = sourceLength,
                Preserved = true
            },
            Target = new
            {
                level.Key,
                level.DisplayName,
                level.SourceWadEntry,
                NativeTextureCount =
                    binding.ExpectedSourceTextureCount,
                binding.ExpectedTextureComponentSha256,
                binding.ExpectedLevelDataSha256,
                FaceUnreferencedAnimationSourceIds =
                    unreferencedIds,
                StableDonorIds = stableDonors,
                RuntimeControlledTextureIds =
                    runtime.ControlledTextureIds,
                RuntimeAnimationSourceTextureIds =
                    runtime.AnimationSourceTextureIds,
                RuntimeStaticMaterialTemplateIds =
                    safeAppendedFaceReferences
                        .Select(face => face.OriginalTextureId)
                        .ToArray()
            },
            ExportRequested = exportFinalBins,
            KnownLoadingDormantBaseline = new
            {
                Path = knownLoadingDormantBaselinePath,
                Present = File.Exists(
                    knownLoadingDormantBaselinePath),
                Sha256 = File.Exists(
                    knownLoadingDormantBaselinePath)
                    ? Sha256File(
                        knownLoadingDormantBaselinePath)
                    : ""
            },
            OpeningActorSourcePreimage = new
            {
                UniqueClass0125Count = class0125Rows.Length,
                TrueIndex =
                    class0125Rows[0]
                        .GetProperty("trueIndex")
                        .GetInt32(),
                PropertiesPointer =
                    class0125Rows[0]
                        .GetProperty("propertiesPointer")
                        .GetString(),
                AdjacentClass0126Count = class0126Rows.Length,
                AdjacentTrueIndex =
                    class0126Rows[0]
                        .GetProperty("trueIndex")
                        .GetInt32()
            },
            Diagnostics = diagnostics,
            IntroHandlerDiscriminator =
                introHandlerDiscriminator,
            Conclusions = minimalFaceDiagnostics
                ? new[]
                {
                    "DORMANT46-KNOWN regenerates the prior runtime-loading 46-appended-plus-four-native structure byte-for-byte and adds only the separately guarded eight-byte fast-entry clone.",
                    "DORMANT47 exactly isolates SAFE47 structural count and packing with zero visible appended face assignments.",
                    "MIN01 changes only the texture-ID byte of exact face 1:32:hp from native T12 to appended T81 against the known-loading dormant structural baseline; T81 is byte-identical to T12 in that baseline.",
                    "MIN01-INTRO retires only the unique T0/class 0x0125 update dispatch: its four-byte pointer word changes from handler 0x80082F30 to the dispatcher's native next-actor path 0x80086AEC (two byte values differ). T0's source row/properties/route and T78/class 0x0126 remain unchanged.",
                    "MIN01-NEAR changes only near-entry face 207:9:hp from native T12 to appended T81, preventing a false pass caused by the far MIN01 anchor not being rendered during entry.",
                    "MIN-T82-SINGLE changes the same exact face 1:32:hp from native T12 to appended T82, isolating T81/start-slot behavior from general appended-index visibility.",
                    "MIN02 through MIN32 cumulatively double the number of appended face references while retaining the same structural transaction, allowing the first failing range to be bracketed.",
                    "Each fast-entry BIN is a preserved control clone plus exactly the guarded eight executable bytes; its CUE explicitly names the cloned BIN and texture logical readback is repeated after the warp patch.",
                    "Every output is a DuckStation diagnostic only. Tree Tops and normal Beta/Create BIN promotion remain blocked."
                }
                : new[]
                {
                    "SAFE is the primary editor-capacity retest: all 47 remaining seven-bit IDs T81-T127 are appended, assigned, and use face-referenced material templates outside runtime-controlled destinations and animation sources.",
                    "A, B, C, and D retain the exact known-loading dormant structural transaction: four face-unreferenced animation-source row overrides plus 46 appended rows with the sequential 0-45 material-template contract.",
                    "A differs from the dormant baseline only by all 46 appended face texture-ID assignments. B differs only by the four T3/T5/T9/T11 face assignments.",
                    "C and D differ from the dormant baseline only by disjoint 23-face appended assignment halves.",
                    "Every face assignment is an exact source-bound four-byte HP texture-ID patch; every structural candidate uses final-BIN and immutable-source logical readback.",
                    "All five outputs are DuckStation diagnostics only. Normal Beta/Create BIN promotion remains false."
                }
        },
        new JsonSerializerOptions { WriteIndented = true }) +
    Environment.NewLine);

if (minimalFaceDiagnostics)
{
    string fastSelection =
        TestLevelWarpPatch.TargetSelectionText(level.LevelId);
    string checklist = $"""
        # Tree Tops minimal appended-face runtime checklist

        These are disposable clean-USA diagnostics. Tree Tops remains unpromoted.

        ## Opening-handler discriminator

        1. Cold-boot `treetops-KNOWN-LOADING-46-plus-4-DORMANT-CONTROL-FAST-ENTRY.cue`.
        2. Start a new game and reach a point where Inventory opens.
        3. In Inventory, enter `{TestLevelWarpPatch.ActivationSequence}`.
        4. Press `{fastSelection}` to load Tree Tops.
        5. Confirm this exact regeneration of the previously loading dormant baseline reaches Tree Tops.
        6. Cold-boot `treetops-MIN-01-cumulative-visible-faces-T81-DIAGNOSTIC-FAST-ENTRY.cue` and repeat the same entry.
        7. If MIN01 freezes, cold-boot `treetops-MIN-01-T81-INTRO-GEM-THIEF-T0-HANDLER-RETIRED-DIAGNOSTIC-FAST-ENTRY.cue`.
        8. Report whether normal MIN01 and handler-retired MIN01 each load or freeze. If only the handler-retired candidate loads, the opening gem-thief handler is implicated; if both freeze, the visible appended face remains implicated.

        The handler-retired candidate changes exactly one four-byte pointer word relative to ordinary MIN01; two byte values differ (`30 2F 08 80` -> `EC 6A 08 80`). It changes only the unique T0/class `0x0125` dispatcher from native handler `0x80082F30` to the dispatcher's existing next-actor path `0x80086AEC`. T0's row, properties, route, and the adjacent T78/class `0x0126` handler entry remain untouched.

        ## Cumulative texture test

        If ordinary MIN01 loads, test MIN01-NEAR next, then MIN02, MIN04, MIN08, MIN16, and MIN32. Stop at the first failure.

        MIN01 changes exactly one terrain texture-ID byte: exact face `1:32:hp`, native T12 -> appended T81. Its unpatched control BIN differs from the known-loading dormant structural baseline by exactly one byte. The `-FAST-ENTRY` BIN differs from that control by exactly eight guarded executable bytes.

        `treetops-MIN-01-NEAR-ENTRY-face-207-9-hp-T81-DIAGNOSTIC-FAST-ENTRY.cue` changes exactly one near-entry face `207:9:hp`, native T12 -> appended T81. It is about 603 world units from the entry pose, so a successful load cannot be explained by the edited face remaining outside the rendered entry neighborhood.

        ## Same-face T82 discriminator

        `treetops-MIN-T82-SINGLE-same-face-1-32-hp-DIAGNOSTIC-FAST-ENTRY.cue` changes the same exact face `1:32:hp` from native T12 -> appended T82, with T81 remaining dormant. Test it after MIN01, or immediately if MIN01 freezes, to distinguish a T81/start-slot problem from general appended-index visibility.

        ## Structural control

        `treetops-DORMANT-47-appended-zero-visible-faces-STRUCTURAL-CONTROL-FAST-ENTRY.cue` installs the same 47 appended rows as the failed SAFE47 candidate but assigns none to terrain. Test it if MIN01 fails to distinguish table/packing growth from visible appended-index lookup.

        ## Retail fallback level-select code

        Without the diagnostic fast-entry patch, pause the retail game and enter:

        `Square, Square, Circle, Square, Left, Right, Left, Right, Circle, Up, Right, Down`

        Do not use an old per-level save state for these cold-load checks.
        """;
    await File.WriteAllTextAsync(
        checklistPath,
        checklist + Environment.NewLine);
}

Console.WriteLine("Tree Tops private-texture load-freeze diagnostic smoke passed.");
foreach (DiagnosticProof proof in diagnostics)
{
    Console.WriteLine(
        $"- {proof.Id}: existing {proof.ExistingNativeRows}, appended " +
        $"{proof.AppendedRows}, native-face assignments " +
        $"{proof.ExistingNativeFaceAssignments}, visible faces " +
        $"{proof.FaceAssignmentCount}, " +
        $"+0x{proof.SectorGrowthBytes:X}, dormant-baseline delta " +
        $"{proof.KnownDormantBaselineByteDifferences?.ToString() ?? "n/a"}, " +
        $"SHA {proof.OutputImageSha256}.");
    if (exportFinalBins)
    {
        Console.WriteLine($"  Control CUE: {proof.OutputCuePath}");
        Console.WriteLine(
            $"  Fast-entry CUE: {proof.FastEntryOutputCuePath}");
        Console.WriteLine(
            $"  Fast-entry SHA: {proof.FastEntryOutputImageSha256}; " +
            $"control delta {proof.FastEntryControlByteDifferences}.");
    }
}
if (introHandlerDiscriminator != null)
{
    Console.WriteLine(
        "- Intro-handler retired MIN01 fast-entry CUE: " +
        introHandlerDiscriminator.FastEntryCuePath);
    Console.WriteLine(
        "  Fast-entry SHA: " +
        introHandlerDiscriminator.FastEntrySha256 +
        "; ordinary MIN01 fast delta " +
        introHandlerDiscriminator
            .OrdinaryMin01FastByteDifferences +
        ".");
}
Console.WriteLine($"- Proof: {proofPath}");
if (minimalFaceDiagnostics)
    Console.WriteLine($"- Checklist: {checklistPath}");
Console.WriteLine("- Normal Beta/Create BIN promotion remains false.");

static DiagnosticScenario BuildDormant47Scenario() =>
    new(
        "DORMANT47",
        "treetops-DORMANT-47-appended-zero-visible-faces-STRUCTURAL-CONTROL",
        "47-row structural control matching SAFE47 packing and material contracts, with zero terrain faces assigned to appended IDs.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: false,
        IncludeAppendedRows: true,
        InstalledAppendedRows: 47,
        AssignedAppendedIndices: [],
        UseLegacySequentialMaterialTemplates: false,
        UseSafeStaticFaceSet: true);

static DiagnosticScenario BuildKnownLoadingDormant46Scenario() =>
    new(
        "DORMANT46-KNOWN",
        "treetops-KNOWN-LOADING-46-plus-4-DORMANT-CONTROL",
        "Exact regeneration of the previously runtime-loading Tree Tops structure: four native row overrides plus 46 appended rows, with zero terrain faces assigned to those private IDs.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: 46,
        AssignedAppendedIndices: [],
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: true);

static DiagnosticScenario BuildMinimalScenario(int faceCount)
{
    if (faceCount is < 1 or > 46)
    {
        throw new ArgumentOutOfRangeException(
            nameof(faceCount),
            faceCount,
            "Minimal Tree Tops face count must be between 1 and 46.");
    }

    string targetRange = faceCount == 1
        ? "T81"
        : $"T81-T{80 + faceCount}";
    return new DiagnosticScenario(
        $"MIN{faceCount:00}",
        $"treetops-MIN-{faceCount:00}-cumulative-visible-faces-{targetRange}-DIAGNOSTIC",
        faceCount == 1
            ? "Single-byte proof: exact face 1:32:hp changes from native T12 to appended T81 while T81 remains byte-identical to T12 in the known-loading dormant structural baseline."
            : $"Cumulative binary-escalation proof: the first {faceCount} exact runtime-static faces change to appended IDs {targetRange}; every other byte retains the known-loading dormant structural baseline.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: 46,
        AssignedAppendedIndices: Enumerable.Range(
            0,
            faceCount).ToArray(),
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: true);
}

static DiagnosticScenario BuildSameAnchorT82Scenario() =>
    new(
        "MIN-T82-SINGLE",
        "treetops-MIN-T82-SINGLE-same-face-1-32-hp-DIAGNOSTIC",
        "Single-byte discriminator: exact face 1:32:hp changes from native T12 to appended T82 while T81 remains dormant, isolating the first appended slot from general appended-index visibility.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: 46,
        AssignedAppendedIndices: [1],
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: true,
        AssignedFaceIndices: [0]);

static DiagnosticScenario BuildNearEntryT81Scenario(
    int nearEntryAnchorIndex) =>
    new(
        "MIN01-NEAR",
        "treetops-MIN-01-NEAR-ENTRY-face-207-9-hp-T81-DIAGNOSTIC",
        "Single-byte near-entry visibility proof: exact face 207:9:hp changes from native T12 to appended T81 while every other byte retains the known-loading dormant structural baseline.",
        IncludeExistingNativeAssignments: false,
        IncludeExistingNativeEdits: true,
        IncludeAppendedRows: true,
        InstalledAppendedRows: 46,
        AssignedAppendedIndices: [0],
        UseLegacySequentialMaterialTemplates: true,
        UseSafeStaticFaceSet: true,
        AssignedFaceIndices: [nearEntryAnchorIndex]);

static TerrainPolygon RequireFace(
    IReadOnlyDictionary<string, TerrainPolygon> facesByKey,
    FaceReference reference)
{
    Assert(
        facesByKey.TryGetValue(reference.RuntimeKey, out TerrainPolygon? face),
        $"Missing exact face {reference.RuntimeKey}.");
    TerrainPolygon result = face
        ?? throw new InvalidOperationException(
            $"Missing exact face {reference.RuntimeKey}.");
    Assert(
        result.OriginalTextureId == reference.OriginalTextureId &&
        result.SectorIndex == reference.SectorIndex &&
        result.FaceIndex == reference.FaceIndex,
        $"Exact face preimage changed for {reference.RuntimeKey}.");
    return result;
}

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
        "2026-07-27T20:00:00Z",
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
        "2026-07-27T20:00:00Z",
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

static void AssertExpectedTerrainSkips(
    TerrainPatchPlan plan,
    string scenarioId)
{
    Assert(
        plan.SkippedEdits.All(item =>
            item.Equals(
                "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
                StringComparison.Ordinal)),
        $"{scenarioId}: terrain planner reported an unexpected skip: " +
        $"{string.Join(" | ", plan.SkippedEdits)}");
}

static void AssertGlobalProof(
    NativeTerrainTextureSectorPrivateRecordPlan sectorPlan,
    string scenarioId)
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
        $"{scenarioId}: global packer omitted an indexed-pixel, palette, alias, protected-storage, fixed-row, or material proof.");
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

static string BuildCueTextForImage(
    string sourceCuePath,
    string outputBinFileName)
{
    string[] lines = File.ReadAllLines(sourceCuePath);
    int fileLineCount = 0;
    for (int index = 0; index < lines.Length; index++)
    {
        if (!lines[index]
                .TrimStart()
                .StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        lines[index] = $"FILE \"{outputBinFileName}\" BINARY";
        fileLineCount++;
    }

    Assert(
        fileLineCount == 1,
        $"Expected exactly one FILE line in source CUE {sourceCuePath}, found {fileLineCount}.");
    return string.Join(Environment.NewLine, lines) +
        Environment.NewLine;
}

static int CountByteDifferences(
    string firstPath,
    string secondPath)
{
    using FileStream first = File.OpenRead(firstPath);
    using FileStream second = File.OpenRead(secondPath);
    Assert(
        first.Length == second.Length,
        $"Cannot compare differently sized BINs: {first.Length} and {second.Length} bytes.");
    byte[] firstBuffer = new byte[1024 * 1024];
    byte[] secondBuffer = new byte[firstBuffer.Length];
    int differences = 0;
    while (true)
    {
        int firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
        int secondRead = second.Read(
            secondBuffer,
            0,
            secondBuffer.Length);
        Assert(
            firstRead == secondRead,
            "BIN comparison streams returned different read lengths.");
        if (firstRead == 0)
            return differences;
        for (int index = 0; index < firstRead; index++)
        {
            if (firstBuffer[index] != secondBuffer[index])
                differences++;
        }
    }
}

static string FormatIds(IEnumerable<int> ids) =>
    string.Join(", ", ids.Select(textureId => $"T{textureId}"));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FaceReference(
    string RuntimeKey,
    int OriginalTextureId,
    int SectorIndex,
    int FaceIndex);

internal sealed record DiagnosticScenario(
    string Id,
    string OutputName,
    string Description,
    bool IncludeExistingNativeAssignments,
    bool IncludeExistingNativeEdits,
    bool IncludeAppendedRows,
    int InstalledAppendedRows,
    int[] AssignedAppendedIndices,
    bool UseLegacySequentialMaterialTemplates,
    bool UseSafeStaticFaceSet,
    int[]? AssignedFaceIndices = null);

internal sealed record DiagnosticProof(
    string Id,
    string OutputName,
    string Description,
    int ExistingNativeRows,
    int AppendedRows,
    int ExistingNativeFaceAssignments,
    int[] AssignedAppendedIndices,
    int[] AssignedFaceIndices,
    int FaceAssignmentCount,
    int FinalTextureCount,
    string WriterKind,
    string StructuralGrowthPolicy,
    int SectorGrowthBytes,
    bool NormalReleaseAuthorized,
    string NormalGateReason,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool FastEntryEnabled,
    string FastEntryInstructions,
    string FastEntryImageOffset,
    string FastEntryOutputImagePath,
    string FastEntryOutputCuePath,
    string FastEntryOutputImageSha256,
    int? FastEntryControlByteDifferences,
    bool FastEntryLogicalReadbackVerified,
    int? KnownDormantBaselineByteDifferences,
    bool SourcePreserved,
    bool ExactFinalBinReadbackVerified,
    bool LogicalReadbackVerified);

internal sealed record IntroHandlerDiscriminatorProof(
    int ActorTrueIndex,
    string ActorClassId,
    int AdjacentActorTrueIndex,
    string AdjacentActorClassId,
    string NativeHandlerRuntimeAddress,
    string DefaultIterationRuntimeAddress,
    string PatchImageOffset,
    string ControlImagePath,
    string ControlCuePath,
    string ControlSha256,
    int OrdinaryMin01ControlByteDifferences,
    string FastEntryImagePath,
    string FastEntryCuePath,
    string FastEntrySha256,
    int FastEntryControlByteDifferences,
    int OrdinaryMin01FastByteDifferences,
    bool LogicalReadbackVerified,
    bool NormalReleaseAuthorized,
    string Description);
