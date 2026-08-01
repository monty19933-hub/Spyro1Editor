using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "private-texture-batch-compiler");
Directory.CreateDirectory(outputRoot);
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
await WadAnalysisBuilder.BuildAsync(sourceImagePath, wadAnalysisPath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is absent from the level catalog.");
LevelDefinition stoneHill = catalog.FindByKey("stonehill")
    ?? throw new InvalidOperationException("Stone Hill is absent from the level catalog.");
LevelDefinition darkHollow = catalog.FindByKey("darkhollow")
    ?? throw new InvalidOperationException("Dark Hollow is absent from the level catalog.");
LevelDefinition magicCrafters = catalog.FindByKey("magiccrafters")
    ?? throw new InvalidOperationException("Magic Crafters is absent from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is absent from the level catalog.");

NativeTerrainTextureRecordAppendSourceBinding artisansBinding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, artisans);
TerrainPatch ordinaryTerrainPatch = new(
    "selected face texture id",
    "texture-id-word3",
    "95:3:hp",
    "0x1234",
    "",
    4,
    "37022001",
    "44022001",
    "Set one face to its appended-private texture id.");
TerrainPatchPlan ordinaryTerrainPlan = BuildTerrainPlan([ordinaryTerrainPatch]);
NativeTerrainTextureRecordExistingPatch convertedOrdinary =
    NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(ordinaryTerrainPlan).Single();
Assert(convertedOrdinary.WadOffset == 0x1234 &&
       convertedOrdinary.Before.SequenceEqual(Convert.FromHexString("37022001")) &&
       convertedOrdinary.After.SequenceEqual(Convert.FromHexString("44022001")),
    "The ordinary-terrain bridge did not retain the retail WAD offset and complete before/after bytes.");
bool legacyWriterRejected = false;
try
{
    NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
        BuildTerrainPlan(
            [ordinaryTerrainPatch with { Kind = "native-terrain-texture-page", RuntimeKey = "legacy-writer" }],
            nativeRelocationPatchCount: 1));
}
catch (InvalidOperationException)
{
    legacyWriterRejected = true;
}
Assert(legacyWriterRejected,
    "The ordinary-terrain bridge accepted a second legacy native-texture page writer.");
NativeTerrainTextureRelocationEdit artisansPrivate = BuildEdit(
    artisansBinding,
    gnastysWorld,
    donorTextureId: 17,
    materialTemplateTextureId: 55,
    targetTextureId: artisansBinding.ExpectedSourceTextureCount);
string appendedManifestPath = Path.Combine(outputRoot, "artisans-appended-private-manifest.json");
await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
    appendedManifestPath,
    artisans.Key,
    artisans.DisplayName,
    [artisansPrivate]);
TerrainPatchPlan forbiddenLegacyPlan = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    Path.Combine(outputRoot, "legacy-not-written.bin"),
    Path.Combine(outputRoot, "legacy-not-written.cue"),
    artisans,
    "",
    "",
    Path.Combine(outputRoot, "no-terrain-edits.json"),
    nativeTextureRelocationsPath: appendedManifestPath);
Assert(forbiddenLegacyPlan.PatchCount == 0 &&
       forbiddenLegacyPlan.SkippedEdits.Any(item =>
           item.Contains("appended-private", StringComparison.OrdinalIgnoreCase) &&
           item.Contains("exclusive private-record batch writer", StringComparison.OrdinalIgnoreCase)),
    "The legacy terrain exporter did not fail closed when it discovered an appended-private record.");
NativeTerrainTexturePrivateRecordBatchRequest artisansRequest = new(
    sourceImagePath,
    sourceCuePath,
    Path.Combine(outputRoot, "artisans-not-exported"),
    wadAnalysisPath,
    artisans,
    [artisansPrivate],
    OrdinaryLevelDataPatches: []);

Assert(!NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
        artisansRequest with { HasCustomImageTexturePatches = true },
        out _,
        out string customFailure) &&
       customFailure.Contains("custom/imported-image", StringComparison.OrdinalIgnoreCase),
    "The batch compiler accepted a competing custom-image texture-page writer.");
Assert(!NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
        artisansRequest with
        {
            SavedEdits = [artisansPrivate with { DescriptorTier = "normal" }]
        },
        out _,
        out string descriptorFailure) &&
       descriptorFailure.Contains("descriptor tier", StringComparison.OrdinalIgnoreCase) &&
       descriptorFailure.Contains("both", StringComparison.OrdinalIgnoreCase),
    "The batch compiler silently reinterpreted a partial/noncanonical descriptor tier as a complete record.");
Assert(!NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
        artisansRequest with
        {
            SavedEdits =
            [
                artisansPrivate with
                {
                    DonorRuntimeKey = NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
                        gnastysWorld.Key,
                        artisansPrivate.DonorTextureId + 1)
                }
            ]
        },
        out _,
        out string provenanceFailure) &&
       provenanceFailure.Contains("provenance", StringComparison.OrdinalIgnoreCase) &&
       provenanceFailure.Contains("expected", StringComparison.OrdinalIgnoreCase),
    "The batch compiler silently accepted a donor runtime/provenance key bound to another texture.");
Assert(!NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
        artisansRequest with
        {
            SavedEdits = [artisansPrivate with { SourceLevelDataSha256 = new string('0', 64) }]
        },
        out _,
        out string staleFailure) &&
       staleFailure.Contains("stale", StringComparison.OrdinalIgnoreCase),
    "The batch compiler accepted a stale saved level-data preimage.");
Assert(!NativeTerrainTexturePrivateRecordBatchCompiler.TryBuild(
        artisansRequest with
        {
            SavedEdits =
            [
                artisansPrivate,
                artisansPrivate with
                {
                    TargetTextureId = artisansPrivate.TargetTextureId + 1,
                    PrivateRecordEditId = NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(
                        artisansPrivate.TargetTextureId + 1)
                }
            ]
        },
        out _,
        out string duplicateFailure) &&
       duplicateFailure.Contains("same donor/material contract", StringComparison.OrdinalIgnoreCase),
    "The batch compiler accepted two physical rows for one reusable donor/material contract.");

NativeTerrainTexturePrivateRecordBatchPlan artisansPlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(artisansRequest);
Assert(artisansPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail &&
       artisansPlan.FixedTailPlan != null &&
       artisansPlan.SectorRelocationPlan == null &&
       artisansPlan.ExclusiveImageWriterSelected &&
       artisansPlan.AppendedPrivateEdits is [{ TargetTextureId: 68 }],
    "Artisans did not select exactly one fixed-tail private texture writer.");

// Exercise the actual batch writer with two distinct donor levels/rows, not
// merely the store allocator. The final BIN must retain both contiguous rows
// and pass logical readback against the immutable retail donor image.
NativeTerrainTextureRelocationEdit artisansSecondPrivate = BuildEdit(
    artisansBinding,
    darkHollow,
    donorTextureId: 8,
    materialTemplateTextureId: 54,
    targetTextureId: artisansBinding.ExpectedSourceTextureCount + 1);
string twoDonorSourceSha256 = Sha256File(sourceImagePath);
NativeTerrainTexturePrivateRecordBatchPlan twoDonorPlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(artisansRequest with
    {
        OutputPrefix = Path.Combine(outputRoot, "artisans-two-donor-final-bin"),
        SavedEdits = [artisansPrivate, artisansSecondPrivate]
    });
Assert(twoDonorPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail &&
       twoDonorPlan.AppendedPrivateEdits.Select(edit => edit.TargetTextureId)
           .SequenceEqual([
               artisansBinding.ExpectedSourceTextureCount,
               artisansBinding.ExpectedSourceTextureCount + 1
           ]) &&
       twoDonorPlan.SyntheticRecords.Select(record => record.DonorWadEntry).Distinct().Count() == 2,
    "The two-donor compiler plan did not retain two distinct contiguous donor rows.");
NativeTerrainTexturePrivateRecordBatchExportResult twoDonorExport =
    await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(twoDonorPlan);
Assert(twoDonorExport.SourceImagePreserved &&
       twoDonorExport.ExactReadbackVerified &&
       twoDonorExport.RuntimeTargetReadbackVerified &&
       twoDonorExport.GlobalLogicalReadbackVerified &&
       twoDonorExport.AtomicRenameCompleted &&
       File.Exists(twoDonorExport.OutputImagePath) &&
       File.Exists(twoDonorExport.OutputCuePath),
    "The two-donor final-BIN compiler export omitted a required source/readback/atomic-write proof.");
NativeTerrainTextureGlobalRepackPackedRecord[] expectedSyntheticRows =
    twoDonorPlan.FixedTailPlan!.GlobalPacking.SyntheticRecords
        .OrderBy(record => record.TargetTextureId)
        .ToArray();
string twoDonorLogicalFailure = "";
bool twoDonorLogicalReadback = expectedSyntheticRows.Length == 2 &&
    NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        twoDonorExport.OutputImagePath,
        artisans,
        expectedSyntheticRows,
        sourceImagePath,
        out twoDonorLogicalFailure);
Assert(expectedSyntheticRows.Length == 2 &&
       twoDonorLogicalReadback,
    $"The two-donor final BIN failed explicit immutable-donor logical readback: {twoDonorLogicalFailure}");
IReadOnlyList<TerrainTextureSlot> twoDonorSlots = TerrainPatchExporter.InspectTextureSlots(
    twoDonorExport.OutputImagePath,
    artisans);
Assert(twoDonorSlots.Count == artisansBinding.ExpectedSourceTextureCount + 2 &&
       twoDonorSlots[^2].TextureId == artisansBinding.ExpectedSourceTextureCount &&
       twoDonorSlots[^1].TextureId == artisansBinding.ExpectedSourceTextureCount + 1 &&
       string.Equals(Sha256File(sourceImagePath), twoDonorSourceSha256, StringComparison.OrdinalIgnoreCase),
    "The final two-donor table was not contiguous or the immutable retail source changed.");

NativeTerrainTextureRecordAppendSourceBinding stoneBinding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, stoneHill);
NativeTerrainTextureRelocationEdit stonePrivate = BuildEdit(
    stoneBinding,
    gnastysWorld,
    donorTextureId: 17,
    materialTemplateTextureId: 11,
    targetTextureId: stoneBinding.ExpectedSourceTextureCount);
NativeTerrainTexturePrivateRecordBatchPlan stonePlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(new NativeTerrainTexturePrivateRecordBatchRequest(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(outputRoot, "stone-hill-not-exported"),
        wadAnalysisPath,
        stoneHill,
        [stonePrivate],
        OrdinaryLevelDataPatches: []));
Assert(stonePlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
       stonePlan.FixedTailPlan == null &&
       stonePlan.SectorRelocationPlan != null &&
       stonePlan.ExclusiveImageWriterSelected &&
       stonePlan.SectorRelocationPlan.StructuralSectorGrowthVerified,
    "Stone Hill did not select exactly one +0x800 sector-relocation private texture writer.");

NativeTerrainTextureRecordAppendSourceBinding darkBinding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, darkHollow);
NativeTerrainTextureRelocationEdit darkPrivate = BuildEdit(
    darkBinding,
    gnastysWorld,
    donorTextureId: 17,
    materialTemplateTextureId: 0,
    targetTextureId: darkBinding.ExpectedSourceTextureCount);
NativeTerrainTexturePrivateRecordBatchPlan darkPlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(new NativeTerrainTexturePrivateRecordBatchRequest(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(outputRoot, "dark-hollow-not-exported"),
        wadAnalysisPath,
        darkHollow,
        [darkPrivate],
        OrdinaryLevelDataPatches: []));
Assert(darkPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
       darkPlan.FixedTailPlan == null &&
       darkPlan.SectorRelocationPlan != null &&
       darkPlan.ExclusiveImageWriterSelected &&
       darkPlan.SectorRelocationPlan.StructuralSectorGrowthVerified,
    "Dark Hollow did not select exactly one +0x800 sector-relocation private texture writer.");

NativeTerrainTextureRecordAppendSourceBinding magicBinding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, magicCrafters);
NativeTerrainTextureRelocationEdit magicPrivate = BuildEdit(
    magicBinding,
    gnastysWorld,
    donorTextureId: 17,
    materialTemplateTextureId: 0,
    targetTextureId: magicBinding.ExpectedSourceTextureCount);
NativeTerrainTexturePrivateRecordBatchPlan magicPlan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(new NativeTerrainTexturePrivateRecordBatchRequest(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(outputRoot, "magic-crafters-not-exported"),
        wadAnalysisPath,
        magicCrafters,
        [magicPrivate],
        OrdinaryLevelDataPatches: []));
Assert(magicPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail &&
       magicPlan.FixedTailPlan != null &&
       magicPlan.SectorRelocationPlan == null,
    "Magic Crafters did not produce the second fixed-tail destination plan required by the multi-level smoke.");

string multiLevelSourceSha256 = Sha256File(sourceImagePath);
NativeTerrainTexturePrivateMultiLevelBatchPlan multiLevelPlan =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "artisans-magic-crafters-two-destination-final-bin"),
            [artisansPlan, magicPlan]));
Assert(multiLevelPlan.FixedTailWritersOnly &&
       multiLevelPlan.AggregateWriterSelected &&
       multiLevelPlan.TextureOnlyWritersVerified &&
       multiLevelPlan.SourceBindingsVerified &&
       multiLevelPlan.DestinationWadEntriesUnique &&
       multiLevelPlan.PatchPreimagesVerified &&
       multiLevelPlan.CombinedPatchesDisjoint &&
       multiLevelPlan.RelocatedPatchesDisjoint &&
       multiLevelPlan.SectorRelocationDestinationCount == 0 &&
       multiLevelPlan.WadGrowthBytes == 0 &&
       multiLevelPlan.ExpandedLevelDataWrites.Count == 0 &&
       multiLevelPlan.RelocatedPatches.Count == multiLevelPlan.CombinedPatches.Count &&
       multiLevelPlan.LevelPlans.Count == 2 &&
       multiLevelPlan.CombinedPatches.Count ==
           artisansPlan.FixedTailPlan!.CombinedPatches.Count + magicPlan.FixedTailPlan!.CombinedPatches.Count,
    "The two-destination fixed-tail compiler did not retain one disjoint, source-bound patch transaction.");
NativeTerrainTexturePrivateMultiLevelBatchExportResult multiLevelExport =
    await NativeTerrainTexturePrivateMultiLevelBatchCompiler.ExportAsync(multiLevelPlan);
IReadOnlyList<TerrainTextureSlot> multiArtisansSlots = TerrainPatchExporter.InspectTextureSlots(
    multiLevelExport.OutputImagePath,
    artisans);
IReadOnlyList<TerrainTextureSlot> multiMagicSlots = TerrainPatchExporter.InspectTextureSlots(
    multiLevelExport.OutputImagePath,
    magicCrafters);
Assert(multiLevelExport.SourceImagePreserved &&
       multiLevelExport.ExactReadbackVerified &&
       multiLevelExport.EveryRuntimeTargetReadbackVerified &&
       multiLevelExport.EveryGlobalLogicalReadbackVerified &&
       multiLevelExport.AtomicRenameCompleted &&
       multiArtisansSlots.Count == artisansBinding.ExpectedSourceTextureCount + 1 &&
       multiMagicSlots.Count == magicBinding.ExpectedSourceTextureCount + 1 &&
       Sha256File(sourceImagePath) == multiLevelSourceSha256,
    "The two-destination final BIN failed exact/runtime/logical readback or modified its immutable source.");

NativeTerrainTexturePrivateMultiLevelBatchPlan mixedPlan =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "stone-magic-mixed-final-bin"),
            [stonePlan, magicPlan]));
NativeTerrainTexturePrivateAggregatePatch relocatedMagicLevelData = mixedPlan.RelocatedPatches.Single(patch =>
    patch.SourceWadOffset == magicPlan.FixedTailPlan!.Append.Patch.WadOffset &&
    patch.ByteLength == magicPlan.FixedTailPlan.Append.Patch.SourceByteLength);
Assert(!mixedPlan.FixedTailWritersOnly &&
       mixedPlan.AggregateWriterSelected &&
       mixedPlan.TextureOnlyWritersVerified &&
       mixedPlan.SectorRelocationDestinationCount == 1 &&
       mixedPlan.WadGrowthBytes == 0x800 &&
       mixedPlan.RelocatedExecutableLba == mixedPlan.OriginalExecutableLba + 1 &&
       mixedPlan.ExpandedLevelDataWrites is [{ TargetLevelKey: "stonehill" }] &&
       relocatedMagicLevelData.RelocatedWadOffset == relocatedMagicLevelData.SourceWadOffset + 0x800,
    "The mixed fixed-tail/+0x800 plan did not apply one aggregate WAD/ISO growth map to the later fixed-tail destination.");
NativeTerrainTexturePrivateMultiLevelBatchExportResult mixedExport =
    await NativeTerrainTexturePrivateMultiLevelBatchCompiler.ExportAsync(mixedPlan);
IReadOnlyList<TerrainTextureSlot> mixedStoneSlots = TerrainPatchExporter.InspectTextureSlots(
    mixedExport.OutputImagePath,
    stoneHill);
IReadOnlyList<TerrainTextureSlot> mixedMagicSlots = TerrainPatchExporter.InspectTextureSlots(
    mixedExport.OutputImagePath,
    magicCrafters);
Assert(mixedExport.SourceImagePreserved &&
       mixedExport.ExactReadbackVerified &&
       mixedExport.EveryRuntimeTargetReadbackVerified &&
       mixedExport.EveryGlobalLogicalReadbackVerified &&
       mixedExport.AtomicRenameCompleted &&
       mixedStoneSlots.Count == stoneBinding.ExpectedSourceTextureCount + 1 &&
       mixedMagicSlots.Count == magicBinding.ExpectedSourceTextureCount + 1 &&
       Sha256File(sourceImagePath) == multiLevelSourceSha256,
    "The mixed fixed-tail/+0x800 aggregate final BIN failed exact/runtime/logical readback.");

NativeTerrainTexturePrivateRecordBatchPlan extendedStonePlan =
    stonePlan with
    {
        StructuralGrowthPolicy =
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended
    };
NativeTerrainTexturePrivateRecordBatchPlan plus2000StonePlan =
    stonePlan with
    {
        StructuralGrowthPolicy =
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000
    };
NativeTerrainTexturePrivateMultiLevelBatchPlan plus2000PolicyPlan =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "normal-plus-runtime-plus2000-not-exported"),
            [plus2000StonePlan, magicPlan],
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000));
Assert(
    plus2000PolicyPlan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000 &&
    plus2000PolicyPlan.LevelPlans.Any(plan =>
        plan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked) &&
    plus2000PolicyPlan.LevelPlans.Any(plan =>
        plan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000),
    "The aggregate compiler did not preserve compatible NormalChecked and RuntimeProvenPlus2000 child policies.");
NativeTerrainTexturePrivateMultiLevelBatchPlan mixedPolicyPlan =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "normal-plus-runtime-extended-not-exported"),
            [extendedStonePlan, magicPlan],
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended));
Assert(
    mixedPolicyPlan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended &&
    mixedPolicyPlan.LevelPlans.Any(plan =>
        plan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked) &&
    mixedPolicyPlan.LevelPlans.Any(plan =>
        plan.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended),
    "The aggregate compiler did not preserve compatible NormalChecked and RuntimeProvenExtended child policies.");
bool extendedChildInNormalAggregateBuilt =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.TryBuild(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "extended-child-normal-aggregate-must-not-write"),
            [extendedStonePlan, magicPlan],
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked),
        out _,
        out string extendedChildInNormalAggregateFailure);
Assert(
    !extendedChildInNormalAggregateBuilt &&
    extendedChildInNormalAggregateFailure.Contains(
        "incompatible",
        StringComparison.OrdinalIgnoreCase),
    "A RuntimeProvenExtended child escaped into a NormalChecked aggregate.");
bool extendedChildInPlus2000AggregateBuilt =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.TryBuild(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "extended-child-plus2000-aggregate-must-not-write"),
            [extendedStonePlan, magicPlan],
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000),
        out _,
        out string extendedChildInPlus2000AggregateFailure);
Assert(
    !extendedChildInPlus2000AggregateBuilt &&
    extendedChildInPlus2000AggregateFailure.Contains(
        "incompatible",
        StringComparison.OrdinalIgnoreCase),
    "A RuntimeProvenExtended child escaped into the smaller RuntimeProvenPlus2000 aggregate.");

NativeTerrainTexturePrivateMultiLevelBatchPlan twoSectorPlan =
    NativeTerrainTexturePrivateMultiLevelBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateMultiLevelBatchRequest(
            sourceImagePath,
            sourceCuePath,
            Path.Combine(outputRoot, "stone-dark-two-sector-final-bin"),
            [stonePlan, darkPlan]));
NativeTerrainTexturePrivateAggregateLevelDataWrite darkExpandedWrite =
    twoSectorPlan.ExpandedLevelDataWrites.Single(write => write.TargetLevelKey == "darkhollow");
Assert(!twoSectorPlan.FixedTailWritersOnly &&
       twoSectorPlan.AggregateWriterSelected &&
       twoSectorPlan.TextureOnlyWritersVerified &&
       twoSectorPlan.SectorRelocationDestinationCount == 2 &&
       twoSectorPlan.WadGrowthBytes == 0x1000 &&
       twoSectorPlan.RelocatedExecutableLba == twoSectorPlan.OriginalExecutableLba + 2 &&
       twoSectorPlan.ExpandedLevelDataWrites.Count == 2 &&
       darkExpandedWrite.RelocatedWadOffset == darkExpandedWrite.SourceWadOffset + 0x800,
    "The two-sector plan did not accumulate two destination growth sectors and rebase the later level once.");
NativeTerrainTexturePrivateMultiLevelBatchExportResult twoSectorExport =
    await NativeTerrainTexturePrivateMultiLevelBatchCompiler.ExportAsync(twoSectorPlan);
IReadOnlyList<TerrainTextureSlot> twoSectorStoneSlots = TerrainPatchExporter.InspectTextureSlots(
    twoSectorExport.OutputImagePath,
    stoneHill);
IReadOnlyList<TerrainTextureSlot> twoSectorDarkSlots = TerrainPatchExporter.InspectTextureSlots(
    twoSectorExport.OutputImagePath,
    darkHollow);
Assert(twoSectorExport.SourceImagePreserved &&
       twoSectorExport.ExactReadbackVerified &&
       twoSectorExport.EveryRuntimeTargetReadbackVerified &&
       twoSectorExport.EveryGlobalLogicalReadbackVerified &&
       twoSectorExport.AtomicRenameCompleted &&
       twoSectorStoneSlots.Count == stoneBinding.ExpectedSourceTextureCount + 1 &&
       twoSectorDarkSlots.Count == darkBinding.ExpectedSourceTextureCount + 1 &&
       Sha256File(sourceImagePath) == multiLevelSourceSha256,
    "The two-sector aggregate final BIN failed exact/runtime/logical readback.");

bool unrelatedWriterBuilt = NativeTerrainTexturePrivateMultiLevelBatchCompiler.TryBuild(
    new NativeTerrainTexturePrivateMultiLevelBatchRequest(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(outputRoot, "unrelated-writer-must-not-write"),
        [stonePlan, magicPlan with
        {
            WriterKind = (NativeTerrainTexturePrivateImageWriterKind)99,
            FixedTailPlan = null
        }]),
    out _,
    out string unrelatedWriterFailure);
Assert(!unrelatedWriterBuilt &&
       unrelatedWriterFailure.Contains("unrelated or unsupported image writer", StringComparison.OrdinalIgnoreCase) &&
       !File.Exists(Path.Combine(outputRoot, "unrelated-writer-must-not-write.bin")),
    "The texture-only aggregate transaction accepted an unrelated writer type.");

Console.WriteLine("Private texture batch compiler smoke passed.");
Console.WriteLine($"- Artisans: {artisansPlan.WriterKind}, private T{artisansPrivate.TargetTextureId}");
Console.WriteLine($"- Stone Hill: {stonePlan.WriterKind}, private T{stonePrivate.TargetTextureId}");
Console.WriteLine($"- Dark Hollow: {darkPlan.WriterKind}, private T{darkPrivate.TargetTextureId}");
Console.WriteLine("- Stale binding, duplicate contract, and custom-image conflict: rejected before export");
Console.WriteLine("- Noncanonical descriptor tier and donor provenance: rejected before reinterpretation");
Console.WriteLine($"- Two-donor exact final BIN: {twoDonorExport.OutputImagePath}");
Console.WriteLine("- Two distinct donor levels/rows passed contiguous-table and immutable-donor logical readback");
Console.WriteLine($"- Two fixed-tail destination levels passed one atomic final BIN: {multiLevelExport.OutputImagePath}");
Console.WriteLine($"- Mixed fixed-tail/+0x800 destinations passed one aggregate final BIN: {mixedExport.OutputImagePath}");
Console.WriteLine("- NormalChecked + RuntimeProvenPlus2000 child policies compose only under RuntimeProvenPlus2000 or the larger extended aggregate");
Console.WriteLine("- NormalChecked + RuntimeProvenExtended child policies compose only under a RuntimeProvenExtended aggregate");
Console.WriteLine($"- Two +0x800 destinations passed one +0x1000 aggregate final BIN: {twoSectorExport.OutputImagePath}");
Console.WriteLine("- Unrelated writer types remain fail-closed outside the texture-only aggregate transaction");
Console.WriteLine("- DuckStation runtime proof remains required");

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
        NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(donor.Key, donorTextureId),
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-21T12:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        binding.TargetWadEntry,
        binding.SourceImageSha256,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        materialTemplateTextureId,
        NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(targetTextureId));

static TerrainPatchPlan BuildTerrainPlan(
    IReadOnlyList<TerrainPatch> patches,
    int customTexturePatchCount = 0,
    int nativeRelocationPatchCount = 0) =>
    new(
        GeneratedAt: DateTimeOffset.UtcNow,
        SourceImagePath: "retail.bin",
        OutputImagePath: "not-written.bin",
        OutputCuePath: "not-written.cue",
        LevelKey: "artisans",
        LevelName: "Artisans",
        RamPath: "",
        SourceSearchPath: "source-search.json",
        TerrainEditsPath: "terrain-edits.json",
        CustomTexturesPath: "",
        NativeTextureRelocationsPath: "",
        TextureAssetWadIndex: 10,
        PatchCount: patches.Count,
        TotalPatchedBytes: patches.Sum(patch => patch.ByteLength),
        CustomTextureImportCount: 0,
        CustomTextureBytePatchCount: customTexturePatchCount,
        CustomTextureImports: [],
        NativeTextureRelocationCount: 0,
        NativeTextureRelocationBytePatchCount: nativeRelocationPatchCount,
        NativeTextureRelocations: [],
        TerrainSideWalls: [],
        Patches: patches,
        SkippedEdits: [],
        Notes: []);

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
